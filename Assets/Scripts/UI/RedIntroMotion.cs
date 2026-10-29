using UnityEngine;
namespace Overdrive
{
    // Small unscaled movements only on the title menu. Existing race UI owns its visibility/actions.
    public sealed class RedIntroMotion : MonoBehaviour
    {
        public RectTransform hero,title;
        public CanvasGroup fade;
        public UnityEngine.UI.Image pulse;
        public RaceManager race;
        public UnityEngine.UI.Button normalButton,hardButton,professionalButton;
        public TMPro.TMP_Text modeSummary;
        public RectTransform[] ribbons;
        Vector2 heroOrigin,titleOrigin;
        Vector2[] ribbonOrigins;Quaternion heroRotation;TMPro.TMP_Text titleText;
        bool poseCaptured;
        float started;
        void OnEnable()
        {
            if(!Application.isPlaying)return;
            started=Time.unscaledTime;
            if(hero)heroOrigin=hero.anchoredPosition;
            if(hero)heroRotation=hero.localRotation;
            if(title)titleOrigin=title.anchoredPosition;
            if(title)titleText=title.GetComponent<TMPro.TMP_Text>();
            ribbonOrigins=new Vector2[ribbons==null?0:ribbons.Length];
            for(int i=0;i<ribbonOrigins.Length;i++)if(ribbons[i])ribbonOrigins[i]=ribbons[i].anchoredPosition;
            poseCaptured=true;
            if(fade){fade.alpha=0;fade.interactable=false;}
        }
        void LateUpdate()
        {
            float elapsed=Mathf.Max(0,Time.unscaledTime-started);
            float reveal=1-Mathf.Pow(1-Mathf.Clamp01(elapsed/.65f),3);
            if(fade){fade.alpha=reveal;fade.interactable=elapsed>.35f;}
            Vector2 pointer=UnityEngine.InputSystem.Mouse.current!=null?UnityEngine.InputSystem.Mouse.current.position.ReadValue():new Vector2(Screen.width*.5f,Screen.height*.5f);
            pointer=new Vector2(Mathf.Clamp(pointer.x/Mathf.Max(1,Screen.width)-.5f,-.5f,.5f),Mathf.Clamp(pointer.y/Mathf.Max(1,Screen.height)-.5f,-.5f,.5f));
            if(hero)
            {
                var target=heroOrigin+new Vector2(42*(1-reveal)+Mathf.Sin(elapsed*.65f)*7+pointer.x*18,Mathf.Sin(elapsed*.8f)*4+pointer.y*10);
                hero.anchoredPosition=Vector2.Lerp(hero.anchoredPosition,target,1-Mathf.Exp(-7*Time.unscaledDeltaTime));
                hero.localRotation=heroRotation*Quaternion.Euler(0,0,Mathf.Sin(elapsed*.42f)*.32f);
            }
            if(title)title.anchoredPosition=titleOrigin+Vector2.left*(14*(1-reveal));
            if(titleText)titleText.maxVisibleCharacters=elapsed>.65f?int.MaxValue:Mathf.FloorToInt(Mathf.Max(0,elapsed-.1f)*24);
            for(int i=0;i<ribbonOrigins.Length;i++)if(ribbons[i])ribbons[i].anchoredPosition=ribbonOrigins[i]+Vector2.right*Mathf.Sin(elapsed*.35f+i)*20;
            if(pulse){var c=pulse.color;c.a=.66f+Mathf.Sin(elapsed*.8f)*.13f;pulse.color=c;}
            if(race)
            {
                var selected=new Color(.38f,.035f,.055f,1);var idle=new Color(.10f,.105f,.12f,1);
                if(normalButton)normalButton.GetComponent<UnityEngine.UI.Image>().color=race.SelectedDifficulty==RaceDifficulty.Normal?selected:idle;
                if(hardButton)hardButton.GetComponent<UnityEngine.UI.Image>().color=race.HardSelected?selected:idle;
                if(professionalButton)professionalButton.GetComponent<UnityEngine.UI.Image>().color=race.ProfessionalSelected?new Color(.42f,.12f,.04f):idle;
                if(modeSummary)modeSummary.text=race.IsQualifying?"CLEAR TRACK. THREE FLYING LAPS.":race.ProfessionalSelected?"ELITE PACE. COMMITTED OVERTAKES.":race.HardSelected?"LESS MARGIN. MORE PRESSURE.":"BUILD YOUR RHYTHM. FIND THE APEX.";
            }
        }
        void OnDisable()
        {
            if(!poseCaptured)return;
            if(hero){hero.anchoredPosition=heroOrigin;hero.localRotation=heroRotation;}
            if(title)title.anchoredPosition=titleOrigin;
            if(titleText)titleText.maxVisibleCharacters=int.MaxValue;
            if(ribbonOrigins!=null)for(int i=0;i<ribbonOrigins.Length;i++)if(ribbons[i])ribbons[i].anchoredPosition=ribbonOrigins[i];
            if(fade){fade.alpha=1;fade.interactable=true;}
            poseCaptured=false;
        }
    }
}
