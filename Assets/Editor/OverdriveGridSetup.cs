using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using TMPro;
using Overdrive;

public static class OverdriveGridSetup
{
    public static string AlignGridToRoad()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play.");
        var race=Object.FindFirstObjectByType<RaceManager>();var points=race.ai.path.points;int misses=0;
        foreach(var identity in Object.FindObjectsByType<RacerIdentity>(FindObjectsSortMode.None))
        {
            int slot=identity.gridSlot;float distance=slot/2*12+(slot%2)*3-3;float side=slot%2==0?-3:3;int next=0,previous=points.Length-1;
            Vector3 position;Quaternion rotation;
            if(distance<=0){position=points[0].position-points[0].forward*distance;rotation=points[0].rotation;identity.gridCheckpoint=0;identity.startsBeforeLine=false;}
            else
            {
                while(distance>Vector3.Distance(points[next].position,points[previous].position)){distance-=Vector3.Distance(points[next].position,points[previous].position);next=previous;previous=(previous+points.Length-1)%points.Length;}
                position=Vector3.Lerp(points[next].position,points[previous].position,distance/Vector3.Distance(points[next].position,points[previous].position));rotation=Quaternion.LookRotation(points[next].position-points[previous].position,Vector3.up);identity.gridCheckpoint=previous;identity.startsBeforeLine=true;
            }
            identity.gridRotation=rotation;identity.gridPosition=position+rotation*Vector3.right*side+Vector3.up*.7f;identity.transform.SetPositionAndRotation(identity.gridPosition,rotation);
            bool road=false;foreach(var hit in Physics.RaycastAll(identity.gridPosition+Vector3.up*5,Vector3.down,15,~0,QueryTriggerInteraction.Ignore))if(hit.collider.name=="Asphalt Surface")road=true;if(!road)misses++;
            EditorUtility.SetDirty(identity);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());return "20 grid poses follow actual road; ray misses="+misses+"; first start-line crossing excluded for cars behind line.";
    }
    const string Folder="Assets/Materials/GrandPrixGrid";
    static readonly string[] Teams={"APEX","ROSSO","PAPAYA","AURORA","EMERALD","ALPINE BLUE","VOLT","POLAR","SAKURA","GOLDLINE"};
    static readonly Color[] Colors={new Color(.02f,.7f,.66f),new Color(.85f,.025f,.045f),new Color(1,.34f,.015f),new Color(.08f,.15f,.65f),new Color(.015f,.4f,.21f),new Color(.08f,.51f,.94f),new Color(.55f,.95f,.035f),new Color(.86f,.88f,.91f),new Color(.92f,.16f,.48f),new Color(.9f,.64f,.12f)};
    static void Batch(Transform root,string path)
    {
        var groups=new Dictionary<Material,List<CombineInstance>>();var renderers=new List<Renderer>();
        foreach(var f in root.GetComponentsInChildren<MeshFilter>())
        {
            var r=f.GetComponent<MeshRenderer>();if(!r || !r.enabled || f.GetComponent<TextMesh>() || !f.sharedMesh || f.sharedMesh.subMeshCount!=1)continue;
            var m=r.sharedMaterial;if(!m)continue;if(!groups.ContainsKey(m))groups[m]=new List<CombineInstance>();
            groups[m].Add(new CombineInstance{mesh=f.sharedMesh,transform=root.worldToLocalMatrix*f.transform.localToWorldMatrix});renderers.Add(r);
        }
        foreach(var pair in groups)
        {
            var mesh=new Mesh{name=root.name+" "+pair.Key.name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(path+".asset"));var go=new GameObject("Grid render "+pair.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=pair.Key;
        }
        foreach(var r in renderers)r.enabled=false;
    }
    static TMP_Text Label(Transform parent,string name,Vector2 pos,Vector2 size,float fontSize,TMP_FontAsset font)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(pos.x,-pos.y);r.sizeDelta=size;
        var t=go.GetComponent<TextMeshProUGUI>();t.font=font;t.fontSize=fontSize;t.color=Color.white;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;return t;
    }
    public static string Apply()
    {
        var scene=EditorSceneManager.GetActiveScene();if(EditorApplication.isPlaying || scene.path!="Assets/Scenes/OverdriveF1.unity" || scene.isDirty)throw new System.InvalidOperationException("Open saved F1 scene and stop Play.");
        if(GameObject.Find("Grand Prix 20 Car Grid"))return "Grid already exists.";
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/OverdriveF1_Before20CarGrid.unity",true);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Materials","GrandPrixGrid");
        if(!AssetDatabase.IsValidFolder("Assets/Meshes/GrandPrixGrid"))AssetDatabase.CreateFolder("Assets/Meshes","GrandPrixGrid");
        var race=Object.FindFirstObjectByType<RaceManager>();var ui=Object.FindFirstObjectByType<RaceUI>();var root=new GameObject("Grand Prix 20 Car Grid").transform;
        var opponents=new List<AIDriver>();var cars=new List<OverdriveVehicle>{race.player,race.opponent};
        for(int i=2;i<20;i++){var go=Object.Instantiate(race.opponent.gameObject);go.transform.SetParent(root,false);cars.Add(go.GetComponent<OverdriveVehicle>());}
        var start=race.ai.path.points[0];
        for(int i=0;i<cars.Count;i++)
        {
            var car=cars[i];int team=i/2;int slot=i==0?9:i<=9?i-1:i;int number=i==0?27:10+team*7+i%2;
            car.name=i==0?"Overdrive Player":Teams[team]+" #"+number;
            var identity=car.GetComponent<RacerIdentity>();if(!identity)identity=car.gameObject.AddComponent<RacerIdentity>();identity.teamName=Teams[team];identity.driverCode=i==0?"YOU":Teams[team].Substring(0,3)+(i%2+1);identity.carNumber=number;identity.teamColor=Colors[team];identity.gridSlot=slot;
            int row=slot/2;float side=slot%2==0?-3:3;car.transform.SetPositionAndRotation(start.position+start.forward*(3-row*12-(slot%2)*3)+start.right*side+Vector3.up*.7f,start.rotation);
            identity.gridPosition=car.transform.position;identity.gridRotation=car.transform.rotation;
            var paint=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+Teams[team]+".mat");
            if(!paint){paint=new Material(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial);paint.color=Colors[team];paint.SetFloat("_Metallic",.22f);paint.SetFloat("_Smoothness",.65f);AssetDatabase.CreateAsset(paint,Folder+"/"+Teams[team]+".mat");}
            foreach(var renderer in car.GetComponentsInChildren<Renderer>(true))if(renderer.sharedMaterial && renderer.sharedMaterial.name.StartsWith("Team "))renderer.sharedMaterial=paint;
            var body=car.transform.Find("Grand Prix Bodywork");if(body)
            {
                foreach(var label in body.GetComponentsInChildren<TextMesh>()){if(int.TryParse(label.text,out _))label.text=number.ToString();else label.text=Teams[team];}
                Batch(body,"Assets/Meshes/GrandPrixGrid/Car "+i+" Body");
            }
            for(int w=0;w<car.visuals.Length;w++)Batch(car.visuals[w],"Assets/Meshes/GrandPrixGrid/Car "+i+" Wheel "+w);
            var driver=car.GetComponent<AIDriver>();if(driver){driver.preferredLane=side*.75f;driver.pace=.94f+((i*7)%9)*.0075f;opponents.Add(driver);}
        }
        race.gridOpponents=opponents.ToArray();
        var broadcast=ui.gameObject.AddComponent<GrandPrixBroadcast>();broadcast.race=race;
        var tower=Label(ui.hud.transform,"Grand Prix timing tower",new Vector2(48,192),new Vector2(300,540),17,ui.standings.font);tower.lineSpacing=3;tower.GetComponent<RectTransform>().sizeDelta=new Vector2(300,540);
        var backing=new GameObject("Timing tower backing",typeof(RectTransform),typeof(UnityEngine.UI.Image));backing.transform.SetParent(tower.transform.parent,false);var rect=backing.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(36,-184);rect.sizeDelta=new Vector2(320,550);backing.GetComponent<UnityEngine.UI.Image>().color=new Color(.015f,.025f,.04f,.88f);backing.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;tower.transform.SetAsLastSibling();broadcast.timingTower=tower;
        broadcast.classification=Label(ui.results.transform,"Full field classification",new Vector2(650,130),new Vector2(380,470),16,ui.standings.font);
        ui.resultText.rectTransform.sizeDelta=new Vector2(580,440);broadcast.lights=new UnityEngine.UI.Image[5];
        foreach(Transform child in ui.countdown.transform)child.gameObject.SetActive(false);
        for(int i=0;i<5;i++)
        {
            var go=new GameObject("Start light "+(i+1),typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(ui.countdown.transform,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2((i-2)*86,0);r.sizeDelta=new Vector2(68,68);var image=go.GetComponent<UnityEngine.UI.Image>();image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");image.raycastTarget=false;broadcast.lights[i]=image;
        }
        foreach(var t in ui.menu.GetComponentsInChildren<TMP_Text>(true)){t.text=t.text.Replace("AI DUEL","20 CAR GRID").Replace("LOCAL RACING EXPERIENCE","GRAND PRIX EXPERIENCE");}
        race.normal.maximumSpeedKmh=260;race.hard.maximumSpeedKmh=300;EditorUtility.SetDirty(race.normal);EditorUtility.SetDirty(race.hard);
        EditorUtility.SetDirty(race);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "20 cars / 10 original teams; player P10; 19 physical AI; five start lights; field classification, team minimap and render batches saved.";
    }
}
