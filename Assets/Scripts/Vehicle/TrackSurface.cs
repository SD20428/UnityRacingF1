using UnityEngine;
namespace Overdrive
{
    public sealed class TrackSurface : MonoBehaviour
    {
        [Range(.1f,1.5f)] public float grip=1;
        [Range(0,.2f)] public float rollingResistance=.012f;
        public string surfaceName="Asphalt";
    }
}
