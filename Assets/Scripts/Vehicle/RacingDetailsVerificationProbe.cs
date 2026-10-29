#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Overdrive
{
    public sealed class RacingDetailsVerificationProbe : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public bool passed=true;public int checks,highestGear,gearAt200=-1,gearAt120=-1,gearAt50=-1,upshifts,downshifts,skidWheels,sparkBursts;
            public float peakSpeed,peakHeat,maximumG;public bool dust,left,right,rear;
            public List<string> assertions=new List<string>();
        }
        public static string Status="Not run";
        readonly Report report=new Report();readonly List<string> trace=new List<string>{"stage,seconds,speed,gear,roadRPM,engineRPM,brake,throttle,heat,skid,dust"};
        RaceManager race;OverdriveVehicle car;PlayerVehicleInput input;VehicleSurfaceDetails surface;RaceDetailTelemetry hud;
        TrackSurface ground;string stage;float throttle,brake,began,nextRecord;bool driving;
        public static string Run()
        {
            if(!Application.isPlaying || FindFirstObjectByType<RacingDetailsVerificationProbe>())throw new InvalidOperationException("Fresh PlayMode required");
            new GameObject("Temporary surface and transmission verification").AddComponent<RacingDetailsVerificationProbe>();return "Physical acceleration, braking/downshifts, loose surface, impact and radar fixture started";
        }
        void Check(bool ok,string label){report.checks++;report.passed&=ok;report.assertions.Add((ok?"PASS ":"FAIL ")+label);}
        void FixedUpdate()
        {
            if(!driving || !car)return;
            car.Throttle=throttle;car.Brake=brake;car.Steering=0;
            report.peakSpeed=Mathf.Max(report.peakSpeed,car.SpeedKmh);report.highestGear=Mathf.Max(report.highestGear,car.Gear);
            report.peakHeat=Mathf.Max(report.peakHeat,surface.BrakeHeat);report.skidWheels=Mathf.Max(report.skidWheels,surface.SkidEmittingWheels);
            report.maximumG=Mathf.Max(report.maximumG,hud.MaximumBrakingG);
            if(stage=="braking")
            {
                if(car.SpeedKmh<=200 && report.gearAt200<0)report.gearAt200=car.Gear;
                if(car.SpeedKmh<=120 && report.gearAt120<0)report.gearAt120=car.Gear;
                if(car.SpeedKmh<=50 && report.gearAt50<0)report.gearAt50=car.Gear;
            }
            if(Time.fixedTime>=nextRecord)
            {
                nextRecord=Time.fixedTime+.1f;
                trace.Add(FormattableString.Invariant($"{stage},{Time.fixedTime-began:F2},{car.SpeedKmh:F2},{car.Gear},{car.RoadEngineRPM:F0},{car.EngineRPM:F0},{car.Brake:F2},{car.Throttle:F2},{surface.BrakeHeat:F3},{surface.SkidEmittingWheels},{surface.DustActive}"));
            }
        }
        IEnumerator Start()
        {
            race=FindFirstObjectByType<RaceManager>();car=race.player;hud=FindFirstObjectByType<RaceDetailTelemetry>();surface=car.GetComponent<VehicleSurfaceDetails>();
            race.MainMenu();input=car.GetComponent<PlayerVehicleInput>();input.enabled=false;
            race.BeginRace();race.enabled=false;
            foreach(var e in race.Racers)if(e.vehicle!=car)e.vehicle.gameObject.SetActive(false);
            var pad=GameObject.CreatePrimitive(PrimitiveType.Cube);pad.name="Temporary detail road";pad.transform.SetParent(transform);pad.transform.position=new Vector3(10000,-.5f,1000);pad.transform.localScale=new Vector3(120,1,4000);ground=pad.AddComponent<TrackSurface>();
            car.Body.constraints=RigidbodyConstraints.None;car.Body.position=new Vector3(10000,.52f,0);car.Body.rotation=Quaternion.identity;
            car.transform.SetPositionAndRotation(car.Body.position,Quaternion.identity);car.Body.linearVelocity=car.Body.angularVelocity=Vector3.zero;car.ResetDynamics();car.ControlsEnabled=true;car.OvertakeRequested=false;
            car.GetComponent<CircuitProgress>().AutoRecoverBelowTerrain=false;car.GetComponent<CircuitProgress>().AcceptingCheckpoints=false;
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;typeof(RaceManager).GetField("<State>k__BackingField",flags).SetValue(race,RaceState.Racing);typeof(RaceManager).GetField("startedAt",flags).SetValue(race,Time.timeAsDouble);
            Physics.SyncTransforms();began=Time.fixedTime;driving=true;throttle=1;stage=Status="acceleration";
            float until=Time.unscaledTime+25;
            while(car.SpeedKmh<300 && Time.unscaledTime<until)yield return null;
            Check(car.SpeedKmh>=290 && report.highestGear==8,"Actual automatic acceleration reaches the high-speed top gear");
            stage=Status="braking";throttle=0;brake=.55f;
            yield return new WaitForSecondsRealtime(.9f);ScreenCapture.CaptureScreenshot("Docs/RacingDetails_Braking.png");yield return null;
            until=Time.unscaledTime+12;while(car.SpeedKmh>2 && Time.unscaledTime<until)yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            report.upshifts=car.AutomaticUpshifts;report.downshifts=car.AutomaticDownshifts;
            Check(report.gearAt200<=6 && report.gearAt200>0,"Braking at 200km/h has downshifted out of eighth");
            Check(report.gearAt120<=3 && report.gearAt120>0,"Braking at 120km/h has a useful low gear");
            Check(car.Gear==1 && report.downshifts>=7,"Stopping sequentially returns to first gear");
            Check(report.peakHeat>.18f,"Real braking heats the cosmetic discs");
            Check(report.skidWheels>0,"Real wheel slip emits tyre traces");
            Check(hud.TopSpeed>290 && report.maximumG>.1f,"HUD speed and braking G derive from actual driving");
            stage=Status="loose surface";ground.grip=.55f;brake=0;throttle=.4f;
            until=Time.unscaledTime+15;while(car.SpeedKmh<45 && Time.unscaledTime<until)yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            report.dust=surface.DustActive && surface.dust.particleCount>0;
            Check(report.dust,"Loose ground and moving grounded wheels emit visible dust");ScreenCapture.CaptureScreenshot("Docs/RacingDetails_Dust.png");yield return null;
            stage=Status="impact";var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary spark contact";wall.transform.SetParent(transform);wall.transform.position=car.Body.position+car.transform.forward*12+Vector3.up*.7f;wall.transform.rotation=car.transform.rotation;wall.transform.localScale=new Vector3(12,2,.5f);Physics.SyncTransforms();
            throttle=.45f;until=Time.unscaledTime+5;
            while(surface.SparkBursts==0 && Time.unscaledTime<until)yield return null;
            report.sparkBursts=surface.SparkBursts;Check(report.sparkBursts>0,"A real body impact emits sparks");ScreenCapture.CaptureScreenshot("Docs/RacingDetails_Sparks.png");yield return null;
            driving=false;car.Throttle=0;car.Brake=1;car.Body.constraints=RigidbodyConstraints.FreezeAll;
            stage=Status="proximity";var other=race.Racers[1].vehicle;
            other.gameObject.SetActive(true);other.GetComponent<AIDriver>().enabled=false;other.ControlsEnabled=false;other.Body.isKinematic=true;
            other.transform.position=car.transform.TransformPoint(new Vector3(-4,0,0));other.Body.position=other.transform.position;Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.25f);report.left=hud.LeftNearby;
            other.transform.position=car.transform.TransformPoint(new Vector3(4,0,0));other.Body.position=other.transform.position;Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.25f);report.right=hud.RightNearby;
            other.transform.position=car.transform.TransformPoint(new Vector3(0,0,-18));other.Body.position=other.transform.position;Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.25f);report.rear=hud.RearNearby;
            Check(report.left && report.right && report.rear,"All three proximity directions follow active nearby cars");
            other.gameObject.SetActive(false);yield return new WaitForSecondsRealtime(.25f);Check(!hud.LeftNearby && !hud.RightNearby && !hud.RearNearby,"Hidden/finished cars do not leave proximity warnings");
            car.ControlsEnabled=false;yield return null;Check(!surface.DustActive && surface.SkidEmittingWheels==0,"Vehicle hold clears active surface emitters");
            File.WriteAllText("Docs/RacingDetails_PhysicalChecks.json",JsonUtility.ToJson(report,true));File.WriteAllLines("Docs/RacingDetails_PhysicalTrace.csv",trace);
            Status=(report.passed?"PASS ":"FAIL ")+report.checks+" checks";
            if(!report.passed)Debug.LogError(Status+": Docs/RacingDetails_PhysicalChecks.json");
            // Remain in this disposable fixture for visual inspection; exit PlayMode to restore the scene.
        }
    }
}
#endif
