using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Overdrive;
public static class OverdriveDrivingAssistSetup
{
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first.");
        var race=Object.FindFirstObjectByType<RaceManager>();var ui=Object.FindFirstObjectByType<RaceUI>();
        if(!race || EditorSceneManager.GetActiveScene().path!="Assets/Scenes/OverdriveF1.unity")throw new System.InvalidOperationException("Open F1 scene.");
        if(Object.FindFirstObjectByType<DrivingGuide>())return "Guide already installed.";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Scenes/OverdriveF1_BeforeDrivingGuide.unity",true);
        var go=new GameObject("Driving reference line",typeof(MeshFilter),typeof(MeshRenderer),typeof(DrivingGuide));var guide=go.GetComponent<DrivingGuide>();guide.race=race;
        var mats=new Material[3];var colors=new[]{new Color(.1f,1,.38f),new Color(1,.73f,.04f),new Color(1,.06f,.055f)};
        if(!AssetDatabase.IsValidFolder("Assets/Materials/DrivingGuide"))AssetDatabase.CreateFolder("Assets/Materials","DrivingGuide");
        for(int i=0;i<3;i++){mats[i]=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mats[i].color=colors[i];mats[i].SetFloat("_Cull",0);AssetDatabase.CreateAsset(mats[i],"Assets/Materials/DrivingGuide/Guide "+i+".mat");}go.GetComponent<MeshRenderer>().sharedMaterials=mats;
        var label=new GameObject("Driving advice",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(ui.hud.transform,false);var r=label.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(0,75);r.sizeDelta=new Vector2(900,30);var text=label.GetComponent<TextMeshProUGUI>();text.font=ui.standings.font;text.fontSize=19;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;guide.advice=text;
        foreach(var t in ui.GetComponentsInChildren<TMP_Text>(true))
        {
            if(t.name=="Controls")t.text="W 가속 / S 브레이크 / 키 해제 타력 주행   A/D 조향\n자동 기어 변속\nC 시점   F DRS   M 오버테이크   G 가이드   Esc 일시정지";
            if(t.name=="Driving Guide")t.text="W  THROTTLE / SPACE  BRAKE / RELEASE  COAST    AUTO GEARS    C  CAMERA    M  OVERTAKE    G  GUIDE";
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();return "Dynamic 240m centreline guide, brake targets and new keymap saved.";
    }
}
