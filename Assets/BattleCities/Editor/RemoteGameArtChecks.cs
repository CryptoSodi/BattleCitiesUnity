using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace BattleCities.Editor
{
    public static class RemoteGameArtChecks
    {
        public static void Run()
        {
            var path = Path.Combine(Application.temporaryCachePath, "remote-art-test-" + Guid.NewGuid().ToString("N"));
            int checks = 0;
            void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
            void Reject(RemoteGameArt.Manifest candidate, string label)
            {
                bool rejected = false;
                try { RemoteGameArt.Validate(candidate); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, label);
            }
            try
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes("complete game content");
                File.WriteAllBytes(path, bytes);
                string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                RemoteGameArt.Manifest Good() => new RemoteGameArt.Manifest { url = "https://assets.example.invalid/android/" + hash + ".bundle",
                    sha256 = hash, bytes = bytes.Length, unityVersion = Application.unityVersion };
                var manifest = Good(); RemoteGameArt.Validate(manifest);
                Check(RemoteGameArt.VerifyFile(path, manifest), "valid cache is reusable");
                Check(!RemoteGameArt.VerifyFile(path + ".missing", manifest), "missing cache downloads again");
                File.WriteAllText(path, "partial");
                Check(!RemoteGameArt.VerifyFile(path, manifest), "truncated content rejected");
                File.WriteAllBytes(path, new byte[bytes.Length]);
                Check(!RemoteGameArt.VerifyFile(path, manifest), "same-length corrupt content rejected");
                manifest = Good(); manifest.url = "http://example.invalid/art"; Reject(manifest, "HTTP rejected");
                manifest = Good(); manifest.url = "https://name:password@example.invalid/art"; Reject(manifest, "credentials rejected");
                manifest = Good(); manifest.sha256 = "../../payload"; Reject(manifest, "unsafe cache key rejected");
                manifest = Good(); manifest.bytes = 0; Reject(manifest, "zero length rejected");
                manifest = Good(); manifest.bytes = 1024L * 1024 * 1024 + 1; Reject(manifest, "oversize rejected");
                manifest = Good(); manifest.unityVersion = "different-engine"; Reject(manifest, "incompatible engine rejected");
                Reject(null, "missing manifest rejected");
                Check(RemoteGameArt.Load<UI.ShopArt>("ShopArt") != null, "ordinary build keeps local artwork");
                var connection = new RemoteGameArt.DownloadException(UnityEngine.Networking.UnityWebRequest.Result.ConnectionError,
                    0, "Cannot resolve destination host", 0);
                Check(connection.IsTransient, "pre-HTTP connection failures retry as connectivity issues");
                Check(connection.Message.Contains("Cannot resolve destination host") && connection.Message.Contains("HTTP 0"),
                    "preserve native transport diagnostics instead of only HTTP zero");
                foreach (long code in new long[] { 408, 429, 500, 503 })
                    Check(new RemoteGameArt.DownloadException(UnityEngine.Networking.UnityWebRequest.Result.ProtocolError,
                        code, "unavailable", 0).IsTransient, "transient HTTP failure " + code);
                Check(!new RemoteGameArt.DownloadException(UnityEngine.Networking.UnityWebRequest.Result.ProtocolError,
                    404, "missing", 0).IsTransient, "missing content is not reported as a connection issue");
                Check(!new RemoteGameArt.DownloadException(UnityEngine.Networking.UnityWebRequest.Result.DataProcessingError,
                    200, "file write failed", 20).IsTransient, "local storage failure is not reported as a connection issue");
                Debug.Log("REMOTE ART CHECKS PASSED: " + checks);
                Directory.CreateDirectory("Builds/RemoteArtChecks");
                File.WriteAllText("Builds/RemoteArtChecks/result.txt", "PASS " + checks);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
