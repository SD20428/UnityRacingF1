using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Overdrive;
using Object=UnityEngine.Object;

public static class OverdriveHarborCircuitBuilder
{
    const string Folder="Assets/Art/HarborCircuit",ScenePath="Assets/Scenes/OverdriveHarbor.unity";
    static Transform decor;static TMP_FontAsset font;
    public static Material Mat(string name,Color color,float smooth=.1f,string assetFolder=null)
    {
        string path=(assetFolder??Folder)+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);return m;
    }
    public static void SetDecor(Transform target,TMP_FontAsset targetFont){decor=target;font=targetFont;}
    public static GameObject Box(string name,Vector3 pos,Vector3 scale,Material m,bool collision=false,Transform parent=null)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent?parent:decor,false);go.transform.position=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=m;
        if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    public static void Beam(string name,Vector3 a,Vector3 b,float width,Material m)
    {var g=Box(name,(a+b)*.5f,new Vector3(width,width,(b-a).magnitude),m);g.transform.rotation=Quaternion.LookRotation(b-a);}
    public static void Word(string text,Vector3 pos,Quaternion rotation,float size,Vector2 area,Color color)
    {
        var t=new GameObject("Sign "+text).AddComponent<TextMeshPro>();t.transform.SetParent(decor,false);t.transform.SetPositionAndRotation(pos,rotation);
        t.font=font;t.text=text;t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.rectTransform.sizeDelta=area;t.color=color;
    }
    static Vector3 Cubic(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
        =>.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
    static Vector3[] Route()
    {
        var xy=new[]{new Vector2(0,-450),new Vector2(0,-100),new Vector2(0,250),new Vector2(0,450),new Vector2(90,610),new Vector2(260,620),new Vector2(370,500),new Vector2(380,330),new Vector2(520,280),new Vector2(660,410),new Vector2(820,400),new Vector2(920,230),new Vector2(920,50),new Vector2(800,-80),new Vector2(670,-170),new Vector2(700,-340),new Vector2(860,-440),new Vector2(890,-610),new Vector2(730,-750),new Vector2(530,-760),new Vector2(360,-660),new Vector2(200,-590),new Vector2(150,-700),new Vector2(20,-740),new Vector2(-100,-620),new Vector2(-50,-520)};
        int n=xy.Length;var controls=xy.Select(v=>new Vector3(v.x,1.2f,v.y)).ToArray();var dense=new List<Vector3>();
        // Start on the middle of the long straight, leaving room for the entire 20-car grid.
        for(int k=0;k<n;k++){int i=(k+1)%n;for(int j=0;j<80;j++)dense.Add(Cubic(controls[(i+n-1)%n],controls[i],controls[(i+1)%n],controls[(i+2)%n],j/80f));}
        dense.Add(dense[0]);var lengths=new float[dense.Count];for(int i=1;i<lengths.Length;i++)lengths[i]=lengths[i-1]+Vector3.Distance(dense[i-1],dense[i]);
        int count=Mathf.RoundToInt(lengths[lengths.Length-1]/4);var output=new Vector3[count];int segment=0;
        for(int i=0;i<count;i++){float distance=i*lengths[lengths.Length-1]/count;while(segment<dense.Count-2 && lengths[segment+1]<distance)segment++;output[i]=Vector3.Lerp(dense[segment],dense[segment+1],(distance-lengths[segment])/Mathf.Max(.001f,lengths[segment+1]-lengths[segment]));}
        return output;
    }
    public static Mesh Strip(string name,Vector3[] path,float a,float b,float ya,float yb,Material first,Material second,bool collision,float grip,Transform root,bool wall=false,string assetFolder=null,float minimumInsideLimit=0)
    {
        int n=path.Length;var v=new Vector3[(n+1)*2];var uv=new Vector2[v.Length];var t0=new List<int>();var t1=new List<int>();float distance=0;
        for(int i=0;i<=n;i++)
        {
            int p=i%n;var tangent=(path[(p+1)%n]-path[(p+n-1)%n]).normalized;var right=Vector3.Cross(Vector3.up,tangent).normalized;
            if(i>0)distance+=Vector3.Distance(path[(i-1)%n],path[p]);
            float left=a,rightOffset=b;
            if(minimumInsideLimit>0)
            {
                var incoming=path[p]-path[(p+n-1)%n];var outgoing=path[(p+1)%n]-path[p];
                float turn=Vector3.SignedAngle(incoming,outgoing,Vector3.up)*Mathf.Deg2Rad;
                float limit=Mathf.Max(minimumInsideLimit,(incoming.magnitude+outgoing.magnitude)*.5f/Mathf.Max(.00001f,Mathf.Abs(turn))*.7f);
                if(left*turn>0)left=Mathf.Sign(left)*Mathf.Min(Mathf.Abs(left),limit);
                if(rightOffset*turn>0)rightOffset=Mathf.Sign(rightOffset)*Mathf.Min(Mathf.Abs(rightOffset),limit);
            }
            v[i*2]=path[p]+right*left+Vector3.up*ya;v[i*2+1]=path[p]+right*rightOffset+Vector3.up*yb;
            uv[i*2]=new Vector2(0,distance/12);uv[i*2+1]=new Vector2(Mathf.Abs(b-a)/8,distance/12);
            if(i<n){int x=i*2,y=x+2;var t=second && ((int)(distance/3)%2==1)?t1:t0;t.AddRange(new[]{x,y,x+1,x+1,y,y+1});}
        }
        var mesh=new Mesh{name="Circuit "+name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=v;mesh.uv=uv;mesh.subMeshCount=second?2:1;mesh.SetTriangles(t0,0);if(second)mesh.SetTriangles(t1,1);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,(assetFolder??Folder)+"/"+name+".asset");
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=second?new[]{first,second}:new[]{first};
        if(collision)
        {
            var collider=go.AddComponent<MeshCollider>();
            if(wall){var back=Object.Instantiate(mesh);back.name=name+" collision";var tris=new List<int>(mesh.triangles);var front=mesh.triangles;for(int j=0;j<front.Length;j+=3)tris.AddRange(new[]{front[j+2],front[j+1],front[j]});back.subMeshCount=1;back.SetTriangles(tris,0);AssetDatabase.CreateAsset(back,(assetFolder??Folder)+"/"+name+" collision.asset");collider.sharedMesh=back;}
            else collider.sharedMesh=mesh;
            var surface=go.AddComponent<TrackSurface>();surface.grip=grip;surface.rollingResistance=grip<.9f?.035f:.012f;
        }
        return mesh;
    }
    public static UnityEngine.UI.Button TrackButton(RaceUI ui,string name,string title,float x)
    {
        var found=ui.menu.transform.Find(name);var go=found?found.gameObject:new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));go.transform.SetParent(ui.menu.transform,false);
        var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-126);r.sizeDelta=new Vector2(245,46);
        var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.04f,.07f,.1f);var button=go.GetComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
        var label=go.GetComponentInChildren<TMP_Text>();if(!label){label=new GameObject("Caption",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();label.transform.SetParent(go.transform,false);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(15,0);label.rectTransform.offsetMax=new Vector2(-8,0);}
        label.text=title;label.font=ui.startLabel.font;label.fontSize=16;label.color=Color.white;label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;
        var motion=go.GetComponent<UIButtonMotion>();if(!motion)motion=go.AddComponent<UIButtonMotion>();motion.caption=label;
        var nav=button.navigation;nav.mode=UnityEngine.UI.Navigation.Mode.Automatic;button.navigation=nav;return button;
    }
    static CircuitInfo Menu(RaceUI ui,bool harbor,float length)
    {
        var info=ui.race.GetComponent<CircuitInfo>();if(!info)info=ui.race.gameObject.AddComponent<CircuitInfo>();info.ui=ui;info.harbor=harbor;info.displayName=harbor?"HARBOR RUN":"APEX PARK";info.lengthKm=length/1000;ui.race.circuit=info;
        info.apexButton=TrackButton(ui,"Apex track","01  APEX PARK",740);info.harborButton=TrackButton(ui,"Harbor track","02  HARBOR RUN",1001);
        info.description=ui.menu.transform.Find("Red Intro Hero Subcaption").GetComponent<TMP_Text>();
        foreach(var text in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            if(text.name=="Circuit heading")text.text=info.displayName;
            if(harbor && text.text.Contains("APEX PARK"))text.text=text.text.Replace("APEX PARK","HARBOR RUN");
        }
        info.apexButton.GetComponentInChildren<TMP_Text>().text="01  APEX PARK";
        info.harborButton.GetComponentInChildren<TMP_Text>().text="02  HARBOR RUN";
        EditorUtility.SetDirty(info);EditorUtility.SetDirty(ui.race);return info;
    }
    public static void BatchDecorations(string assetFolder=null)
    {
        var groups=new Dictionary<Material,List<CombineInstance>>();var original=new List<GameObject>();
        foreach(var f in decor.GetComponentsInChildren<MeshFilter>())
        {
            var renderer=f.GetComponent<MeshRenderer>();if(!renderer || f.GetComponent<TMP_Text>() || !f.sharedMesh || f.sharedMesh.subMeshCount!=1)continue;
            var m=renderer.sharedMaterial;if(!groups.ContainsKey(m))groups[m]=new List<CombineInstance>();groups[m].Add(new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix});original.Add(f.gameObject);
        }
        int index=0;foreach(var pair in groups){var mesh=new Mesh{name="Circuit scenery "+index,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);AssetDatabase.CreateAsset(mesh,(assetFolder??Folder)+"/Scenery "+index+".asset");var go=new GameObject("Scenery batch "+index++,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(decor,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=pair.Key;}
        foreach(var go in original)Object.DestroyImmediate(go);
    }
    [MenuItem("Overdrive/Build Harbor Run Circuit")]
    public static string Build()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying || scene.path!="Assets/Scenes/OverdriveF1.unity" || scene.isDirty)throw new System.InvalidOperationException("Open saved Apex Park scene and stop Play");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))throw new System.InvalidOperationException("Harbor scene already exists; edit it in place");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art","HarborCircuit");
        var ui=Object.FindFirstObjectByType<RaceUI>();var old=ui.race.ai.path;float oldLength=0;for(int i=0;i<old.points.Length;i++)oldLength+=Vector3.Distance(old.Position(i),old.Position(i+1));
        Menu(ui,false,oldLength);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        EditorSceneManager.SaveScene(scene,ScenePath,true);scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);ui=Object.FindFirstObjectByType<RaceUI>();font=ui.startLabel.font;
        var asphalt=GameObject.Find("Refined Circuit").transform.Find("Asphalt Surface").GetComponent<MeshRenderer>().sharedMaterial;
        foreach(string name in new[]{"Terrain","F1 Circuit - Apex Park","Refined Circuit","Grand Prix Circuit Dressing","Detailed Circuit Facilities","Race Weekend Atmosphere","Precision Detail Polish"}){var go=GameObject.Find(name);if(go)go.SetActive(false);}
        var root=new GameObject("Harbor Run Circuit").transform;decor=new GameObject("Harbor Scenery").transform;decor.SetParent(root,false);
        var sea=Mat("Deep blue sea",new Color(.025f,.22f,.31f),.8f);var sand=Mat("Coastal sand",new Color(.48f,.43f,.29f));var teal=Mat("Teal runoff",new Color(.025f,.28f,.29f));var white=Mat("Warm white",new Color(.88f,.9f,.88f));var red=Mat("Coral kerb",new Color(.95f,.12f,.10f));var navy=Mat("Harbor navy",new Color(.03f,.065f,.10f));var steel=Mat("Weathered steel",new Color(.29f,.35f,.37f),.35f);var orange=Mat("Crane orange",new Color(1,.38f,.045f));var glass=Mat("Ocean glass",new Color(.035f,.27f,.36f),.7f);var green=Mat("Palm green",new Color(.07f,.19f,.10f));var bark=Mat("Palm trunk",new Color(.33f,.23f,.14f));
        Box("Open sea",new Vector3(400,-1.4f,-100),new Vector3(7000,.2f,7000),sea,false,root);
        var island=Box("Coastal island",new Vector3(420,.7f,-80),new Vector3(1400,.6f,1800),sand,true,root);var islandSurface=island.AddComponent<TrackSurface>();islandSurface.grip=.65f;islandSurface.rollingResistance=.05f;
        var route=Route();float length=0;for(int i=0;i<route.Length;i++)length+=Vector3.Distance(route[i],route[(i+1)%route.Length]);
        Strip("Asphalt Surface",route,-9.5f,9.5f,0,0,asphalt,null,true,1,root);
        foreach(int side in new[]{-1,1})
        {
            float lo=side<0?-19:10.6f,hi=side<0?-10.6f:19;
            Strip((side<0?"Left":"Right")+" Runoff",route,lo,hi,-.01f,-.01f,teal,null,true,.86f,root);
            lo=side<0?-10.6f:9.5f;hi=side<0?-9.5f:10.6f;Strip((side<0?"Left":"Right")+" Kerb",route,lo,hi,.012f,.012f,red,white,true,.95f,root);
            lo=side<0?-9.5f:9.32f;hi=side<0?-9.32f:9.5f;Strip((side<0?"Left":"Right")+" White Line",route,lo,hi,.02f,.02f,white,null,false,1,root);
            Strip((side<0?"Left":"Right")+" Safety Barrier",route,side*24,side*24,0,1.15f,white,navy,true,1,root,true);
        }
        var checkpoints=new GameObject("Harbor ordered checkpoints").transform;checkpoints.SetParent(root,false);var path=GameObject.Find("AI centerline").GetComponent<WaypointPath>();path.points=new Transform[route.Length/2];
        for(int i=0;i<path.points.Length;i++)
        {
            int j=i*2;var point=new GameObject("Harbor CP "+i).transform;point.SetParent(checkpoints,false);point.SetPositionAndRotation(route[j],Quaternion.LookRotation(route[(j+2)%route.Length]-route[j]));
            var box=point.gameObject.AddComponent<BoxCollider>();box.isTrigger=true;box.center=Vector3.up*2;box.size=new Vector3(22,4,1.5f);point.gameObject.AddComponent<CircuitCheckpoint>().index=i;path.points[i]=point;
        }
        foreach(var car in Object.FindObjectsByType<OverdriveVehicle>(FindObjectsInactive.Include,FindObjectsSortMode.None)){car.GetComponent<CircuitProgress>().checkpoints=path.points;var driver=car.GetComponent<AIDriver>();if(driver)driver.path=path;EditorUtility.SetDirty(car.GetComponent<CircuitProgress>());if(driver)EditorUtility.SetDirty(driver);}
        var guide=Object.FindFirstObjectByType<DrivingGuide>();guide.qualifyingLine=null;guide.cornerCalibrations=new DrivingGuide.CornerCalibration[0];guide.tuning.baseLateralAcceleration=14;guide.tuning.maximumLateralAcceleration=22;guide.RebuildProfile();EditorUtility.SetDirty(guide);
        Menu(ui,true,length);
        // Main straight pit complex, roofed spectator terraces and start gantry.
        Box("Pit paddock",new Vector3(-47,7,35),new Vector3(28,12,190),white);Box("Pit roof",new Vector3(-47,13.4f,35),new Vector3(32,1,196),navy);
        Box("Control glazing",new Vector3(-31.9f,10,35),new Vector3(.3f,4,176),glass);
        for(int i=0;i<12;i++){Box("Garage door",new Vector3(-32.8f,4,-45+i*14),new Vector3(.3f,5.5f,11),i%2==0?teal:navy);Box("Pit apron",new Vector3(-25,1.22f,-45+i*14),new Vector3(7,.05f,11),navy);}
        for(int row=0;row<8;row++)Box("Grandstand terrace",new Vector3(33+row*2.1f,2.3f+row*.65f,65),new Vector3(2.2f,.7f,150),row%2==0?teal:navy);
        Box("Grandstand canopy",new Vector3(42,12,65),new Vector3(27,1,158),white);
        for(int z=-5;z<145;z+=25){Beam("Roof support",new Vector3(29,1,z),new Vector3(29,12,z),.3f,steel);Beam("Roof support",new Vector3(55,1,z),new Vector3(55,12,z),.3f,steel);}
        Beam("Gantry left",new Vector3(-15,1,10),new Vector3(-15,10,10),.5f,steel);Beam("Gantry right",new Vector3(15,1,10),new Vector3(15,10,10),.5f,steel);Box("Harbor start gantry",new Vector3(0,9.2f,10),new Vector3(31,2.6f,1.2f),navy);Word("HARBOR RUN",new Vector3(0,9.2f,9.35f),Quaternion.identity,8,new Vector2(29,2.3f),Color.white);
        // Harbor cranes, cargo rows and ships sit beyond the driveable corridor.
        for(int k=0;k<3;k++)
        {
            float z=100+k*210;Box("Dock platform",new Vector3(-200,.2f,z),new Vector3(80,2,170),steel);
            Beam("Crane tower",new Vector3(-190,1,z),new Vector3(-190,42,z),1.4f,orange);Beam("Crane boom",new Vector3(-235,41,z),new Vector3(-135,41,z),1.2f,orange);Beam("Crane brace",new Vector3(-190,42,z),new Vector3(-190,28,z+18),.7f,orange);Beam("Hoist cable",new Vector3(-229,41,z),new Vector3(-229,7,z),.12f,steel);
            for(int row=0;row<4;row++)for(int j=0;j<5;j++)Box("Cargo container",new Vector3(-115-row*16,2.7f+(j%2)*3,z-60+j*23),new Vector3(12,3,20),j%3==0?orange:j%3==1?teal:navy);
            Box("Freighter hull",new Vector3(-330,1.2f,z+30),new Vector3(45,6,135),navy);Box("Freighter deck",new Vector3(-330,5,z+30),new Vector3(39,1,123),white);Box("Bridge house",new Vector3(-330,12,z-10),new Vector3(30,14,25),white);
        }
        for(int i=0;i<path.points.Length;i+=25)
        {
            var t=path.points[i];int side=(i/25)%2==0?1:-1;var p=t.position+t.right*side*32;
            Beam("Track light mast",p,p+Vector3.up*10,.17f,steel);Box("Track luminaire",p+Vector3.up*10+t.right*side*1,new Vector3(2,.2f,.7f),white);
            if(i%50!=0)continue;var palm=p+t.right*side*8;Beam("Palm trunk",palm,palm+Vector3.up*9,.38f,bark);
            for(int leaf=0;leaf<6;leaf++){float angle=leaf*Mathf.PI/3;var d=new Vector3(Mathf.Cos(angle),-.35f,Mathf.Sin(angle));Beam("Palm frond",palm+Vector3.up*9,palm+Vector3.up*9+d*5,.65f,green);}
        }
        // Six distinct braking areas; boards show distance to the sampled corner apex.
        var candidates=Enumerable.Range(0,path.points.Length).OrderByDescending(i=>path.CurvatureAhead(i+1,1));var chosen=new List<int>();
        foreach(int apex in candidates){if(chosen.Any(i=>Mathf.Min(Mathf.Abs(i-apex),path.points.Length-Mathf.Abs(i-apex))<65))continue;chosen.Add(apex);if(chosen.Count==6)break;}
        foreach(int apex in chosen)foreach(int metres in new[]{300,200,100})
        {
            int index=(apex-Mathf.RoundToInt(metres/8f)+path.points.Length)%path.points.Length;var t=path.points[index];var p=t.position+t.right*12;
            var board=Box("Braking board "+metres,p+Vector3.up*1.4f,new Vector3(1.8f,2.4f,.12f),white);board.transform.rotation=t.rotation;Word(metres.ToString(),p+Vector3.up*1.4f-t.forward*.08f,t.rotation,7,new Vector2(1.7f,2.3f),new Color(.02f,.04f,.05f));
        }
        BatchDecorations();Physics.SyncTransforms();OverdriveGridSetup.AlignGridToRoad();OverdriveLineGatesSetup.Apply();
        var camera=Camera.main;camera.farClipPlane=3500;camera.transform.position=ui.race.player.transform.position+new Vector3(0,3,-9);camera.transform.LookAt(ui.race.player.transform.position+Vector3.up);
        RenderSettings.fog=true;RenderSettings.fogColor=new Color(.48f,.65f,.72f);RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=1100;RenderSettings.fogEndDistance=3500;
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/OverdriveF1.unity",true),new EditorBuildSettingsScene(ScenePath,true)};
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        var report=new{track="HARBOR RUN",lengthM=length,checkpoints=path.points.Length,roadWidth=19,grid=20,scene=ScenePath};System.IO.File.WriteAllText("Docs/Harbor_Circuit_Build.json",JsonUtility.ToJson(new BuildReport{lengthM=length,checkpoints=path.points.Length},true));return Newtonsoft.Json.JsonConvert.SerializeObject(report);
    }
    [System.Serializable] sealed class BuildReport {public float lengthM;public int checkpoints;}
}
