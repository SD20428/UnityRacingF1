using UnityEngine;
using TMPro;

namespace Overdrive
{
    // Presentation only: observes race/car state, never writes controls, physics or race timing.
    [DefaultExecutionOrder(250)]
    public sealed class RacePresentationMotion : MonoBehaviour
    {
        public RaceUI ui;
        public OverdriveRacingHUD dashboard;
        public GrandPrixBroadcast broadcast;
        public CanvasGroup notice,wipe;
        public TMP_Text noticeTitle,noticeDetail;
        public TMP_Text startCaption;
        public UnityEngine.UI.Image noticeAccent;
        public RectTransform[] wipeStripes,speedStreaks,finishRibbons;
        public UnityEngine.UI.Image[] lightHalos;
        public int NoticeCount {get;private set;}
        public int GearPulseCount {get;private set;}
        public int CountdownPulseCount {get;private set;}
        public float SpeedEffectAmount {get;private set;}
        RaceState? previous;
        int lastGear,lastLap,lastPosition,lastCount=99,noticePriority;
        bool lastDRS,lastERS,initialized;
        float stateBegan,noticeBegan,noticeDuration,gearBegan=-100,countBegan=-100,positionBegan=-100,nextPoll;
        float ersAnnouncedAt=-100,drsAnnouncedAt=-100;
        Vector3 gearScale=Vector3.one,positionScale=Vector3.one;
        Vector3[] lightScales;Vector2 noticeOrigin;
        static readonly Color Red=new Color(1,.10f,.17f),Mint=new Color(.24f,1,.70f);
        void Start()
        {
            if(!ui || !ui.race){enabled=false;return;}
            if(dashboard && dashboard.gear)gearScale=dashboard.gear.transform.localScale;
            positionScale=ui.standings.transform.localScale;
            if(notice){noticeOrigin=((RectTransform)notice.transform).anchoredPosition;notice.alpha=0;}
            if(wipe)wipe.alpha=0;
            lightScales=new Vector3[broadcast && broadcast.lights!=null?broadcast.lights.Length:0];
            for(int i=0;i<lightScales.Length;i++)lightScales[i]=broadcast.lights[i].transform.localScale;
            initialized=true;
        }
        public void ShowNotice(string title,string detail,Color color,float seconds=1.8f,int priority=1)
        {
            if(!notice || !noticeTitle || !noticeDetail)return;
            if(Time.unscaledTime-noticeBegan<noticeDuration-.2f && priority<noticePriority)return;
            noticeTitle.text=title;noticeDetail.text=detail;
            if(noticeAccent)noticeAccent.color=color;
            noticeBegan=Time.unscaledTime;noticeDuration=seconds;noticePriority=priority;NoticeCount++;
        }
        void LateUpdate()
        {
            if(!initialized)return;
            var race=ui.race;var car=race.player;float now=Time.unscaledTime;
            if(previous!=race.State)
            {
                var old=previous;previous=race.State;stateBegan=now;
                lastGear=car.Gear;lastLap=race.PlayerProgress.CompletedLaps;lastPosition=race.Position;
                lastDRS=car.DRSOpen;lastERS=car.HybridPowerKW>5;lastCount=99;
                noticeDuration=0;noticePriority=0;
                if(race.State==RaceState.Countdown)ShowNotice(race.IsQualifying?"QUALIFYING":"GRID READY",race.IsQualifying?"OUT LAP + 3 TIMED LAPS":race.CircuitName+"  /  GRAND PRIX",Red,1.5f,4);
                else if(race.State==RaceState.Racing)
                    ShowNotice(old==RaceState.Paused?"BACK ON TRACK":"LIGHTS OUT","FOCUS FORWARD. RACE ON.",Mint,1.7f,4);
            }
            if(race.State==RaceState.Countdown)
            {
                int count=Mathf.CeilToInt(race.CountdownRemaining);
                if(count!=lastCount){lastCount=count;countBegan=now;CountdownPulseCount++;}
            }
            if(race.State==RaceState.Racing)
            {
                if(car.Gear!=lastGear){lastGear=car.Gear;gearBegan=now;GearPulseCount++;}
                if(race.PlayerProgress.CompletedLaps>lastLap)
                {
                    lastLap=race.PlayerProgress.CompletedLaps;
                    if(race.IsQualifying)ShowNotice(lastLap==1?"FLYING LAP":lastLap==3?"FINAL ATTEMPT":"LAP COMPLETE",$"TIMED LAP {Mathf.Min(3,race.TimedLapCount+1)} / 3  /  BEST {RaceUI.Format(race.PlayerLaps.BestLap)}",Mint,2.5f,5);
                    else ShowNotice("FINAL LAP","LAP 2 / 2  —  MAKE IT COUNT",Red,2.5f,5);
                }
                if(now>=nextPoll)
                {
                    nextPoll=now+.1f;int position=race.Position;
                    if(position<lastPosition && race.Elapsed>1 && now-positionBegan>.85f)
                    {positionBegan=now;ShowNotice("POSITION GAINED",$"P{position:00}  /  {race.FieldSize:00}",Mint,1.45f,2);}
                    lastPosition=position;
                    if(car.DRSOpen && !lastDRS && now-drsAnnouncedAt>3.5f)
                    {ShowNotice("DRS OPEN","STRAIGHT-LINE ATTACK",Mint,1.35f,1);drsAnnouncedAt=now;}
                    bool ers=car.HybridPowerKW>5;
                    if(ers && !lastERS && now-ersAnnouncedAt>4)
                    {ShowNotice("OVERTAKE ACTIVE","ENERGY DEPLOYED",new Color(1,.73f,.27f),1.35f,1);ersAnnouncedAt=now;}
                    lastDRS=car.DRSOpen;lastERS=ers;
                }
            }
            float age=now-noticeBegan;
            float visibility=noticeDuration>0?Mathf.Clamp01(age/.16f)*Mathf.Clamp01((noticeDuration-age)/.28f):0;
            if(race.State==RaceState.Menu || race.State==RaceState.Paused || race.State==RaceState.Finished)visibility=0;
            if(notice)
            {
                notice.alpha=visibility;
                ((RectTransform)notice.transform).anchoredPosition=noticeOrigin+Vector2.up*18*(1-Mathf.Clamp01(age/.25f));
            }
            float stateAge=now-stateBegan;
            if(wipe)
            {
                float t=stateAge/.58f;wipe.alpha=race.State==RaceState.Countdown && t<1?1:0;
                if(wipeStripes!=null)for(int i=0;i<wipeStripes.Length;i++)if(wipeStripes[i])
                    wipeStripes[i].anchoredPosition=new Vector2(Mathf.Lerp(-1750,2150,Mathf.Clamp01(t))+i*215,0);
            }
            if(dashboard && dashboard.gear)
            {
                float pop=Mathf.Exp(-Mathf.Max(0,now-gearBegan)*15);
                dashboard.gear.transform.localScale=gearScale*(1+pop*.24f);
                dashboard.gear.color=Color.Lerp(Color.white,Red,pop*.8f);
            }
            ui.standings.transform.localScale=positionScale*(1+.055f*Mathf.Exp(-Mathf.Max(0,now-positionBegan)*9));
            int lit=race.State==RaceState.Countdown?Mathf.Clamp(6-Mathf.CeilToInt(race.CountdownRemaining),0,5):0;
            if(startCaption){startCaption.text=race.State==RaceState.Countdown?"HOLD YOUR LINE":"LIGHTS OUT";startCaption.color=race.State==RaceState.Countdown?Color.white:Mint;}
            for(int i=0;i<lightScales.Length;i++)
            {
                float pop=i==lit-1?Mathf.Exp(-Mathf.Max(0,now-countBegan)*10):0;
                broadcast.lights[i].transform.localScale=lightScales[i]*(1+pop*.18f);
                if(lightHalos!=null && i<lightHalos.Length && lightHalos[i])
                {var color=Red;color.a=i<lit?.16f+pop*.20f:0;lightHalos[i].color=color;}
            }
            float target=race.State==RaceState.Racing?Mathf.InverseLerp(210,315,car.SpeedKmh):0;
            SpeedEffectAmount=Mathf.Lerp(SpeedEffectAmount,target,1-Mathf.Exp(-5*Time.unscaledDeltaTime));
            if(speedStreaks!=null)for(int i=0;i<speedStreaks.Length;i++)if(speedStreaks[i])
            {
                float phase=Mathf.Repeat(now*(1.2f+SpeedEffectAmount)+i*.173f,1);bool right=(i&1)==1;
                var r=speedStreaks[i];r.anchoredPosition=new Vector2((right?-1:1)*Mathf.Lerp(160,-65,phase),-160+(i/2)*106);
                r.sizeDelta=new Vector2(36+SpeedEffectAmount*82,1.5f);
                var graphic=r.GetComponent<UnityEngine.UI.Image>();var color=new Color(.68f,.80f,.9f,SpeedEffectAmount*.14f*Mathf.Sin(phase*Mathf.PI));graphic.color=color;
            }
            bool victory=race.State==RaceState.Finished && race.PlayerWon;
            if(finishRibbons!=null)for(int i=0;i<finishRibbons.Length;i++)if(finishRibbons[i])
            {
                var r=finishRibbons[i];float t=Mathf.Clamp01(stateAge/2.3f);
                r.anchoredPosition=new Vector2(100+i*73+Mathf.Sin(i*2.1f)*t*100,-30-t*110+Mathf.Sin(t*8+i)*12);
                r.localRotation=Quaternion.Euler(0,0,i*37+stateAge*(i%2==0?90:-70));
                var c=i%3==0 && victory?new Color(1,.76f,.24f):i%2==0?Red:Color.white;
                c.a=race.State==RaceState.Finished?Mathf.Sin(t*Mathf.PI)*.7f:0;r.GetComponent<UnityEngine.UI.Image>().color=c;
            }
        }
        void OnDisable()
        {
            if(!initialized)return;
            if(dashboard && dashboard.gear){dashboard.gear.transform.localScale=gearScale;dashboard.gear.color=Color.white;}
            if(ui && ui.standings)ui.standings.transform.localScale=positionScale;
            if(broadcast)for(int i=0;i<lightScales.Length;i++)if(broadcast.lights[i])broadcast.lights[i].transform.localScale=lightScales[i];
            if(notice)notice.alpha=0;if(wipe)wipe.alpha=0;
        }
    }
}
