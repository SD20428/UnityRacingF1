using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
namespace Overdrive
{
    public enum RaceState { Menu, Countdown, Racing, Paused, Finished }
    public enum RaceDifficulty { Normal, Hard, Professional }
    public enum SessionMode { GrandPrix, Qualifying }
    [DefaultExecutionOrder(-200)]
    public sealed class RaceManager : MonoBehaviour
    {
        public OverdriveVehicle player, opponent;
        public AIDriver ai;
        public AIDriver[] gridOpponents;
        public CircuitInfo circuit;
        public string CircuitName=>circuit?circuit.displayName:"APEX PARK";
        public sealed class Racer
        {
            public OverdriveVehicle vehicle;public AIDriver driver;public CircuitProgress progress;public RacerIdentity identity;
            public float finish=-1;public LapTimer laps=new LapTimer();
            internal Vector3 grid;internal Quaternion heading;internal System.Action<int> onLap;
        }
        readonly List<Racer> racers=new List<Racer>(),classification=new List<Racer>();
        public IReadOnlyList<Racer> Racers=>racers;
        public SessionMode SelectedMode {get;private set;}
        public bool IsQualifying=>SelectedMode==SessionMode.Qualifying;
        public int TimedLapCount=>Mathf.Clamp(PlayerProgress?PlayerProgress.CompletedLaps-1:0,0,3);
        public int FieldSize=>IsQualifying?1:racers.Count>0?racers.Count:1+(gridOpponents!=null && gridOpponents.Length>0?gridOpponents.Length:1);
        public IReadOnlyList<Racer> Classification
        {get{classification.Clear();foreach(var e in racers)if(!IsQualifying || e.vehicle==player)classification.Add(e);classification.Sort(CompareRacers);return classification;}}
        static int CompareRacers(Racer a,Racer b)
        {
            if(a.finish>=0 || b.finish>=0){if(a.finish<0)return 1;if(b.finish<0)return -1;int f=a.finish.CompareTo(b.finish);if(f!=0)return f;}
            int progress=b.progress.OrderedProgress.CompareTo(a.progress.OrderedProgress);if(progress!=0)return progress;
            return (a.identity?a.identity.gridSlot:0).CompareTo(b.identity?b.identity.gridSlot:0);
        }
        public AIDifficulty normal, hard, professional;
        public RaceState State { get; private set; } = RaceState.Menu;
        public float CountdownRemaining { get; private set; }
        public float PlayerFinish { get; private set; } = -1;
        public float AIFinish { get; private set; } = -1;
        public bool PlayerWon => !IsQualifying && PlayerFinish>=0 && Position==1;
        public RaceDifficulty SelectedDifficulty { get; private set; }
        public bool HardSelected => SelectedDifficulty==RaceDifficulty.Hard;
        public bool ProfessionalSelected => SelectedDifficulty==RaceDifficulty.Professional;
        public AIDifficulty SelectedPreset => ProfessionalSelected && professional?professional:HardSelected?hard:normal;
        public LapTimer PlayerLaps { get; } = new LapTimer();
        public LapTimer AILaps { get; } = new LapTimer();
        public CircuitProgress PlayerProgress { get; private set; }
        public CircuitProgress AIProgress { get; private set; }
        public float Elapsed => State==RaceState.Racing ? (float)(Time.timeAsDouble-startedAt) : frozenElapsed;
        public float CurrentLap => Mathf.Max(0,Elapsed-PlayerLaps.LastLapEnd);
        public int Position
        {
            get
            {
                if(racers.Count==0)return 1;
                var sorted=Classification;for(int i=0;i<sorted.Count;i++)if(sorted[i].vehicle==player)return i+1;return 1;
            }
        }
        double startedAt;
        float frozenElapsed;
        bool escapeHeld;
        void Start()
        {
            if(racers.Count>0)return;
            if(!player || !opponent || !ai || !normal || !hard)
            { Debug.LogError("RaceManager requires both vehicles, AI and difficulty assets.",this); enabled=false; return; }
            PlayerProgress=player.GetComponent<CircuitProgress>(); AIProgress=opponent.GetComponent<CircuitProgress>();
            if(!PlayerProgress || !AIProgress) { Debug.LogError("Both racers need CircuitProgress.",this); enabled=false; return; }
            AddRacer(player,null);
            if(gridOpponents!=null && gridOpponents.Length>0){foreach(var driver in gridOpponents)if(driver)AddRacer(driver.GetComponent<OverdriveVehicle>(),driver);}
            else AddRacer(opponent,ai);
            MainMenu();
        }
        void OnDestroy()
        {
            Time.timeScale=1;
            foreach(var entry in racers)if(entry.progress)entry.progress.LapCompleted-=entry.onLap;
        }
        void AddRacer(OverdriveVehicle car,AIDriver driver)
        {
            if(racers.Exists(e=>e.vehicle==car))return;
            var identity=car.GetComponent<RacerIdentity>();
            var entry=new Racer{vehicle=car,driver=driver,progress=car.GetComponent<CircuitProgress>(),identity=identity,grid=identity?identity.gridPosition:car.transform.position,heading=identity?identity.gridRotation:car.transform.rotation};
            if(car==player)entry.laps=PlayerLaps;else if(car==opponent)entry.laps=AILaps;
            entry.onLap=lap=>RecordLap(entry,lap);entry.progress.LapCompleted+=entry.onLap;racers.Add(entry);classification.Add(entry);
        }
        void RecordLap(Racer entry,int lap)
        {
            if(State!=RaceState.Racing || entry.finish>=0)return;
            if(IsQualifying && entry.vehicle==player && lap==1){entry.laps.StartAt(Elapsed);return;}
            entry.laps.Record(Elapsed);if(lap<(IsQualifying?4:2))return;entry.finish=Elapsed;
            if(entry.vehicle==opponent)AIFinish=entry.finish;
            if(entry.vehicle==player){PlayerFinish=entry.finish;frozenElapsed=PlayerFinish;State=RaceState.Finished;}
            ApplyRules();
            if(entry.driver)
            {
                // Keep the classification entry, but remove the finished car's body from the circuit.
                entry.driver.CompleteRace(entry.finish);
                entry.vehicle.gameObject.SetActive(false);
            }
        }
        public void SelectNormal() { SelectedDifficulty=RaceDifficulty.Normal; }
        public void SelectHard() { SelectedDifficulty=RaceDifficulty.Hard; }
        public void SelectProfessional() { if(professional)SelectedDifficulty=RaceDifficulty.Professional; }
        public void SelectGrandPrix() { if(State==RaceState.Menu){SelectedMode=SessionMode.GrandPrix;MainMenu();} }
        public void SelectQualifying() { if(State==RaceState.Menu){SelectedMode=SessionMode.Qualifying;MainMenu();} }
        public void BeginRace()
        {
            ResetRace(); CountdownRemaining=FieldSize>2?5:3;
            State=RaceState.Countdown; ApplyRules();
        }
        public void MainMenu()
        {
            ResetRace(); State=RaceState.Menu; ApplyRules();
        }
        void ResetRace()
        {
            Time.timeScale=1; frozenElapsed=0; CountdownRemaining=0; PlayerFinish=AIFinish=-1;
            foreach(var entry in racers)
            {
                entry.finish=-1;entry.laps.Reset();entry.progress.ResetProgress();
                if(entry.driver){entry.driver.difficulty=SelectedPreset;entry.driver.ResetForRace();}
                ResetCar(entry.vehicle,entry.grid,entry.heading);
                entry.vehicle.gameObject.SetActive(!IsQualifying || entry.vehicle==player);
            }
        }
        static void ResetCar(OverdriveVehicle car,Vector3 position,Quaternion rotation)
        {
            car.ResetDynamics();
            car.Body.constraints=RigidbodyConstraints.None;
            car.Body.linearVelocity=car.Body.angularVelocity=Vector3.zero;
            car.Body.position=position; car.Body.rotation=rotation;
            car.transform.SetPositionAndRotation(position,rotation);
            car.Throttle=car.Steering=0; car.Brake=1;
        }
        public void TogglePause()
        {
            if(State==RaceState.Racing)
            { frozenElapsed=Elapsed; State=RaceState.Paused; Time.timeScale=0; ApplyRules(); }
            else if(State==RaceState.Paused)
            { Time.timeScale=1; startedAt=Time.timeAsDouble-frozenElapsed; State=RaceState.Racing; ApplyRules(); }
        }
        void Update()
        {
            // Script hot reload discards nonserialized event subscriptions and standings.
            // Rebuild from saved grid poses instead of silently running an empty field.
            if(racers.Count==0){Start();if(racers.Count==0)return;}
            bool pressed=Keyboard.current!=null && Keyboard.current.escapeKey.isPressed;
            // A held key must not toggle twice when editor/input updates are decoupled.
            if(pressed && !escapeHeld) TogglePause();
            escapeHeld=pressed;
            if(State!=RaceState.Countdown) return;
            CountdownRemaining-=Time.unscaledDeltaTime;
            if(CountdownRemaining<=0)
            { CountdownRemaining=0; startedAt=Time.timeAsDouble; frozenElapsed=0; foreach(var e in racers)if(e.driver)e.driver.ResetForRace(); State=RaceState.Racing; ApplyRules(); }
        }
        void PlayerLap(int lap)
        {
            RecordLap(racers.Find(e=>e.vehicle==player),lap);
        }
        void AILap(int lap)
        {
            RecordLap(racers.Find(e=>e.vehicle==opponent),lap);
        }
        void ApplyRules()
        {
            bool racing=State==RaceState.Racing;
            foreach(var entry in racers)SetCar(entry.vehicle,entry.progress,racing && entry.finish<0 && (!IsQualifying || entry.vehicle==player));
        }
        static void SetCar(OverdriveVehicle car,CircuitProgress progress,bool allow)
        {
            car.ControlsEnabled=allow; progress.AcceptingCheckpoints=allow;
            car.Body.constraints=allow?RigidbodyConstraints.None:RigidbodyConstraints.FreezeAll;
            if(!allow) { car.Throttle=car.Steering=0; car.Brake=1; car.Body.linearVelocity=car.Body.angularVelocity=Vector3.zero; }
            else { car.Brake=0; car.Body.WakeUp(); }
        }
    }
}
