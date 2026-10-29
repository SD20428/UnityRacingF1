using UnityEngine;
using UnityEngine.EventSystems;

namespace Overdrive
{
    [DefaultExecutionOrder(300)]
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class UIButtonMotion : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,
        IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler,ISubmitHandler
    {
        public UnityEngine.UI.Image accent;
        public TMPro.TMP_Text caption;
        UnityEngine.UI.Button button;Vector3 origin,captionOrigin;bool hovered,selected,pressed;float submitUntil,amount;
        void Awake(){button=GetComponent<UnityEngine.UI.Button>();}
        void OnEnable()
        {
            if(!Application.isPlaying)return;
            origin=transform.localScale;if(caption)captionOrigin=caption.transform.localScale;
            hovered=selected=pressed=false;amount=0;
        }
        void LateUpdate()
        {
            if(!Application.isPlaying || !button)return;
            bool down=(pressed || Time.unscaledTime<submitUntil) && button.IsInteractable();
            float target=button.IsInteractable() && (hovered || selected)?1:0;
            amount=Mathf.Lerp(amount,target,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
            float scale=down?.977f:1+amount*.014f;
            transform.localScale=Vector3.Lerp(transform.localScale,origin*scale,1-Mathf.Exp(-24*Time.unscaledDeltaTime));
            if(accent){var color=accent.color;color.a=.22f+amount*.78f;accent.color=color;}
            if(caption)caption.transform.localScale=captionOrigin*(1+amount*.012f);
        }
        void OnDisable()
        {
            if(!Application.isPlaying)return;
            transform.localScale=origin;if(caption)caption.transform.localScale=captionOrigin;
            hovered=selected=pressed=false;
        }
        public void OnPointerEnter(PointerEventData e){hovered=true;}
        public void OnPointerExit(PointerEventData e){hovered=false;pressed=false;}
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)pressed=true;}
        public void OnPointerUp(PointerEventData e){pressed=false;}
        public void OnSelect(BaseEventData e){selected=true;}
        public void OnDeselect(BaseEventData e){selected=false;pressed=false;}
        public void OnSubmit(BaseEventData e){submitUntil=Time.unscaledTime+.12f;}
    }
}
