using System;
using BattleCities.LevelEditor;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BattleCities.EditorTools
{
    [InitializeOnLoad]
    public static class LevelEditorMapLibrary
    {
        public const string Folder="Assets/BattleCities/LevelDrafts/ConceptMaps";
        public static readonly string[] Names={"Canal Crossroads","Timber Ambush","Ironworks Yard"};
        public static readonly string[] Slugs={"canal-crossroads","timber-ambush","ironworks-yard"};
        static LevelEditorMapLibrary() { LevelEditorDocument.LibraryRequested=Show; }
        [MenuItem("Battle Cities/Level Editor/Concept maps/Canal Crossroads")]
        static void Canal()=>Open(0);
        [MenuItem("Battle Cities/Level Editor/Concept maps/Timber Ambush")]
        static void Timber()=>Open(1);
        [MenuItem("Battle Cities/Level Editor/Concept maps/Ironworks Yard")]
        static void Iron()=>Open(2);
        public static void Show(LevelEditorDocument doc)
        {
            if(doc.MapPicker)doc.MapPicker.Toggle();
            else doc.Status("Open the saved LevelEditor scene to use MAPS, or use Battle Cities > Level Editor > Concept maps.");
        }
        public static void Open(int index)
        {
            if(index<0||index>=Slugs.Length)throw new ArgumentOutOfRangeException(nameof(index));
            var asset=AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/"+Slugs[index]+".json");
            if(!asset)throw new InvalidOperationException("Missing concept map: "+Names[index]);
            var doc=UnityEngine.Object.FindAnyObjectByType<LevelEditorDocument>();
            if(!doc){LevelEditorIntegration.Open();doc=UnityEngine.Object.FindAnyObjectByType<LevelEditorDocument>();}
            if(doc&&doc.OpenLibraryMap(asset))
            {
                if(doc.MapPicker)doc.MapPicker.Close();
                LevelEditorIntegration.FrameScene(doc);
            }
        }
        // Explicit migration of the saved authoring scene, never a scene-entry rebuild.
        public static void InstallButton(LevelEditorDocument doc)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Install the persistent MAPS control outside Play mode.");
            var canvas=GameObject.Find("EditorCanvas");
            var workspace=canvas?canvas.transform.Find("Workspace"):null;
            var title=workspace?workspace.Find("Title"):null;
            if(!title)throw new InvalidOperationException("The saved editor title container is missing.");
            var source=title.Find("OPEN").GetComponent<UnityEngine.UI.Button>();
            var button=title.Find("MAPS")?.GetComponent<UnityEngine.UI.Button>();
            if(!button)
            {
                button=UnityEngine.Object.Instantiate(source,title);button.name="MAPS";Undo.RegisterCreatedObjectUndo(button.gameObject,"Add concept map library");
                Place((RectTransform)button.transform,786,10,118,44);
                button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(button.onClick,doc.OpenLibrary);
                button.GetComponentInChildren<TMP_Text>().text="MAPS";
            }
            if(doc.MapPicker&&doc.MapPicker.MenuRoot)return;
            if(workspace.Find("MapMenu"))throw new InvalidOperationException("An existing map menu needs its references repaired; do not duplicate it.");
            Undo.RecordObject(doc,"Connect map picker");
            var picker=doc.GetComponent<LevelEditorMapPicker>();if(!picker)picker=Undo.AddComponent<LevelEditorMapPicker>(doc.gameObject);
            doc.MapPicker=picker;picker.Document=doc;picker.MapsButton=button;
            picker.Maps=Array.ConvertAll(Slugs,slug=>AssetDatabase.LoadAssetAtPath<TextAsset>(Folder+"/"+slug+".json"));

            var overlay=Rect("MapMenu",workspace);overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
            picker.MenuRoot=overlay.gameObject;
            var shade=overlay.gameObject.AddComponent<UnityEngine.UI.Image>();shade.color=new Color(.02f,.05f,.08f,.24f);shade.raycastTarget=true;
            var dismiss=overlay.gameObject.AddComponent<UnityEngine.UI.Button>();dismiss.targetGraphic=shade;dismiss.transition=UnityEngine.UI.Selectable.Transition.None;
            dismiss.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};UnityEventTools.AddPersistentListener(dismiss.onClick,picker.Close);
            var panel=Rect("List",overlay);Place(panel,790,72,400,290);
            var background=panel.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=new Color(.94f,.91f,.82f);background.raycastTarget=true;
            var border=panel.gameObject.AddComponent<UnityEngine.UI.Outline>();border.effectColor=new Color(.15f,.29f,.37f);border.effectDistance=new Vector2(1,-1);border.useGraphicAlpha=true;
            var headingSource=workspace.Find("ElementPalette/Heading").GetComponent<TMP_Text>();
            var heading=UnityEngine.Object.Instantiate(headingSource,panel);heading.name="Heading";heading.text="CONCEPT MAPS";heading.fontSize=24;heading.fontSizeMin=24;heading.fontSizeMax=24;heading.raycastTarget=false;Place(heading.rectTransform,14,8,280,34);
            var close=CloneButton(source,panel,"Close","CLOSE");Place((RectTransform)close.transform,308,10,78,30);var closeText=close.GetComponentInChildren<TMP_Text>();closeText.fontSize=18;closeText.fontSizeMin=18;closeText.fontSizeMax=18;UnityEventTools.AddPersistentListener(close.onClick,picker.Close);
            var choices=new UnityEngine.UI.Button[3];
            for(int i=0;i<Names.Length;i++)
            {
                var choice=CloneButton(source,panel,Slugs[i],Names[i].ToUpperInvariant()+"   /   25 x 25");Place((RectTransform)choice.transform,14,54+i*60,372,50);
                var text=choice.GetComponentInChildren<TMP_Text>();text.fontSize=22;text.fontSizeMin=20;text.fontSizeMax=22;
                UnityEventTools.AddIntPersistentListener(choice.onClick,picker.OpenMap,i);choices[i]=choice;
            }
            picker.FirstChoice=choices[0];
            for(int i=0;i<choices.Length;i++)choices[i].navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=i==0?close:choices[i-1],selectOnDown=i==2?close:choices[i+1]};
            close.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnDown=choices[0],selectOnUp=choices[2]};
            var note=UnityEngine.Object.Instantiate(headingSource,panel);note.name="CopyHint";note.text="Opens an editable copy. Your draft is backed up.";note.fontSize=18;note.fontSizeMin=17;note.fontSizeMax=18;note.raycastTarget=false;Place(note.rectTransform,14,240,372,38);
            foreach(var child in overlay.GetComponentsInChildren<Transform>(true))child.gameObject.layer=5;
            Undo.RegisterCreatedObjectUndo(overlay.gameObject,"Add saved map picker");overlay.gameObject.SetActive(false);
            EditorUtility.SetDirty(picker);EditorUtility.SetDirty(doc);EditorSceneManager.MarkSceneDirty(doc.gameObject.scene);
        }
        static RectTransform Rect(string name,Transform parent)
        {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        static void Place(RectTransform rect,float x,float y,float w,float h)
        {rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);}
        static UnityEngine.UI.Button CloneButton(UnityEngine.UI.Button source,Transform parent,string name,string caption)
        {
            var button=UnityEngine.Object.Instantiate(source,parent);button.name=name;button.onClick=new UnityEngine.UI.Button.ButtonClickedEvent();button.GetComponentInChildren<TMP_Text>().text=caption;return button;
        }
    }
}
