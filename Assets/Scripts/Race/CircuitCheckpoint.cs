using UnityEngine;
namespace Overdrive
{
    public sealed class CircuitCheckpoint : MonoBehaviour
    {
        public int index;
        // A car may already overlap the first gate while countdown input is locked.
        void OnTriggerStay(Collider other) { OnTriggerEnter(other); }
        void OnTriggerEnter(Collider other)
        {
            var progress = other.attachedRigidbody ? other.attachedRigidbody.GetComponent<CircuitProgress>() : null;
            if (progress) progress.Pass(index);
        }
    }
}
