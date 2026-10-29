using UnityEngine;
using System.Collections.Generic;

namespace Overdrive
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class OverdriveVehicle : MonoBehaviour
    {
        public WheelCollider[] wheels;
        public Transform[] visuals;
        [SerializeField, Min(0)] float driveTorque = 1350f;
        [SerializeField, Min(0)] float brakeTorque = 3200f;
        [SerializeField, Range(5, 45)] float steeringAngle = 30f;
        [SerializeField, Min(20)] float maximumForwardKmh = 160;
        [SerializeField, Min(5)] float maximumReverseKmh = 25;
        [SerializeField, Range(3,20)] float highSpeedSteering = 10;
        [SerializeField, Min(30)] float steeringFadeKmh = 140;
        [SerializeField, Min(0)] float downforce = .8f;
        [SerializeField, Min(0)] float aeroDrag;
        [SerializeField, Range(1,8)] int gearCount=5;
        [SerializeField, Min(10)] float gearStepKmh=35;
        [SerializeField] bool advancedPhysics;
        [SerializeField] bool usePowertrain;
        [SerializeField] bool manualTransmission;
        [SerializeField] Vector3 centerOfMass=new Vector3(0,-.35f,0);
        [SerializeField, Min(0)] float engineTorque=620;
        [SerializeField, Min(1)] float finalDrive=4.1f;
        [SerializeField] float[] gearRatios={4.2f,3.4f,2.8f,2.35f,2f,1.72f,1.5f,1.32f};
        [SerializeField, Min(100)] float idleRPM=4000;
        [SerializeField, Min(1000)] float redlineRPM=14500;
        [SerializeField, Min(0)] float shiftSeconds=.09f;
        [SerializeField, Range(.4f,.75f)] float frontBrakeBias=.58f;
        [SerializeField, Min(0)] float antiRollStiffness=6500;
        [SerializeField, Min(0)] float dragArea=.9f;
        [SerializeField, Min(0)] float liftArea=3.6f;
        [SerializeField] Vector3 centerOfPressure=new Vector3(0,0,-.2f);
        [SerializeField] bool tractionAssist=true;
        [SerializeField] bool absAssist=true;
        [Header("2025 reference power unit (approximate maps)")]
        [SerializeField] bool reference2025;
        [SerializeField, Min(1)] float icePowerKW=560;
        [SerializeField, Min(0)] float hybridPowerKW=120;
        [SerializeField, Min(0)] float startingFuelKg=10;
        public float FuelKg { get; private set; }
        public float HybridEnergyMJ { get; private set; }
        public float HybridPowerKW { get; private set; }
        float deployedThisLap, recoveredThisLap;
        int energyLap;
        CircuitProgress progress;
        VehicleSlipstream slipstream;
        public float SlipstreamStrength => slipstream ? slipstream.Strength : 0;
        public float Throttle { get; set; }
        public float Steering { get; set; }
        public float Brake { get; set; }
        public bool ClutchPressed {get;set;}
        public bool OvertakeRequested {get;set;}=true;
        public bool DRSOpen {get;private set;}
        public bool DRSEligible=>ControlsEnabled && SpeedKmh>=100 && Mathf.Abs(Steering)<.15f && Brake<.05f;
        public bool ToggleDRS(){if(DRSOpen){DRSOpen=false;return true;}if(!DRSEligible)return false;DRSOpen=true;return true;}
        public bool Reversing=>Body && (Throttle<-.01f || Vector3.Dot(Body.linearVelocity,transform.forward)<-.5f);
        public bool ControlsEnabled { get; set; } = true;
        public float SpeedKmh => Body ? Body.linearVelocity.magnitude * 3.6f : 0;
        public int Gear => usePowertrain?gearIndex+1:Mathf.Clamp(1 + (int)(SpeedKmh / gearStepKmh), 1, gearCount);
        public bool PowertrainEnabled => usePowertrain;
        public float CombustionPowerKW => usePowertrain && reference2025?icePowerKW:0;
        public float AerodynamicDragArea => advancedPhysics?dragArea:0;
        public bool ManualTransmission => manualTransmission;
        public int AutomaticUpshifts {get;private set;}
        public int AutomaticDownshifts {get;private set;}
        public float RoadEngineRPM => usePowertrain && Body?Mathf.Abs(Vector3.Dot(Body.linearVelocity,transform.forward))/(2*Mathf.PI*wheels[2].radius)*60*gearRatios[gearIndex]*finalDrive:0;
        public bool ShiftUp() => RequestShift(1);
        public bool ShiftDown() => RequestShift(-1);
        bool RequestShift(int direction)
        {
            if(!usePowertrain || !manualTransmission || !ControlsEnabled || !Body || Body.isKinematic || Time.timeScale<=0 || shiftRemaining>0) return false;
            int target=gearIndex+direction;
            if(target<0 || target>=gearRatios.Length) return false;
            float roadRPM=Mathf.Abs(Vector3.Dot(Body.linearVelocity,transform.forward))/(2*Mathf.PI*wheels[2].radius)*60;
            float axleRPM=(Mathf.Abs(wheels[2].rpm)+Mathf.Abs(wheels[3].rpm))*.5f;
            if(direction<0 && Mathf.Max(roadRPM,axleRPM)*gearRatios[target]*finalDrive>redlineRPM) return false;
            gearIndex=target; shiftRemaining=shiftSeconds;
            return true;
        }
        public float EngineRPM { get; private set; }
        public int GroundedWheels { get; private set; }
        public float DownforceNewtons { get; private set; }
        public float MaximumSlip { get; private set; }
        public bool TractionLimited { get; private set; }
        public bool ABSActive { get; private set; }
        public float FrontGripUsage { get; private set; }
        public bool BrakeLocking { get; private set; }
        public bool RearWheelspin { get; private set; }
        public float SurfaceGrip { get; private set; } = 1;
        public float MaximumForwardKmh => maximumForwardKmh;
        public float CurrentSteeringLimit => Mathf.Lerp(steeringAngle, highSpeedSteering, SpeedKmh / steeringFadeKmh);
        public Rigidbody Body { get; private set; }
        readonly Dictionary<Collider,TrackSurface> surfaces=new Dictionary<Collider,TrackSurface>();
        readonly WheelHit[] contacts=new WheelHit[4];
        readonly bool[] grounded=new bool[4];
        [SerializeField,HideInInspector] float[] baseForward=new float[4],baseSideways=new float[4];
        [SerializeField,HideInInspector] bool frictionCacheReady;
        int gearIndex;
        int automaticShiftDirection;
        float shiftRemaining;
        public void ResetDynamics()
        { gearIndex=0; shiftRemaining=0; automaticShiftDirection=0;AutomaticUpshifts=AutomaticDownshifts=0;EngineRPM=idleRPM; Throttle=Steering=0; Brake=1;
          FuelKg=startingFuelKg; HybridEnergyMJ=4; HybridPowerKW=0; DRSOpen=false; ClutchPressed=false; OvertakeRequested=false;
          deployedThisLap=recoveredThisLap=0; energyLap=0; }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            progress=GetComponent<CircuitProgress>();
            slipstream=GetComponent<VehicleSlipstream>();
            Body.centerOfMass = centerOfMass;
            if (wheels == null || wheels.Length != 4 || System.Array.Exists(wheels, w => w == null))
            { Debug.LogError("Vehicle needs four wheels: front left/right, rear left/right.", this); enabled = false; }
            if(!enabled) return;
            if(usePowertrain && (gearRatios==null || gearRatios.Length==0 || System.Array.Exists(gearRatios,g=>g<=0)))
            { Debug.LogError("Powertrain needs positive gear ratios.",this); enabled=false; return; }
            bool capture=!frictionCacheReady || baseForward==null || baseSideways==null || baseForward.Length!=4 || baseSideways.Length!=4;
            if(capture){baseForward=new float[4];baseSideways=new float[4];}
            for(int i=0;i<4;i++)
            {
                if(capture){baseForward[i]=wheels[i].forwardFriction.stiffness; baseSideways[i]=wheels[i].sidewaysFriction.stiffness;}
                wheels[i].ConfigureVehicleSubsteps(5,8,12);
            }
            frictionCacheReady=true;
            ResetDynamics(); Brake=0;
        }

        void FixedUpdate()
        {
            if (!ControlsEnabled) { Throttle=Steering=0; Brake=1; }
            if(!DRSEligible)DRSOpen=false;
            float steer = Mathf.Clamp(Steering, -1, 1) * CurrentSteeringLimit;
            float forwardSpeed = Vector3.Dot(Body.linearVelocity, transform.forward);
            float throttle = Mathf.Clamp(Throttle, -1, 1);
            bool changingDirection = throttle * forwardSpeed < -0.7f;
            GroundedWheels=0; MaximumSlip=0; SurfaceGrip=0; TractionLimited=ABSActive=false;
            FrontGripUsage=0; BrakeLocking=RearWheelspin=false;
            float rolling=0;
            for(int i=0;i<4;i++)
            {
                grounded[i]=wheels[i].GetGroundHit(out contacts[i]);
                float grip=1;
                if(grounded[i])
                {
                    GroundedWheels++; MaximumSlip=Mathf.Max(MaximumSlip,Mathf.Abs(contacts[i].forwardSlip));
                    if(i<2) FrontGripUsage=Mathf.Max(FrontGripUsage,Mathf.Abs(contacts[i].sidewaysSlip)/Mathf.Max(.001f,wheels[i].sidewaysFriction.extremumSlip));
                    if(Brake>.1f && SpeedKmh>15 && contacts[i].forwardSlip>.6f) BrakeLocking=true;
                    if(i>=2 && Throttle>.1f && Brake<.1f && Mathf.Abs(contacts[i].forwardSlip)>.3f) RearWheelspin=true;
                    var collider=contacts[i].collider;
                    if(!surfaces.TryGetValue(collider,out var surface)) { surface=collider.GetComponentInParent<TrackSurface>(); surfaces[collider]=surface; }
                    grip=surface?surface.grip:1; rolling+=surface?surface.rollingResistance:.012f;
                }
                SurfaceGrip+=grip;
                if(advancedPhysics)
                {
                    var f=wheels[i].forwardFriction; f.stiffness=baseForward[i]*grip; wheels[i].forwardFriction=f;
                    var s=wheels[i].sidewaysFriction; s.stiffness=baseSideways[i]*grip; wheels[i].sidewaysFriction=s;
                }
            }
            SurfaceGrip/=4;
            float motor=throttle*driveTorque*Mathf.Clamp01(1f-SpeedKmh/(throttle<0?maximumReverseKmh:maximumForwardKmh))/Mathf.Sqrt(Gear);
            if(usePowertrain)
            {
                shiftRemaining=Mathf.Max(0,shiftRemaining-Time.fixedDeltaTime);
                float wheelRPM=(Mathf.Abs(wheels[2].rpm)+Mathf.Abs(wheels[3].rpm))*.5f;
                float roadRPM=Mathf.Abs(forwardSpeed)/(2*Mathf.PI*wheels[2].radius)*60;
                if(!manualTransmission && ControlsEnabled && !Body.isKinematic && !ClutchPressed && throttle>=0 && shiftRemaining<=0)
                {
                    int target=AutomaticGearbox.TargetGear(gearIndex,roadRPM,SpeedKmh,throttle,Brake,gearRatios,finalDrive,redlineRPM);
                    automaticShiftDirection=System.Math.Sign(target-gearIndex);
                    if(automaticShiftDirection!=0)
                    {
                        gearIndex+=automaticShiftDirection;shiftRemaining=shiftSeconds;
                        if(automaticShiftDirection>0)AutomaticUpshifts++;else AutomaticDownshifts++;
                    }
                }
                float coupled=wheelRPM*gearRatios[gearIndex]*finalDrive;
                float engineTarget=coupled;
                if(!manualTransmission && automaticShiftDirection<0 && shiftRemaining>0)
                    engineTarget=Mathf.Max(engineTarget,roadRPM*gearRatios[gearIndex]*finalDrive);
                EngineRPM=Mathf.Lerp(EngineRPM,Mathf.Clamp(engineTarget,idleRPM,redlineRPM),1-Mathf.Exp(-15*Time.fixedDeltaTime));
                float curve=Mathf.Clamp01(1-Mathf.Pow((EngineRPM-10000)/11000,2));
                float shaftTorque=engineTorque*curve;
                if(reference2025)
                {
                    // Public limits, approximate ICE/harvest maps; not a proprietary team model.
                    if(progress && progress.CompletedLaps!=energyLap)
                    { energyLap=progress.CompletedLaps; deployedThisLap=recoveredThisLap=0; }
                    float omega=Mathf.Max(idleRPM,EngineRPM)*2*Mathf.PI/60;
                    shaftTorque=Mathf.Min(shaftTorque,icePowerKW*1000/omega);
                    float combustionKW=FuelKg>0?shaftTorque*omega/1000:0;
                    if(FuelKg<=0) shaftTorque=0;
                    float dt=Time.fixedDeltaTime;
                    float deploy=Mathf.Min(hybridPowerKW,Mathf.Min(HybridEnergyMJ,4-deployedThisLap)*1000/dt);
                    HybridPowerKW=throttle>0 && shiftRemaining<=0 && OvertakeRequested && !ClutchPressed?Mathf.Max(0,deploy)*Mathf.Clamp01(throttle):0;
                    float energy=HybridPowerKW*dt/1000;
                    HybridEnergyMJ-=energy; deployedThisLap+=energy;
                    shaftTorque+=HybridPowerKW*1000/omega/Mathf.Max(.001f,Mathf.Abs(throttle));
                    float harvest=Mathf.Min(120,Mathf.Clamp01(Brake)*Body.mass*Body.linearVelocity.sqrMagnitude*.002f);
                    float recovered=Mathf.Max(0,Mathf.Min(harvest*dt/1000,Mathf.Min(2-recoveredThisLap,4-HybridEnergyMJ)));
                    HybridEnergyMJ+=recovered; recoveredThisLap+=recovered;
                    FuelKg=Mathf.Max(0,FuelKg-100f/3600*dt*Mathf.Clamp01(throttle)*combustionKW/icePowerKW);
                    Body.mass=800+FuelKg;
                }
                motor=throttle*shaftTorque*(throttle<0?gearRatios[0]:gearRatios[gearIndex])*finalDrive*.92f*.5f;
                if(ClutchPressed || shiftRemaining>0 || (EngineRPM>=redlineRPM-100 && coupled>redlineRPM) ||
                   ((!reference2025 || throttle<0) && SpeedKmh>(throttle<0?maximumReverseKmh:maximumForwardKmh))) motor=0;
            }
            for (int i = 0; i < 4; i++)
            {
                float wheelSteer=steer;
                if(advancedPhysics && i<2 && Mathf.Abs(steer)>.01f)
                {
                    float wheelbase=Mathf.Abs(wheels[0].transform.localPosition.z-wheels[2].transform.localPosition.z);
                    float track=Mathf.Abs(wheels[0].transform.localPosition.x-wheels[1].transform.localPosition.x);
                    float radius=wheelbase/Mathf.Tan(Mathf.Abs(steer)*Mathf.Deg2Rad);
                    bool inner=(steer>0 && i==1)||(steer<0 && i==0);
                    wheelSteer=Mathf.Sign(steer)*Mathf.Atan(wheelbase/Mathf.Max(.2f,radius+(inner?-track*.5f:track*.5f)))*Mathf.Rad2Deg;
                }
                wheels[i].steerAngle = i < 2 ? wheelSteer : 0;
                float brake=changingDirection ? brakeTorque : Mathf.Clamp01(Brake)*brakeTorque;
                if(advancedPhysics)
                {
                    brake*=i<2?frontBrakeBias*2:(1-frontBrakeBias)*2;
                    if(absAssist && brake>0 && grounded[i] && SpeedKmh>15)
                    {
                        brake=Mathf.Min(brake,contacts[i].force*wheels[i].radius*wheels[i].forwardFriction.stiffness*.9f);
                        // Unity forward slip is positive under braking, negative under acceleration.
                        if(contacts[i].forwardSlip>.3f) { brake*=.45f; ABSActive=true; }
                    }
                    if(usePowertrain && i>=2 && Mathf.Abs(throttle)<.01f && SpeedKmh>3) brake+=130;
                }
                wheels[i].brakeTorque=brake;
                // Rear axle only; reduce torque with speed rather than overwriting velocity.
                wheels[i].motorTorque = i >= 2 && !changingDirection
                    ? motor : 0;
                if(advancedPhysics && tractionAssist && i>=2 && grounded[i])
                {
                    float feedback=Mathf.Clamp(.3f/Mathf.Max(.3f,Mathf.Abs(contacts[i].forwardSlip)),.12f,1);
                    float limit=contacts[i].force*wheels[i].radius*wheels[i].forwardFriction.stiffness*.8f*feedback;
                    if(Mathf.Abs(wheels[i].motorTorque)>limit) { wheels[i].motorTorque=Mathf.Clamp(wheels[i].motorTorque,-limit,limit); TractionLimited=true; }
                }
            }
            if(advancedPhysics)
            {
                float q=.5f*1.225f*Body.linearVelocity.sqrMagnitude;
                DownforceNewtons=.5f*1.225f*Mathf.Pow(Mathf.Max(0,forwardSpeed),2)*liftArea*(DRSOpen?.85f:1)*(slipstream?slipstream.DownforceMultiplier:1);
                Body.AddForceAtPosition(-transform.up*DownforceNewtons,transform.TransformPoint(centerOfPressure));
                Body.AddForce(-Body.linearVelocity.normalized*q*dragArea*(DRSOpen?.75f:1)*(slipstream?slipstream.DragMultiplier:1));
                if(GroundedWheels>0)
                {
                    float resistance=Body.mass*Physics.gravity.magnitude*(rolling/GroundedWheels);
                    float capped=Mathf.Min(resistance,Body.mass*Body.linearVelocity.magnitude/Time.fixedDeltaTime);
                    Body.AddForce(-Body.linearVelocity.normalized*capped);
                }
                AntiRoll(0,1); AntiRoll(2,3);
            }
            else
            {
                DownforceNewtons=Body.linearVelocity.sqrMagnitude*downforce;
                Body.AddForce(-transform.up*DownforceNewtons);
                Body.AddForce(-Body.linearVelocity*Body.linearVelocity.magnitude*aeroDrag);
            }
        }
        void AntiRoll(int left,int right)
        {
            float a=1,b=1;
            if(grounded[left]) a=(-wheels[left].transform.InverseTransformPoint(contacts[left].point).y-wheels[left].radius)/wheels[left].suspensionDistance;
            if(grounded[right]) b=(-wheels[right].transform.InverseTransformPoint(contacts[right].point).y-wheels[right].radius)/wheels[right].suspensionDistance;
            float force=(Mathf.Clamp01(a)-Mathf.Clamp01(b))*antiRollStiffness;
            if(grounded[left]) Body.AddForceAtPosition(-transform.up*force,wheels[left].transform.position);
            if(grounded[right]) Body.AddForceAtPosition(transform.up*force,wheels[right].transform.position);
        }

        void LateUpdate()
        {
            if (visuals == null || wheels == null) return;
            for (int i = 0; i < Mathf.Min(visuals.Length, wheels.Length); i++)
                if (visuals[i]) { wheels[i].GetWorldPose(out var p, out var q); visuals[i].SetPositionAndRotation(p, q); }
        }
    }
}
