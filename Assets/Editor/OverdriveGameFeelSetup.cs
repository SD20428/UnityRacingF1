using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Overdrive;
using Object=UnityEngine.Object;

public static class OverdriveGameFeelSetup
{
    static readonly Color Red=new Color(.96f,.065f,.13f),White=new Color(.95f,.96f,.98f);
    static TMP_FontAsset font;
    static T Component<T>(GameObject go) where T:Component
    {var c=go.GetComponent<T>();return c?c:go.AddComponent<T>();}
    static RectTransform Rect(string name,Transform parent)
    {
        var t=parent.Find(name);if(t)return (RectTransform)t;
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;
    }
    static void Place(RectTransform r,float x,float y,float w,float h)
    {r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    static void Fill(RectTransform r)
    {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static UnityEngine.UI.Image Image(string name,Transform parent,Color color)
    {var r=Rect(name,parent);var image=Component<UnityEngine.UI.Image>(r.gameObject);image.color=color;image.raycastTarget=false;return image;}
    static TMP_Text Label(string name,Transform parent,string text,float x,float y,float w,float h,float size,Color color)
    {
        var r=Rect(name,parent);Place(r,x,y,w,h);var t=Component<TextMeshProUGUI>(r.gameObject);
        t.font=font;t.text=text;t.fontSize=size;t.color=color;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;
        t.alignment=TextAlignmentOptions.MidlineLeft;return t;
    }
    static void Reveal(GameObject panel,string[] names,Vector2 offset,bool fade,float stagger=.035f,float duration=.32f)
    {
        var reveal=Component<UIRevealMotion>(panel);var targets=new List<RectTransform>();var groups=new List<CanvasGroup>();
        foreach(var name in names)
        {
            var child=panel.transform.Find(name);if(!child)continue;
            targets.Add((RectTransform)child);var group=Component<CanvasGroup>(child.gameObject);group.alpha=1;groups.Add(group);
        }
        reveal.items=targets.ToArray();reveal.itemGroups=groups.ToArray();reveal.offset=offset;reveal.duration=duration;reveal.stagger=stagger;
        reveal.delay=fade?0:.08f;reveal.panelGroup=fade?Component<CanvasGroup>(panel):null;
        if(reveal.panelGroup)reveal.panelGroup.alpha=1;EditorUtility.SetDirty(reveal);
    }
    [MenuItem("Overdrive/Add Racing Game Presentation")]
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
        var ui=Object.FindFirstObjectByType<RaceUI>();var scene=EditorSceneManager.GetActiveScene();
        if(!ui || scene.path!="Assets/Scenes/OverdriveF1.unity")throw new System.InvalidOperationException("Open F1 scene");
        font=ui.startButton.GetComponentInChildren<TMP_Text>().font;
        var intro=ui.menu.GetComponent<RedIntroMotion>();
        intro.modeSummary=Label("Mode summary",ui.menu.transform,"BUILD YOUR RHYTHM. FIND THE APEX.",100,444,740,20,13,new Color(.76f,.78f,.82f));
        intro.modeSummary.characterSpacing=1.1f;
        Label("Weekend status",ui.menu.transform,"APEX PARK / GRAND PRIX",1390,59,255,28,12,new Color(.75f,.77f,.81f));
        intro.ribbons=new RectTransform[3];
        for(int i=0;i<3;i++)
        {
            var image=Image("Motion ribbon "+i,ui.menu.transform,new Color(.95f,.04f,.1f,.045f+i*.018f));
            Place(image.rectTransform,1000+i*290,-80,35+i*22,1450);image.rectTransform.localRotation=Quaternion.Euler(0,0,22);
            image.transform.SetSiblingIndex(1+i);intro.ribbons[i]=image.rectTransform;
        }
        EditorUtility.SetDirty(intro);
        foreach(var button in ui.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            var motion=Component<UIButtonMotion>(button.gameObject);motion.caption=button.GetComponentInChildren<TMP_Text>();
            var accent=Image("Interaction accent",button.transform,Red);var r=accent.rectTransform;
            r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,.5f);r.offsetMin=new Vector2(0,9);r.offsetMax=new Vector2(3,-9);motion.accent=accent;
            var line=Image("Button top edge",button.transform,new Color(1,1,1,.10f));
            line.rectTransform.anchorMin=new Vector2(0,1);line.rectTransform.anchorMax=Vector2.one;line.rectTransform.offsetMin=new Vector2(0,-1);line.rectTransform.offsetMax=Vector2.zero;
            EditorUtility.SetDirty(motion);
        }
        foreach(var button in new[]{ui.resumeButton,ui.restartButton,ui.pauseMenuButton,ui.retryButton,ui.resultMenuButton})
        {
            bool primary=button==ui.resumeButton || button==ui.retryButton;
            button.GetComponent<UnityEngine.UI.Image>().color=primary?Red:new Color(.035f,.044f,.06f);
            var text=button.GetComponentInChildren<TMP_Text>();text.color=White;text.fontStyle=FontStyles.Bold;
            var colors=button.colors;colors.normalColor=colors.selectedColor=Color.white;
            colors.highlightedColor=new Color(1,.86f,.88f);colors.pressedColor=new Color(.72f,.72f,.76f);colors.fadeDuration=.1f;button.colors=colors;
            var outline=button.GetComponent<UnityEngine.UI.Outline>();if(outline)outline.effectColor=new Color(.8f,.85f,.92f,.12f);
            EditorUtility.SetDirty(button);EditorUtility.SetDirty(text);
        }
        foreach(string name in new[]{"Driver dashboard","Circuit card","Timing card","Position card","Timing tower backing"})
        {
            var card=ui.hud.transform.Find(name);if(!card)continue;
            var image=card.GetComponent<UnityEngine.UI.Image>();if(image){image.color=new Color(.018f,.028f,.044f,.92f);image.raycastTarget=false;EditorUtility.SetDirty(image);}
            var line=Image("Card racing accent",card,Red);Place(line.rectTransform,0,0,76,3);
            var edge=Image("Card lower edge",card,new Color(.8f,.9f,1,.09f));
            edge.rectTransform.anchorMin=Vector2.zero;edge.rectTransform.anchorMax=new Vector2(1,0);edge.rectTransform.offsetMin=Vector2.zero;edge.rectTransform.offsetMax=new Vector2(0,1);
        }
        Reveal(ui.menu,new[]{"Subtitle","Mode summary","Specification","Difficulty label","Normal","Hard","F1 Driver","Start","Controls","Red Intro Hero Caption","Red Intro Hero Subcaption"},new Vector2(-22,0),false);
        Reveal(ui.hud,new[]{"Position card","Timing card","Circuit card","Driver dashboard"},new Vector2(0,-20),true,.04f,.28f);
        Reveal(ui.pause,new[]{"Title","Pause caption","Resume","Restart","Menu"},new Vector2(0,-16),true,.035f,.22f);
        Reveal(ui.results,new[]{"Result heading","Result","Full field classification","Retry","Menu","Result footer"},new Vector2(0,-28),true,.065f,.4f);
        foreach(var panel in new[]{ui.pause,ui.results})
        {
            var bg=panel.GetComponent<UnityEngine.UI.Image>();if(bg)bg.color=new Color(.018f,.024f,.036f,.98f);
        }
        var checker=Rect("Finish flag",ui.results.transform);Place(checker,64,96,952,10);
        for(int row=0;row<2;row++)for(int col=0;col<32;col++)
        {
            var cell=Image("Square "+row+" "+col,checker,(row+col)%2==0?new Color(.84f,.86f,.9f):new Color(.045f,.055f,.07f));
            Place(cell.rectTransform,col*29.75f,row*5,29.75f,5);
        }
        var effects=Component<RacePresentationMotion>(ui.gameObject);effects.ui=ui;
        effects.dashboard=Object.FindFirstObjectByType<OverdriveRacingHUD>(FindObjectsInactive.Include);
        effects.broadcast=Object.FindFirstObjectByType<GrandPrixBroadcast>(FindObjectsInactive.Include);
        var layer=Rect("Race presentation",ui.transform);Fill(layer);layer.SetAsLastSibling();
        var layerGroup=Component<CanvasGroup>(layer.gameObject);layerGroup.blocksRaycasts=false;layerGroup.interactable=false;
        var notice=Rect("Race control notice",layer);notice.anchorMin=notice.anchorMax=notice.pivot=new Vector2(.5f,1);notice.anchoredPosition=new Vector2(0,-190);notice.sizeDelta=new Vector2(640,84);
        var backing=Component<UnityEngine.UI.Image>(notice.gameObject);backing.color=new Color(.012f,.02f,.035f,.94f);backing.raycastTarget=false;
        effects.notice=Component<CanvasGroup>(notice.gameObject);effects.notice.alpha=0;effects.notice.blocksRaycasts=false;effects.notice.interactable=false;
        effects.noticeAccent=Image("Notice accent",notice,Red);Place(effects.noticeAccent.rectTransform,0,0,4,84);
        effects.noticeTitle=Label("Notice title",notice,"LIGHTS OUT",25,12,590,34,28,White);effects.noticeTitle.fontStyle=FontStyles.Bold|FontStyles.Italic;
        effects.noticeDetail=Label("Notice detail",notice,"APEX PARK / GRAND PRIX",27,51,590,22,13,new Color(.66f,.72f,.8f));effects.noticeDetail.characterSpacing=1.4f;
        var wipe=Rect("Grid transition",layer);Fill(wipe);effects.wipe=Component<CanvasGroup>(wipe.gameObject);effects.wipe.alpha=0;effects.wipe.blocksRaycasts=false;effects.wipe.interactable=false;
        effects.wipeStripes=new RectTransform[3];
        for(int i=0;i<3;i++)
        {
            var stripe=Image("Sweep "+i,wipe,i==1?new Color(.055f,.01f,.022f):Red);
            stripe.rectTransform.anchorMin=stripe.rectTransform.anchorMax=stripe.rectTransform.pivot=new Vector2(.5f,.5f);stripe.rectTransform.sizeDelta=new Vector2(165,1800);stripe.rectTransform.localRotation=Quaternion.Euler(0,0,22);
            effects.wipeStripes[i]=stripe.rectTransform;
        }
        effects.speedStreaks=new RectTransform[8];
        for(int i=0;i<8;i++)
        {
            var image=Image("Speed edge "+i,layer,Color.clear);var r=image.rectTransform;
            r.anchorMin=r.anchorMax=new Vector2((i&1)==1?1:0,.5f);r.pivot=new Vector2(.5f,.5f);r.localRotation=Quaternion.Euler(0,0,(i&1)==1?-12:12);effects.speedStreaks[i]=r;
        }
        effects.lightHalos=new UnityEngine.UI.Image[effects.broadcast.lights.Length];
        for(int i=0;i<effects.lightHalos.Length;i++)
        {
            var original=effects.broadcast.lights[i];var halo=Image("Start light halo "+i,original.transform.parent,Color.clear);
            halo.sprite=original.sprite;halo.rectTransform.anchorMin=original.rectTransform.anchorMin;halo.rectTransform.anchorMax=original.rectTransform.anchorMax;
            halo.rectTransform.pivot=original.rectTransform.pivot;halo.rectTransform.anchoredPosition=original.rectTransform.anchoredPosition;halo.rectTransform.sizeDelta=original.rectTransform.sizeDelta*1.28f;
            halo.transform.SetAsFirstSibling();effects.lightHalos[i]=halo;
        }
        var caption=ui.countdown.transform.Find("Countdown caption").GetComponent<TMP_Text>();caption.gameObject.SetActive(true);
        caption.font=font;caption.text="HOLD YOUR LINE";caption.fontSize=15;caption.color=White;caption.characterSpacing=3;caption.raycastTarget=false;
        caption.rectTransform.anchorMin=caption.rectTransform.anchorMax=caption.rectTransform.pivot=new Vector2(.5f,.5f);caption.rectTransform.anchoredPosition=new Vector2(0,-86);caption.rectTransform.sizeDelta=new Vector2(500,32);caption.alignment=TextAlignmentOptions.Center;effects.startCaption=caption;
        effects.finishRibbons=new RectTransform[12];
        for(int i=0;i<effects.finishRibbons.Length;i++)
        {var image=Image("Finish ribbon "+i,ui.results.transform,Color.clear);Place(image.rectTransform,100+i*73,30,4+i%3,9+i%4*3);effects.finishRibbons[i]=image.rectTransform;}
        EditorUtility.SetDirty(effects);EditorUtility.SetDirty(caption);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Racing menu, button feedback, HUD reveals, five-light pulses, race notices, speed edges and finish presentation saved";
    }
}
