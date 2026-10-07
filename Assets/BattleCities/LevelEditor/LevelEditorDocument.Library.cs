using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BattleCities.LevelEditor
{
    public sealed partial class LevelEditorDocument
    {
        public static Action<LevelEditorDocument> LibraryRequested;
        public LevelEditorMapPicker MapPicker;
        public void OpenLibrary()
        {
            if (MapPicker) MapPicker.Toggle();
            else if (LibraryRequested != null) LibraryRequested(this);
            else Status("Map library is unavailable. Open the saved LevelEditor scene.");
        }
        public bool OpenLibraryMap(UnityEngine.TextAsset template)
        {
            if (!template) { Status("Map template is missing."); return false; }
            try
            {
                string json = template.text;
                EndMove();
#if UNITY_EDITOR
                string backup = "Library/LevelEditorMapBackups/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0,8) + ".json";
                WriteJson(backup, ToJson());
#endif
                Import(json); SavePath = ""; SetTool("Select"); FrameMap(); Dirty();
                Status(LevelName + " / Editable copy. Previous draft backed up; Undo restores it.");
                return true;
            }
            catch (Exception error) { Status("Could not open map: " + error.Message); return false; }
        }

        void ImportElementNotes(JToken metadata)
        {
            if (!(metadata?["elements"] is JArray records)) return;
            foreach (var record in records)
            {
                if (!Enum.TryParse((string)record["kind"], out LevelElementKind kind)) continue;
                int index = (int?)record["index"] ?? -1;
                var element = index < 0 ? null : Elements.Where(e => e.Kind == kind).ElementAtOrDefault(index);
                if (!element) continue;
                element.SourceId = (string)record["id"] ?? "";
                element.DesignNotes = (string)record["notes"] ?? "";
                if (!string.IsNullOrEmpty(element.SourceId)) element.name = element.SourceId + " - " + element.Tile;
            }
        }
        JArray ExportElementNotes()
        {
            var records = new JArray();
            foreach (var group in Elements.Where(e => e.gameObject.activeSelf).GroupBy(e => e.Kind))
            {
                int index = 0;
                foreach (var element in group)
                {
                    if (!string.IsNullOrEmpty(element.SourceId) || !string.IsNullOrEmpty(element.DesignNotes))
                        records.Add(new JObject { ["kind"]=group.Key.ToString(), ["index"]=index, ["id"]=element.SourceId, ["notes"]=element.DesignNotes });
                    index++;
                }
            }
            return records;
        }
    }
}
