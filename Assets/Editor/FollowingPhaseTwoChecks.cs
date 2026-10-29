using System;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    public static class FollowingPhaseTwoChecks
    {
        [MenuItem("Overdrive/AI/Verify Phase 2 decisions")]
        public static string Run()
        {
            int count = 0;
            Action<bool,string> check = (ok,label) => { if(!ok) throw new Exception(label); count++; };
            var settings = new AIFollowingSettings();
            var decision = new AIFollowingDecision();
            var input = new AIFollowingInput { referenceValid=true, speedMps=15, cruiseKmh=100,
                selfLength=5, leadLength=5, observationAge=0 };
            decision.Reset(0);
            var plan = decision.Evaluate(input,settings,0);
            check(plan.state==AIBehaviourState.RACING && float.IsPositiveInfinity(plan.speedLimitKmh), "Clear road preserves existing cruise");
            input.hasLead=true; input.centreDistance=40; input.leadSpeedMps=10; input.closingSpeedMps=5;
            plan=decision.Evaluate(input,settings,.1f);
            check(plan.state==AIBehaviourState.FOLLOWING && plan.speedLimitKmh<100 && !plan.emergencyBrake, "Slower lead starts following");
            check(Mathf.Abs(plan.bumperGap-35)<.001f, "Uses bumper gap, not centre gap");
            check(Mathf.Abs(plan.ttc-7)<.001f, "Closing speed TTC");
            float freshLimit=plan.speedLimitKmh;
            input.observationAge=.2f; plan=decision.Evaluate(input,settings,.3f);
            check(plan.bumperGap<35 && plan.speedLimitKmh<freshLimit, "Aged observation is conservative");
            input.observationAge=0;
            plan=decision.Evaluate(input,settings,2.2f);
            check(plan.state==AIBehaviourState.PREPARE_PASS && plan.speedLimitKmh<100, "Preparation keeps following limit");
            plan=decision.Evaluate(input,settings,2.3f);
            check(plan.state==AIBehaviourState.PREPARE_PASS, "Minimum state hold");
            plan=decision.Evaluate(input,settings,2.8f);
            check(plan.state==AIBehaviourState.FOLLOWING, "No validated path returns to following");
            plan=decision.Evaluate(input,settings,5);
            check(plan.state==AIBehaviourState.FOLLOWING, "Pass retry cooldown");
            input.centreDistance=10; input.closingSpeedMps=15; input.leadSpeedMps=0;
            plan=decision.Evaluate(input,settings,5.01f);
            check(plan.state==AIBehaviourState.EVADING && plan.emergencyBrake && plan.speedLimitKmh==0, "Emergency bypasses decision tick/hold");
            input.hasLead=false;
            plan=decision.Evaluate(input,settings,5.1f);
            check(plan.emergencyBrake, "Emergency release hysteresis");
            plan=decision.Evaluate(input,settings,5.7f);
            check(plan.state==AIBehaviourState.RACING && !plan.emergencyBrake, "Lead disappears: resume racing after clear delay");
            input.referenceValid=false;
            check(decision.Evaluate(input,settings,6).emergencyBrake, "Invalid sensor stops");
            input.referenceValid=true; input.observationAge=1;
            check(decision.Evaluate(input,settings,7).emergencyBrake, "Stale sensor stops");
            input.observationAge=0; input.speedMps=float.NaN;
            check(decision.Evaluate(input,settings,8).emergencyBrake, "NaN speed stops");
            input.speedMps=0; input.hasLead=true; input.centreDistance=9; input.leadSpeedMps=0; input.closingSpeedMps=0;
            decision.Reset(9); plan=decision.Evaluate(input,settings,9);
            check(plan.state==AIBehaviourState.FOLLOWING && plan.speedLimitKmh==0 && !plan.emergencyBrake && plan.blockedByTraffic,
                "Stopped traffic holds without a recovery request");
            input.centreDistance=float.PositiveInfinity;
            check(decision.Evaluate(input,settings,10).emergencyBrake, "Invalid lead stops");
            input.hasLead=false;
            check(decision.Evaluate(input,null,11).emergencyBrake, "Missing settings stops");
            settings.timeHeadway=float.NaN;
            check(decision.Evaluate(input,settings,12).emergencyBrake, "Invalid settings stops");
            decision.Recover(13);
            check(decision.State==AIBehaviourState.RECOVERY, "Recovery state exposes existing recovery");
            decision.Reset(14);
            check(decision.State==AIBehaviourState.RACING, "Race reset clears state");
            string result=$"PASS: {count} Phase 2 decision checks";
            Debug.Log(result); return result;
        }
    }
}
