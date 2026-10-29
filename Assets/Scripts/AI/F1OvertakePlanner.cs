using System.Collections.Generic;
using UnityEngine;

namespace Overdrive
{
    // Straight-line lane changes only. Retain longitudinal following until lateral separation exists.
    public static class F1OvertakePlanner
    {
        public static bool ClearLane(IReadOnlyList<RelativeVehicleObservation> observations,
            OverdriveVehicle lead,float selfLateral,float candidate,float speedMps)
        {
            if(observations==null || !TrackReferenceAdapter.Finite(candidate) || !TrackReferenceAdapter.Finite(selfLateral) ||
                !TrackReferenceAdapter.Finite(speedMps) || speedMps<0 || Mathf.Abs(candidate)>3.25f)return false;
            foreach(var other in observations)
            {
                if(other.vehicle==lead || !other.vehicle || !other.vehicle.isActiveAndEnabled)continue;
                if(!TrackReferenceAdapter.Finite(other.longitudinal) || !TrackReferenceAdapter.Finite(other.lateral) ||
                    !TrackReferenceAdapter.Finite(other.velocity) || !TrackReferenceAdapter.Finite(other.track.tangent))return false;
                float longitudinalVelocity=Vector3.Dot(other.velocity,other.track.tangent)-speedMps;
                var right=Vector3.Cross(Vector3.up,other.track.tangent);
                float lateralVelocity=Vector3.Dot(other.velocity,right);
                for(int step=0;step<=6;step++)
                {
                    float seconds=step*.5f;
                    float z=other.longitudinal+longitudinalVelocity*seconds;
                    float x=selfLateral+other.lateral+lateralVelocity*seconds;
                    if(z < -12 || z > Mathf.Max(35,speedMps*.8f))continue;
                    if(Mathf.Abs(x-candidate)<3 || Mathf.Abs(z)<14 &&
                        x>Mathf.Min(selfLateral,candidate)-2.5f && x<Mathf.Max(selfLateral,candidate)+2.5f)return false;
                }
            }
            return true;
        }
    }
}
