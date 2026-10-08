using BattleCities.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities
{
    // A wallet battle stays at tick zero until its server session is ready.
    public sealed class BattleConnectionNotice : MonoBehaviour
    {
        BattleGame game;
        GameObject surface;
        TMP_Text message;
        Button back;
        float waitingSince;
        readonly ArcadeTextStyles styles=new ArcadeTextStyles();
        public void Initialize(BattleGame owner){game=owner;waitingSince=Time.unscaledTime;}
        void Update()
        {
            bool visible=game&&!game.ReplayReady&&!game.IsReplaying;
            if(!visible){waitingSince=Time.unscaledTime;if(surface)surface.SetActive(false);return;}
            if(Time.unscaledTime-waitingSince<.5f)return;
            if(!surface)Build();
            if(!surface.activeSelf)
            {
                surface.SetActive(true);
                if(EventSystem.current)EventSystem.current.SetSelectedGameObject(back.gameObject);
            }
            message.text=game.ReplayStatus;
        }
        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;return r;
        }
        Image Panel(string name,Transform parent,Vector2 position,Vector2 size,Sprite sprite,Color color)
        {var i=Rect(name,parent,position,size).gameObject.AddComponent<Image>();i.sprite=sprite;i.type=Image.Type.Sliced;i.color=color;return i;}
        TMP_Text Text(string name,Transform parent,string value,Vector2 position,Vector2 size,int fontSize,bool white)
        {
            var t=Rect(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();
            t.font=ArcadeTextStyles.HeadingSdf;t.text=value;t.richText=false;t.fontSize=fontSize;
            t.enableAutoSizing=true;t.fontSizeMin=18;t.fontSizeMax=fontSize;t.alignment=TextAlignmentOptions.Center;
            t.textWrappingMode=TextWrappingModes.Normal;t.raycastTarget=false;styles.ApplyCleanButton(t,!white);return t;
        }
        void Build()
        {
            var art=Resources.Load<PreBattleArt>("PreBattleArt");
            surface=new GameObject("Battle connection",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            surface.transform.SetParent(transform,false);
            var canvas=surface.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=500;
            var scale=surface.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution=new Vector2(1280,720);scale.matchWidthOrHeight=.5f;
            var shade=Panel("Backdrop",surface.transform,Vector2.zero,Vector2.zero,null,new Color(0,.03f,.08f,.55f));
            shade.rectTransform.anchorMin=Vector2.zero;shade.rectTransform.anchorMax=Vector2.one;shade.rectTransform.offsetMin=shade.rectTransform.offsetMax=Vector2.zero;
            var paper=Panel("Connection status",surface.transform,Vector2.zero,new Vector2(580,230),art?art.statusPanel:null,new Color(1,.97f,.89f));
            var title=Panel("Title",paper.transform,new Vector2(0,83),new Vector2(554,46),art?art.tankTitlePanel:null,new Color(.04f,.3f,.7f));
            Text("Heading",title.transform,"CONNECTING BATTLE",Vector2.zero,new Vector2(516,40),30,true);
            message=Text("Message",paper.transform,"Connecting...",new Vector2(0,9),new Vector2(520,80),26,false);
            var button=Panel("Main menu",paper.transform,new Vector2(0,-74),new Vector2(220,46),art?art.tankCostButton:null,Color.white);
            back=button.gameObject.AddComponent<Button>();back.targetGraphic=button;
            var colors=back.colors;colors.highlightedColor=colors.selectedColor=new Color(.65f,.9f,1);colors.pressedColor=new Color(1,.74f,.2f);back.colors=colors;
            back.navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnUp=back,selectOnDown=back,selectOnLeft=back,selectOnRight=back};
            back.onClick.AddListener(()=>SceneManager.LoadScene("MainMenu"));
            Text("Label",button.transform,"MAIN MENU",Vector2.zero,new Vector2(190,40),28,true);
            surface.SetActive(false);
        }
        void OnDestroy(){styles.Dispose();}
    }
}
