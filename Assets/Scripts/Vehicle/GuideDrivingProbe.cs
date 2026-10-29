#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Overdrive
{
    // Actual track/WheelCollider experiment. Controls only; no forces or velocity changes after spawning.
    [DefaultExecutionOrder(-80)]
    public sealed class GuideDrivingProbe : MonoBehaviour
    {
        [Serializable] public sealed class Trial
        {
            public string label;
            public int start,end,collisions,samples;
            public bool completed,failed;
            public float seconds,maxDeviation,brakeSeconds,brakeIntegral,firstBrakeDistance=-1,minimumSpeed=999,maximumSpeed,apexSpeed;
            public float maximumBodySlip,slipSeconds;public int maximumGear,gearChanges;public bool automatic;
        }
        [Serializable] public sealed class Report { public List<Trial> trials=new List<Trial>(); }
        public static string Status="Not run";
        string outputStem="Guide_DrivingComparison";
        RaceManager race; OverdriveVehicle car; CircuitProgress progress; PlayerVehicleInput input;
        WaypointPath path; DrivingSpeedProfile profile; Trial trial;
        GuideProbeContact contact;
        readonly Report report=new Report(); readonly List<string> trace=new List<string>();
        readonly int[] starts={65,335,385},ends={150,395,450},apex={125,372,415};
        int test=-1; float began,recordAt,travelled; Vector3 previous;
        float liftUntil;
        DrivingGuideTuning candidate;bool trackCeiling;int previousGear;
        public static string RunCalibration(float lateral,float lateralCap,float acceleration,string label)
        {
            if(!Application.isPlaying || FindFirstObjectByType<GuideDrivingProbe>())throw new InvalidOperationException("Fresh trial required");
            var probe=new GameObject("Temporary guide capacity calibration").AddComponent<GuideDrivingProbe>();
            probe.test=5;probe.outputStem="Guide_Recalibration_"+label;probe.trackCeiling=true;
            probe.candidate=new DrivingGuideTuning{baseLateralAcceleration=lateral,maximumLateralAcceleration=lateralCap,aeroLateralCoefficient=.001f,brakingDeceleration=14,exitAcceleration=acceleration,maximumSpeedKmh=320};
            return "Physical full lap started: "+label;
        }
        public static string Run()
        {
            if(!Application.isPlaying) throw new InvalidOperationException("PlayMode required");
            if(FindFirstObjectByType<GuideDrivingProbe>()) throw new InvalidOperationException("Probe already active");
            var host=new GameObject("Temporary real-track guide experiment");host.AddComponent<GuideDrivingProbe>();
            return "Three real-track comparisons plus a complete candidate lap started. Exit PlayMode to restore scene.";
        }
        public static string RunFullLap()
        {
            if(!Application.isPlaying || FindFirstObjectByType<GuideDrivingProbe>()) throw new InvalidOperationException("Fresh PlayMode required");
            var host=new GameObject("Temporary complete guide lap");
            var probe=host.AddComponent<GuideDrivingProbe>();probe.test=5;probe.outputStem="Guide_FullLap";
            return "Complete physical lap started; results: Docs/Guide_FullLap.json";
        }
        void Start()
        {
            race=FindFirstObjectByType<RaceManager>(); race.MainMenu(); race.enabled=false; path=race.ai.path;
            var activeGuide=FindFirstObjectByType<DrivingGuide>();if(test==5 && candidate==null && activeGuide)path=activeGuide.ActivePath;
            foreach(var racer in race.Racers) racer.vehicle.gameObject.SetActive(false);
            trace.Add("case,seconds,checkpoint,speedKmh,targetKmh,brake,throttle,deviation,grounded");
            Next();
        }
        void Next()
        {
            if(trial!=null)
            {
                trial.collisions=contact?contact.hits:0;
                trial.seconds=Time.fixedTime-began;
                report.trials.Add(trial);
                File.WriteAllText("Docs/"+outputStem+".json",JsonUtility.ToJson(report,true));
                File.WriteAllLines("Docs/"+outputStem+".csv",trace);
            }
            if(car) Destroy(car.gameObject);
            test++;
            if(test>=7) { Status="Completed: Docs/"+outputStem+".json"; Destroy(gameObject); return; }
            bool legacy=test<6 && test%2==0; int sector=test==6?0:test/2;
            trial=new Trial {label=test==6?"candidate_full_lap":(legacy?"legacy":"candidate")+"_sector"+sector,start=test==6?0:starts[sector],end=test==6?0:ends[sector]};
            var tuning=legacy?new DrivingGuideTuning {baseLateralAcceleration=14,aeroLateralCoefficient=0,maximumLateralAcceleration=14,brakingDeceleration=10,exitAcceleration=0}:new DrivingGuideTuning();
            if(test==6 && candidate==null)
            {
                var guide=FindFirstObjectByType<DrivingGuide>();
                if(guide)tuning=guide.VehicleTuning();
            }
            profile=new DrivingSpeedProfile(path,candidate??tuning);
            var actor=Instantiate(race.player.gameObject);actor.name="Physical guide comparison "+trial.label;
            // Original scene actor is inactive; configure the fresh copy before Awake/physics.
            input=actor.GetComponent<PlayerVehicleInput>(); input.enabled=false;
            progress=actor.GetComponent<CircuitProgress>();
            var identity=actor.GetComponent<RacerIdentity>(); identity.gridCheckpoint=trial.start; identity.startsBeforeLine=false;
            var forward=(path.Position(trial.start+1)-path.Position(trial.start)).normalized;
            actor.transform.SetPositionAndRotation(path.Position(trial.start)+forward*2+Vector3.up*.52f,Quaternion.LookRotation(forward));
            actor.SetActive(true);
            car=actor.GetComponent<OverdriveVehicle>();car.ControlsEnabled=true;car.ResetDynamics();
            previousGear=car.Gear;trial.automatic=!car.ManualTransmission;
            contact=actor.AddComponent<GuideProbeContact>();
            car.Body.constraints=RigidbodyConstraints.None;car.Body.linearVelocity=Vector3.zero;car.Body.angularVelocity=Vector3.zero;
            progress.ResetProgress();progress.AutoRecoverBelowTerrain=false;progress.AcceptingCheckpoints=true;
            previous=car.Body.position;travelled=0;began=Time.fixedTime;recordAt=0;
            Status="Running "+trial.label;
            Physics.SyncTransforms();
        }
        void FixedUpdate()
        {
            if(!car || trial==null) return;
            float elapsed=Time.fixedTime-began; int segment=progress.LastCheckpoint;
            var a=path.Position(segment);var delta=path.Position(segment+1)-a;
            float fraction=Mathf.Clamp01(Vector3.Dot(car.Body.position-a,delta)/delta.sqrMagnitude);
            float deviation=Vector3.Distance(new Vector3(car.Body.position.x,0,car.Body.position.z),a+delta*fraction);
            trial.maxDeviation=Mathf.Max(trial.maxDeviation,deviation);trial.samples++;
            travelled+=Vector3.Distance(car.Body.position,previous);previous=car.Body.position;
            trial.maximumSpeed=Mathf.Max(trial.maximumSpeed,car.SpeedKmh);
            trial.maximumGear=Mathf.Max(trial.maximumGear,car.Gear);
            if(car.Gear!=previousGear){trial.gearChanges++;previousGear=car.Gear;}
            var localMotion=car.transform.InverseTransformDirection(car.Body.linearVelocity);
            float bodySlip=car.SpeedKmh>50?Mathf.Abs(Mathf.Atan2(localMotion.x,Mathf.Max(1,localMotion.z))*Mathf.Rad2Deg):0;
            trial.maximumBodySlip=Mathf.Max(trial.maximumBodySlip,bodySlip);
            if(bodySlip>5)trial.slipSeconds+=Time.fixedDeltaTime;
            if(Mathf.Abs(segment-apex[test==6?0:test/2])<=2) {trial.apexSpeed=car.SpeedKmh;trial.minimumSpeed=Mathf.Min(trial.minimumSpeed,car.SpeedKmh);}
            if(deviation>7.5f || car.transform.position.y<-.5f || elapsed>(test==6?200:40))
            {trial.failed=true;Next();return;}
            if(test==6?progress.CompletedLaps>=1:segment>=trial.end) {trial.completed=true;Next();return;}
            if(car.ManualTransmission)
            {
                if(car.RoadEngineRPM>12500)car.ShiftUp();
                else if(car.Gear>1 && car.RoadEngineRPM<6500)car.ShiftDown();
            }
            var target=path.Target(car.Body.position,progress.NextCheckpoint,10+car.SpeedKmh*.10f);
            var local=car.transform.InverseTransformPoint(target);
            float wheelbase=Mathf.Abs(car.wheels[0].transform.localPosition.z-car.wheels[2].transform.localPosition.z);
            float steer=Mathf.Atan2(2*wheelbase*local.x,Mathf.Max(.01f,local.x*local.x+local.z*local.z))*Mathf.Rad2Deg/car.CurrentSteeringLimit;
            car.Steering=Mathf.Clamp(steer,-input.SteeringInputLimit,input.SteeringInputLimit);
            float desired=test<6 && test%2==0?Mathf.Lerp(profile.speeds[segment],profile.speeds[(segment+1)%profile.speeds.Length],fraction)*3.6f:profile.SpeedKmh(segment,fraction);
            if(test==6 && candidate==null)
            {
                var guide=FindFirstObjectByType<DrivingGuide>();
                if(guide)desired=profile.AdvisoryKmh(segment,fraction,car.SpeedKmh,guide.tuning.reactionSeconds)*Mathf.Sqrt(Mathf.Clamp(car.SurfaceGrip,.25f,1));
            }
            float error=desired-car.SpeedKmh;
            car.Brake=Mathf.Min(test<6 && test%2==0?.65f:.35f,Mathf.Clamp01((-error-2)/14));
            car.Throttle=car.Brake>.001f?0:Mathf.Clamp01(.12f+error/8);
            if(test==6)
            {
                // Follow the displayed advisory bands instead of chasing the speed ceiling.
                if(!trackCeiling && error>=-DrivingGuide.BrakeToleranceKmh && error<=DrivingGuide.CoastMarginKmh) {car.Throttle=0;car.Brake=0;}
                if(!trackCeiling)
                {
                    if(DrivingGuide.FeatherThrottle(car))
                    {
                        // HOLD SPEED asks for balanced pedals, not zero throttle/rear engine braking.
                        car.Brake=Mathf.Min(input.KeyboardBrakeLimit,Mathf.Clamp01((-error-1)/12));
                        car.Throttle=car.Brake>.001f?0:Mathf.Min(.45f,Mathf.Clamp01(.12f+error/10));
                        if(car.BrakeLocking)car.Brake*=.6f;
                    }
                }
                if(trackCeiling)
                {
                    car.Brake=Mathf.Min(input.KeyboardBrakeLimit,Mathf.Clamp01((-error-1)/12));
                    car.Throttle=car.Brake>.001f?0:Mathf.Clamp01(.12f+error/10);
                    if(Mathf.Abs(car.Steering)>.04f && car.SpeedKmh>80)car.Throttle=Mathf.Min(car.Throttle,.45f);
                    if(car.BrakeLocking)car.Brake*=.6f;
                }
                var localVelocity=car.transform.InverseTransformDirection(car.Body.linearVelocity);
                float slip=Mathf.Abs(Mathf.Atan2(localVelocity.x,Mathf.Max(1,localVelocity.z))*Mathf.Rad2Deg);
                bool rearSlip=false;
                // Negative longitudinal slip is wheelspin; positive slip is engine/brake drag.
                // Treating both as wheelspin repeatedly lifted the pedals during corner coasting.
                for(int i=2;i<4;i++) if(car.wheels[i].GetGroundHit(out var hit) && hit.forwardSlip<-.2f)rearSlip=true;
                // Test-driver pedal lift, equivalent to releasing W; no shipped traction assist.
                if(car.SpeedKmh>50 && Mathf.Abs(car.Steering)>.035f && car.Throttle>.1f &&
                    (slip>4 || rearSlip || car.FrontGripUsage>1.1f))liftUntil=Time.fixedTime+.15f;
                if(Time.fixedTime<liftUntil)car.Throttle=0;
            }
            if(car.Brake>.01f)
            {
                if(trial.firstBrakeDistance<0)trial.firstBrakeDistance=travelled;
                trial.brakeSeconds+=Time.fixedDeltaTime;trial.brakeIntegral+=car.Brake*Time.fixedDeltaTime;
            }
            if(elapsed>=recordAt)
            {
                recordAt=elapsed+.1f;
                trace.Add(FormattableString.Invariant($"{trial.label},{elapsed:F2},{segment},{car.SpeedKmh:F2},{desired:F2},{car.Brake:F3},{car.Throttle:F3},{deviation:F3},{car.GroundedWheels}"));
            }
        }
    }
}
#endif
