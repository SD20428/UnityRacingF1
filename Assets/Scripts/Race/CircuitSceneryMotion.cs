using UnityEngine;
using TMPro;

namespace Overdrive
{
    // Cosmetic observers only. Does not change cars, colliders, race state or lap timing.
    [DefaultExecutionOrder(500)]
    public sealed class CircuitSceneryMotion : MonoBehaviour
    {
        [System.Serializable] public sealed class Zone { public GameObject root;public Vector3 centre;public float radius,viewDistance=750; }
        [System.Serializable] public sealed class Placement { public string label;public Vector3 centre;public float radius; }
        public RaceManager race;
        public string theme;
        public float protectedHalfWidth;
        public Zone[] zones;
        public Placement[] placements;
        public Mesh flagSource;
        public MeshFilter[] flags;
        public Transform[] waveArms,swayObjects;
        public Renderer[] marshalLamps,facades;
        public TMP_Text[] timingBoards;
        public int AnimationTicks {get;private set;}
        public int VisibleZones {get;private set;}
        Mesh[] flagMeshes;Vector3[][] flagVertices;Vector3[] rest;
        Quaternion[] armRotations,swayRotations;
        MaterialPropertyBlock block;
        RaceState lastState=(RaceState)(-1);
        float nextAnimation,nextCulling,nextBoard;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),Emission=Shader.PropertyToID("_EmissionColor");
        void Start(){Initialize();}
        void Initialize()
        {
            if(!flagSource || flags==null)return;
            block=new MaterialPropertyBlock();rest=flagSource.vertices;
            flagMeshes=new Mesh[flags.Length];flagVertices=new Vector3[flags.Length][];
            for(int i=0;i<flags.Length;i++)
            {
                if(!flags[i])continue;
                var previous=flags[i].sharedMesh;
                if(previous && previous.name.StartsWith("Runtime detail flag"))Destroy(previous);
                flagMeshes[i]=Instantiate(flagSource);flagMeshes[i].name="Runtime detail flag "+i;flagMeshes[i].MarkDynamic();
                flags[i].sharedMesh=flagMeshes[i];flagVertices[i]=(Vector3[])rest.Clone();
            }
            armRotations=new Quaternion[waveArms.Length];for(int i=0;i<waveArms.Length;i++)if(waveArms[i])armRotations[i]=waveArms[i].localRotation;
            swayRotations=new Quaternion[swayObjects.Length];for(int i=0;i<swayObjects.Length;i++)if(swayObjects[i])swayRotations[i]=swayObjects[i].localRotation;
        }
        void LateUpdate()
        {
            if(!race)return;if(flagMeshes==null)Initialize();if(block==null)return;
            if(Time.unscaledTime>=nextCulling)
            {
                nextCulling=Time.unscaledTime+.25f;VisibleZones=0;var camera=Camera.main;
                if(camera && zones!=null)foreach(var zone in zones)
                {
                    if(!zone.root)continue;
                    bool visible=(zone.centre-camera.transform.position).sqrMagnitude<(zone.viewDistance+zone.radius)*(zone.viewDistance+zone.radius);
                    if(zone.root.activeSelf!=visible)zone.root.SetActive(visible);if(visible)VisibleZones++;
                }
            }
            if(lastState!=race.State)
            {
                lastState=race.State;Color color=race.State==RaceState.Countdown?new Color(1,.08f,.07f):race.State==RaceState.Racing?new Color(.1f,1,.55f):race.State==RaceState.Paused?new Color(1,.65f,.1f):new Color(.12f,.2f,.25f);
                foreach(var renderer in marshalLamps)if(renderer){block.Clear();block.SetColor(BaseColor,color);block.SetColor(Emission,color*1.5f);renderer.SetPropertyBlock(block);}
            }
            if(Time.timeScale<=0)return;
            if(Time.time>=nextAnimation)
            {
                nextAnimation=Time.time+.05f;AnimationTicks++;float t=Time.time;
                for(int i=0;i<flags.Length;i++)
                {
                    if(!flags[i] || !flags[i].gameObject.activeInHierarchy)continue;
                    var vertices=flagVertices[i];
                    for(int j=0;j<vertices.Length;j++){var p=rest[j];float u=Mathf.Clamp01(p.x/2.4f);p.z=(Mathf.Sin(t*2.7f-p.x*3+i*.83f)*.14f+Mathf.Sin(t*4.3f-p.x*5+p.y)*.045f)*u;p.y+=Mathf.Sin(t*2-p.x*2+i)*.035f*u;vertices[j]=p;}
                    flagMeshes[i].vertices=vertices;
                }
                for(int i=0;i<waveArms.Length;i++)if(waveArms[i] && waveArms[i].gameObject.activeInHierarchy)waveArms[i].localRotation=armRotations[i]*Quaternion.Euler(0,0,Mathf.Sin(t*2.4f+i*1.7f)*18);
                for(int i=0;i<swayObjects.Length;i++)if(swayObjects[i] && swayObjects[i].gameObject.activeInHierarchy)swayObjects[i].localRotation=swayRotations[i]*Quaternion.Euler(0,Mathf.Sin(t*.7f+i)*7,Mathf.Sin(t*1.1f+i)*3);
                if(facades!=null)foreach(var renderer in facades)if(renderer && renderer.gameObject.activeInHierarchy)
                {block.Clear();block.SetColor(Emission,Color.Lerp(new Color(.2f,.08f,.7f),new Color(.5f,.12f,1.15f),.5f+.5f*Mathf.Sin(t*.35f)));renderer.SetPropertyBlock(block);}
            }
            if(Time.time>=nextBoard)
            {
                nextBoard=Time.time+.25f;
                string detail=race.State==RaceState.Menu?"RACE WEEKEND":race.IsQualifying?$"QUALIFYING  /  BEST {RaceUI.Format(race.PlayerLaps.BestLap)}":$"LAP {Mathf.Min(2,race.PlayerProgress.CompletedLaps+1)}/2   /   {RaceUI.Format(race.Elapsed)}";
                foreach(var text in timingBoards)if(text && text.gameObject.activeInHierarchy)text.text=race.CircuitName+"\n"+detail;
            }
        }
        void OnDestroy(){if(flagMeshes!=null)foreach(var mesh in flagMeshes)if(mesh)Destroy(mesh);}
    }
}
