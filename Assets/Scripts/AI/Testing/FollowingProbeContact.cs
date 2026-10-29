#if UNITY_EDITOR
using UnityEngine;
namespace Overdrive.EditorTools
{
    public sealed class FollowingProbeContact : MonoBehaviour
    {
        public Rigidbody lead;
        public int hits;
        void OnCollisionEnter(Collision collision) { if(collision.rigidbody==lead) hits++; }
    }
}
#endif
