using System;
using System.Collections.Generic;
using BattleCities.Core;
using BattleCities.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities
{
    public sealed class ReplayBrowser : MonoBehaviour
    {
        private static ReplayBrowser instance;
        public static bool IsOpen=>instance;
        public static BattleReplay Pending {get;private set;}
        private BattleGame game;
        private bool wasPaused;
        private int page;
        private List<ReplayArchive> archives;
        private readonly List<UnityEngine.UI.Button> rows=new List<UnityEngine.UI.Button>();
        private readonly ArcadeTextStyles styles=new ArcadeTextStyles();
        private RectTransform root,header,footer,body;
        private TMP_Text title,status;
        private UnityEngine.UI.Button back,prev,next,retry,sync;
        private PreBattleArt art;
        private GameObject previousFocus,ownedCanvas;
        public static void Open(BattleGame game)
        {
            if(instance||game&&game.IsOnline)return;
            var host=new GameObject("Replay browser");instance=host.AddComponent<ReplayBrowser>();instance.Build(game);
        }
        public static BattleReplay TakePending(){var data=Pending;Pending=null;return data;}
        public static void Launch(BattleReplay data)
        {ReplayJson.Validate(data);Pending=data;SceneManager.LoadScene("BattleCity");}
        private void Build(BattleGame owner)
        {
            game=owner;wasPaused=game&&game.Paused;if(game)game.Paused=true;
            if(EventSystem.current)previousFocus=EventSystem.current.currentSelectedGameObject;
            art=Resources.Load<PreBattleArt>("PreBattleArt");
            var profile=!game?FindAnyObjectByType<PlayerProfileScreen>():null;
            Transform parent=profile&&profile.IsOpen?profile.Root:null;
            if(parent==null)
            {ownedCanvas=ReplayUi.Canvas("Replay library canvas",300);parent=ownedCanvas.transform;}
            root=ReplayUi.Panel("Replay library",parent,art.statusPanel);root.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            ReplayUi.Fit(root,ownedCanvas?new Rect(.04f,.05f,.92f,.90f):new Rect(0,0,1,1));
            header=ReplayUi.Panel("Title",root,art.tankTitlePanel);header.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier=4;
            title=ReplayUi.Label("Title",header,"RECORDED BATTLES",styles,ArcadeTextTreatment.Gold);
            title.alignment=TextAlignmentOptions.MidlineLeft;
            back=ReplayUi.Button("Back",header,"BACK",Close,styles,art);
            sync=ReplayUi.Button("Sync",header,"SYNC",SyncOnline,styles,art);
            footer=ReplayUi.Panel("Status",root,art.statusPanel);TvStatusFooterLayout.ApplySkin(footer.GetComponent<UnityEngine.UI.Image>(),art.statusPanel);
            status=ReplayUi.Label("Status",footer,"",styles,ArcadeTextTreatment.PrizeAmount);
            ReplayUi.Fit(status.rectTransform,new Rect(.025f,.1f,.72f,.8f));
            retry=ReplayUi.Button("Upload",footer,"RETRY UPLOADS",()=>{BattleReplayService.Instance.RetryPending();status.text="Retrying uploads for the signed-in recording account";},styles,art);TvStatusFooterLayout.PlaceAction((RectTransform)retry.transform);
            body=ReplayUi.Panel("Recordings",root,null);
            prev=ReplayUi.Button("Previous",body,"PREV",()=>{page=Math.Max(0,page-1);Refresh();},styles,art);
            next=ReplayUi.Button("Next",body,"NEXT",()=>{page++;Refresh();},styles,art);
            ReplayUi.Fit((RectTransform)prev.transform,new Rect(.64f,0,.17f,.08f));ReplayUi.Fit((RectTransform)next.transform,new Rect(.82f,0,.17f,.08f));
            for(int i=0;i<6;i++)
            {
                int slot=i;
                var row=ReplayUi.Button("Recording "+i,body,"",()=>Watch(slot),styles,art);
                ReplayUi.Fit((RectTransform)row.transform,new Rect(.01f,.11f+i*.145f,.98f,.128f));rows.Add(row);
            }
            Canvas.ForceUpdateCanvases();Layout();Refresh();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(back.gameObject);
        }
        private void Layout()
        {
            var head=new TvTitleHeaderLayout(root);var foot=new TvStatusFooterLayout(root);
            head.PlaceTitle(header,null,title.rectTransform,true);
            MainMenuScene.Place((RectTransform)back.transform,header.rect.width-130,8,120,head.Height-16);
            MainMenuScene.Place((RectTransform)sync.transform,header.rect.width-256,8,120,head.Height-16);
            title.rectTransform.sizeDelta=new Vector2(Mathf.Max(100,header.rect.width-330),title.rectTransform.sizeDelta.y);
            foot.Place(footer);
            MainMenuScene.Place(body,8,head.Height+16,root.rect.width-16,root.rect.height-head.Height-foot.Height-30);
        }
        private void Refresh()
        {
            archives=BattleReplayStore.List();page=Mathf.Clamp(page,0,Mathf.Max(0,(archives.Count-1)/6));
            for(int i=0;i<rows.Count;i++)
            {
                int index=page*6+i;rows[i].gameObject.SetActive(index<archives.Count);
                if(index>=archives.Count)continue;
                var a=archives[index];var r=a.replay;
                string date=DateTime.TryParse(r.createdAt,out var time)?time.ToLocalTime().ToString("dd MMM HH:mm"):"Saved battle";
                rows[i].GetComponentInChildren<TMP_Text>().text=$"{date}    STAGE {r.levelNumber:00}    {r.mode.ToUpperInvariant()}    {r.durationTicks/60}s\n{r.claimedResult.score:N0} SCORE  ·  {r.completion.ToUpperInvariant()}  ·  {a.uploadStatus}";
            }
            prev.interactable=page>0;next.interactable=(page+1)*6<archives.Count;
            status.text=archives.Count==0?"No saved recordings yet. Play a battle to record it.":$"{archives.Count} LOCAL RECORDINGS  ·  PAGE {page+1}/{Math.Max(1,(archives.Count+5)/6)}";
            var navigation=new List<Selectable[]> {new Selectable[]{sync,back},new Selectable[]{prev,next}};
            foreach(var row in rows)if(row.gameObject.activeSelf)navigation.Add(new Selectable[]{row});
            navigation.Add(new Selectable[]{retry});Psg1UiNavigation.Rows(navigation.ToArray());
        }
        private void Watch(int slot)
        {
            int index=page*6+slot;if(index>=archives.Count)return;
            var recording=archives[index].replay;
            try
            { if(game){var owner=game;Close();owner.PlayReplay(recording);}else{Close();Launch(recording);} }
            catch(Exception e){if(status)status.text="Cannot play recording: "+e.Message;Debug.LogWarning(e.Message);}
        }
        private void SyncOnline()
        {
            var api=FindAnyObjectByType<MainMenuApiClient>();
            string url=api?api.BaseUrl:BattlePreparation.ApiUrl;
            if(string.IsNullOrEmpty(url)){status.text="Sign in from the main menu to sync recordings";return;}
            sync.interactable=false;status.text="Loading your online recordings...";
            BattleReplayService.Instance.DownloadRecent(url,message=>{if(!this)return;sync.interactable=true;Refresh();status.text=message;});
        }
        private void Update()
        {
            if(!root||!root.gameObject.activeInHierarchy){Close();return;}
            Layout();
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true||Gamepad.current?.buttonEast.wasPressedThisFrame==true)Close();
            else Psg1UiNavigation.KeepFocus(root,back);
        }
        public void Close()
        {
            if(game)game.Paused=wasPaused;
            if(EventSystem.current&&previousFocus)EventSystem.current.SetSelectedGameObject(previousFocus);
            if(root)Destroy(root.gameObject);if(ownedCanvas)Destroy(ownedCanvas);Destroy(gameObject);instance=null;
        }
        private void OnDestroy(){styles.Dispose();if(instance==this)instance=null;}
    }

    internal static class ReplayUi
    {
        public static GameObject Canvas(string name,int order)
        {
            var host=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=order;
            var scaler=host.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            if(!EventSystem.current){var system=new GameObject("Replay EventSystem",typeof(EventSystem),typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));system.transform.SetParent(host.transform);}
            return host;
        }
        public static RectTransform Panel(string name,Transform parent,Sprite sprite)
        {
            var host=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));host.transform.SetParent(parent,false);
            var image=host.GetComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=sprite?Color.white:Color.clear;image.raycastTarget=false;
            return (RectTransform)host.transform;
        }
        public static TMP_Text Label(string name,Transform parent,string text,ArcadeTextStyles styles,ArcadeTextTreatment treatment)
        {
            var host=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));host.transform.SetParent(parent,false);
            var label=host.GetComponent<TMP_Text>();label.text=text;label.richText=false;label.fontSize=label.fontSizeMax=28;label.fontSizeMin=16;label.enableAutoSizing=true;
            label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;styles.Apply(label,treatment);Fit(label.rectTransform,new Rect(.04f,.08f,.92f,.84f));return label;
        }
        public static UnityEngine.UI.Button Button(string name,Transform parent,string text,UnityEngine.Events.UnityAction action,ArcadeTextStyles styles,PreBattleArt art)
        {
            var rect=Panel(name,parent,art.tankCostButton);var b=rect.gameObject.AddComponent<UnityEngine.UI.Button>();b.image=rect.GetComponent<UnityEngine.UI.Image>();b.image.raycastTarget=true;b.transition=Selectable.Transition.None;b.onClick.AddListener(action);
            var focus=Panel("Focus",rect,art.tankCostButton);focus.GetComponent<UnityEngine.UI.Image>().color=new Color(.35f,.83f,1);Fit(focus,new Rect(0,0,1,1));
            var caption=Label("Caption",rect,text,styles,ArcadeTextTreatment.PrizeAmount);styles.ApplyCleanButton(caption,false);
            if(text=="BACK")
            {
                var arrow=new GameObject("Arrow",typeof(RectTransform),typeof(CanvasRenderer),typeof(BackTabArrow));arrow.transform.SetParent(rect,false);
                Fit((RectTransform)arrow.transform,new Rect(.09f,.20f,.13f,.60f));arrow.GetComponent<BackTabArrow>().raycastTarget=false;
                Fit(caption.rectTransform,new Rect(.28f,.08f,.68f,.84f));
            }
            rect.gameObject.AddComponent<SettingsControlVisual>().Configure(focus.GetComponent<UnityEngine.UI.Image>(),null,caption,styles);return b;
        }
        public static void Fit(RectTransform rect,Rect bounds)
        {rect.pivot=new Vector2(.5f,.5f);rect.anchorMin=new Vector2(bounds.x,1-bounds.y-bounds.height);rect.anchorMax=new Vector2(bounds.x+bounds.width,1-bounds.y);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
}
