using System.Collections.Generic;
using System.Linq;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BattleCities
{
    public sealed partial class BattleGame
    {
        private sealed class ShotPresentation
        {
            public Color Color;
            public Vector3 Previous;
        }

        private readonly Dictionary<int, ShotPresentation> shotPresentation = new Dictionary<int, ShotPresentation>();
        private readonly Dictionary<string, Material> bulletPaint = new Dictionary<string, Material>();

        private static string TankTint(TankState tank) => tank.Player ? "#f3bf32" : tank.Drop ? "#f0d7c0" :
            new[] { "#c55343", "#769995", "#9296b5", "#736d82" }[tank.Tier];

        private Color ShotColor(ShotState shot)
        {
            if (shotPresentation.TryGetValue(shot.Id, out var existing)) return existing.Color;
            var owner = Simulation.Tanks.Find(t => t.Id == shot.Owner);
            ColorUtility.TryParseHtmlString(owner != null ? TankTint(owner) : "#f3bf32", out var color);
            return color;
        }

        private void OnShotFired(ShotState shot)
        {
            var color = ShotColor(shot);
            shotPresentation[shot.Id] = new ShotPresentation { Color = color, Previous = World(shot.X, shot.Y, .48f) };
            if (actors.TryGetValue(shot.Owner, out var actor))
            {
                actor.Animation.Fire();
                if (actor.Muzzles.Length > 0)
                {
                    foreach (var muzzle in actor.Muzzles) effects.MuzzleFlash(muzzle.position, muzzle.forward, color, shot.PowerShot ? 1 : .55f);
                    return;
                }
            }
            effects.MuzzleFlash(World(shot.X, shot.Y, .48f), Quaternion.Euler(0, (int)shot.Direction * 90, 0) * Vector3.forward, color, shot.PowerShot ? 1 : .55f);
        }

        private void OnShotImpact(ShotState shot)
        {
            var direction = Quaternion.Euler(0, (int)shot.Direction * 90, 0) * Vector3.forward;
            var position = World(shot.X, shot.Y, .48f);
            if (shot.PowerShot && shotPresentation.TryGetValue(shot.Id, out var visual))
                effects.Trail(visual.Previous, position, visual.Color, true);
            effects.Impact(position + direction * .1f, direction, shot.PowerShot, ShotColor(shot), shot.PowerShot ? 1 : .45f);
        }

        private Material BulletMaterial(Material source, Color color)
        {
            // Keep the metal bands, golden casing and lightning emblem distinct.
            if (source.name != "PowerOrange") return source;
            string key = source.GetEntityId() + ColorUtility.ToHtmlStringRGB(color);
            if (bulletPaint.TryGetValue(key, out var material)) return material;
            material = new Material(source);
            material.name = "Power shell paint " + ColorUtility.ToHtmlStringRGB(color);
            material.color = color;
            bulletPaint.Add(key, material);
            ownedMaterials.Add(material);
            return material;
        }

        private void SyncProjectiles(float dt)
        {
            var living = new HashSet<int>();
            foreach (var shot in Simulation.Shots.Where(s => s.Alive))
            {
                living.Add(shot.Id);
                var position = World(shot.X, shot.Y, .48f);
                if (!shotPresentation.TryGetValue(shot.Id, out var visual))
                {
                    visual = new ShotPresentation { Color = ShotColor(shot), Previous = position };
                    shotPresentation.Add(shot.Id, visual);
                }
                if (!shots.TryGetValue(shot.Id, out var model))
                {
                    model = Instantiate(BulletModels[shot.PowerShot ? 2 : shot.Speed > 600 ? 1 : 0], actorsRoot);
                    model.name = (shot.PowerShot ? "Glowing bullet " : "Classic bullet ") + shot.Id;
                    // Normal rounds keep the original prefab's size and authored materials.
                    if (shot.PowerShot)
                    {
                        // Give the existing heavy shell a distinct size for charged shots.
                        model.transform.localScale *= 1.5f;
                        foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                        {
                            renderer.sharedMaterials = renderer.sharedMaterials.Select(source => BulletMaterial(source, visual.Color)).ToArray();
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                            renderer.receiveShadows = false;
                        }
                    }
                    shots.Add(shot.Id, model);
                }
                model.transform.SetPositionAndRotation(position, Quaternion.Euler(0, (int)shot.Direction * 90, 0));
                if (shot.PowerShot)
                {
                    var tailOffset = model.transform.forward * .3f;
                    if (dt > 0) effects.Trail(visual.Previous - tailOffset, position - tailOffset, visual.Color, true);
                    effects.ShellGlow(position, visual.Color, gameCamera);
                }
                else if (dt > 0)
                    effects.NormalShotExhaust(position - model.transform.forward * .12f, -model.transform.forward);
                visual.Previous = position;
            }
            foreach (var id in shots.Keys.Where(id => !living.Contains(id)).ToArray()) { Destroy(shots[id]); shots.Remove(id); }
            foreach (var id in shotPresentation.Keys.Where(id => !living.Contains(id)).ToArray()) shotPresentation.Remove(id);
        }
    }
}
