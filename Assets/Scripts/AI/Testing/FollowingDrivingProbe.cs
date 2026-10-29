#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    // Disposable PlayMode fixture. Exit PlayMode after the probe to restore the scene.
    [DefaultExecutionOrder(-180)]
    public sealed class FollowingDrivingProbe : MonoBehaviour
    {
        AIDriver ai;
        OverdriveVehicle car, lead;
        FollowingProbeContact contact;
        float started, nextRecord, minimumGap=float.PositiveInfinity;
        float movingSpeed, stoppedSpeed, resumedSpeed;
        bool sawFollowing, sawPreparation, sawEmergency, resumed, stopHeld, finished;
        readonly List<string> rows=new List<string>();
        [Serializable]
        public sealed class Result
        {
            public bool passed, sawFollowing, sawPreparation, sawEmergency, stopHeld, resumed;
            public int collisions, recoveries, frames;
            public float minimumBumperGap, movingSpeedKmh, stoppedSpeedKmh, resumedSpeedKmh, duration;
            public string scenario="Player at 8m/s, stops at 10s, removed at 26s; AI uses unchanged WheelCollider physics";
        }

        public static string Run()
        {
            if(!Application.isPlaying) throw new InvalidOperationException("Enter PlayMode first");
            if(FindFirstObjectByType<FollowingDrivingProbe>()) throw new InvalidOperationException("Probe already running");
            var race=FindFirstObjectByType<RaceManager>();
            if(!race || race.Racers.Count<2) throw new InvalidOperationException("Loaded race required");
            race.MainMenu(); race.enabled=false;
            var root=new GameObject("Temporary Phase 2 driving fixture");
            var probe=root.AddComponent<FollowingDrivingProbe>();
            probe.ai=race.ai; probe.car=race.opponent; probe.lead=race.player;
            foreach(var entry in race.Racers)
                if(entry.vehicle!=probe.car && entry.vehicle!=probe.lead) entry.vehicle.gameObject.SetActive(false);

            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name="Temporary asphalt"; ground.transform.SetParent(root.transform);
            ground.transform.position=new Vector3(10000,-.5f,400);
            ground.transform.localScale=new Vector3(60,1,1000);
            ground.AddComponent<TrackSurface>();
            var path=root.AddComponent<WaypointPath>(); path.points=new Transform[4];
            Vector3[] points={new Vector3(10000,0,0),new Vector3(10000,0,800),new Vector3(10040,0,800),new Vector3(10040,0,0)};
            for(int i=0;i<4;i++)
            {
                var point=new GameObject("Temporary checkpoint "+i).transform;
                point.SetParent(root.transform); point.position=points[i]; path.points[i]=point;
            }
            var difficulty=Instantiate(probe.ai.difficulty);
            difficulty.name="Temporary Phase2 72kmh decision tuning";
            difficulty.maximumSpeedKmh=difficulty.cornerSpeedKmh=72;
            probe.ai.difficulty=difficulty; probe.ai.path=path; probe.ai.preferredLane=0;
            probe.ai.pace=1; probe.ai.enablePerception=probe.ai.enableFollowing=true;
            probe.ai.following=new AIFollowingSettings();
            foreach(var vehicle in new[]{probe.car,probe.lead})
            {
                var progress=vehicle.GetComponent<CircuitProgress>(); progress.checkpoints=path.points;
                progress.AcceptingCheckpoints=false;
                var identity=vehicle.GetComponent<RacerIdentity>();
                if(identity) { identity.gridCheckpoint=0; identity.startsBeforeLine=false; }
                progress.ResetProgress();
                vehicle.Body.constraints=RigidbodyConstraints.None;
                vehicle.Body.position=new Vector3(10000,.7f,vehicle==probe.car?20:60);
                vehicle.Body.rotation=Quaternion.identity;
                vehicle.transform.SetPositionAndRotation(vehicle.Body.position,Quaternion.identity);
                vehicle.Body.linearVelocity=vehicle==probe.car?Vector3.forward*12:Vector3.zero;
                vehicle.Body.angularVelocity=Vector3.zero;
            }
            probe.car.ControlsEnabled=true;
            var playerInput=probe.lead.GetComponent<PlayerVehicleInput>(); if(playerInput) playerInput.enabled=false;
            probe.lead.ControlsEnabled=false; probe.lead.Body.isKinematic=true;
            probe.ai.ResetForRace();
            probe.contact=probe.car.gameObject.AddComponent<FollowingProbeContact>(); probe.contact.lead=probe.lead.Body;
            Physics.SyncTransforms();
            probe.started=Time.fixedTime;
            probe.rows.Add("seconds,speedKmh,targetKmh,gap,state,brake,throttle,recoveries");
            return "32-second Phase 2 physics probe started; exit PlayMode afterward to restore the scene";
        }

        void FixedUpdate()
        {
            if(finished || !ai) return;
            float elapsed=Time.fixedTime-started;
            if(elapsed<10) lead.Body.MovePosition(lead.Body.position+Vector3.forward*8*Time.fixedDeltaTime);
            else if(elapsed<26) lead.Body.MovePosition(lead.Body.position);
            else if(lead.gameObject.activeSelf) lead.gameObject.SetActive(false);
            var plan=ai.FollowingPlan;
            if(elapsed>.3f && elapsed<26)
            {
                float gap=lead.Body.position.z-car.Body.position.z-(ai.Perception.SelfBodySize.z+ai.Perception.Lead.bodySize.z)*.5f;
                minimumGap=Mathf.Min(minimumGap,gap);
                sawFollowing|=ai.BehaviourState==AIBehaviourState.FOLLOWING;
                sawPreparation|=ai.BehaviourState==AIBehaviourState.PREPARE_PASS;
                sawEmergency|=ai.BehaviourState==AIBehaviourState.EVADING;
            }
            if(elapsed>8 && elapsed<10) movingSpeed=car.SpeedKmh;
            if(elapsed>24 && elapsed<26)
            { stoppedSpeed=car.SpeedKmh; stopHeld=car.SpeedKmh<1 && car.Brake>.9f; }
            if(elapsed>30)
            { resumedSpeed=car.SpeedKmh; resumed|=ai.BehaviourState==AIBehaviourState.RACING && car.SpeedKmh>10; }
            if(elapsed>=nextRecord)
            {
                nextRecord=elapsed+.2f;
                rows.Add(FormattableString.Invariant($"{elapsed:F2},{car.SpeedKmh:F3},{ai.TargetSpeedKmh:F3},{plan.bumperGap:F3},{ai.BehaviourState},{car.Brake:F3},{car.Throttle:F3},{ai.RecoveryCount}"));
            }
            if(elapsed<32) return;
            finished=true;
            var result=new Result
            {
                sawFollowing=sawFollowing,sawPreparation=sawPreparation,sawEmergency=sawEmergency,
                stopHeld=stopHeld,resumed=resumed,collisions=contact.hits,recoveries=ai.RecoveryCount,
                minimumBumperGap=minimumGap,movingSpeedKmh=movingSpeed,stoppedSpeedKmh=stoppedSpeed,
                resumedSpeedKmh=resumedSpeed,duration=elapsed,frames=Time.frameCount
            };
            result.passed=sawFollowing && stopHeld && resumed && contact.hits==0 && ai.RecoveryCount==0 && minimumGap>1;
            Directory.CreateDirectory("Docs");
            File.WriteAllLines("Docs/Following_Phase2_DrivingTrace.csv",rows);
            File.WriteAllText("Docs/Following_Phase2_DrivingResults.json",JsonUtility.ToJson(result,true));
            car.ControlsEnabled=false; car.Throttle=0; car.Brake=1;
            Debug.Log("Phase 2 physics probe: "+JsonUtility.ToJson(result));
        }
    }

}
#endif
