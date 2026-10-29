using UnityEngine;

namespace Overdrive
{
    [DefaultExecutionOrder(220)]
    public sealed class UIRevealMotion : MonoBehaviour
    {
        public RectTransform[] items;
        public CanvasGroup[] itemGroups;
        public CanvasGroup panelGroup;
        public Vector2 offset=new Vector2(-24,0);
        public float duration=.32f,stagger=.035f,delay;
        public float RevealProgress {get;private set;}
        Vector2[] origins;float began;bool captured;
        void OnEnable()
        {
            if(!Application.isPlaying)return;
            began=Time.unscaledTime;RevealProgress=0;
            origins=new Vector2[items==null?0:items.Length];
            for(int i=0;i<origins.Length;i++)if(items[i])origins[i]=items[i].anchoredPosition;
            captured=true;Apply();
        }
        void LateUpdate(){if(Application.isPlaying && captured)Apply();}
        void Apply()
        {
            float elapsed=Time.unscaledTime-began;
            RevealProgress=Mathf.Clamp01((elapsed-delay)/Mathf.Max(.01f,duration+stagger*Mathf.Max(0,origins.Length-1)));
            if(panelGroup)panelGroup.alpha=1-Mathf.Pow(1-Mathf.Clamp01(elapsed/Mathf.Max(.01f,duration)),3);
            for(int i=0;i<origins.Length;i++)
            {
                float t=Mathf.Clamp01((elapsed-delay-i*stagger)/Mathf.Max(.01f,duration));
                float eased=1-Mathf.Pow(1-t,3);
                if(items[i])items[i].anchoredPosition=origins[i]+offset*(1-eased);
                if(itemGroups!=null && i<itemGroups.Length && itemGroups[i])itemGroups[i].alpha=eased;
            }
        }
        void OnDisable()
        {
            if(!captured)return;
            for(int i=0;i<origins.Length;i++)
            {
                if(items[i])items[i].anchoredPosition=origins[i];
                if(itemGroups!=null && i<itemGroups.Length && itemGroups[i])itemGroups[i].alpha=1;
            }
            if(panelGroup)panelGroup.alpha=1;captured=false;
        }
    }
}
