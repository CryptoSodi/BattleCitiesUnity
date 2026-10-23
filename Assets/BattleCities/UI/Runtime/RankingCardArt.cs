using UnityEngine;

namespace BattleCities.UI
{
    public sealed class RankingCardArt : ScriptableObject
    {
        public Sprite goldRow,silverRow,bronzeRow,creamRow,blueRow,yourRow;
        public Sprite goldLaurel,silverLaurel,bronzeLaurel;
        public Sprite Row(int rank,int index,bool yours)=>yours?yourRow:rank==1?goldRow:rank==2?silverRow:rank==3?bronzeRow:index%2==0?blueRow:creamRow;
        public Sprite Medal(int rank)=>rank==1?goldLaurel:rank==2?silverLaurel:rank==3?bronzeLaurel:null;
    }
}
