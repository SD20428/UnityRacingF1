using UnityEngine;
using UnityEngine.InputSystem;
namespace Overdrive
{
    [RequireComponent(typeof(OverdriveVehicle), typeof(CircuitProgress))]
    public sealed class PlayerVehicleInput : MonoBehaviour
    {
        [SerializeField, Min(.1f)] float steeringResponse=12;
        [SerializeField, Min(.1f)] float pedalResponse=4;
        [SerializeField] bool speedSensitiveSteering=true;
        [SerializeField, Min(.05f)] float steeringTravelSeconds=.22f;
        [SerializeField] bool keyboardBrakeTravel=true;
        OverdriveVehicle vehicle;
        CircuitProgress progress;
        bool launchClutch,overTaking,usingGamepad;
        public string ControlNotice {get;private set;}
        public float NoticeUntil {get;private set;}
        public float SteeringInputLimit
        {
            get
            {
                if(!vehicle || !vehicle.PowertrainEnabled || !speedSensitiveSteering) return 1;
                float v=vehicle.SpeedKmh/3.6f;
                float wheelbase=Mathf.Abs(vehicle.wheels[0].transform.localPosition.z-vehicle.wheels[2].transform.localPosition.z);
                // Virtual steering-wheel travel, not added lateral force or slip feedback.
                float lateralAcceleration=9.81f*(1.55f+1.4f*Mathf.Pow(vehicle.SpeedKmh/200,2));
                float angle=Mathf.Atan(wheelbase*lateralAcceleration/Mathf.Max(1,v*v))*Mathf.Rad2Deg;
                return Mathf.Clamp01(angle/vehicle.CurrentSteeringLimit);
            }
        }
        public float KeyboardBrakeLimit => vehicle && vehicle.PowertrainEnabled && keyboardBrakeTravel
            ? Mathf.Lerp(.2f,1,Mathf.Pow(Mathf.Clamp01(vehicle.SpeedKmh/300),2)):1;
        void Awake() { vehicle = GetComponent<OverdriveVehicle>(); progress = GetComponent<CircuitProgress>(); }
        void Update()
        {
            var key = Keyboard.current;
            if (!vehicle.ControlsEnabled) { vehicle.Throttle=vehicle.Steering=0;if(Time.timeScale>0){overTaking=false;vehicle.OvertakeRequested=false;launchClutch=vehicle.ManualTransmission && key!=null && key.leftCtrlKey.isPressed;}vehicle.ClutchPressed=launchClutch;return; }
            if(Time.timeScale<=0)return;
            if(!vehicle.ManualTransmission || key==null || !key.leftCtrlKey.isPressed)launchClutch=false;
            vehicle.ClutchPressed=launchClutch;
            var pad=Gamepad.current;
            // Keep analogue trigger control after a pad is used; W takes keyboard control back.
            if(key!=null && (key.wKey.isPressed || key.wKey.wasReleasedThisFrame || key.sKey.isPressed || key.sKey.wasReleasedThisFrame || key.aKey.isPressed || key.dKey.isPressed || key.spaceKey.isPressed || key.leftShiftKey.isPressed)) usingGamepad=false;
            else if(pad!=null && (pad.rightTrigger.ReadValue()>.01f || pad.leftTrigger.ReadValue()>.01f || Mathf.Abs(pad.leftStick.x.ReadValue())>.08f || pad.rightShoulder.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame)) usingGamepad=true;
            if(pad==null)usingGamepad=false;
            bool up=(key!=null && key.eKey.wasPressedThisFrame) || (pad!=null && pad.rightShoulder.wasPressedThisFrame);
            bool down=(key!=null && key.leftShiftKey.wasPressedThisFrame) || (pad!=null && pad.leftShoulder.wasPressedThisFrame);
            if(vehicle.ManualTransmission && up!=down) { if(up) vehicle.ShiftUp(); else vehicle.ShiftDown(); }
            float accelerator=key!=null && key.wKey.isPressed?1:0;
            // Space is the keyboard brake; releasing both pedals coasts.
            float brake=key!=null && key.spaceKey.isPressed?KeyboardBrakeLimit:0;
            if(key!=null && key.mKey.wasPressedThisFrame)overTaking=!overTaking;
            vehicle.OvertakeRequested=overTaking;
            if(key!=null && key.fKey.wasPressedThisFrame)
            {bool changed=vehicle.ToggleDRS();ControlNotice=!changed?"DRS: STRAIGHT / 100+ KM/H":vehicle.DRSOpen?"DRS OPEN":"DRS CLOSED";NoticeUntil=Time.unscaledTime+2;}
            float target=(key!=null && key.dKey.isPressed ? 1 : 0) - (key!=null && key.aKey.isPressed ? 1 : 0);
            if(pad!=null)
            {
                accelerator=Mathf.Max(accelerator,pad.rightTrigger.ReadValue());
                brake=Mathf.Max(brake,pad.leftTrigger.ReadValue());
                float axis=pad.leftStick.x.ReadValue(); if(Mathf.Abs(axis)>.08f) target=axis;
            }
            // Pedal travel smoothing is input mapping, independent of tyre slip (no TC/ABS).
            vehicle.Throttle=brake>.01f?0:Mathf.MoveTowards(vehicle.Throttle,accelerator,pedalResponse*Time.deltaTime);
            vehicle.Brake=usingGamepad?Mathf.MoveTowards(vehicle.Brake,brake,pedalResponse*Time.deltaTime):brake;
            float limit=SteeringInputLimit;
            target*=limit;
            float response=vehicle.PowertrainEnabled && speedSensitiveSteering
                ? limit/(Mathf.Abs(target)<.001f?.16f:steeringTravelSeconds):steeringResponse;
            vehicle.Steering = Mathf.MoveTowards(vehicle.Steering,target,response*Time.deltaTime);
            if (key!=null && key.rKey.wasPressedThisFrame) progress.Recover();
        }
        void OnDisable() { if (vehicle) vehicle.Throttle = vehicle.Steering = 0; }
    }
}
