using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Overdrive;

public static class OverdriveF1DriverSetup
{
    [MenuItem("Overdrive/Configure F1 Driver Mode")]
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first");
        var race=Object.FindFirstObjectByType<RaceManager>();var ui=Object.FindFirstObjectByType<RaceUI>();
        if(!race || !ui || EditorSceneManager.GetActiveScene().path!="Assets/Scenes/OverdriveF1.unity")
            throw new System.InvalidOperationException("Open the F1 scene");
        var preset=AssetDatabase.LoadAssetAtPath<AIDifficulty>("Assets/Settings/F1_Professional.asset");
        if(!preset)
        {
            preset=ScriptableObject.CreateInstance<AIDifficulty>();preset.name="F1_Professional";
            preset.maximumSpeedKmh=320;preset.cornerSpeedKmh=100;preset.lookaheadBase=10;preset.lookaheadPerKmh=.1f;
            preset.steeringResponse=9;preset.lateralAcceleration=16;preset.brakingLookahead=180;
            preset.professionalDriver=true;preset.racingProfile=new DrivingGuideTuning();
            AssetDatabase.CreateAsset(preset,"Assets/Settings/F1_Professional.asset");
        }
        race.professional=preset;
        if(!ui.professionalButton)
        {
            ui.professionalButton=Object.Instantiate(ui.hardButton,ui.hardButton.transform.parent);
            ui.professionalButton.name="F1 Driver";
            ui.professionalButton.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();
        }
        var rect=ui.professionalButton.GetComponent<RectTransform>();
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(100,-658);rect.sizeDelta=new Vector2(546,52);
        var text=ui.professionalButton.GetComponentInChildren<TMPro.TMP_Text>();text.text="03   F1 DRIVER";text.fontSize=20;
        ui.professionalButton.GetComponent<UnityEngine.UI.Image>().color=new Color(.10f,.105f,.12f);
        var start=ui.startButton.GetComponent<RectTransform>();start.anchoredPosition=new Vector2(100,-736);start.sizeDelta=new Vector2(546,64);
        var motion=ui.menu.GetComponent<RedIntroMotion>();if(motion){motion.professionalButton=ui.professionalButton;EditorUtility.SetDirty(motion);}
        Navigation(ui.normalButton,null,ui.hardButton,ui.quitButton,ui.professionalButton);
        Navigation(ui.hardButton,ui.normalButton,null,ui.quitButton,ui.professionalButton);
        Navigation(ui.professionalButton,ui.normalButton,ui.hardButton,ui.normalButton,ui.startButton);
        Navigation(ui.startButton,null,null,ui.professionalButton,ui.normalButton);
        race.SelectNormal();
        EditorUtility.SetDirty(race);EditorUtility.SetDirty(ui);EditorUtility.SetDirty(preset);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        return "F1 DRIVER preset and third menu choice saved";
    }
    static void Navigation(UnityEngine.UI.Button button,UnityEngine.UI.Button left,UnityEngine.UI.Button right,UnityEngine.UI.Button up,UnityEngine.UI.Button down)
    {
        button.navigation=new UnityEngine.UI.Navigation {mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnLeft=left,selectOnRight=right,selectOnUp=up,selectOnDown=down};
        EditorUtility.SetDirty(button);
    }
}
