using UnityEngine;

namespace Overdrive
{
    // Zero-based target gear. Braking selects the useful rev band; the vehicle still shifts sequentially.
    public static class AutomaticGearbox
    {
        public static int TargetGear(int current,float roadRpm,float speedKmh,float throttle,float brake,
            float[] ratios,float finalDrive,float redline)
        {
            if(ratios==null || ratios.Length==0 || !TrackReferenceAdapter.Finite(roadRpm) || roadRpm<0 ||
                !TrackReferenceAdapter.Finite(speedKmh) || !TrackReferenceAdapter.Finite(finalDrive) || finalDrive<=0)return current;
            current=Mathf.Clamp(current,0,ratios.Length-1);
            if(speedKmh<8)return 0;
            bool decelerating=brake>.08f || throttle<.08f;
            float upshift=decelerating?redline-250:Mathf.Min(12500,redline-500);
            float rpm=roadRpm*ratios[current]*finalDrive;
            if(rpm>upshift && current<ratios.Length-1)return current+1;
            float lowerBand=brake>.08f?10500:decelerating?8500:6500;
            float safeRpm=Mathf.Min(redline-800,decelerating?11750:12000);
            int target=current;
            while(target>0 && roadRpm*ratios[target]*finalDrive<lowerBand &&
                roadRpm*ratios[target-1]*finalDrive<=safeRpm)target--;
            return target;
        }
    }
}
