using System.Linq;
using Fusion;
using UnityEngine;

namespace BattleCities.Multiplayer
{
    public sealed partial class BattleSession
    {
        public readonly System.Collections.Generic.List<BattleNetworkChunk> Chunks=new System.Collections.Generic.List<BattleNetworkChunk>();
        public BattleNetworkChunk Chunk(int kind,int index)=>Chunks.FirstOrDefault(c=>c&&c.Runner==Runner&&c.Kind==kind&&c.Index==index);
    }
    public sealed partial class BattleNetworkMatch
    {
        const int TerrainChunks=4,RecoveryChunks=8;
        bool restorePending;
        readonly BattleNetworkChunk[] terrainData=new BattleNetworkChunk[TerrainChunks],recoveryData=new BattleNetworkChunk[RecoveryChunks];
        bool HasChunks
        {
            get
            {
                for(int i=0;i<TerrainChunks;i++){if(!terrainData[i])terrainData[i]=BattleSession.Instance.Chunk(0,i);if(!terrainData[i])return false;}
                for(int i=0;i<RecoveryChunks;i++){if(!recoveryData[i])recoveryData[i]=BattleSession.Instance.Chunk(1,i);if(!recoveryData[i])return false;}
                return true;
            }
        }
        void SpawnChunks()
        {
            var prefab=Resources.Load<NetworkObject>("Multiplayer/BattleNetworkChunk");
            for(int kind=0;kind<2;kind++)for(int index=0;index<(kind==0?TerrainChunks:RecoveryChunks);index++)
            {
                int k=kind,n=index;
                Runner.Spawn(prefab,onBeforeSpawned:(r,obj)=>{var chunk=obj.GetComponent<BattleNetworkChunk>();chunk.Kind=k;chunk.Index=n;});
            }
        }
        uint HealthPair(int index)=>terrainData[index/BattleNetworkChunk.Words].Data[index%BattleNetworkChunk.Words];
        void SetHealthPair(int index,uint value)=>terrainData[index/BattleNetworkChunk.Words].Data.Set(index%BattleNetworkChunk.Words,value);
        void WriteCheckpoint(byte[] bytes)
        {
            for(int offset=0;offset<bytes.Length;offset+=4)
            {
                uint word=0;for(int b=0;b<4&&offset+b<bytes.Length;b++)word|=(uint)bytes[offset+b]<<(b*8);
                int index=offset/4;
                recoveryData[index/BattleNetworkChunk.Words].Data.Set(index%BattleNetworkChunk.Words,word);
            }
        }
        byte[] ReadCheckpoint()
        {
            var bytes=new byte[CheckpointLength];
            for(int i=0;i<bytes.Length;i++)
            {int word=i/4;bytes[i]=(byte)(recoveryData[word/BattleNetworkChunk.Words].Data[word%BattleNetworkChunk.Words]>>((i%4)*8));}
            return bytes;
        }
    }
}
