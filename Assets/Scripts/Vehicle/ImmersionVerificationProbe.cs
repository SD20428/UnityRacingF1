#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Overdrive
{
    public sealed class ImmersionVerificationProbe : MonoBehaviour
    {
        [System.Serializable] public sealed class Report
        {
            public bool passed=true;
            public List<string> checks=new List<string>();
            public float peakSpeed, straightSpeed, peakPitch, peakCameraMotion, peakTow, soloCoastSpeed, towCoastSpeed, brakingTyreVolume;
            public int frames;
        }
        public static string Status="Not run";
        readonly Report report=new Report();
        RaceManager race; OverdriveVehicle car;
        int firstFrame;
        void Check(bool result,string label) { report.passed &= result; report.checks.Add((result?"PASS ":"FAIL ")+label); }
        public static string Run()
        {
            if(!Application.isPlaying || FindFirstObjectByType<ImmersionVerificationProbe>())return "Fresh Play mode required";
            new GameObject("Immersion verification (temporary)").AddComponent<ImmersionVerificationProbe>();
            return "Running driving, audio, camera, pause, restart and physical slipstream checks";
        }
        IEnumerator Start()
        {
            firstFrame=Time.frameCount;race=FindFirstObjectByType<RaceManager>();car=race.player;
            var audio=car.GetComponent<VehicleRaceAudio>();var camera=FindFirstObjectByType<VehicleFollowCamera>();
            Check(VehicleSlipstream.EvaluateWake(new Vector3(0,0,18),1,200,200)>.95f,"Aligned moving lead car produces wake");
            Check(VehicleSlipstream.EvaluateWake(new Vector3(8,0,18),1,200,200)==0,"Adjacent lane outside wake is clear air");
            Check(VehicleSlipstream.EvaluateWake(new Vector3(0,0,-18),1,200,200)==0,"Car behind provides no tow");
            Check(VehicleSlipstream.EvaluateWake(new Vector3(0,0,18),-1,200,200)==0,"Oncoming car provides no tow");
            Check(VehicleSlipstream.EvaluateWake(new Vector3(0,0,18),1,200,0)==0,"Stopped car provides no tow");
            Check(VehicleSlipstream.EvaluateWake(new Vector3(0,4,18),1,200,200)==0,"Another track level provides no tow");
            int installed=0;foreach(var entry in race.Racers)if(entry.vehicle.GetComponent<VehicleRaceAudio>() && entry.vehicle.GetComponent<VehicleSlipstream>())installed++;
            Check(installed==race.FieldSize,"Every grid car has identical immersion components");
            Check(audio && audio.engineLoop && audio.tyreLoop && audio.windLoop && audio.impactClip && audio.countdownClip && audio.startClip,"Six audio assets assigned");
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==1,"Exactly one audio listener");
            race.MainMenu();yield return new WaitForSeconds(.3f);
            float idlePitch=audio.EnginePitch;
            var input=car.GetComponent<PlayerVehicleInput>();input.enabled=false;
            var driver=car.gameObject.AddComponent<AIDriver>();driver.path=race.ai.path;driver.difficulty=race.normal;driver.preferredLane=0;
            Status="Real 20-car start and driving";race.BeginRace();
            while(race.State==RaceState.Countdown)yield return null;
            float until=Time.time+14;
            while(Time.time<until)
            {
                report.peakSpeed=Mathf.Max(report.peakSpeed,car.SpeedKmh);report.peakPitch=Mathf.Max(report.peakPitch,audio.EnginePitch);
                report.peakCameraMotion=Mathf.Max(report.peakCameraMotion,camera.MotionAngles.magnitude);
                yield return null;
            }
            Check(report.peakSpeed>50,"20-car grid drives forward under real traffic constraints");
            Check(report.peakPitch>idlePitch+.3f,"Engine pitch follows actual revs");
            Check(report.peakCameraMotion>.01f && report.peakCameraMotion<2.1f,"Inertial camera responds within comfort limits");
            Check(audio.StartSignals==1 && audio.CountdownSignals>=4,"Countdown ticks and one lights-out signal");
            ScreenCapture.CaptureScreenshot("Docs/Immersion_Driving.png");
            Status="Pause and resume";race.TogglePause();yield return new WaitForSecondsRealtime(.3f);
            Vector3 paused=car.transform.position;float elapsed=race.Elapsed;
            yield return new WaitForSecondsRealtime(.3f);
            Check(audio.AudioPaused && Vector3.Distance(paused,car.transform.position)<.001f && race.Elapsed==elapsed,"Pause freezes simulation and all local audio");
            race.TogglePause();yield return new WaitForSeconds(.2f);
            Check(!audio.AudioPaused && audio.StartSignals==1,"Resume restores sound without replaying start cue");
            driver.enabled=false;
            Status="Controlled physical coast comparison";
            foreach(var entry in race.Racers)if(entry.vehicle!=car)entry.vehicle.gameObject.SetActive(false);
            var leader=race.opponent;leader.gameObject.SetActive(true);leader.GetComponent<AIDriver>().enabled=false;
            car.GetComponent<CircuitProgress>().AcceptingCheckpoints=false;leader.GetComponent<CircuitProgress>().AcceptingCheckpoints=false;
            var platform=GameObject.CreatePrimitive(PrimitiveType.Cube);platform.name="Temporary aero test surface";
            platform.transform.position=new Vector3(10000,1999.5f,10600);platform.transform.localScale=new Vector3(80,1,2000);
            for(int pass=0;pass<2;pass++)
            {
                car.ControlsEnabled=false;leader.ControlsEnabled=false;car.Body.constraints=RigidbodyConstraints.FreezeRotation;leader.Body.constraints=RigidbodyConstraints.FreezeRotation;
                car.Body.position=new Vector3(10000,2000.8f,9800);car.Body.rotation=Quaternion.identity;
                car.transform.SetPositionAndRotation(car.Body.position,Quaternion.identity);
                leader.Body.position=car.Body.position+new Vector3(pass==0?12:0,0,18);leader.Body.rotation=Quaternion.identity;
                leader.transform.SetPositionAndRotation(leader.Body.position,Quaternion.identity);
                car.Body.linearVelocity=leader.Body.linearVelocity=Vector3.zero;
                car.Body.angularVelocity=leader.Body.angularVelocity=Vector3.zero;
                yield return new WaitForSeconds(.7f);
                car.ControlsEnabled=leader.ControlsEnabled=true;car.Throttle=car.Brake=car.Steering=0;
                leader.Throttle=leader.Brake=leader.Steering=0;
                car.Body.linearVelocity=leader.Body.linearVelocity=Vector3.forward*(200f/3.6f);
                for(int frame=0;frame<100;frame++)
                {
                    leader.Body.position=car.Body.position+new Vector3(pass==0?12:0,0,18);
                    leader.Body.linearVelocity=car.Body.linearVelocity;
                    yield return new WaitForFixedUpdate();
                    if(pass==1)report.peakTow=Mathf.Max(report.peakTow,car.SlipstreamStrength);
                }
                if(pass==0)report.soloCoastSpeed=car.SpeedKmh;else report.towCoastSpeed=car.SpeedKmh;
            }
            Check(report.peakTow>.8f,"Live moving lead car builds slipstream");
            Check(report.towCoastSpeed>report.soloCoastSpeed+.5f,"Tow reduces measured coast-down loss without throttle");
            Check(car.GetComponent<VehicleSlipstream>().DragMultiplier>=.84f && car.GetComponent<VehicleSlipstream>().DownforceMultiplier>=.92f,"Aerodynamic effects remain bounded");
            Status="Unobstructed acceleration and tyre audio";leader.gameObject.SetActive(false);
            car.ResetDynamics();car.Body.position=new Vector3(10000,2000.8f,9800);car.Body.rotation=Quaternion.identity;
            car.transform.SetPositionAndRotation(car.Body.position,Quaternion.identity);car.Body.linearVelocity=car.Body.angularVelocity=Vector3.zero;
            yield return new WaitForSeconds(.7f);car.Throttle=1;car.Brake=0;
            until=Time.time+10;
            while(Time.time<until){report.straightSpeed=Mathf.Max(report.straightSpeed,car.SpeedKmh);yield return null;}
            Check(report.straightSpeed>200 && car.Gear>=4,"Unobstructed throttle physically exceeds 200 km/h with automatic upshifts");
            car.Throttle=0;car.Brake=.6f;until=Time.time+2;
            while(Time.time<until){report.brakingTyreVolume=Mathf.Max(report.brakingTyreVolume,audio.TyreVolume);yield return null;}
            Check(car.SpeedKmh<report.straightSpeed-30 && report.brakingTyreVolume>.001f,"Real braking reduces speed and produces slip-based tyre sound");
            Destroy(platform);
            // Perception requires AIDriver: release the observer before its temporary driver.
            var perception=car.GetComponent<VehiclePerception>();if(perception)Destroy(perception);
            yield return null;Destroy(driver);yield return null;input.enabled=true;
            Status="Restart and solo-session reset";
            race.BeginRace();yield return new WaitForSeconds(.2f);
            Check(car.SlipstreamStrength==0 && !car.DRSOpen && car.HybridEnergyMJ==4,"Restart resets aerodynamic state and energy");
            race.MainMenu();race.SelectQualifying();yield return new WaitForSeconds(.2f);
            Check(race.FieldSize==1 && car.SlipstreamStrength==0,"Qualifying has no ghost tow from disabled opponents");
            race.SelectGrandPrix();race.MainMenu();
            report.frames=Time.frameCount-firstFrame;Check(report.frames>100,"Player loop genuinely advances");
            Directory.CreateDirectory("Docs");File.WriteAllText("Docs/ImmersionVerification.json",JsonUtility.ToJson(report,true));
            Status=report.passed?"PASS":"FAIL";
            Debug.Log("Immersion verification "+Status+": "+report.checks.Count+" checks");
            Destroy(gameObject);
        }
    }
}
#endif
