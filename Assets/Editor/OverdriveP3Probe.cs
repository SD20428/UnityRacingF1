using UnityEditor;
using UnityEngine;
using Overdrive;
using System.Collections.Generic;

// Physical-input integration test only. Not a player assist or shipped driving component.
public static class OverdriveP3Probe
{
    static RaceManager race;
    static RaceUI ui;
    static PlayerVehicleInput input;
    static bool hard, paused, resumed, countdownChecked;
    static double pauseReal;
    static float pauseClock;
    static float maxPlayerSpeed, maxAISpeed;
    static Vector3 grid;
    static List<string> log;
    public static string Result="Not run";
    public static void Run(bool hardMode=false)
    {
        EditorApplication.update-=Tick;
        race=Object.FindFirstObjectByType<RaceManager>(); ui=Object.FindFirstObjectByType<RaceUI>(); input=race.player.GetComponent<PlayerVehicleInput>();
        race.MainMenu(); hard=hardMode; paused=resumed=countdownChecked=false; log=new List<string>();
        maxPlayerSpeed=maxAISpeed=0;
        log.Add($"menu={race.State}; playerLocked={!race.player.ControlsEnabled}; aiLocked={!race.opponent.ControlsEnabled}; progressLocked={!race.PlayerProgress.AcceptingCheckpoints}");
        if(hard) ui.hardButton.onClick.Invoke(); else ui.normalButton.onClick.Invoke();
        ui.startButton.onClick.Invoke(); grid=race.player.Body.position;
        bool accepted=race.PlayerProgress.Pass(race.PlayerProgress.NextCheckpoint);
        log.Add($"difficulty={race.ai.difficulty.name}; countdown={race.State}; earlyCheckpointAccepted={accepted}");
        input.enabled=false; Result="Running"; EditorApplication.update+=Tick;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || !race) { EditorApplication.update-=Tick; return; }
        maxPlayerSpeed=Mathf.Max(maxPlayerSpeed,race.player.SpeedKmh); maxAISpeed=Mathf.Max(maxAISpeed,race.opponent.SpeedKmh);
        if(!countdownChecked && race.State==RaceState.Countdown && race.CountdownRemaining<1)
        {
            countdownChecked=true;
            log.Add($"countdownDistance={Vector3.Distance(grid,race.player.Body.position):F5}; playerLocked={!race.player.ControlsEnabled}; aiLocked={!race.opponent.ControlsEnabled}");
        }
        if(!paused && race.State==RaceState.Racing && race.Elapsed>3)
        {
            paused=true; race.TogglePause(); pauseClock=race.Elapsed; pauseReal=EditorApplication.timeSinceStartup;
        }
        if(paused && !resumed && EditorApplication.timeSinceStartup-pauseReal>1)
        {
            log.Add($"pauseState={race.State}; clockDelta={race.Elapsed-pauseClock:F6}; timeScale={Time.timeScale}; playerLocked={!race.player.ControlsEnabled}");
            resumed=true; race.TogglePause();
            log.Add($"resume={race.State}; bothEnabled={race.player.ControlsEnabled && race.opponent.ControlsEnabled}");
        }
        if(race.State==RaceState.Finished || race.Elapsed>240)
        {
            log.Add($"state={race.State}; won={race.PlayerWon}; playerTime={race.PlayerFinish:F3}; aiTime={race.AIFinish:F3}; playerLaps={race.PlayerProgress.CompletedLaps}; aiLaps={race.AIProgress.CompletedLaps}; bestLap={race.PlayerLaps.BestLap:F3}; aiRecoveries={race.ai.RecoveryCount}; position={race.Position}; finishProgressLocked={!race.PlayerProgress.AcceptingCheckpoints && !race.AIProgress.AcceptingCheckpoints}");
            log.Add($"maxPlayerSpeed={maxPlayerSpeed:F2}; maxAISpeed={maxAISpeed:F2}");
            Result=string.Join("\n",log);
            System.IO.File.WriteAllText("Docs/"+(race.player.MaximumForwardKmh>200?"F1_":"P3_")+(hard?"HARD":"NORMAL")+"_Race.txt",Result);
            Debug.Log("P3 integration probe:\n"+Result);
            input.enabled=true; EditorApplication.update-=Tick; return;
        }
        if(race.State!=RaceState.Racing || !race.player.ControlsEnabled) return;
        var progress=race.PlayerProgress; var vehicle=race.player;
        if(vehicle.ManualTransmission)
        {
            if(vehicle.RoadEngineRPM>12500)vehicle.ShiftUp();
            else if(vehicle.RoadEngineRPM<6500)vehicle.ShiftDown();
        }
        int next=progress.NextCheckpoint;
        var target=progress.checkpoints[next].position-progress.checkpoints[next].right*2.5f;
        bool formula=vehicle.MaximumForwardKmh>200;
        float look=formula?12+vehicle.SpeedKmh*.12f:10+vehicle.SpeedKmh*.15f;
        for(int i=0;i<(formula?16:4) && Vector3.Distance(vehicle.transform.position,target)<look;i++)
        { next=(next+1)%progress.checkpoints.Length; target=progress.checkpoints[next].position-progress.checkpoints[next].right*2.5f; }
        var local=vehicle.transform.InverseTransformPoint(target);
        float wheelbase=Mathf.Abs(vehicle.wheels[0].transform.localPosition.z-vehicle.wheels[2].transform.localPosition.z);
        float angle=Mathf.Atan2(2*wheelbase*local.x,local.x*local.x+local.z*local.z)*Mathf.Rad2Deg;
        vehicle.Steering=Mathf.Clamp(angle/vehicle.CurrentSteeringLimit,-1,1);
        float desired=Mathf.Lerp(hard?57:74,hard?30:38,Mathf.Clamp01(Mathf.Abs(angle)/18));
        if(formula)
        {
            desired=Mathf.Lerp(hard?180:190,60,Mathf.Clamp01(Mathf.Abs(angle)/18));
            float curvature=race.ai.path.CurvatureAhead(progress.NextCheckpoint,85);
            if(curvature>.0001f) desired=Mathf.Min(desired,Mathf.Sqrt((hard?13:12)/curvature)*3.6f);
        }
        vehicle.Throttle=vehicle.SpeedKmh<desired?1:0; vehicle.Brake=vehicle.SpeedKmh>desired+3?.35f:0;
    }
    public static string CheckRetryAndMenu()
    {
        race=Object.FindFirstObjectByType<RaceManager>(); ui=Object.FindFirstObjectByType<RaceUI>();
        ui.retryButton.onClick.Invoke();
        string retry=$"retryState={race.State}; clock={race.Elapsed}; playerLaps={race.PlayerProgress.CompletedLaps}; aiLaps={race.AIProgress.CompletedLaps}; playerCP={race.PlayerProgress.LastCheckpoint}; aiCP={race.AIProgress.LastCheckpoint}; playerTime={race.PlayerFinish}; aiTime={race.AIFinish}; bestLap={race.PlayerLaps.BestLap}; aiRecoveries={race.ai.RecoveryCount}; playerSpeed={race.player.SpeedKmh}; aiSpeed={race.opponent.SpeedKmh}";
        race.MainMenu();
        string menu=$"menu={race.State}; clock={race.Elapsed}; playerLaps={race.PlayerProgress.CompletedLaps}; aiLaps={race.AIProgress.CompletedLaps}; gridDistance={Vector3.Distance(race.player.Body.position,race.opponent.Body.position):F3}; timeScale={Time.timeScale}; inputsLocked={!race.player.ControlsEnabled && !race.opponent.ControlsEnabled}";
        string result=retry+"\n"+menu; System.IO.File.WriteAllText("Docs/P3_Reset_Result.txt",result); return result;
    }
}
