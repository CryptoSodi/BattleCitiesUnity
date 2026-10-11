using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BattleCities.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace BattleCities.Tests
{
    // The original implementation is an independent compatibility oracle: do not
    // update it when optimizing ReplayJson.Hash. Recorder/player round trips alone
    // would not detect a shared change that invalidated an older replay's hashes.
    public static class ReplayHashChecks
    {
        sealed class LegacyStateFields : DefaultContractResolver
        {
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                if (type.Namespace != "BattleCities.Core") return base.CreateProperties(type, memberSerialization);
                var properties = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Select(f => base.CreateProperty(f, memberSerialization)).ToList();
                if (type == typeof(TankState))
                    properties.Add(base.CreateProperty(type.GetProperty("ReloadDuration"), memberSerialization));
                return properties;
            }
        }

        static readonly LegacyStateFields LegacyFields = new LegacyStateFields();
        static readonly JsonSerializerSettings LegacySettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, MaxDepth = 64,
            DateParseHandling = DateParseHandling.None,
            Culture = CultureInfo.InvariantCulture, FloatParseHandling = FloatParseHandling.Double
        };

        public static string LegacyHash(object value)
        {
            var serializer = JsonSerializer.Create(LegacySettings);
            serializer.ContractResolver = LegacyFields;
            var token = JToken.FromObject(value, serializer);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(LegacySort(token)
                    .ToString(Formatting.None)))).Replace("-", "").ToLowerInvariant();
        }

        static JToken LegacySort(JToken token)
        {
            if (token is JObject obj)
                return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                    .Select(p => new JProperty(p.Name, LegacySort(p.Value))));
            if (token is JArray array) return new JArray(array.Select(LegacySort));
            if (token.Type == JTokenType.Float)
            {
                float value = Convert.ToSingle(((JValue)token).Value, CultureInfo.InvariantCulture);
                return new JValue("f32:" + BitConverter.ToUInt32(BitConverter.GetBytes(value), 0).ToString("x8"));
            }
            return token.DeepClone();
        }

        static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception("Replay hash compatibility: " + message);
        }

        public static void AssertParity(object value, string description)
        {
            string expected = LegacyHash(value), actual = ReplayJson.Hash(value);
            Check(actual == expected, description + ": expected " + expected + ", received " + actual);
        }

        static void AssertSimulationParity(BattleSimulation simulation, string description)
        {
            object live = simulation.ReplayStateData(), captured = simulation.CaptureReplayStateData();
            string expected = LegacyHash(live);
            Check(ReplayJson.Hash(live) == expected, description + ": live hash changed");
            Check(ReplayJson.Hash(captured) == expected, description + ": detached hash changed");
        }

        // Populate every stored field by reflection so a newly added scalar field
        // fails parity if its explicit copy is forgotten. New reference types must
        // be deliberately handled here and in the production snapshot, even if null.
        static void SeedStoredFields(object target, int salt)
        {
            int index = salt;
            foreach (var field in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Type type = field.FieldType;
                int value = ++index;
                if (type == typeof(int)) field.SetValue(target, value);
                else if (type == typeof(uint)) field.SetValue(target, (uint)value);
                else if (type == typeof(float)) field.SetValue(target, value + .375f);
                else if (type == typeof(bool)) field.SetValue(target, !(bool)field.GetValue(target));
                else if (type == typeof(string)) field.SetValue(target, field.Name + "_" + value);
                else if (type.IsEnum)
                {
                    var options = Enum.GetValues(type);
                    int current = Array.IndexOf(options.Cast<object>().ToArray(), field.GetValue(target));
                    field.SetValue(target, options.GetValue((current + 1) % options.Length));
                }
                else if (type == typeof(Box)) field.SetValue(target, new Box(value + .125f, value + .25f, value + .5f, value + .75f));
                else if (type == typeof(MapDamage) || type == typeof(MapBuildingSettings))
                {
                    object nested = Activator.CreateInstance(type);
                    SeedStoredFields(nested, value * 2); field.SetValue(target, nested);
                }
                else if (type == typeof(List<Point>))
                {
                    var points = (List<Point>)field.GetValue(target);
                    points.Clear(); points.Add(Seeded(new Point(), value)); points.Add(null);
                    points.Add(Seeded(new Point(), value + 10));
                }
                else if (type == typeof(HashSet<int>))
                {
                    var tiles = (HashSet<int>)field.GetValue(target);
                    tiles.Clear(); tiles.Add(value + 3); tiles.Add(value); tiles.Add(value + 1);
                }
                else throw new Exception("Snapshot field guard needs explicit coverage for " + target.GetType().Name + "." + field.Name + " (" + type + ")");
            }
            if (target is TankState)
                typeof(TankState).GetProperty("ReloadDuration").SetValue(target, salt + .625f, null);
        }

        static T Seeded<T>(T target, int salt) where T : class
        { SeedStoredFields(target, salt); return target; }

        static IEnumerable<KeyValuePair<string, object>> StoredMembers(object value)
        {
            Type type = value.GetType();
            if (type.Namespace == "BattleCities.Core")
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    yield return new KeyValuePair<string, object>(field.Name, field.GetValue(value));
                if (value is TankState tank)
                    yield return new KeyValuePair<string, object>("ReloadDuration", tank.ReloadDuration);
            }
            else foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                yield return new KeyValuePair<string, object>(property.Name, property.GetValue(value, null));
        }

        static void AssertDetached(object live, object captured, string path)
        {
            if (live == null || captured == null)
            { Check(live == null && captured == null, path + ": null changed"); return; }
            Type type = live.GetType();
            Check(type == captured.GetType(), path + ": stored type changed");
            if (type.IsValueType || live is string)
            { Check(live.Equals(captured), path + ": stored value changed"); return; }
            Check(!ReferenceEquals(live, captured), path + ": mutable simulation reference was retained");
            if (live is IEnumerable items)
            {
                var first = items.Cast<object>().ToArray();
                var second = ((IEnumerable)captured).Cast<object>().ToArray();
                Check(first.Length == second.Length, path + ": collection length changed");
                for (int i = 0; i < first.Length; i++) AssertDetached(first[i], second[i], path + "[" + i + "]");
            }
            else
            {
                var second = StoredMembers(captured).ToDictionary(p => p.Key, p => p.Value);
                foreach (var member in StoredMembers(live)) AssertDetached(member.Value, second[member.Key], path + "." + member.Key);
            }
        }

        static void MutateReferences(object value)
        {
            if (value == null || value is string || value.GetType().IsValueType) return;
            if (value is IEnumerable items)
            {
                foreach (object item in items.Cast<object>().ToArray()) MutateReferences(item);
                if (value is Array array)
                {
                    if (array.Length > 0) array.SetValue(null, 0);
                }
                else if (value is IList list) list.Clear();
            }
            else
            {
                foreach (var member in StoredMembers(value)) MutateReferences(member.Value);
                if (value.GetType().Namespace == "BattleCities.Core") SeedStoredFields(value, 900);
            }
        }

        static T PrivateState<T>(BattleSimulation simulation, string name)
            => (T)typeof(BattleSimulation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulation);

        static void SnapshotIsolation()
        {
            var map = new MapData
            {
                terrain = new TerrainData { regions = Array.Empty<Region>() },
                spawn = new SpawnData
                {
                    enemy = new SpawnGroup { locations = new[] { Seeded(new Point(), 10), null },
                        list = new[] { Seeded(new EnemySpec(), 20), null } },
                    player = new SpawnGroup { locations = new[] { Seeded(new Point(), 30) } }
                }
            };
            var simulation = new BattleSimulation(map);
            simulation.Terrain.Clear(); simulation.Terrain.Add(Seeded(new Wall(), 40)); simulation.Terrain.Add(null);
            simulation.Tanks.Clear(); simulation.Tanks.Add(Seeded(new TankState(), 50)); simulation.Tanks.Add(null);
            simulation.Shots.Add(Seeded(new ShotState(), 60)); simulation.Shots.Add(null);
            simulation.Mines.Add(Seeded(new MineState(), 70)); simulation.Mines.Add(null);
            simulation.Drones.Add(Seeded(new DroneState(), 80)); simulation.Drones.Add(null);
            simulation.Turrets.Add(Seeded(new TurretState(), 90)); simulation.Turrets.Add(null);
            simulation.LandDrones.Add(Seeded(new LandDroneState(), 100));
            for (int i = 0; i < simulation.Participants.Length; i++) SeedStoredFields(simulation.Participants[i], 110 + i * 10);
            var spawns = PrivateState<Point[]>(simulation, "onlineSpawns");
            for (int i = 0; i < spawns.Length - 1; i++) spawns[i] = Seeded(new Point(), 150 + i * 10);
            PrivateState<List<Wall>>(simulation, "defenceWalls").Add(simulation.Terrain[0]);
            PrivateState<List<Wall>>(simulation, "defenceBricks").Add(simulation.Terrain[0]);

            object live = simulation.ReplayStateData(), captured = simulation.CaptureReplayStateData();
            string expected = LegacyHash(live);
            Check(LegacyHash(captured) == expected && ReplayJson.Hash(captured) == expected, "snapshot fixture parity");
            AssertDetached(live, captured, "snapshot");
            var landProjection = ((Array)captured.GetType().GetProperty("LandDrones").GetValue(captured, null)).GetValue(0);
            var projectedNames = landProjection.GetType().GetProperties().Select(p => p.Name).OrderBy(n => n).ToArray();
            var storedNames = typeof(LandDroneState).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).OrderBy(n => n).ToArray();
            Check(projectedNames.SequenceEqual(storedNames), "land drone projection must cover every stored field");

            MutateReferences(live);
            SeedStoredFields(simulation.LandDrones[0], 1900);
            simulation.LandDrones[0].PatrolledTiles.Clear(); simulation.LandDrones[0].PatrolledTiles.Add(999);
            simulation.LandDrones.Clear();
            PrivateState<List<Wall>>(simulation, "defenceWalls").Clear();
            PrivateState<List<Wall>>(simulation, "defenceBricks").Clear();
            Check(ReplayJson.Hash(captured) == expected && LegacyHash(captured) == expected,
                "captured checkpoint changed after nested mutation/collection clearing");
            Check(ReplayJson.Hash(simulation.ReplayStateData()) != expected, "isolation fixture did not change live state");
        }

        static void BackgroundRecorderParity()
        {
            var map = JsonConvert.DeserializeObject<MapData>(File.ReadAllText(Path.Combine(MapDirectory(), "01.json")), LegacySettings);
            foreach (var mode in new[] { BattleMode.Offline, BattleMode.Coop, BattleMode.Versus })
            foreach (int duration in new[] { 60, 181, 420 })
            {
                var simulation = new BattleSimulation(ReplayJson.Copy(map));
                if (mode != BattleMode.Offline) simulation.ConfigureMultiplayer(mode);
                var recorder = new ReplayRecorder(simulation, map, "background-check", backgroundCheckpoints: true);
                recorder.Reseed(33);
                var expected = new SortedDictionary<int, object> { [0] = simulation.CaptureReplayStateData() };
                Check(recorder.Data.seed == 33 && recorder.Data.checkpoints.Count == 1 &&
                    recorder.Data.checkpoints[0].stateHash == LegacyHash(expected[0]), "background reseed at tick zero");
                if (mode != BattleMode.Offline)
                { simulation.SetParticipant(0, true); simulation.SetParticipant(1, true); simulation.BeginMatch(); }
                simulation.DisableEnemyFire = true;
                var commands = new Dictionary<int, Command> { [0] = default(Command), [1] = default(Command) };
                TaskCompletionSource<string> gate = null;
                if (mode == BattleMode.Offline && duration == 420)
                {
                    // Hold the worker predecessor to deterministically exercise the
                    // two-snapshot bound; production never injects this test gate.
                    gate = new TaskCompletionSource<string>();
                    typeof(ReplayRecorder).GetField("lastCheckpointWork", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(recorder, gate.Task);
                }
                try
                {
                    for (int tick = 1; tick <= duration; tick++)
                    {
                        Check(!simulation.Won && !simulation.Lost, mode + " background fixture ended early");
                        recorder.BeforeStep(default(Command), mode == BattleMode.Offline ? null : commands);
                        if (mode == BattleMode.Offline) simulation.Step(default(Command)); else simulation.StepMultiplayer(commands);
                        if (tick % 60 == 0 || tick == duration) expected[tick] = simulation.CaptureReplayStateData();
                        if (gate != null && tick == 180)
                        {
                            var release = gate;
                            Task.Run(async () => { await Task.Delay(25); release.TrySetResult(""); });
                        }
                        recorder.AfterStep();
                        Check(recorder.Error == null, "background recorder failed: " + recorder.Error);
                        if (gate != null && tick == 120)
                        {
                            var pending = (ICollection)typeof(ReplayRecorder).GetField("pendingCheckpoints", BindingFlags.Instance | BindingFlags.NonPublic)
                                .GetValue(recorder);
                            Check(pending.Count == 2, "background regression did not fill the bounded queue");
                        }
                    }
                }
                finally { if (gate != null) gate.TrySetResult(""); }
                recorder.Finish("aborted");
                Check(recorder.Finished && recorder.Error == null, "background finish failed: " + recorder.Error);
                Check(recorder.Data.checkpoints.Count == expected.Count, "background checkpoint was omitted/duplicated");
                int index = 0;
                foreach (var pair in expected)
                {
                    var checkpoint = recorder.Data.checkpoints[index++];
                    Check(checkpoint.tick == pair.Key && checkpoint.stateHash == LegacyHash(pair.Value),
                        mode + " background checkpoint differs at tick " + pair.Key);
                }
                ReplayJson.Validate(recorder.Data);
                var player = new ReplayPlayer(ReplayJson.Read(ReplayJson.Write(recorder.Data)));
                while (!player.Complete && player.Error == null) player.Step();
                Check(player.Complete, mode + " background round trip failed: " + player.Error);
            }
        }

        static float FloatBits(uint bits) => BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        static double DoubleBits(ulong bits) => BitConverter.ToDouble(BitConverter.GetBytes(bits), 0);

        sealed class PropertyFixture
        {
            public int Zebra = 7;
            [JsonProperty("a renamed", Order = 100)] public float First => -0f;
            public string Alpha => "quote\" slash\\ line\n tab\t café 中文 😀";
            [JsonIgnore] public int Excluded => throw new Exception("JsonIgnore property was read");
        }

        [JsonConverter(typeof(ReadOnlyFixtureConverter))]
        sealed class ReadOnlyConverterFixture { public int Stored = 3; public float Value = .1f; }

        public sealed class ReadOnlyFixtureConverter : JsonConverter
        {
            public override bool CanConvert(Type type) => type == typeof(ReadOnlyConverterFixture);
            public override bool CanWrite => false;
            public override bool CanRead => false;
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
                => throw new Exception("A CanWrite=false converter was invoked");
            public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
                => throw new NotSupportedException();
        }

        [JsonConverter(typeof(UnorderedFixtureConverter))]
        sealed class UnorderedConverterFixture { public float Value = .125f; }

        public sealed class UnorderedFixtureConverter : JsonConverter
        {
            public override bool CanConvert(Type type) => type == typeof(UnorderedConverterFixture);
            public override bool CanRead => false;
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("z"); writer.WriteValue(((UnorderedConverterFixture)value).Value);
                writer.WritePropertyName("A"); writer.WriteValue("first by ordinal order");
                writer.WriteEndObject();
            }
            public override object ReadJson(JsonReader reader, Type type, object existing, JsonSerializer serializer)
                => throw new NotSupportedException();
        }

        sealed class ExtensionDataFixture
        {
            public int Zebra = 1;
            [JsonExtensionData] public IDictionary<string, JToken> More = new Dictionary<string, JToken>
            {
                ["z"] = new JValue(.1f), ["A"] = new JValue("extension first")
            };
        }

        static void ValueParity()
        {
            var floats = new[]
            {
                0f, FloatBits(0x80000000), 1f, -1f, float.Epsilon, FloatBits(0x80000001),
                float.MinValue, float.MaxValue, .1f, float.PositiveInfinity, float.NegativeInfinity,
                FloatBits(0x7fc01234), FloatBits(0xffc05678), FloatBits(0x7f801234)
            };
            var doubles = new[]
            {
                0d, DoubleBits(0x8000000000000000), .1d, double.Epsilon, -double.Epsilon,
                double.MinValue, double.MaxValue, double.PositiveInfinity, double.NegativeInfinity,
                double.NaN, 1.0000000596046448d
            };
            var cases = new object[]
            {
                true, "", "quote\" slash\\ controls\b\f\n\r\t\0 café 中文 😀\ud800",
                int.MinValue, uint.MaxValue, long.MinValue, ulong.MaxValue, (byte)255, 'é', Facing.Left,
                floats, doubles, new[] { 0m, new decimal(0, 0, 0, true, 0), .1m, -1.234567890123456789m, decimal.MinValue, decimal.MaxValue },
                new object[] { null, true, "f32:00000000", 0, 0f, new object[0], new object[] { null, floats } },
                new byte[] { 0, 1, 127, 255 },
                new { Zero = (float?)null, Value = (float?)FloatBits(0x80000000), Empty = Array.Empty<int>() },
                new Dictionary<string, object>
                {
                    ["z"] = floats, ["ä"] = "unicode key", ["A"] = null, ["a"] = doubles,
                    ["quote\"\n"] = new Dictionary<string, object> { ["second"] = 2, ["first"] = 1 },
                    ["中"] = new PropertyFixture()
                },
                new Dictionary<int, string> { [2] = "two", [10] = "ten", [1] = "one" },
                new PropertyFixture(),
                new JObject { ["z"] = new JArray(1, .1d, JValue.CreateNull()), ["A"] = new JValue(FloatBits(0x80000000)) },
                new HashFieldSelectionFixture(),
                new Box(-0f, 12.5f, float.Epsilon, .33333334f),
                new Wall { Id = 2, Type = "brick", Bounds = new Box(16, 32, 16, 16), Damage = new MapDamage(), Health = 1 },
                new TurretState { Id = 9, Phase = TurretPhase.Deployed, Heading = Facing.Left, X = 123.25f, ReloadRemaining = .3f },
                new BattleParticipant { Connected = true, Lives = 2, Secondary = SecondaryAttack.LandDrone, Respawn = .125f },
                new ReadOnlyConverterFixture(), new UnorderedConverterFixture(), new ExtensionDataFixture()
            };
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;
            try
            {
                foreach (string culture in new[] { "en-US", "fr-FR", "tr-TR" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                    foreach (float value in floats) AssertParity(value, "individual f32 bits " +
                        BitConverter.ToUInt32(BitConverter.GetBytes(value), 0).ToString("x8") + " under " + culture);
                    foreach (double value in doubles) AssertParity(value, "individual f64 bits " +
                        BitConverter.ToUInt64(BitConverter.GetBytes(value), 0).ToString("x16") + " under " + culture);
                    for (int i = 0; i < cases.Length; i++) AssertParity(cases[i], "value " + i + " under " + culture);
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
            Exception legacyNull = null, currentNull = null;
            try { LegacyHash(null); } catch (Exception error) { legacyNull = error; }
            try { ReplayJson.Hash(null); } catch (Exception error) { currentNull = error; }
            Check(legacyNull != null && currentNull?.GetType() == legacyNull.GetType(), "root-null outcome changed");

            var a = new Dictionary<string, int> { ["z"] = 1, ["A"] = 2, ["a"] = 3 };
            var b = new Dictionary<string, int> { ["a"] = 3, ["A"] = 2, ["z"] = 1 };
            AssertParity(a, "dictionary insertion order A"); AssertParity(b, "dictionary insertion order B");
            Check(ReplayJson.Hash(a) == ReplayJson.Hash(b), "dictionary insertion order affects hash");
            Check(ReplayJson.Hash(new[] { 1, 2 }) != ReplayJson.Hash(new[] { 2, 1 }), "array order was discarded");
            Check(ReplayJson.Hash(0f) != ReplayJson.Hash(FloatBits(0x80000000)), "negative-zero sign was discarded");

            var tank = new TankState { Tier = 3, X = 1.25f, Y = 5, Cooldown = .125f };
            var reload = typeof(TankState).GetProperty("ReloadDuration");
            reload.SetValue(tank, .321f, null); AssertParity(tank, "tank with stored reload");
            string before = ReplayJson.Hash(tank);
            reload.SetValue(tank, .123f, null); AssertParity(tank, "tank with changed reload");
            Check(before != ReplayJson.Hash(tank), "TankState.ReloadDuration was omitted");
        }

        static string MapDirectory()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory })
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    string path = Path.Combine(directory.FullName, "Assets", "BattleCities", "Resources", "Maps");
                    if (Directory.Exists(path)) return path;
                }
            throw new Exception("Replay hash checks could not locate campaign Maps directory.");
        }

        static void CampaignParity()
        {
            string directory = MapDirectory();
            for (int stage = 1; stage <= 35; stage++)
            {
                var map = JsonConvert.DeserializeObject<MapData>(File.ReadAllText(Path.Combine(directory,
                    stage.ToString("00", CultureInfo.InvariantCulture) + ".json")), LegacySettings);
                AssertParity(map, "campaign map " + stage);
                var simulation = new BattleSimulation(map, stage);
                simulation.SetReplaySeed((uint)(87654321 + stage));
                AssertParity(simulation.ReplayConfiguration(), "campaign configuration " + stage);
                AssertSimulationParity(simulation, "campaign state " + stage + " tick 0");
                for (int tick = 1; tick <= 180 && !simulation.Won && !simulation.Lost; tick++)
                {
                    simulation.Step(new Command
                    {
                        Move = tick % 120 < 60 ? Facing.Up : Facing.Right, Aim = Facing.Up,
                        Fire = tick % 9 == 0, PowerShot = tick % 27 == 0
                    });
                    if (tick == 60 || tick == 180 || (stage == 1 && (tick == 59 || tick == 61 || tick == 120)))
                        AssertSimulationParity(simulation, "campaign state " + stage + " tick " + tick);
                }
                AssertSimulationParity(simulation, "campaign terminal state " + stage);
            }
        }

        static void RecordedFixtureParity()
        {
            foreach (var mode in new[] { BattleMode.Offline, BattleMode.Coop, BattleMode.Versus })
            {
                var data = ReplayChecks.Fixture(mode);
                AssertParity(data.map, mode + " fixture map");
                AssertParity(data.config, mode + " fixture configuration");
                AssertParity(data.claimedResult, mode + " fixture result");
                var player = new ReplayPlayer(data);
                AssertSimulationParity(player.Simulation, mode + " playback tick 0");
                while (!player.Complete && player.Error == null)
                {
                    player.Step();
                    if (player.Simulation.Tick % 60 == 0 || player.Complete)
                        AssertSimulationParity(player.Simulation, mode + " playback tick " + player.Simulation.Tick);
                }
                Check(player.Complete, mode + " fixture playback diverged: " + player.Error);
            }
        }

        public static void Run()
        {
            ValueParity();
            SnapshotIsolation();
            BackgroundRecorderParity();
            CampaignParity();
            RecordedFixtureParity();
        }
    }
}

namespace BattleCities.Core
{
    // Verify that Core's derived properties are excluded, even if evaluating one
    // would fail. Only its public stored field belongs to the checkpoint schema.
    public sealed class HashFieldSelectionFixture
    {
        public float Stored = .125f;
        public float Derived => throw new InvalidOperationException("Derived Core property was read");
    }
}
