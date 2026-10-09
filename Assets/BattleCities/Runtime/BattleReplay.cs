using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;

namespace BattleCities.Core
{
    [Serializable] public sealed class ReplayConfig
    {
        public float normalReload, upgradedReload, turretRange, turretCooldown, turretReload, turretTurn, turretMuzzle, turretDeploy;
        public int playerTier, turretDamage, turretHealth;
        public LandDroneConfig landDrone;
    }
    [Serializable] public sealed class ReplayInputRun
    {
        public int tick, count;
        public int[] commands, secondaries;
        public bool enemyFire;
    }
    [Serializable] public sealed class ReplayEvent
    {
        public int tick, slot;
        public string kind, value;
        public bool connected;
    }
    [Serializable] public sealed class ReplayCheckpoint { public int tick; public string stateHash; }
    [Serializable] public sealed class ReplayResult
    {
        public int score, lives, remaining, winnerSlot;
        public bool won, lost;
        public static ReplayResult From(BattleSimulation s) => new ReplayResult { score=s.Score, lives=s.IsMultiplayer?s.Participants.Sum(p=>p.Lives):s.Lives, remaining=s.Remaining, winnerSlot=s.WinnerSlot, won=s.Won, lost=s.Lost };
    }
    [Serializable] public sealed class BattleReplay
    {
        public const string Format = "battlecities-unity-input-v1", SimulationVersion = "unity-sim-v1";
        public const int MaxTicks = 108000, MaxBytes = 2097152;
        public string format=Format, simulationVersion=SimulationVersion, buildVersion, id, createdAt, mode="single";
        public uint seed;
        public int levelNumber, tickRate=60, durationTicks;
        public string levelHash, configHash, completion;
        public bool debugUsed;
        public MapData map;
        public ReplayConfig config;
        public List<ReplayInputRun> inputs=new List<ReplayInputRun>();
        public List<ReplayEvent> events=new List<ReplayEvent>();
        public List<ReplayCheckpoint> checkpoints=new List<ReplayCheckpoint>();
        public ReplayResult claimedResult;
    }

    public static class ReplayJson
    {
        private sealed class StateFields : DefaultContractResolver
        {
            private readonly bool canonical;
            public StateFields(bool canonical = false) { this.canonical = canonical; }
            protected override IList<JsonProperty> CreateProperties(Type type,MemberSerialization memberSerialization)
            {
                IList<JsonProperty> properties;
                if(type.Namespace!="BattleCities.Core")properties=base.CreateProperties(type,memberSerialization);
                else
                {
                    properties=type.GetFields(BindingFlags.Public|BindingFlags.Instance).Select(f=>base.CreateProperty(f,memberSerialization)).ToList();
                    // This is stored simulation state; the other TankState properties are derived UI values.
                    if(type==typeof(TankState))properties.Add(base.CreateProperty(type.GetProperty("ReloadDuration"),memberSerialization));
                }
                if(!canonical)return properties;
                foreach(var property in properties)
                {
                    if(property.Converter!=null)property.Converter=new CanonicalFallback(property.Converter);
                    if(property.ItemConverter!=null)property.ItemConverter=new CanonicalFallback(property.ItemConverter);
                }
                // Json.NET caches these contracts, so property ordering is computed once per type.
                return properties.OrderBy(p=>p.PropertyName,StringComparer.Ordinal).ToList();
            }
            protected override JsonContract CreateContract(Type type)
            {
                var contract=base.CreateContract(type);
                if(!canonical)return contract;
                if(contract.Converter!=null&&contract.Converter.CanWrite)contract.Converter=new CanonicalFallback(contract.Converter);
                else if(contract is JsonDictionaryContract||contract is JsonDynamicContract||contract is JsonLinqContract||
                    contract is JsonISerializableContract||(contract is JsonObjectContract obj&&obj.ExtensionDataGetter!=null))
                    contract.Converter=new CanonicalFallback(null);
                if(contract is JsonContainerContract container&&container.ItemConverter!=null)
                    container.ItemConverter=new CanonicalFallback(container.ItemConverter);
                return contract;
            }
        }
        private static readonly StateFields LegacyFields=new StateFields();
        private static readonly StateFields HashFields=new StateFields(true);
        private static readonly UTF8Encoding HashEncoding=new UTF8Encoding(false);
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings {
            TypeNameHandling=TypeNameHandling.None, MaxDepth=64, DateParseHandling=DateParseHandling.None,
            Culture=System.Globalization.CultureInfo.InvariantCulture, FloatParseHandling=FloatParseHandling.Double
        };
        public static string Write(object value) => JsonConvert.SerializeObject(value, Formatting.None, Settings);
        public static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(Write(value), Settings);
        public static string Hash(object value)
        {
            if(value==null)throw new ArgumentNullException(nameof(value));
            var serializer=JsonSerializer.Create(Settings);serializer.ContractResolver=HashFields;
            using(var sha=SHA256.Create())
            using(var stream=new CryptoStream(Stream.Null,sha,CryptoStreamMode.Write))
            {
                using(var text=new StreamWriter(stream,HashEncoding,4096,true))
                using(var json=new StateHashWriter(text))
                {
                    serializer.Serialize(json,value);
                    json.Flush();
                    text.Flush();
                }
                stream.FlushFinalBlock();
                return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
            }
        }
        private sealed class StateHashWriter : JsonTextWriter
        {
            public StateHashWriter(TextWriter text):base(text)
            {
                Formatting=Formatting.None;
                Culture=System.Globalization.CultureInfo.InvariantCulture;
                CloseOutput=false;
                ArrayPool=HashCharPool.Instance;
            }
            private void Float(float value)=>base.WriteValue("f32:"+unchecked((uint)BitConverter.SingleToInt32Bits(value)).ToString("x8",System.Globalization.CultureInfo.InvariantCulture));
            public override void WriteValue(float value)=>Float(value);
            // Match JValue's numeric storage, including its handling of signed double zero.
            // Simulation fields are Single; these compatibility cases stay off that hot path.
            private void LegacyFloat(JValue value)=>Float(Convert.ToSingle(value.Value,System.Globalization.CultureInfo.InvariantCulture));
            public override void WriteValue(double value)=>LegacyFloat(new JValue(value));
            public override void WriteValue(decimal value)=>LegacyFloat(new JValue(value));
            public override void WriteValue(float? value){if(value.HasValue)Float(value.Value);else WriteNull();}
            public override void WriteValue(double? value){if(value.HasValue)WriteValue(value.Value);else WriteNull();}
            public override void WriteValue(decimal? value){if(value.HasValue)WriteValue(value.Value);else WriteNull();}
        }
        private sealed class HashCharPool : IArrayPool<char>
        {
            public static readonly HashCharPool Instance=new HashCharPool();
            public char[] Rent(int minimumLength)=>System.Buffers.ArrayPool<char>.Shared.Rent(minimumLength);
            public void Return(char[] array)=>System.Buffers.ArrayPool<char>.Shared.Return(array);
        }
        // Dictionary keys, extension data, JTokens and custom converters can emit properties
        // outside the cached object contracts. Preserve their legacy canonical form locally;
        // the simulation's ordinary field/array graph takes the streaming path.
        private sealed class CanonicalFallback : JsonConverter
        {
            private readonly JsonConverter inner;
            public CanonicalFallback(JsonConverter inner){this.inner=inner;}
            public override bool CanConvert(Type type)=>true;
            public override bool CanRead=>false;
            public override bool CanWrite=>inner==null||inner.CanWrite;
            public override object ReadJson(JsonReader reader,Type type,object existing,JsonSerializer serializer)=>throw new NotSupportedException();
            public override void WriteJson(JsonWriter writer,object value,JsonSerializer serializer)
            {
                var legacy=JsonSerializer.Create(Settings);legacy.ContractResolver=LegacyFields;
                JToken token;
                if(inner==null)token=JToken.FromObject(value,legacy);
                else
                {
                    using(var buffered=new JTokenWriter())
                    {inner.WriteJson(buffered,value,legacy);token=buffered.Token;}
                }
                Sort(token).WriteTo(writer);
            }
        }
        private static JToken Sort(JToken token)
        {
            if(token is JObject obj)return new JObject(obj.Properties().OrderBy(p=>p.Name,StringComparer.Ordinal).Select(p=>new JProperty(p.Name,Sort(p.Value))));
            if(token is JArray array)return new JArray(array.Select(Sort));
            // Preserve the existing hash representation across Mono, CoreCLR and IL2CPP.
            if(token.Type==JTokenType.Float)
            {
                float value=Convert.ToSingle(((JValue)token).Value,System.Globalization.CultureInfo.InvariantCulture);
                return new JValue("f32:"+BitConverter.ToUInt32(BitConverter.GetBytes(value),0).ToString("x8"));
            }
            return token.DeepClone();
        }
        public static BattleReplay Read(string json)
        {
            if(string.IsNullOrEmpty(json)||Encoding.UTF8.GetByteCount(json)>BattleReplay.MaxBytes)throw new FormatException("Replay exceeds the 2 MiB limit.");
            var replay=JsonConvert.DeserializeObject<BattleReplay>(json,Settings);
            Validate(replay);return replay;
        }
        private static void Require(bool ok,string message) { if(!ok)throw new FormatException(message); }
        public static bool ValidHash(string hash) => hash!=null&&hash.Length==64&&hash.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f'));
        public static void Validate(BattleReplay r)
        {
            Require(r!=null&&r.format==BattleReplay.Format&&r.simulationVersion==BattleReplay.SimulationVersion,"Unsupported replay version.");
            Require(r.tickRate==60&&r.seed!=0&&r.levelNumber>=1&&r.levelNumber<=35,"Invalid replay timing or level.");
            Require(r.mode=="single"||r.mode=="coop"||r.mode=="versus"||r.mode=="2v2"||(r.mode=="ctf"||r.mode=="ctf1v1"),"Invalid replay mode.");
            Require(r.durationTicks>0&&r.durationTicks<=BattleReplay.MaxTicks,"Invalid replay duration.");
            Require(r.completion=="completed"||r.completion=="aborted"||r.completion=="truncated","Invalid completion state.");
            Require(r.map?.field!=null&&r.config?.landDrone!=null&&r.claimedResult!=null,"Missing replay setup or result.");
            ValidateMap(r.map);
            Require(ValidHash(r.levelHash)&&r.levelHash==Hash(r.map)&&ValidHash(r.configHash)&&r.configHash==Hash(r.config),"Replay setup hash mismatch.");
            var c=r.config;
            Require(c.playerTier>=0&&c.playerTier<=3&&c.turretDamage>=1&&c.turretDamage<=100&&c.turretHealth>=1&&c.turretHealth<=100,"Invalid weapon configuration.");
            foreach(var value in new[]{c.normalReload,c.upgradedReload,c.turretRange,c.turretCooldown,c.turretReload,c.turretMuzzle,c.turretDeploy})Require(Finite(value)&&value>0&&value<=4096,"Invalid weapon setting.");
            Require(Finite(c.turretTurn)&&c.turretTurn>=0&&c.turretTurn<=60,"Invalid turret turn setting.");
            foreach(var field in typeof(LandDroneConfig).GetFields())
            { var v=Convert.ToDouble(field.GetValue(c.landDrone));Require(!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0&&v<=4096,"Invalid drone setting."); }
            Require(r.inputs!=null&&r.inputs.Count>0&&r.inputs.Count<=r.durationTicks,"Missing replay inputs.");
            int next=1;
            foreach(var run in r.inputs)
            {
                Require(run!=null&&run.tick==next&&run.count>0&&run.count<=r.durationTicks-next+1,"Replay input ticks have a gap or overlap.");
                Require(run.commands?.Length==4&&run.secondaries?.Length==4,"Replay needs four input slots.");
                foreach(int command in run.commands)Require(command>=0&&command<=511&&(command&7)<=4&&((command>>3)&7)<=4,"Invalid packed input.");
                foreach(int secondary in run.secondaries)Require(secondary>=0&&secondary<=4,"Invalid secondary weapon.");
                if(r.mode=="single")Require(run.commands.Skip(1).All(v=>v==0),"Single-player replay contains remote inputs.");
                next+=run.count;
            }
            Require(next==r.durationTicks+1,"Replay input duration mismatch.");
            Require(r.events!=null&&r.events.Count<=4096,"Too many replay events.");
            int last=1;
            foreach(var e in r.events)
            {
                Require(e!=null&&e.tick>=last&&e.tick<=r.durationTicks,"Invalid event order.");last=e.tick;
                Require(e.slot>=0&&e.slot<4,"Invalid event slot.");
                Require(e.kind=="pickup"||e.kind=="powerup"||e.kind=="participant"||e.kind=="begin"||((r.mode=="ctf"||r.mode=="ctf1v1")&&e.kind=="flagdrop"),"Unsupported replay event.");
                if(e.kind=="pickup"||e.kind=="powerup")Require((e.kind=="pickup"&&e.value==null)||Powerups.Contains(e.value),"Invalid replay power-up.");
                else Require(r.mode!="single","Single-player replay contains a network event.");
            }
            Require(r.checkpoints!=null&&r.checkpoints.Count>=2&&r.checkpoints.Count<=1802,"Missing replay checkpoints.");
            int index=0;
            for(int tick=0;tick<r.durationTicks;tick+=60)
            { Require(index<r.checkpoints.Count&&r.checkpoints[index]?.tick==tick&&ValidHash(r.checkpoints[index].stateHash),"Missing periodic checkpoint.");index++; }
            Require(index==r.checkpoints.Count-1&&r.checkpoints[index]?.tick==r.durationTicks&&ValidHash(r.checkpoints[index].stateHash),"Missing terminal checkpoint.");
            Require(r.claimedResult.score>=0&&r.claimedResult.lives>=0&&r.claimedResult.remaining>=0&&r.claimedResult.winnerSlot>=-1&&r.claimedResult.winnerSlot<4,"Invalid claimed result.");
            Require(r.completion!="completed"||r.claimedResult.won||r.claimedResult.lost,"Completed replay has no outcome.");
        }
        public static readonly string[] Powerups={"shield","defence","freeze","life","speed","upgrade","zoomout","wipeout","batc100","batc200"};
        private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        private static void ValidateMap(MapData m)
        {
            Require(m.field.widthTiles>=4&&m.field.widthTiles<=64&&m.field.heightTiles>=4&&m.field.heightTiles<=64,"Invalid map dimensions.");
            int w=m.field.widthTiles*64,h=m.field.heightTiles*64;
            var regions=(m.terrain?.regions??Array.Empty<Region>()).Concat(m.ground?.regions??Array.Empty<Region>()).ToArray();
            Require(regions.Length<=4096&&(m.objects?.Length??0)<=4096&&(m.lights?.Length??0)<=512,"Map exceeds replay limits.");
            double cells=0;
            foreach(var region in regions)
            { Require(region!=null&&!string.IsNullOrEmpty(region.type)&&region.type.Length<=80,"Invalid map region.");Bounds(region.x,region.y,region.width,region.height,w,h);cells+=Math.Ceiling(region.width/16)*Math.Ceiling(region.height/16); }
            foreach(var item in m.objects??Array.Empty<MapObjectData>())
            { Require(item!=null&&!string.IsNullOrEmpty(item.type)&&item.type.Length<=128&&Finite(item.rotation),"Invalid map object.");Bounds(item.x,item.y,item.width,item.height,w,h);cells+=Math.Ceiling(item.width/32)*Math.Ceiling(item.height/32); }
            Require(cells<=32768,"Map has too many simulation cells.");
            foreach(var group in new[]{m.spawn?.enemy,m.spawn?.player})
            {
                Require((group?.locations?.Length??0)<=64&&(group?.list?.Length??0)<=1000,"Too many spawn entries.");
                foreach(var p in group?.locations??Array.Empty<Point>())Require(p!=null&&Finite(p.x)&&Finite(p.y)&&p.x>=0&&p.y>=0&&p.x<=w-64&&p.y<=h-64,"Invalid spawn.");
                foreach(var enemy in group?.list??Array.Empty<EnemySpec>())Require(enemy!=null&&enemy.tier!=null&&enemy.tier.Length==1&&"abcd".Contains(enemy.tier),"Invalid enemy.");
            }
            if(m.@base!=null)Require(Finite(m.@base.x)&&Finite(m.@base.y)&&m.@base.x>=0&&m.@base.y>=0&&m.@base.x<=w-128&&m.@base.y<=h-96,"Invalid base.");
        }
        private static void Bounds(float x,float y,float width,float height,int w,int h)=>Require(Finite(x)&&Finite(y)&&Finite(width)&&Finite(height)&&x>=0&&y>=0&&width>0&&height>0&&x+width<=w&&y+height<=h,"Map region outside bounds.");
    }

    public sealed class ReplayRecorder
    {
        public BattleReplay Data {get;}
        public bool Finished {get;private set;}
        public string Error {get;private set;}
        private readonly BattleSimulation simulation;
        private readonly bool backgroundCheckpoints;
        private const int MaxPendingCheckpoints=2;
        private readonly Queue<PendingCheckpoint> pendingCheckpoints=new Queue<PendingCheckpoint>();
        private Task<string> lastCheckpointWork;
        private sealed class PendingCheckpoint
        {
            public object snapshot;
            public ReplayCheckpoint checkpoint;
            public Task<string> work;
        }
        public ReplayRecorder(BattleSimulation simulation,MapData map,string buildVersion,bool backgroundCheckpoints=false)
        {
            this.simulation=simulation;
#if UNITY_WEBGL && !UNITY_EDITOR
            this.backgroundCheckpoints=false;
#else
            this.backgroundCheckpoints=backgroundCheckpoints;
#endif
            if(simulation.Tick!=0)throw new InvalidOperationException("Recording must start before tick one.");
            Data=new BattleReplay { id=Guid.NewGuid().ToString("N"),createdAt=DateTime.UtcNow.ToString("O"),buildVersion=buildVersion,
                mode=simulation.Mode==BattleMode.Offline?"single":simulation.Mode==BattleMode.Coop?"coop":simulation.Mode==BattleMode.CaptureFlagDuel?"ctf1v1":simulation.IsCaptureFlag?"ctf":simulation.IsTeamBattle?"2v2":"versus",seed=simulation.ReplaySeed,
                levelNumber=simulation.Stage,map=ReplayJson.Copy(map),config=simulation.ReplayConfiguration() };
            // Legacy campaign JSON omits field dimensions and relies on the engine's
            // 13x13 default. Persist the resolved dimensions so evidence is self-contained.
            if(Data.map.field==null)Data.map.field=new FieldData {widthTiles=simulation.Width/64,heightTiles=simulation.Height/64};
            Data.levelHash=ReplayJson.Hash(Data.map);Data.configHash=ReplayJson.Hash(Data.config);
            Checkpoint();simulation.ReplayLifecycle+=RecordEvent;
        }
        public void Reseed(uint seed) { if(simulation.Tick!=0)throw new InvalidOperationException("Session seed arrived after gameplay started.");simulation.SetReplaySeed(seed);Data.seed=seed;Data.checkpoints.Clear();Checkpoint(); }
        public void RecordEvent(ReplayEvent e)
        {
            if(Finished)return;
            if(Data.events.Count>=4096){Finish("truncated");return;}
            e.tick=simulation.Tick+1;Data.events.Add(ReplayJson.Copy(e));
            if(e.kind=="powerup")Data.debugUsed|=e.value=="life";
        }
        public void BeforeStep(Command command,IReadOnlyDictionary<int,Command> online=null)
        {
            if(Finished)return;
            if(Data.inputs.Count>=10000){Finish("truncated");return;}
            var run=new ReplayInputRun { tick=simulation.Tick+1,count=1,commands=new int[4],secondaries=new int[4],enemyFire=!simulation.DisableEnemyFire };
            for(int i=0;i<4;i++)
            { Command cmd=default;if(online!=null)online.TryGetValue(i,out cmd);else if(i==0)cmd=command;run.commands[i]=Pack(cmd);run.secondaries[i]=simulation.IsMultiplayer?(int)simulation.Participants[i].Secondary:i==0?(int)simulation.EquippedSecondary:0; }
            Data.debugUsed|=!run.enemyFire;
            var prior=Data.inputs.LastOrDefault();
            if(prior!=null&&prior.enemyFire==run.enemyFire&&prior.commands.SequenceEqual(run.commands)&&prior.secondaries.SequenceEqual(run.secondaries))prior.count++;
            else Data.inputs.Add(run);
        }
        public void AfterStep()
        {
            if(Finished)return;
            while(pendingCheckpoints.Count>0&&pendingCheckpoints.Peek().work.IsCompleted)
                CompleteOldestCheckpoint();
            if(Finished)return;
            Data.durationTicks=simulation.Tick;
            if(simulation.Tick%60==0)Checkpoint();
            if(simulation.Won||simulation.Lost)Finish("completed");
            else if(simulation.Tick>=BattleReplay.MaxTicks)Finish("truncated");
        }
        public void Finish(string reason)
        {
            if(Finished)return;Finished=true;simulation.ReplayLifecycle-=RecordEvent;
            Data.durationTicks=simulation.Tick;Data.events.RemoveAll(e=>e.tick>Data.durationTicks);
            Data.completion=reason;Data.claimedResult=ReplayResult.From(simulation);
            if(Data.checkpoints.Last().tick!=simulation.Tick)Checkpoint();
            // Saving/uploading starts only after every captured checkpoint is complete.
            while(pendingCheckpoints.Count>0&&Error==null)CompleteOldestCheckpoint();
        }
        private void Checkpoint()
        {
            // Session preparation needs tick zero immediately. Browser players and the
            // verifier retain the synchronous path; native recording opts into workers.
            if(!backgroundCheckpoints||simulation.Tick==0)
            {
                Data.checkpoints.Add(new ReplayCheckpoint{tick=simulation.Tick,stateHash=simulation.ReplayStateHash()});
                return;
            }
            // Bound retained snapshots and worker backlog, even if recording is driven
            // faster than real time. No checkpoint may be skipped to recover frame time.
            while(pendingCheckpoints.Count>=MaxPendingCheckpoints&&Error==null)CompleteOldestCheckpoint();
            if(Error!=null)return;
            try
            {
                object snapshot=simulation.CaptureReplayStateData();
                var checkpoint=new ReplayCheckpoint{tick=simulation.Tick};
                Data.checkpoints.Add(checkpoint);
                // Continuations serialize work on the pool, never on Unity's context.
                // Workers own only detached state and return a string; the game thread
                // alone updates the replay/checkpoint list.
                Task<string> work=lastCheckpointWork==null
                    ?Task.Run(()=>ReplayJson.Hash(snapshot))
                    :lastCheckpointWork.ContinueWith(_=>ReplayJson.Hash(snapshot),TaskScheduler.Default);
                pendingCheckpoints.Enqueue(new PendingCheckpoint{snapshot=snapshot,checkpoint=checkpoint,work=work});
                lastCheckpointWork=work;
            }
            catch(Exception error){FailCheckpoint(error);}
        }
        private void CompleteOldestCheckpoint()
        {
            var pending=pendingCheckpoints.Dequeue();
            try
            {
                try {pending.checkpoint.stateHash=pending.work.GetAwaiter().GetResult();}
                catch {pending.checkpoint.stateHash=ReplayJson.Hash(pending.snapshot);}
                if(pendingCheckpoints.Count==0)lastCheckpointWork=null;
            }
            catch(Exception error){FailCheckpoint(error);}
        }
        private void FailCheckpoint(Exception error)
        {
            Error="Checkpoint hashing failed ("+error.GetType().Name+").";
            Finished=true;simulation.ReplayLifecycle-=RecordEvent;
            while(pendingCheckpoints.Count>0)
            {
                var abandoned=pendingCheckpoints.Dequeue().work;
                // These detached tasks may finish after recording is abandoned.
                // Observe failures without touching Unity or the invalid archive.
                abandoned.ContinueWith(task=>{var observed=task.Exception;},
                    System.Threading.CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
            lastCheckpointWork=null;
        }
        public static int Pack(Command c)=>((int?)c.Move+1??0)|(((int?)c.Aim+1??0)<<3)|(c.Fire?64:0)|(c.PowerShot?128:0)|(c.SecondaryFire?256:0);
        public static Command Unpack(int bits)=>new Command { Move=(bits&7)==0?(Facing?)null:(Facing)((bits&7)-1),Aim=((bits>>3)&7)==0?(Facing?)null:(Facing)(((bits>>3)&7)-1),Fire=(bits&64)!=0,PowerShot=(bits&128)!=0,SecondaryFire=(bits&256)!=0 };
    }

    public sealed class ReplayPlayer
    {
        public BattleReplay Data {get;}
        public BattleSimulation Simulation {get;}
        public bool Complete {get;private set;}
        public string Error {get;private set;}
        private int runIndex,eventIndex,checkpointIndex;
        private readonly Dictionary<int,Command> commands=new Dictionary<int,Command>();
        public ReplayPlayer(BattleReplay data,BattleSimulation simulation=null)
        {
            ReplayJson.Validate(data);Data=data;
            Simulation=simulation??CreateSimulation(data);
            if(Simulation.Tick!=0)throw new InvalidOperationException("Replay requires a fresh simulation.");
            Check();
        }
        public static BattleSimulation CreateSimulation(BattleReplay data)
        {
            var c=data.config;
            var sim=new BattleSimulation(ReplayJson.Copy(data.map),data.levelNumber,c.normalReload,c.upgradedReload,c.playerTier);
            sim.ApplyReplayConfiguration(c);sim.SetReplaySeed(data.seed);
            if(data.mode!="single")sim.ConfigureMultiplayer(data.mode=="coop"?BattleMode.Coop:data.mode=="ctf1v1"?BattleMode.CaptureFlagDuel:data.mode=="ctf"?BattleMode.CaptureFlag:data.mode=="2v2"?BattleMode.TeamBattle:BattleMode.Versus);
            return sim;
        }
        public bool Step()
        {
            if(Complete||Error!=null)return false;
            int tick=Simulation.Tick+1;
            while(eventIndex<Data.events.Count&&Data.events[eventIndex].tick==tick)ApplyEvent(Simulation,Data.events[eventIndex++]);
            var run=Data.inputs[runIndex];
            if(tick>=run.tick+run.count)run=Data.inputs[++runIndex];
            Simulation.DisableEnemyFire=!run.enemyFire;
            commands.Clear();
            for(int i=0;i<4;i++){commands[i]=ReplayRecorder.Unpack(run.commands[i]);if(Simulation.IsMultiplayer)Simulation.Participants[i].Secondary=(SecondaryAttack)run.secondaries[i];}
            if(Simulation.IsMultiplayer)Simulation.StepMultiplayer(commands);
            else {Simulation.EquippedSecondary=(SecondaryAttack)run.secondaries[0];Simulation.Step(commands[0]);}
            if(Simulation.Tick!=tick){Error="Replay stopped advancing at tick "+tick;return false;}
            Check();
            if(Simulation.Tick==Data.durationTicks&&Error==null)
            {
                if(ReplayJson.Hash(ReplayResult.From(Simulation))!=ReplayJson.Hash(Data.claimedResult))Error="Replay result differs from the recording.";
                else Complete=true;
            }
            return Error==null;
        }
        private void Check()
        {
            if(checkpointIndex>=Data.checkpoints.Count||Data.checkpoints[checkpointIndex].tick!=Simulation.Tick)return;
            if(Data.checkpoints[checkpointIndex++].stateHash!=Simulation.ReplayStateHash())Error="Replay diverged at tick "+Simulation.Tick+".";
        }
        public static void ApplyEvent(BattleSimulation sim,ReplayEvent e)
        {
            switch(e.kind)
            {
                case "pickup":sim.SpawnPickup(e.value);break;
                case "powerup":sim.ApplyPowerup(e.value);break;
                case "participant":sim.SetParticipant(e.slot,e.connected);break;
                case "flagdrop":sim.DropCarriedFlag(e.slot);break;
                case "begin":sim.BeginMatch();break;
                default:throw new FormatException("Unsupported replay event.");
            }
        }
    }
}
