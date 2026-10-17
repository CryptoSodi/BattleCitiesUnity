using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class AndroidTextureCompression
    {
        [Serializable] public sealed class Entry
        {
            public string path, format;
            public int width, height, maxSize;
        }
        [Serializable] public sealed class Audit { public List<Entry> textures = new List<Entry>(); }
        public static void Apply()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new Exception("Select Android before applying texture overrides.");
            const string directory = "Builds/TextureCompression";
            var audit = new Audit();
            foreach (var path in File.ReadAllLines(directory + "/targets.txt"))
            {
                // Launcher icons, editor-only previews and data textures need separate treatment.
                if (path.Contains("/Branding/AndroidIcons/") || path.Contains("/LevelEditor/") || path.Contains("/Fonts/")) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer || !importer.sRGBTexture || (importer.textureType != TextureImporterType.Default && importer.textureType != TextureImporterType.Sprite)) continue;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var settings = importer.GetPlatformTextureSettings("Android");
                var defaults = importer.GetDefaultPlatformTextureSettings();
                var format = path.Contains("/backgrounds/") || !path.Contains("/UI/") ? TextureImporterFormat.ASTC_6x6 : TextureImporterFormat.ASTC_4x4;
                var entry = new Entry { path = path, format = format.ToString(), width = texture.width, height = texture.height,
                    maxSize = settings.overridden ? settings.maxTextureSize : defaults.maxTextureSize };
                var backup = directory + "/OriginalMetadata/" + path + ".meta";
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                if (!File.Exists(backup)) File.Copy(path + ".meta", backup);
                settings.name = "Android"; settings.overridden = true;
                settings.maxTextureSize = entry.maxSize;
                settings.format = format;
                settings.textureCompression = TextureImporterCompression.Compressed;
                settings.compressionQuality = 50;
                settings.crunchedCompression = false;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
                texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture.width != entry.width || texture.height != entry.height)
                    throw new Exception("Unexpected resolution change: " + path);
                audit.textures.Add(entry);
                File.WriteAllText(directory + "/audit.json", JsonUtility.ToJson(audit, true));
                Debug.Log("ANDROID ASTC " + audit.textures.Count + ": " + path + " " + texture.width + "x" + texture.height + " " + texture.format);
            }
            AssetDatabase.SaveAssets();
            RemoteModelChecks.Run();
            Debug.Log("ANDROID TEXTURE COMPRESSION COMPLETE: " + audit.textures.Count);
        }
    }
}
