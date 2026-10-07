using System;
using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace BattleCities.LevelEditor
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed partial class LevelEditorDocument : MonoBehaviour
    {
        [Header("Level")]
        public string LevelName = "Stage 01 - Draft";
        [Range(4, 40)] public int WidthTiles = 13, HeightTiles = 13;
        [Range(1, 35), Tooltip("Campaign slot used only for existing visual/theme defaults during testing.")]
        public int PreviewStage = 1;
        public EnemySpec[] Enemies = new EnemySpec[0];
        [Header("Brush")]
        public string Brush = "brick";
        public LevelElementKind BrushKind = LevelElementKind.Terrain;
        [Tooltip("Select, Paint or Erase")]
        public string Tool = "Select";
        public int Snap = 32;
        [Header("Saved scene references")]
        public Transform MapRoot;
        public Camera PreviewCamera;
        public UnityEngine.UI.RawImage Viewport;
        public TMP_Text StatusLabel, SelectionLabel, ToolLabel;
        public TMP_InputField HealthField;
        public UnityEngine.UI.Toggle DamageToggle, InvulnerableToggle, NormalToggle, PowerToggle;
        public GameObject BrickModel, SteelModel, BushModel, EagleModel, TankModel;
        public Material[] PreviewMaterials;
        public string[] MaterialKeys;
        public RenderTexture PreviewTexture;
        [HideInInspector] public string SavePath = "";
        [HideInInspector, TextArea] public string ImportedJson = "{}";
        [HideInInspector] public LevelElement Selected;
        [NonSerialized] private bool updatingPanel;
        [NonSerialized] public bool BuildPreviews = true;
        [NonSerialized] public bool TrackUndo = true;
        [NonSerialized] private float lastWidth, lastHeight;
        [NonSerialized] private string lastSelectionState;
        public static Action<LevelEditorDocument> TestRequested;

        public LevelElement[] Elements => MapRoot ? MapRoot.GetComponentsInChildren<LevelElement>(true) : Array.Empty<LevelElement>();
        public void Status(string message) { if (StatusLabel) StatusLabel.text = message; }
        private void OnEnable()
        {
            if (PreviewCamera && PreviewTexture) PreviewCamera.targetTexture = PreviewTexture;
            if (Viewport && PreviewTexture) Viewport.texture = PreviewTexture;
            lastSelectionState = null;
        }
        private void Update()
        {
            if (WidthTiles != lastWidth || HeightTiles != lastHeight) { FrameMap(); lastWidth = WidthTiles; lastHeight = HeightTiles; }
#if UNITY_EDITOR
            if (!Application.isPlaying && Selection.activeGameObject)
            {
                var element = Selection.activeGameObject.GetComponentInParent<LevelElement>();
                if (element && element.GetComponentInParent<LevelEditorDocument>() == this && Selected != element) Select(element);
            }
#endif
            string state = (Selected ? JsonUtility.ToJson(Selected) + Selected.transform.localPosition : "none") + "|" + BrushProperties + "|" + Brush + "|" + Snap + "|" + WidthTiles + "|" + HeightTiles;
            if (state != lastSelectionState) { lastSelectionState = state; RefreshSelection(); }
            if (ToolLabel) ToolLabel.text = Tool.ToUpperInvariant() + "  /  " + LevelEditorCatalog.DisplayLabel(Brush) + "  /  " + Snap + " UNITS";
        }
        public void FrameMap()
        {
            if (!PreviewCamera) return;
            float aspect = PreviewTexture ? (float)PreviewTexture.width / PreviewTexture.height : 1;
            PreviewCamera.transform.position = new Vector3(WidthTiles * .5f, 35, -HeightTiles * .5f);
            PreviewCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            PreviewCamera.orthographicSize = Mathf.Max(HeightTiles, WidthTiles / aspect) * .5f + .7f;
        }
        public void Select(LevelElement element)
        {
            if (Selected != element) BuildingPage = "TYPE";
            Selected = element; BrushProperties = false;
#if UNITY_EDITOR
            if (element && TrackUndo) Selection.activeGameObject = element.gameObject;
#endif
            RefreshSelection();
        }
        public void RefreshSelection()
        {
            updatingPanel = true;
            bool supported = Selected && Selected.SupportsDamage;
            if (SelectionLabel) SelectionLabel.text = Selected ? Selected.Kind.ToString().ToUpperInvariant() + " / " + Selected.Tile.ToUpperInvariant() +
                "\nPOSITION  " + Mathf.RoundToInt(Selected.X) + ", " + Mathf.RoundToInt(Selected.Y) +
                "\nSIZE  " + Selected.Size.x + " x " + Selected.Size.y +
                (supported ? "\nDamage overrides affect playtests." : "\nUse the Unity Inspector for all properties.") : "SELECT AN ELEMENT\n\nClick a map element or choose it in the Hierarchy.\n\nMove and resize with the Unity Inspector.";
            if (DamageToggle) { DamageToggle.interactable = supported; DamageToggle.SetIsOnWithoutNotify(supported && Selected.OverrideDamage); }
            bool edit = supported && Selected.OverrideDamage;
            if (HealthField) { HealthField.interactable = edit; HealthField.SetTextWithoutNotify(supported ? Selected.Damage.hitPoints.ToString() : "1"); }
            if (InvulnerableToggle) { InvulnerableToggle.interactable = edit; InvulnerableToggle.SetIsOnWithoutNotify(supported && Selected.Damage.invulnerable); }
            if (NormalToggle) { NormalToggle.interactable = edit; NormalToggle.SetIsOnWithoutNotify(!supported || Selected.Damage.normalShots); }
            if (PowerToggle) { PowerToggle.interactable = edit; PowerToggle.SetIsOnWithoutNotify(!supported || Selected.Damage.powerShots); }
            RefreshPropertyContext();
            updatingPanel = false;
        }
        public void SetDamageOverride(bool value) => EditDamage(() => Selected.OverrideDamage = value);
        public void SetInvulnerable(bool value) => EditDamage(() => Selected.Damage.invulnerable = value);
        public void SetNormal(bool value) => EditDamage(() => Selected.Damage.normalShots = value);
        public void SetPower(bool value) => EditDamage(() => Selected.Damage.powerShots = value);
        public void SetHealth(string value)
        {
            if (!int.TryParse(value, out int health) || health < 1 || health > 9999) { RefreshSelection(); return; }
            EditDamage(() => Selected.Damage.hitPoints = health);
        }
        private void EditDamage(Action edit)
        {
            if (updatingPanel || !Selected || !Selected.SupportsDamage) return;
#if UNITY_EDITOR
            if (TrackUndo) Undo.RecordObject(Selected, "Change level damage");
#endif
            edit(); Dirty(); RefreshSelection();
        }
        public void SetTool(string value) { EndMove(); Tool = value; if (value == "Select") BrushProperties = false; RefreshSelection(); Status(value == "Select" ? "Select: click and drag an item to move it. Escape cancels." : value + " tool selected."); }
        public void ChooseBrush(string key)
        {
            EndMove();
            var parts = (key ?? "").Split(':');
            if (parts.Length != 2 || !Enum.TryParse(parts[0], out LevelElementKind kind) || !LevelEditorCatalog.Allows(kind, parts[1])) { Status("Choose an element used by the campaign maps."); return; }
            BrushKind = kind; Brush = kind == LevelElementKind.Terrain && parts[1] == "water" ? SurfaceBrush : parts[1]; Tool = "Paint";
            if (kind == LevelElementKind.Terrain && LevelEditorCatalog.IsSurface(Brush)) SurfaceBrush = Brush;
            if (kind == LevelElementKind.Prop && LevelEditorCatalog.IsTree(Brush))
            {
                Brush = parts[1] == LevelEditorCatalog.TreeTypes[0] && LevelEditorCatalog.IsTree(TreeBrush) ? TreeBrush : parts[1];
                TreeBrush = Brush;
            }
            if (kind == LevelElementKind.Prop && LevelEditorCatalog.IsBuilding(Brush))
            {
                Brush = parts[1] == LevelEditorCatalog.DefaultBuilding && LevelEditorCatalog.IsBuilding(BuildingBrush) ? BuildingBrush : parts[1];
                BuildingBrush = Brush; BuildingPage = "TYPE";
            }
            Selected = null; BrushProperties = true;
#if UNITY_EDITOR
            if (TrackUndo) Selection.activeGameObject = gameObject;
#endif
            RefreshSelection();
            Status("Paint " + LevelEditorCatalog.DisplayLabel(Brush) + ". Drag on the map; right-click erases.");
        }
        public void CycleSnap() { SetTileSize(Snap == 64 ? 32 : Snap == 32 ? 16 : 64); }
        public void UndoEdit()
        {
            EndMove();
#if UNITY_EDITOR
            Undo.PerformUndo();
#endif
        }
        public void RedoEdit()
        {
            EndMove();
#if UNITY_EDITOR
            Undo.PerformRedo();
#endif
        }
        public void DeleteSelected()
        {
            EndMove();
            if (!Selected) return;
            Remove(Selected); Selected = null; Dirty(); RefreshSelection();
        }
        public void NewLevel()
        {
#if UNITY_EDITOR
            if (!EditorUtility.DisplayDialog("New level", "Replace this draft? Save JSON first if you want to keep a separate copy. This action can be undone.", "New level", "Cancel")) return;
#endif
            var map = new MapData { field = new FieldData(), terrain = new Core.TerrainData { regions = Array.Empty<Region>() },
                spawn = new SpawnData { enemy = new SpawnGroup { list = Enumerable.Range(0, 20).Select(_ => new EnemySpec()).ToArray() } } };
            Import(JsonConvert.SerializeObject(map)); SavePath = ""; LevelName = "Untitled level"; Dirty();
        }
        public void OpenMap()
        {
#if UNITY_EDITOR
            var path = EditorUtility.OpenFilePanel("Open a map or draft", "Assets/BattleCities/Resources/Maps", "json");
            if (string.IsNullOrEmpty(path)) return;
            try { Import(System.IO.File.ReadAllText(path)); LevelName = System.IO.Path.GetFileNameWithoutExtension(path) + " - Draft"; SavePath = ""; Status("Opened a copy. Save creates a separate draft."); }
            catch (Exception e) { Status("Could not open: " + e.Message); }
#endif
        }
        public void Save() => SaveDraft(false);
        public void SaveAs() => SaveDraft(true);
        private void SaveDraft(bool choose)
        {
#if UNITY_EDITOR
            try
            {
                var errors = Validate();
                if (errors.Count > 0) { Status("Fix before saving: " + errors[0]); Debug.LogWarning(string.Join("\n", errors), this); return; }
                string path = SavePath;
                if (choose || string.IsNullOrEmpty(path)) path = EditorUtility.SaveFilePanel("Save level draft", "Assets/BattleCities/LevelDrafts", LevelName, "json");
                if (string.IsNullOrEmpty(path)) return;
                WriteJson(path, ToJson()); SavePath = path; Dirty(); AssetDatabase.Refresh(); Status("Saved " + System.IO.Path.GetFileName(path));
            }
            catch (Exception e) { Status("Save failed: " + e.Message); }
#endif
        }
        public static void WriteJson(string path, string json)
        {
            string full = System.IO.Path.GetFullPath(path);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
            string temp = full + ".tmp";
            System.IO.File.WriteAllText(temp, json, new System.Text.UTF8Encoding(false));
            if (System.IO.File.Exists(full)) System.IO.File.Replace(temp, full, full + ".bak");
            else System.IO.File.Move(temp, full);
        }
        public void ValidateAction()
        {
            var errors = Validate(); Status(errors.Count == 0 ? "VALID / Bounds, spawns, terrain and damage checked." : errors[0] + " (" + errors.Count + " issues; see Console)");
            if (errors.Count > 0) Debug.LogWarning(string.Join("\n", errors), this);
        }
        public void TestLevel()
        {
            var errors = Validate();
            if (errors.Count > 0) { Status("Cannot test: " + errors[0]); return; }
            TestRequested?.Invoke(this);
        }
        public void Dirty()
        {
            if (GamePreview) GamePreview.Invalidate();
#if UNITY_EDITOR
            if (!TrackUndo) return;
            EditorUtility.SetDirty(this);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }
        public LevelElement Create(LevelElementKind kind, string tile, float x, float y, float w, float h)
        {
            var go = new GameObject(kind + " - " + tile); go.transform.SetParent(MapRoot, false);
            var element = go.AddComponent<LevelElement>(); element.Kind = kind; element.Tile = tile; element.SetBounds(x, y, w, h);
            if(element.IsBuilding)element.Damage=BattleBuildings.DefaultDamage();
#if UNITY_EDITOR
            if (TrackUndo) Undo.RegisterCreatedObjectUndo(go, "Add level element");
#endif
            return element;
        }
        private void Remove(LevelElement element)
        {
#if UNITY_EDITOR
            if (TrackUndo) Undo.DestroyObjectImmediate(element.gameObject); else DestroyImmediate(element.gameObject);
#else
            Destroy(element.gameObject);
#endif
        }
        public LevelElement At(float x, float y) => Elements.Reverse().FirstOrDefault(e => e.Kind != LevelElementKind.Ground && e.Bounds.X <= x && e.Bounds.Right > x && e.Bounds.Y <= y && e.Bounds.Bottom > y)
            ?? Elements.Reverse().FirstOrDefault(e => e.Bounds.X <= x && e.Bounds.Right > x && e.Bounds.Y <= y && e.Bounds.Bottom > y);
        public void ApplyBrush(float x, float y, bool erase = false)
        {
            if (Tool == "Select" && !erase) { Select(At(x, y)); return; }
            int grid = Snap == 16 || Snap == 32 ? Snap : 64;
            x = Mathf.Floor(x / grid) * grid; y = Mathf.Floor(y / grid) * grid;
            bool removing = erase || Tool == "Erase";
            if (!removing && !LevelEditorCatalog.Allows(BrushKind, Brush)) { Status("Choose an element used by the campaign maps."); return; }
            float w = grid, h = grid;
            EnvironmentPiece prefab = null;
            if (BrushKind == LevelElementKind.Prop)
            {
                var palette = Resources.Load<EnvironmentPalette>("EnvironmentPalette");
                prefab = palette ? palette.Prefabs.FirstOrDefault(p => p && p.Key == Brush) : null;
                if (!removing && !prefab) { Status("Missing prop: " + Brush); return; }
                if (prefab) { w = prefab.FootprintWidth; h = prefab.FootprintHeight; }
            }
            if (BrushKind == LevelElementKind.PlayerSpawn || BrushKind == LevelElementKind.EnemySpawn) w = h = 64;
            if (BrushKind == LevelElementKind.Base) { w = 128; h = 96; }
            if (x < 0 || y < 0 || x + w > WidthTiles * 64 || y + h > HeightTiles * 64) return;
            var box = new Box(x, y, w, h);
            if (removing)
            {
                var hit = At(x + grid * .5f, y + grid * .5f);
                if (hit) { if (hit.Kind == LevelElementKind.Terrain || hit.Kind == LevelElementKind.Ground) Cut(hit, new Box(x, y, grid, grid)); else Remove(hit); }
            }
            else
            {
                if (BrushKind == LevelElementKind.Base || BrushKind == LevelElementKind.PlayerSpawn)
                    foreach (var e in Elements.Where(e => e.Kind == BrushKind)) Remove(e);
                if (BrushKind == LevelElementKind.Terrain || BrushKind == LevelElementKind.Ground)
                    foreach (var e in Elements.Where(e => e.Kind == BrushKind && e.Bounds.Overlaps(box))) Cut(e, box);
                else if (Elements.Any(e => e.Kind == BrushKind && e.Bounds.Overlaps(box))) return;
                var added = Create(BrushKind, Brush, x, y, w, h); if (prefab) added.PropRole = prefab.Role;
                RefreshVisual(added); Select(added);
            }
            Dirty();
        }
        private void Cut(LevelElement original, Box cut)
        {
            var b = original.Bounds; float left = Mathf.Max(b.X, cut.X), right = Mathf.Min(b.Right, cut.Right), top = Mathf.Max(b.Y, cut.Y), bottom = Mathf.Min(b.Bottom, cut.Bottom);
            if (right <= left || bottom <= top) return;
            void Part(float x, float y, float w, float h)
            {
                if (w <= .01f || h <= .01f) return;
                var e = Create(original.Kind, original.Tile, x, y, w, h); e.OverrideDamage = original.OverrideDamage; e.Damage = original.Damage.Copy(); e.SourceId = original.SourceId; e.DesignNotes = original.DesignNotes; RefreshVisual(e);
            }
            Part(b.X, b.Y, b.W, top - b.Y); Part(b.X, bottom, b.W, b.Bottom - bottom);
            Part(b.X, top, left - b.X, bottom - top); Part(right, top, b.Right - right, bottom - top);
            Remove(original);
        }
        public void Import(string json)
        {
            EndMove();
            var map = JsonConvert.DeserializeObject<MapData>(json) ?? throw new ArgumentException("Empty map");
            if ((map.terrain?.regions?.Length ?? 0) + (map.objects?.Length ?? 0) > 10000) throw new ArgumentException("Map has too many elements");
            int width = map.field?.widthTiles ?? 13, height = map.field?.heightTiles ?? 13;
            if (width < 4 || width > 40 || height < 4 || height > 40) throw new ArgumentException("Map dimensions must be 4 to 40 tiles.");
            foreach (var region in (map.terrain?.regions ?? Array.Empty<Region>()).Concat(map.ground?.regions ?? Array.Empty<Region>()))
                if (region == null || string.IsNullOrEmpty(region.type) || float.IsNaN(region.x + region.y + region.width + region.height) || float.IsInfinity(region.x + region.y + region.width + region.height) || region.width <= 0 || region.height <= 0 || region.width > 2560 || region.height > 2560) throw new ArgumentException("Map has an invalid region.");
            var metadata = JObject.Parse(json)["editor"];
            bool authored = (int?)metadata?["version"] >= 1;
#if UNITY_EDITOR
            int undo = -1;
            if (TrackUndo) { Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Open level"); undo = Undo.GetCurrentGroup(); Undo.RecordObject(this, "Open level"); }
#endif
            foreach (var e in Elements) Remove(e);
            ImportedJson = json; WidthTiles = map.field?.widthTiles ?? 13; HeightTiles = map.field?.heightTiles ?? 13;
            if (metadata != null) { LevelName = (string)metadata["name"] ?? LevelName; PreviewStage = (int?)metadata["previewStage"] ?? PreviewStage; }
            Enemies = map.spawn?.enemy?.list ?? Enumerable.Range(0, 20).Select(_ => new EnemySpec()).ToArray();
            foreach (var r in map.terrain?.regions ?? Array.Empty<Region>())
            { var e = Create(LevelElementKind.Terrain, BattleTerrain.Normalize(r.type), r.x, r.y, r.width, r.height); e.OverrideDamage = r.damage != null; e.Damage = r.damage?.Copy() ?? new MapDamage(); }
            foreach (var r in map.ground?.regions ?? Array.Empty<Region>()) Create(LevelElementKind.Ground, r.type, r.x, r.y, r.width, r.height);
            foreach (var p in map.objects ?? Array.Empty<MapObjectData>())
            { var e = Create(LevelElementKind.Prop, p.type, p.x, p.y, p.width, p.height); e.PropRole = p.role; e.Rotation = p.rotation; e.BridgeSurface = p.bridge; e.OverrideDamage = p.damage != null; e.Damage = p.damage?.Copy() ?? (e.IsBuilding ? BattleBuildings.DefaultDamage() : new MapDamage()); e.OverrideBuildingLighting = p.building != null; e.BuildingLighting = p.building?.Copy() ?? new MapBuildingSettings(); }
            foreach (var p in map.lights ?? Array.Empty<MapLightData>())
            { var e = Create(LevelElementKind.Light, "light", p.x - 8, p.y - 8, 16, 16); e.LightColor = new Color(p.r, p.g, p.b); e.LightHeight = p.height <= 0 ? 1.7f : p.height; e.LightRange = p.range <= 0 ? 5 : p.range; e.LightIntensity = p.intensity <= 0 ? 1 : p.intensity; }
            var players = map.spawn?.player?.locations;
            foreach (var p in authored ? players ?? Array.Empty<Point>() : players == null || players.Length == 0 ? new[] { new Point { x = WidthTiles * 32 - 160, y = HeightTiles * 64 - 64 } } : players) Create(LevelElementKind.PlayerSpawn, "player", p.x, p.y, 64, 64);
            var spawns = map.spawn?.enemy?.locations;
            foreach (var p in authored ? spawns ?? Array.Empty<Point>() : spawns == null || spawns.Length == 0 ? new[] { new Point { x = WidthTiles * 32 - 32, y = 0 }, new Point { x = WidthTiles * 64 - 64, y = 0 }, new Point() } : spawns) Create(LevelElementKind.EnemySpawn, "enemy", p.x, p.y, 64, 64);
            if (!authored || map.@base != null) Create(LevelElementKind.Base, "base", map.@base?.x ?? WidthTiles * 32 - 64, map.@base?.y ?? HeightTiles * 64 - 96, 128, 96);
            ImportElementNotes(metadata);
            foreach (var e in Elements) RefreshVisual(e);
            Selected = null; FrameMap(); Dirty();
#if UNITY_EDITOR
            if (TrackUndo) Undo.CollapseUndoOperations(undo);
#endif
        }
        public MapData Export()
        {
            var elements = Elements.Where(e => e.gameObject.activeSelf).ToArray();
            Region Region(LevelElement e) => new Region { type = e.Tile, x = e.X, y = e.Y, width = e.Size.x, height = e.Size.y, damage = e.ExportDamage() };
            Point Point(LevelElement e) => new Point { x = e.X, y = e.Y };
            var b = elements.FirstOrDefault(e => e.Kind == LevelElementKind.Base);
            return new MapData { field = new FieldData { widthTiles = WidthTiles, heightTiles = HeightTiles },
                terrain = new Core.TerrainData { regions = elements.Where(e => e.Kind == LevelElementKind.Terrain).Select(Region).ToArray() },
                ground = new GroundData { regions = elements.Where(e => e.Kind == LevelElementKind.Ground).Select(Region).ToArray() },
                objects = elements.Where(e => e.Kind == LevelElementKind.Prop).Select(e => new MapObjectData { type = e.Tile, role = e.PropRole, bridge = e.BridgeSurface, x = e.X, y = e.Y, width = e.Size.x, height = e.Size.y, rotation = e.Rotation, damage = e.ExportDamage(), building = e.ExportBuilding() }).ToArray(),
                lights = elements.Where(e => e.Kind == LevelElementKind.Light).Select(e => new MapLightData { x = e.X + e.Size.x * .5f, y = e.Y + e.Size.y * .5f, height = e.LightHeight, r = e.LightColor.r, g = e.LightColor.g, b = e.LightColor.b, range = e.LightRange, intensity = e.LightIntensity }).ToArray(),
                spawn = new SpawnData { player = new SpawnGroup { locations = elements.Where(e => e.Kind == LevelElementKind.PlayerSpawn).Select(Point).ToArray() }, enemy = new SpawnGroup { locations = elements.Where(e => e.Kind == LevelElementKind.EnemySpawn).Select(Point).ToArray(), list = Enemies } },
                @base = b ? new BaseData { x = b.X, y = b.Y } : null };
        }
        public string ToJson()
        {
            var json = JObject.Parse(string.IsNullOrEmpty(ImportedJson) ? "{}" : ImportedJson);
            var map = JObject.FromObject(Export(), JsonSerializer.Create(new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
            foreach (var p in map.Properties()) json[p.Name] = p.Value;
            if (Export().@base == null) json["base"] = null;
            var metadata = json["editor"] as JObject ?? new JObject();
            metadata["name"]=LevelName; metadata["previewStage"]=PreviewStage; metadata["version"]=1; metadata["elements"]=ExportElementNotes();
            json["editor"] = metadata;
            return json.ToString(Formatting.Indented);
        }
        public List<string> Validate()
        {
            var errors = new List<string>(); var elements = Elements.Where(e => e.gameObject.activeSelf).ToArray();
            if (WidthTiles < 4 || HeightTiles < 4 || WidthTiles > 40 || HeightTiles > 40) errors.Add("Map dimensions must be between 4 and 40 tiles.");
            if (elements.Count(e => e.Kind == LevelElementKind.PlayerSpawn) != 1) errors.Add("Place exactly one player spawn.");
            if (!elements.Any(e => e.Kind == LevelElementKind.EnemySpawn)) errors.Add("Place at least one enemy spawn.");
            if (elements.Count(e => e.Kind == LevelElementKind.Base) != 1) errors.Add("Place exactly one base.");
            if (Enemies == null || Enemies.Length == 0 || Enemies.Any(e => e == null || !new[] { "a", "b", "c", "d" }.Contains(e.tier))) errors.Add("Enemy roster needs valid a/b/c/d entries.");
            var palette = Resources.Load<EnvironmentPalette>("EnvironmentPalette"); var props = palette ? palette.Lookup() : new Dictionary<string, EnvironmentPiece>();
            foreach (var e in elements)
            {
                var b = e.Bounds;
                if (float.IsNaN(b.X + b.Y + b.W + b.H) || float.IsInfinity(b.X + b.Y + b.W + b.H) || b.W <= 0 || b.H <= 0 || b.X < -.01f || b.Y < -.01f || b.Right > WidthTiles * 64 + .01f || b.Bottom > HeightTiles * 64 + .01f) errors.Add(e.name + " is outside the map or has an invalid size.");
                if (e.transform.localScale != Vector3.one || Quaternion.Angle(e.transform.localRotation, Quaternion.identity) > .01f) errors.Add(e.name + ": use Size and Rotation fields; keep root Transform scale 1 and rotation 0.");
                if (e.Kind == LevelElementKind.Terrain && !new[] { "brick", "steel", "jungle", "water", "ice", "lava", "muddyWater", "grease", "quicksand" }.Contains(e.Tile)) errors.Add("Unknown terrain: " + e.Tile);
                if (e.Kind == LevelElementKind.Ground && !new[] { "city", "plaza", "road", "forestPath", "meadow", "dirt" }.Contains(e.Tile)) errors.Add("Unknown ground: " + e.Tile);
                if (e.Kind == LevelElementKind.Prop && (!props.TryGetValue(e.Tile, out var prefab) || prefab.Role != e.PropRole)) errors.Add("Unknown prop or role mismatch: " + e.Tile);
                if (e.BridgeSurface && (e.Kind != LevelElementKind.Prop || e.PropRole != "groundDetail")) errors.Add(e.name + ": bridges must be ground-detail props.");
                if (e.OverrideDamage && (!e.SupportsDamage || e.Damage == null || e.Damage.hitPoints < 1 || e.Damage.hitPoints > 9999)) errors.Add(e.name + " has invalid damage settings.");
                if (e.IsBuilding && e.OverrideBuildingLighting && (e.BuildingLighting == null ||
                    float.IsNaN(e.BuildingLighting.lightIntensity) || e.BuildingLighting.lightIntensity < 0 || e.BuildingLighting.lightIntensity > 8 ||
                    float.IsNaN(e.BuildingLighting.lightRange) || e.BuildingLighting.lightRange < .1f || e.BuildingLighting.lightRange > 8))
                    errors.Add(e.name + " needs a building light intensity from 0 to 8 and range from 0.1 to 8.");
            }
            var surfaces = elements.Where(e => e.Kind == LevelElementKind.Terrain && BattleTerrain.IsSurface(e.Tile)).ToArray();
            for (int i = 0; i < surfaces.Length; i++) for (int j = i + 1; j < surfaces.Length; j++) if (surfaces[i].Tile != surfaces[j].Tile && surfaces[i].Bounds.Overlaps(surfaces[j].Bounds)) errors.Add("Different terrain surfaces overlap.");
            if (errors.Count == 0)
            {
                var sim = new BattleSimulation(Export(), PreviewStage);
                foreach (var e in elements.Where(e => e.Kind == LevelElementKind.PlayerSpawn || e.Kind == LevelElementKind.EnemySpawn))
                    if (sim.Terrain.Any(w => w.Solid && w.Bounds.Overlaps(e.Bounds)) || sim.BaseBounds.Overlaps(e.Bounds)) errors.Add(e.name + " is blocked. A tank needs a clear 64 x 64 footprint.");
            }
            return errors.Distinct().ToList();
        }

        public void RefreshVisual(LevelElement e)
        {
#if UNITY_EDITOR
            if (!BuildPreviews) return;
            if (GamePreview) { GamePreview.Invalidate(); return; }
            if (float.IsNaN(e.Size.x + e.Size.y) || float.IsInfinity(e.Size.x + e.Size.y) || e.Size.x <= 0 || e.Size.y <= 0 || e.Size.x > 2560 || e.Size.y > 2560) return;
            if (e.Visual) DestroyImmediate(e.Visual.gameObject);
            var root = new GameObject("Preview"); root.transform.SetParent(e.transform, false); e.Visual = root.transform;
            Material material = PreviewMaterials != null && MaterialKeys != null ? PreviewMaterials.ElementAtOrDefault(Array.IndexOf(MaterialKeys, e.Tile)) : null;
            if (!material && PreviewMaterials?.Length > 0) material = PreviewMaterials[0];
            void Cube(Vector3 position, Vector3 size, Material mat)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "Footprint"; cube.transform.SetParent(root.transform, false); cube.transform.localPosition = position; cube.transform.localScale = size;
                DestroyImmediate(cube.GetComponent<Collider>()); cube.GetComponent<Renderer>().sharedMaterial = mat;
            }
            GameObject model = e.Kind == LevelElementKind.PlayerSpawn || e.Kind == LevelElementKind.EnemySpawn ? TankModel : e.Kind == LevelElementKind.Base ? EagleModel : null;
            if (e.Kind == LevelElementKind.Prop)
            {
                var palette = Resources.Load<EnvironmentPalette>("EnvironmentPalette"); var prefab = palette ? palette.Prefabs.FirstOrDefault(p => p && p.Key == e.Tile) : null;
                if (prefab)
                {
                    var obj = Instantiate(prefab.gameObject, root.transform); obj.transform.localPosition = Vector3.zero; obj.transform.localRotation = Quaternion.Euler(0, e.Rotation, 0);
                    bool quarter = Mathf.Abs(Mathf.Repeat(e.Rotation, 180) - 90) < .01f;
                    obj.transform.localScale = new Vector3((quarter ? e.Size.y : e.Size.x) / prefab.FootprintWidth, 1, (quarter ? e.Size.x : e.Size.y) / prefab.FootprintHeight);
                }
            }
            else if (model)
            {
                var obj = Instantiate(model, root.transform); obj.transform.localPosition = e.Kind == LevelElementKind.Base ? new Vector3(0, .1f, -.25f) : Vector3.up * .05f;
                obj.transform.localRotation = Quaternion.Euler(0, 180, 0);
                if (e.Kind == LevelElementKind.Base)
                {
                    Cube(new Vector3(0, .2f, .5f), new Vector3(2, .4f, .5f), material);
                    Cube(new Vector3(-.75f, .2f, -.25f), new Vector3(.5f, .4f, 1), material);
                    Cube(new Vector3(.75f, .2f, -.25f), new Vector3(.5f, .4f, 1), material);
                }
                else Cube(new Vector3(0, -.01f, 0), new Vector3(1, .035f, 1), material);
            }
            else if (e.Kind == LevelElementKind.Light)
            {
                Cube(Vector3.up * .15f, new Vector3(.15f, .3f, .15f), material);
                var light = root.AddComponent<Light>(); light.type = LightType.Point; light.color = e.LightColor; light.range = e.LightRange; light.intensity = e.LightIntensity; root.transform.localPosition = Vector3.up * e.LightHeight;
            }
            else
            {
                GameObject tileModel = e.Kind == LevelElementKind.Terrain ? ((e.Tile ?? "").Contains("brick") ? BrickModel : e.Tile == "steel" ? SteelModel : e.Tile == "jungle" ? BushModel : null) : null;
                if (tileModel)
                {
                    for (float y = 0; y < e.Size.y; y += 64) for (float x = 0; x < e.Size.x; x += 64)
                    {
                        float w = Mathf.Min(64, e.Size.x - x), h = Mathf.Min(64, e.Size.y - y);
                        var obj = Instantiate(tileModel, root.transform); obj.transform.localPosition = new Vector3((x + w * .5f - e.Size.x * .5f) / 64, 0, -(y + h * .5f - e.Size.y * .5f) / 64);
                        if (e.Tile == "brick") obj.transform.localPosition += Vector3.up * (.415f - .79094963f);
                        obj.transform.localRotation = Quaternion.Euler(0, 180, 0); obj.transform.localScale = new Vector3(w / (e.Tile == "steel" ? 32 : 64), 1, h / (e.Tile == "steel" ? 32 : 64));
                    }
                }
                else Cube(new Vector3(0, e.Kind == LevelElementKind.Ground ? -.035f : .015f, 0), new Vector3(e.Size.x / 64, .03f, e.Size.y / 64), material);
            }
            foreach (var collider in root.GetComponentsInChildren<Collider>()) DestroyImmediate(collider);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m ? Resources.Load<Material>("RuntimeShaders/Terrain/" + m.name) ?? m : material).ToArray();
#endif
        }
    }
}
