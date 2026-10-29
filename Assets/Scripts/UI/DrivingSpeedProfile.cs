using System;
using UnityEngine;

namespace Overdrive
{
    [Serializable]
    public sealed class DrivingGuideTuning
    {
        [Tooltip("Empirical advisory limits, calibrated on the current F1 car; not changes to vehicle physics.")]
        [Min(1)] public float baseLateralAcceleration = 16;
        [Min(0)] public float aeroLateralCoefficient = .001f;
        [Min(1)] public float maximumLateralAcceleration = 26;
        [Min(1)] public float brakingDeceleration = 10;
        [Tooltip("Limits how quickly the advice asks for acceleration after the apex; zero disables this envelope.")]
        [Min(0)] public float exitAcceleration = 6;
        [Min(30)] public float maximumSpeedKmh = 320;
        [Tooltip("Braking cue reaction allowance, applied once at the sampled position.")]
        [Range(0,.8f)] public float reactionSeconds=.25f;
        [HideInInspector] public float powerLimitKW,vehicleMassKg=810,dragArea=1.2f,longitudinalReserve;
        [NonSerialized] public float[] localBaseLateral;
    }

    // Shared geometric speed envelope; consumers remain responsible for vehicle controls.
    public sealed class DrivingSpeedProfile
    {
        public readonly float[] speeds, lengths;
        public DrivingSpeedProfile(WaypointPath path, DrivingGuideTuning tuning, float pace=1)
        {
            if(!path || !path.Valid) throw new ArgumentException("A valid ordered circuit is required",nameof(path));
            tuning=tuning??new DrivingGuideTuning();
            pace=Mathf.Clamp(pace,.5f,1.1f);
            float ceiling=tuning.maximumSpeedKmh/3.6f;
            if(tuning.powerLimitKW>0)
            {
                float low=0,high=ceiling;
                for(int step=0;step<20;step++){float mid=(low+high)*.5f;if(PowerAcceleration(mid,tuning)>0)low=mid;else high=mid;}
                ceiling=Mathf.Min(ceiling,low);
            }
            int n=path.points.Length; speeds=new float[n]; lengths=new float[n];
            for(int i=0;i<n;i++)
            {
                var a=path.Position(i)-path.Position(i-1); var b=path.Position(i+1)-path.Position(i);
                lengths[i]=b.magnitude;
                float k=Vector3.Angle(a,b)*Mathf.Deg2Rad/Mathf.Max(.1f,(a.magnitude+b.magnitude)*.5f);
                // v²*k <= base + aero*v², also capped by the measured working lateral limit.
                // Reserve a tyre-force share for braking/drive while steering (friction ellipse).
                float reserve=Mathf.Clamp(tuning.longitudinalReserve/Mathf.Max(1,tuning.maximumLateralAcceleration),0,.95f);
                float lateralShare=Mathf.Sqrt(1-reserve*reserve);
                float baseLateral=tuning.localBaseLateral!=null && tuning.localBaseLateral.Length==n?tuning.localBaseLateral[i]:tuning.baseLateralAcceleration;
                float aeroLimit=Mathf.Sqrt(baseLateral*lateralShare/Mathf.Max(.0001f,k-tuning.aeroLateralCoefficient*lateralShare));
                float lateralLimit=Mathf.Sqrt(tuning.maximumLateralAcceleration*lateralShare/Mathf.Max(.0001f,k));
                speeds[i]=Mathf.Min(ceiling,Mathf.Min(ceiling,Mathf.Min(aeroLimit,lateralLimit))*pace);
            }
            for(int pass=0;pass<3;pass++)
            {
                for(int i=n-1;i>=0;i--)
                    speeds[i]=Mathf.Min(speeds[i],Mathf.Sqrt(speeds[(i+1)%n]*speeds[(i+1)%n]+2*tuning.brakingDeceleration*lengths[i]));
                if(tuning.exitAcceleration>0) for(int i=0;i<n;i++)
                {
                    float acceleration=tuning.exitAcceleration;
                    if(tuning.powerLimitKW>0)
                    {
                        float next=Mathf.Min(speeds[(i+1)%n],Mathf.Sqrt(speeds[i]*speeds[i]+2*acceleration*lengths[i]));
                        float midpoint=Mathf.Sqrt((speeds[i]*speeds[i]+next*next)*.5f);
                        acceleration=Mathf.Min(acceleration,Mathf.Max(0,PowerAcceleration(midpoint,tuning)));
                    }
                    speeds[(i+1)%n]=Mathf.Min(speeds[(i+1)%n],Mathf.Sqrt(speeds[i]*speeds[i]+2*acceleration*lengths[i]));
                }
            }
        }
        public static float PowerAcceleration(float speedMps,DrivingGuideTuning tuning)
        {
            // Same 0.92 driveline efficiency and air density used by the vehicle; do not assume ERS/DRS.
            float force=tuning.powerLimitKW*1000*.92f/Mathf.Max(10,speedMps)-.5f*1.225f*tuning.dragArea*speedMps*speedMps;
            return force/Mathf.Max(1,tuning.vehicleMassKg)-9.81f*.012f;
        }
        public float AdvisoryKmh(int segment,float fraction,float speedKmh,float reactionSeconds)
        {
            float current=SpeedKmh(segment,fraction);
            float distance=Mathf.Clamp01(fraction)*lengths[segment]+Mathf.Max(0,speedKmh)/3.6f*Mathf.Clamp(reactionSeconds,0,.8f);
            int ahead=segment;
            for(int step=0;step<speeds.Length-1 && distance>lengths[ahead];step++){distance-=lengths[ahead];ahead=(ahead+1)%speeds.Length;}
            return Mathf.Min(current,SpeedKmh(ahead,distance/Mathf.Max(.01f,lengths[ahead])));
        }
        public float SpeedKmh(int segment,float fraction)
        {
            int next=(segment+1)%speeds.Length;
            // Interpolate kinetic energy, matching a constant-deceleration envelope.
            return Mathf.Sqrt(Mathf.Lerp(speeds[segment]*speeds[segment],speeds[next]*speeds[next],Mathf.Clamp01(fraction)))*3.6f;
        }
    }
}
