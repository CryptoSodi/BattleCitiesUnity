using Fusion;

namespace BattleCities.Multiplayer
{
    // Keep each object well below Fusion's 32 KiB per-object limit.
    public sealed class BattleNetworkChunk : NetworkBehaviour
    {
        public const int Words=2048;
        [Networked] public int Kind {get;set;}
        [Networked] public int Index {get;set;}
        [Networked] public int Generation {get;set;}
        [Networked,Capacity(Words)] public NetworkArray<uint> Data=>default;
        public override void Spawned(){if(BattleSession.Instance)BattleSession.Instance.Chunks.Add(this);}
        public override void Despawned(NetworkRunner runner,bool hasState){if(BattleSession.Instance)BattleSession.Instance.Chunks.Remove(this);}
    }
}
