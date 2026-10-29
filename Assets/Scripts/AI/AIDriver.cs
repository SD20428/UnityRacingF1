using UnityEngine;
namespace Overdrive
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(OverdriveVehicle), typeof(CircuitProgress))]
    public sealed class AIDriver : MonoBehaviour
    {
        public WaypointPath path;
        public AIDifficulty difficulty;
        [Header("Autonomous racing")]
        public bool enablePerception = true;
        public bool enableFollowing = true;
        public AIFollowingSettings following = new AIFollowingSettings();
        public VehiclePerception Perception { get; private set; }
        public AIFollowingPlan FollowingPlan { get; private set; }
        public AIBehaviourState BehaviourState => passLead?AIBehaviourState.OVERTAKING:FollowingActive ? decision.State : AIBehaviourState.RACING;
        public bool FollowingActive => enableFollowing && enablePerception;
        readonly AIFollowingDecision decision = new AIFollowingDecision();
        bool wasFollowing;
        [Range(-4,4)] public float preferredLane;
        [Range(.8f,1.1f)] public float pace=1;
        [SerializeField, Min(.1f)] float wheelbase=2.7f;
        public int RecoveryCount { get; private set; }
        public float FinishSeconds { get; private set; } = -1;
        public float TargetSpeedKmh { get; private set; }
        public bool Finished => FinishSeconds>=0;
        public void CompleteRace(float seconds)
        {
            if(Finished)return;
            passLead=null;
            FinishSeconds=seconds;TargetSpeedKmh=0;
            if(vehicle)Hold();
        }
        OverdriveVehicle vehicle;
        CircuitProgress progress;
        float started, idleSeconds, noProgressSeconds, penaltyUntil;
        int lastCheckpoint;
        RaceManager race;float lane;
        DrivingSpeedProfile speedProfile;
        AIDifficulty plannedDifficulty;
        WaypointPath plannedPath;
        float plannedPace,liftUntil;
        bool Professional => difficulty && difficulty.professionalDriver;
        public int OvertakeCount { get; private set; }
        OverdriveVehicle passLead;float passLane,passDeadline;
        float standardSensorRange=70;bool professionalSensorRange;
        readonly AIFollowingSettings professionalFollowing=new AIFollowingSettings {timeHeadway=1.05f,comfortableDeceleration=8};
        AIFollowingSettings FollowingSettings => Professional?professionalFollowing:following;
        void BuildSpeedProfile()
        {
            if(!path || !path.Valid || !difficulty)return;
            // Preserve the measured F1 guide limits, with difficulty controlling pace/grip.
            var profile=new DrivingGuideTuning {
                baseLateralAcceleration=Mathf.Min(14,difficulty.lateralAcceleration),
                maximumLateralAcceleration=Mathf.Min(22,difficulty.lateralAcceleration*22f/14f),
                aeroLateralCoefficient=difficulty.maximumSpeedKmh>160?.0006f:0,
                brakingDeceleration=difficulty.maximumSpeedKmh>160?12:8,
                exitAcceleration=3.5f,
                maximumSpeedKmh=Mathf.Min(difficulty.maximumSpeedKmh,vehicle?vehicle.MaximumForwardKmh:difficulty.maximumSpeedKmh)
            };
            if(Professional)
            {
                var calibrated=difficulty.racingProfile??new DrivingGuideTuning();
                float laneReserve=1-Mathf.Min(.12f,Mathf.Abs(preferredLane)*.04f);
                profile=new DrivingGuideTuning {
                    baseLateralAcceleration=calibrated.baseLateralAcceleration*laneReserve,
                    maximumLateralAcceleration=calibrated.maximumLateralAcceleration*laneReserve,
                    aeroLateralCoefficient=calibrated.aeroLateralCoefficient,
                    brakingDeceleration=calibrated.brakingDeceleration,exitAcceleration=calibrated.exitAcceleration,
                    maximumSpeedKmh=Mathf.Min(difficulty.maximumSpeedKmh,vehicle?vehicle.MaximumForwardKmh:difficulty.maximumSpeedKmh)
                };
            }
            speedProfile=new DrivingSpeedProfile(path,profile,pace);
            plannedDifficulty=difficulty;plannedPath=path;plannedPace=pace;
        }
        public void ResetForRace()
        {
            RecoveryCount=0; FinishSeconds=-1; TargetSpeedKmh=0;
            idleSeconds=noProgressSeconds=penaltyUntil=0;
            started=Time.time; lastCheckpoint=0;
            decision.Reset(Time.fixedTime); FollowingPlan=default;
            liftUntil=0;lane=preferredLane;BuildSpeedProfile();
            passLead=null;OvertakeCount=0;
            if(progress)progress.AutoRecoverBelowTerrain=false;
            if(vehicle){vehicle.OvertakeRequested=true;vehicle.ClutchPressed=false;if(vehicle.DRSOpen)vehicle.ToggleDRS();}
        }
        void Start()
        {
            vehicle=GetComponent<OverdriveVehicle>(); progress=GetComponent<CircuitProgress>();
            race=FindFirstObjectByType<RaceManager>();lane=preferredLane;
            var player=GetComponent<PlayerVehicleInput>(); if(player) player.enabled=false;
            if(!path || !path.Valid || !difficulty || !progress.enabled || path.points.Length!=progress.checkpoints.Length)
            { Debug.LogError("AI needs a valid path, matching progress checkpoints, and difficulty.",this); enabled=false; return; }
            for(int i=0;i<path.points.Length;i++)
                if(path.points[i]!=progress.checkpoints[i]) { Debug.LogError("AI path must match ordered checkpoint references.",this); enabled=false; return; }
            progress.AutoRecoverBelowTerrain=false;
            if(vehicle.wheels.Length==4)
                wheelbase=Mathf.Abs(vehicle.wheels[0].transform.localPosition.z-vehicle.wheels[2].transform.localPosition.z);
            BuildSpeedProfile();
            started=Time.time; lastCheckpoint=progress.LastCheckpoint;
            // Attach only to AI; the player is read through the existing race registry.
            Perception=GetComponent<VehiclePerception>();
            if(!Perception) Perception=gameObject.AddComponent<VehiclePerception>();
        }
        bool PlanPass(ref float desiredLane,out bool separated)
        {
            separated=false;
            if(!Professional || !FollowingActive || !Perception || !Perception.ReferenceValid)return false;
            if(passLead)
            {
                bool observed=false;RelativeVehicleObservation lead=default;
                foreach(var observation in Perception.Observations)if(observation.vehicle==passLead){lead=observation;observed=true;break;}
                if(!passLead.isActiveAndEnabled || !observed || Time.fixedTime>passDeadline){passLead=null;return false;}
                if(lead.longitudinal < -14){OvertakeCount++;passLead=null;return false;}
                desiredLane=passLane;
                bool clear=F1OvertakePlanner.ClearLane(Perception.Observations,passLead,Perception.SelfTrack.lateral,passLane,vehicle.SpeedKmh/3.6f);
                separated=clear && Mathf.Abs(lead.lateral)>3.2f;
                // A newly occupied destination stops the lane change; following still owns braking.
                if(!clear)desiredLane=lane;
                return clear;
            }
            if(!Perception.HasLead || path.CurvatureAhead(progress.NextCheckpoint,180)>.002f)return false;
            var front=Perception.Lead;
            float closing=Mathf.Max(0,front.closingSpeed);
            if(front.longitudinal<18+closing*3 || front.longitudinal>180 ||
                difficulty.maximumSpeedKmh*pace-front.velocity.magnitude*3.6f<8)return false;
            float leadLateral=Perception.SelfTrack.lateral+front.lateral;
            float candidate=leadLateral>=0?-3.25f:3.25f;
            if(!F1OvertakePlanner.ClearLane(Perception.Observations,front.vehicle,Perception.SelfTrack.lateral,candidate,vehicle.SpeedKmh/3.6f))return false;
            passLead=front.vehicle;passLane=desiredLane=candidate;passDeadline=Time.fixedTime+12;
            return true;
        }
        void FixedUpdate()
        {
            var following=FollowingSettings;
            if(Perception && Professional && !professionalSensorRange)
            { standardSensorRange=Perception.sensorRange;Perception.sensorRange=Mathf.Max(220,standardSensorRange);professionalSensorRange=true; }
            else if(Perception && !Professional && professionalSensorRange)
            { Perception.sensorRange=standardSensorRange;professionalSensorRange=false; }
            if(FollowingActive!=wasFollowing)
            { decision.Reset(Time.fixedTime); wasFollowing=FollowingActive; }
            if(!vehicle.ControlsEnabled) { decision.Reset(Time.fixedTime); Hold(); return; }
            if(progress.TwoLapsComplete && !Finished)
            { FinishSeconds=Time.time-started; Debug.Log($"AI {difficulty.name} finished: {FinishSeconds:F2}s, recoveries={RecoveryCount}",this); }
            if(Finished || Time.time<penaltyUntil) { Hold(); return; }
            if(progress.LastCheckpoint!=lastCheckpoint)
            { lastCheckpoint=progress.LastCheckpoint; noProgressSeconds=0; }
            else noProgressSeconds+=Time.fixedDeltaTime;
            // A valid, slow lead is a traffic queue, not a stuck-car recovery request.
            bool sensorReady=Perception && Perception.isActiveAndEnabled && Perception.ReferenceValid && following!=null && following.Valid &&
                Time.fixedTime-Perception.LastSampleTime<=following.maximumObservationAge;
            bool queued=FollowingActive && sensorReady &&
                Perception.HasLead &&
                Perception.Lead.vehicle && Perception.Lead.vehicle.isActiveAndEnabled &&
                Perception.Lead.longitudinal<following.standstillGap+Perception.SelfBodySize.z+vehicle.SpeedKmh/3.6f*following.timeHeadway+5;
            bool safeWait=FollowingActive && (queued || !sensorReady);
            if(safeWait) noProgressSeconds=0;
            bool inverted=Vector3.Dot(transform.up,Vector3.up)<.25f;
            idleSeconds=vehicle.SpeedKmh<3 && (!safeWait || inverted) ? idleSeconds+Time.fixedDeltaTime : 0;
            if(transform.position.y < -5 || idleSeconds>difficulty.stuckSeconds || noProgressSeconds>20 || (inverted && idleSeconds>2))
            {
                if(FollowingActive) decision.Recover(Time.fixedTime);
                Hold();
                if(progress.TryRecover())
                {
                    RecoveryCount++; penaltyUntil=Time.time+difficulty.recoveryPenaltySeconds;
                    idleSeconds=noProgressSeconds=0;
                }
                return;
            }
            if(FollowingActive && (!sensorReady || !path))
            {
                FollowingPlan=decision.Evaluate(default,following,Time.fixedTime);
                TargetSpeedKmh=0; Hold(); return;
            }
            float trafficLimit=float.PositiveInfinity;float desiredLane=preferredLane;bool passing=false;
            bool separated=false;
            if(Professional && sensorReady)passing=PlanPass(ref desiredLane,out separated);
            if(Professional && sensorReady && !passing &&
                !F1OvertakePlanner.ClearLane(Perception.Observations,null,Perception.SelfTrack.lateral,desiredLane,vehicle.SpeedKmh/3.6f))desiredLane=lane;
            if(!FollowingActive && race && race.FieldSize>2)
            {
                float nearest=float.PositiveInfinity;RaceManager.Racer lead=null;
                foreach(var other in race.Racers)
                {
                    if(other.vehicle==vehicle || !other.vehicle.gameObject.activeInHierarchy)continue;
                    var relative=transform.InverseTransformPoint(other.vehicle.transform.position);
                    if(relative.z>0 && relative.z<nearest && Mathf.Abs(relative.x)<2.5f){nearest=relative.z;lead=other;}
                }
                if(lead!=null && nearest<18+vehicle.SpeedKmh*.18f)
                {
                    float candidate=preferredLane>=0?-3:3;bool clear=true;
                    foreach(var other in race.Racers)
                    {
                        if(other.vehicle==vehicle)continue;var relative=transform.InverseTransformPoint(other.vehicle.transform.position);
                        float lateral=Vector3.Dot(other.vehicle.transform.position-path.Position(progress.NextCheckpoint),path.points[progress.NextCheckpoint].right);
                        if(Mathf.Abs(relative.z)<14 && Mathf.Abs(lateral-candidate)<2.7f){clear=false;break;}
                    }
                    if(clear && nearest>7){desiredLane=candidate;passing=true;}
                    trafficLimit=Mathf.Max(0,lead.vehicle.SpeedKmh+(nearest-9)*2);
                    // Steering around a stationary car needs forward motion; stopping at the
                    // following gap with a changed steering target alone creates a deadlock.
                    if(passing)trafficLimit=Mathf.Max(14,trafficLimit);
                }
            }
            lane=Mathf.MoveTowards(lane,desiredLane,Time.fixedDeltaTime*(Professional?1.8f:1.2f));
            float lookahead=difficulty.lookaheadBase+vehicle.SpeedKmh*difficulty.lookaheadPerKmh;
            if(difficulty.maximumSpeedKmh>160)lookahead=Mathf.Min(lookahead,10+vehicle.SpeedKmh*.10f);
            if(passing)lookahead=Mathf.Min(lookahead,10+vehicle.SpeedKmh*.06f);
            var target=path.SampleAhead(transform.position,progress.NextCheckpoint,lookahead,out var targetRight)+targetRight*lane;
            var local=transform.InverseTransformPoint(target);
            float angle=Mathf.Atan2(2*wheelbase*local.x,Mathf.Max(.01f,local.x*local.x+local.z*local.z))*Mathf.Rad2Deg;
            float desiredSteer=Mathf.Clamp(angle/vehicle.CurrentSteeringLimit,-1,1);
            if(Professional)
            {
                float speed=vehicle.SpeedKmh/3.6f;
                float acceleration=9.81f*(1.55f+1.4f*Mathf.Pow(vehicle.SpeedKmh/200,2));
                float limit=Mathf.Clamp01(Mathf.Atan(wheelbase*acceleration/Mathf.Max(1,speed*speed))*Mathf.Rad2Deg/vehicle.CurrentSteeringLimit);
                desiredSteer=Mathf.Clamp(desiredSteer,-limit,limit);
            }
            vehicle.Steering=Mathf.MoveTowards(vehicle.Steering,desiredSteer,difficulty.steeringResponse*Time.fixedDeltaTime);
            if(speedProfile==null || plannedDifficulty!=difficulty || plannedPath!=path || plannedPace!=pace)BuildSpeedProfile();
            int segment=progress.LastCheckpoint;
            TargetSpeedKmh=Mathf.Min(speedProfile.SpeedKmh(segment,path.SegmentFraction(transform.position,segment)),trafficLimit);
            if(FollowingActive)
            {
                var input=new AIFollowingInput
                {
                    referenceValid=Perception && Perception.isActiveAndEnabled && Perception.ReferenceValid,
                    speedMps=vehicle.SpeedKmh/3.6f, cruiseKmh=TargetSpeedKmh,
                    observationAge=Perception?Time.fixedTime-Perception.LastSampleTime:float.PositiveInfinity,
                    selfLength=Perception?Perception.SelfBodySize.z:0
                };
                if(Perception && Perception.HasLead && Perception.Lead.vehicle && Perception.Lead.vehicle.isActiveAndEnabled)
                {
                    var lead=Perception.Lead;
                    input.hasLead=true; input.centreDistance=lead.longitudinal; input.leadLength=lead.bodySize.z;
                    input.leadSpeedMps=Vector3.Dot(lead.velocity,lead.track.tangent);
                    // Refresh self speed between sensor samples; lead velocity remains a timestamped observation.
                    input.closingSpeedMps=Vector3.Dot(vehicle.Body.linearVelocity,Perception.SelfTrack.tangent)-input.leadSpeedMps;
                }
                FollowingPlan=decision.Evaluate(input,following,Time.fixedTime);
                bool anotherLead=Perception.HasLead && Perception.Lead.vehicle!=passLead;
                if(!passing || !separated || anotherLead || FollowingPlan.emergencyBrake)TargetSpeedKmh=Mathf.Min(TargetSpeedKmh,FollowingPlan.speedLimitKmh);
            }
            float error=TargetSpeedKmh-vehicle.SpeedKmh;
            // Coast near the ceiling; progressively apply/release braking instead of alternating full pedals.
            vehicle.Brake=Mathf.Min(.35f,Mathf.Clamp01((-error-2)/14));
            vehicle.Throttle=vehicle.Brake>.001f?0:Mathf.Clamp01(.12f+error/8);
            if(vehicle.SpeedKmh>50 && error>=-2 && error<=18)vehicle.Throttle=0;
            var velocity=transform.InverseTransformDirection(vehicle.Body.linearVelocity);
            float slip=Mathf.Abs(Mathf.Atan2(velocity.x,Mathf.Max(1,velocity.z))*Mathf.Rad2Deg);
            if(vehicle.SpeedKmh>50 && Mathf.Abs(vehicle.Steering)>.035f &&
                (slip>4 || vehicle.RearWheelspin || vehicle.FrontGripUsage>1.1f))liftUntil=Time.fixedTime+.15f;
            if(Time.fixedTime<liftUntil)vehicle.Throttle=0;
            if(vehicle.BrakeLocking)vehicle.Brake*=.65f;
            if(Professional)
            {
                float brakeLimit=Mathf.Lerp(.2f,1,Mathf.Pow(Mathf.Clamp01(vehicle.SpeedKmh/300),2));
                vehicle.Brake=Mathf.Min(brakeLimit,Mathf.Clamp01((-error-1)/12));
                vehicle.Throttle=vehicle.Brake>.001f?0:Mathf.Clamp01(.12f+error/10);
                if(vehicle.SpeedKmh>80 && Mathf.Abs(vehicle.Steering)>.04f)vehicle.Throttle=Mathf.Min(.45f,vehicle.Throttle);
                if(Time.fixedTime<liftUntil)vehicle.Throttle=0;
                if(vehicle.BrakeLocking)vehicle.Brake*=.6f;
                bool straight=path.CurvatureAhead(progress.NextCheckpoint,200)<.001f;
                vehicle.OvertakeRequested=vehicle.SpeedKmh>100 && (straight || passing) && vehicle.Brake<.05f;
                bool drs=straight && vehicle.SpeedKmh>180 && vehicle.DRSEligible && error>2;
                if(vehicle.DRSOpen!=drs)vehicle.ToggleDRS();
            }
            if(vehicle.ManualTransmission)
            {
                if(vehicle.RoadEngineRPM>12500)vehicle.ShiftUp();
                else if(vehicle.Gear>1 && vehicle.RoadEngineRPM<6500)vehicle.ShiftDown();
            }
            if(FollowingActive && (FollowingPlan.emergencyBrake || TargetSpeedKmh<.1f && vehicle.SpeedKmh<3))
            { vehicle.Throttle=0; vehicle.Brake=1; }
        }
        void Hold() { vehicle.Throttle=vehicle.Steering=0; vehicle.Brake=1; }
        void OnDisable() { if(vehicle) Hold(); if(progress) progress.AutoRecoverBelowTerrain=true; }
    }
}
