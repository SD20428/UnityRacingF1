#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Overdrive
{
    public sealed class GameFeelVerificationProbe : MonoBehaviour
    {
        [Serializable] public sealed class Report
        {
            public bool passed=true;public int checks,frames,gearPulses,countdownPulses,notices;
            public float maximumSpeed,maximumSpeedEffect,pausedReveal;
            public List<string> assertions=new List<string>();
        }
        public static string Status="Not run";
        readonly Report report=new Report();RaceUI ui;RacePresentationMotion motion;PlayerVehicleInput input;AIDriver temporaryDriver;
        readonly List<GameObject> originals=new List<GameObject>();int beganFrame;
        public static string Run()
        {
            if(!Application.isPlaying || FindFirstObjectByType<GameFeelVerificationProbe>())throw new InvalidOperationException("Fresh fixture required");
            new GameObject("Temporary presentation verification").AddComponent<GameFeelVerificationProbe>();return "Menu, countdown, physical driving, paused UI, lap and finish checks started";
        }
        void Check(bool ok,string label){report.checks++;report.passed&=ok;report.assertions.Add((ok?"PASS ":"FAIL ")+label);}
        bool Hit(UnityEngine.UI.Button button)
        {
            var canvas=ui.GetComponent<Canvas>();
            var rect=button.GetComponent<RectTransform>();var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
            var data=new PointerEventData(EventSystem.current){position=point};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            return hits.Count>0 && (hits[0].gameObject==button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform));
        }
        IEnumerator Start()
        {
            beganFrame=Time.frameCount;ui=FindFirstObjectByType<RaceUI>();motion=ui.GetComponent<RacePresentationMotion>();
            var race=ui.race;input=race.player.GetComponent<PlayerVehicleInput>();input.enabled=false;
            race.MainMenu();ui.menu.SetActive(false);ui.menu.SetActive(true);
            EventSystem.current.SetSelectedGameObject(null);EventSystem.current.SetSelectedGameObject(ui.startButton.gameObject);
            Status="Menu and button interaction";
            Check(ui.menu.GetComponent<UIRevealMotion>().RevealProgress<.2f,"Menu reveal begins from its opening pose");
            yield return new WaitForSecondsRealtime(1.05f);
            Check(ui.menu.GetComponent<UIRevealMotion>().RevealProgress>.99f,"Menu stagger completes");
            var intro=ui.menu.GetComponent<RedIntroMotion>();var heroBefore=intro.hero.anchoredPosition;
            var data=new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute<IPointerEnterHandler>(ui.hardButton.gameObject,data,ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.2f);float hover=ui.hardButton.transform.localScale.x;
            Check(hover>1.008f,"Hover scales the existing button");
            ExecuteEvents.Execute<IPointerDownHandler>(ui.hardButton.gameObject,data,ExecuteEvents.pointerDownHandler);
            yield return new WaitForSecondsRealtime(.12f);
            Check(ui.hardButton.transform.localScale.x<hover-.02f,"Pointer press compresses the button");
            ExecuteEvents.Execute<IPointerUpHandler>(ui.hardButton.gameObject,data,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute<IPointerExitHandler>(ui.hardButton.gameObject,data,ExecuteEvents.pointerExitHandler);
            EventSystem.current.SetSelectedGameObject(ui.startButton.gameObject);
            yield return new WaitForSecondsRealtime(.25f);
            Check(Mathf.Abs(ui.hardButton.transform.localScale.x-1)<.003f,"Button returns to its original scale");
            Check(Vector2.Distance(heroBefore,intro.hero.anchoredPosition)>.05f,"Menu hero advances over real frames");
            Check(Hit(ui.startButton) && Hit(ui.professionalButton),"Presentation graphics do not intercept menu clicks");
            ui.professionalButton.onClick.Invoke();yield return null;
            Check(race.ProfessionalSelected && intro.modeSummary.text.Contains("ELITE"),"Mode selection and description update");
            Check(race.player.Throttle==0 && race.player.Body.linearVelocity.sqrMagnitude<.001f,"Menu effects leave vehicle controls and physics idle");
            ScreenCapture.CaptureScreenshot("Docs/GameFeel_Menu.png");
            yield return null;
            temporaryDriver=race.player.gameObject.AddComponent<AIDriver>();temporaryDriver.path=race.ai.path;temporaryDriver.difficulty=race.professional;temporaryDriver.preferredLane=0;
            race.BeginRace();
            foreach(var entry in race.Racers)if(entry.vehicle!=race.player){originals.Add(entry.vehicle.gameObject);entry.vehicle.gameObject.SetActive(false);}
            Status="Five-light start sequence";yield return new WaitForSecondsRealtime(1.2f);
            Check(motion.CountdownPulseCount>0,"Countdown ticks trigger light pulses");
            ScreenCapture.CaptureScreenshot("Docs/GameFeel_Countdown.png");
            while(race.State==RaceState.Countdown)yield return null;
            yield return new WaitForSecondsRealtime(.25f);
            Check(motion.CountdownPulseCount>=5,"All five countdown stages animate");
            Check(motion.noticeTitle.text=="LIGHTS OUT" && motion.notice.alpha>.5f,"Race start notice appears");
            ScreenCapture.CaptureScreenshot("Docs/GameFeel_LightsOut.png");
            Status="Physical acceleration and gear-change feedback";
            float driveUntil=Time.unscaledTime+13;
            while(Time.unscaledTime<driveUntil)
            {
                report.maximumSpeed=Mathf.Max(report.maximumSpeed,race.player.SpeedKmh);
                report.maximumSpeedEffect=Mathf.Max(report.maximumSpeedEffect,motion.SpeedEffectAmount);
                yield return null;
            }
            Check(motion.GearPulseCount>=3,"Actual automatic gear changes pulse the gear readout");
            Check(report.maximumSpeed>210 && report.maximumSpeedEffect>.02f,"High-speed edge accents follow actual driving speed");
            ScreenCapture.CaptureScreenshot("Docs/GameFeel_Driving.png");
            yield return null;
            Status="Paused animation and resume";race.TogglePause();yield return null;
            float initialPause=ui.pause.GetComponent<UIRevealMotion>().RevealProgress;
            yield return new WaitForSecondsRealtime(.6f);
            report.pausedReveal=ui.pause.GetComponent<UIRevealMotion>().RevealProgress;
            Check(Time.timeScale==0 && initialPause<.5f && report.pausedReveal>.99f,"Pause panel animates while simulation time is stopped");
            var pausePosition=ui.resumeButton.GetComponent<RectTransform>().anchoredPosition;
            Check(Hit(ui.resumeButton),"Resume button remains clickable");ScreenCapture.CaptureScreenshot("Docs/GameFeel_Pause.png");
            ui.resumeButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.2f);
            Check(race.State==RaceState.Racing && Time.timeScale==1,"Resume retains the existing race action");
            race.TogglePause();yield return new WaitForSecondsRealtime(.6f);
            Check(Vector2.Distance(pausePosition,ui.resumeButton.GetComponent<RectTransform>().anchoredPosition)<.01f,"Repeated pause does not accumulate transform offsets");
            ui.resumeButton.onClick.Invoke();yield return null;
            Status="Lap and finish presentation";
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            typeof(CircuitProgress).GetField("<CompletedLaps>k__BackingField",flags).SetValue(race.PlayerProgress,1);
            yield return new WaitForSecondsRealtime(.25f);
            Check(motion.noticeTitle.text=="FINAL LAP" && motion.notice.alpha>.5f,"Final-lap event produces the priority notice");
            ScreenCapture.CaptureScreenshot("Docs/GameFeel_FinalLap.png");
            yield return null;
            typeof(CircuitProgress).GetField("<LastCheckpoint>k__BackingField",flags).SetValue(race.PlayerProgress,race.PlayerProgress.checkpoints.Length-1);
            typeof(CircuitProgress).GetField("awaitingStartLine",flags).SetValue(race.PlayerProgress,false);
            race.player.Body.linearVelocity=race.PlayerProgress.checkpoints[0].forward*5;
            Check(race.PlayerProgress.Pass(0),"Production finish gate accepts the staged final lap");
            yield return new WaitForSecondsRealtime(1.05f);
            Check(race.State==RaceState.Finished && ui.results.GetComponent<UIRevealMotion>().RevealProgress>.99f,"Results reveal completes with the existing finish action");
            Check(Hit(ui.retryButton),"Finish decorations do not block retry");ScreenCapture.CaptureScreenshot("Docs/GameFeel_Results.png");
            var sensor=temporaryDriver.GetComponent<VehiclePerception>();if(sensor)Destroy(sensor);
            yield return null;
            Destroy(temporaryDriver);temporaryDriver=null;yield return null;
            Check(!race.player.GetComponent<AIDriver>() && !race.player.GetComponent<VehiclePerception>(),"Temporary driver and its dependent sensor are removed cleanly");
            ui.retryButton.onClick.Invoke();yield return null;
            Check(race.State==RaceState.Countdown && race.PlayerProgress.CompletedLaps==0,"Retry starts a clean race");
            race.MainMenu();foreach(var original in originals)if(original)original.SetActive(true);input.enabled=true;
            yield return new WaitForSecondsRealtime(1);
            Check(ui.menu.GetComponent<UIRevealMotion>().RevealProgress>.99f && Hit(ui.startButton),"Returning menu finishes its reveal and remains interactive");
            report.frames=Time.frameCount-beganFrame;report.gearPulses=motion.GearPulseCount;report.countdownPulses=motion.CountdownPulseCount;report.notices=motion.NoticeCount;
            File.WriteAllText("Docs/GameFeel_Verification.json",JsonUtility.ToJson(report,true));
            Status=(report.passed?"PASS ":"FAIL ")+report.checks+" checks";
            if(!report.passed)Debug.LogError(Status+": inspect Docs/GameFeel_Verification.json");
            Destroy(gameObject);
        }
    }
}
#endif
