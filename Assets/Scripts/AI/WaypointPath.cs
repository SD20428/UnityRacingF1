using UnityEngine;
namespace Overdrive
{
    public sealed class WaypointPath : MonoBehaviour
    {
        public Transform[] points;
        public bool Valid => points != null && points.Length >= 3 && !System.Array.Exists(points,p=>p==null);
        public Vector3 Position(int index) => points[(index+points.Length)%points.Length].position;
        public Vector3 Target(Vector3 origin, int next, float lookahead)
        {
            return SampleAhead(origin,next,lookahead,out _);
        }
        public float SegmentFraction(Vector3 origin,int segment)
        {
            var delta=Position(segment+1)-Position(segment);
            return Mathf.Clamp01(Vector3.Dot(origin-Position(segment),delta)/Mathf.Max(.001f,delta.sqrMagnitude));
        }
        public Vector3 SampleAhead(Vector3 origin,int next,float lookahead,out Vector3 right)
        {
            int segment=(next-1+points.Length)%points.Length;
            float distance=SegmentFraction(origin,segment)*Vector3.Distance(Position(segment),Position(segment+1))+Mathf.Max(0,lookahead);
            for(int i=0;i<points.Length;i++)
            {
                var delta=Position(segment+1)-Position(segment);float length=delta.magnitude;
                if(distance<=length || i==points.Length-1)
                {
                    right=Vector3.Cross(Vector3.up,delta.normalized).normalized;
                    return Position(segment)+delta*Mathf.Clamp01(distance/Mathf.Max(.001f,length));
                }
                distance-=length;segment=(segment+1)%points.Length;
            }
            right=Vector3.right;return Position(next);
        }
        public float BendAhead(int next, float distance)
        {
            float bend=0, traversed=0;
            for(int i=0;i<points.Length && traversed<distance;i++)
            {
                Vector3 a=Position(next+i)-Position(next+i-1), b=Position(next+i+1)-Position(next+i);
                bend=Mathf.Max(bend,Vector3.Angle(a,b)); traversed+=a.magnitude;
            }
            return bend;
        }
        public float CurvatureAhead(int next,float distance)
        {
            float curvature=0,traversed=0;
            for(int i=0;i<points.Length && traversed<distance;i++)
            {
                Vector3 a=Position(next+i)-Position(next+i-1), b=Position(next+i+1)-Position(next+i);
                float length=Mathf.Max(.1f,(a.magnitude+b.magnitude)*.5f);
                curvature=Mathf.Max(curvature,Vector3.Angle(a,b)*Mathf.Deg2Rad/length); traversed+=a.magnitude;
            }
            return curvature;
        }
        void OnDrawGizmosSelected()
        {
            if(!Valid) return;
            Gizmos.color=Color.cyan;
            for(int i=0;i<points.Length;i++) Gizmos.DrawLine(Position(i)+Vector3.up*.3f,Position(i+1)+Vector3.up*.3f);
        }
    }
}
