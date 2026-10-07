using UnityEngine;

namespace BattleCities
{
    [CreateAssetMenu(menuName = "Battle Cities/Visual Assets")]
    public sealed class BattleVisualAssets : ScriptableObject
    {
        public Material WaterMaterial;
        public GameObject MuzzleFlash;
        public GameObject Impact;
        public GameObject Explosion;
        public float MuzzleScale = .28f;
        public float ImpactScale = .32f;
        public float ExplosionScale = .65f;
    }
}
