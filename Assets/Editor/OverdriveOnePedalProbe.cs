using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Overdrive;
using Object=UnityEngine.Object;

// Historical class name retained for compatibility. Now verifies separate W/S pedals.
public static class OverdriveOnePedalProbe
{
    public static string Run()
    {
        if(!EditorApplication.isPlaying || Time.deltaTime<=0) throw new InvalidOperationException("Ticking PlayMode required");
        var race=Object.FindFirstObjectByType<RaceManager>();race.MainMenu();
        var oldKeyboard=Keyboard.current;var oldPad=Gamepad.current;
        var oldMode=InputSystem.settings.updateMode;
        var inputMode=InputSystem.settings.editorInputBehaviorInPlayMode;
        var background=InputSystem.settings.backgroundBehavior;
        GameObject actor=null;Keyboard keyboard=null;Gamepad pad=null;
        var rows=new List<string>{"stage,action,throttle,brake,pass"};bool all=true;
        try
        {
            actor=Object.Instantiate(race.player.gameObject);actor.name="Temporary W/S input verification";
            actor.transform.position=new Vector3(10000,100,10000);
            var car=actor.GetComponent<OverdriveVehicle>();car.enabled=false;car.ControlsEnabled=true;car.Throttle=car.Brake=0;
            car.Body.useGravity=false;car.Body.constraints=RigidbodyConstraints.FreezeAll;
            actor.GetComponent<CircuitProgress>().enabled=false;foreach(var w in car.wheels)w.enabled=false;
            var input=actor.GetComponent<PlayerVehicleInput>();input.enabled=true;
            var update=typeof(PlayerVehicleInput).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            keyboard=InputSystem.AddDevice<Keyboard>();pad=InputSystem.AddDevice<Gamepad>();
            InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            string[] names={"W throttle","Release W coasts","W+S brake priority","S cannot reverse","W again","Pad throttle","Pad coast","Pad brake","S retakes keyboard","Release S coasts"};
            for(int stage=0;stage<10;stage++)
            {
                keyboard.MakeCurrent();pad.MakeCurrent();
                var keys=stage==0||stage==4?new[]{Key.W}:stage==2?new[]{Key.W,Key.S}:stage==3||stage==8?new[]{Key.S}:new Key[0];
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
                InputSystem.QueueStateEvent(pad,new GamepadState{rightTrigger=stage==5?.8f:0,leftTrigger=stage==7?.65f:0});
                InputSystem.Update();
                int iterations=Mathf.Clamp(Mathf.CeilToInt(2/Time.deltaTime),20,100000);
                for(int i=0;i<iterations;i++) update.Invoke(input,null);
                bool pass=stage==0||stage==4?car.Throttle>.85f&&car.Brake<.001f:
                    stage==2||stage==3||stage==8?Mathf.Abs(car.Throttle)<.001f&&car.Brake>.1f:
                    stage==1||stage==6||stage==9?Mathf.Abs(car.Throttle)<.001f&&car.Brake<.001f:
                    stage==5?Mathf.Abs(car.Throttle-.8f)<.02f&&car.Brake<.001f:
                    stage==7?Mathf.Abs(car.Throttle)<.001f&&car.Brake>.6f:false;
                all&=pass;rows.Add(FormattableString.Invariant($"{stage},{names[stage]},{car.Throttle:F4},{car.Brake:F4},{pass}"));
            }
            rows.Add("overall,"+(all?"PASS":"FAIL"));System.IO.File.WriteAllLines("Docs/SeparatePedal_InputProof.csv",rows);
            return all?"PASS: 10 production InputSystem W/S/gamepad checks":"FAIL: inspect Docs/SeparatePedal_InputProof.csv";
        }
        finally
        {
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);if(pad!=null)InputSystem.RemoveDevice(pad);
            InputSystem.settings.updateMode=oldMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=inputMode;InputSystem.settings.backgroundBehavior=background;
            if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldPad!=null&&oldPad.added)oldPad.MakeCurrent();
            if(actor)Object.DestroyImmediate(actor);
        }
    }
}
