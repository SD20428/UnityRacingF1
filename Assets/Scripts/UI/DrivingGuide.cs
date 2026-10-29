using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using TMPro;
namespace Overdrive
{
    // Advisory centreline and braking envelope. Never writes to vehicle controls or forces.
    public sealed class DrivingGuide : MonoBehaviour
    {
        [System.Serializable] public sealed class CornerCalibration
        {
            public string name;
            public bool qualifyingOnly;
            [Min(0)] public int firstCheckpoint,lastCheckpoint;
            [Min(1)] public float baseLateralAcceleration=14;
        }
        public RaceManager race;
        public WaypointPath qualifyingLine;
        public WaypointPath ActivePath=>race && race.IsQualifying && qualifyingLine && qualifyingLine.Valid?qualifyingLine:race && race.ai?race.ai.path:null;
        public TMP_Text advice;
        public bool visible=true;
        public DrivingGuideTuning tuning=new DrivingGuideTuning();
        public CornerCalibration[] cornerCalibrations=new CornerCalibration[0];
        public const float CoastMarginKmh=8, BrakeToleranceKmh=3;
        public static bool FeatherThrottle(OverdriveVehicle car)=>car && car.SpeedKmh>80 && Mathf.Abs(car.Steering)>.04f;
        public float RecommendedKmh {get;private set;}
        public bool CornerGuidanceActive {get;private set;}
        public bool BrakeRecommended=>CornerGuidanceActive && race && race.player && race.player.SpeedKmh>RecommendedKmh+BrakeToleranceKmh;
        public int ArrowCount {get;private set;}
        Mesh mesh;MeshRenderer drawing;float[] speeds,lengths;float nextRefresh;
        DrivingSpeedProfile profile;
        CornerSpeedProfile cornerProfile;
        bool plannedQualifying;
        readonly List<Vector3> vertices=new List<Vector3>(400);
        readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>()};
        void Start()
        {
            RebuildProfile();
            mesh=new Mesh{name="Dynamic driving guide"};GetComponent<MeshFilter>().sharedMesh=mesh;drawing=GetComponent<MeshRenderer>();drawing.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;drawing.receiveShadows=false;
        }
        [ContextMenu("Rebuild advisory speed profile")]
        public void RebuildProfile()
        {
            if(!ActivePath || !ActivePath.Valid)return;
            var calibrated=VehicleTuning();
            profile=new DrivingSpeedProfile(ActivePath,calibrated);cornerProfile=new CornerSpeedProfile(ActivePath,calibrated);plannedQualifying=race.IsQualifying;
            speeds=profile.speeds;lengths=profile.lengths;
        }
        public DrivingGuideTuning VehicleTuning(bool? qualifying=null)
        {
            var settings=tuning??new DrivingGuideTuning();
            var car=race?race.player:null;var body=car?car.GetComponent<Rigidbody>():null;
            var calibrated=new DrivingGuideTuning {
                baseLateralAcceleration=settings.baseLateralAcceleration,aeroLateralCoefficient=settings.aeroLateralCoefficient,
                maximumLateralAcceleration=settings.maximumLateralAcceleration,brakingDeceleration=settings.brakingDeceleration,
                exitAcceleration=settings.exitAcceleration,longitudinalReserve=Mathf.Max(settings.brakingDeceleration,settings.exitAcceleration),
                maximumSpeedKmh=Mathf.Min(settings.maximumSpeedKmh,car?car.MaximumForwardKmh:settings.maximumSpeedKmh),
                reactionSeconds=settings.reactionSeconds,powerLimitKW=car?car.CombustionPowerKW:0,
                vehicleMassKg=body?body.mass:settings.vehicleMassKg,dragArea=car?car.AerodynamicDragArea:settings.dragArea
            };
            if(race && race.ai && race.ai.path && race.ai.path.Valid && cornerCalibrations!=null && cornerCalibrations.Length>0)
            {
                int count=race.ai.path.points.Length;calibrated.localBaseLateral=new float[count];
                for(int i=0;i<count;i++)calibrated.localBaseLateral[i]=settings.baseLateralAcceleration;
                foreach(var zone in cornerCalibrations)
                {
                    if(zone==null || zone.qualifyingOnly && !(qualifying??race.IsQualifying))continue;
                    for(int i=0;i<count;i++)
                    {
                        bool inside=zone.firstCheckpoint<=zone.lastCheckpoint?i>=zone.firstCheckpoint && i<=zone.lastCheckpoint:i>=zone.firstCheckpoint || i<=zone.lastCheckpoint;
                        if(inside)calibrated.localBaseLateral[i]=Mathf.Min(calibrated.localBaseLateral[i],Mathf.Max(1,zone.baseLateralAcceleration));
                    }
                }
            }
            return calibrated;
        }
        public int ColorBand(float speedKmh,float suggestedKmh)=>speedKmh>suggestedKmh+BrakeToleranceKmh?2:speedKmh>suggestedKmh-CoastMarginKmh?1:0;
        void Update()
        {
            if(Keyboard.current!=null && Keyboard.current.gKey.wasPressedThisFrame)visible=!visible;
            if(race && plannedQualifying!=race.IsQualifying)RebuildProfile();
            if(mesh==null || profile==null || !race || !race.player || !race.PlayerProgress)return;
            bool racing=race.State==RaceState.Countdown || race.State==RaceState.Racing || race.State==RaceState.Paused;
            drawing.enabled=visible && racing;if(!racing){CornerGuidanceActive=false;advice.text="";return;}
            if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.1f;
            var progress=race.PlayerProgress;var path=ActivePath;int n=speeds.Length;int segment=progress.LastCheckpoint;
            float fraction=path.SegmentFraction(race.player.transform.position,segment);
            float surface=Mathf.Clamp(race.player.SurfaceGrip,.25f,1);
            CornerGuidanceActive=visible && cornerProfile.NeedsGuidance(segment,fraction,race.player.SpeedKmh,surface,out _);
            RecommendedKmh=cornerProfile.TargetKmh(segment,fraction,race.player.SpeedKmh,surface);
            bool feather=FeatherThrottle(race.player);
            advice.text=!CornerGuidanceActive?"":$"{(BrakeRecommended?"<color=#FF5555>BRAKE</color>":race.player.SpeedKmh>RecommendedKmh-CoastMarginKmh?(feather?"<color=#FFD056>HOLD SPEED / BALANCE THROTTLE</color>":"<color=#FFD056>LIFT / COAST</color>"):feather?"<color=#FFD056>FEATHER THROTTLE</color>":"<color=#4FE09A>ACCELERATE</color>")}   CORNER {RecommendedKmh:000} KM/H   /   G GUIDE";
            vertices.Clear();foreach(var list in triangles)list.Clear();ArrowCount=0;
            float offset=fraction*lengths[segment]+5;
            for(int arrow=0;arrow<48;arrow++,offset+=5)
            {
                int s=segment;float d=offset;for(int step=0;step<n && d>lengths[s];step++){d-=lengths[s];s=(s+1)%n;}
                float t=d/Mathf.Max(.01f,lengths[s]);var position=Vector3.Lerp(path.Position(s),path.Position(s+1),t)+Vector3.up*.055f;
                var forward=(path.Position(s+1)-path.Position(s)).normalized;var right=Vector3.Cross(Vector3.up,forward).normalized;
                if(!visible || !cornerProfile.NeedsGuidance(s,t,race.player.SpeedKmh,surface,out _))continue;
                float suggested=cornerProfile.TargetKmh(s,t,race.player.SpeedKmh,arrow<6?surface:1);
                int band=ColorBand(race.player.SpeedKmh,suggested);
                if(feather && arrow<6 && band==0)band=1;
                int v=vertices.Count;
                vertices.Add(position-forward*1.5f-right*.38f);vertices.Add(position+forward*.25f-right*.38f);vertices.Add(position+forward*.25f-right*.8f);vertices.Add(position+forward*1.55f);vertices.Add(position+forward*.25f+right*.8f);vertices.Add(position+forward*.25f+right*.38f);vertices.Add(position-forward*1.5f+right*.38f);
                triangles[band].AddRange(new[]{v,v+1,v+6,v+1,v+5,v+6,v+2,v+3,v+4});ArrowCount++;
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.subMeshCount=3;for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
