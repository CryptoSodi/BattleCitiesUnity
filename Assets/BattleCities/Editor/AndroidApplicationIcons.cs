using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BattleCities.Editor
{
    /// <summary>Official Android launcher icons and a build-time guard against PKG-007 regressions.</summary>
    public sealed class AndroidApplicationIcons : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        const string Root = "Assets/BattleCities/Branding/AndroidIcons/";
        public int callbackOrder => -1000;

        [MenuItem("Battle Cities/Android/Configure Official Application Icons")]
        public static void Configure()
        {
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "sol.battle.cities")
                throw new BuildFailedException("Expected sol.battle.cities; refusing to change the package ID.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string file in Directory.GetFiles(Root, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            PlayerSettings.productName = "Battle Cities";
#pragma warning disable CS0618
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { Load("default-1024.png") });
#pragma warning restore CS0618
            // Unity 6.6 removed the Legacy/Round Player Settings APIs. Their
            // Android resources are derived below from these same approved assets.
            Assign(AndroidPlatformIconKind.Adaptive, "adaptive");
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("Battle Cities default, legacy, round and adaptive application icons configured.");
        }

        static Texture2D Load(string name)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + name);
            if (!texture) throw new BuildFailedException("Missing official icon: " + Root + name);
            return texture;
        }

        static void Assign(PlatformIconKind kind, string prefix)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (icons.Length == 0) throw new BuildFailedException("Unity exposes no " + prefix + " icon slots.");
            foreach (var icon in icons)
            {
                if (kind == AndroidPlatformIconKind.Adaptive)
                {
                    // Unity 6.6 exports layer 0 as background and layer 1 as foreground.
                    icon.SetTextures(new[] { Load("adaptive-background-" + icon.width + ".png"), Load("adaptive-foreground-" + icon.width + ".png") });
                }
                else icon.SetTextures(new[] { Load(prefix + "-" + icon.width + ".png") });
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }

        public static void Validate()
        {
#pragma warning disable CS0618
            var defaults = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
#pragma warning restore CS0618
            if (defaults.Length == 0 || defaults.Any(texture => !IsOfficial(texture)))
                throw new BuildFailedException("Default application icon is missing or unofficial. Run Configure Official Application Icons.");
            foreach (var kind in new[] { AndroidPlatformIconKind.Adaptive })
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                int layers = kind == AndroidPlatformIconKind.Adaptive ? 2 : 1;
                if (icons.Length == 0 || icons.Any(icon => icon.GetTextures().Length != layers || icon.GetTextures().Any(texture => !IsOfficial(texture))))
                    throw new BuildFailedException("Incomplete official Android icons: " + kind + ". Run Configure Official Application Icons.");
            }
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "sol.battle.cities")
                throw new BuildFailedException("Android package ID must remain sol.battle.cities.");
            if (PlayerSettings.productName != "Battle Cities")
                throw new BuildFailedException("Android release display name must be Battle Cities.");
        }

        static bool IsOfficial(Texture2D texture) => texture && AssetDatabase.GetAssetPath(texture).StartsWith(Root, StringComparison.Ordinal);

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) Validate();
        }

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            // The launcher owns the installable application's identity resources.
            // Keep Unity's generated adaptive icon; add explicit legacy/round assets.
            string launcher = Path.GetFullPath(Path.Combine(unityLibraryPath, "../launcher/src/main"));
            if (!Directory.Exists(launcher))
                throw new BuildFailedException("Cannot locate the generated Android launcher for icon resources.");
            string resources = Path.Combine(launcher, "res");
            string[] densities = { "ldpi", "mdpi", "hdpi", "xhdpi", "xxhdpi", "xxxhdpi" };
            int[] sizes = { 36, 48, 72, 96, 144, 192 };
            for (int i = 0; i < densities.Length; i++)
            {
                string folder = Path.Combine(resources, "mipmap-" + densities[i]);
                Directory.CreateDirectory(folder);
                File.Copy(Root + "legacy-" + sizes[i] + ".png", Path.Combine(folder, "app_icon.png"), true);
                File.Copy(Root + "round-" + sizes[i] + ".png", Path.Combine(folder, "battlecities_round.png"), true);
            }
            string adaptiveFolder = Path.Combine(resources, "mipmap-anydpi-v26");
            string adaptiveIcon = Path.Combine(adaptiveFolder, "app_icon.xml");
            if (!File.Exists(adaptiveIcon))
                throw new BuildFailedException("Unity did not generate the configured adaptive application icon.");
            // Check exported pixels too: an opaque foreground would hide the crest.
            string foregroundPath = Path.Combine(resources, "mipmap-xxxhdpi/ic_launcher_foreground.png");
            var foreground = new Texture2D(2, 2);
            try
            {
                if (!foreground.LoadImage(File.ReadAllBytes(foregroundPath)) ||
                    !foreground.GetPixels32().Any(pixel => pixel.a == 0) ||
                    !foreground.GetPixels32().Any(pixel => pixel.a > 0))
                    throw new BuildFailedException("Exported adaptive foreground is not a visible transparent crest. Check layer order.");
            }
            finally { UnityEngine.Object.DestroyImmediate(foreground); }
            File.Copy(adaptiveIcon, Path.Combine(adaptiveFolder, "battlecities_round.xml"), true);
            string manifestPath = Path.Combine(launcher, "AndroidManifest.xml");
            var manifest = new XmlDocument();
            manifest.Load(manifestPath);
            var application = manifest.SelectSingleNode("/manifest/application") as XmlElement;
            if (application == null) throw new BuildFailedException("Generated launcher manifest has no application element.");
            const string android = "http://schemas.android.com/apk/res/android";
            application.SetAttribute("icon", android, "@mipmap/app_icon");
            application.SetAttribute("roundIcon", android, "@mipmap/battlecities_round");
            manifest.Save(manifestPath);
            Debug.Log("Official legacy and round launcher resources generated; adaptive layers retained.");
        }
    }
}
