#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Overdrive
{
    public sealed class AudioTrackDetailProbe : MonoBehaviour
    {
        [System.Serializable] public sealed class Report
        {
            public bool passed=true;public List<string> checks=new List<string>();
            public float peakSpeed,intake,kerb,gravel,asphaltResidual;
            public int audioSources,shiftSounds,liftSounds,renderers,vertices,baselineBatches,detailBatches;
        }
        public static string Status="Not run";
        readonly Report report=new Report();
        void Check(bool pass,string label){report.passed&=pass;report.checks.Add((pass?"PASS ":"FAIL ")+label);}
        public static string Run()
        {
            if(!Application.isPlaying || FindFirstObjectByType<AudioTrackDetailProbe>())return "Fresh Play mode required";
            new GameObject("Temporary sound and surface verification").AddComponent<AudioTrackDetailProbe>();return "Running sound layers, road contacts, pause/reset and detail batch checks";
        }
        GameObject Road(string name,float x,float grip)
        {
            var road=GameObject.CreatePrimitive(PrimitiveType.Cube);road.name=name;road.transform.SetParent(transform);
            road.transform.position=new Vector3(x,-.5f,1000);road.transform.localScale=new Vector3(70,1,3000);
            var surface=road.AddComponent<TrackSurface>();surface.grip=grip;return road;
        }
        void Place(OverdriveVehicle car,float x,float speed)
        {
            car.Body.position=new Vector3(x,.52f,0);car.Body.rotation=Quaternion.identity;
            car.transform.SetPositionAndRotation(car.Body.position,car.Body.rotation);
            car.Body.linearVelocity=Vector3.forward*speed;car.Body.angularVelocity=Vector3.zero;
            car.Body.constraints=RigidbodyConstraints.None;car.ControlsEnabled=true;car.Steering=0;car.Brake=0;
            Physics.SyncTransforms();
        }
        IEnumerator Start()
        {
            var race=FindFirstObjectByType<RaceManager>();var car=race.player;var audio=car.GetComponent<VehicleRaceAudio>();
            var scenery=FindFirstObjectByType<CircuitSceneryMotion>();var detail=scenery.transform.Find("Track Finishing Details");var ambience=detail.GetComponent<CircuitAmbience>();
            report.audioSources=FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length;
            Check(report.audioSources<=32,"Full 20-car grid stays within the configured 32 audio voices");
            Check(detail.GetComponentsInChildren<Collider>(true).Length==0,"Track finishing geometry has no colliders");
            report.renderers=detail.GetComponentsInChildren<MeshRenderer>(true).Length;
            foreach(var filter in detail.GetComponentsInChildren<MeshFilter>(true))report.vertices+=filter.sharedMesh.vertexCount;
            Check(report.renderers<=48 && report.vertices<350000,"Detail geometry respects renderer and vertex budgets");
            Check(audio.intakeLoop && audio.transmissionLoop && audio.kerbLoop && audio.gravelLoop && audio.shiftClip && audio.liftClip,"Six additional vehicle sound layers assigned");
            Check(ambience && ambience.grandstands.Length>=4 && ambience.crowdClip && ambience.environmentClip,"Circuit ambience and grandstands linked");
            var input=car.GetComponent<PlayerVehicleInput>();input.enabled=false;
            var driver=car.gameObject.AddComponent<AIDriver>();driver.path=race.ai.path;driver.difficulty=race.professional;
            Status="Live track presentation";race.BeginRace();while(race.State==RaceState.Countdown)yield return null;
            yield return new WaitForSeconds(8);
            ScreenCapture.CaptureScreenshot("Docs/TrackFinish_Driving.png");yield return null;
            driver.enabled=false;car.ControlsEnabled=false;car.Body.constraints=RigidbodyConstraints.FreezeAll;
            foreach(var entry in race.Racers)if(entry.vehicle!=car)entry.vehicle.ControlsEnabled=false;
            yield return new WaitForSeconds(.4f);report.detailBatches=UnityEditor.UnityStats.batches;
            detail.gameObject.SetActive(false);yield return new WaitForSeconds(.4f);report.baselineBatches=UnityEditor.UnityStats.batches;
            detail.gameObject.SetActive(true);yield return new WaitForSeconds(.2f);
            Check(report.detailBatches-report.baselineBatches<=96,"Visible detail batch increase remains bounded (including render passes)");
            Destroy(car.GetComponent<VehiclePerception>());yield return null;Destroy(driver);yield return null;
            race.enabled=false;foreach(var entry in race.Racers)if(entry.vehicle!=car)entry.vehicle.gameObject.SetActive(false);
            car.GetComponent<CircuitProgress>().AcceptingCheckpoints=false;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(RaceManager).GetField("<State>k__BackingField",flags).SetValue(race,RaceState.Racing);
            Road("Asphalt Audio Fixture",10000,1);Road("Kerb Audio Fixture",10100,.96f);Road("Runoff Audio Fixture",10200,.65f);
            Place(car,10000,0);car.ResetDynamics();car.Brake=0;car.Throttle=1;
            Status="Engine load, gear engagement and throttle lift";float end=Time.time+10;
            while(Time.time<end && car.SpeedKmh<210)
            {report.peakSpeed=Mathf.Max(report.peakSpeed,car.SpeedKmh);report.intake=Mathf.Max(report.intake,audio.IntakeVolume);yield return null;}
            report.shiftSounds=audio.ShiftSounds;
            Check(report.peakSpeed>150 && report.intake>.02f && report.shiftSounds>=2,"Real acceleration engages intake and automatic shift sounds");
            int lifts=audio.LiftSounds;car.Throttle=0;yield return new WaitForSeconds(.3f);report.liftSounds=audio.LiftSounds;
            Check(report.liftSounds>lifts,"Releasing high-load throttle triggers a lift sound");
            Status="Wheel contact surface sound";Place(car,10100,22);car.Throttle=.25f;
            yield return new WaitForSeconds(.9f);report.kerb=audio.KerbVolume;
            Check(report.kerb>.005f && audio.GravelVolume<.001f,"Actual kerb wheel contacts produce rumble without runoff audio");
            Place(car,10200,22);car.Throttle=.25f;yield return new WaitForSeconds(.9f);report.gravel=audio.GravelVolume;
            Check(report.gravel>.005f && audio.KerbVolume<.001f,"Actual runoff contacts produce grit without kerb rumble");
            Place(car,10000,22);car.Throttle=.25f;yield return new WaitForSeconds(.9f);report.asphaltResidual=audio.GravelVolume+audio.KerbVolume;
            Check(report.asphaltResidual<.001f,"Returning to asphalt clears surface sounds");
            Status="Pause, resume and restart";race.TogglePause();yield return new WaitForSecondsRealtime(.3f);
            bool silent=true;foreach(var source in car.GetComponents<AudioSource>())silent&=!source.isPlaying;
            Check(audio.AudioPaused && ambience.Paused && silent,"Pause stops every vehicle layer and ambience");
            race.TogglePause();yield return new WaitForSeconds(.2f);
            Check(!audio.AudioPaused && !ambience.Paused,"Resume restores layered audio and ambience");
            race.enabled=true;race.MainMenu();input.enabled=true;yield return new WaitForSeconds(.4f);
            Check(audio.KerbVolume<.001f && audio.GravelVolume<.001f && audio.IntakeVolume<.001f,"Menu/restart clears driving surface and load sound");
            File.WriteAllText("Docs/AudioTrackDetailVerification.json",JsonUtility.ToJson(report,true));Status=report.passed?"PASS":"FAIL";
            Debug.Log("Audio / track detail verification "+Status);Destroy(gameObject);
        }
    }
}
#endif
