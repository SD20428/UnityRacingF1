using UnityEngine;
namespace Overdrive
{
    [RequireComponent(typeof(OverdriveVehicle))]
    public sealed class CircuitProgress : MonoBehaviour
    {
        public Transform[] checkpoints;
        public int CompletedLaps { get; private set; }
        public int LastCheckpoint { get; private set; }
        public int NextCheckpoint => (LastCheckpoint + 1) % checkpoints.Length;
        public bool TwoLapsComplete => CompletedLaps >= 2;
        public bool AutoRecoverBelowTerrain { get; set; } = true;
        public bool AcceptingCheckpoints { get; set; } = true;
        bool awaitingStartLine;
        public event System.Action<int> LapCompleted;
        public event System.Action<int> CheckpointPassed;
        public float OrderedProgress
        {
            get
            {
                if(!enabled || checkpoints==null || checkpoints.Length<3 || !vehicle) return 0;
                var a=checkpoints[LastCheckpoint].position;
                var segment=checkpoints[NextCheckpoint].position-a;
                float fraction=Mathf.Clamp(Vector3.Dot(vehicle.Body.position-a,segment)/Mathf.Max(.01f,segment.sqrMagnitude),0,.999f);
                return CompletedLaps*checkpoints.Length+LastCheckpoint+fraction-(awaitingStartLine?checkpoints.Length:0);
            }
        }
        public void ResetProgress() { CompletedLaps=0;var identity=GetComponent<RacerIdentity>();LastCheckpoint=identity?identity.gridCheckpoint:0;awaitingStartLine=identity && identity.startsBeforeLine; }
        OverdriveVehicle vehicle;
        void Awake()
        {
            vehicle = GetComponent<OverdriveVehicle>();
            if (checkpoints == null || checkpoints.Length < 3 || System.Array.Exists(checkpoints, t => t == null))
            { Debug.LogError("Circuit requires at least three assigned checkpoints.", this); enabled = false; }
        }
        public bool Pass(int index)
        {
            if (!enabled || !AcceptingCheckpoints || index != NextCheckpoint || Vector3.Dot(vehicle.Body.linearVelocity, checkpoints[index].forward) < 0.5f) return false;
            LastCheckpoint = index;
            if (index == 0) { if(awaitingStartLine){awaitingStartLine=false;CheckpointPassed?.Invoke(index);return true;} CompletedLaps++; Debug.Log($"OVERDRIVE lap {CompletedLaps}/2", this); LapCompleted?.Invoke(CompletedLaps); }
            CheckpointPassed?.Invoke(index);
            return true;
        }
        public void Recover()
        {
            TryRecover();
        }
        public bool TryRecover()
        {
            if (!enabled) return false;
            var point = checkpoints[LastCheckpoint];
            // Return just after the last accepted checkpoint; progression is never incremented.
            foreach (float side in new[] { 0f, -3f, 3f })
            {
                var position = point.position + point.forward * 3f + point.right * side + Vector3.up * .85f;
                bool occupied = false;
                foreach (var hit in Physics.OverlapBox(position + Vector3.up * .2f, new Vector3(1.1f,.65f,2.3f), point.rotation, ~0, QueryTriggerInteraction.Ignore))
                    if (hit.attachedRigidbody && hit.attachedRigidbody != vehicle.Body) { occupied = true; break; }
                if (occupied) continue;
                vehicle.Body.position = position;
                vehicle.Body.rotation = point.rotation;
                vehicle.Body.linearVelocity = vehicle.Body.angularVelocity = Vector3.zero;
                vehicle.Body.WakeUp();
                return true;
            }
            return false;
        }
        void Update() { if (AcceptingCheckpoints && AutoRecoverBelowTerrain && transform.position.y < -5f) Recover(); }
    }
}
