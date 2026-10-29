using UnityEngine;
using TMPro;
namespace Overdrive
{
    public sealed class RaceUI : MonoBehaviour
    {
        public RaceManager race;
        public GameObject menu, hud, pause, results, countdown;
        public TMP_Text standings, clock, speed, countdownText, resultText;
        public bool racingTheme;
        public UnityEngine.UI.Button startButton, normalButton, hardButton, quitButton;
        public UnityEngine.UI.Button professionalButton;
        public UnityEngine.UI.Button grandPrixButton,qualifyingButton;
        public TMP_Text sessionDescription,startLabel;
        public TMP_Text sessionGridChip,sessionLapChip,eventTitle,weekendStatus,resultFooter,retryLabel,resultHeading;
        public GameObject aiLegend;
        public UnityEngine.UI.Button resumeButton, restartButton, pauseMenuButton, retryButton, resultMenuButton;
        RaceState? previousState;
        static void Bind(UnityEngine.UI.Button button,UnityEngine.Events.UnityAction action)
        { button.onClick.RemoveListener(action); button.onClick.AddListener(action); }
        void OnEnable()
        {
            if(!race || !menu || !hud || !pause || !results || !countdown || !standings || !clock || !speed || !countdownText || !resultText)
            { Debug.LogError("RaceUI has missing scene references.",this); enabled=false; return; }
            Bind(startButton,race.BeginRace);
            Bind(normalButton,race.SelectNormal); Bind(hardButton,race.SelectHard);
            if(professionalButton)Bind(professionalButton,race.SelectProfessional);
            if(grandPrixButton)Bind(grandPrixButton,race.SelectGrandPrix);
            if(qualifyingButton)Bind(qualifyingButton,race.SelectQualifying);
            Bind(quitButton,Application.Quit);
            Bind(resumeButton,race.TogglePause); Bind(restartButton,race.BeginRace);
            Bind(pauseMenuButton,race.MainMenu); Bind(retryButton,race.BeginRace);
            Bind(resultMenuButton,race.MainMenu);
        }
        void Update()
        {
            var state=race.State;
            normalButton.interactable=hardButton.interactable=!race.IsQualifying;
            if(professionalButton)professionalButton.interactable=!race.IsQualifying;
            if(grandPrixButton && qualifyingButton)
            {
                var nav=grandPrixButton.navigation;nav.selectOnDown=race.IsQualifying?startButton:normalButton;grandPrixButton.navigation=nav;
                nav=qualifyingButton.navigation;nav.selectOnDown=race.IsQualifying?startButton:hardButton;qualifyingButton.navigation=nav;
                nav=startButton.navigation;nav.selectOnUp=race.IsQualifying?qualifyingButton:professionalButton?professionalButton:normalButton;startButton.navigation=nav;
            }
            if(startLabel)startLabel.text=race.IsQualifying?"START QUALIFYING                      >":"START RACE                              >";
            if(sessionDescription)sessionDescription.text=race.IsQualifying?"SOLO QUALIFYING  /  1 OUT LAP + 3 TIMED LAPS":"20 CARS  /  GRAND PRIX  /  2 LAPS";
            if(sessionGridChip)sessionGridChip.text=race.IsQualifying?"SOLO / CLEAR TRACK":"20-CAR GRID";
            if(sessionLapChip)sessionLapChip.text=race.IsQualifying?"3 TIMED LAPS":"2-LAP SPRINT";
            if(eventTitle)eventTitle.text=$"{race.CircuitName}  /  {(race.IsQualifying?"QUALIFYING":"GRAND PRIX")}";
            if(weekendStatus)weekendStatus.text=$"{race.CircuitName} / {(race.IsQualifying?"QUALIFYING":"GRAND PRIX")}";
            if(resultFooter)resultFooter.text=$"{race.CircuitName} {(race.IsQualifying?"QUALIFYING  /  3 TIMED LAPS":"GRAND PRIX  /  2 LAPS")}";
            if(resultHeading)resultHeading.text=race.IsQualifying?"SESSION RESULTS":"RACE CLASSIFICATION";
            if(retryLabel)retryLabel.text=race.IsQualifying?"TRY AGAIN   >":"RACE AGAIN   >";
            if(aiLegend)aiLegend.SetActive(!race.IsQualifying);
            if(grandPrixButton)grandPrixButton.GetComponent<UnityEngine.UI.Image>().color=race.IsQualifying?new Color(.06f,.09f,.13f):new Color(.38f,.035f,.055f);
            if(qualifyingButton)qualifyingButton.GetComponent<UnityEngine.UI.Image>().color=race.IsQualifying?new Color(.38f,.035f,.055f):new Color(.06f,.09f,.13f);
            menu.SetActive(state==RaceState.Menu); hud.SetActive(state==RaceState.Countdown || state==RaceState.Racing || state==RaceState.Paused);
            pause.SetActive(state==RaceState.Paused); results.SetActive(state==RaceState.Finished);
            bool showCount=state==RaceState.Countdown || (state==RaceState.Racing && race.Elapsed<.7f);
            countdown.SetActive(showCount);
            if(showCount) countdownText.text=state==RaceState.Countdown?Mathf.CeilToInt(race.CountdownRemaining).ToString():"GO!";
            bool normalSelected=race.SelectedDifficulty==RaceDifficulty.Normal;
            normalButton.GetComponent<UnityEngine.UI.Image>().color=racingTheme?(!normalSelected?new Color(.06f,.09f,.13f):new Color(.06f,.29f,.32f)):(!normalSelected?new Color(.12f,.17f,.23f):new Color(.02f,.45f,.65f));
            hardButton.GetComponent<UnityEngine.UI.Image>().color=racingTheme?(race.HardSelected?new Color(.37f,.18f,.11f):new Color(.06f,.09f,.13f)):(race.HardSelected?new Color(.8f,.25f,.06f):new Color(.12f,.17f,.23f));
            if(professionalButton)professionalButton.GetComponent<UnityEngine.UI.Image>().color=race.ProfessionalSelected?new Color(.42f,.12f,.04f):new Color(.10f,.105f,.12f);
            standings.text=$"순위  {race.Position}/{race.FieldSize}\nLAP  {Mathf.Min(2,race.PlayerProgress.CompletedLaps+1)}/2";
            clock.text=$"경과  {Format(race.Elapsed)}\n현재 랩  {Format(race.CurrentLap)}";
            speed.text=$"{race.player.SpeedKmh:0} km/h\n기어 {race.player.Gear}";
            if(race.player.PowertrainEnabled)
            {
                var car=race.player;
                string warning=car.BrakeLocking?"BRAKE LOCK":car.FrontGripUsage>1.15f?"FRONT SLIP":car.RearWheelspin?"WHEELSPIN":"";
                speed.text=$"{car.SpeedKmh:0} km/h\n<size=55%>{(car.ManualTransmission?"M":"A")} 기어 {car.Gear}   {car.EngineRPM:0} RPM</size>\n<size=40%><color=#FFBD69>{warning}</color></size>";
            }
            if(racingTheme)
            {
                standings.text=$"<size=13>POSITION</size>\n<size=50>{race.Position:00}</size><size=20> / {race.FieldSize:00}    LAP {Mathf.Min(2,race.PlayerProgress.CompletedLaps+1)}/2</size>";
                clock.text=$"<size=13>RACE TIME</size>\n<size=34>{Format(race.Elapsed)}</size>\n<size=17>LAP  {Format(race.CurrentLap)}</size>";
                speed.text=$"{race.player.SpeedKmh:000}";
            }
            if(state==RaceState.Finished)
            {
                string delta=race.FieldSize>2?"CLASSIFICATION AT PLAYER FINISH":race.AIFinish>=0?$"AI 대비  {race.PlayerFinish-race.AIFinish:+0.00;-0.00;0.00} s":"AI 미완주 — 종료 시점 기준";
                resultText.text=$"{(race.PlayerWon?"WIN / 승리":"LOSE / 패배")}\n\n총 시간  {Format(race.PlayerFinish)}\n베스트 랩  {Format(race.PlayerLaps.BestLap)}\n{delta}";
                if(racingTheme) resultText.text=$"<size=66><i>{(race.PlayerWon?"VICTORY":"RACE COMPLETE")}</i></size>\n<size=22>POSITION {race.Position:00} / {race.FieldSize:00}</size>\n\n<size=19>TOTAL TIME</size>   <size=34>{Format(race.PlayerFinish)}</size>\n<size=19>BEST LAP</size>     <size=34>{Format(race.PlayerLaps.BestLap)}</size>\n<size=19>{delta}</size>";
            }
            if(race.IsQualifying)
            {
                string lap=race.PlayerProgress.CompletedLaps==0?"OUT LAP":$"TIMED LAP {Mathf.Min(3,race.TimedLapCount+1)} / 3";
                standings.text=$"<size=13>QUALIFYING / SOLO</size>\n<size=32>{lap}</size>\n<size=18>BEST  {Format(race.PlayerLaps.BestLap)}</size>";
                clock.text=$"<size=13>{(race.PlayerProgress.CompletedLaps==0?"OUT LAP TIME":"CURRENT LAP")}</size>\n<size=34>{Format(race.CurrentLap)}</size>\n<size=17>SESSION  {Format(race.Elapsed)}</size>";
                if(state==RaceState.Finished)resultText.text=$"<size=44><i>QUALIFYING COMPLETE</i></size>\n<size=19>3 TIMED LAPS  /  SOLO SESSION</size>\n\n<size=19>BEST LAP</size>  <size=38>{Format(race.PlayerLaps.BestLap)}</size>\n<size=19>SESSION TIME</size>  <size=28>{Format(race.PlayerFinish)}</size>";
            }
            if(previousState!=state)
            {
                // Rebuild after reactivating panels so cached multi-atlas text is complete immediately.
                Canvas.ForceUpdateCanvases();
                if(state==RaceState.Finished) resultText.ForceMeshUpdate();
                var events=UnityEngine.EventSystems.EventSystem.current;
                if(events) events.SetSelectedGameObject(state==RaceState.Menu?startButton.gameObject:state==RaceState.Paused?resumeButton.gameObject:state==RaceState.Finished?retryButton.gameObject:null);
                previousState=state;
            }
        }
        public static string Format(float seconds)
        {
            if(seconds<0) return "--:--.--";
            int hundredths=Mathf.FloorToInt(seconds*100);
            return $"{hundredths/6000:00}:{hundredths/100%60:00}.{hundredths%100:00}";
        }
    }
}
