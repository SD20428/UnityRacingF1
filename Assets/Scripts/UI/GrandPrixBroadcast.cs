using UnityEngine;
using TMPro;
using System.Text;
namespace Overdrive
{
    public sealed class GrandPrixBroadcast : MonoBehaviour
    {
        public RaceManager race;
        public TMP_Text timingTower,classification;
        public UnityEngine.UI.Image[] lights;
        float nextRefresh;
        readonly StringBuilder text=new StringBuilder(2200);
        void Update()
        {
            bool starting=race.State==RaceState.Countdown;
            for(int i=0;i<lights.Length;i++)lights[i].color=starting && i<6-Mathf.CeilToInt(race.CountdownRemaining)?new Color(1,.025f,.035f):new Color(.12f,.035f,.045f);
            if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+.2f;
            if(race.IsQualifying)
            {
                timingTower.text=$"<size=16>QUALIFYING</size>\n\nSOLO / CLEAR TRACK\n\nOUT LAP + 3 TIMED LAPS\n\nBEST LAP\n<size=28>{RaceUI.Format(race.PlayerLaps.BestLap)}</size>\n\nCOMPLETED  {race.TimedLapCount} / 3";
                if(race.State==RaceState.Finished)classification.text=$"<size=18>SESSION BEST</size>\n\nYOU   {RaceUI.Format(race.PlayerLaps.BestLap)}\n\n3 TIMED LAPS COMPLETE";
                return;
            }
            text.Clear();text.Append("<size=12>POS  DRIVER              LAP</size>\n");int position=0;
            foreach(var entry in race.Classification)
            {
                position++;var identity=entry.identity;string code=identity?identity.driverCode:entry.vehicle==race.player?"YOU":"CPU";
                string color=identity?ColorUtility.ToHtmlStringRGB(identity.teamColor):"FFFFFF";
                text.Append(entry.vehicle==race.player?"<b>":"");text.Append(position.ToString("00")).Append("  <color=#").Append(color).Append(">|</color> ").Append(code.PadRight(7));
                text.Append("  ").Append(entry.finish>=0?"FIN":Mathf.Min(2,entry.progress.CompletedLaps+1).ToString());text.Append(entry.vehicle==race.player?"</b>\n":"\n");
            }
            timingTower.text=text.ToString();
            if(race.State==RaceState.Finished)
            {
                text.Clear();text.Append("<size=14>FINAL CLASSIFICATION</size>\n");position=0;
                foreach(var entry in race.Classification)
                {
                    position++;text.Append(position.ToString("00")).Append("  ").Append(entry.identity?entry.identity.driverCode:"CPU").Append("   ").Append(entry.finish>=0?RaceUI.Format(entry.finish):"RUNNING").Append('\n');
                }
                classification.text=text.ToString();
            }
        }
    }
}
