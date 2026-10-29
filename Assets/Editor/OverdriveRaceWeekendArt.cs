using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Overdrive;
using Object=UnityEngine.Object;

// Visual atmosphere pass derived from the live circuit; no gameplay components or colliders.
public static class OverdriveRaceWeekendArt
{
    const string Folder="Assets/Art/GrandPrix/Weekend";
    static Transform[] route;
    static Transform root;
    static Material charcoal,stone,cream,red,teal,bark,leaf,leafLight;
    static readonly List<Bounds> occupied=new List<Bounds>();
    static Material Mat(string name,Color c,float rough=.3f)
    {
        var path=Folder+"/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m) {m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.color=c;m.SetFloat("_Smoothness",rough);EditorUtility.SetDirty(m);return m;
    }
    static Transform Group(string name,Transform parent,Vector3 p)
    {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;}
    static void Box(Transform parent,string name,Vector3 p,Vector3 size,Material m)
    {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());}
    static void Beam(Transform parent,Vector3 a,Vector3 b,float width,Material m)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name="Support";g.transform.SetParent(parent,false);g.transform.localPosition=(a+b)*.5f;
        g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);g.transform.localScale=new Vector3(width,(a-b).magnitude*.5f,width);g.GetComponent<Renderer>().sharedMaterial=m;Object.DestroyImmediate(g.GetComponent<Collider>());
    }
    static void Text(Transform parent,string value,Vector3 p,float size)
    {var t=Group(value,parent,p);var tm=t.gameObject.AddComponent<TextMesh>();tm.text=value;tm.fontSize=64;tm.characterSize=size;tm.anchor=TextAnchor.MiddleCenter;tm.color=Color.white;}
    static Mesh SaveMesh(string name,Mesh mesh)
    {
        mesh.name=name;var path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static void MeshObject(string name,Transform parent,Mesh mesh,Material material)
    {var t=Group(name,parent,Vector3.zero);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;t.gameObject.isStatic=true;}
    static float DistanceToRoute(Vector3 p)
    {
        p.y=0;float best=float.PositiveInfinity;
        for(int i=0;i<route.Length;i++){var a=route[i].position;var b=route[(i+1)%route.Length].position;a.y=b.y=0;var d=b-a;float u=Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(.001f,d.sqrMagnitude));best=Mathf.Min(best,Vector3.Distance(p,a+d*u));}return best;
    }
    static bool Clear(Vector3 p,float radius)
    {
        if(DistanceToRoute(p)<22+radius)return false;
        foreach(var b in occupied){var near=b.ClosestPoint(new Vector3(p.x,b.center.y,p.z));near.y=p.y;if(Vector3.Distance(near,p)<radius+3)return false;}return true;
    }
    static void Register(Transform t)
    {bool first=true;Bounds b=default;foreach(var r in t.GetComponentsInChildren<Renderer>()){if(first){b=r.bounds;first=false;}else b.Encapsulate(r.bounds);}if(!first)occupied.Add(b);}
    static void Batch(Transform t)
    {
        var groups=new Dictionary<Material,List<CombineInstance>>();var remove=new List<GameObject>();
        foreach(var f in t.GetComponentsInChildren<MeshFilter>()){var r=f.GetComponent<Renderer>();if(!r||!f.sharedMesh)continue;var m=r.sharedMaterial;if(!groups.ContainsKey(m))groups[m]=new List<CombineInstance>();groups[m].Add(new CombineInstance{mesh=f.sharedMesh,transform=t.worldToLocalMatrix*f.transform.localToWorldMatrix});remove.Add(f.gameObject);}
        foreach(var pair in groups){var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(pair.Value.ToArray(),true,true);m.RecalculateBounds();MeshObject("Combined "+pair.Key.name,t,SaveMesh(t.name+" "+pair.Key.name,m),pair.Key);}
        foreach(var go in remove)Object.DestroyImmediate(go);
    }
    static Texture2D SaveTexture(string name,Texture2D t)
    {
        var path=Folder+"/"+name+".png";System.IO.File.WriteAllBytes(path,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.wrapMode=TextureWrapMode.Repeat;imp.anisoLevel=8;imp.mipmapEnabled=true;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void Landscape()
    {
        const int n=256;var tex=new Texture2D(n,n,TextureFormat.RGB24,true);var pixels=new Color[n*n];
        var rng=new System.Random(730);
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)
        {
            float broad=Mathf.PerlinNoise(x/51f,y/51f),fine=(float)rng.NextDouble();float blade=Mathf.Pow((float)rng.NextDouble(),9);
            pixels[y*n+x]=Color.Lerp(new Color(.095f,.145f,.065f),new Color(.25f,.29f,.13f),broad*.8f+fine*.2f)+new Color(.07f,.06f,.025f)*blade;
        }
        tex.SetPixels(pixels);tex.Apply();var grass=Mat("Meadow grass",Color.white,.12f);grass.mainTexture=SaveTexture("Grass detail",tex);
        var terrain=GameObject.Find("Terrain").GetComponent<Renderer>();var bounds=terrain.bounds;grass.mainTextureScale=new Vector2(bounds.size.x/24,bounds.size.z/24);terrain.sharedMaterial=grass;EditorUtility.SetDirty(grass);
        var shader=Shader.Find("Skybox/Procedural");if(shader)
        {
            var path=Folder+"/Circuit sky.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(path);if(!sky){sky=new Material(shader);AssetDatabase.CreateAsset(sky,path);}
            sky.SetColor("_SkyTint",new Color(.52f,.59f,.68f));sky.SetColor("_GroundColor",new Color(.24f,.29f,.22f));sky.SetFloat("_AtmosphereThickness",.85f);sky.SetFloat("_SunSize",.025f);sky.SetFloat("_Exposure",1.1f);RenderSettings.skybox=sky;EditorUtility.SetDirty(sky);
        }
        var sun=GameObject.Find("Sun").GetComponent<Light>();sun.intensity=1.65f;sun.color=new Color(1,.97f,.91f);sun.shadows=LightShadows.Soft;
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.49f,.57f,.67f);RenderSettings.ambientEquatorColor=new Color(.36f,.41f,.44f);RenderSettings.ambientGroundColor=new Color(.19f,.22f,.16f);
        RenderSettings.fogColor=new Color(.65f,.72f,.76f);RenderSettings.fogDensity=.00027f;DynamicGI.UpdateEnvironment();
    }
    static void Rubber()
    {
        var road=GameObject.Find("Refined Circuit").transform.Find("Asphalt Surface");var mesh=road.GetComponent<MeshFilter>().sharedMesh;var input=mesh.vertices;const int stride=5;
        if(input.Length%stride!=0)throw new InvalidOperationException("Expected five vertices per road ring.");int count=input.Length/stride;
        var tex=new Texture2D(128,256,TextureFormat.RGBA32,true);var pixels=new Color[128*256];
        for(int y=0;y<256;y++)for(int x=0;x<128;x++)
        {float u=(x-63.5f)/63.5f,grain=Mathf.PerlinNoise(x*.32f,y*.075f);float alpha=Mathf.Exp(-u*u*4)*(.035f+grain*.11f);pixels[y*128+x]=new Color(.035f,.037f,.039f,alpha);}
        tex.SetPixels(pixels);tex.Apply();var m=Mat("Racing rubber",Color.white,.13f);m.mainTexture=SaveTexture("Rubber grain",tex);
        m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;EditorUtility.SetDirty(m);
        var centers=new Vector3[count];for(int i=0;i<count;i++)centers[i]=road.TransformPoint(input[i*5+2]);float distance=0;int chunks=0;
        for(int start=0;start<count-1;start+=512)
        {
            int end=Mathf.Min(count-1,start+512);var v=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
            for(int i=start;i<=end;i++)
            {
                if(i>0)distance+=Vector3.Distance(centers[i],centers[i-1]);
                var right=road.TransformVector(input[i*5+4]-input[i*5]).normalized;
                int before=Mathf.Max(0,i-60),after=Mathf.Min(count-1,i+60);var a=(centers[i]-centers[before]).normalized;var b=(centers[after]-centers[i]).normalized;
                float offset=Mathf.Clamp(-Vector3.Cross(a,b).y*7,-3,3);float width=1.35f;
                for(int side=-1;side<=1;side+=2)
                {
                    float roadWidth=Vector3.Distance(road.TransformPoint(input[i*5]),road.TransformPoint(input[i*5+4]));
                    float lateral=offset+side*width;float u=.5f+lateral/Mathf.Max(1,roadWidth);
                    // Sample the current road's cross-section so paint follows elevation/camber.
                    float at=Mathf.Clamp01(u)*4;int col=Mathf.Min(3,Mathf.FloorToInt(at));var p=Vector3.Lerp(input[i*5+col],input[i*5+col+1],at-col);
                    v.Add(road.TransformPoint(p)+Vector3.up*.009f);uv.Add(new Vector2(side==-1?0:1,distance/18));
                }
                if(i<end){int j=(i-start)*2;tri.AddRange(new[]{j,j+2,j+1,j+1,j+2,j+3});}
            }
            var result=new Mesh();result.SetVertices(v);result.SetUVs(0,uv);result.SetTriangles(tri,0);result.RecalculateNormals();result.RecalculateBounds();
            var group=Group("Rubber sector "+chunks,root,Vector3.zero);MeshObject("Rubber surface",group,SaveMesh("Rubber sector "+chunks,result),m);var r=group.GetComponentInChildren<Renderer>();r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;chunks++;
        }
    }
    static Mesh Crown()
    {
        var v=new List<Vector3>();var tri=new List<int>();const int rings=8,sides=10;
        for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
        {float angle=x*Mathf.PI*2/sides,lat=y*Mathf.PI/rings;float radius=Mathf.Sin(lat)*(1+.08f*Mathf.Sin(x*13+y*7));v.Add(new Vector3(Mathf.Cos(angle)*radius,Mathf.Cos(lat),Mathf.Sin(angle)*radius));if(y<rings&&x<sides){int j=y*(sides+1)+x;tri.AddRange(new[]{j,j+1,j+sides+1,j+1,j+sides+2,j+sides+1});}}
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(tri,0);m.RecalculateNormals();m.RecalculateBounds();return SaveMesh("Tree crown",m);
    }
    static int Trees()
    {
        var crown=Crown();var dummy=GameObject.CreatePrimitive(PrimitiveType.Cylinder);var trunk=dummy.GetComponent<MeshFilter>().sharedMesh;Object.DestroyImmediate(dummy);
        var rng=new System.Random(337);int total=0,section=0;var group=Group("Tree belt 0",root,Vector3.zero);int inGroup=0;
        for(int i=0;i<route.Length;i+=12)for(int side=-1;side<=1;side+=2)
        {
            var p=route[i];float offset=55+(float)rng.NextDouble()*70;var pos=p.position+p.right*side*offset+p.forward*((float)rng.NextDouble()-.5f)*24;pos.y=0;
            if(!Clear(pos,5))continue;
            float height=5+(float)rng.NextDouble()*4;
            var t=Group("Boundary tree",group,pos);
            var tr=Group("Trunk",t,new Vector3(0,height*.26f,0));tr.localScale=new Vector3(.3f,height*.26f,.3f);tr.gameObject.AddComponent<MeshFilter>().sharedMesh=trunk;tr.gameObject.AddComponent<MeshRenderer>().sharedMaterial=bark;
            for(int k=0;k<3;k++)
            {var c=Group("Foliage",t,new Vector3((k-1)*1.1f,height*.64f+(k%2)*.8f,Mathf.Sin(k*2)*.8f));c.localScale=new Vector3(height*.27f,height*.34f,height*.27f);c.gameObject.AddComponent<MeshFilter>().sharedMesh=crown;c.gameObject.AddComponent<MeshRenderer>().sharedMaterial=k==1?leafLight:leaf;}
            Register(t);total++;inGroup++;
            if(inGroup==16){Batch(group);group=Group("Tree belt "+(++section),root,Vector3.zero);inGroup=0;}
        }
        if(inGroup>0)Batch(group);return total;
    }
    static int SpectatorZones()
    {
        var details=GameObject.Find("Detailed Circuit Facilities").transform;Transform source=null;
        foreach(Transform t in details)if(t.name.StartsWith("Spectator stand")){source=t;break;}
        if(!source)return 0;int count=0;
        for(int i=45;i<route.Length;i+=85)
        {
            var p=route[i];int side=(i/85)%2==0?-1:1;var pos=p.position+p.right*side*42;pos.y=0;
            if(!Clear(pos,14))continue;
            var q=Quaternion.LookRotation(Vector3.Cross(p.right*side,Vector3.up),Vector3.up);
            var stand=Object.Instantiate(source.gameObject,root).transform;stand.name="Corner grandstand "+count;stand.SetPositionAndRotation(pos,q);stand.localScale=Vector3.one;
            foreach(var text in stand.GetComponentsInChildren<TextMesh>())if(text.text.StartsWith("APEX PARK"))text.text="APEX PARK / SECTOR "+(count+1);
            Register(stand);
            var plaza=Group("Fan plaza "+count,root,pos+q*new Vector3(23,0,0));plaza.rotation=q;
            if(Clear(plaza.position,10))
            {
                Box(plaza,"Spectator promenade",new Vector3(0,.015f,0),new Vector3(15,.03f,18),stone);
                Box(plaza,"Refreshments kiosk",new Vector3(3,1.45f,0),new Vector3(4,2.9f,6),charcoal);
                Box(plaza,"Kiosk canopy",new Vector3(1,3.1f,0),new Vector3(8,.16f,7),cream);
                Box(plaza,"Serving window",new Vector3(.97f,1.7f,0),new Vector3(.08f,1.1f,4.6f),teal);
                for(int s=-1;s<=1;s+=2)for(int k=0;k<3;k++)
                {
                    float z=s*5;Box(plaza,"Picnic table",new Vector3(-4+k*3,.75f,z),new Vector3(2,.1f,1),cream);
                    for(int bench=-1;bench<=1;bench+=2)Box(plaza,"Bench",new Vector3(-4+k*3,.4f,z+bench*.85f),new Vector3(2,.12f,.3f),charcoal);
                }
                for(int flag=0;flag<3;flag++)
                {
                    float z=-6+flag*6;Beam(plaza,new Vector3(7,0,z),new Vector3(7,6,z),.07f,cream);
                    var v=new Vector3[15];var tri=new List<int>();for(int col=0;col<5;col++)for(int row=0;row<3;row++)
                    {v[col*3+row]=new Vector3(7-col*.5f,5.8f-row*.55f,z+Mathf.Sin(col*1.5f+row*.2f)*col*.08f);if(col<4&&row<2){int a=col*3+row;tri.AddRange(new[]{a,a+3,a+1,a+1,a+3,a+4,a+1,a+3,a,a+4,a+3,a+1});}}
                    var cloth=new Mesh();cloth.vertices=v;cloth.SetTriangles(tri,0);cloth.RecalculateNormals();cloth.RecalculateBounds();MeshObject("Team flag",plaza,SaveMesh("Flag "+count+" "+flag,cloth),flag%2==0?teal:red);
                }
                Batch(plaza);Register(plaza);
            }
            count++;
        }
        return count;
    }
    static void StartFinish()
    {
        var circuit=GameObject.Find("F1 Circuit - Apex Park").transform;
        foreach(Transform t in circuit) if(t.name=="Start bridge"||t.name=="Finish") foreach(var r in t.GetComponentsInChildren<Renderer>())r.enabled=false;
        var dressing=GameObject.Find("Grand Prix Circuit Dressing");
        if(dressing)foreach(var text in dressing.GetComponentsInChildren<TextMesh>())
        {
            if(text.text=="APEX  /  GRAND PRIX")
            {
                text.transform.localRotation=Quaternion.identity;text.characterSize=.18f;
            }
            else if(text.transform.parent.name=="Trackside branding")
            {var parent=text.transform.parent;parent.localRotation=Quaternion.Euler(0,Mathf.Sign(parent.localPosition.x)*90,0);text.transform.localRotation=Quaternion.identity;}
            else if(text.transform.parent.name.StartsWith("Braking "))text.transform.localRotation=Quaternion.identity;
        }
        // Finish markings were removed by user request. Retain race checkpoint logic.
        OverdriveGrandPrixPresentation.RemoveFinishMarkings();
    }
    [MenuItem("Overdrive/Apply Race Weekend Atmosphere")]
    public static string Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!="Assets/Scenes/OverdriveF1.unity")throw new InvalidOperationException("Apply in the saved F1 scene, outside Play mode.");
        var race=Object.FindFirstObjectByType<RaceManager>();route=race.ai.path.points;occupied.Clear();
        var old=GameObject.Find("Race Weekend Atmosphere");if(old)Object.DestroyImmediate(old);
        root=Group("Race Weekend Atmosphere",null,Vector3.zero);System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        charcoal=Mat("Paddock charcoal",new Color(.055f,.065f,.07f));stone=Mat("Walkway paving",new Color(.26f,.28f,.27f));cream=Mat("Warm white",new Color(.72f,.75f,.72f));red=Mat("Event red",new Color(.66f,.025f,.035f));teal=Mat("Event teal",new Color(.02f,.35f,.38f));bark=Mat("Tree bark",new Color(.18f,.135f,.085f));leaf=Mat("Deep foliage",new Color(.10f,.20f,.075f),.08f);leafLight=Mat("Sunlit foliage",new Color(.18f,.28f,.10f),.08f);
        var details=GameObject.Find("Detailed Circuit Facilities");if(details)foreach(Transform t in details.transform)Register(t);
        Landscape();Rubber();StartFinish();int stands=SpectatorZones();int trees=Trees();
        var paint=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/GrandPrix/Team 0.mat");if(paint){paint.SetFloat("_Metallic",.08f);paint.SetFloat("_Smoothness",.36f);EditorUtility.SetDirty(paint);}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        string result=$"routeGates={route.Length}; extraGrandstands={stands}; boundaryTrees={trees}; generatedColliders={root.GetComponentsInChildren<Collider>().Length}; rubberFollowsCurrentRoad=true; textureAndLightingPass=true";
        System.IO.File.WriteAllText("Docs/RaceWeekendArt_Result.txt",result);return result;
    }
    [MenuItem("Overdrive/Rebuild All Circuit Visuals For Current Layout")]
    public static string RebuildForCurrentLayout()
    {return OverdriveEnvironmentArt.Apply()+"\n"+Apply();}
    public static string Preview()
    {
        var race=Object.FindFirstObjectByType<RaceManager>();var car=race.player.transform;
        var go=new GameObject("Weekend preview camera");var cam=go.AddComponent<Camera>();cam.transform.position=car.TransformPoint(new Vector3(7,3.6f,-10));cam.transform.LookAt(car.position+car.forward*8);cam.fieldOfView=57;cam.farClipPlane=2500;
        var rt=RenderTexture.GetTemporary(1440,900,24);var old=RenderTexture.active;
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();System.IO.File.WriteAllBytes("Docs/RaceWeekendArt_Preview.png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}
        finally{RenderTexture.active=old;cam.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(go);}return "Preview saved";
    }
}
