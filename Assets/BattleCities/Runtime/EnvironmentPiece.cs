using System;
using System.Collections.Generic;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    /// <summary>A reusable visual tied to one simulation wall or ground detail.</summary>
    public sealed class EnvironmentPiece : MonoBehaviour
    {
        public string Key;
        public string Role;
        public float FootprintWidth = 64;
        public float FootprintHeight = 64;
        public GameObject IntactVisual;
        public GameObject DestroyedVisual;

        private Wall wall;
        private BuildingDamageView building;

        public void BindBuilding(IReadOnlyList<Wall> sections)
        {
            if (sections.Count == 0 || sections[0].PropKey != Key || sections[0].PropRole != Role)
                throw new InvalidOperationException("Building prefab and map role disagree: " + Key);
            building = GetComponent<BuildingDamageView>();
            if (!building) building = gameObject.AddComponent<BuildingDamageView>();
            building.Bind(this, sections);
        }

        public void Bind(Wall state)
        {
            if (state == null || state.PropKey != Key || state.PropRole != Role)
                throw new InvalidOperationException("Environment prefab and map role disagree: " + Key);
            wall = state;
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (building) { building.RefreshVisual(); return; }
            bool intact = wall == null || wall.Alive;
            if (IntactVisual) IntactVisual.SetActive(intact);
            if (DestroyedVisual) DestroyedVisual.SetActive(!intact);
        }
    }

}
