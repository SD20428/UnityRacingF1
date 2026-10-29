using System.Collections.Generic;
using UnityEngine;

namespace Overdrive
{
    // A bounded aerodynamic wake shared by player and AI. Never adds propulsion.
    [DefaultExecutionOrder(-20)]
    [RequireComponent(typeof(OverdriveVehicle))]
    public sealed class VehicleSlipstream : MonoBehaviour
    {
        static readonly List<VehicleSlipstream> active = new List<VehicleSlipstream>();
        public float Strength { get; private set; }
        public float DragMultiplier => 1 - .16f * Strength;
        public float DownforceMultiplier => 1 - .08f * Strength;
        OverdriveVehicle car;
        float nextScan, target;
        Vector3 lastPosition;
        bool positionKnown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() { active.Clear(); }
        void Awake() { car = GetComponent<OverdriveVehicle>(); }
        void OnEnable() { if (!active.Contains(this)) active.Add(this); positionKnown = false; }
        void OnDisable() { active.Remove(this); Strength = target = 0; positionKnown = false; }

        public static float EvaluateWake(Vector3 ahead, float alignment, float speedKmh, float leaderKmh)
        {
            if (ahead.z <= 7 || ahead.z >= 65 || Mathf.Abs(ahead.y) > 2 || alignment < .95f) return 0;
            float width = Mathf.Lerp(2.2f, 4.2f, ahead.z / 65);
            float lateral = Mathf.Clamp01(1 - Mathf.Abs(ahead.x) / width);
            float distance = Mathf.InverseLerp(7, 13, ahead.z) * (1 - Mathf.InverseLerp(20, 65, ahead.z));
            return lateral * distance * Mathf.InverseLerp(80, 170, Mathf.Min(speedKmh, leaderKmh));
        }

        void FixedUpdate()
        {
            bool teleported = positionKnown && (transform.position - lastPosition).sqrMagnitude > 225;
            lastPosition = transform.position; positionKnown = true;
            if (!car || !car.ControlsEnabled || car.SpeedKmh < 80 || teleported)
            { Strength = target = 0; nextScan = 0; return; }
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + .1f; target = 0;
                foreach (var other in active)
                {
                    if (!other || other == this || !other.car || !other.car.ControlsEnabled) continue;
                    float candidate = EvaluateWake(transform.InverseTransformPoint(other.transform.position),
                        Vector3.Dot(transform.forward, other.transform.forward), car.SpeedKmh, other.car.SpeedKmh);
                    if (candidate <= target) continue;
                    // A wall or a different track level cannot provide a tow.
                    if (Physics.Linecast(transform.position + Vector3.up * .5f, other.transform.position + Vector3.up * .5f,
                        out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != other.car.Body) continue;
                    target = candidate;
                }
            }
            Strength = Mathf.MoveTowards(Strength, target, Time.fixedDeltaTime * 2);
        }
    }
}
