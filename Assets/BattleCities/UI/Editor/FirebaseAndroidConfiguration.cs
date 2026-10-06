using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace BattleCities.UI.Editor
{
    /// <summary>Generate Android Firebase resources from the package-matched client configuration.</summary>
    public sealed class FirebaseAndroidConfiguration : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) Generate();
        }
        [MenuItem("Battle Cities/Notifications/Validate Android Firebase configuration")]
        public static void Generate()
        {
            string path = "Assets/BattleCities/Notifications/google-services.json";
            if (!File.Exists(path)) throw new BuildFailedException("Register this Unity Android package in Firebase and place its google-services.json at " + path);
            var config = JObject.Parse(File.ReadAllText(path));
            string package = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            var client = (config["client"] as JArray)?.OfType<JObject>().FirstOrDefault(c => (string)c["client_info"]?["android_client_info"]?["package_name"] == package);
            if (client == null) throw new BuildFailedException("Firebase configuration does not contain the Android package " + package);
            string appId = (string)client["client_info"]?["mobilesdk_app_id"];
            string sender = (string)config["project_info"]?["project_number"];
            string project = (string)config["project_info"]?["project_id"];
            string key = (string)client["api_key"]?.First?["current_key"];
            if (new[] { appId, sender, project, key }.Any(string.IsNullOrWhiteSpace)) throw new BuildFailedException("Firebase Android configuration is missing required client identifiers.");
            var resources = new XElement("resources",
                Value("google_app_id", appId), Value("gcm_defaultSenderId", sender),
                Value("google_api_key", key), Value("project_id", project));
            string bucket = (string)config["project_info"]?["storage_bucket"];
            if (!string.IsNullOrEmpty(bucket)) resources.Add(Value("google_storage_bucket", bucket));
            string output = "Assets/Plugins/Android/BattleCitiesNotifications.androidlib/src/main/res/values/firebase-config.xml";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var document = new XDocument(new XDeclaration("1.0", "utf-8", null), resources);
            string contents = document.ToString();
            if (!File.Exists(output) || File.ReadAllText(output) != contents) { File.WriteAllText(output, contents); AssetDatabase.ImportAsset(output); }
        }
        static XElement Value(string name, string value) => new XElement("string", new XAttribute("name", name), new XAttribute("translatable", "false"), value);
    }
}
