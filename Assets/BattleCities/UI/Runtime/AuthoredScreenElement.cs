using UnityEngine;
using UnityEngine.UI;
namespace BattleCities.UI
{
    [DefaultExecutionOrder(30000),DisallowMultipleComponent]
    public sealed class AuthoredScreenElement : MonoBehaviour
    {
        string screen,key,fallback; Text label;
        public void Configure(string screenId,string contentKey,Text target)
        {
            if(label!=target||screen!=screenId||key!=contentKey)fallback=target.text;
            screen=screenId;key=contentKey;label=target;
        }
        void LateUpdate()=>Apply();
        public void Apply()
        {
            if(!label)return;
            // Live readouts can move with the screen, but their values belong to the API.
            if(!ScreenContent.IsLiveText(screen,key))label.text=ScreenContent.Text(screen,key,fallback);
            if(ScreenContent.TryRegion(screen,key,out var bounds))
            {
                var rect=label.rectTransform;rect.anchorMin=new Vector2(bounds[0],1-bounds[1]-bounds[3]);rect.anchorMax=new Vector2(bounds[0]+bounds[2],1-bounds[1]);
                rect.offsetMin=rect.offsetMax=Vector2.zero;
            }
        }
    }
}
