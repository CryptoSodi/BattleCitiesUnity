using UnityEngine;

namespace BattleCities.UI
{
    public sealed partial class MainMenuScene
    {
        ShopScreen shop;
        public bool IsShopOpen
        {
            get {var screen=mainFrame?mainFrame.Find("Shop screen"):null;return screen&&screen.gameObject.activeSelf;}
        }
        bool IsTvScreenOpen
        {
            get {var screen=mainFrame?mainFrame.Find("Pre-battle screens"):null;return IsShopOpen||(screen&&screen.gameObject.activeSelf);}
        }
        void EnsureShop()
        {
            if(!shop)shop=GetComponent<ShopScreen>();
            if(!shop)shop=gameObject.AddComponent<ShopScreen>();
            if(!shop.IsConfigured)shop.Configure(this,theme,apiClient,mainFrame);
        }
        public void OpenShop()
        {
            EnsureApiClient();
            if(preBattle&&preBattle.IsOpen)preBattle.Back();
            EnsureShop();shop.Open();
        }
        void LayoutShopScreen()
        {
            var screen=mainFrame?mainFrame.Find("Shop screen") as RectTransform:null;
            if(!screen)return;
            var opening=mainFrame.Find("TV Background Viewport") as RectTransform;
            if(opening)
            {
                const float clearance=12f;
                var min=mainFrame.InverseTransformPoint(opening.TransformPoint(new Vector3(opening.rect.xMin,opening.rect.yMin)));
                var max=mainFrame.InverseTransformPoint(opening.TransformPoint(new Vector3(opening.rect.xMax,opening.rect.yMax)));
                screen.localScale=Vector3.one;
                Place(screen,min.x-mainFrame.rect.xMin+clearance,mainFrame.rect.yMax-max.y+clearance,
                    Mathf.Max(1,max.x-min.x-clearance*2),Mathf.Max(1,max.y-min.y-clearance*2));
            }
            if(!shop)shop=GetComponent<ShopScreen>();
            if(shop&&shop.IsConfigured)shop.ApplyLayout(lastPlatform);
        }
        internal void ApplyShopInventorySurface(RectTransform frame,RectTransform paper)
        {
            var source=howItWorks?howItWorks.GetComponent<UnityEngine.UI.Image>():null;
            var image=frame.GetComponent<UnityEngine.UI.Image>();
            if(source&&source.sprite){image.sprite=source.sprite;image.pixelsPerUnitMultiplier=source.pixelsPerUnitMultiplier;}
            image.enabled=true;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=Color.white;
            float pixelsPerUnit=image.pixelsPerUnit*image.pixelsPerUnitMultiplier;
            var inset=new Vector4(114f,138f,113f,126f)/Mathf.Max(1f,pixelsPerUnit);
            paper.anchorMin=Vector2.zero;paper.anchorMax=Vector2.one;
            paper.offsetMin=new Vector2(inset.x,inset.y);paper.offsetMax=new Vector2(-inset.z,-inset.w);
            foreach(var name in new[]{"TV Background","TV White Fog"})
            {
                var backgroundLayer=paper.Find(name);if(backgroundLayer)backgroundLayer.gameObject.SetActive(true);
            }
            var backdrop=transform.Find("World backdrop") as RectTransform;
            var background=backdrop?backdrop.GetComponent<UnityEngine.UI.Image>():null;
            var television=mainFrame.Find("TV Background Viewport/TV Background")?.GetComponent<UnityEngine.UI.Image>();
            ApplyPaperBackdrop(paper,backdrop,background?background.sprite:null,television?television.material:null,
                ref inventoryBackdropMaterial,46f/Mathf.Max(1f,pixelsPerUnit));
        }
    }
}
