using System.Collections.Generic;
using UnityEngine;

namespace Overdrive
{
    public struct RelativeVehicleObservation
    {
        public OverdriveVehicle vehicle;
        public int id;
        public Vector3 position, velocity, forward, bodySize;
        public TrackPosition track;
        public float longitudinal, lateral, closingSpeed, worldDistance;
        public bool sameDirection, aheadInCorridor;
    }

    public interface IVehiclePerception
    {
        IReadOnlyList<RelativeVehicleObservation> Observations { get; }
        bool HasLead { get; }
        RelativeVehicleObservation Lead { get; }
        void Sample();
    }

    // Phase 1: observation only. No steering, throttle, braking or physics writes.
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-150)]
    [RequireComponent(typeof(AIDriver))]
    public sealed class VehiclePerception : MonoBehaviour, IVehiclePerception
    {
        [Min(1)] public float sensorRange = 70;
        [Range(1, 30)] public float sampleHz = 15;
        [Min(.1f)] public float corridorHalfWidth = 2.5f;
        [Range(-1, 1)] public float directionDotMinimum = .5f;
        public bool showGizmos = true;
        [Tooltip("Tuning fallback used only when a vehicle has no body BoxCollider.")]
        public Vector3 fallbackBodySize = new Vector3(2, 1.2f, 4.8f);
        readonly List<RelativeVehicleObservation> observations = new List<RelativeVehicleObservation>(32);
        readonly Dictionary<OverdriveVehicle, Vector3> sizes = new Dictionary<OverdriveVehicle, Vector3>();
        public IReadOnlyList<RelativeVehicleObservation> Observations => observations;
        public bool HasLead { get; private set; }
        public RelativeVehicleObservation Lead { get; private set; }
        public bool ReferenceValid { get; private set; }
        public TrackPosition SelfTrack { get; private set; }
        public float LastSampleTime { get; private set; } = float.NegativeInfinity;
        public Vector3 SelfBodySize { get; private set; }
        AIDriver driver;
        OverdriveVehicle self;
        CircuitProgress progress;
        RaceManager race;
        TrackReferenceAdapter track;
        WaypointPath boundPath;
        float nextSample;

        void Awake()
        {
            driver = GetComponent<AIDriver>();
            self = GetComponent<OverdriveVehicle>();
            progress = GetComponent<CircuitProgress>();
            race = FindFirstObjectByType<RaceManager>();
        }
        void OnEnable() { nextSample = 0; }
        void OnDisable() { Clear(); sizes.Clear(); }
        void FixedUpdate()
        {
            if (!driver || !driver.enabled || !driver.enablePerception) { Clear(); return; }
            if (Time.fixedTime < nextSample) return;
            nextSample = Time.fixedTime + 1 / Mathf.Max(1, sampleHz);
            Sample();
        }
        void Clear() { observations.Clear(); HasLead = ReferenceValid = false; Lead = default; SelfTrack = default; LastSampleTime = float.NegativeInfinity; }
        public void Sample()
        {
            Clear();
            if (!driver || !driver.enabled || !driver.enablePerception || !self || !self.enabled || !self.Body) return;
            if (!race) race = FindFirstObjectByType<RaceManager>();
            if (track == null || boundPath != driver.path)
            { boundPath = driver.path; track = new TrackReferenceAdapter(boundPath); sizes.Clear(); }
            if (!race || !track.TryProject(progress, self.Body.position, out var own) ||
                !TrackReferenceAdapter.Finite(self.Body.linearVelocity)) return;
            ReferenceValid = true; SelfTrack = own; LastSampleTime = Time.fixedTime; SelfBodySize = BodySize(self);
            float nearest = float.PositiveInfinity;
            foreach (var entry in race.Racers)
            {
                var other = entry.vehicle;
                if (!other || other == self || !other.isActiveAndEnabled || !other.Body || !entry.progress ||
                    !entry.progress.isActiveAndEnabled || !TrackReferenceAdapter.Finite(other.Body.linearVelocity)) continue;
                Vector3 position = other.Body.position;
                float distance = Vector3.Distance(self.Body.position, position);
                if (!TrackReferenceAdapter.Finite(distance) || distance > sensorRange ||
                    !track.TryProject(entry.progress, position, out var projected)) continue;
                float longitudinal = TrackReferenceAdapter.SignedDistance(own.distance, projected.distance, track.Length);
                bool same = Vector3.Dot(own.tangent, projected.tangent) >= directionDotMinimum &&
                    Vector3.Dot(transform.forward, other.transform.forward) >= directionDotMinimum;
                var item = new RelativeVehicleObservation
                {
                    vehicle = other, id = other.GetInstanceID(), position = position,
                    velocity = other.Body.linearVelocity, forward = other.transform.forward,
                    bodySize = BodySize(other), track = projected, longitudinal = longitudinal,
                    lateral = projected.lateral - own.lateral, worldDistance = distance,
                    closingSpeed = Vector3.Dot(self.Body.linearVelocity, own.tangent) -
                        Vector3.Dot(other.Body.linearVelocity, projected.tangent),
                    sameDirection = same,
                    aheadInCorridor = same && longitudinal > 0 && longitudinal <= sensorRange &&
                        Mathf.Abs(projected.lateral - own.lateral) <= corridorHalfWidth
                };
                observations.Add(item);
                if (item.aheadInCorridor && longitudinal < nearest)
                { nearest = longitudinal; Lead = item; HasLead = true; }
            }
        }
        Vector3 BodySize(OverdriveVehicle car)
        {
            if (sizes.TryGetValue(car, out var size)) return size;
            var boxes = car.GetComponentsInChildren<BoxCollider>();
            Bounds bounds = default; bool found = false;
            foreach (var box in boxes)
            {
                if (!box.enabled || box.isTrigger || box.attachedRigidbody != car.Body) continue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = box.center + Vector3.Scale(box.size * .5f,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    corner = car.transform.InverseTransformPoint(box.transform.TransformPoint(corner));
                    if (!found) { bounds = new Bounds(corner, Vector3.zero); found = true; }
                    else bounds.Encapsulate(corner);
                }
            }
            size = Vector3.Scale(found ? bounds.size : fallbackBodySize, Abs(car.transform.lossyScale));
            sizes[car] = size; return size;
        }
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        void OnDrawGizmosSelected()
        {
            if (!showGizmos || !enabled) return;
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, sensorRange);
            foreach (var item in observations)
            {
                if (!item.vehicle) continue;
                Gizmos.color = item.aheadInCorridor ? Color.red : Color.gray;
                Gizmos.DrawLine(transform.position, item.position);
            }
        }
    }
}
