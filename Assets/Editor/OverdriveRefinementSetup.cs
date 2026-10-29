using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using Overdrive;

public static class OverdriveRefinementSetup
{
    const string MeshFolder="Assets/Meshes/OverdriveRefined/";
    const string MatFolder="Assets/Materials/Refined/";
    static List<Vector3> points;
    static Vector3[] rights,ups;
    static float[] distances,widths;
    static Transform root;
    static Material asphalt,white,red,green,gravel,metal,glass,concrete,carbon;
    static void Folder(string path)
    { if(!AssetDatabase.IsValidFolder(path)) { string parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/'); Folder(parent); AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path)); } }
    static Material Mat(string name,Color color,float metallic=0,float smoothness=.2f)
    {
        var material=new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial); material.name=name;
        material.color=color; material.SetFloat("_Metallic",metallic); material.SetFloat("_Smoothness",smoothness);
        AssetDatabase.CreateAsset(material,MatFolder+name+".mat"); return material;
    }
    static float Noise(int x,int y)
    { unchecked { uint n=(uint)(x*374761393+y*668265263+17); n=(n^(n>>13))*1274126177; return (n^(n>>16))%65536/65535f; } }
    static Texture2D Texture(string name,Color baseColor,bool fence=false)
    {
        const int size=512; var texture=new Texture2D(size,size,TextureFormat.RGBA32,false); var colors=new Color[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            float noise=Noise(x,y), coarse=Noise(x/5,y/5);
            float tint=.72f+noise*.4f+coarse*.16f;
            if(name=="Asphalt") { float line=Mathf.Exp(-Mathf.Pow((x-210)/80f,2))*.17f; tint-=line*(.6f+coarse*.4f); }
            var c=baseColor*tint; c.a=fence?((x%64<3 || y%64<3)?1:0):1; colors[y*size+x]=c;
        }
        texture.SetPixels(colors); texture.Apply(); string file=MatFolder+name+".png";
        System.IO.File.WriteAllBytes(file,texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(file);
        var importer=(TextureImporter)AssetImporter.GetAtPath(file); importer.wrapMode=TextureWrapMode.Repeat; importer.anisoLevel=8; importer.mipmapEnabled=true; importer.alphaIsTransparency=fence; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(file);
    }
    static List<Vector3> Resample(List<Vector3> input,float spacing)
    {
        var cumulative=new float[input.Count+1]; for(int i=0;i<input.Count;i++) cumulative[i+1]=cumulative[i]+Vector3.Distance(input[i],input[(i+1)%input.Count]);
        int count=Mathf.RoundToInt(cumulative[input.Count]/spacing); var result=new List<Vector3>(count); int segment=0;
        for(int i=0;i<count;i++)
        { float d=i*cumulative[input.Count]/count; while(segment<input.Count-1 && cumulative[segment+1]<d) segment++; float t=(d-cumulative[segment])/(cumulative[segment+1]-cumulative[segment]); result.Add(Vector3.Lerp(input[segment],input[(segment+1)%input.Count],t)); }
        return result;
    }
    static Vector3 Edge(int i,float offset,float height)
    { return points[i]+rights[i]*offset+ups[i]*height; }
    static GameObject Strip(string name,float inner,float outer,float height,Material a,Material b,bool variable=false,bool solid=false,string surface="Asphalt",float grip=1,float resistance=.012f,bool fence=false,float wallHeight=2.3f)
    {
        int n=points.Count; var v=new Vector3[(n+1)*2]; var uv=new Vector2[(n+1)*2]; var first=new List<int>(); var second=new List<int>();
        for(int i=0;i<=n;i++)
        {
            int index=i%n; float distance=i<n?distances[index]:distances[n-1]+Vector3.Distance(points[n-1],points[0]);
            float x=variable && Mathf.Abs(inner)>=12.5f?Mathf.Sign(inner)*(widths[index]+Mathf.Abs(inner)-15):inner;
            float y=variable && Mathf.Abs(outer)>=12.5f?Mathf.Sign(outer)*(widths[index]+Mathf.Abs(outer)-15):outer;
            if(fence) { v[i*2]=Edge(index,x,height); v[i*2+1]=Edge(index,x,height+wallHeight); uv[i*2]=new Vector2(distance/2,0); uv[i*2+1]=new Vector2(distance/2,wallHeight/2); }
            else { v[i*2]=Edge(index,x,height); v[i*2+1]=Edge(index,y,height+(surface=="Kerb"?.04f:0)); uv[i*2]=new Vector2(0,distance/16); uv[i*2+1]=new Vector2(1,distance/16); }
            if(i==n) continue;
            int next=i+1; var list=(int)(distance/3)%2==0?first:second;
            if(!fence || inner>0) list.AddRange(new[]{i*2,next*2,i*2+1,i*2+1,next*2,next*2+1});
            else list.AddRange(new[]{i*2,i*2+1,next*2,i*2+1,next*2+1,next*2});
        }
        var mesh=new Mesh {name=name}; mesh.vertices=v; mesh.uv=uv; mesh.subMeshCount=2; mesh.SetTriangles(first,0); mesh.SetTriangles(second,1); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,MeshFolder+name+".asset");
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.transform.SetParent(root,false);
        go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<Renderer>().sharedMaterials=new[]{a,b};
        if(solid)
        {
            var collision=mesh;
            if(fence)
            {
                collision=Object.Instantiate(mesh); collision.name=name+" Collision"; var all=new List<int>(mesh.triangles); var original=mesh.triangles;
                for(int j=0;j<original.Length;j+=3) all.AddRange(new[]{original[j+2],original[j+1],original[j]});
                collision.subMeshCount=1; collision.SetTriangles(all,0); AssetDatabase.CreateAsset(collision,MeshFolder+name+"_Collision.asset");
            }
            go.AddComponent<MeshCollider>().sharedMesh=collision; var s=go.AddComponent<TrackSurface>(); s.surfaceName=surface; s.grip=grip; s.rollingResistance=resistance;
        }
        if(name.Contains("Fence")) go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        return go;
    }
    static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material,bool collider=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=material;
        if(!collider) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    static void Beam(string name,Transform parent,Vector3 a,Vector3 b,float radius,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=(a+b)*.5f;
        go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized); go.transform.localScale=new Vector3(radius*2,Vector3.Distance(a,b)*.5f,radius*2);
        go.GetComponent<Renderer>().sharedMaterial=material; Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static void Float(Object target,string field,float value)
    { var so=new SerializedObject(target); so.FindProperty(field).floatValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
    static void SculptBody(OverdriveVehicle car)
    {
        var oldBody=car.transform.Find("Monocoque");
        var paint=new Material(oldBody.GetComponent<Renderer>().sharedMaterial); paint.SetFloat("_Metallic",.18f); paint.SetFloat("_Smoothness",.58f);
        string id=car.GetComponent<AIDriver>()?"AI":"Player"; AssetDatabase.CreateAsset(paint,MatFolder+id+" Livery.mat");
        Object.DestroyImmediate(oldBody.gameObject); Object.DestroyImmediate(car.transform.Find("Nose").gameObject);
        float[] z={-2.05f,-1.3f,-.6f,.3f,.9f,1.7f,2.4f}, w={.25f,.65f,.62f,.42f,.30f,.17f,.10f}, h={.32f,.58f,.48f,.37f,.30f,.23f,.13f};
        var vertices=new List<Vector3>(); var triangles=new List<int>();
        for(int ring=0;ring<z.Length;ring++)
        {
            vertices.AddRange(new[]{new Vector3(-w[ring],-.12f,z[ring]),new Vector3(-w[ring],h[ring]*.6f,z[ring]),new Vector3(-w[ring]*.65f,h[ring],z[ring]),new Vector3(w[ring]*.65f,h[ring],z[ring]),new Vector3(w[ring],h[ring]*.6f,z[ring]),new Vector3(w[ring],-.12f,z[ring])});
            if(ring==z.Length-1) continue;
            for(int face=0;face<6;face++) { int a=ring*6+face,b=ring*6+(face+1)%6; triangles.AddRange(new[]{a,a+6,b,b,a+6,b+6}); }
        }
        for(int j=1;j<5;j++) { triangles.AddRange(new[]{0,j,j+1}); int end=(z.Length-1)*6; triangles.AddRange(new[]{end,end+j+1,end+j}); }
        var shell=new Mesh {name=id+" Sculpted Body"}; shell.SetVertices(vertices); shell.SetTriangles(triangles,0); shell.RecalculateNormals(); shell.RecalculateBounds(); AssetDatabase.CreateAsset(shell,MeshFolder+id+" Body.asset");
        var skin=new GameObject("Sculpted Monocoque",typeof(MeshFilter),typeof(MeshRenderer)); skin.transform.SetParent(car.transform,false); skin.GetComponent<MeshFilter>().sharedMesh=shell; skin.GetComponent<Renderer>().sharedMaterial=paint;
        var helmet=car.transform.Find("Helmet"); if(helmet) Object.DestroyImmediate(helmet.gameObject);
        var head=GameObject.CreatePrimitive(PrimitiveType.Sphere); head.name="Helmet"; head.transform.SetParent(car.transform,false); head.transform.localPosition=new Vector3(0,.64f,-.55f); head.transform.localScale=new Vector3(.36f,.34f,.4f); head.GetComponent<Renderer>().sharedMaterial=white; Object.DestroyImmediate(head.GetComponent<Collider>());
        Cube("Helmet Visor",car.transform,new Vector3(0,.66f,-.35f),new Vector3(.28f,.10f,.035f),glass);
    }
    static void Vehicle(OverdriveVehicle car)
    {
        SculptBody(car);
        var so=new SerializedObject(car); so.FindProperty("advancedPhysics").boolValue=true; so.FindProperty("usePowertrain").boolValue=true;
        so.FindProperty("centerOfMass").vector3Value=new Vector3(0,-.12f,0); so.ApplyModifiedPropertiesWithoutUndo();
        Float(car,"brakeTorque",4200); Float(car,"engineTorque",620); Float(car,"antiRollStiffness",6500);
        var rb=car.GetComponent<Rigidbody>(); rb.useGravity=true; rb.solverIterations=12; rb.solverVelocityIterations=6; rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        foreach(var wheel in car.wheels) { wheel.mass=12; wheel.forceAppPointDistance=.12f; }
        Cube("Halo Front Support",car.transform,new Vector3(0,.56f,.12f),new Vector3(.065f,.57f,.075f),carbon);
        Beam("Halo Front Hoop",car.transform,new Vector3(-.44f,.84f,.15f),new Vector3(.44f,.84f,.15f),.035f,carbon);
        foreach(int side in new[]{-1,1})
        {
            Beam("Halo Upper Rail",car.transform,new Vector3(side*.44f,.84f,.15f),new Vector3(side*.38f,.77f,-1.05f),.035f,carbon);
            Cube("Front Wing Element",car.transform,new Vector3(side*.7f,-.08f,2.15f),new Vector3(1,.06f,.18f),carbon);
            for(int axle=0;axle<2;axle++)
            {
                float z=axle==0?1.65f:-1.65f;
                Beam("Wishbone",car.transform,new Vector3(side*.45f,.03f,z-.45f),new Vector3(side*1.05f,-.05f,z),.018f,carbon);
                Beam("Wishbone",car.transform,new Vector3(side*.45f,.03f,z+.4f),new Vector3(side*1.05f,-.05f,z),.018f,carbon);
            }
        }
        for(int i=0;i<4;i++)
        {
            var rim=GameObject.CreatePrimitive(PrimitiveType.Cylinder); rim.name="Wheel Rim"; rim.transform.SetParent(car.visuals[i],false);
            rim.transform.localPosition=new Vector3(i%2==0?-.2f:.2f,0,0); rim.transform.localRotation=Quaternion.Euler(0,0,90); rim.transform.localScale=new Vector3(.43f,.012f,.43f); rim.GetComponent<Renderer>().sharedMaterial=metal; Object.DestroyImmediate(rim.GetComponent<Collider>());
        }
    }
    public static string FinishSculpture()
    {
        if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
        var race=Object.FindFirstObjectByType<RaceManager>();
        white=AssetDatabase.LoadAssetAtPath<Material>(MatFolder+"Paint.mat"); glass=AssetDatabase.LoadAssetAtPath<Material>(MatFolder+"Glass.mat");
        foreach(var car in new[]{race.player,race.opponent}) if(car.transform.Find("Monocoque")) SculptBody(car);
        var scene=EditorSceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); return "Sculpted vehicle bodies saved; existing track/details preserved.";
    }
    static void Lighting()
    {
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(!pipeline) throw new System.InvalidOperationException("URP required.");
        pipeline.supportsHDR=true; pipeline.shadowDistance=180; EditorUtility.SetDirty(pipeline);
        var data=Camera.main.GetUniversalAdditionalCameraData(); data.renderPostProcessing=true; data.volumeLayerMask=1; data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,"Assets/Settings/RefinedVolume.asset");
        var tone=profile.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
        var color=profile.Add<ColorAdjustments>(); color.contrast.Override(8); color.saturation.Override(-6); color.postExposure.Override(.35f);
        var bloom=profile.Add<Bloom>(); bloom.intensity.Override(.08f); bloom.threshold.Override(1.1f);
        var vignette=profile.Add<Vignette>(); vignette.intensity.Override(.10f);
        foreach(var component in profile.components) AssetDatabase.AddObjectToAsset(component,profile);
        var volume=new GameObject("Refined Lighting").AddComponent<Volume>(); volume.isGlobal=true; volume.priority=10; volume.sharedProfile=profile;
        var sun=Object.FindFirstObjectByType<Light>(); sun.transform.rotation=Quaternion.Euler(38,-28,0); sun.intensity=2.2f; sun.shadows=LightShadows.Soft;
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared; RenderSettings.fogDensity=.0008f; RenderSettings.fogColor=new Color(.64f,.72f,.79f);
    }
    [MenuItem("Overdrive/Refine Track And Physics")]
    public static string Build()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.isDirty || scene.path!="Assets/Scenes/OverdriveF1.unity") throw new System.InvalidOperationException("Open saved F1 scene and stop Play.");
        if(GameObject.Find("Refined Circuit")) return "Refinement already present; preserved.";
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/OverdriveF1_PreRefinement.unity",true);
        Folder(MeshFolder.TrimEnd('/')); Folder(MatFolder.TrimEnd('/'));
        var race=Object.FindFirstObjectByType<RaceManager>(); var source=new List<Vector3>(); foreach(var p in race.ai.path.points) source.Add(p.position);
        // Periodic low-pass smoothing preserves the long straight and broad hairpin while removing short curvature kinks.
        for(int pass=0;pass<4;pass++)
        { var next=new List<Vector3>(); for(int i=0;i<source.Count;i++) next.Add((source[(i+source.Count-2)%source.Count]+source[(i+source.Count-1)%source.Count]*4+source[i]*6+source[(i+1)%source.Count]*4+source[(i+2)%source.Count])/16); source=next; }
        points=Resample(source,2); int count=points.Count; distances=new float[count]; rights=new Vector3[count]; ups=new Vector3[count]; widths=new float[count];
        float total=0; for(int i=1;i<count;i++) distances[i]=distances[i-1]+Vector3.Distance(points[i-1],points[i]); total=distances[count-1]+Vector3.Distance(points[count-1],points[0]);
        for(int i=0;i<count;i++)
        { float phase=distances[i]/total; var p=points[i]; p.y=1.25f*(1-Mathf.Cos(phase*Mathf.PI*2))+.25f*Mathf.Pow(Mathf.Sin(phase*Mathf.PI*4),2); points[i]=p; }
        for(int i=0;i<count;i++)
        {
            var a=(points[i]-points[(i+count-1)%count]).normalized; var b=(points[(i+1)%count]-points[i]).normalized; var direction=(a+b).normalized;
            float bend=Mathf.Atan2(Vector3.Cross(a,b).y,Vector3.Dot(a,b))/2;
            float bank=Mathf.Clamp(bend*.8f,-.035f,.035f)*Mathf.Rad2Deg;
            ups[i]=Quaternion.AngleAxis(-bank,direction)*Vector3.up; rights[i]=Vector3.Cross(ups[i],direction).normalized;
            float nearest=1000;
            for(int j=0;j<count;j++) { int apart=Mathf.Abs(i-j); apart=Mathf.Min(apart,count-apart); if(apart<35) continue; nearest=Mathf.Min(nearest,Vector3.Distance(points[i],points[j])); }
            widths[i]=Mathf.Clamp(nearest*.44f,11.4f,15);
        }
        root=new GameObject("Refined Circuit").transform;
        asphalt=Mat("Asphalt",Color.white,0,.18f); asphalt.mainTexture=Texture("Asphalt",new Color(.25f,.26f,.27f));
        white=Mat("Paint",new Color(.86f,.87f,.82f),0,.25f); red=Mat("Kerb Red",new Color(.65f,.025f,.018f),0,.28f);
        green=Mat("Runoff",new Color(.11f,.32f,.25f),0,.15f); gravel=Mat("Gravel",Color.white,0,.05f); gravel.mainTexture=Texture("Gravel",new Color(.48f,.42f,.31f));
        metal=Mat("Metal",new Color(.36f,.38f,.40f),.8f,.65f); glass=Mat("Glass",new Color(.05f,.11f,.16f),.6f,.9f);
        concrete=Mat("Concrete",new Color(.44f,.44f,.41f),0,.12f); carbon=Mat("Carbon",new Color(.025f,.03f,.033f),.25f,.5f);
        Strip("Asphalt Surface",-8.2f,8.2f,0,asphalt,asphalt,false,true);
        Strip("Left Kerb",-9.1f,-8.2f,.025f,red,white,false,true,"Kerb",.88f,.02f);
        Strip("Right Kerb",8.2f,9.1f,.025f,red,white,false,true,"Kerb",.88f,.02f);
        Strip("Left Runoff",-13,-9.1f,-.008f,green,green,true,true,"Runoff",.72f,.03f);
        Strip("Right Runoff",9.1f,13,-.008f,green,green,true,true,"Runoff",.72f,.03f);
        Strip("Left Gravel",-14.7f,-13,-.01f,gravel,gravel,true,true,"Gravel",.45f,.09f);
        Strip("Right Gravel",13,14.7f,-.01f,gravel,gravel,true,true,"Gravel",.45f,.09f);
        Strip("Left White Line",-8.14f,-8.01f,.008f,white,white); Strip("Right White Line",8.01f,8.14f,.008f,white,white);
        var fence=Mat("Catch Fence",new Color(.24f,.26f,.27f),.7f,.3f); fence.mainTexture=Texture("Fence",Color.white,true); fence.SetFloat("_AlphaClip",1); fence.SetFloat("_Cutoff",.5f); fence.EnableKeyword("_ALPHATEST_ON"); fence.SetFloat("_Cull",0);
        Strip("Left Fence",-15,-15,1.05f,fence,fence,true,true,"Fence",1,.012f,true);
        Strip("Right Fence",15,15,1.05f,fence,fence,true,true,"Fence",1,.012f,true);
        Strip("Left Concrete",-15,-15,0,concrete,concrete,true,true,"Barrier",1,.012f,true,1.05f);
        Strip("Right Concrete",15,15,0,concrete,concrete,true,true,"Barrier",1,.012f,true,1.05f);
        for(int i=0;i<count;i+=6)
        {
            foreach(int side in new[]{-1,1})
            {
                Beam("Fence Post",root,Edge(i,side*widths[i],1),Edge(i,side*widths[i],3.4f),.045f,metal);
            }
        }
        var oldRoot=GameObject.Find("F1 Circuit - Apex Park").transform; var oldGates=race.ai.path.points;
        var gates=Resample(points,8); var checkpoints=new Transform[gates.Count];
        for(int i=0;i<gates.Count;i++)
        {
            var go=new GameObject("Checkpoint "+i); go.transform.SetParent(root,false); var forward=(gates[(i+1)%gates.Count]-gates[i]).normalized;
            go.transform.SetPositionAndRotation(gates[i],Quaternion.LookRotation(forward,Vector3.up));
            var trigger=go.AddComponent<BoxCollider>(); trigger.isTrigger=true; trigger.center=Vector3.up*2; trigger.size=new Vector3(19,4,1); go.AddComponent<CircuitCheckpoint>().index=i; checkpoints[i]=go.transform;
        }
        race.ai.path.points=checkpoints; race.player.GetComponent<CircuitProgress>().checkpoints=checkpoints; race.opponent.GetComponent<CircuitProgress>().checkpoints=checkpoints;
        foreach(var gate in oldGates) Object.DestroyImmediate(gate.gameObject);
        foreach(string name in new[]{"Smooth Tarmac","Left Kerb","Right Kerb","Left Wall","Right Wall"}) { var found=oldRoot.Find(name); if(found) Object.DestroyImmediate(found.gameObject); }
        var terrain=GameObject.Find("Terrain"); var ground=terrain.AddComponent<TrackSurface>(); ground.grip=.55f; ground.rollingResistance=.06f; ground.surfaceName="Grass";
        var grass=Mat("Grass",Color.white,0,.05f); grass.mainTexture=Texture("Grass",new Color(.15f,.23f,.11f)); terrain.GetComponent<Renderer>().sharedMaterial=grass; terrain.GetComponent<Renderer>().sharedMaterial.mainTextureScale=new Vector2(45,50);
        foreach(Transform child in oldRoot)
        {
            if(child.name=="Pit garage")
            {
                child.GetComponent<Renderer>().sharedMaterial=concrete;
                // Siblings use world coordinates so details are not distorted by scaled primitive buildings.
                var pos=child.position; Cube("Garage Shutter",root,new Vector3(pos.x+10.03f,1.9f,pos.z),new Vector3(.12f,3.4f,14),metal);
                Cube("Pit Window",root,new Vector3(pos.x+10.12f,4.7f,pos.z),new Vector3(.14f,1.2f,15),glass);
                for(int stripe=0;stripe<7;stripe++) Cube("Shutter Rib",root,new Vector3(pos.x+10.12f,.5f+stripe*.43f,pos.z),new Vector3(.05f,.04f,14),carbon);
            }
            if(child.name=="Grandstand")
            {
                child.GetComponent<Renderer>().sharedMaterial=concrete; var pos=child.position;
                for(int row=0;row<5;row++) Cube("Seating Tier",root,new Vector3(pos.x-5+row*2,2.2f+row*.42f,pos.z),new Vector3(1.6f,.22f,15),row%2==0?red:white);
                Cube("Stand Canopy",root,new Vector3(pos.x,6.5f,pos.z),new Vector3(13,.18f,17),metal);
            }
        }
        Vehicle(race.player); Vehicle(race.opponent);
        var start=checkpoints[0]; race.player.transform.SetPositionAndRotation(start.position+start.forward*3-start.right*2.3f+Vector3.up*.7f,start.rotation); race.opponent.transform.SetPositionAndRotation(start.position+start.forward*3+start.right*2.3f+Vector3.up*.7f,start.rotation);
        Lighting(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return $"Refined track: {points.Count} render samples (~2m), {gates.Count} race gates (~8m), mild elevation/camber, collision kerbs/surface grip, powertrain/aero/anti-roll, detailed facilities and ACES lighting.";
    }
}
