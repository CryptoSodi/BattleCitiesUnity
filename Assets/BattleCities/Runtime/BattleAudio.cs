using System;
using System.Collections.Generic;
using BattleCities.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BattleCities
{
    /// <summary>Original arcade effects plus a quiet, generated music bed and heavier blast layer.</summary>
    public sealed class BattleAudio : MonoBehaviour
    {
        [Range(0, 1)] public float MusicVolume = .12f;
        [Range(0, 1)] public float EffectsVolume = .58f;
        [Range(0, 1)] public float EngineVolume = .12f;

        private static BattleAudio instance;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<Button> wiredButtons = new HashSet<Button>();
        private AudioSource music, engine;
        private AudioSource[] voices;
        private AudioClip musicClip, blastClip;
        private BattleGame game;
        private BattleSimulation simulation;
        private int voiceIndex, lastScore, lastLives;
        private bool lastPaused, lastWon, lastLost, lastSliding, highScorePlayed;
        private int savedHighScore;
        private string lastPickup;
        private float introUntil, nextSearch, nextButtons, lastFire, lastImpact;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (instance) return;
            var root = new GameObject("Battle Cities Audio");
            DontDestroyOnLoad(root);
            root.AddComponent<BattleAudio>();
        }

        private void Awake()
        {
            if (instance && instance != this) { Destroy(gameObject); return; }
            instance = this;
            foreach (var name in new[] { "fire", "game-over", "high-score", "hit-enemy", "hit-steel", "hit-brick", "ice", "level-intro", "life", "pause", "powerup-appear", "powerup-pickup", "player-explosion", "enemy-explosion", "score", "score-bonus", "tank-idle", "tank-move", "victory" })
            {
                var clip = Resources.Load<AudioClip>("Audio/" + name);
                if (clip) clips[name] = clip;
                else Debug.LogWarning("[BattleAudio] Missing Audio/" + name);
            }
            music = NewSource("Arcade music", true);
            engine = NewSource("Tank engine", true);
            voices = new AudioSource[16];
            for (int i = 0; i < voices.Length; i++) voices[i] = NewSource("Effect " + (i + 1), false);
            musicClip = MakeMusic();
            blastClip = MakeBlast();
            music.clip = musicClip;
            music.volume = 0;
            music.Play();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private AudioSource NewSource(string label, bool loop)
        {
            var source = new GameObject(label).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.loop = loop;
            return source;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Bind(null);
            game = null;
            nextSearch = nextButtons = 0;
            wiredButtons.Clear();
            engine.Stop();
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + .5f;
                if (!game) game = FindAnyObjectByType<BattleGame>();
            }
            if (game && game.Simulation != simulation) Bind(game.Simulation);
            if (Time.unscaledTime >= nextButtons)
            {
                nextButtons = Time.unscaledTime + 2;
                foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Exclude))
                    if (wiredButtons.Add(button)) button.onClick.AddListener(PlayMenuClick);
            }

            bool playing = game && simulation != null && !game.Paused && !simulation.Won && !simulation.Lost;
            if (game && simulation != null) TrackState();
            float targetMusic = (!game || playing) && Time.unscaledTime >= introUntil ? MusicVolume : 0;
            music.volume = Mathf.MoveTowards(music.volume, targetMusic, Time.unscaledDeltaTime * .35f);
            UpdateEngine(playing);
        }

        private void Bind(BattleSimulation next)
        {
            if (simulation != null)
            {
                simulation.ShotFired -= OnShotFired;
                simulation.ShotImpact -= OnShotImpact;
                simulation.TankDestroyed -= OnTankDestroyed;
                simulation.WallDestroyed -= OnWallDestroyed;
                simulation.BaseDestroyed -= OnBaseDestroyed;
                simulation.DroneDetonated -= OnDroneDetonated;
                simulation.MineDetonated -= OnMineDetonated;
                simulation.LandDroneExploded -= OnLandDroneExploded;
                simulation.TurretFired -= OnTurretFired;
                simulation.TurretHit -= OnTurretHit;
                simulation.TurretDestroyed -= OnTurretDestroyed;
            }
            simulation = next;
            engine.Stop();
            if (next == null) return;
            next.ShotFired += OnShotFired;
            next.ShotImpact += OnShotImpact;
            next.TankDestroyed += OnTankDestroyed;
            next.WallDestroyed += OnWallDestroyed;
            next.BaseDestroyed += OnBaseDestroyed;
            next.DroneDetonated += OnDroneDetonated;
            next.MineDetonated += OnMineDetonated;
            next.LandDroneExploded += OnLandDroneExploded;
            next.TurretFired += OnTurretFired;
            next.TurretHit += OnTurretHit;
            next.TurretDestroyed += OnTurretDestroyed;
            lastScore = next.Score;
            lastLives = next.Lives;
            lastPickup = next.PickupType;
            lastWon = next.Won;
            lastLost = next.Lost;
            lastPaused = game && game.Paused;
            lastSliding = next.Player != null && next.Player.Slide > 0;
            savedHighScore = PlayerPrefs.GetInt("battlecities.highScore", 0);
            highScorePlayed = false;
            introUntil = Time.unscaledTime + 2;
            Play("level-intro", .65f);
        }

        private void TrackState()
        {
            if (game.Paused != lastPaused)
            {
                if (game.Paused) Play("pause", .45f);
                lastPaused = game.Paused;
            }
            if (simulation.Won && !lastWon) Play("victory", .9f);
            if (simulation.Lost && !lastLost) Play("game-over", .9f);
            if (simulation.PickupType != null && lastPickup == null) Play("powerup-appear", .45f);
            bool collectedPickup = lastPickup != null && simulation.PickupType == null && simulation.Score >= lastScore + 500;
            if (collectedPickup)
            {
                Play("powerup-pickup", .85f);
                Play("score-bonus", .32f);
            }
            else if (simulation.Score > lastScore) Play("score", .16f);
            if (simulation.Lives > lastLives) Play("life", .8f);
            var player = simulation.Player;
            bool sliding = player != null && player.Slide > 0;
            if (sliding && !lastSliding) Play("ice", .35f);
            lastSliding = sliding;
            if (!highScorePlayed && savedHighScore > 0 && lastScore <= savedHighScore && simulation.Score > savedHighScore)
            {
                Play("high-score", .55f);
                highScorePlayed = true;
            }
            lastPaused = game.Paused;
            lastWon = simulation.Won;
            lastLost = simulation.Lost;
            lastPickup = simulation.PickupType;
            lastScore = simulation.Score;
            lastLives = simulation.Lives;
        }

        private void UpdateEngine(bool playing)
        {
            var player = playing ? simulation.Player : null;
            string name = player == null ? null : player.Moving ? "tank-move" : "tank-idle";
            if (name != null && clips.TryGetValue(name, out var clip) && (engine.clip != clip || !engine.isPlaying))
            {
                engine.clip = clip;
                engine.volume = 0;
                engine.Play();
            }
            engine.volume = Mathf.MoveTowards(engine.volume, name == null ? 0 : EngineVolume, Time.unscaledDeltaTime * .5f);
            if (name == null && engine.isPlaying && engine.volume <= .001f) engine.Stop();
        }

        private void OnShotFired(ShotState shot)
        {
            if (Time.unscaledTime - lastFire < .055f) return;
            lastFire = Time.unscaledTime;
            Play("fire", shot.Player ? .52f : .22f, shot.PowerShot ? .8f : UnityEngine.Random.Range(.94f, 1.07f));
        }
        private void OnShotImpact(ShotState shot)
        {
            if (shot.PowerShot) { BigBlast(.6f); return; }
            if (Time.unscaledTime - lastImpact < .045f) return;
            lastImpact = Time.unscaledTime;
            Play("hit-steel", .22f, UnityEngine.Random.Range(.92f, 1.09f));
        }
        private void OnWallDestroyed(Wall wall) => Play(wall.Brick ? "hit-brick" : "hit-steel", .28f);
        private void OnTankDestroyed(TankState tank)
        {
            Play(tank.Player ? "player-explosion" : "enemy-explosion", tank.Player ? 1 : .7f);
            if (tank.Player || tank.Tier >= 2) PlayClip(blastClip, tank.Player ? .7f : .45f, 1);
        }
        private void OnBaseDestroyed() => BigBlast(1);
        private void OnDroneDetonated(DroneState drone) => BigBlast(.8f);
        private void OnMineDetonated(MineState mine) => BigBlast(.75f);
        private void OnLandDroneExploded(LandDroneState drone) => BigBlast(.9f);
        private void OnTurretFired(TurretState turret) => Play("fire", .22f);
        private void OnTurretHit(TurretState turret) => Play("hit-enemy", .3f);
        private void OnTurretDestroyed(TurretState turret) => BigBlast(.85f);
        private void BigBlast(float strength)
        {
            Play("enemy-explosion", strength * .65f, .82f);
            PlayClip(blastClip, strength * .8f, UnityEngine.Random.Range(.92f, 1.03f));
        }
        private void PlayMenuClick() => Play("pause", .16f, 1.3f);
        private void Play(string name, float level, float pitch = 1)
        {
            if (clips.TryGetValue(name, out var clip)) PlayClip(clip, level, pitch);
        }
        private void PlayClip(AudioClip clip, float level, float pitch)
        {
            if (!clip || voices == null) return;
            var source = voices[voiceIndex++ % voices.Length];
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = EffectsVolume * level;
            source.Play();
        }

        // An original, quiet 120 BPM arcade loop; no third-party music asset or runtime download.
        private static AudioClip MakeMusic()
        {
            const int rate = 22050, samples = rate * 16;
            var data = new float[samples];
            int[] bass = { 45, 45, 48, 45, 41, 41, 45, 41, 48, 48, 52, 48, 43, 43, 47, 43 };
            int[] melody = { 69, 72, 76, 72, 67, 69, 72, 69, 64, 67, 72, 67, 67, 71, 74, 71,
                             69, 72, 76, 79, 67, 69, 72, 76, 72, 76, 79, 76, 71, 74, 79, 74 };
            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / rate;
                int eighth = (int)(t * 4);
                double pulse = t * 4 - eighth;
                int chord = ((int)(t / 4)) % 4;
                int bassNote = bass[(int)(t * 2) % bass.Length];
                double bassHz = 440 * Math.Pow(2, (bassNote - 69) / 12.0);
                double bassWave = Math.Sin(2 * Math.PI * bassHz * t) * Math.Exp(-pulse * 3.5) * .13;
                int leadNote = melody[eighth % melody.Length];
                double leadHz = 440 * Math.Pow(2, (leadNote - 69) / 12.0);
                double leadPhase = leadHz * t;
                double triangle = 2 * Math.Abs(2 * (leadPhase - Math.Floor(leadPhase + .5))) - 1;
                double lead = triangle * Math.Min(1, pulse * 18) * Math.Exp(-pulse * 2.8) * .08;
                int[] roots = { 57, 53, 60, 55 };
                double pad = 0;
                foreach (int interval in new[] { 0, 4, 7 })
                {
                    double hz = 440 * Math.Pow(2, (roots[chord] + interval - 69) / 12.0);
                    pad += Math.Sin(2 * Math.PI * hz * t) * .018;
                }
                double beat = t * 2 - Math.Floor(t * 2);
                double kick = Math.Sin(2 * Math.PI * (62 - beat * 25) * t) * Math.Exp(-beat * 32) * .19;
                double noise = Math.Sin(i * 78.233) * Math.Sin(i * 12.9898);
                double hat = noise * Math.Exp(-pulse * 46) * .027;
                double snare = ((int)(t * 2) % 4 == 1 || (int)(t * 2) % 4 == 3) ? noise * Math.Exp(-beat * 29) * .065 : 0;
                data[i] = Mathf.Clamp((float)(bassWave + lead + pad + kick + hat + snare), -.75f, .75f);
            }
            var clip = AudioClip.Create("Battle Cities arcade loop", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip MakeBlast()
        {
            const int rate = 22050, samples = rate * 3 / 4;
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / rate;
                double phase = 2 * Math.PI * (105 * t - 43 * t * t);
                double noise = Math.Sin(i * 54.313) * Math.Sin(i * 17.117);
                data[i] = (float)((Math.Sin(phase) * .72 + noise * .28) * Math.Exp(-t * 6));
            }
            var clip = AudioClip.Create("Heavy blast low end", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            Bind(null);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (musicClip) Destroy(musicClip);
            if (blastClip) Destroy(blastClip);
            instance = null;
        }
    }
}
