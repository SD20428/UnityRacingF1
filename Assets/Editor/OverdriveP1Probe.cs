using UnityEditor;
using UnityEngine;
using Overdrive;

// Editor-only integration probe, not the P2 AI driver or a shipped game component.
public static class OverdriveP1Probe
{
    static OverdriveVehicle vehicle;
    static CircuitProgress progress;
    static float started;
    static int firstFrame;
    public static string Result = "Not run";
    public static void Start()
    {
        Stop();
        vehicle=Object.FindFirstObjectByType<OverdriveVehicle>(); progress=vehicle.GetComponent<CircuitProgress>();
        vehicle.GetComponent<PlayerVehicleInput>().enabled=false;
        progress.Recover(); started=Time.time; firstFrame=Time.frameCount;
        Result="Running"; EditorApplication.update+=Tick;
    }
    public static void Stop()
    {
        EditorApplication.update-=Tick;
        if(vehicle) { vehicle.Throttle=vehicle.Steering=vehicle.Brake=0; vehicle.GetComponent<PlayerVehicleInput>().enabled=true; }
        Time.timeScale=1;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || !vehicle) { Stop(); return; }
        if(progress.TwoLapsComplete || Time.time-started>240)
        {
            Result=$"laps={progress.CompletedLaps}; checkpoint={progress.LastCheckpoint}; seconds={Time.time-started:F2}; frames={Time.frameCount-firstFrame}; complete={progress.TwoLapsComplete}";
            Debug.Log("P1 physical probe: " + Result); Stop(); return;
        }
        int next=progress.NextCheckpoint;
        var target=progress.checkpoints[next].position;
        float look=10+vehicle.SpeedKmh*.15f;
        for(int count=0;count<4 && Vector3.Distance(vehicle.transform.position,target)<look;count++)
        { next=(next+1)%progress.checkpoints.Length; target=progress.checkpoints[next].position; }
        var local=vehicle.transform.InverseTransformPoint(target);
        float steer=Mathf.Atan2(2*2.7f*local.x,local.x*local.x+local.z*local.z)*Mathf.Rad2Deg;
        float maxSteer=Mathf.Lerp(30,10,vehicle.SpeedKmh/140);
        vehicle.Steering=Mathf.Clamp(steer/maxSteer,-1,1);
        float desired=Mathf.Lerp(65,30,Mathf.Clamp01(Mathf.Abs(steer)/18));
        vehicle.Throttle=vehicle.SpeedKmh<desired?1:0;
        vehicle.Brake=vehicle.SpeedKmh>desired+3?.35f:0;
    }
}
