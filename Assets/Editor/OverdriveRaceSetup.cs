using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using Overdrive;

public static class OverdriveRaceSetup
{
    static TMP_FontAsset font;
    static readonly Color Ink=new Color(.035f,.06f,.09f,.96f);
    static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform)); var r=go.GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin=r.anchorMax=r.pivot=anchor; r.anchoredPosition=position; r.sizeDelta=size; return r;
    }
    static GameObject Panel(string name,Transform parent,Vector2 size)
    {
        var r=Rect(name,parent,new Vector2(.5f,.5f),Vector2.zero,size);
        r.gameObject.AddComponent<UnityEngine.UI.Image>().color=Ink; return r.gameObject;
    }
    static TMP_Text Label(string name,Transform parent,string value,Vector2 anchor,Vector2 position,Vector2 size,int fontSize=32,TextAlignmentOptions align=TextAlignmentOptions.Center)
    {
        var r=Rect(name,parent,anchor,position,size); var t=r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font=font; t.text=value; t.fontSize=fontSize; t.color=new Color(.94f,.97f,1); t.alignment=align;
        t.raycastTarget=false; t.textWrappingMode=TextWrappingModes.Normal; return t;
    }
    static UnityEngine.UI.Button Button(string name,Transform parent,string label,Vector2 p,Vector2 size)
    {
        var r=Rect(name,parent,new Vector2(.5f,.5f),p,size); var image=r.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color=new Color(.12f,.17f,.23f);
        var button=r.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=image;
        var colors=button.colors; colors.highlightedColor=new Color(.7f,.9f,1); colors.pressedColor=new Color(.4f,.7f,.9f); button.colors=colors;
        Label("Label",r,label,new Vector2(.5f,.5f),Vector2.zero,size,30); return button;
    }
    static TMP_FontAsset FontAsset()
    {
        const string file="Assets/Settings/OverdriveKorean.asset";
        var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(file); if(asset) return asset;
        asset=TMP_FontAsset.CreateFontAsset("Malgun Gothic","Regular");
        if(!asset) throw new System.InvalidOperationException("Malgun Gothic OS font required for Korean UI baking.");
        asset.name="Overdrive Korean";
        string chars=""; for(int i=32;i<127;i++) chars+=(char)i;
        chars+="시작종료난이도선택조작가속제동후진향차량복구일정지계속메인뉴재도전순위경과현재랩기어승리패배총시간베스트대비미완주시점준드라이버를겨라 ·—";
        if(!asset.TryAddCharacters(chars,out string missing)) throw new System.InvalidOperationException("Missing UI font glyphs: "+missing);
        // Bake only needed glyphs; no system font file is copied into the project.
        asset.atlasPopulationMode=AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(asset,file);
        foreach(var texture in asset.atlasTextures) AssetDatabase.AddObjectToAsset(texture,asset);
        AssetDatabase.AddObjectToAsset(asset.material,asset); EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets(); return asset;
    }
    [MenuItem("Overdrive/Configure P3 Race")]
    public static string Configure()
    {
        var scene=EditorSceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.isDirty || scene.path!="Assets/Scenes/OverdriveCircuit.unity")
            throw new System.InvalidOperationException("Open saved circuit and stop Play first.");
        if(Object.FindFirstObjectByType<RaceManager>()) return "Existing P3 race preserved.";
        font=FontAsset();
        var player=Object.FindFirstObjectByType<PlayerVehicleInput>(); var ai=Object.FindFirstObjectByType<AIDriver>();
        var race=new GameObject("Race Manager").AddComponent<RaceManager>(); race.player=player.GetComponent<OverdriveVehicle>(); race.opponent=ai.GetComponent<OverdriveVehicle>(); race.ai=ai;
        race.normal=AssetDatabase.LoadAssetAtPath<AIDifficulty>("Assets/Settings/AI_Normal.asset"); race.hard=AssetDatabase.LoadAssetAtPath<AIDifficulty>("Assets/Settings/AI_Hard.asset");
        var canvas=new GameObject("Overdrive UI",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        var ui=canvas.AddComponent<RaceUI>();
        ui.menu=Panel("Main Menu",canvas.transform,new Vector2(1000,850));
        Label("Title",ui.menu.transform,"PROJECT\nOVERDRIVE",new Vector2(.5f,1),new Vector2(0,-65),new Vector2(900,180),70);
        Label("Subtitle",ui.menu.transform,"AI 드라이버를 이겨라  /  2 LAPS",new Vector2(.5f,1),new Vector2(0,-255),new Vector2(900,60),30);
        ui.normalButton=Button("Normal",ui.menu.transform,"NORMAL",new Vector2(-190,60),new Vector2(340,70));
        ui.hardButton=Button("Hard",ui.menu.transform,"HARD",new Vector2(190,60),new Vector2(340,70));
        ui.startButton=Button("Start",ui.menu.transform,"시작 / START",new Vector2(0,-45),new Vector2(720,80));
        Label("Controls",ui.menu.transform,"W 가속 · S 제동 · A/D 조향\nR 차량 복구 · Esc 일시정지",new Vector2(.5f,.5f),new Vector2(0,-175),new Vector2(900,120),30);
        ui.quitButton=Button("Quit",ui.menu.transform,"종료 / QUIT",new Vector2(0,-335),new Vector2(350,65));
        ui.hud=Rect("HUD",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1920,1080)).gameObject;
        var hudRect=ui.hud.GetComponent<RectTransform>(); hudRect.anchorMin=Vector2.zero; hudRect.anchorMax=Vector2.one; hudRect.sizeDelta=Vector2.zero;
        ui.standings=Label("Standings",ui.hud.transform,"",new Vector2(0,1),new Vector2(45,-35),new Vector2(340,130),42,TextAlignmentOptions.TopLeft);
        ui.clock=Label("Clock",ui.hud.transform,"",Vector2.one,new Vector2(-45,-35),new Vector2(500,130),36,TextAlignmentOptions.TopRight);
        ui.speed=Label("Speed",ui.hud.transform,"",new Vector2(1,0),new Vector2(-45,35),new Vector2(360,130),48,TextAlignmentOptions.BottomRight);
        Label("Driving Guide",ui.hud.transform,"W/S 가속·제동   A/D 조향\nR 복구   Esc 일시정지",Vector2.zero,new Vector2(45,35),new Vector2(650,90),27,TextAlignmentOptions.BottomLeft);
        ui.countdown=Rect("Countdown",canvas.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(650,250)).gameObject;
        ui.countdownText=Label("Number",ui.countdown.transform,"3",new Vector2(.5f,.5f),Vector2.zero,new Vector2(650,250),160);
        ui.pause=Panel("Pause",canvas.transform,new Vector2(800,600));
        Label("Title",ui.pause.transform,"일시정지 / PAUSED",new Vector2(.5f,1),new Vector2(0,-55),new Vector2(700,90),48);
        ui.resumeButton=Button("Resume",ui.pause.transform,"계속 / RESUME",new Vector2(0,65),new Vector2(600,80));
        ui.restartButton=Button("Restart",ui.pause.transform,"재도전 / RESTART",new Vector2(0,-55),new Vector2(600,80));
        ui.pauseMenuButton=Button("Menu",ui.pause.transform,"메인 메뉴",new Vector2(0,-175),new Vector2(600,80));
        ui.results=Panel("Results",canvas.transform,new Vector2(1000,800));
        ui.resultText=Label("Result",ui.results.transform,"",new Vector2(.5f,1),new Vector2(0,-65),new Vector2(900,430),42);
        ui.retryButton=Button("Retry",ui.results.transform,"재도전 / RETRY",new Vector2(-210,-255),new Vector2(370,85));
        ui.resultMenuButton=Button("Menu",ui.results.transform,"메인 메뉴",new Vector2(210,-255),new Vector2(370,85));
        ui.hud.SetActive(false); ui.pause.SetActive(false); ui.results.SetActive(false); ui.countdown.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(canvas,"Assets/Prefabs/OverdriveRaceUI.prefab"); ui.race=race;
        if(!Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>())
        {
            var events=new GameObject("UI EventSystem",typeof(UnityEngine.EventSystems.EventSystem));
            events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        return "P3 race manager, Korean TMP UI, menu/HUD/pause/results and UI input connected.";
    }
}
