using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using BattleCities.Core;

namespace BattleCities
{
    [Serializable] public sealed class ReplayArchive
    {
        public BattleReplay replay;
        public string apiUrl, ownerId, sessionId, replayId, matchId, uploadStatus="Local recording", verificationStatus="unverified";
    }
    public static class BattleReplayStore
    {
#if UNITY_EDITOR
        public static string EditorDirectoryOverride;
#endif
        public static string DirectoryPath {
            get {
#if UNITY_EDITOR
                if(!string.IsNullOrEmpty(EditorDirectoryOverride))return EditorDirectoryOverride;
#endif
                return Path.Combine(Application.persistentDataPath,"Replays");
            }
        }
        public static string LastError {get;private set;}
        public static string Save(ReplayArchive archive)
        {
            if(archive?.replay==null||archive.replay.durationTicks<=0)return null;
            ReplayJson.Validate(archive.replay);
            string json=ReplayJson.Write(archive);
            if(System.Text.Encoding.UTF8.GetByteCount(ReplayJson.Write(archive.replay))>BattleReplay.MaxBytes)throw new IOException("Recording exceeds the replay size limit.");
            Directory.CreateDirectory(DirectoryPath);
            string path=PathFor(archive.replay.id),temporary=path+".tmp";
            File.WriteAllText(temporary,json);
            if(File.Exists(path))File.Copy(path,path+".bak",true);
            File.Copy(temporary,path,true);File.Delete(temporary);
            var files=new DirectoryInfo(DirectoryPath).GetFiles("*.json").OrderByDescending(f=>f.LastWriteTimeUtc).ToArray();
            foreach(var old in files.Skip(20))
            {
                var item=Load(old.Name.Substring(0,old.Name.Length-5));
                // Keep unsubmitted evidence; never prune an outstanding server session.
                if(item!=null&&!string.IsNullOrEmpty(item.sessionId)&&string.IsNullOrEmpty(item.replayId))continue;
                old.Delete();if(File.Exists(old.FullName+".bak"))File.Delete(old.FullName+".bak");
            }
            FlushWeb();return path;
        }
        private static string PathFor(string id)
        { if(!Guid.TryParseExact(id,"N",out _))throw new FormatException("Invalid local replay ID.");return Path.Combine(DirectoryPath,id+".json"); }
        public static ReplayArchive Load(string id)
        {
            try
            {
                string path=PathFor(id);
                if(!File.Exists(path))return null;
                try{return Parse(File.ReadAllText(path));}
                catch {if(File.Exists(path+".bak"))return Parse(File.ReadAllText(path+".bak"));throw;}
            }
            catch(Exception e){LastError=e.Message;return null;}
        }
        public static ReplayArchive Parse(string json)
        {
            if(System.Text.Encoding.UTF8.GetByteCount(json)>BattleReplay.MaxBytes+16384)throw new FormatException("Replay archive too large.");
            var archive=JsonConvert.DeserializeObject<ReplayArchive>(json,ReplayJson.Settings);
            ReplayJson.Validate(archive?.replay);return archive;
        }
        public static List<ReplayArchive> List()
        {
            var result=new List<ReplayArchive>();
            try
            {
                if(!Directory.Exists(DirectoryPath))return result;
                foreach(var file in new DirectoryInfo(DirectoryPath).GetFiles("*.json").OrderByDescending(f=>f.LastWriteTimeUtc).Take(100))
                {var item=Load(Path.GetFileNameWithoutExtension(file.Name));if(item!=null)result.Add(item);}
            }
            catch(Exception e){LastError=e.Message;}
            return result;
        }
        public static void FlushWeb()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            BattleCitiesReplaySync();
#endif
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] private static extern void BattleCitiesReplaySync();
#endif
    }
}
