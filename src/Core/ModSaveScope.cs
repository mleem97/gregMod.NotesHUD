// ModSaveScope — binds NotesHUD UserData JSON to a single playthrough so a
// new game does not inherit notes from a deleted/old save. Adapted from
// gregMod.IPAM's ModSaveScope (same idea, own binding file + own reset hook).
// Scope is captured lazily once gameplay objects exist.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Il2Cpp;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GregMod.NotesHUD.Core
{
    internal static class ModSaveScope
    {
        private const string SubDir = "gregMod.NotesHUD";
        private const string BindingFileName = "save_binding.json";

        private static string _currentScopeId;
        private static bool _captured;
        private static bool _bindingChecked;

        internal static bool HasScope { get { return _captured; } }

        internal static string CurrentScopeId { get { return _currentScopeId ?? "global"; } }

        internal static void NotifySceneLoaded()
        {
            _captured = false;
            _bindingChecked = false;
            _currentScopeId = null;
        }

        internal static void TickCapture()
        {
            if (_captured) return;
            try
            {
                if (MainGameManager.instance == null) return;

                var sb = new StringBuilder(128);
                try { sb.Append(SceneManager.GetActiveScene().name ?? "?"); }
                catch { sb.Append("?"); }

                try
                {
                    var servers = UnityEngine.Object.FindObjectsOfType<Server>();
                    sb.Append("|srv:").Append(servers != null ? servers.Length : 0);
                }
                catch { sb.Append("|srv:?"); }

                try
                {
                    var switches = UnityEngine.Object.FindObjectsOfType<NetworkSwitch>();
                    sb.Append("|sw:").Append(switches != null ? switches.Length : 0);
                }
                catch { sb.Append("|sw:?"); }

                try
                {
                    var pm = PlayerManager.instance;
                    var pc = pm != null ? pm.playerClass : null;
                    if (pc != null) sb.Append("|m:").Append(pc.money);
                }
                catch { }

                _currentScopeId = HashScope(sb.ToString());
                _captured = true;
            }
            catch { /* stay uncaptured until gameplay is ready */ }
        }

        /// <summary>True when the notes file for this scope may be used.</summary>
        internal static bool EnsureBindingChecked()
        {
            if (_bindingChecked) return _captured;
            TickCapture();
            if (!_captured) return false;
            _bindingChecked = true;
            SaveBinding(_currentScopeId);
            return true;
        }

        internal static string GetNotesFilePath()
        {
            return GetNotesFilePathFor(CurrentScopeId);
        }

        internal static string GetNotesFilePathFor(string scope)
        {
            if (string.IsNullOrEmpty(scope)) scope = "global";
            return Path.Combine(GetModDir(), "notes_" + scope + ".json");
        }

        internal static string GetModDir()
        {
            try
            {
                var dataPath = Application.dataPath;
                if (!string.IsNullOrEmpty(dataPath))
                {
                    var rootDir = Path.GetDirectoryName(dataPath);
                    if (!string.IsNullOrEmpty(rootDir))
                        return Path.Combine(rootDir, "UserData", SubDir);
                }
            }
            catch { }
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), SubDir);
        }

        private static string HashScope(string raw)
        {
            try
            {
                using (var sha = SHA256.Create())
                {
                    var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw ?? ""));
                    return Convert.ToHexString(bytes.AsSpan(0, 8));
                }
            }
            catch { return "global"; }
        }

        private static string GetBindingPath()
        {
            return Path.Combine(GetModDir(), BindingFileName);
        }

        private static void SaveBinding(string scopeId)
        {
            if (string.IsNullOrEmpty(scopeId)) return;
            try
            {
                var path = GetBindingPath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonSerializer.Serialize(new SaveBindingFile { ScopeId = scopeId }));
            }
            catch { /* best-effort */ }
        }

        private sealed class SaveBindingFile
        {
            public string ScopeId { get; set; }
        }
    }
}
