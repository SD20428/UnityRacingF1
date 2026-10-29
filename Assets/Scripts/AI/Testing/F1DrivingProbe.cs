#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Overdrive
{
    // Disposable production-driver laps on the real circuit, with original vehicle physics.
    public sealed class F1DrivingProbe : MonoBehaviour
    {
        [Serializable] public sealed class Trial
        {
            public string difficulty;
            public bool completed,failed;
            public float seconds,maximumSpeed,maxDeviation;
            public int collisions,recoveries,checkpoint,samples;
            public float lane,pace;
        }
        [Serializable] public sealed class Report { public List<Trial> trials=new List<Trial>(); }
        public static string Status="Not started";
        RaceManager race; AIDriver driver; OverdriveVehicle car; CircuitProgress progress;
        GuideProbeContact contact; Trial trial; readonly Report report=new Report();
        readonly List<string> trace=new List<string>{"difficulty,seconds,checkpoint,speed,target,steering,throttle,brake,deviation"};
        int index=-1;float began,nextRecord;
        AIDifficulty[] presets;float[] lanes;string outputStem="F1_AI_Driving";
        public static string RunProfessional()
        {
            if(!Application.isPlaying || FindFirstObjectByType<F1DrivingProbe>())throw new InvalidOperationException("Fresh PlayMode required");
            var race=FindFirstObjectByType<RaceManager>();
            if(!race.professional)throw new InvalidOperationException("F1 DRIVER preset required");
            var probe=new GameObject("Temporary professional AI lap validation").AddComponent<F1DrivingProbe>();
            probe.presets=new[]{race.professional,race.professional,race.professional};probe.lanes=new[]{0f,-2.25f,2.25f};
            probe.outputStem="F1_Professional_Driving";
            return "F1 DRIVER centre, left and right lane full laps started";
        }
        public static string Run()
        {
            if(!Application.isPlaying || FindFirstObjectByType<F1DrivingProbe>())throw new InvalidOperationException("Fresh PlayMode required");
            new GameObject("Temporary F1 AI lap validation").AddComponent<F1DrivingProbe>();
            return "Normal and Hard production AI laps started";
        }
        void Start()
        {
            race=FindFirstObjectByType<RaceManager>();race.MainMenu();race.enabled=false;
            foreach(var racer in race.Racers)racer.vehicle.gameObject.SetActive(false);
            Next();
        }
        void Next()
        {
            if(trial!=null)
            {
                trial.seconds=Time.fixedTime-began;trial.collisions=contact.hits;trial.recoveries=driver.RecoveryCount;
                trial.checkpoint=progress.LastCheckpoint;report.trials.Add(trial);
                File.WriteAllText("Docs/"+outputStem+"Results.json",JsonUtility.ToJson(report,true));
                File.WriteAllLines("Docs/"+outputStem+"Trace.csv",trace);
            }
            if(car)Destroy(car.gameObject);
            if(++index>=(presets==null?2:presets.Length)){Status="Completed";Destroy(gameObject);return;}
            var actor=Instantiate(race.opponent.gameObject);actor.name="Temporary production F1 AI";
            driver=actor.GetComponent<AIDriver>();driver.difficulty=presets==null?(index==0?race.normal:race.hard):presets[index];
            driver.preferredLane=lanes==null?0:lanes[index];driver.pace=1;
            var identity=actor.GetComponent<RacerIdentity>();identity.gridCheckpoint=0;identity.startsBeforeLine=false;
            var path=driver.path;var forward=(path.Position(1)-path.Position(0)).normalized;
            actor.transform.SetPositionAndRotation(path.Position(0)+forward*2+Vector3.up*.52f,Quaternion.LookRotation(forward));
            actor.SetActive(true);car=actor.GetComponent<OverdriveVehicle>();car.ControlsEnabled=true;car.ResetDynamics();
            car.Body.constraints=RigidbodyConstraints.None;car.Body.linearVelocity=car.Body.angularVelocity=Vector3.zero;
            progress=actor.GetComponent<CircuitProgress>();progress.ResetProgress();progress.AcceptingCheckpoints=true;
            contact=actor.AddComponent<GuideProbeContact>();trial=new Trial{difficulty=driver.difficulty.name,lane=driver.preferredLane,pace=driver.pace};
            began=Time.fixedTime;nextRecord=0;Status="Running "+trial.difficulty;Physics.SyncTransforms();
        }
        void FixedUpdate()
        {
            if(!car)return;
            var path=driver.path;int s=progress.LastCheckpoint;
            var projected=Vector3.Lerp(path.Position(s),path.Position(s+1),path.SegmentFraction(car.Body.position,s));
            var offset=car.Body.position-projected;offset.y=0;float deviation=offset.magnitude;
            trial.samples++;trial.maximumSpeed=Mathf.Max(trial.maximumSpeed,car.SpeedKmh);trial.maxDeviation=Mathf.Max(trial.maxDeviation,deviation);
            float elapsed=Time.fixedTime-began;
            if(elapsed>=nextRecord)
            {
                nextRecord=elapsed+.2f;
                trace.Add(FormattableString.Invariant($"{trial.difficulty},{elapsed:F2},{s},{car.SpeedKmh:F2},{driver.TargetSpeedKmh:F2},{car.Steering:F3},{car.Throttle:F3},{car.Brake:F3},{deviation:F3}"));
            }
            if(progress.CompletedLaps>=1){trial.completed=true;Next();}
            else if(deviation>7.5f || car.transform.position.y<-.5f || driver.RecoveryCount>0 || elapsed>200){trial.failed=true;Next();}
        }
    }
}
#endif
