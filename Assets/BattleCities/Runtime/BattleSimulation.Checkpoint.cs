using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BattleCities.Core
{
    public sealed partial class BattleSimulation
    {
        // Host migration needs the authoritative timers, RNG and AI paths too;
        // a render frame alone cannot resume the combat simulation.
        public byte[] SaveNetworkCheckpoint()
        {
            var envelope=new {version=1,state=ReplayStateData(false),config=ReplayConfiguration(),
                InitialTerrainCount,visualSequence,ResultStats,RivalBaseAlive,RivalBaseBounds,teamSpawnTimers,teamSpawnCounts,Flags,FlagScores};
            var bytes=System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(envelope));
            using(var output=new MemoryStream())
            {
                using(var zip=new GZipStream(output,CompressionLevel.Fastest,true))zip.Write(bytes,0,bytes.Length);
                return output.ToArray();
            }
        }

        public void RestoreNetworkCheckpoint(byte[] bytes)
        {
            if(bytes==null||bytes.Length==0||bytes.Length>65536)throw new InvalidDataException("Invalid match checkpoint size.");
            JObject root;
            using(var input=new MemoryStream(bytes))using(var zip=new GZipStream(input,CompressionMode.Decompress))
            using(var output=new MemoryStream())
            {
                var buffer=new byte[8192];int count;
                while((count=zip.Read(buffer,0,buffer.Length))>0)
                {if(output.Length+count>8*1024*1024)throw new InvalidDataException("Match checkpoint exceeds limit.");output.Write(buffer,0,count);}
                root=JObject.Parse(System.Text.Encoding.UTF8.GetString(output.ToArray()));
            }
            if((int)root["version"]!=1)throw new InvalidDataException("Unsupported match checkpoint.");
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var state=(JObject)root["state"];
            // Keys are limited to those authored by ReplayStateData, never arbitrary type names.
            foreach(var item in state.Properties())
            {
                if(item.Name=="defenceWalls"||item.Name=="defenceBricks")continue;
                var field=typeof(BattleSimulation).GetField(item.Name,flags);
                if(field!=null)
                {
                    object value=item.Value.ToObject(field.FieldType);
                    if(field.IsInitOnly)
                    {
                        if(field.GetValue(this) is Array array)Array.Copy((Array)value,array,array.Length);
                        else if(field.GetValue(this) is IList list){list.Clear();foreach(var entry in (IEnumerable)value)list.Add(entry);}
                    }
                    else field.SetValue(this,value);
                }
                else
                {
                    var property=typeof(BattleSimulation).GetProperty(item.Name,flags);
                    if(property?.GetSetMethod(true)!=null)property.SetValue(this,item.Value.ToObject(property.PropertyType));
                }
            }
            defenceWalls.Clear();defenceBricks.Clear();
            foreach(int id in state["defenceWalls"].ToObject<int[]>())defenceWalls.Add(Terrain.Single(w=>w.Id==id));
            foreach(int id in state["defenceBricks"].ToObject<int[]>())defenceBricks.Add(Terrain.Single(w=>w.Id==id));
            InitialTerrainCount=(int)root["InitialTerrainCount"];visualSequence=0;
            Array.Copy(root["ResultStats"].ToObject<BattleResultStats[]>(),ResultStats,MaxPlayers);
            RivalBaseAlive=(bool)root["RivalBaseAlive"];RivalBaseBounds=root["RivalBaseBounds"].ToObject<Box>();
            Array.Copy(root["teamSpawnTimers"].ToObject<float[]>(),teamSpawnTimers,2);
            Array.Copy(root["teamSpawnCounts"].ToObject<int[]>(),teamSpawnCounts,2);
            if(root["Flags"]!=null)Array.Copy(root["Flags"].ToObject<BattleFlag[]>(),Flags,2);
            if(root["FlagScores"]!=null)Array.Copy(root["FlagScores"].ToObject<int[]>(),FlagScores,2);
            ApplyReplayConfiguration(root["config"].ToObject<ReplayConfig>());
            VisualEvents.Clear();actingSlot=-1;onlineCommands=null;TerrainChanged?.Invoke();
        }
    }
}
