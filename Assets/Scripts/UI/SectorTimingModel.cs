using UnityEngine;

namespace Overdrive
{
    // Presentation timing derived from accepted checkpoint crossings. Never advances race progress.
    public sealed class SectorTimingModel
    {
        public readonly float[] Current={-1,-1,-1},Last={-1,-1,-1},Best={-1,-1,-1},Deltas={float.NaN,float.NaN,float.NaN};
        public float BestLap {get;private set;}=-1;
        public float LastLap {get;private set;}=-1;
        public int Sector {get;private set;}
        float[] currentCheckpoints,bestCheckpoints;
        int count,lap;float lapStart,sectorStart,lastElapsed;bool armed;
        public void Reset(int checkpoints,int initialCheckpoint=0)
        {
            count=checkpoints;lap=Sector=0;lapStart=sectorStart=lastElapsed=0;BestLap=LastLap=-1;
            currentCheckpoints=new float[count];bestCheckpoints=null;armed=initialCheckpoint<count/3;
            for(int i=0;i<count;i++)currentCheckpoints[i]=-1;
            for(int i=0;i<3;i++){Current[i]=Last[i]=Best[i]=-1;Deltas[i]=float.NaN;}
        }
        void Record(int sector,float elapsed)
        {
            float seconds=elapsed-sectorStart;if(seconds<=0)return;
            Current[sector]=seconds;Deltas[sector]=Best[sector]>0?seconds-Best[sector]:float.NaN;
            if(Best[sector]<0 || seconds<Best[sector])Best[sector]=seconds;
            sectorStart=elapsed;
        }
        // Returns the completed sector, or -1. The start-grid crossing is never a completed sector.
        public int Observe(int completedLaps,int checkpoint,float elapsed)
        {
            if(count<3 || checkpoint<0 || checkpoint>=count || !TrackReferenceAdapter.Finite(elapsed) || elapsed<lastElapsed)return -1;
            lastElapsed=elapsed;
            if(completedLaps<lap || completedLaps>lap+1){Reset(count,checkpoint);return -1;}
            if(completedLaps>lap)
            {
                if(armed && Sector==2)Record(2,elapsed);
                bool valid=Current[0]>0 && Current[1]>0 && Current[2]>0;
                LastLap=valid?elapsed-lapStart:-1;
                for(int i=0;i<3;i++)Last[i]=Current[i];
                if(valid && (BestLap<0 || LastLap<BestLap))
                {BestLap=LastLap;bestCheckpoints=(float[])currentCheckpoints.Clone();bestCheckpoints[0]=0;}
                for(int i=0;i<count;i++)currentCheckpoints[i]=-1;
                for(int i=0;i<3;i++)Current[i]=-1;
                lap=completedLaps;Sector=0;lapStart=sectorStart=elapsed;armed=true;currentCheckpoints[0]=0;
                return valid?2:-1;
            }
            int section=checkpoint>=count*2/3?2:checkpoint>=count/3?1:0;
            if(!armed){if(section!=0)return -1;armed=true;}
            currentCheckpoints[checkpoint]=elapsed-lapStart;
            if(section==Sector+1)
            {int complete=Sector;Record(complete,elapsed);Sector=section;return complete;}
            return -1;
        }
        public float LiveDelta(int segment,float fraction,float elapsed)
        {
            if(!armed || bestCheckpoints==null || segment<0 || segment>=count || BestLap<=0)return float.NaN;
            int next=(segment+1)%count;float a=bestCheckpoints[segment],b=next==0?BestLap:bestCheckpoints[next];
            if(a<0 || b<a)return float.NaN;
            return elapsed-lapStart-Mathf.Lerp(a,b,Mathf.Clamp01(fraction));
        }
    }
}
