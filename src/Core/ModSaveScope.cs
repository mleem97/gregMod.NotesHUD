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
            // Do NOT invalidate the captured scope on scene change: the scope
            // is scene-name based (stable across progress), and resetting it
            // here orphaned notes after every load. Only refresh the binding
            // marker so diagnostics stay current.
            _bindingChecked = false;
        }

        internal static void TickCapture()
        {
            if (_captured) return;
            try
            {
                // Stable scope: scene name only. Device counts and money are
                // deliberately excluded — they change during normal play and
                // previously orphaned the notes on every reload of the SAME
                // save (same bug class as gregMod.IPAM's old ModSaveScope).
                string sceneName = "?";
                try { sceneName = SceneManager.GetActiveScene().name ?? "?"; }
                catch { }

                _currentScopeId = HashScope("scene:" + sceneName);
                _captured = true;
            }
            catch { /* retry next tick */ }
        }

        /// <summary>True when the notes file for this scope may be used.</summary>
        internal static bool EnsureBindingChecked()
        {
            if (_bindingChecked) return _captured;
            TickCapture();
            if (!_captured)
            {
                // Capture is scene-only now, so this is rare (very early boot).
                // Return true anyway so callers use the stable fallback scope
                // instead of deferring writes that could be lost.
                _bindingChecked = true;
                return true;
            }
            _bindingChecked = true;
            try
            {
                if (LoadBindingScope() != _currentScopeId)
                    SaveBinding(_currentScopeId);
            }
            catch { /* diagnostics only */ }
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
                AtomicFile.WriteAllText(path, JsonSerializer.Serialize(new SaveBindingFile { ScopeId = scopeId }));
            }
            catch { /* best-effort */ }
        }

        private static string LoadBindingScope()
        {
            try
            {
                var path = GetBindingPath();
                if (!File.Exists(path)) return null;
                var file = JsonSerializer.Deserialize<SaveBindingFile>(AtomicFile.ReadAllTextWithBackup(path));
                return file != null ? file.ScopeId : null;
            }
            catch { return null; }
        }

        private sealed class SaveBindingFile
        {
            public string ScopeId { get; set; }
        }
    }
}
