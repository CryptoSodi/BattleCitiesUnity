using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleCities.Core;
using UnityEngine;

namespace BattleCities
{
    /// <summary>Capture-only harness used by the 10-second gameplay reel.</summary>
    public sealed class ShowcaseCapture : MonoBehaviour
    {
        const int Width = 1280, Height = 720, Fps = 30, FrameCount = 300;
        const string Trigger = @"C:\repos\Battle Cities Game\video\unity-capture.trigger";
        const string DefaultOutput = @"C:\repos\Battle Cities Game\video\.unity-frames";
        BattleGame game;
        BattleWeather weather;
        Camera battleCamera;
        RenderTexture target;
        Texture2D pixels;
        string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWhenRequested()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BATTLE_CITIES_CAPTURE_DIR")) && !File.Exists(Trigger)) return;
            new GameObject("Battle Cities showcase capture").AddComponent<ShowcaseCapture>();
        }

        IEnumerator Start()
        {
            output = Environment.GetEnvironmentVariable("BATTLE_CITIES_CAPTURE_DIR");
            if (string.IsNullOrWhiteSpace(output)) output = DefaultOutput;
            Directory.CreateDirectory(output);
            Time.captureFramerate = Fps;
            Application.targetFrameRate = Fps;
            Screen.SetResolution(Width, Height, false);

            while ((game = FindFirstObjectByType<BattleGame>()) == null || game.Simulation == null) yield return null;
            weather = (BattleWeather)typeof(BattleGame).GetField("weather", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            battleCamera = Camera.main ?? FindFirstObjectByType<Camera>();
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            PrepareStage(1, .50f, true, "upgrade", 0);
            for (int frame = 0; frame < FrameCount; frame++)
            {
                ApplyBeat(frame);
                var direction = frame < 100 ? Facing.Up : frame < 200 ? Facing.Right : Facing.Left;
                game.Simulation.Step(new Command { Move = direction, Aim = direction, Fire = true });
                game.Simulation.Step(new Command { Move = direction, Aim = direction, Fire = frame % 2 == 0 });

                yield return new WaitForEndOfFrame();
                Capture(frame);
            }

            File.WriteAllText(Path.Combine(output, "complete.txt"), "300 frames at 30 fps");
            if (File.Exists(Trigger)) File.Delete(Trigger);
            battleCamera.targetTexture = null;
            RenderTexture.active = null;
            Destroy(target);
            Destroy(pixels);
            Time.captureFramerate = 0;
#if UNITY_EDITOR
            if (Application.isBatchMode) UnityEditor.EditorApplication.Exit(0);
            else UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(0);
#endif
        }

        void ApplyBeat(int frame)
        {
            if (frame == 52) SetWeather(0f, true);
            if (frame == 88) DestroyShowcaseTank();
            if (frame == 104) game.Simulation.SpawnPickup("shield");
            if (frame == 120) PrepareStage(5, .50f, false, "speed", 100);
            if (frame == 164) SetWeather(.08f, true);
            if (frame == 188) DestroyShowcaseTank();
            if (frame == 205) PrepareStage(12, 0f, true, "wipeout", 200);
            if (frame == 252) DestroyShowcaseTank();
            if (frame == 270) game.Simulation.SpawnPickup("defence");
        }

        void PrepareStage(int stage, float timeOfDay, bool rain, string pickup, int idOffset)
        {
            game.LoadStage(stage);
            SetWeather(timeOfDay, rain);
            typeof(BattleSimulation).GetField("intro", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game.Simulation, 0f);
            var sim = game.Simulation;
            sim.Tanks.Add(new TankState { Id = -101 - idOffset, Tier = stage % 4, Health = 1, X = sim.Width * .38f, Y = sim.Height * .42f, Direction = Facing.Down, Aim = Facing.Down });
            sim.Tanks.Add(new TankState { Id = -102 - idOffset, Tier = (stage + 1) % 4, Health = 1, Drop = true, X = sim.Width * .62f, Y = sim.Height * .34f, Direction = Facing.Down, Aim = Facing.Down });
            sim.SpawnPickup(pickup);
            sim.PickupX = sim.Width * .50f;
            sim.PickupY = sim.Height * .55f;
        }

        void SetWeather(float timeOfDay, bool rain)
        {
            weather.Cycle = false;
            weather.TimeOfDay = timeOfDay;
            weather.Rain = rain;
            weather.Clouds = true;
            weather.RainIntensity = rain ? .9f : 0f;
        }

        void DestroyShowcaseTank()
        {
            var victim = game.Simulation.Tanks.FirstOrDefault(t => !t.Player && t.Alive);
            if (victim != null) game.Simulation.Kill(victim);
        }

        void Capture(int frame)
        {
            var previous = RenderTexture.active;
            battleCamera.targetTexture = target;
            battleCamera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            pixels.Apply(false, false);
            File.WriteAllBytes(Path.Combine(output, $"frame-{frame:0000}.jpg"), pixels.EncodeToJPG(88));
            battleCamera.targetTexture = null;
            RenderTexture.active = previous;
        }
    }
}
