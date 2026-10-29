using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Overdrive;
public static class OverdriveUIStyle
{
    static readonly Color Ink=new Color(.025f,.042f,.065f,.94f),Cyan=new Color(.25f,.92f,.82f),Grey=new Color(.53f,.63f,.71f);
    static TMP_FontAsset font;
    static RectTransform Box(string name,Transform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color tint)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);Place(r,anchor,pos,size);
        var image=go.GetComponent<UnityEngine.UI.Image>();image.color=tint;image.raycastTarget=false;return r;
    }
    static void Place(RectTransform r,Vector2 anchor,Vector2 pos,Vector2 size)
    {r.anchorMin=r.anchorMax=r.pivot=anchor;r.anchoredPosition=pos;r.sizeDelta=size;}
    static void At(RectTransform r,float x,float y,float w,float h)=>Place(r,new Vector2(0,1),new Vector2(x,-y),new Vector2(w,h));
    static TMP_Text Label(string name,Transform parent,string value,float x,float y,float w,float h,float size,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TextMeshProUGUI>();
        At(t.rectTransform,x,y,w,h);t.font=font;t.text=value;t.fontSize=size;t.color=color;t.alignment=TextAlignmentOptions.TopLeft;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;return t;
    }
    static void Text(TMP_Text t,float x,float y,float w,float h,float size,TextAlignmentOptions align=TextAlignmentOptions.TopLeft)
    {At(t.rectTransform,x,y,w,h);t.fontSize=size;t.alignment=align;t.color=Color.white;t.textWrappingMode=TextWrappingModes.NoWrap;t.raycastTarget=false;}
    static void Button(UnityEngine.UI.Button b,float x,float y,float w,float h,string value,bool primary=false)
    {
        At(b.GetComponent<RectTransform>(),x,y,w,h);b.GetComponent<UnityEngine.UI.Image>().color=primary?Cyan:new Color(.06f,.09f,.13f);
        var t=b.GetComponentInChildren<TMP_Text>();Place(t.rectTransform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(w-32,h));t.text=value;t.fontSize=25;t.fontStyle=FontStyles.Bold;t.color=primary?Ink:Color.white;t.alignment=TextAlignmentOptions.Center;t.textWrappingMode=TextWrappingModes.NoWrap;
        var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.85f,1,1);colors.selectedColor=new Color(.85f,1,1);colors.pressedColor=new Color(.55f,.7f,.72f);b.colors=colors;
        var outline=b.GetComponent<UnityEngine.UI.Outline>();if(!outline)outline=b.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=new Color(.26f,.6f,.61f,.35f);outline.effectDistance=new Vector2(1,-1);
    }
    static CircuitMapGraphic Map(Transform parent,RaceManager race,float x,float y,float w,float h)
    {
        var go=new GameObject("Circuit map",typeof(RectTransform),typeof(CanvasRenderer),typeof(CircuitMapGraphic));go.transform.SetParent(parent,false);At(go.GetComponent<RectTransform>(),x,y,w,h);
        var map=go.GetComponent<CircuitMapGraphic>();map.race=race;map.color=new Color(.56f,.73f,.8f,.9f);map.raycastTarget=false;return map;
    }
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first.");
        var ui=Object.FindFirstObjectByType<RaceUI>();
        var canvas=ui.GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=Camera.main;canvas.planeDistance=1;
        if(ui.transform.Find("Racing Theme Applied"))return "Theme already applied.";
        font=ui.standings.font;var white=new Color(.94f,.97f,1);var menu=ui.menu.GetComponent<RectTransform>();
        menu.anchorMin=Vector2.zero;menu.anchorMax=Vector2.one;menu.offsetMin=menu.offsetMax=Vector2.zero;ui.menu.GetComponent<UnityEngine.UI.Image>().color=new Color(.018f,.03f,.05f,1);
        var title=ui.menu.transform.Find("Title").GetComponent<TMP_Text>();Text(title,96,142,900,250,106);title.fontStyle=FontStyles.Bold|FontStyles.Italic;title.characterSpacing=-3;title.lineSpacing=-20;
        Label("Edition",menu,"FORMULA RACING  /  2025",100,92,800,30,19,Cyan).characterSpacing=4;
        var sub=ui.menu.transform.Find("Subtitle").GetComponent<TMP_Text>();Text(sub,102,412,800,40,25);sub.text="APEX PARK GRAND PRIX";sub.color=Grey;sub.characterSpacing=3;
        Box("Title accent",menu,new Vector2(0,1),new Vector2(100,-470),new Vector2(620,3),Cyan);
        Label("Difficulty label",menu,"SELECT DIFFICULTY",100,502,600,30,14,Grey).characterSpacing=3;
        Button(ui.normalButton,100,542,300,68,"NORMAL");Button(ui.hardButton,420,542,300,68,"HARD");
        Label("Specification",menu,"2 LAPS    /    RWD    /    MANUAL 8-SPEED",100,632,750,30,17,Grey);
        Button(ui.startButton,100,687,620,84,"START RACE   >",true);
        Label("Keybind label",menu,"DRIVER CONTROLS",100,812,750,25,13,Cyan).characterSpacing=3;
        var controls=ui.menu.transform.Find("Controls").GetComponent<TMP_Text>();Text(controls,100,854,750,95,20);controls.color=Grey;controls.text="W 가속   S 제동   A/D 조향   Q/E 기어 -/+\nR 차량 복구   Esc 일시정지";
        Button(ui.quitButton,100,970,200,46,"QUIT");
        Box("Hero panel",menu,new Vector2(0,1),new Vector2(1010,-165),new Vector2(810,660),new Color(.035f,.06f,.085f));
        Label("Circuit index",menu,"01  /  APEX PARK",1050,205,740,42,27,white).fontStyle=FontStyles.Bold;
        Label("Circuit note",menu,"GRAND PRIX CIRCUIT",1050,257,740,30,15,Grey).characterSpacing=4;
        Map(menu,ui.race,1040,315,750,420).showRacers=false;
        float circuitLength=0;var circuitPoints=ui.race.ai.path.points;for(int i=0;i<circuitPoints.Length;i++)circuitLength+=Vector3.Distance(circuitPoints[i].position,circuitPoints[(i+1)%circuitPoints.Length].position);
        Label("Circuit specifications",menu,(circuitLength/1000).ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+" KM      2 LAPS      AI DUEL",1050,770,740,30,18,Cyan);
        Box("Footer line",menu,new Vector2(0,1),new Vector2(100,-1030),new Vector2(1720,1),new Color(.15f,.23f,.29f));
        Label("Footer",menu,"PROJECT OVERDRIVE                                      LOCAL RACING EXPERIENCE",350,976,1440,30,13,Grey);
        var hud=ui.hud.transform;
        var pos=Box("Position card",hud,new Vector2(0,1),new Vector2(48,-40),new Vector2(300,132),Ink);pos.SetAsFirstSibling();
        Box("Position accent",pos,new Vector2(0,1),Vector2.zero,new Vector2(4,132),Cyan);ui.standings.transform.SetParent(pos,false);Text(ui.standings,22,14,265,110,32);
        var clock=Box("Timing card",hud,Vector2.one,new Vector2(-48,-40),new Vector2(340,132),Ink);clock.SetAsFirstSibling();ui.clock.transform.SetParent(clock,false);Text(ui.clock,20,14,300,110,32,TextAlignmentOptions.TopRight);
        var mapCard=Box("Circuit card",hud,Vector2.zero,new Vector2(48,48),new Vector2(330,265),Ink);mapCard.SetAsFirstSibling();
        Label("Circuit heading",mapCard,"APEX PARK",20,17,290,25,16,white).characterSpacing=3;Map(mapCard,ui.race,20,48,290,180);
        Label("Player legend",mapCard,"YOU",20,238,100,20,12,Cyan);Label("AI legend",mapCard,"AI",100,238,100,20,12,new Color(1,.44f,.24f));
        var dash=Box("Driver dashboard",hud,new Vector2(1,0),new Vector2(-48,48),new Vector2(420,290),Ink);dash.SetAsFirstSibling();
        var live=ui.hud.AddComponent<OverdriveRacingHUD>();live.race=ui.race;live.revLights=new UnityEngine.UI.Image[12];
        for(int i=0;i<12;i++)live.revLights[i]=Box("Rev light "+i,dash,new Vector2(0,1),new Vector2(20+i*32,-18),new Vector2(25,7),Grey).GetComponent<UnityEngine.UI.Image>();
        Label("Gear caption",dash,"GEAR",24,43,100,20,12,Grey);live.gear=Label("Gear",dash,"1",20,57,106,110,96,white);live.gear.fontStyle=FontStyles.Bold;
        ui.speed.transform.SetParent(dash,false);Text(ui.speed,136,64,258,92,78,TextAlignmentOptions.TopRight);ui.speed.fontStyle=FontStyles.Bold;
        Label("Speed unit",dash,"KM/H",346,146,54,22,12,Grey);live.rpm=Label("RPM",dash,"04000 RPM / MANUAL",140,172,260,24,16,Grey);
        live.warning=Label("Grip warning",dash,"",20,200,380,24,15,new Color(1,.71f,.34f));
        live.energy=Label("Energy",dash,"ERS 100%",20,229,95,20,12,Cyan);
        Label("Throttle caption",dash,"THROTTLE",20,249,95,20,11,Grey);Label("Brake caption",dash,"BRAKE",20,269,95,20,11,Grey);
        foreach(int y in new[]{235,255,275})Box("Bar backing",dash,new Vector2(0,1),new Vector2(108,-y),new Vector2(292,4),new Color(.12f,.18f,.23f));
        live.energyBar=Box("ERS bar",dash,new Vector2(0,1),new Vector2(108,-235),new Vector2(292,4),Cyan);
        live.throttleBar=Box("Throttle bar",dash,new Vector2(0,1),new Vector2(108,-255),new Vector2(292,4),new Color(.35f,.88f,.55f));
        live.brakeBar=Box("Brake bar",dash,new Vector2(0,1),new Vector2(108,-275),new Vector2(292,4),new Color(1,.3f,.28f));
        var guide=ui.hud.transform.Find("Driving Guide").GetComponent<TMP_Text>();Place(guide.rectTransform,new Vector2(.5f,0),new Vector2(0,28),new Vector2(840,24));guide.fontSize=15;guide.color=Grey;guide.alignment=TextAlignmentOptions.Center;guide.text="W / SPACE  THROTTLE / BRAKE     A / D  STEER     AUTO GEARS     R  RESET     ESC  PAUSE";
        var eventTitle=Label("Event title",hud,"APEX PARK  /  GRAND PRIX",0,0,800,28,16,white);Place(eventTitle.rectTransform,new Vector2(.5f,1),new Vector2(0,-44),new Vector2(800,28));eventTitle.alignment=TextAlignmentOptions.Center;eventTitle.characterSpacing=4;
        Box("Countdown plate",ui.countdown.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(310,230),Ink).SetAsFirstSibling();ui.countdownText.color=Cyan;ui.countdownText.fontStyle=FontStyles.Bold|FontStyles.Italic;
        var ready=Label("Countdown caption",ui.countdown.transform,"LIGHTS OUT",0,0,300,25,15,Grey);Place(ready.rectTransform,new Vector2(.5f,.5f),new Vector2(0,-88),new Vector2(300,25));ready.alignment=TextAlignmentOptions.Center;
        ui.pause.GetComponent<UnityEngine.UI.Image>().color=Ink;
        var pauseTitle=ui.pause.transform.Find("Title").GetComponent<TMP_Text>();Text(pauseTitle,56,54,688,90,58);pauseTitle.text="PAUSED";pauseTitle.fontStyle=FontStyles.Bold|FontStyles.Italic;
        Label("Pause caption",ui.pause.transform,"일시정지",56,150,688,35,22,Grey);
        Button(ui.resumeButton,56,220,688,76,"RESUME   >",true);Button(ui.restartButton,56,320,688,76,"RESTART");Button(ui.pauseMenuButton,56,420,688,76,"MAIN MENU");
        ui.results.GetComponent<UnityEngine.UI.Image>().color=Ink;Place(ui.results.GetComponent<RectTransform>(),new Vector2(.5f,.5f),Vector2.zero,new Vector2(1080,820));
        Label("Result heading",ui.results.transform,"RACE CLASSIFICATION",64,48,952,35,17,Cyan).characterSpacing=4;
        Text(ui.resultText,64,118,952,440,34);Button(ui.retryButton,64,636,450,80,"RACE AGAIN   >",true);Button(ui.resultMenuButton,554,636,462,80,"MAIN MENU");
        Label("Result footer",ui.results.transform,"APEX PARK GRAND PRIX    /    2 LAPS",64,758,952,30,13,Grey);
        ui.racingTheme=true;new GameObject("Racing Theme Applied").transform.SetParent(ui.transform,false);
        EditorUtility.SetDirty(ui);EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return "Racing theme applied to existing menu/HUD/pause/results; existing button actions and race references retained.";
    }
}
