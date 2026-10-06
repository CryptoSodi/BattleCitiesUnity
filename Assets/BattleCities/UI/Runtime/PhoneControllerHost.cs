using System;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;

namespace BattleCities.UI
{
    /// <summary>WebRTC browser controller, exposed as a regular Unity Input System gamepad.</summary>
    public sealed class PhoneControllerHost : MonoBehaviour
    {
        static PhoneControllerHost instance;
        Gamepad device;double lastPacket;bool hasInput;bool acceptsInput=true;
        public bool Supported {
            get {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
        public bool Started {get;private set;}
        public string Status {get;private set;}="NOT CONNECTED";
        public string RoomCode {get;private set;}="";
        public Texture2D QrTexture {get;private set;}
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void BattleCitiesPhoneStart(string target,string controllerUrl);
        [DllImport("__Internal")] static extern void BattleCitiesPhoneStop();
#endif
        public static PhoneControllerHost Ensure()
        {
            if(!instance)instance=FindFirstObjectByType<PhoneControllerHost>();
            if(!instance){var go=new GameObject("Battle Cities phone controller");DontDestroyOnLoad(go);instance=go.AddComponent<PhoneControllerHost>();}
            return instance;
        }
        public void Begin(bool restart=false)
        {
            if(!Supported||Started&&!restart)return;
            Stop();Started=true;Status="CREATING PAIRING...";
#if UNITY_WEBGL && !UNITY_EDITOR
            BattleCitiesPhoneStart(gameObject.name,Application.streamingAssetsPath+"/BattleCitiesPhone/index.html");
#endif
        }
        public void Stop()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BattleCitiesPhoneStop();
#endif
            ResetInput();if(device!=null&&device.added)InputSystem.RemoveDevice(device);device=null;
            Started=false;RoomCode="";Status="NOT CONNECTED";
            if(QrTexture)Destroy(QrTexture);QrTexture=null;
        }
        // Called only by the bundled WebGL bridge. Data is validated before reaching Input System.
        public void OnPhoneEvent(string json)
        {
            if(string.IsNullOrEmpty(json)||json.Length>160000)return;
            try
            {
                var value=JObject.Parse(json);string type=(string)value["type"];
                if(type=="ready")
                {
                    string data=(string)value["qr"],room=(string)value["room"];
                    if(string.IsNullOrEmpty(room)||room.Length>16||data==null||!data.StartsWith("data:image/png;base64,"))return;
                    var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    if(!texture.LoadImage(Convert.FromBase64String(data.Substring(22)))){Destroy(texture);return;}
                    texture.filterMode=FilterMode.Point;if(QrTexture)Destroy(QrTexture);QrTexture=texture;RoomCode=room;Started=true;Status="NOT CONNECTED";
                }
                else if(type=="state")ReceiveState(value);
                else if(type=="closed"){ResetInput();Status="NOT CONNECTED";}
                else if(type=="error")
                {
                    ResetInput();Started=false;Status="PAIRING UNAVAILABLE - RETRY";
                    if(QrTexture)Destroy(QrTexture);QrTexture=null;RoomCode="";
                }
            }
            catch(ArgumentException){ResetInput();}
            catch(Newtonsoft.Json.JsonException){ResetInput();}
            catch(FormatException){ResetInput();}
        }
        void ReceiveState(JObject data)
        {
            if(!acceptsInput)return;
            var axes=data["axes"] as JArray;var buttons=data["buttons"] as JArray;
            if(axes==null||axes.Count<2||axes.Count>8||buttons==null||buttons.Count>32)return;
            foreach(var axis in axes)if(axis.Type!=JTokenType.Float&&axis.Type!=JTokenType.Integer)return;
            float Axis(int i){float n=i<axes.Count?(float)axes[i]:0;return float.IsNaN(n)||float.IsInfinity(n)?0:Mathf.Clamp(n,-1,1);}
            bool Press(int i)=>i<buttons.Count&&buttons[i].Type==JTokenType.Boolean&&(bool)buttons[i];
            var state=new GamepadState{leftStick=new Vector2(Axis(0),-Axis(1)),rightStick=new Vector2(Axis(2),-Axis(3))};
            var mapping=new[]{GamepadButton.South,GamepadButton.East,GamepadButton.West,GamepadButton.North,GamepadButton.LeftShoulder,GamepadButton.RightShoulder,GamepadButton.LeftTrigger,GamepadButton.RightTrigger,GamepadButton.Select,GamepadButton.Start,GamepadButton.LeftStick,GamepadButton.RightStick,GamepadButton.DpadUp,GamepadButton.DpadDown,GamepadButton.DpadLeft,GamepadButton.DpadRight};
            for(int i=0;i<mapping.Length;i++)if(i!=6&&i!=7&&Press(i))state=state.WithButton(mapping[i]);
            state.leftTrigger=Press(6)?1:0;state.rightTrigger=Press(7)?1:0;
            if(device==null||!device.added)device=InputSystem.AddDevice<Gamepad>("Battle Cities phone");
            InputSystem.QueueStateEvent(device,state);lastPacket=Time.realtimeSinceStartupAsDouble;hasInput=true;Status="PHONE CONNECTED";
        }
        void ResetInput(){if(device!=null&&device.added)InputSystem.QueueStateEvent(device,new GamepadState());hasInput=false;}
        void Update(){if(hasInput&&Time.realtimeSinceStartupAsDouble-lastPacket>1){ResetInput();Status="CONNECTION LOST";}}
        void OnApplicationFocus(bool focused){acceptsInput=focused;if(!focused)ResetInput();}
        void OnDestroy(){Stop();if(instance==this)instance=null;}
    }
}
