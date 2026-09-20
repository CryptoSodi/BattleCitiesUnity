using System;
using BattleCities.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BattleCities.Editor
{
    public static class ReferenceStartArt
    {
        public static void ApplyTo(RectTransform start,MenuTheme theme)
        {
            var inactive=AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuBuilder.Root+"Art/reference-style-v2/shared/buttons/start/inactive.png");
            var active=AssetDatabase.LoadAssetAtPath<Sprite>(MainMenuBuilder.Root+"Art/reference-style-v2/shared/buttons/start/active.png");
            if(!inactive||!active)throw new InvalidOperationException("Import Start button states first.");
            var image=start.GetComponent<Image>();
            image.sprite=inactive;image.type=Image.Type.Simple;image.preserveAspect=true;image.color=Color.white;
            theme.PlayButton=inactive;EditorUtility.SetDirty(theme);
            var label=start.Find("Editable label");if(label)label.gameObject.SetActive(false);
            var visual=start.GetComponent<MenuButtonVisual>();
            visual.Configure(null,start);
            visual.ConfigureSkins(image,inactive,active,false);
            var copy=UnityEngine.Object.Instantiate(start.gameObject);copy.name="PlayButton";
            var button=copy.GetComponent<Button>();button.onClick=new Button.ButtonClickedEvent();
            button.navigation=new Navigation{mode=Navigation.Mode.Automatic};
            PrefabUtility.SaveAsPrefabAsset(copy,MainMenuBuilder.Root+"Prefabs/Shared/PlayButton.prefab");
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }
}
