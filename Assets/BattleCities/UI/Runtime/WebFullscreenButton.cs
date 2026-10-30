using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleCities.UI
{
    // Fullscreen must be requested on press, so Unity can defer it to browser pointer-up.
    public sealed class WebFullscreenButton : Button
    {
        readonly List<Image> glyph=new List<Image>();
        Sprite blue,gold;
        Sprite frame;
        Image framedArtwork;
        WebFullscreenArt art;
        bool hovered,focused,pressed,lastFullscreen;
        public static float TargetSize(bool mobile,float pixelScale,float canvasScale)=>mobile?64*Mathf.Max(1,pixelScale)/Mathf.Max(.001f,canvasScale):48;
        public static bool Supported {
            get {
#if UNITY_WEBGL && !UNITY_EDITOR
                return BattleCitiesFullscreenAvailable()!=0;
#elif UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern int BattleCitiesFullscreenAvailable();
#endif
        public void Initialize(PreBattleArt art)
        {
            blue=art.tankCostButton;gold=art.tankCostButtonSelected;
            this.art=Resources.Load<WebFullscreenArt>("WebFullscreenArt");
            transition=Transition.None;
            var surface=GetComponent<Image>();surface.sprite=blue;surface.type=Image.Type.Sliced;surface.raycastTarget=true;targetGraphic=surface;
            foreach(var image in GetComponentsInChildren<Image>())if(image.gameObject!=gameObject)glyph.Add(image);
            if(glyph.Count==0)foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
            {
                AddStroke(new Vector2(24+x*9,y*9),new Vector2(9,3));
                AddStroke(new Vector2(24+x*12,y*6),new Vector2(3,9));
            }
            Refresh();
        }
        void AddStroke(Vector2 position,Vector2 size)
        {
            var stroke=new GameObject("Fullscreen icon",typeof(RectTransform),typeof(Image));stroke.transform.SetParent(transform,false);
            var rect=(RectTransform)stroke.transform;rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
            var image=stroke.GetComponent<Image>();image.raycastTarget=false;glyph.Add(image);
        }
        public void FitSkin()
        {
            Refresh();
        }
        public void SetFrame(Sprite sprite)
        {
            frame=sprite;
            if(frame&&!framedArtwork){
                var child=new GameObject("Framed fullscreen artwork",typeof(RectTransform),typeof(Image));
                child.transform.SetParent(transform,false);
                var rect=(RectTransform)child.transform;rect.anchorMin=new Vector2(.12f,.12f);rect.anchorMax=new Vector2(.88f,.88f);rect.offsetMin=rect.offsetMax=Vector2.zero;
                framedArtwork=child.GetComponent<Image>();framedArtwork.raycastTarget=false;framedArtwork.preserveAspect=true;
            }
            if(framedArtwork)framedArtwork.enabled=frame;
            Refresh();
        }
        void FitFallbackSkin()
        {
            var surface=(Image)targetGraphic;if(!surface||!blue)return;
            float height=((RectTransform)transform).rect.height,width=((RectTransform)transform).rect.width;
            float corner=Mathf.Max(1,Mathf.Min(height*.22f,width*.22f));
            var border=blue.border;float source=Mathf.Max(Mathf.Max(border.x,border.y),Mathf.Max(border.z,border.w));
            surface.pixelsPerUnitMultiplier=Mathf.Max(.01f,source/(corner*surface.pixelsPerUnit));
        }
        void Refresh()
        {
            if(!targetGraphic)return;
            bool active=IsInteractable()&&(hovered||focused||pressed);
            lastFullscreen=Screen.fullScreen;
            var artwork=art?art.ForState(lastFullscreen,active):null;
            var surface=(Image)targetGraphic;
            if(frame){
                surface.sprite=frame;surface.type=Image.Type.Simple;surface.preserveAspect=true;
                framedArtwork.sprite=artwork?artwork:active?gold:blue;
                framedArtwork.type=artwork?Image.Type.Simple:Image.Type.Sliced;
                framedArtwork.preserveAspect=artwork;
            }else{
                surface.sprite=artwork?artwork:active?gold:blue;
                surface.type=artwork?Image.Type.Simple:Image.Type.Sliced;surface.preserveAspect=artwork;
            }
            foreach(var stroke in glyph){stroke.enabled=!artwork;stroke.color=active?ArcadeTextStyles.PrizeAmountFace:Color.white;}
            if(!artwork&&!frame)FitFallbackSkin();
        }
        void Update(){if(lastFullscreen!=Screen.fullScreen)Refresh();}
        void ToggleFullscreen()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Screen.fullScreen=!Screen.fullScreen;
#endif
        }
        public override void OnPointerDown(PointerEventData data){base.OnPointerDown(data);if(data.button!=PointerEventData.InputButton.Left||!IsActive()||!IsInteractable())return;pressed=true;Refresh();ToggleFullscreen();}
        public override void OnPointerUp(PointerEventData data){base.OnPointerUp(data);pressed=false;Refresh();}
        public override void OnSubmit(BaseEventData data){if(IsActive()&&IsInteractable())ToggleFullscreen();base.OnSubmit(data);}
        public override void OnPointerEnter(PointerEventData data){base.OnPointerEnter(data);hovered=true;Refresh();}
        public override void OnPointerExit(PointerEventData data){base.OnPointerExit(data);hovered=false;Refresh();}
        public override void OnSelect(BaseEventData data){base.OnSelect(data);focused=true;Refresh();}
        public override void OnDeselect(BaseEventData data){base.OnDeselect(data);focused=false;Refresh();}
        protected override void OnDisable(){base.OnDisable();hovered=focused=pressed=false;Refresh();}
    }
}
