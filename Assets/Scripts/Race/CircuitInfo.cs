using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Overdrive
{
    [DefaultExecutionOrder(400)]
    public sealed class CircuitInfo : MonoBehaviour
    {
        public string displayName="APEX PARK";
        public float lengthKm;
        public bool harbor;
        public bool yasMarina;
        public float racingLineHalfWidth=6;
        public RaceUI ui;
        public UnityEngine.UI.Button apexButton,harborButton,yasButton;
        public TMP_Text description;
        static bool restore;
        static SessionMode requestedMode;
        static RaceDifficulty requestedDifficulty;
        void OnEnable()
        {
            if(apexButton){apexButton.onClick.RemoveListener(LoadApex);apexButton.onClick.AddListener(LoadApex);}
            if(harborButton){harborButton.onClick.RemoveListener(LoadHarbor);harborButton.onClick.AddListener(LoadHarbor);}
            if(yasButton){yasButton.onClick.RemoveListener(LoadYas);yasButton.onClick.AddListener(LoadYas);}
            if(ui && ui.qualifyingButton && apexButton && harborButton)
            {
                var nav=ui.qualifyingButton.navigation;nav.selectOnRight=apexButton;ui.qualifyingButton.navigation=nav;
                nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnLeft=ui.qualifyingButton,selectOnRight=harborButton,selectOnUp=ui.quitButton,selectOnDown=ui.startButton};apexButton.navigation=nav;
                nav.selectOnLeft=apexButton;nav.selectOnRight=yasButton?yasButton:ui.quitButton;harborButton.navigation=nav;
                if(yasButton){nav.selectOnLeft=harborButton;nav.selectOnRight=ui.quitButton;yasButton.navigation=nav;}
                nav=ui.quitButton.navigation;nav.selectOnLeft=yasButton?yasButton:harborButton;ui.quitButton.navigation=nav;
            }
        }
        void Start()
        {
            if(!restore || !ui)return;restore=false;
            if(requestedDifficulty==RaceDifficulty.Professional)ui.race.SelectProfessional();
            else if(requestedDifficulty==RaceDifficulty.Hard)ui.race.SelectHard();else ui.race.SelectNormal();
            if(requestedMode==SessionMode.Qualifying)ui.race.SelectQualifying();else ui.race.SelectGrandPrix();
        }
        void LoadApex(){Load(0);}
        void LoadHarbor(){Load(1);}
        void LoadYas(){Load(2);}
        void Load(int next)
        {
            if(!ui || ui.race.State!=RaceState.Menu || next==(yasMarina?2:harbor?1:0))return;
            requestedMode=ui.race.SelectedMode;requestedDifficulty=ui.race.SelectedDifficulty;restore=true;
            Time.timeScale=1;SceneManager.LoadScene(next==2?"OverdriveYasMarina":next==1?"OverdriveHarbor":"OverdriveF1");
        }
        void LateUpdate()
        {
            if(apexButton)apexButton.GetComponent<UnityEngine.UI.Image>().color=harbor || yasMarina?new Color(.04f,.07f,.1f):new Color(.04f,.27f,.3f);
            if(harborButton)harborButton.GetComponent<UnityEngine.UI.Image>().color=harbor?new Color(.04f,.27f,.3f):new Color(.04f,.07f,.1f);
            if(yasButton)yasButton.GetComponent<UnityEngine.UI.Image>().color=yasMarina?new Color(.04f,.27f,.3f):new Color(.04f,.07f,.1f);
            if(description)description.text=$"{displayName}  /  {lengthKm:0.000} KM  /  {(yasMarina?"ABU DHABI GRAND PRIX":harbor?"COASTAL GRAND PRIX":"GRAND PRIX CIRCUIT")}";
        }
    }
}
