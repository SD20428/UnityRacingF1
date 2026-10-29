#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Overdrive
{
    // Disposable straight-road traffic fixture; ego uses production controls and WheelColliders.
    [DefaultExecutionOrder(-180)]
    public sealed class F1OvertakeProbe : MonoBehaviour
    {
        [Serializable] public sealed class Result
        {
            public bool passed,blocked,sawOvertaking;public int overtakes,contacts,recoveries;
            public float seconds,maximumLateral,minimumPassSeparation=999;
        }
        public static string Status="Not run";
        RaceManager race;AIDriver driver;OverdriveVehicle car,lead,blocker;
        GuideProbeContact contact;Result result;float began,nextRecord;
        readonly List<string> trace=new List<string>{"seconds,speed,target,lateral,leadDistance,state,overtakes"};
        bool blocked,done;
        public static string Run(bool blocked=false)
        {
            if(!Application.isPlaying || FindFirstObjectByType<F1OvertakeProbe>())throw new InvalidOperationException("Fresh PlayMode required");
            var p=new GameObject("Temporary F1 passing fixture").AddComponent<F1OvertakeProbe>();p.blocked=blocked;
            return blocked?"Occupied passing lane fixture started":"Clear straight passing fixture started";
        }
        void Start()
        {
            race=FindFirstObjectByType<RaceManager>();race.MainMenu();race.enabled=false;
            driver=race.ai;car=race.opponent;lead=race.player;blocker=race.Racers[2].vehicle;
            foreach(var e in race.Racers)e.vehicle.gameObject.SetActive(e.vehicle==car || e.vehicle==lead || blocked && e.vehicle==blocker);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.SetParent(transform);
            ground.transform.position=new Vector3(10000,-.5f,800);ground.transform.localScale=new Vector3(60,1,2000);ground.AddComponent<TrackSurface>();
            var path=gameObject.AddComponent<WaypointPath>();path.points=new Transform[6];
            var positions=new[]{new Vector3(10000,0,0),new Vector3(10000,0,800),new Vector3(10000,0,1600),new Vector3(10040,0,1600),new Vector3(10040,0,800),new Vector3(10040,0,0)};
            for(int i=0;i<6;i++){path.points[i]=new GameObject("Fixture gate "+i).transform;path.points[i].SetParent(transform);path.points[i].position=positions[i];}
            driver.path=path;driver.difficulty=Instantiate(race.professional);driver.difficulty.maximumSpeedKmh=72;
            driver.preferredLane=0;driver.pace=1;driver.enablePerception=driver.enableFollowing=true;
            foreach(var vehicle in new[]{car,lead,blocker})
            {
                if(vehicle==blocker && !blocked)continue;
                var progress=vehicle.GetComponent<CircuitProgress>();progress.checkpoints=path.points;progress.AcceptingCheckpoints=false;
                var identity=vehicle.GetComponent<RacerIdentity>();identity.gridCheckpoint=0;identity.startsBeforeLine=false;progress.ResetProgress();
                var p=new Vector3(10000+(vehicle==blocker?-3.25f:0),.7f,vehicle==car?20:vehicle==lead?60:47);
                vehicle.Body.constraints=RigidbodyConstraints.None;vehicle.Body.position=p;vehicle.Body.rotation=Quaternion.identity;
                vehicle.transform.SetPositionAndRotation(p,Quaternion.identity);vehicle.Body.linearVelocity=vehicle==car?Vector3.forward*12:Vector3.zero;vehicle.Body.angularVelocity=Vector3.zero;
                if(vehicle!=car)
                {
                    var input=vehicle.GetComponent<PlayerVehicleInput>();if(input)input.enabled=false;
                    var ai=vehicle.GetComponent<AIDriver>();if(ai)ai.enabled=false;
                    vehicle.ControlsEnabled=false;vehicle.Body.isKinematic=true;
                }
            }
            car.ControlsEnabled=true;driver.ResetForRace();contact=car.gameObject.AddComponent<GuideProbeContact>();
            result=new Result{blocked=blocked};began=Time.fixedTime;Physics.SyncTransforms();driver.Perception.Sample();Status="Running "+(blocked?"blocked":"clear");
        }
        void FixedUpdate()
        {
            if(done || !car)return;
            lead.Body.MovePosition(lead.Body.position+Vector3.forward*8*Time.fixedDeltaTime);
            if(blocked)blocker.Body.MovePosition(blocker.Body.position+Vector3.forward*8*Time.fixedDeltaTime);
            float elapsed=Time.fixedTime-began;
            float x=car.Body.position.x-10000,z=lead.Body.position.z-car.Body.position.z;
            result.maximumLateral=Mathf.Max(result.maximumLateral,Mathf.Abs(x));
            result.sawOvertaking|=driver.BehaviourState==AIBehaviourState.OVERTAKING;
            if(Mathf.Abs(z)<5)result.minimumPassSeparation=Mathf.Min(result.minimumPassSeparation,Mathf.Abs(x));
            if(elapsed>=nextRecord)
            {
                nextRecord=elapsed+.1f;
                trace.Add(FormattableString.Invariant($"{elapsed:F2},{car.SpeedKmh:F2},{driver.TargetSpeedKmh:F2},{x:F2},{z:F2},{driver.BehaviourState},{driver.OvertakeCount}"));
            }
            if(elapsed<18)return;
            done=true;result.seconds=elapsed;result.contacts=contact.hits;result.recoveries=driver.RecoveryCount;result.overtakes=driver.OvertakeCount;
            result.passed=contact.hits==0 && driver.RecoveryCount==0 && (blocked?driver.OvertakeCount==0 && result.maximumLateral<1.5f:driver.OvertakeCount>=1 && result.minimumPassSeparation>2.5f);
            string stem="Docs/F1_Overtake_"+(blocked?"Blocked":"Clear");
            File.WriteAllText(stem+".json",JsonUtility.ToJson(result,true));File.WriteAllLines(stem+".csv",trace);
            car.ControlsEnabled=false;Status="Completed "+JsonUtility.ToJson(result);
        }
    }
}
#endif
