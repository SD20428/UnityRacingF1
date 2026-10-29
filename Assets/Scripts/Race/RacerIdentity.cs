using UnityEngine;
namespace Overdrive
{
    public sealed class RacerIdentity : MonoBehaviour
    {
        public string teamName="APEX",driverCode="YOU";
        public int carNumber=27,gridSlot;
        public Color teamColor=Color.cyan;
        public Vector3 gridPosition;
        public Quaternion gridRotation=Quaternion.identity;
        public int gridCheckpoint;
        public bool startsBeforeLine;
    }
}
