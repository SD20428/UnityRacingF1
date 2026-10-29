#if UNITY_EDITOR
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
namespace Overdrive
{
    // Temporary physical test rig, excluded from player builds. No prescribed forces/velocities after setup.
    [DefaultExecutionOrder(-50)]
    public sealed class VehicleHandlingProbe : MonoBehaviour
    {
        [Serializable] public class Result
        {
            public string name;
            public float seconds, speed, maximumSpeed, distance, time100=-1,time200=-1,time300=-1;
            public float lateralG,yawRadius,bodySlipDegrees,frontSlip,rearSlip,frontLongSlip,rearLongSlip,steerDegrees,minimumLoad=1e9f;
            public int samples,airborne;
            public float lastBrake,lastThrottle;
            public int pressedBrakeSamples;
        }
        [Serializable] public class Report { public string label; public bool manual,tractionAssist,absAssist; public List<Result> tests=new List<Result>(); }
        public string label="baseline";
        public bool accelerationOnly;
        public int firstCase=0,lastCase=14;
        public static string LastReport="Not run";
        RaceManager race; OverdriveVehicle car,original; PlayerVehicleInput input; CircuitProgress progress;
        GameObject pad; bool opponentActive,oldRecovery,oldInput;
        UnityEngine.InputSystem.Keyboard testKeyboard;
        Report report; Result result;
        int test=-1; float began,started,shiftCooldown; Vector3 origin,lastVelocity; bool braking;
        readonly string[] names={"acceleration","brake100","brake200","brake300","circle80_3deg","circle80_minus3deg","circle120_3deg","circle180_3deg","circle80_7deg","circle80_fullLock","circle80_fullThrottle","circle80_keyboard","brake100_keyboard","brake200_keyboard","circle180_2deg"};
        readonly float[] initial={0,100,200,300,80,80,120,180,80,80,80,80,100,200,180};
        void Update()
        {
            // Keep fixture input deterministic even while the user types in another desktop app.
            if(testKeyboard!=null && testKeyboard.added)testKeyboard.MakeCurrent();
        }
        void Start()
        {
            race=FindFirstObjectByType<RaceManager>();car=original=race.player; input=car.GetComponent<PlayerVehicleInput>();progress=car.GetComponent<CircuitProgress>();
            oldInput=input.enabled; input.enabled=false; oldRecovery=progress.AutoRecoverBelowTerrain; progress.AutoRecoverBelowTerrain=false;
            opponentActive=race.opponent.gameObject.activeSelf;
            race.BeginRace(); race.opponent.gameObject.SetActive(false);
            report=new Report{label=label,manual=car.ManualTransmission};
            var serialized=new UnityEditor.SerializedObject(car);report.tractionAssist=serialized.FindProperty("tractionAssist").boolValue;report.absAssist=serialized.FindProperty("absAssist").boolValue;
            pad=GameObject.CreatePrimitive(PrimitiveType.Cube);pad.name="Temporary handling laboratory";pad.transform.position=new Vector3(3000,-.1f,2000);pad.transform.localScale=new Vector3(1400,.2f,6500);pad.AddComponent<TrackSurface>();
            began=Time.time;LastReport="Running "+label;
        }
        void Next()
        {
            if(testKeyboard!=null && testKeyboard.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(testKeyboard);
            testKeyboard=null;
            if(result!=null)
            {
                result.seconds=Time.time-started;result.maximumSpeed=Mathf.Max(result.maximumSpeed,car.SpeedKmh);result.distance=Vector3.Distance(origin,car.Body.position);
                if(result.samples>0) {float n=result.samples;result.speed/=n;result.lateralG/=n;result.yawRadius/=n;result.bodySlipDegrees/=n;result.frontSlip/=n;result.rearSlip/=n;result.frontLongSlip/=n;result.rearLongSlip/=n;result.steerDegrees/=n;}
                report.tests.Add(result);
            }
            test=test<0?firstCase:test+1;
            if(test==names.Length || test>lastCase || (accelerationOnly && test==1))
            {
                LastReport=JsonUtility.ToJson(report,true);File.WriteAllText("Docs/Handling_"+label+".json",LastReport);
                if(car!=original)Destroy(car.gameObject);
                race.opponent.gameObject.SetActive(opponentActive);input.enabled=oldInput;progress.AutoRecoverBelowTerrain=oldRecovery;race.MainMenu();Destroy(pad);Destroy(this);return;
            }
            result=new Result{name=names[test]};started=Time.time;shiftCooldown=0;braking=false;
            // Fresh physical actor per case: teleporting a previously loaded WheelCollider retains
            // angular/suspension state and can launch a car, invalidating a comparison.
            if(car!=original)Destroy(car.gameObject);
            original.Body.constraints=RigidbodyConstraints.FreezeAll;
            var copy=Instantiate(original.gameObject);
            foreach(var probe in copy.GetComponents<VehicleHandlingProbe>())DestroyImmediate(probe);
            copy.GetComponent<PlayerVehicleInput>().enabled=false;copy.GetComponent<CircuitProgress>().enabled=false;
            car=copy.GetComponent<OverdriveVehicle>();copy.name="Handling test car "+names[test];car.Body.constraints=RigidbodyConstraints.None;car.ControlsEnabled=true;
            car.transform.SetPositionAndRotation(new Vector3(3000,.47f,0),Quaternion.identity);
            car.ResetDynamics();car.Body.position=origin=new Vector3(3000,.47f,0);car.Body.rotation=Quaternion.identity;
            car.Body.linearVelocity=Vector3.forward*(test<4 || test==12 || test==13?0:initial[test]/3.6f);car.Body.angularVelocity=Vector3.zero;lastVelocity=car.Body.linearVelocity;
            if(test==11)
            {
                copy.GetComponent<PlayerVehicleInput>().enabled=true;
                testKeyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(testKeyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.D));
            }
            Physics.SyncTransforms();
        }
        void FixedUpdate()
        {
            if(!race || race.State!=RaceState.Racing)return;
            if(test<0) {Next();return;} if(test>=names.Length)return;
            float t=Time.time-started;
            float speed=car.SpeedKmh;
            result.lastBrake=car.Brake;result.lastThrottle=car.Throttle;
            if(testKeyboard!=null && testKeyboard.sKey.isPressed)result.pressedBrakeSamples++;
            bool brakeCase=(test>=1 && test<=3) || test==12 || test==13;
            result.maximumSpeed=Mathf.Max(result.maximumSpeed,speed);
            if(test==0)
            {
                if(result.time100<0 && speed>=100) result.time100=t;
                if(result.time200<0 && speed>=200) result.time200=t;
                if(result.time300<0 && speed>=300) result.time300=t;
            }
            if(brakeCase && !braking && speed>=initial[test])
            {
                braking=true;started=Time.time;origin=car.Body.position;result.maximumSpeed=speed;t=0;
                if(test>=12)
                {
                    car.GetComponent<PlayerVehicleInput>().enabled=true;
                    testKeyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
                    UnityEngine.InputSystem.InputSystem.QueueStateEvent(testKeyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.S));
                }
            }
            if((test==0 && t>=40) || (brakeCase && ((braking && (speed<1 || t>10)) || (!braking && t>50))) || (test>=4 && !brakeCase && t>=10)) {Next();return;}
            float wheelRPM=speed/3.6f/(2*Mathf.PI*car.wheels[2].radius)*60;
            float[] ratios={4.2f,3.4f,2.8f,2.35f,2,1.72f,1.5f,1.32f};
            shiftCooldown=Mathf.Max(0,shiftCooldown-Time.fixedDeltaTime);
            if(car.ManualTransmission && shiftCooldown<=0)
            {
                if(wheelRPM*ratios[car.Gear-1]*4.1f>12500 && car.ShiftUp()) shiftCooldown=.12f;
                else if(car.Gear>1 && wheelRPM*ratios[car.Gear-1]*4.1f<6500 && car.ShiftDown()) shiftCooldown=.12f;
            }
            if(test!=11)car.Steering=0;
            if(!(brakeCase && test>=12 && braking)) {car.Brake=0;car.Throttle=1;}
            if(brakeCase && test<4 && braking){car.Throttle=0;car.Brake=1;}
            if(test>=4 && !brakeCase)
            {
                float angle=test==5?-3:test==8?7:test==14?2:3;
                if(test!=11)car.Steering=test==9?1:angle/car.CurrentSteeringLimit;
                float error=initial[test]-speed;
                car.Throttle=test==10?1:Mathf.Clamp01(.08f+error*.12f);
                car.Brake=test==10?0:Mathf.Clamp01((-error-1)*.08f);
            }
            if(test==0 || (brakeCase && braking) || (test>=4 && !brakeCase && t>3))
            {
                result.samples++;result.speed+=speed;
                var acceleration=(car.Body.linearVelocity-lastVelocity)/Time.fixedDeltaTime;
                result.lateralG+=Mathf.Abs(Vector3.Dot(acceleration,car.transform.right))/9.81f;
                result.yawRadius+=speed/3.6f/Mathf.Max(.01f,Mathf.Abs(car.Body.angularVelocity.y));
                var local=car.transform.InverseTransformDirection(car.Body.linearVelocity);
                result.bodySlipDegrees+=Mathf.Abs(Mathf.Atan2(local.x,Mathf.Max(.1f,local.z))*Mathf.Rad2Deg);
                if(car.GroundedWheels==0)result.airborne++;
                for(int i=0;i<4;i++)if(car.wheels[i].GetGroundHit(out var hit))
                {
                    result.minimumLoad=Mathf.Min(result.minimumLoad,hit.force);
                    if(i<2){result.frontSlip+=Mathf.Abs(hit.sidewaysSlip)*.5f;result.frontLongSlip+=Mathf.Abs(hit.forwardSlip)*.5f;}
                    else {result.rearSlip+=Mathf.Abs(hit.sidewaysSlip)*.5f;result.rearLongSlip+=Mathf.Abs(hit.forwardSlip)*.5f;}
                }
                result.steerDegrees+=(Mathf.Abs(car.wheels[0].steerAngle)+Mathf.Abs(car.wheels[1].steerAngle))*.5f;
            }
            lastVelocity=car.Body.linearVelocity;
        }
    }
}
#endif
