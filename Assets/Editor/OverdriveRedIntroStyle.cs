using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Overdrive;
using Object=UnityEngine.Object;

public static class OverdriveRedIntroStyle
{
    const string Folder="Assets/UI/RedIntro";
    static readonly Color Red=new Color(.91f,.025f,.08f),White=new Color(.96f,.96f,.97f),Grey=new Color(.60f,.60f,.64f);
    static TMP_FontAsset latin,korean;
    static void Place(RectTransform r,float x,float y,float width,float height)
    {r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);}
    static void Fill(RectTransform r)
    {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static Transform Get(string name,Transform parent,System.Type type)
    {var t=parent.Find(name);if(t)return t;var go=new GameObject(name,typeof(RectTransform),type);go.transform.SetParent(parent,false);return go.transform;}
    static TMP_Text Label(string name,Transform parent,string text,float x,float y,float w,float h,float size,Color color,bool bold=false)
    {
        var t=Get(name,parent,typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();Place(t.rectTransform,x,y,w,h);
        t.font=latin;t.text=text;t.fontSize=size;t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;t.color=color;t.raycastTarget=false;t.alignment=TextAlignmentOptions.TopLeft;t.textWrappingMode=TextWrappingModes.NoWrap;t.characterSpacing=0;t.lineSpacing=0;
        return t;
    }
    static UnityEngine.UI.Image Bar(string name,Transform parent,float x,float y,float w,float h,Color color)
    {var t=Get(name,parent,typeof(UnityEngine.UI.Image));Place(t.GetComponent<RectTransform>(),x,y,w,h);var image=t.GetComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=false;return image;}
    static void Button(UnityEngine.UI.Button b,float x,float y,float w,float h,string label,bool primary=false)
    {
        Place(b.GetComponent<RectTransform>(),x,y,w,h);var image=b.GetComponent<UnityEngine.UI.Image>();image.sprite=null;image.color=primary?Red:new Color(.10f,.105f,.12f);image.raycastTarget=true;
        var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1,.87f,.88f);colors.selectedColor=Color.white;colors.pressedColor=new Color(.72f,.72f,.74f);colors.fadeDuration=.16f;b.colors=colors;
        var outline=b.GetComponent<UnityEngine.UI.Outline>();if(outline){outline.effectColor=primary?new Color(1,.06f,.10f,.35f):new Color(.42f,.42f,.46f,.45f);outline.effectDistance=new Vector2(1,-1);}
        var text=b.GetComponentInChildren<TMP_Text>();text.font=latin;text.fontSize=primary?27:20;text.fontStyle=FontStyles.Bold;text.text=label;text.alignment=TextAlignmentOptions.MidlineLeft;text.color=White;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.NoWrap;
        var r=text.rectTransform;Fill(r);r.offsetMin=new Vector2(26,0);r.offsetMax=new Vector2(-22,0);
    }
    static Texture2D Import(string name,Texture2D texture)
    {
        string path=Folder+"/"+name+".png";System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.textureType=TextureImporterType.Default;imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=2048;imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static Texture2D Backdrop()
    {
        const int w=960,h=540;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);var colors=new Color[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            float u=x/(float)w,v=y/(float)h;
            float glow=Mathf.Exp(-((u-.81f)*(u-.81f)*12+(v-.44f)*(v-.44f)*8));
            var c=new Color(.024f,.025f,.033f)+new Color(.14f,.004f,.009f)*glow;
            float diagonal=u+v*.25f;if(diagonal>.82f&&diagonal<1.12f)c+=new Color(.19f,.003f,.012f)*(1-u*.28f);
            if(diagonal>1.13f&&diagonal<1.155f)c+=new Color(.24f,.005f,.02f);
            float lines=(x+y*2)%12==0?.006f:0;c+=new Color(lines,lines*.4f,lines*.4f);colors[y*w+x]=c;
        }
        tex.SetPixels(colors);tex.Apply();return Import("Red title backdrop",tex);
    }
    static Texture2D Hero()
    {
        var race=Object.FindFirstObjectByType<RaceManager>();var car=race.player;
        foreach(var id in Object.FindObjectsByType<RacerIdentity>(FindObjectsSortMode.None))if(id.teamName=="ROSSO"){car=id.GetComponent<OverdriveVehicle>();break;}
        var studio=new GameObject("Temporary title car studio");var origin=studio.transform;
        foreach(var filter in car.GetComponentsInChildren<MeshFilter>())
        {
            var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!renderer.enabled||!renderer.gameObject.activeInHierarchy||!filter.sharedMesh)continue;
            var go=new GameObject("Title car part",typeof(MeshFilter),typeof(MeshRenderer));go.layer=30;go.transform.SetParent(origin,false);
            go.transform.localPosition=car.transform.InverseTransformPoint(filter.transform.position);go.transform.localRotation=Quaternion.Inverse(car.transform.rotation)*filter.transform.rotation;go.transform.localScale=filter.transform.lossyScale;
            go.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;go.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
        }
        var masks=new Dictionary<Light,int>();foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)){masks[light]=light.cullingMask;light.cullingMask&=~(1<<30);}
        var key=new GameObject("Studio key");var l=key.AddComponent<Light>();l.type=LightType.Directional;l.intensity=2.2f;l.color=new Color(1,.94f,.91f);l.cullingMask=1<<30;key.transform.rotation=Quaternion.Euler(42,-28,0);
        var fill=new GameObject("Studio rim");var rim=fill.AddComponent<Light>();rim.type=LightType.Directional;rim.intensity=1.1f;rim.color=new Color(.65f,.72f,1);rim.cullingMask=1<<30;fill.transform.rotation=Quaternion.Euler(15,140,0);
        var cameraGo=new GameObject("Title studio camera");var camera=cameraGo.AddComponent<Camera>();camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.fieldOfView=30;camera.nearClipPlane=.1f;camera.farClipPlane=60;
        camera.transform.position=new Vector3(7,3.1f,8);camera.transform.LookAt(new Vector3(0,.12f,0));
        var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1600,1000,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();return Import("Red formula hero",texture);
        }
        finally
        {RenderTexture.active=old;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);foreach(var pair in masks)if(pair.Key)pair.Key.cullingMask=pair.Value;Object.DestroyImmediate(cameraGo);Object.DestroyImmediate(key);Object.DestroyImmediate(fill);Object.DestroyImmediate(studio);}
    }
    [MenuItem("Overdrive/Apply Red Racing Title Menu")]
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Apply outside Play mode.");
        var scene=EditorSceneManager.GetActiveScene();var ui=Object.FindFirstObjectByType<RaceUI>();if(!ui||scene.path!="Assets/Scenes/OverdriveF1.unity")throw new System.InvalidOperationException("Open the existing F1 scene.");
        System.IO.Directory.CreateDirectory(Folder);AssetDatabase.Refresh();latin=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");korean=ui.standings.font;
        var menu=ui.menu.transform;Fill(menu.GetComponent<RectTransform>());ui.menu.GetComponent<UnityEngine.UI.Image>().color=new Color(.022f,.022f,.028f,1);
        foreach(string name in new[]{"Hero panel","Circuit index","Circuit note","Circuit map","Circuit specifications","Title accent"}){var t=menu.Find(name);if(t)t.gameObject.SetActive(false);}
        var backdrop=Get("Red Intro Backdrop",menu,typeof(UnityEngine.UI.RawImage));Fill(backdrop.GetComponent<RectTransform>());backdrop.GetComponent<UnityEngine.UI.RawImage>().texture=Backdrop();backdrop.GetComponent<UnityEngine.UI.RawImage>().raycastTarget=false;backdrop.SetAsFirstSibling();
        var hero=Get("Red Intro Hero",menu,typeof(UnityEngine.UI.RawImage));Place(hero.GetComponent<RectTransform>(),760,170,1220,762);hero.GetComponent<UnityEngine.UI.RawImage>().texture=Hero();hero.GetComponent<UnityEngine.UI.RawImage>().raycastTarget=false;hero.SetSiblingIndex(1);
        Bar("Red Intro Top Rule",menu,0,0,1920,4,Red);
        var brand=Label("Edition",menu,"OD  /  FORMULA RACING",100,66,900,30,19,White,true);brand.characterSpacing=3;
        var title=Label("Title",menu,"OVERDRIVE",92,240,940,150,108,White,true);title.fontStyle=FontStyles.Bold|FontStyles.Italic;title.characterSpacing=-3;
        Label("Subtitle",menu,"CHASE THE LIMIT.",100,400,700,42,28,Grey).characterSpacing=3;
        Bar("Red Intro Title Marker",menu,100,202,68,5,Red);
        Label("Specification",menu,"20 CARS   /   APEX PARK GRAND PRIX   /   2 LAPS",100,473,740,26,15,Grey).characterSpacing=1;
        Label("Difficulty label",menu,"RACE DIFFICULTY",100,542,600,24,13,Grey).characterSpacing=3;
        Button(ui.normalButton,100,581,265,52,"01   NORMAL");Button(ui.hardButton,381,581,265,52,"02   HARD");
        ui.normalButton.GetComponent<UnityEngine.UI.Image>().color=ui.race.HardSelected?new Color(.10f,.105f,.12f):new Color(.38f,.035f,.055f);
        ui.hardButton.GetComponent<UnityEngine.UI.Image>().color=ui.race.HardSelected?new Color(.38f,.035f,.055f):new Color(.10f,.105f,.12f);
        Button(ui.startButton,100,658,546,80,"START RACE                              >",true);
        Button(ui.quitButton,1678,55,142,44,"EXIT GAME");ui.quitButton.GetComponentInChildren<TMP_Text>().fontSize=14;
        Label("Keybind label",menu,"DRIVER CONTROLS",100,814,700,22,12,new Color(.82f,.26f,.30f)).characterSpacing=2;
        var controls=menu.Find("Controls").GetComponent<TMP_Text>();Place(controls.rectTransform,100,850,950,110);controls.font=latin;controls.fontSize=15;controls.color=Grey;controls.alignment=TextAlignmentOptions.TopLeft;controls.raycastTarget=false;controls.textWrappingMode=TextWrappingModes.Normal;
        controls.text="W  THROTTLE    SPACE  BRAKE    RELEASE  COAST    A/D  STEER\nAUTO GEARS\nC  CAMERA    F  DRS    M  OVERTAKE    G  GUIDE    ESC  PAUSE";
        Label("Footer",menu,"PROJECT OVERDRIVE   /   GRAND PRIX EXPERIENCE",100,1022,1200,22,12,new Color(.42f,.42f,.46f)).characterSpacing=2;
        var line=Bar("Footer line",menu,100,992,1720,1,new Color(.36f,.12f,.15f,.79f));
        Label("Red Intro Hero Caption",menu,"RACE WEEKEND",1250,897,570,34,16,White,true).characterSpacing=4;
        Label("Red Intro Hero Subcaption",menu,"APEX PARK  /  5.08 KM",1250,936,570,25,13,Grey).characterSpacing=2;
        var fade=ui.menu.GetComponent<CanvasGroup>();if(!fade)fade=ui.menu.AddComponent<CanvasGroup>();fade.alpha=1;fade.interactable=true;fade.blocksRaycasts=true;
        var motion=ui.menu.GetComponent<RedIntroMotion>();if(!motion)motion=ui.menu.AddComponent<RedIntroMotion>();motion.hero=hero.GetComponent<RectTransform>();motion.title=title.rectTransform;motion.fade=fade;motion.pulse=line;motion.race=ui.race;motion.normalButton=ui.normalButton;motion.hardButton=ui.hardButton;
        var nav=new UnityEngine.UI.Navigation {mode=UnityEngine.UI.Navigation.Mode.Explicit};nav.selectOnDown=ui.normalButton;nav.selectOnRight=ui.hardButton;ui.startButton.navigation=nav;
        nav.selectOnDown=ui.startButton;nav.selectOnUp=ui.quitButton;nav.selectOnRight=ui.hardButton;nav.selectOnLeft=null;ui.normalButton.navigation=nav;
        nav.selectOnRight=null;nav.selectOnLeft=ui.normalButton;ui.hardButton.navigation=nav;
        nav.selectOnUp=ui.startButton;nav.selectOnDown=ui.normalButton;nav.selectOnLeft=ui.hardButton;ui.quitButton.navigation=nav;
        if(ui.professionalButton)
        {
            Button(ui.professionalButton,100,658,546,52,"03   F1 DRIVER");
            Button(ui.startButton,100,736,546,64,"START RACE                              >",true);
            motion.professionalButton=ui.professionalButton;
            nav=new UnityEngine.UI.Navigation {mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=ui.normalButton,selectOnDown=ui.startButton,selectOnLeft=ui.normalButton,selectOnRight=ui.hardButton};
            ui.professionalButton.navigation=nav;
            nav=ui.normalButton.navigation;nav.selectOnDown=ui.professionalButton;ui.normalButton.navigation=nav;
            nav=ui.hardButton.navigation;nav.selectOnDown=ui.professionalButton;ui.hardButton.navigation=nav;
            nav=ui.startButton.navigation;nav.selectOnUp=ui.professionalButton;ui.startButton.navigation=nav;
        }
        Canvas.ForceUpdateCanvases();foreach(var text in ui.menu.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true);
        EditorUtility.SetDirty(motion);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();return "Red racing title applied to existing menu; 4 existing buttons retained; menu-only fade/hero drift/red pulse; HUD and race actions preserved.";
    }
    public static string Capture(int width=1920,int height=1080)
    {
        var ui=Object.FindFirstObjectByType<RaceUI>();var panels=new[]{ui.menu,ui.hud,ui.pause,ui.results,ui.countdown};var states=System.Array.ConvertAll(panels,p=>p.activeSelf);
        var fade=ui.menu.GetComponent<CanvasGroup>();float alpha=fade?fade.alpha:1;var camera=Camera.main;var oldTarget=camera.targetTexture;var old=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(width,height,24);
        try
        {
            ui.menu.SetActive(true);ui.hud.SetActive(false);ui.pause.SetActive(false);ui.results.SetActive(false);ui.countdown.SetActive(false);if(fade)fade.alpha=1;
            camera.targetTexture=rt;Canvas.ForceUpdateCanvases();foreach(var text in ui.menu.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate(true);camera.Render();
            RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();System.IO.File.WriteAllBytes("Docs/RedIntro_"+width+"x"+height+".png",tex.EncodeToPNG());Object.DestroyImmediate(tex);
        }
        finally{camera.targetTexture=oldTarget;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);for(int i=0;i<panels.Length;i++)panels[i].SetActive(states[i]);if(fade)fade.alpha=alpha;}
        return "Title capture saved";
    }
    public static bool IntroProofComplete {get;private set;}
    public static string VerifyLiveIntro()
    {
        if(!EditorApplication.isPlaying)throw new System.InvalidOperationException("Play mode required.");
        IntroProofComplete=false;
        var ui=Object.FindFirstObjectByType<RaceUI>();ui.race.MainMenu();ui.menu.SetActive(true);
        var motion=ui.menu.GetComponent<RedIntroMotion>();motion.enabled=false;motion.enabled=true;
        float initialFade=motion.fade.alpha,began=Time.unscaledTime,midFade=-1;int initialFrame=Time.frameCount;var initialHero=motion.hero.anchoredPosition;
        EditorApplication.CallbackFunction tick=null;
        tick=()=>
        {
            if(!EditorApplication.isPlaying){EditorApplication.update-=tick;return;}
            float elapsed=Time.unscaledTime-began;
            if(elapsed>.12f&&midFade<0)midFade=motion.fade.alpha;
            if(elapsed<1)return;
            bool moving=motion.hero.anchoredPosition!=initialHero;float finalFade=motion.fade.alpha;
            var events=UnityEngine.EventSystems.EventSystem.current;
            System.Func<UnityEngine.UI.Button,bool> click=b=>
            {
                var data=new UnityEngine.EventSystems.PointerEventData(events);
                var r=b.GetComponent<RectTransform>();data.position=RectTransformUtility.WorldToScreenPoint(ui.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center));
                var hits=new List<UnityEngine.EventSystems.RaycastResult>();events.RaycastAll(data,hits);if(hits.Count==0)return false;
                UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                return hits[0].gameObject==b.gameObject;
            };
            bool hardHit=click(ui.hardButton),hardSelected=ui.race.HardSelected;
            bool normalHit=click(ui.normalButton),normalSelected=!ui.race.HardSelected;
            bool startHit=click(ui.startButton),started=ui.race.State==RaceState.Countdown;
            var result=new {initialFrame,frame=Time.frameCount,initialFade,midFade,finalFade,heroMoved=moving,hardHit,hardSelected,normalHit,normalSelected,startHit,started,
                pass=Time.frameCount>initialFrame&&initialFade==0&&finalFade>.99f&&moving&&hardHit&&hardSelected&&normalHit&&normalSelected&&startHit&&started};
            System.IO.File.WriteAllText("Docs/RedIntro_InteractionProof.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            ui.race.MainMenu();EditorApplication.update-=tick;IntroProofComplete=true;
        };
        EditorApplication.update+=tick;return "Intro fade, drift and routed button-click verification started";
    }
}
