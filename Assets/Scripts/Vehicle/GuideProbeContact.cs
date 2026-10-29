#if UNITY_EDITOR
using UnityEngine;
namespace Overdrive
{
    public sealed class GuideProbeContact : MonoBehaviour
    {
        public int hits;
        void OnCollisionEnter(Collision collision)
        {
            foreach(var point in collision.contacts)
                if(Mathf.Abs(Vector3.Dot(point.normal,Vector3.up))<.5f) {hits++;break;}
        }
    }
}
#endif
