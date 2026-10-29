using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Overdrive;
using Object=UnityEngine.Object;

public static class OverdriveTrackFinishSetup
{
    const string Folder="Assets/Art/TrackFinish",RootName="Track Finishing Details";
    static readonly Dictionary<(int,Material),List<CombineInstance>> batches=new Dictionary<(int,Material),List<CombineInstance>>();
    static Mesh cube;static WaypointPath path;static int pieces;
    static void EnsureFolder(string p)
    {if(AssetDatabase.IsValidFolder(p))return;EnsureFolder(Path.GetDirectoryName(p).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(p).Replace('\\','/'),Path.GetFileName(p));}
    static Material Material(string name,Color color,float metal=0,float smooth=.2f,bool emission=false)
    {
        string p=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;
        if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.8f);}EditorUtility.SetDirty(m);return m;
    }
    static Texture2D Texture(string name,bool asphalt)
    {
        string p=Folder+"/"+name+".png";
        if(!File.Exists(p))
        {
            const int size=256;var texture=new Texture2D(size,size,TextureFormat.RGB24,true);
            var colors=new Color[size*size];var random=new System.Random(734);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float grain=(float)random.NextDouble(),patch=Mathf.PerlinNoise(x*.045f,y*.045f);
                float v=asphalt?.28f+grain*.09f+patch*.025f:.82f+grain*.15f;
                if(!asphalt && (grain>.98f || Mathf.PerlinNoise(x*.13f,y*.035f)>.72f))v=.57f;
                colors[y*size+x]=asphalt?new Color(v*.97f,v,v*1.025f):new Color(v,v,v);
            }
            texture.SetPixels(colors);texture.Apply();File.WriteAllBytes(p,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(p);importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.anisoLevel=8;importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }
    static void Box(int sector,Material m,Vector3 p,Vector3 size,Quaternion rotation)
    {
        var id=(sector,m);if(!batches.TryGetValue(id,out var list)){list=new List<CombineInstance>();batches[id]=list;}
        list.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(p,rotation,size)});pieces++;
    }
    static void Beam(int sector,Material m,Vector3 a,Vector3 b,float thickness)
    {Box(sector,m,(a+b)*.5f,new Vector3(thickness,(b-a).magnitude,thickness),Quaternion.FromToRotation(Vector3.up,b-a));}
    static float Clearance(Vector3 position)
    {
        float best=float.PositiveInfinity;position.y=0;
        for(int i=0;i<path.points.Length;i++)
        {var a=path.Position(i);var b=path.Position(i+1);a.y=b.y=0;var d=b-a;var q=a+d*Mathf.Clamp01(Vector3.Dot(position-a,d)/Mathf.Max(.001f,d.sqrMagnitude));best=Mathf.Min(best,(q-position).sqrMagnitude);}
        return Mathf.Sqrt(best);
    }
    static bool SurfacePoint(Vector3 p,out Vector3 found)
    {
        foreach(var hit in Physics.RaycastAll(p+Vector3.up*7,Vector3.down,15,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
        {
            if(hit.collider.attachedRigidbody)continue;
            var surface=hit.collider.GetComponentInParent<TrackSurface>();
            if(surface && hit.normal.y>.9f){found=hit.point+hit.normal*.023f;return true;}
        }
        found=p;return false;
    }
    static string PhysicsSignature(RaceManager race)
    {
        var data=new System.Text.StringBuilder();
        foreach(var p in race.ai.path.points)data.Append(p.position.ToString("F4"));
        foreach(var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.InstanceID))
        {data.Append(c.GetInstanceID()).Append(c.transform.position.ToString("F4"));if(c is MeshCollider mesh)data.Append(mesh.sharedMesh?mesh.sharedMesh.GetInstanceID():0);}
        data.Append(JsonUtility.ToJson(Object.FindFirstObjectByType<DrivingGuide>()));
        return Hash128.Compute(data.ToString()).ToString();
    }
    static void SurfaceMaterials(string key,Texture2D asphalt,Texture2D wear)
    {
        foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            bool road=renderer.name=="Asphalt Surface";
            bool kerb=renderer.name.IndexOf("Kerb",StringComparison.OrdinalIgnoreCase)>=0 && renderer.GetComponent<Collider>();
            if(!road && !kerb)continue;
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                string file=Folder+"/"+key+" "+(road?"Asphalt":renderer.name.Replace('/','-'))+" "+i+".mat";
                var m=AssetDatabase.LoadAssetAtPath<Material>(file);
                if(!m){m=new Material(materials[i]);AssetDatabase.CreateAsset(m,file);}
                m.SetTexture("_BaseMap",road?asphalt:wear);m.SetTextureScale("_BaseMap",road?new Vector2(3,3):new Vector2(1,2));
                if(road)m.SetColor("_BaseColor",Color.white);
                m.SetFloat("_Smoothness",road?.17f:.23f);m.enableInstancing=true;EditorUtility.SetDirty(m);materials[i]=m;
            }
            Undo.RecordObject(renderer,"Detailed road material");renderer.sharedMaterials=materials;EditorUtility.SetDirty(renderer);
        }
    }
    static AudioClip Sound(string name,float seconds,Func<float,float,float> sample)=>OverdriveImmersionSetup.Clip(name,seconds,sample);
    static void SoundDetails(RaceManager race,Transform root)
    {
        var engine=Sound("Detailed Engine",2,(t,n)=>
            (.37f*Mathf.Sin(2*Mathf.PI*250*t)+.20f*Mathf.Sin(2*Mathf.PI*500*t+.35f)+.12f*Mathf.Sin(2*Mathf.PI*750*t)+.065f*Mathf.Sin(2*Mathf.PI*1250*t))*(.94f+.06f*Mathf.Sin(2*Mathf.PI*31*t))+n*.11f);
        var intake=Sound("Intake load",2,(t,n)=>n*.7f+Mathf.Sin(2*Mathf.PI*375*t)*.13f+Mathf.Sin(2*Mathf.PI*1000*t)*.09f);
        var transmission=Sound("Transmission",2,(t,n)=>Mathf.Sin(2*Mathf.PI*850*t)*.30f+Mathf.Sin(2*Mathf.PI*1700*t)*.06f);
        var kerb=Sound("Kerb rumble",2,(t,n)=>(Mathf.Pow(.5f+.5f*Mathf.Sin(2*Mathf.PI*22*t),6)-.23f)*.9f+n*.28f);
        var gravel=Sound("Runoff grit",3,(t,n)=>n*(.5f+.5f*Mathf.Abs(Mathf.Sin(2*Mathf.PI*17*t)))+Mathf.Sin(2*Mathf.PI*65*t)*.07f);
        var shift=Sound("Gear engagement",.10f,(t,n)=>(n*.75f+Mathf.Sin(2*Mathf.PI*160*t)*.4f)*Mathf.Exp(-t*45));
        var lift=Sound("Throttle lift",.23f,(t,n)=>n*Mathf.Exp(-t*14)*.85f);
        var crowd=Sound("Distant crowd",8,(t,n)=>n*(.35f+.12f*Mathf.Sin(2*Mathf.PI*.375f*t))+.07f*Mathf.Sin(2*Mathf.PI*180*t+Mathf.Sin(t*2*Mathf.PI*.5f)*18)*Mathf.Pow(.5f+.5f*Mathf.Sin(t*2*Mathf.PI*.25f),3));
        var environment=Sound(race.circuit.harbor || race.circuit.yasMarina?"Coastal breeze":"Park breeze",8,(t,n)=>n*(.28f+.13f*Mathf.Sin(t*2*Mathf.PI*.125f)));
        foreach(var audio in Object.FindObjectsByType<VehicleRaceAudio>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            Undo.RecordObject(audio,"Layered vehicle sounds");audio.engineLoop=engine;audio.intakeLoop=intake;audio.transmissionLoop=transmission;audio.kerbLoop=kerb;audio.gravelLoop=gravel;audio.shiftClip=shift;audio.liftClip=lift;EditorUtility.SetDirty(audio);
        }
        var ambience=root.gameObject.AddComponent<CircuitAmbience>();ambience.race=race;ambience.crowdClip=crowd;ambience.environmentClip=environment;
        var scenery=Object.FindFirstObjectByType<CircuitSceneryMotion>();
        ambience.grandstands=scenery.zones.Where(z=>z.root && z.root.name.StartsWith("Fan village")).Select(z=>z.centre).ToArray();
        ambience.environmentVolume=race.circuit.harbor?.045f:race.circuit.yasMarina?.032f:.022f;
    }
    [MenuItem("Overdrive/Add Track Surface And Sound Detail")]
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play mode first");
        var race=Object.FindFirstObjectByType<RaceManager>();var scenery=Object.FindFirstObjectByType<CircuitSceneryMotion>();
        if(!race || !scenery)throw new InvalidOperationException("Open a detailed racing circuit");
        string signature=PhysicsSignature(race);string key=race.circuit.yasMarina?"YasMarina":race.circuit.harbor?"HarborRun":"ApexPark";
        EnsureFolder(Folder);EnsureFolder(Folder+"/"+key);
        var previous=scenery.transform.Find(RootName);if(previous)Object.DestroyImmediate(previous.gameObject);
        var root=new GameObject(RootName).transform;root.SetParent(scenery.transform,false);path=race.ai.path;
        var zones=scenery.zones.Where(z=>z.root).ToList();batches.Clear();pieces=0;
        var temp=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=temp.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(temp);
        var steel=Material("Galvanized metal",new Color(.40f,.44f,.47f),.7f,.28f);
        var dark=Material("Drainage and rubber",new Color(.028f,.032f,.037f),.1f,.15f);
        var reflect=Material(key+" Reflectors",race.circuit.yasMarina?new Color(.07f,.78f,.86f):race.circuit.harbor?new Color(1,.55f,.19f):new Color(.92f,.84f,.57f),.15f,.4f,true);
        var white=Material("Floodlight lenses",new Color(.86f,.93f,1),.2f,.5f,true);
        SurfaceMaterials(key,Texture("Asphalt aggregate",true),Texture("Kerb weathering",false));Physics.SyncTransforms();
        float roadHalf=race.circuit.yasMarina?7.5f:9.43f,wall=race.circuit.yasMarina?24.6f:26.6f;
        int fenceSpans=0,drains=0,marbles=0,masts=0;
        var random=new System.Random(912);int n=path.points.Length;
        for(int i=0;i<n;i++)
        {
            int sector=i*12/n;var point=path.Position(i);var next=path.Position(i+1);
            var forward=(next-point).normalized;var right=Vector3.Cross(Vector3.up,forward).normalized;var rotation=Quaternion.LookRotation(forward);
            float curvature=path.CurvatureAhead(i+1,1);
            foreach(int side in new[]{-1,1})
            {
                if(i%10==0 && SurfacePoint(point+right*side*(roadHalf+1.7f),out var drain) && Clearance(drain)>roadHalf+.5f)
                {
                    Box(sector,dark,drain,new Vector3(.55f,.025f,1.8f),rotation);
                    for(int bar=0;bar<10;bar++)Box(sector,steel,drain+Vector3.up*.016f+forward*(-.8f+bar*.175f),new Vector3(.53f,.014f,.045f),rotation);drains++;
                }
                var a=point+right*side*wall;var b=next+right*side*wall;
                if(curvature<.002f && Clearance(a)>18 && Clearance(b)>18)
                {
                    a.y=b.y=1;
                    Beam(sector,steel,a,a+Vector3.up*3.6f,.11f);
                    for(int wire=0;wire<5;wire++)Beam(sector,steel,a+Vector3.up*(1.2f+wire*.58f),b+Vector3.up*(1.2f+wire*.58f),wire==4?.045f:.018f);
                    Box(sector,reflect,a+Vector3.up*.8f-right*side*.08f,new Vector3(.055f,.26f,.52f),rotation);fenceSpans++;
                    if(i%24==0)
                    {
                        Beam(sector,steel,a,a+Vector3.up*12,.20f);Beam(sector,steel,a+Vector3.up*12-right*2,a+Vector3.up*12+right*2,.13f);
                        for(int lamp=0;lamp<4;lamp++)Box(sector,white,a+Vector3.up*11.85f+right*(-1.5f+lamp),new Vector3(.65f,.16f,.82f),rotation);masts++;
                    }
                }
                if(curvature>.0025f)
                for(int k=0;k<7;k++)
                {
                    var p=Vector3.Lerp(point,next,(float)random.NextDouble())+right*side*(roadHalf-.8f+(float)random.NextDouble()*.5f);
                    if(!SurfacePoint(p,out var mark))continue;
                    float size=.025f+(float)random.NextDouble()*.045f;
                    Box(sector,dark,mark,new Vector3(size,.009f,size*1.7f),Quaternion.Euler(0,(float)random.NextDouble()*180,0));marbles++;
                }
            }
        }
        int renderers=0,vertices=0;
        for(int sector=0;sector<12;sector++)
        {
            var group=new GameObject("Finish sector "+sector).transform;group.SetParent(root,false);
            var bounds=new Bounds(path.Position(sector*n/12),Vector3.zero);
            foreach(var pair in batches.Where(p=>p.Key.Item1==sector))
            {
                var mesh=new Mesh{name=key+" Finish "+sector+" "+pair.Key.Item2.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);mesh.RecalculateBounds();
                string p=Folder+"/"+key+"/"+mesh.name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(p);
                if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,p);
                var go=new GameObject(pair.Key.Item2.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(group,false);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key.Item2;
                // Already combined per sector; avoid Unity duplicating these meshes in a second static batch.
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;go.isStatic=false;
                bounds.Encapsulate(mesh.bounds);vertices+=mesh.vertexCount;renderers++;
            }
            zones.Add(new CircuitSceneryMotion.Zone{root=group.gameObject,centre=bounds.center,radius=bounds.extents.magnitude,viewDistance=480});
        }
        scenery.zones=zones.ToArray();EditorUtility.SetDirty(scenery);SoundDetails(race,root);
        if(signature!=PhysicsSignature(race))throw new Exception("Physics or corner guide changed unexpectedly");
        EditorSceneManager.MarkSceneDirty(race.gameObject.scene);EditorSceneManager.SaveScene(race.gameObject.scene);AssetDatabase.SaveAssets();
        string result=$"{key}: {fenceSpans} fence spans, {drains} drainage grates, {marbles} rubber marbles, {masts} light masts; {pieces} pieces in {renderers} renderers / {vertices} vertices; physics and corner guide unchanged";
        File.WriteAllText("Docs/"+key+"_TrackFinishBuild.txt",result);return result;
    }
}
