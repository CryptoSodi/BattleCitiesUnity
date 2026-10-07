using System;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities.LevelEditor
{
    public enum LevelElementKind { Terrain, Ground, Prop, PlayerSpawn, EnemySpawn, Base, Light }

    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class LevelElement : MonoBehaviour
    {
        public LevelElementKind Kind;
        public string Tile = "brick";
        [Tooltip("Footprint in map units. One tile is 64 units. Move this object with the Transform tool.")]
        public Vector2 Size = new Vector2(64, 64);
        public string PropRole;
        [Tooltip("Permanent passable deck: cuts water inside this ground-detail footprint.")]
        public bool BridgeSurface;
        public string SourceId;
        [TextArea(2, 6)] public string DesignNotes;
        [Tooltip("Visual rotation. The collision footprint stays aligned to the map grid.")]
        public float Rotation;
        public bool OverrideDamage;
        [Tooltip("Health is per building section (32 units), brick fragment (16), or steel fragment (32). Other props have one health pool. Power shots include splash damage.")]
        public MapDamage Damage = new MapDamage();
        [Tooltip("Override the default warm window lighting for this building.")]
        public bool OverrideBuildingLighting;
        public MapBuildingSettings BuildingLighting = new MapBuildingSettings();
        public Color LightColor = Color.white;
        [Min(.1f)] public float LightHeight = 1.7f, LightRange = 5, LightIntensity = 1;
        [HideInInspector] public Transform Visual;
        [NonSerialized] private string signature;

        public float X => transform.localPosition.x * 64 - Size.x * .5f;
        public float Y => -transform.localPosition.z * 64 - Size.y * .5f;
        public Box Bounds => new Box(X, Y, Size.x, Size.y);
        public bool IsBuilding => Kind == LevelElementKind.Prop && BattleBuildings.IsBuilding(Tile);
        public MapBuildingSettings ExportBuilding() => IsBuilding && OverrideBuildingLighting ? BuildingLighting.Copy() : null;
        public bool SupportsDamage => (Kind == LevelElementKind.Terrain && ((Tile ?? "").Contains("brick") || Tile == "steel")) ||
            (Kind == LevelElementKind.Prop && (PropRole == "solidCover" || PropRole == "destructibleObstacle" || PropRole == "bulletBlocker"));
        public MapDamage ExportDamage() => OverrideDamage && SupportsDamage ? Damage.Copy() : null;
        public void SetBounds(float x, float y, float width, float height)
        {
            Size = new Vector2(width, height);
            transform.localPosition = new Vector3((x + width * .5f) / 64, 0, -(y + height * .5f) / 64);
        }
        private void OnEnable() => signature = null;
        private void Update()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling) return;
            var document = GetComponentInParent<LevelEditorDocument>();
            string next = transform.localPosition + "|" + Kind + "|" + Tile + "|" + Size + "|" + Rotation + "|" + BridgeSurface + "|" + LightColor + "|" + LightHeight + "|" + LightRange + "|" + LightIntensity + "|" + OverrideBuildingLighting + "|" + BuildingLighting.lights + "|" + BuildingLighting.lightIntensity + "|" + BuildingLighting.lightRange;
            if (signature == next && (Visual || (document && document.GamePreview))) return;
            signature = next;
            if (document) document.RefreshVisual(this);
#endif
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + Vector3.up * .08f, new Vector3(Size.x / 64, .16f, Size.y / 64));
        }
    }
}
