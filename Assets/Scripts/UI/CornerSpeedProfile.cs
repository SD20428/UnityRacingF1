using UnityEngine;

namespace Overdrive
{
    // Advisory only: corner grip and approach braking, without a straight/exit acceleration target.
    public sealed class CornerSpeedProfile
    {
        readonly DrivingSpeedProfile braking;
        readonly bool[] corners;
        readonly float deceleration, reaction;
        public float MaximumKmh { get; }
        public int Count => corners.Length;

        public CornerSpeedProfile(WaypointPath path, DrivingGuideTuning source)
        {
            var tuning=new DrivingGuideTuning {
                baseLateralAcceleration=source.baseLateralAcceleration,
                aeroLateralCoefficient=source.aeroLateralCoefficient,
                maximumLateralAcceleration=source.maximumLateralAcceleration,
                brakingDeceleration=source.brakingDeceleration,exitAcceleration=0,
                maximumSpeedKmh=source.maximumSpeedKmh,reactionSeconds=source.reactionSeconds,
                powerLimitKW=source.powerLimitKW,vehicleMassKg=source.vehicleMassKg,
                dragArea=source.dragArea,longitudinalReserve=source.longitudinalReserve,
                localBaseLateral=source.localBaseLateral
            };
            braking=new DrivingSpeedProfile(path,tuning);
            deceleration=Mathf.Max(1,tuning.brakingDeceleration);reaction=tuning.reactionSeconds;
            MaximumKmh=tuning.maximumSpeedKmh;corners=new bool[path.points.Length];
            for(int i=0;i<corners.Length;i++)
            {
                var incoming=path.Position(i)-path.Position(i-1);
                var outgoing=path.Position(i+1)-path.Position(i);
                float curvature=Vector3.Angle(incoming,outgoing)*Mathf.Deg2Rad/Mathf.Max(.1f,(incoming.magnitude+outgoing.magnitude)*.5f);
                corners[i]=curvature>=.0015f;
            }
        }
        public bool IsCorner(int segment) => corners[segment] || corners[(segment+1)%Count];
        public float TargetKmh(int segment,float fraction,float speedKmh,float grip=1)
        { return braking.AdvisoryKmh(segment,fraction,speedKmh,reaction)*Mathf.Sqrt(Mathf.Clamp(grip,.25f,1)); }

        public bool NeedsGuidance(int segment,float fraction,float speedKmh,float grip,out float distanceToCorner)
        {
            distanceToCorner=0;
            if(IsCorner(segment))return true;
            float v=Mathf.Max(0,speedKmh)/3.6f;
            float gripScale=Mathf.Clamp(grip,.25f,1);
            float brakingRate=deceleration*gripScale;
            float preview=v*reaction+12; // Includes a short readable cue before the braking point.
            float horizon=v*v/(2*brakingRate)+preview;
            float distance=braking.lengths[segment]*(1-Mathf.Clamp01(fraction));
            for(int step=1;step<Count && distance<=horizon;step++)
            {
                int next=(segment+step)%Count;
                if(corners[next])
                {
                    float target=braking.speeds[next]*Mathf.Sqrt(gripScale);
                    float needed=Mathf.Max(0,(v*v-target*target)/(2*brakingRate))+preview;
                    if(distance<=needed){distanceToCorner=distance;return true;}
                }
                distance+=braking.lengths[next];
            }
            distanceToCorner=float.PositiveInfinity;return false;
        }
    }
}
