using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using TMPro;
using Overdrive;
using Object=UnityEngine.Object;
using Art=OverdriveHarborCircuitBuilder;

public static class OverdriveYasMarinaBuilder
{
    const string Folder="Assets/Art/YasMarina",ScenePath="Assets/Scenes/OverdriveYasMarina.unity";
    public static Vector3[] Route()
    {
        var data=JObject.Parse(File.ReadAllText(Folder+"/source.geojson"));var coordinates=(JArray)data["features"][0]["geometry"]["coordinates"];
        double lon=(double)coordinates[0][0],lat=(double)coordinates[0][1],east=111320*System.Math.Cos(lat*System.Math.PI/180);
        var original=coordinates.Select(c=>new Vector3((float)(((double)c[0]-lon)*east),1.2f,(float)(((double)c[1]-lat)*111320))).ToList();
        if(Vector3.Distance(original[0],original[original.Count-1])<.01f)original.RemoveAt(original.Count-1);
        int n=original.Count;var dense=new List<Vector3>();
        for(int i=0;i<n;i++)
        {
            var p=original[i];var q=original[(i+1)%n];var before=p-original[(i+n-1)%n];var segment=q-p;var after=original[(i+2)%n]-q;
            var a=p+(before.normalized+segment.normalized).normalized*Mathf.Min(before.magnitude,segment.magnitude)*.33f;
            var b=q-(segment.normalized+after.normalized).normalized*Mathf.Min(segment.magnitude,after.magnitude)*.33f;
            int samples=Mathf.Max(5,Mathf.CeilToInt(segment.magnitude/2));
            for(int j=0;j<samples;j++){float t=j/(float)samples,s=1-t;dense.Add(s*s*s*p+3*s*s*t*a+3*s*t*t*b+t*t*t*q);}
        }
        dense.Add(dense[0]);var distances=new float[dense.Count];for(int i=1;i<dense.Count;i++)distances[i]=distances[i-1]+Vector3.Distance(dense[i-1],dense[i]);
        float total=distances[distances.Length-1],scale=5281/total;if(scale<.97f || scale>1.03f)throw new System.InvalidOperationException("Source layout length does not match the current 5.281km circuit");
        const int count=1320;var route=new Vector3[count];int index=0;
        for(int i=0;i<count;i++){float d=i*total/count;while(index<dense.Count-2 && distances[index+1]<d)index++;var p=Vector3.Lerp(dense[index],dense[index+1],(d-distances[index])/Mathf.Max(.001f,distances[index+1]-distances[index]));p.x*=scale;p.z*=scale;route[i]=p;}
        return route;
    }
    public static void AddTrackChoice()
    {
        var ui=Object.FindFirstObjectByType<RaceUI>();var info=ui.race.circuit;
        info.yasButton=Art.TrackButton(ui,"Yas Marina track","03  YAS MARINA",1262);
        info.apexButton.GetComponentInChildren<TMP_Text>().text="01  APEX PARK";info.harborButton.GetComponentInChildren<TMP_Text>().text="02  HARBOR RUN";
        EditorUtility.SetDirty(info);EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);EditorSceneManager.SaveScene(ui.gameObject.scene);
    }
    static void HotelDome(Transform decor,Vector3 centre,Material shell,Material ribs)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        const int rings=10,around=40;
        Vector3 Point(int ring,int j){float a=j*Mathf.PI*2/around,b=ring*Mathf.PI*.5f/rings;return centre+new Vector3(Mathf.Cos(a)*74*Mathf.Cos(b),Mathf.Sin(b)*20,Mathf.Sin(a)*48*Mathf.Cos(b));}
        for(int i=0;i<=rings;i++)for(int j=0;j<=around;j++)vertices.Add(Point(i,j));
        for(int i=0;i<rings;i++)for(int j=0;j<around;j++){int a=i*(around+1)+j,b=a+around+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
        var mesh=new Mesh{name="Yas hotel gridshell"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/Hotel gridshell.asset");
        var go=new GameObject("Illuminated hotel gridshell",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(decor,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=shell;
        for(int i=0;i<rings;i+=2)for(int j=0;j<around;j++)Art.Beam("Hotel lattice",Point(i,j),Point(i,j+1),.16f,ribs);
        for(int j=0;j<around;j+=2)for(int i=0;i<rings;i++)Art.Beam("Hotel lattice",Point(i,j),Point(i+1,j),.16f,ribs);
    }
    [MenuItem("Overdrive/Add Yas Marina Abu Dhabi Circuit")]
    public static string Build()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying || scene.isDirty || scene.path!="Assets/Scenes/OverdriveHarbor.unity")throw new System.InvalidOperationException("Open saved Harbor scene and stop Play");
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))throw new System.InvalidOperationException("Yas Marina already exists; preserve and edit the saved scene");
        AddTrackChoice();EditorSceneManager.OpenScene("Assets/Scenes/OverdriveF1.unity",OpenSceneMode.Single);AddTrackChoice();scene=EditorSceneManager.OpenScene("Assets/Scenes/OverdriveHarbor.unity",OpenSceneMode.Single);
        EditorSceneManager.SaveScene(scene,ScenePath,true);scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var ui=Object.FindFirstObjectByType<RaceUI>();var race=ui.race;var info=race.circuit;info.harbor=false;info.yasMarina=true;info.displayName="YAS MARINA";info.lengthKm=5.281f;info.racingLineHalfWidth=4.8f;
        var old=GameObject.Find("Harbor Run Circuit");old.SetActive(false);
        var root=new GameObject("Yas Marina Circuit").transform;var decor=new GameObject("Yas Marina Facilities").transform;decor.SetParent(root,false);Art.SetDecor(decor,ui.startLabel.font);
        Material Mat(string name,Color color,float gloss=.15f)=>Art.Mat(name,color,gloss,Folder);
        var teal=Mat("Yas turquoise",new Color(.025f,.5f,.57f));var white=Mat("Yas white",new Color(.89f,.92f,.94f));var navy=Mat("Yas dark blue",new Color(.025f,.045f,.10f));var red=Mat("Yas coral",new Color(.9f,.10f,.18f));var sand=Mat("Yas island sand",new Color(.46f,.38f,.28f));var sea=Mat("Marina water",new Color(.015f,.15f,.22f),.85f);var glass=Mat("Hotel glazing",new Color(.07f,.10f,.22f),.75f);var steel=Mat("Floodlight steel",new Color(.27f,.3f,.37f),.45f);
        var purple=Mat("Hotel violet glow",new Color(.27f,.12f,.5f),.5f);purple.EnableKeyword("_EMISSION");purple.SetColor("_EmissionColor",new Color(.45f,.10f,1)*1.3f);
        var luminous=Mat("Floodlight panels",Color.white);luminous.EnableKeyword("_EMISSION");luminous.SetColor("_EmissionColor",new Color(.9f,.95f,1)*3);
        var cyan=Mat("Yas cyan accent",new Color(.03f,.7f,.85f));cyan.EnableKeyword("_EMISSION");cyan.SetColor("_EmissionColor",new Color(.01f,.24f,.4f));
        var asphalt=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/HarborCircuit/Harbor navy.mat");
        // Reuse the established road texture, without changing the shared material.
        asphalt=old.transform.Find("Asphalt Surface").GetComponent<MeshRenderer>().sharedMaterial;
        Art.Box("Island ground",new Vector3(0,.7f,50),new Vector3(1550,.6f,2350),sand,true,root).AddComponent<TrackSurface>().grip=.65f;
        Art.Box("Surrounding gulf",new Vector3(0,-1,0),new Vector3(7000,.2f,7000),sea,false,root);
        var route=Route();
        void Strip(string name,float a,float b,float ya,float yb,Material first,Material second,bool collision,float grip,bool wall=false)
            =>Art.Strip(name,route,a,b,ya,yb,first,second,collision,grip,root,wall,Folder,8.9f);
        Strip("Asphalt Surface",-7.5f,7.5f,0,0,asphalt,null,true,1);
        foreach(int side in new[]{-1,1})
        {
            float a=side<0?-23:8.6f,b=side<0?-8.6f:23;Strip((side<0?"Left":"Right")+" Turquoise Runoff",a,b,-.012f,-.012f,teal,null,true,.86f);
            a=side<0?-8.6f:7.5f;b=side<0?-7.5f:8.6f;Strip((side<0?"Left":"Right")+" Kerb",a,b,.012f,.012f,teal,white,true,.96f);
            a=side<0?-7.5f:7.34f;b=side<0?-7.34f:7.5f;Strip((side<0?"Left":"Right")+" Edge Line",a,b,.018f,.018f,white,null,false,1);
            Strip((side<0?"Left":"Right")+" Safety Wall",side*23,side*23,0,1.1f,navy,teal,true,1,true);
        }
        var path=GameObject.Find("AI centerline").GetComponent<WaypointPath>();var checkpoints=new GameObject("Yas ordered checkpoints").transform;checkpoints.SetParent(root,false);path.points=new Transform[route.Length/2];
        for(int i=0;i<path.points.Length;i++)
        {
            int j=i*2;var p=new GameObject("Yas CP "+i).transform;p.SetParent(checkpoints,false);p.SetPositionAndRotation(route[j],Quaternion.LookRotation(route[(j+2)%route.Length]-route[j]));
            var trigger=p.gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=Vector3.up*2;trigger.size=new Vector3(18,4,1.5f);p.gameObject.AddComponent<CircuitCheckpoint>().index=i;path.points[i]=p;
        }
        foreach(var car in Object.FindObjectsByType<OverdriveVehicle>(FindObjectsInactive.Include,FindObjectsSortMode.None)){car.GetComponent<CircuitProgress>().checkpoints=path.points;var ai=car.GetComponent<AIDriver>();if(ai)ai.path=path;}
        var guide=Object.FindFirstObjectByType<DrivingGuide>();guide.qualifyingLine=null;guide.cornerCalibrations=new DrivingGuide.CornerCalibration[0];guide.tuning.baseLateralAcceleration=14;guide.tuning.maximumLateralAcceleration=22;guide.RebuildProfile();
        var start=path.points[0];var rotation=start.rotation;Vector3 At(float side,float along,float height)=>start.position+start.right*side+start.forward*along+Vector3.up*height;
        GameObject FrameBox(string name,float side,float along,float height,Vector3 scale,Material material){var go=Art.Box(name,At(side,along,height),scale,material);go.transform.rotation=rotation;return go;}
        FrameBox("Yas pit building",36,0,7,new Vector3(25,12,255),white);FrameBox("Pit balcony glazing",22.9f,0,10,new Vector3(.4f,4,242),glass);FrameBox("Pit roof",36,0,13.2f,new Vector3(28,.8f,263),navy);
        for(int i=0;i<15;i++)FrameBox("Pit garage",23.3f,-105+i*15,3.5f,new Vector3(.3f,5,11.5f),i%2==0?teal:navy);
        for(int row=0;row<8;row++)FrameBox("Main grandstand seats",-30-row*2.1f,-40,1+row*.65f,new Vector3(2,.6f,170),row%2==0?teal:navy);
        FrameBox("Main grandstand canopy",-38,-40,12,new Vector3(28,.7f,180),white);
        for(int i=-110;i<50;i+=25){Art.Beam("Canopy support",At(-25,i,0),At(-25,i,12),.25f,steel);Art.Beam("Canopy support",At(-51,i,0),At(-51,i,12),.25f,steel);}
        Art.Beam("Timing gantry support",At(-12,25,0),At(-12,25,9),.4f,steel);Art.Beam("Timing gantry support",At(12,25,0),At(12,25,9),.4f,steel);FrameBox("Yas timing gantry",0,25,8.5f,new Vector3(25,2.3f,.8f),navy);Art.Word("YAS MARINA",At(0,24.55f,8.5f),rotation,7,new Vector2(24,2),Color.white);
        // Stylised hotel landmarks and marina are scenery, with clear space above the track.
        foreach(float x in new[]{65f,145f})
        {
            var pod=GameObject.CreatePrimitive(PrimitiveType.Sphere);pod.name="Yas hotel pod";pod.transform.SetParent(decor,false);pod.transform.position=new Vector3(x,14,-296);pod.transform.localScale=new Vector3(43,25,70);pod.GetComponent<Renderer>().sharedMaterial=glass;Object.DestroyImmediate(pod.GetComponent<Collider>());
        }
        HotelDome(decor,new Vector3(105,22,-300),purple,luminous);
        Art.Box("Marina basin",new Vector3(180,1.06f,-470),new Vector3(190,.025f,160),sea);
        for(int i=0;i<7;i++)
        {
            float x=105+i*24;Art.Box("Marina pontoon",new Vector3(x,1.3f,-463),new Vector3(2,.5f,142),white);
            for(int j=0;j<3;j++){float z=-420-j*40;Art.Box("Yacht hull",new Vector3(x+9,1.8f,z),new Vector3(9,2,23),white);Art.Box("Yacht cabin",new Vector3(x+9,3.8f,z-2),new Vector3(6,2,11),glass);}
        }
        for(int i=0;i<path.points.Length;i+=20)
        {
            var point=path.points[i];
            foreach(int side in new[]{-1,1})
            {
                var p=point.position+point.right*side*28;Art.Beam("Floodlight mast",p,p+Vector3.up*17,.24f,steel);Art.Beam("Floodlight crossbar",p+Vector3.up*17-point.forward*2,p+Vector3.up*17+point.forward*2,.2f,steel);
                for(int k=0;k<4;k++){var lamp=Art.Box("Floodlight bank",p+Vector3.up*16.7f+point.forward*(k-1.5f)*1.05f,new Vector3(1.2f,.45f,.8f),luminous);lamp.transform.rotation=point.rotation;}
            }
        }
        foreach(int along in new[]{-60,50}){var go=new GameObject("Pit area evening light",typeof(Light));go.transform.SetParent(decor,false);go.transform.position=At(0,along,15);var light=go.GetComponent<Light>();light.type=LightType.Point;light.range=70;light.intensity=4;light.color=new Color(.7f,.8f,1);light.shadows=LightShadows.None;}
        // Distance boards before the largest geometric turns.
        var corners=Enumerable.Range(0,path.points.Length).OrderByDescending(i=>path.CurvatureAhead(i+1,1));var selected=new List<int>();
        foreach(int index in corners){if(selected.Any(j=>Mathf.Min(Mathf.Abs(j-index),path.points.Length-Mathf.Abs(j-index))<60))continue;selected.Add(index);if(selected.Count==6)break;}
        foreach(int corner in selected)foreach(int metres in new[]{300,200,100}){int i=(corner-Mathf.RoundToInt(metres/8f)+path.points.Length)%path.points.Length;var point=path.points[i];var pos=point.position+point.right*10;var board=Art.Box("Brake board "+metres,pos+Vector3.up*1.3f,new Vector3(1.5f,2.1f,.1f),white);board.transform.rotation=point.rotation;Art.Word(metres.ToString(),pos+Vector3.up*1.3f-point.forward*.06f,point.rotation,6,new Vector2(1.45f,2),Color.black);}
        Art.BatchDecorations(Folder);
        foreach(var text in ui.GetComponentsInChildren<TMP_Text>(true)){if(text.name=="Circuit heading")text.text="YAS MARINA";if(text.text.Contains("HARBOR RUN") && text.transform.parent!=info.harborButton.transform)text.text=text.text.Replace("HARBOR RUN","YAS MARINA");}
        info.apexButton.GetComponentInChildren<TMP_Text>().text="01  APEX PARK";info.harborButton.GetComponentInChildren<TMP_Text>().text="02  HARBOR RUN";info.yasButton.GetComponentInChildren<TMP_Text>().text="03  YAS MARINA";
        Physics.SyncTransforms();OverdriveGridSetup.AlignGridToRoad();OverdriveLineGatesSetup.Apply();
        var sun=GameObject.Find("Sun").GetComponent<Light>();sun.transform.rotation=Quaternion.Euler(12,-45,0);sun.color=new Color(1,.71f,.58f);sun.intensity=1.15f;
        if(RenderSettings.skybox){var sky=new Material(RenderSettings.skybox);sky.name="Yas twilight sky";if(sky.HasProperty("_SkyTint"))sky.SetColor("_SkyTint",new Color(.25f,.28f,.43f));if(sky.HasProperty("_GroundColor"))sky.SetColor("_GroundColor",new Color(.44f,.22f,.18f));AssetDatabase.CreateAsset(sky,Folder+"/Twilight sky.mat");RenderSettings.skybox=sky;}
        RenderSettings.ambientLight=new Color(.43f,.43f,.56f);RenderSettings.fogColor=new Color(.32f,.31f,.43f);RenderSettings.fogStartDistance=1100;RenderSettings.fogEndDistance=4000;
        Camera.main.transform.SetPositionAndRotation(race.player.transform.position-start.forward*9+Vector3.up*3,start.rotation);Camera.main.farClipPlane=4000;
        var build=EditorBuildSettings.scenes.ToList();if(!build.Any(s=>s.path==ScenePath))build.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=build.ToArray();
        EditorUtility.SetDirty(info);EditorUtility.SetDirty(guide);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        float length=0;for(int i=0;i<route.Length;i++)length+=Vector3.Distance(route[i],route[(i+1)%route.Length]);
        var report=new{track="YAS MARINA",source="bacinger/f1-circuits ae-2009.geojson current 5281m layout",lengthM=length,checkpoints=path.points.Length,roadWidth=15,scene=ScenePath};File.WriteAllText("Docs/YasMarina_Build.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return Newtonsoft.Json.JsonConvert.SerializeObject(report);
    }
}
