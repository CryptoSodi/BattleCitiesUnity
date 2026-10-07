using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace BattleCities.LevelEditor
{
    // Saved Canvas controls, not a native Editor context menu: works in Game view,
    // including when that view is maximized and when domain reload is disabled.
    [DisallowMultipleComponent]
    public sealed class LevelEditorMapPicker : MonoBehaviour
    {
        public LevelEditorDocument Document;
        public GameObject MenuRoot;
        public TextAsset[] Maps;
        public UnityEngine.UI.Button MapsButton;
        public UnityEngine.UI.Button FirstChoice;
        public bool IsOpen => MenuRoot && MenuRoot.activeSelf;

        public void Toggle()
        {
            if (!MenuRoot) { if (Document) Document.Status("Map menu is missing its saved Canvas panel."); return; }
            if (IsOpen) { Close(); return; }
            if (Document) Document.EndMove();
            MenuRoot.SetActive(true);
            MenuRoot.transform.SetAsLastSibling();
            if (EventSystem.current && FirstChoice) EventSystem.current.SetSelectedGameObject(FirstChoice.gameObject);
        }
        public void Close()
        {
            if (MenuRoot) MenuRoot.SetActive(false);
            if (EventSystem.current && MapsButton && MapsButton.isActiveAndEnabled)
                EventSystem.current.SetSelectedGameObject(MapsButton.gameObject);
        }
        public void OpenMap(int index)
        {
            if (!Document) return;
            if (Maps == null || index < 0 || index >= Maps.Length || !Maps[index])
            { Document.Status("This map is missing from the saved library. Check its TextAsset reference."); return; }
            if (Document.OpenLibraryMap(Maps[index])) Close();
        }
        void Update()
        {
            if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }
        void OnDisable() { if (MenuRoot) MenuRoot.SetActive(false); }
    }
}
