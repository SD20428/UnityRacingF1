using UnityEngine;

namespace Overdrive
{
    public enum LineGateGrade { Pending, Perfect, Good, Miss }

    // Independent judgement: gates never alter checkpoints, lap times or vehicle physics.
    public static class LineGateScoring
    {
        public const float PerfectHalfWidth=.85f,GoodHalfWidth=2.6f;
        public static LineGateGrade Grade(float lateral,float height,float heading,float speedKmh)
        {
            if(!TrackReferenceAdapter.Finite(lateral) || !TrackReferenceAdapter.Finite(height) ||
               !TrackReferenceAdapter.Finite(heading) || !TrackReferenceAdapter.Finite(speedKmh) ||
               height<-.3f || height>2.2f || speedKmh<10 || Mathf.Abs(heading)>30)return LineGateGrade.Miss;
            if(Mathf.Abs(lateral)<=PerfectHalfWidth && Mathf.Abs(heading)<=12 && speedKmh>=30)return LineGateGrade.Perfect;
            return Mathf.Abs(lateral)<=GoodHalfWidth?LineGateGrade.Good:LineGateGrade.Miss;
        }
        public static bool Crossing(Vector3 from,Vector3 to,Vector3 centre,Vector3 forward,out Vector3 point)
        {
            point=to;
            if(!TrackReferenceAdapter.Finite(from) || !TrackReferenceAdapter.Finite(to) || (to-from).sqrMagnitude>144)return false;
            float a=Vector3.Dot(from-centre,forward),b=Vector3.Dot(to-centre,forward);
            if(a>0 || b<=0 || b-a<.001f)return false;
            point=Vector3.Lerp(from,to,Mathf.Clamp01(-a/(b-a)));return true;
        }
    }

    public sealed class LineGateScorecard
    {
        public LineGateGrade[] Gates {get;private set;}=new LineGateGrade[0];
        public int Perfect {get;private set;}
        public int Good {get;private set;}
        public int Miss {get;private set;}
        public int SessionPerfect {get;private set;}
        public int SessionGood {get;private set;}
        public int SessionMiss {get;private set;}
        public int Judged=>Perfect+Good+Miss;
        public int Points=>SessionPerfect*100+SessionGood*60;
        public bool PerfectLap=>Gates.Length>0 && Perfect==Gates.Length;
        public void Reset(int count)
        {Gates=new LineGateGrade[Mathf.Max(0,count)];SessionPerfect=SessionGood=SessionMiss=0;BeginLap();}
        public void BeginLap(){System.Array.Clear(Gates,0,Gates.Length);Perfect=Good=Miss=0;}
        public bool Record(int index,LineGateGrade grade,bool timed)
        {
            if(index<0 || index>=Gates.Length || Gates[index]!=LineGateGrade.Pending || grade==LineGateGrade.Pending)return false;
            Gates[index]=grade;
            if(grade==LineGateGrade.Perfect){Perfect++;if(timed)SessionPerfect++;}
            else if(grade==LineGateGrade.Good){Good++;if(timed)SessionGood++;}
            else {Miss++;if(timed)SessionMiss++;}
            return true;
        }
    }
}
