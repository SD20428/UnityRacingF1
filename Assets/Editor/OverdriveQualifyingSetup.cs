using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Overdrive;

public static class OverdriveQualifyingSetup
{
    static UnityEngine.UI.Button Mode(RaceUI ui,string name,string caption,float x)
    {
        var existing=ui.menu.transform.Find(name);
        var go=existing?existing.gameObject:new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
        go.transform.SetParent(ui.menu.transform,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-133);rect.sizeDelta=new Vector2(265,46);
        var image=go.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.06f,.09f,.13f);
        var button=go.GetComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
        var label=go.GetComponentInChildren<TMP_Text>();
        if(!label)
        {
            label=new GameObject("Caption",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();label.transform.SetParent(go.transform,false);
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(16,0);label.rectTransform.offsetMax=new Vector2(-8,0);
        }
        label.font=ui.startButton.GetComponentInChildren<TMP_Text>().font;label.fontSize=16;label.color=Color.white;label.text=caption;label.alignment=TextAlignmentOptions.MidlineLeft;label.raycastTarget=false;
        var motion=go.GetComponent<UIButtonMotion>();if(!motion)motion=go.AddComponent<UIButtonMotion>();motion.caption=label;
        return button;
    }
    [MenuItem("Overdrive/Add Qualifying Mode And Space Brake")]
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
        var ui=Object.FindFirstObjectByType<RaceUI>();if(!ui)throw new System.InvalidOperationException("Open OverdriveF1 scene");
        ui.grandPrixButton=Mode(ui,"Grand Prix mode","GRAND PRIX",100);
        ui.qualifyingButton=Mode(ui,"Qualifying mode","QUALIFYING",381);
        ui.startLabel=ui.startButton.GetComponentInChildren<TMP_Text>();
        ui.sessionDescription=ui.menu.transform.Find("Specification").GetComponent<TMP_Text>();
        ui.sessionDescription.text="20 CARS  /  GRAND PRIX  /  2 LAPS";
        ui.sessionGridChip=ui.menu.transform.Find("Session chip 0/Chip text").GetComponent<TMP_Text>();
        ui.sessionLapChip=ui.menu.transform.Find("Session chip 1/Chip text").GetComponent<TMP_Text>();
        ui.weekendStatus=ui.menu.transform.Find("Weekend status").GetComponent<TMP_Text>();
        ui.eventTitle=ui.hud.transform.Find("Event title").GetComponent<TMP_Text>();
        ui.resultFooter=ui.results.transform.Find("Result footer").GetComponent<TMP_Text>();
        ui.resultHeading=ui.results.transform.Find("Result heading").GetComponent<TMP_Text>();
        ui.retryLabel=ui.retryButton.GetComponentInChildren<TMP_Text>();
        ui.aiLegend=ui.hud.transform.Find("Circuit card/AI legend").gameObject;
        var a=new UnityEngine.UI.Navigation {mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnRight=ui.qualifyingButton,selectOnDown=ui.normalButton,selectOnUp=ui.quitButton};ui.grandPrixButton.navigation=a;
        a.selectOnRight=null;a.selectOnLeft=ui.grandPrixButton;a.selectOnDown=ui.hardButton;ui.qualifyingButton.navigation=a;
        var n=ui.normalButton.navigation;n.selectOnUp=ui.grandPrixButton;ui.normalButton.navigation=n;
        n=ui.hardButton.navigation;n.selectOnUp=ui.qualifyingButton;ui.hardButton.navigation=n;
        foreach(var text in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            if(text.name=="Controls")text.text="W  THROTTLE    SPACE  BRAKE    RELEASE  COAST    A/D  STEER\nAUTO GEARS\nC  CAMERA    F  DRS    M  OVERTAKE    G  GUIDE    ESC  PAUSE";
            if(text.name=="Driving Guide")text.text="W  THROTTLE / SPACE  BRAKE / RELEASE  COAST    AUTO GEARS    C  CAMERA    M  OVERTAKE    G  GUIDE";
        }
        EditorUtility.SetDirty(ui);EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);EditorSceneManager.SaveScene(ui.gameObject.scene);
        return "Grand Prix / solo qualifying menu and Space brake hints saved";
    }
}
