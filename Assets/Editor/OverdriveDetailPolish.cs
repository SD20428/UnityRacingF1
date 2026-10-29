using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using Overdrive;
using TMPro;
using Object=UnityEngine.Object;

// Additional visual geometry only. Existing aero detail, race markings and physics stay intact.
public static class OverdriveDetailPolish
{
    const string Folder="Assets/Art/DetailPolish";
    static Material carbon,metal,rubber,led;
    static Transform Group(string name,Transform parent){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    static Material Mat(string name,Color color,float metallic,float smooth)
    {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color,enableInstancing=true};m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;}
    static Transform Piece(Transform parent,string name,Vector3 p,Vector3 scale,Material material,PrimitiveType primitive=PrimitiveType.Cube,Quaternion? rotation=null)
    {
        var go=GameObject.CreatePrimitive(primitive);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;if(rotation.HasValue)go.transform.localRotation=rotation.Value;go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;
    }
    static void Batch(Transform root,string name)
    {
        var groups=new Dictionary<Material,List<CombineInstance>>();var remove=new List<GameObject>();
        foreach(var f in root.GetComponentsInChildren<MeshFilter>()){var r=f.GetComponent<Renderer>();if(!groups.ContainsKey(r.sharedMaterial))groups[r.sharedMaterial]=new List<CombineInstance>();groups[r.sharedMaterial].Add(new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});remove.Add(f.gameObject);}
        foreach(var pair in groups){var mesh=new Mesh{name=name+" "+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/"+mesh.name+".asset");var t=Group(mesh.name,root);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;}
        foreach(var go in remove)Object.DestroyImmediate(go);
    }
    static Transform CarPrototype(Transform car)
    {
        var root=Group("Precision body detailing",car);
        for(int side=-1;side<=1;side+=2)
        {
            // Recessed radiator grille, body panel joints and flush quarter-turn fasteners.
            for(int i=0;i<7;i++)Piece(root,"Radiator intake grille",new Vector3(side*.66f,.115f+i*.018f,.675f),new Vector3(.32f,.007f,.012f),metal);
            Piece(root,"Sidepod panel seam",new Vector3(side*.955f,.12f,-.56f),new Vector3(.008f,.008f,.78f),rubber);
            for(int i=0;i<5;i++)Piece(root,"Flush panel fastener",new Vector3(side*.961f,.19f,-.95f+i*.21f),new Vector3(.025f,.002f,.025f),metal,PrimitiveType.Cylinder,Quaternion.Euler(0,0,90));
            for(int row=0;row<2;row++)for(int col=0;col<3;col++)Piece(root,"Wing endplate fastener",new Vector3(side*1.058f,.65f+row*.25f,-2.31f+col*.18f),new Vector3(.019f,.002f,.019f),metal,PrimitiveType.Cylinder,Quaternion.Euler(0,0,90));
            Piece(root,"Mirror reflective insert",new Vector3(side*.62f,.48f,.25f),new Vector3(.155f,.065f,.005f),metal);
            Piece(root,"Rear wing trailing edge",new Vector3(side*.53f,1.075f,-2.459f),new Vector3(.89f,.008f,.012f),metal);
            for(int i=0;i<3;i++)Piece(root,"Diffuser heat shield",new Vector3(side*(.19f+i*.17f),-.13f,-2.2f),new Vector3(.035f,.065f,.32f),metal);
            for(int axle=0;axle<2;axle++)Piece(root,"Suspension clevis",new Vector3(side*.96f,-.02f,axle==0?1.65f:-1.65f),new Vector3(.075f,.045f,.065f),metal);
        }
        Piece(root,"Rear impact structure",new Vector3(0,-.035f,-2.28f),new Vector3(.24f,.14f,.27f),carbon);
        for(int x=0;x<4;x++)for(int y=0;y<3;y++)Piece(root,"Rain light LED lens",new Vector3(-.042f+x*.028f,.005f+y*.026f,-2.425f),new Vector3(.018f,.016f,.008f),led);
        for(int i=0;i<5;i++)Piece(root,"Rear crash structure rib",new Vector3(0,-.035f,-2.17f-i*.045f),new Vector3(.255f,.15f,.008f),rubber);
        // Cockpit instruments are visible from the upper camera without blocking the driver.
        Piece(root,"Steering wheel display housing",new Vector3(0,.39f,.015f),new Vector3(.22f,.10f,.045f),carbon);
        for(int i=0;i<5;i++)Piece(root,"Steering shift indicator",new Vector3(-.075f+i*.038f,.449f,.008f),new Vector3(.022f,.009f,.01f),led);
        Batch(root,"Body precision");return root;
    }
    static void TyreLabels(OverdriveVehicle car)
    {
        for(int i=0;i<car.visuals.Length;i++)
        {
            int side=i%2==0?-1:1;
            foreach(int top in new[]{-1,1})
            {
                var t=Group("Embossed tyre compound",car.visuals[i]);t.localPosition=new Vector3(side*(i<2?.205f:.245f),top*.255f,0);t.localRotation=Quaternion.Euler(0,side*90,top<0?180:0);
                var text=t.gameObject.AddComponent<TextMesh>();text.text=top>0?"RACE SLICK":"SOFT  /  18";text.fontSize=64;text.characterSize=.006f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.78f,.73f,.43f);
            }
        }
    }
    public static string RefineTyreLabels()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play.");
        var car=Object.FindFirstObjectByType<RaceManager>().player;var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Sidewall ink.mat");
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Sidewall ink"};material.SetFloat("_AlphaClip",1);material.SetFloat("_Cutoff",.35f);material.SetFloat("_Cull",0);material.EnableKeyword("_ALPHATEST_ON");material.renderQueue=2450;AssetDatabase.CreateAsset(material,Folder+"/Sidewall ink.mat");}
        int count=0;for(int i=0;i<car.visuals.Length;i++)foreach(var text in car.visuals[i].GetComponentsInChildren<TextMesh>())
        {
            if(text.name!="Embossed tyre compound")continue;int side=i%2==0?-1:1;text.characterSize=.006f;text.transform.localRotation=Quaternion.Euler(0,side*90,text.transform.localPosition.y<0?180:0);material.mainTexture=text.font.material.mainTexture;text.GetComponent<Renderer>().sharedMaterial=material;count++;
        }
        EditorUtility.SetDirty(material);EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();return "Sidewall lettering scaled to ~23cm, outward facing, depth-tested alpha clip: "+count;
    }
    static void NormalMap(Material material)
    {
        const int n=256;var texture=new Texture2D(n,n,TextureFormat.RGB24,true,true);var pixels=new Color[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++){float a=Mathf.PerlinNoise(x*.6f,y*.6f)-.5f,b=Mathf.PerlinNoise((x+71)*.6f,(y+19)*.6f)-.5f;var normal=new Vector3(a*.25f,b*.25f,1).normalized;pixels[y*n+x]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f);}
        texture.SetPixels(pixels);texture.Apply();string path=Folder+"/Asphalt micrograin.png";System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.NormalMap;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=8;importer.SaveAndReimport();material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));material.SetFloat("_BumpScale",.35f);material.EnableKeyword("_NORMALMAP");EditorUtility.SetDirty(material);
    }
    static void UiLine(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
    {var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(pos.x,-pos.y);rect.sizeDelta=size;go.GetComponent<UnityEngine.UI.Image>().color=color;go.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;}
    public static string Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying || scene.path!="Assets/Scenes/OverdriveF1.unity" || scene.isDirty)throw new System.InvalidOperationException("Open saved F1 scene in Edit mode.");
        if(GameObject.Find("Precision Detail Polish"))return "Detail polish already present.";
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/OverdriveF1_BeforeDetailPolish.unity",true);
        var cars=Object.FindObjectsByType<OverdriveVehicle>(FindObjectsSortMode.InstanceID);var before=cars.ToDictionary(c=>c,c=>EditorJsonUtility.ToJson(c));int colliders=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
        var road=GameObject.Find("Refined Circuit").transform.Find("Asphalt Surface").GetComponent<MeshFilter>();var roadMesh=road.sharedMesh;var race=Object.FindFirstObjectByType<RaceManager>();var path=race.ai.path.points;
        System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        carbon=Mat("Machined carbon",new Color(.025f,.032f,.04f),.18f,.33f);metal=Mat("Fastener titanium",new Color(.36f,.41f,.46f),.8f,.58f);rubber=Mat("Panel gaskets",new Color(.009f,.012f,.015f),0,.13f);led=Mat("LED lens",new Color(.7f,.012f,.015f),.1f,.7f);led.EnableKeyword("_EMISSION");led.SetColor("_EmissionColor",new Color(.9f,.015f,.02f)*1.6f);
        var prototype=CarPrototype(cars[0].transform);for(int i=1;i<cars.Length;i++){var clone=Object.Instantiate(prototype.gameObject,cars[i].transform,false);clone.name=prototype.name;}TyreLabels(race.player);
        var roadRenderer=road.GetComponent<Renderer>();var originalAsphalt=roadRenderer.sharedMaterial;var detailedAsphalt=new Material(originalAsphalt){name="Asphalt micrograin"};AssetDatabase.CreateAsset(detailedAsphalt,Folder+"/Asphalt micrograin.mat");NormalMap(detailedAsphalt);roadRenderer.sharedMaterials=roadRenderer.sharedMaterials.Select(m=>m==originalAsphalt?detailedAsphalt:m).ToArray();
        var root=Group("Precision Detail Polish",null);
        // Roof equipment and service conduits stay outside the racing surface.
        var garages=GameObject.Find("F1 Circuit - Apex Park").GetComponentsInChildren<Transform>().Where(t=>t.name=="Pit garage").ToArray();
        foreach(var garage in garages)
        {
            var plant=Group("Garage rooftop mechanical plant",root);plant.SetPositionAndRotation(garage.position+Vector3.up*(garage.lossyScale.y*.5f+.5f),garage.rotation);
            for(int unit=0;unit<2;unit++)
            {
                var p=new Vector3(0,0,-3+unit*6);Piece(plant,"Ventilation enclosure",p,new Vector3(2.4f,.8f,2.7f),metal);
                for(int fan=0;fan<2;fan++){var f=p+new Vector3(0,.43f,-.6f+fan*1.2f);Piece(plant,"Fan guard rim",f,new Vector3(.95f,.035f,.95f),rubber,PrimitiveType.Cylinder);Piece(plant,"Fan motor",f+Vector3.up*.055f,new Vector3(.20f,.06f,.20f),metal,PrimitiveType.Cylinder);for(int slat=0;slat<7;slat++)Piece(plant,"Fan safety grille",f+new Vector3(-.36f+slat*.12f,.045f,0),new Vector3(.016f,.018f,.80f),metal);}
                for(int slat=0;slat<9;slat++)Piece(plant,"Plant ventilation louvre",p+new Vector3(1.205f,-.25f+slat*.06f,0),new Vector3(.015f,.017f,2.35f),rubber);
            }
        }
        Batch(root,"Facility mechanical detail");
        var ui=Object.FindFirstObjectByType<RaceUI>();var dash=ui.hud.transform.Find("Driver dashboard");var border=new Color(.34f,.48f,.56f,.4f);
        UiLine(dash,"Precision gear divider",new Vector2(125,60),new Vector2(1,105),border);UiLine(dash,"Precision telemetry divider",new Vector2(20,222),new Vector2(380,1),border);
        for(int i=0;i<3;i++)for(int tick=0;tick<=10;tick++)UiLine(dash,"Telemetry scale tick",new Vector2(108+tick*29.2f,239+i*20),new Vector2(1,3),new Color(.4f,.56f,.6f,.45f));
        var tower=ui.hud.transform.Find("Timing tower backing");if(tower){UiLine(tower,"Timing tower top rail",Vector2.zero,new Vector2(320,2),new Color(.9f,.12f,.16f));UiLine(tower,"Timing tower lower rail",new Vector2(0,548),new Vector2(320,1),border);}
        bool preserved=cars.All(c=>before[c]==EditorJsonUtility.ToJson(c));int after=Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length;
        var result=new{cars=cars.Length,garages=garages.Length,physicsSerializationUnchanged=preserved,colliderCountBefore=colliders,colliderCountAfter=after,roadMeshUnchanged=road.sharedMesh==roadMesh,pathReferencesUnchanged=race.ai.path.points.SequenceEqual(path),newDecorativeColliders=root.GetComponentsInChildren<Collider>().Length+prototype.GetComponentsInChildren<Collider>().Length,sharedCarDetailMeshes=true};
        if(!preserved || colliders!=after || road.sharedMesh!=roadMesh)throw new System.InvalidOperationException("Visual-only preservation check failed.");
        RefineTyreLabels();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();string report=Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented);System.IO.File.WriteAllText("Docs/DetailPolish_Validation.json",report);return report;
    }
    public static string Preview()
    {
        var car=Object.FindFirstObjectByType<RaceManager>().player.transform;var go=new GameObject("Temporary detail preview");var camera=go.AddComponent<Camera>();camera.fieldOfView=38;camera.farClipPlane=2000;camera.transform.position=car.TransformPoint(new Vector3(4.8f,2.25f,-6));camera.transform.LookAt(car.position+Vector3.up*.3f);
        var rt=RenderTexture.GetTemporary(1600,1000,24);var previous=RenderTexture.active;try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();System.IO.File.WriteAllBytes("Docs/DetailPolish_Car.png",tex.EncodeToPNG());Object.DestroyImmediate(tex);}finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(go);}return "Docs/DetailPolish_Car.png";
    }
}
