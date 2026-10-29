using UnityEngine;
using TMPro;

namespace Overdrive
{
    [DefaultExecutionOrder(270)]
    public sealed class RaceDetailTelemetry : MonoBehaviour
    {
        public RaceUI ui;public OverdriveRacingHUD dashboard;
        public TMP_Text[] sectors;
        public TMP_Text delta,fuel,topSpeed,status,gText,resultStats,leftArrow,rightArrow,rearWarning,modeChip;
        public RectTransform gDot,rpmFill;
        public SectorTimingModel Timing {get;}=new SectorTimingModel();
        public float TopSpeed {get;private set;}
        public float MaximumBrakingG {get;private set;}
        public Vector2 AccelerationG {get;private set;}
        public bool LeftNearby {get;private set;}
        public bool RightNearby {get;private set;}
        public bool RearNearby {get;private set;}
        CircuitProgress progress;RaceState? previous;
        float timingEpoch;
        Vector3 lastVelocity,lastPosition;bool velocityReady,bound;
        Color[] lampColors;float shownThrottle,shownBrake,shownRpm,nextRefresh;Vector2 gOrigin;
        readonly float[] sectorPulseAt={-100,-100,-100};
        static readonly Color Mint=new Color(.22f,.92f,.72f),Amber=new Color(1,.7f,.24f),Purple=new Color(.76f,.45f,1),Muted=new Color(.53f,.6f,.68f);
        void Start()
        {
            if(!ui || !dashboard){enabled=false;return;}
            progress=ui.race.PlayerProgress;progress.CheckpointPassed+=OnCheckpoint;bound=true;
            Timing.Reset(progress.checkpoints.Length,progress.LastCheckpoint);
            lampColors=new Color[dashboard.revLights.Length];for(int i=0;i<lampColors.Length;i++)lampColors[i]=dashboard.revLights[i].color;
            if(gDot)gOrigin=gDot.anchoredPosition;
        }
        void OnEnable(){if(progress && !bound){progress.CheckpointPassed+=OnCheckpoint;bound=true;}}
        void OnDisable(){if(bound && progress){progress.CheckpointPassed-=OnCheckpoint;bound=false;}}
        void OnCheckpoint(int checkpoint)
        {
            if(ui.race.State!=RaceState.Racing && ui.race.State!=RaceState.Finished)return;
            if(ui.race.IsQualifying && progress.CompletedLaps==0)return;
            if(ui.race.IsQualifying && progress.CompletedLaps==1 && checkpoint==0)
            {timingEpoch=ui.race.Elapsed;Timing.Reset(progress.checkpoints.Length);return;}
            int complete=Timing.Observe(progress.CompletedLaps-(ui.race.IsQualifying?1:0),checkpoint,ui.race.Elapsed-timingEpoch);
            if(complete>=0)sectorPulseAt[complete]=Time.unscaledTime;
        }
        void FixedUpdate()
        {
            if(!ui || ui.race.State!=RaceState.Racing){velocityReady=false;return;}
            var car=ui.race.player;var body=car.Body;
            if(velocityReady && Vector3.Distance(body.position,lastPosition)<15)
            {
                var a=car.transform.InverseTransformDirection((body.linearVelocity-lastVelocity)/Time.fixedDeltaTime)/9.81f;
                if(TrackReferenceAdapter.Finite(a))
                {
                    AccelerationG=Vector2.Lerp(AccelerationG,new Vector2(Mathf.Clamp(a.x,-5,5),Mathf.Clamp(a.z,-5,5)),1-Mathf.Exp(-8*Time.fixedDeltaTime));
                    MaximumBrakingG=Mathf.Max(MaximumBrakingG,-AccelerationG.y);
                }
            }
            lastVelocity=body.linearVelocity;lastPosition=body.position;velocityReady=true;
        }
        void LateUpdate()
        {
            if(!progress)return;var race=ui.race;var car=race.player;
            if(previous!=race.State)
            {
                previous=race.State;
                if(race.State==RaceState.Countdown || race.State==RaceState.Menu)
                {Timing.Reset(progress.checkpoints.Length,progress.LastCheckpoint);timingEpoch=0;TopSpeed=MaximumBrakingG=0;AccelerationG=Vector2.zero;velocityReady=false;}
            }
            if(race.State==RaceState.Racing)TopSpeed=Mathf.Max(TopSpeed,car.SpeedKmh);
            float blend=1-Mathf.Exp(-12*Time.unscaledDeltaTime);
            shownThrottle=Mathf.Lerp(shownThrottle,car.Throttle,blend);shownBrake=Mathf.Lerp(shownBrake,car.Brake,blend);shownRpm=Mathf.Lerp(shownRpm,Mathf.Clamp01(car.EngineRPM/14500),blend);
            dashboard.throttleBar.sizeDelta=new Vector2(292*shownThrottle,4);dashboard.brakeBar.sizeDelta=new Vector2(292*shownBrake,4);
            if(rpmFill)rpmFill.sizeDelta=new Vector2(380*shownRpm,2);
            for(int i=0;i<lampColors.Length;i++)
            {
                lampColors[i]=Color.Lerp(lampColors[i],dashboard.revLights[i].color,blend);
                dashboard.revLights[i].color=lampColors[i];
            }
            if(gDot)gDot.anchoredPosition=gOrigin+Vector2.ClampMagnitude(AccelerationG,4)*7;
            if(modeChip)modeChip.text=race.IsQualifying?"QUALIFYING":race.ProfessionalSelected?"F1 DRIVER":race.HardSelected?"HARD RACE":"NORMAL RACE";
            if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.1f;
            for(int i=0;i<sectors.Length;i++)
            {
                bool current=Timing.Current[i]>0;
                float time=current?Timing.Current[i]:Timing.Last[i];
                sectors[i].text=$"S{i+1}  {(time>0?time.ToString("00.000"):"--.---")}";
                sectors[i].color=!current && race.State!=RaceState.Finished?(i==Timing.Sector?Color.white:Muted):float.IsNaN(Timing.Deltas[i])?Mint:Timing.Deltas[i]<=0?Purple:Amber;
                sectors[i].transform.localScale=Vector3.one*(1+.08f*Mathf.Exp(-Mathf.Max(0,Time.unscaledTime-sectorPulseAt[i])*8));
            }
            float fraction=ui.race.ai.path.SegmentFraction(car.transform.position,progress.LastCheckpoint);
            float difference=Timing.LiveDelta(progress.LastCheckpoint,fraction,race.Elapsed-timingEpoch);
            delta.text=float.IsNaN(difference)?"DELTA  --.---    /    FIRST LAP":$"DELTA  {difference:+0.000;-0.000;0.000}    /    PERSONAL BEST";
            delta.color=float.IsNaN(difference)?Muted:difference<=0?Mint:Amber;
            fuel.text=$"FUEL   {car.FuelKg:00.0} KG";topSpeed.text=$"TOP SPEED   {TopSpeed:000} KM/H";
            status.text=car.SlipstreamStrength>.15f?$"TOW {car.SlipstreamStrength*100:0}%"+(car.DRSOpen?" / DRS":car.HybridPowerKW>5?" / ERS":" / SLIPSTREAM"):car.DRSOpen?"DRS OPEN":car.HybridPowerKW>5?"ERS DEPLOYING":car.DRSEligible?"DRS READY":"ENERGY SAVING";
            status.color=car.SlipstreamStrength>.15f || car.DRSOpen || car.HybridPowerKW>5?Mint:Muted;
            gText.text=$"{AccelerationG.x:+0.0;-0.0;0.0} / {AccelerationG.y:+0.0;-0.0;0.0} G";
            resultStats.text=$"TOP SPEED  {TopSpeed:000} KM/H     /     MAX BRAKING  {MaximumBrakingG:0.0} G";
            LeftNearby=RightNearby=RearNearby=false;
            if(race.State==RaceState.Racing)foreach(var entry in race.Racers)
            {
                if(entry.vehicle==car || !entry.vehicle.isActiveAndEnabled)continue;
                var local=car.transform.InverseTransformPoint(entry.vehicle.transform.position);
                if(Mathf.Abs(local.y)>3)continue;
                if(Mathf.Abs(local.z)<13 && Mathf.Abs(local.x)<8 && Mathf.Abs(local.x)>1)
                {if(local.x<0)LeftNearby=true;else RightNearby=true;}
                if(local.z< -6 && local.z> -30 && Mathf.Abs(local.x)<4)RearNearby=true;
            }
            float alpha=.7f+.15f*Mathf.Sin(Time.unscaledTime*8);
            var warning=Amber;warning.a=LeftNearby?alpha:0;leftArrow.color=warning;warning.a=RightNearby?alpha:0;rightArrow.color=warning;warning.a=RearNearby?.85f:0;rearWarning.color=warning;
        }
    }
}
