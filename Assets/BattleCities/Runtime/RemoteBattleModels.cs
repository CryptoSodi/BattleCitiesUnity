using System;
using UnityEngine;
namespace BattleCities
{
    public sealed class RemoteBattleModels : ScriptableObject
    {
        public GameObject[] TankModels, BulletModels;
        public GameObject SpeedEnemyModel, BrickModel, SteelModel, BushModel, EagleModel, RuinedEagleModel, MineModel, DroneModel, GroundTurretPrefab;
        public TextAsset BrickSectionData;
        public Texture2D[] GroundTextures;
        public Texture2D PowerupAtlas;
        public void Apply(BattleGame game)
        {
            game.TankModels = TankModels;
            game.SpeedEnemyModel = SpeedEnemyModel;
            game.BrickModel = BrickModel;
            game.SteelModel = SteelModel;
            game.BushModel = BushModel;
            game.EagleModel = EagleModel;
            game.RuinedEagleModel = RuinedEagleModel;
            game.MineModel = MineModel;
            game.DroneModel = DroneModel;
            game.GroundTurretPrefab = GroundTurretPrefab;
            game.BulletModels = BulletModels;
            game.BrickSectionData = BrickSectionData;
            game.GroundTextures = GroundTextures;
            game.PowerupAtlas = PowerupAtlas;
        }
    }
}
