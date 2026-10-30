using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class Psg1KeyboardNavigationChecks
    {
        public static void Run()
        {
            const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
            var originalEvents=EventSystem.current;
            var root=new GameObject("PSG1 keyboard navigation fixture");
            var keyboard=InputSystem.AddDevice<Keyboard>();
            var settings=InputSystem.settings;
            const string feature="RUN_PLAYER_UPDATES_IN_EDIT_MODE";
            bool previous=(bool)typeof(InputSettings).GetMethod("IsFeatureEnabled",Private).Invoke(settings,new object[]{feature});
            Psg1UiInput input=null;
            void Check(bool valid,string message){if(!valid)throw new Exception("PSG1 KEYBOARD NAVIGATION: "+message);}
            try{
                settings.SetInternalFeatureFlag(feature,true);
                var events=root.AddComponent<EventSystem>();
                typeof(EventSystem).GetMethod("OnEnable",Private).Invoke(events,null);
                var module=root.AddComponent<InputSystemUIInputModule>();module.AssignDefaultActions();
                EventSystem.current=events;
                var originalSubmit=module.submit;var originalMove=module.move;
                input=new Psg1UiInput();
                var asset=(InputActionAsset)typeof(Psg1UiInput).GetField("actions",Private).GetValue(input);
                var actions=asset.FindActionMap("PSG1 screen",true);
                void State(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();}
                foreach(var key in new[]{Key.UpArrow,Key.W,Key.DownArrow,Key.S,Key.LeftArrow,Key.A,Key.RightArrow,Key.D}){
                    State(key);var expected=key==Key.UpArrow||key==Key.W?Vector2.up:key==Key.DownArrow||key==Key.S?Vector2.down:key==Key.LeftArrow||key==Key.A?Vector2.left:Vector2.right;
                    Check(actions["Move"].ReadValue<Vector2>()==expected,"menu direction "+key);State();
                }
                var first=new GameObject("Continue",typeof(RectTransform),typeof(Button)).GetComponent<Button>();first.transform.SetParent(root.transform);
                var disabled=new GameObject("Unavailable",typeof(RectTransform),typeof(Button)).GetComponent<Button>();disabled.transform.SetParent(root.transform);disabled.interactable=false;
                var second=new GameObject("Back",typeof(RectTransform),typeof(Button)).GetComponent<Button>();second.transform.SetParent(root.transform);
                Psg1UiNavigation.Rows(new Selectable[]{first,disabled,second});
                Psg1UiNavigation.KeepFocus(root.transform,first);
                State(Key.RightArrow);
                var direction=actions["Move"].ReadValue<Vector2>();
                ExecuteEvents.Execute(first.gameObject,new AxisEventData(events){moveVector=direction,moveDir=MoveDirection.Right},ExecuteEvents.moveHandler);
                Check(events.currentSelectedGameObject==second.gameObject,"focus skips unavailable controls");State();
                int selected=0;second.onClick.AddListener(()=>selected++);
                State(Key.Enter);Check(actions["Submit"].WasPressedThisFrame(),"Enter uses PSG1 A submit action");
                ExecuteEvents.Execute(second.gameObject,new BaseEventData(events),ExecuteEvents.submitHandler);
                Check(selected==1,"selected control activates once");State();
                State(Key.Escape);Check(input.CancelPressed,"Escape uses PSG1 B cancel action");State();
                Check(module.cancel==null,"cancel stays screen-owned, preventing duplicate Back");
                input.Dispose();input=null;
                Check(module.submit==originalSubmit&&module.move==originalMove,"original menu actions restored on disposal");
                Debug.Log("PSG1 KEYBOARD NAVIGATION PASS: 8 directions, focus, disabled-control skip, Enter activation, Escape cancel and action restoration.");
            }finally{
                input?.Dispose();InputSystem.RemoveDevice(keyboard);UnityEngine.Object.DestroyImmediate(root);
                if(originalEvents)EventSystem.current=originalEvents;settings.SetInternalFeatureFlag(feature,previous);
            }
            BattleControlChecks.Run();
        }
    }
}
