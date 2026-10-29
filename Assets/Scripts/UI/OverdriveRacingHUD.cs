using UnityEngine;
using TMPro;
namespace Overdrive
{
    public sealed class OverdriveRacingHUD : MonoBehaviour
    {
        public RaceManager race;
        public TMP_Text gear,rpm,warning,energy;
        public UnityEngine.UI.Image[] revLights;
        public RectTransform energyBar,throttleBar,brakeBar;
        void Update()
        {
            if(!race)return;var c=race.player;
            gear.text=c.Reversing?"R":c.Gear.ToString();rpm.text=$"{c.EngineRPM:00000} RPM   /   {(c.ManualTransmission?"MANUAL":"AUTO")}";
            energy.text=$"ERS  {c.HybridEnergyMJ/4*100:0}%";
            if(c.DRSOpen)energy.text="DRS OPEN";
            warning.text=c.BrakeLocking?"BRAKE LOCK":c.FrontGripUsage>1.15f?"FRONT SLIP":c.RearWheelspin?"WHEELSPIN":c.ManualTransmission && c.EngineRPM>13500 && c.Gear<8?"SHIFT UP":"";
            var input=c.GetComponent<PlayerVehicleInput>();if(input && Time.unscaledTime<input.NoticeUntil)warning.text=input.ControlNotice;else if(c.ClutchPressed)warning.text="RELEASE CTRL TO LAUNCH";else if(c.OvertakeRequested && c.HybridEnergyMJ>0 && warning.text=="")warning.text="OVERTAKE";
            for(int i=0;i<revLights.Length;i++)revLights[i].color=c.EngineRPM/15000>(i+1f)/revLights.Length
                ?i<5?new Color(.24f,.9f,.78f):i<9?new Color(1,.72f,.24f):new Color(1,.25f,.25f):new Color(.13f,.18f,.22f);
            energyBar.sizeDelta=new Vector2(292*Mathf.Clamp01(c.HybridEnergyMJ/4),4);
            throttleBar.sizeDelta=new Vector2(292*Mathf.Clamp01(c.Throttle),4);
            brakeBar.sizeDelta=new Vector2(292*Mathf.Clamp01(c.Brake),4);
        }
    }
}
