using UnityEngine;
using TMPro;

namespace Overdrive
{
    [DefaultExecutionOrder(280)]
    public sealed class QualifyingLineGates : MonoBehaviour
    {
        [System.Serializable] public sealed class Gate
        {
            public int checkpoint;
            public Transform visual;
            public Renderer[] panels,frames;
        }
        public RaceManager race;
        public Gate[] gates;
        public GameObject hudCard;
        public TMP_Text feedback,summary,resultSummary;
        public readonly LineGateScorecard Score=new LineGateScorecard();
        public LineGateGrade LastGrade {get;private set;}
        public int NextGate {get;private set;}
        public int CompletedPerfectLaps {get;private set;}
        CircuitProgress progress;RaceState? previousState;
        Vector3 previousPosition;bool sampled,bound;float feedbackUntil;
        MaterialPropertyBlock block;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        static readonly Color Purple=new Color(.7f,.27f,1),Green=new Color(.2f,1,.66f),Red=new Color(1,.25f,.32f);
        void Start()
        {
            if(!race || gates==null || gates.Length==0){enabled=false;return;}
            block=new MaterialPropertyBlock();progress=race.PlayerProgress;
            progress.CheckpointPassed+=OnCheckpoint;bound=true;ResetSession();
        }
        void OnEnable(){if(progress && !bound){progress.CheckpointPassed+=OnCheckpoint;bound=true;}}
        void OnDisable(){if(progress && bound){progress.CheckpointPassed-=OnCheckpoint;bound=false;}Hide();}
        void OnDestroy(){if(progress && bound)progress.CheckpointPassed-=OnCheckpoint;}
        void Hide(){if(gates!=null)foreach(var g in gates)if(g.visual)g.visual.gameObject.SetActive(false);if(hudCard)hudCard.SetActive(false);}
        public void ResetSession()
        {Score.Reset(gates.Length);NextGate=CompletedPerfectLaps=0;sampled=false;LastGrade=LineGateGrade.Pending;feedbackUntil=0;}
        void Judge(int index,LineGateGrade grade)
        {
            if(!Score.Record(index,grade,progress.CompletedLaps>0))return;
            LastGrade=grade;feedbackUntil=Time.time+1.15f;
            NextGate=Mathf.Max(NextGate,index+1);
        }
        void OnCheckpoint(int checkpoint)
        {
            if(!race.IsQualifying || (race.State!=RaceState.Racing && race.State!=RaceState.Finished))return;
            if(checkpoint==0 && progress.CompletedLaps>0)
            {
                while(NextGate<gates.Length)Judge(NextGate,LineGateGrade.Miss);
                if(progress.CompletedLaps==1)ResetSession();
                else
                {
                    if(Score.PerfectLap)CompletedPerfectLaps++;
                    if(race.State!=RaceState.Finished){Score.BeginLap();NextGate=0;sampled=false;LastGrade=LineGateGrade.Pending;}
                }
                return;
            }
            // If ordered race progress has passed a gate, it was missed. Equal checkpoint may
            // fire before the car's centre crosses the gate due to its front collider.
            while(NextGate<gates.Length && gates[NextGate].checkpoint<checkpoint)Judge(NextGate,LineGateGrade.Miss);
        }
        void FixedUpdate()
        {
            if(!race || !race.IsQualifying || race.State!=RaceState.Racing){sampled=false;return;}
            var car=race.player;var position=car.Body.position;
            if(sampled && NextGate<gates.Length)
            {
                var gate=gates[NextGate];var cp=gate.visual;
                bool ordered=progress.LastCheckpoint>=gate.checkpoint-1 && progress.LastCheckpoint<=gate.checkpoint;
                if(ordered && LineGateScoring.Crossing(previousPosition,position,cp.position,cp.forward,out var crossing))
                {
                    var offset=crossing-cp.position;
                    float heading=Vector3.Angle(car.transform.forward,cp.forward);
                    var grade=LineGateScoring.Grade(Vector3.Dot(offset,cp.right),Vector3.Dot(offset,cp.up),heading,Vector3.Dot(car.Body.linearVelocity,cp.forward)*3.6f);
                    Judge(NextGate,grade);
                }
            }
            previousPosition=position;sampled=true;
        }
        void Tint(Renderer[] renderers,Color color)
        {foreach(var r in renderers){block.SetColor(BaseColor,color);r.SetPropertyBlock(block);}}
        void LateUpdate()
        {
            if(!progress)return;
            if(previousState!=race.State)
            {
                previousState=race.State;
                if(race.State==RaceState.Menu || race.State==RaceState.Countdown)ResetSession();
            }
            bool active=race.IsQualifying && (race.State==RaceState.Countdown || race.State==RaceState.Racing || race.State==RaceState.Paused);
            if(hudCard)hudCard.SetActive(active);
            for(int i=0;i<gates.Length;i++)
            {
                var gate=gates[i];float distance=Vector3.Distance(race.player.transform.position,gate.visual.position);
                bool show=active && i>=NextGate && i<NextGate+4 && distance<310;
                if(gate.visual.gameObject.activeSelf!=show)gate.visual.gameObject.SetActive(show);
                if(!show)continue;
                float alpha=Mathf.Clamp01((310-distance)/80);
                var panel=Purple;panel.a=.10f*alpha;Tint(gate.panels,panel);
                var frame=Purple;frame.a=(i==NextGate?.82f:.42f)*alpha;Tint(gate.frames,frame);
            }
            if(active)
            {
                var color=LastGrade==LineGateGrade.Perfect?Purple:LastGrade==LineGateGrade.Good?Green:Red;
                bool recent=Time.time<feedbackUntil;
                feedback.text=recent?LastGrade.ToString().ToUpperInvariant():"PURPLE LINE GATES";
                feedback.color=recent?color:Purple;
                summary.text=$"{(progress.CompletedLaps==0?"PRACTICE":"TIMED LAP")}  {Score.Judged:00}/{gates.Length:00}    PERFECT {Score.Perfect:00}    GOOD {Score.Good:00}    MISS {Score.Miss:00}";
            }
            if(resultSummary)
            {
                resultSummary.gameObject.SetActive(race.IsQualifying && race.State==RaceState.Finished);
                int target=gates.Length*3;
                resultSummary.text=$"<color=#BE72FF>{(Score.SessionPerfect==target?"PERFECT SESSION":"LINE ACCURACY")}  {Score.Points} / {target*100}</color>\nPERFECT {Score.SessionPerfect}    GOOD {Score.SessionGood}    MISS {Score.SessionMiss}    /    PERFECT LAPS {CompletedPerfectLaps}/3";
            }
        }
    }
}
