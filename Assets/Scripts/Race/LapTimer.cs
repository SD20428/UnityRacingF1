namespace Overdrive
{
    public sealed class LapTimer
    {
        public float LastLapEnd { get; private set; }
        public float BestLap { get; private set; } = -1;
        public void Record(float elapsed)
        {
            float lap=elapsed-LastLapEnd;
            if(lap>0 && (BestLap<0 || lap<BestLap)) BestLap=lap;
            LastLapEnd=elapsed;
        }
        public void Reset() { LastLapEnd=0; BestLap=-1; }
        public void StartAt(float elapsed) { LastLapEnd=elapsed; BestLap=-1; }
    }
}
