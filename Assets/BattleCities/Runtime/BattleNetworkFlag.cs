using BattleCities.Core;
using Fusion;
namespace BattleCities.Multiplayer
{
    public struct NetBattleFlag : INetworkStruct
    {
        public int CarrierId;
        public float X,Y,ReturnSeconds;
        public NetworkBool AtHome;
        public static NetBattleFlag From(BattleFlag f)=>new NetBattleFlag {CarrierId=f.CarrierId,X=f.X,Y=f.Y,ReturnSeconds=f.ReturnSeconds,AtHome=f.AtHome};
        public BattleFlag ToState()=>new BattleFlag {CarrierId=CarrierId,X=X,Y=Y,ReturnSeconds=ReturnSeconds,AtHome=AtHome};
    }
}
