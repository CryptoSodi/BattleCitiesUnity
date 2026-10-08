using UnityEngine;

namespace BattleCities
{
    // Local recent-score display is scoped to the API and player; server standings remain authoritative.
    public static class BattleScoreCache
    {
        static string Key(string apiUrl,string ownerId)=>"battlecities.lastScore."+Hash128.Compute((apiUrl??"")+"|"+(ownerId??"guest"));
        public static int Last(string apiUrl,string ownerId)=>PlayerPrefs.GetInt(Key(apiUrl,ownerId),0);
        public static void Record(ReplayArchive archive)
        {
            if(archive==null||!archive.NeedsMatchSubmission)return;
            int score=Mathf.Max(0,archive.replay.claimedResult.score);
            PlayerPrefs.SetInt(Key(archive.apiUrl,archive.ownerId),score);
            if(archive.ownerProvider=="guest"||string.IsNullOrEmpty(archive.ownerId))
                PlayerPrefs.SetInt("battlecities.guestHighScore",Mathf.Max(score,PlayerPrefs.GetInt("battlecities.guestHighScore",0)));
            PlayerPrefs.Save();
        }
    }
}
