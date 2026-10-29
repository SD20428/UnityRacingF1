#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive
{
    // Only the spawn sets a pose. Every lap afterwards uses real pedals, steering and WheelColliders.
    [DefaultExecutionOrder(-80)]
    public sealed class CornerGuideVerificationProbe : MonoBehaviour
    {
        [Serializable] public sealed class Corner
        {
            public int id,first,last,samples;
            public float targetApexKmh=999,actualApexKmh=-1,maximumSlip,maxRoadOffset,maxLateralG;
        }
        [Serializable] public sealed class Trial
        {
            public string scene,mode,failure="";
            public bool completed,passed;
            public int collisions,frames,straightSamples,cornerSamples;
            public float seconds,maximumSpeed,maxDeviation,maxRoadOffset,maximumBodySlip,offroadSeconds,fullOffroadSeconds;
            public List<Corner> corners=new List<Corner>();
        }
        [Serializable] public sealed class Report {public bool passed;public List<Trial> trials=new List<Trial>();}
        public static string Status="Not run";
        public static string Run(bool qualifying=true,float pace=1,string sceneFilter="",bool qualifyingOnly=false)
        {
            if(!Application.isPlaying || FindFirstObjectByType<CornerGuideVerificationProbe>())throw new InvalidOperationException("Fresh Play mode required");
            var probe=new GameObject("Temporary corner guide verification").AddComponent<CornerGuideVerificationProbe>();
            probe.allModes=qualifying;probe.pace=pace;probe.sceneFilter=sceneFilter;probe.qualifyingOnly=qualifyingOnly;DontDestroyOnLoad(probe.gameObject);
            return "Physical corner laps started across all three circuits";
        }
        readonly Report report=new Report();
        readonly List<string> trace=new List<string>{"scene,mode,seconds,checkpoint,corner,speed,target,guide,brake,throttle,lineDeviation,roadOffset,bodySlip,lateralG"};
        bool allModes=true,running,straightShot,cornerShot;float pace=1,began,liftUntil,nextRecord,roadOutsideTime;
        string sceneFilter="";bool qualifyingOnly;
        int firstFrame;int[] cornerIds;
        RaceManager race;DrivingGuide guide;OverdriveVehicle car;PlayerVehicleInput input;CircuitProgress progress;
        WaypointPath path,centre;CornerSpeedProfile profile;GuideProbeContact contact;Trial trial;
        Vector3 previousVelocity;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        IEnumerator Start()
        {
            Time.timeScale=3;
            foreach(string scene in new[]{"OverdriveF1","OverdriveHarbor","OverdriveYasMarina"})
            for(int mode=qualifyingOnly?1:0;mode<(allModes?2:1);mode++)
            {
                if(sceneFilter!="" && sceneFilter!=scene)continue;
                running=false;
                yield return SceneManager.LoadSceneAsync(scene);
                yield return null;yield return null;
                race=FindFirstObjectByType<RaceManager>();guide=FindFirstObjectByType<DrivingGuide>();
                race.MainMenu();if(mode==1)race.SelectQualifying();
                race.BeginRace();race.enabled=false;
                typeof(RaceManager).GetField("<State>k__BackingField",Private).SetValue(race,RaceState.Racing);
                typeof(RaceManager).GetField("startedAt",Private).SetValue(race,Time.timeAsDouble);
                typeof(RaceManager).GetMethod("ApplyRules",Private).Invoke(race,null);
                foreach(var entry in race.Racers)if(entry.vehicle!=race.player)entry.vehicle.gameObject.SetActive(false);
                car=race.player;input=car.GetComponent<PlayerVehicleInput>();input.enabled=false;
                progress=race.PlayerProgress;path=guide.ActivePath;centre=race.ai.path;
                profile=new CornerSpeedProfile(path,guide.VehicleTuning());guide.RebuildProfile();
                var identity=car.GetComponent<RacerIdentity>();identity.gridCheckpoint=0;identity.startsBeforeLine=false;
                progress.ResetProgress();progress.AutoRecoverBelowTerrain=false;progress.AcceptingCheckpoints=true;
                car.ResetDynamics();car.ControlsEnabled=true;car.Body.constraints=RigidbodyConstraints.None;
                var forward=(path.Position(1)-path.Position(0)).normalized;
                car.Body.position=path.Position(0)+forward*2+Vector3.up*.52f;car.Body.rotation=Quaternion.LookRotation(forward);
                car.transform.SetPositionAndRotation(car.Body.position,car.Body.rotation);car.Body.linearVelocity=car.Body.angularVelocity=Vector3.zero;
                contact=car.gameObject.AddComponent<GuideProbeContact>();
                trial=new Trial{scene=scene,mode=mode==0?"GrandPrix":"Qualifying"};report.trials.Add(trial);
                BuildCorners();firstFrame=Time.frameCount;began=Time.fixedTime;nextRecord=0;liftUntil=0;roadOutsideTime=0;
                previousVelocity=Vector3.zero;straightShot=cornerShot=false;
                Status=scene+" / "+trial.mode+" / physical lap";Physics.SyncTransforms();Time.timeScale=3;running=true;
                while(running)yield return null;
                trial.frames=Time.frameCount-firstFrame;trial.collisions=contact.hits;
                trial.passed=trial.completed && trial.collisions==0 && trial.failure=="" && trial.maximumBodySlip<5 && trial.fullOffroadSeconds==0 && trial.straightSamples>0 && trial.cornerSamples>0;
                WriteReport();car.Throttle=0;car.Brake=1;yield return null;
            }
            report.passed=report.trials.TrueForAll(t=>t.passed);WriteReport();Time.timeScale=1;
            Status=report.passed?"PASS":"FAIL";race.enabled=true;race.MainMenu();
            Debug.Log("Corner guide verification "+Status);Destroy(gameObject);
        }
        void BuildCorners()
        {
            int n=path.points.Length;cornerIds=new int[n];for(int i=0;i<n;i++)cornerIds[i]=-1;
            int start=0;for(int i=0;i<n;i++)if(!profile.IsCorner(i)){start=i;break;}
            Corner current=null;
            for(int step=0;step<n;step++)
            {
                int i=(start+step)%n;if(!profile.IsCorner(i)){current=null;continue;}
                if(current==null){current=new Corner{id=trial.corners.Count+1,first=i};trial.corners.Add(current);}
                current.last=i;current.targetApexKmh=Mathf.Min(current.targetApexKmh,profile.TargetKmh(i,0,0)*pace);cornerIds[i]=current.id-1;
            }
        }
        void WriteReport()
        {
            Directory.CreateDirectory("Docs");
            File.WriteAllText("Docs/CornerGuide_PhysicalLaps.json",JsonUtility.ToJson(report,true));
            File.WriteAllLines("Docs/CornerGuide_PhysicalLaps.csv",trace);
        }
        void FixedUpdate()
        {
            if(!running || !car)return;
            trial.seconds=Time.fixedTime-began;
            int segment=progress.LastCheckpoint;float fraction=path.SegmentFraction(car.Body.position,segment);
            var point=Vector3.Lerp(path.Position(segment),path.Position(segment+1),fraction);
            float deviation=Vector2.Distance(new Vector2(point.x,point.z),new Vector2(car.Body.position.x,car.Body.position.z));
            // Tight bends may cross the next geometric segment before its trigger fires.
            // Measure the nearest local polyline segment, not an extrapolated stale tangent.
            float roadOffset=float.PositiveInfinity;
            for(int j=-3;j<=4;j++)
            {
                int index=(segment+j+centre.points.Length)%centre.points.Length;
                float t=centre.SegmentFraction(car.Body.position,index);
                var p=Vector3.Lerp(centre.Position(index),centre.Position(index+1),t);
                roadOffset=Mathf.Min(roadOffset,Vector2.Distance(new Vector2(p.x,p.z),new Vector2(car.Body.position.x,car.Body.position.z)));
            }
            var localVelocity=car.transform.InverseTransformDirection(car.Body.linearVelocity);
            float slip=car.SpeedKmh>50?Mathf.Abs(Mathf.Atan2(localVelocity.x,Mathf.Max(1,localVelocity.z))*Mathf.Rad2Deg):0;
            float lateralG=Mathf.Abs(car.transform.InverseTransformDirection((car.Body.linearVelocity-previousVelocity)/Time.fixedDeltaTime).x)/9.81f;
            previousVelocity=car.Body.linearVelocity;
            trial.maximumSpeed=Mathf.Max(trial.maximumSpeed,car.SpeedKmh);trial.maximumBodySlip=Mathf.Max(trial.maximumBodySlip,slip);
            trial.maxDeviation=Mathf.Max(trial.maxDeviation,deviation);trial.maxRoadOffset=Mathf.Max(trial.maxRoadOffset,roadOffset);
            int loose=0,grounded=0;
            foreach(var wheel in car.wheels)if(wheel.GetGroundHit(out var ground))
            {grounded++;var surface=ground.collider.GetComponentInParent<TrackSurface>();if(surface && surface.grip<.9f)loose++;}
            if(loose>0)trial.offroadSeconds+=Time.fixedDeltaTime;
            if(grounded==4 && loose==4){roadOutsideTime+=Time.fixedDeltaTime;trial.fullOffroadSeconds+=Time.fixedDeltaTime;}else roadOutsideTime=0;
            if(roadOutsideTime>.15f || deviation>8 || car.transform.position.y<-.5f || trial.seconds>220)
            {trial.failure="Track departure or timeout at checkpoint "+segment;running=false;return;}
            if(progress.CompletedLaps>=1){trial.completed=true;running=false;return;}
            bool show=profile.NeedsGuidance(segment,fraction,car.SpeedKmh,car.SurfaceGrip,out _);
            float desired=profile.TargetKmh(segment,fraction,car.SpeedKmh,car.SurfaceGrip)*pace;
            if(show)trial.cornerSamples++;else trial.straightSamples++;
            int id=cornerIds[segment];
            if(id>=0)
            {
                var corner=trial.corners[id];corner.samples++;corner.maximumSlip=Mathf.Max(corner.maximumSlip,slip);
                corner.maxRoadOffset=Mathf.Max(corner.maxRoadOffset,roadOffset);corner.maxLateralG=Mathf.Max(corner.maxLateralG,lateralG);
                if(desired<=corner.targetApexKmh+4 && (corner.actualApexKmh<0 || car.SpeedKmh<corner.actualApexKmh))corner.actualApexKmh=car.SpeedKmh;
            }
            var target=path.Target(car.Body.position,progress.NextCheckpoint,10+car.SpeedKmh*.10f);
            var local=car.transform.InverseTransformPoint(target);
            float wheelbase=Mathf.Abs(car.wheels[0].transform.localPosition.z-car.wheels[2].transform.localPosition.z);
            float steering=Mathf.Atan2(2*wheelbase*local.x,Mathf.Max(.01f,local.x*local.x+local.z*local.z))*Mathf.Rad2Deg/car.CurrentSteeringLimit;
            car.Steering=Mathf.Clamp(steering,-input.SteeringInputLimit,input.SteeringInputLimit);
            float error=desired-car.SpeedKmh;
            car.Brake=show?Mathf.Min(input.KeyboardBrakeLimit,Mathf.Clamp01((-error-1)/12)):0;
            car.Throttle=car.Brake>.001f?0:show?Mathf.Clamp01(.12f+error/10):1;
            if(show && DrivingGuide.FeatherThrottle(car))car.Throttle=Mathf.Min(.45f,car.Throttle);
            if(car.BrakeLocking)car.Brake*=.6f;
            bool rearSpin=false;for(int i=2;i<4;i++)if(car.wheels[i].GetGroundHit(out var hit) && hit.forwardSlip<-.2f)rearSpin=true;
            if(car.SpeedKmh>50 && Mathf.Abs(car.Steering)>.035f && car.Throttle>.1f && (slip>4 || rearSpin || car.FrontGripUsage>1.1f))liftUntil=Time.fixedTime+.15f;
            if(Time.fixedTime<liftUntil)car.Throttle=0;
            car.OvertakeRequested=!show && car.SpeedKmh>150;
            bool drs=!show && car.SpeedKmh>180 && car.DRSEligible;if(car.DRSOpen!=drs)car.ToggleDRS();
            if(trial.seconds>=nextRecord)
            {
                nextRecord=trial.seconds+.1f;
                trace.Add(FormattableString.Invariant($"{trial.scene},{trial.mode},{trial.seconds:F2},{segment},{id+1},{car.SpeedKmh:F2},{desired:F2},{show},{car.Brake:F3},{car.Throttle:F3},{deviation:F3},{roadOffset:F3},{slip:F3},{lateralG:F3}"));
            }
        }
        void LateUpdate()
        {
            if(!running || !guide || trial.seconds<3)return;
            string stem="Docs/CornerGuide_"+trial.scene+"_"+trial.mode;
            if(!straightShot && !guide.CornerGuidanceActive && guide.advice.text=="" && car.SpeedKmh>140)
            {straightShot=true;ScreenCapture.CaptureScreenshot(stem+"_Straight.png");}
            if(!cornerShot && guide.CornerGuidanceActive && guide.advice.text.Contains("CORNER") && car.SpeedKmh>120)
            {cornerShot=true;ScreenCapture.CaptureScreenshot(stem+"_Corner.png");}
        }
        void OnDestroy(){Time.timeScale=1;}
    }
}
#endif
