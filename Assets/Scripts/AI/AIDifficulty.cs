using UnityEngine;
namespace Overdrive
{
    [CreateAssetMenu(menuName="Overdrive/AI Difficulty")]
    public sealed class AIDifficulty : ScriptableObject
    {
        [Min(10)] public float maximumSpeedKmh = 65;
        [Min(5)] public float cornerSpeedKmh = 30;
        [Min(1)] public float lookaheadBase = 10;
        [Range(0,.5f)] public float lookaheadPerKmh = .15f;
        [Min(.1f)] public float steeringResponse = 8;
        [Min(1)] public float brakingLookahead = 40;
        [Min(1)] public float lateralAcceleration = 8;
        [Min(1)] public float stuckSeconds = 6;
        [Min(1)] public float recoveryPenaltySeconds = 3;
        [Header("F1 driver mode")]
        public bool professionalDriver;
        public DrivingGuideTuning racingProfile=new DrivingGuideTuning();
        void OnValidate()
        {
            maximumSpeedKmh=Mathf.Max(10,maximumSpeedKmh);
            cornerSpeedKmh=Mathf.Clamp(cornerSpeedKmh,5,maximumSpeedKmh);
            stuckSeconds=Mathf.Max(1,stuckSeconds);
            recoveryPenaltySeconds=Mathf.Max(1,recoveryPenaltySeconds);
        }
    }
}
