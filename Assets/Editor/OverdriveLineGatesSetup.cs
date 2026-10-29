using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Overdrive;
using Object=UnityEngine.Object;

public static class OverdriveLineGatesSetup
{
    static Material Material()
    {
        const string folder="Assets/Materials/QualifyingGates";
        if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Materials","QualifyingGates");
        string path=folder+"/Purple transparent.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,path);}
        material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",5);material.SetFloat("_DstBlend",10);
        material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);material.SetColor("_BaseColor",new Color(.7f,.27f,1,.15f));
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;material.enableInstancing=true;EditorUtility.SetDirty(material);return material;
    }
    static Renderer Shape(string name,Transform parent,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;return r;
    }
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var existing=parent.Find(name);var rect=existing?(RectTransform)existing:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);return rect;
    }
    static TMP_Text Label(string name,Transform parent,TMP_FontAsset font,float x,float y,float w,float h,float size)
    {
        var rect=Rect(name,parent,x,y,w,h);var text=rect.GetComponent<TMP_Text>();if(!text)text=rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font;text.fontSize=size;text.color=Color.white;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;return text;
    }
    [MenuItem("Overdrive/Add Purple Qualifying Line Gates")]
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
        var ui=Object.FindFirstObjectByType<RaceUI>();if(!ui || !ui.race.ai.path.Valid)throw new System.InvalidOperationException("Open OverdriveF1 scene");
        var host=GameObject.Find("Qualifying Line Gates");if(!host)host=new GameObject("Qualifying Line Gates");
        var system=host.GetComponent<QualifyingLineGates>();if(!system)system=host.AddComponent<QualifyingLineGates>();system.race=ui.race;
        for(int i=host.transform.childCount-1;i>=0;i--)Object.DestroyImmediate(host.transform.GetChild(i).gameObject);
        var material=Material();var path=ui.race.ai.path;var gates=new System.Collections.Generic.List<QualifyingLineGates.Gate>();
        var positions=RacingLinePlanner.Build(path,ui.race.circuit?ui.race.circuit.racingLineHalfWidth:6);
        var lineRoot=new GameObject("Qualifying racing line");lineRoot.transform.SetParent(host.transform,false);
        var line=lineRoot.AddComponent<WaypointPath>();line.points=new Transform[positions.Length];
        for(int i=0;i<positions.Length;i++)
        {
            var point=new GameObject("Line "+i.ToString("000")).transform;point.SetParent(lineRoot.transform,false);
            point.SetPositionAndRotation(positions[i],Quaternion.LookRotation(positions[(i+1)%positions.Length]-positions[(i+positions.Length-1)%positions.Length]));line.points[i]=point;
        }
        var guide=Object.FindFirstObjectByType<DrivingGuide>();guide.qualifyingLine=line;EditorUtility.SetDirty(guide);
        for(int i=12;i<path.points.Length-3;i+=12)
        {
            var root=new GameObject("Line gate "+gates.Count.ToString("00")+" - CP "+i).transform;root.SetParent(host.transform,false);
            root.SetPositionAndRotation(line.Position(i),line.points[i].rotation);
            var panel=Shape("Transparent purple panel",root,PrimitiveType.Quad,new Vector3(0,1.45f,0),new Vector3(5.2f,2.8f,1),material);
            var frames=new System.Collections.Generic.List<Renderer>();
            foreach(float side in new[]{-1f,1f})
            {
                frames.Add(Shape("Outer upright",root,PrimitiveType.Cube,new Vector3(side*2.6f,1.45f,0),new Vector3(.045f,2.8f,.045f),material));
                frames.Add(Shape("Perfect centre marker",root,PrimitiveType.Cube,new Vector3(side*.85f,.40f,0),new Vector3(.035f,.7f,.05f),material));
            }
            frames.Add(Shape("Top edge",root,PrimitiveType.Cube,new Vector3(0,2.85f,0),new Vector3(5.24f,.045f,.045f),material));
            frames.Add(Shape("Ground threshold",root,PrimitiveType.Cube,new Vector3(0,.065f,0),new Vector3(5.2f,.025f,.18f),material));
            frames.Add(Shape("Perfect ground strip",root,PrimitiveType.Cube,new Vector3(0,.075f,0),new Vector3(1.7f,.025f,.55f),material));
            gates.Add(new QualifyingLineGates.Gate {checkpoint=i,visual=root,panels=new[]{panel},frames=frames.ToArray()});root.gameObject.SetActive(false);
        }
        system.gates=gates.ToArray();var font=ui.startLabel.font;
        var card=Rect("Qualifying gate card",ui.hud.transform,650,96,620,82);var bg=card.GetComponent<UnityEngine.UI.Image>();if(!bg)bg=card.gameObject.AddComponent<UnityEngine.UI.Image>();bg.color=new Color(.018f,.012f,.04f,.82f);bg.raycastTarget=false;
        system.hudCard=card.gameObject;
        system.feedback=Label("Gate feedback",card,font,0,2,620,36,26);system.feedback.text="PURPLE LINE GATES";system.feedback.color=new Color(.75f,.4f,1);
        system.summary=Label("Gate score",card,font,0,38,620,22,12);system.summary.text="PRACTICE  00/52    PERFECT 00    GOOD 00    MISS 00";
        var hint=Label("Gate hint",card,font,0,62,620,18,10);hint.text="CENTRE = PERFECT   /   KEEP THE CAR ALIGNED AND MOVING";hint.color=new Color(.65f,.57f,.76f);
        system.resultSummary=Label("Qualifying gate result",ui.results.transform,font,64,440,570,82,15);system.resultSummary.alignment=TextAlignmentOptions.MidlineLeft;system.resultSummary.gameObject.SetActive(false);
        system.hudCard.SetActive(false);
        EditorUtility.SetDirty(system);EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);EditorSceneManager.SaveScene(ui.gameObject.scene);AssetDatabase.SaveAssets();
        return $"Saved {gates.Count} transparent purple line gates, judgement HUD and qualifying result score";
    }
}
