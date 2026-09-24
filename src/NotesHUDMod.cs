// gregMod.NotesHUD — in-game post-it notebook (IPs, shop item #s, notes).
// Standalone-safe (no hard gregCore dependency): GregHost probe + JIT-split
// NotesCoreBridge for F1-hub (menu/HUD/toggle), input locks and per-save
// mirror. Text input is a custom InputSystem pump (SafeTextPump) — never
// GUI.TextField / TextField (IL2CPP TextEditor is stripped -> hard crash).
using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using GregMod.NotesHUD.Core;
using GregMod.NotesHUD.UI;
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ModCoverage.Tests")]

[assembly: MelonInfo(typeof(GregMod.NotesHUD.NotesHUDMod), "gregMod.NotesHUD", "1.0.0", "TeamGreg Modding")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace GregMod.NotesHUD
{
    public sealed class NotesHUDMod : MelonMod
    {
        internal static NotesHUDMod Instance { get; private set; }

        private static MelonPreferences_Entry<string> ToggleKeyEntry;
        internal static Key ToggleKey = Key.F10;

        private float _statusRefreshAt;

        public override void OnInitializeMelon()
        {
            try
            {
                Instance = this;

                var cat = MelonPreferences.CreateCategory("gregMod.NotesHUD", "NotesHUD");
                ToggleKeyEntry = cat.CreateEntry("ToggleKey", "F10", "Notebook Toggle Key",
                    "Input System key to open/close the notebook (e.g. F10, F3). Letter keys are NOT recommended (they type into the notebook).");
                cat.SaveToFile(false);

                if (Enum.TryParse<Key>(ToggleKeyEntry.Value, true, out var k) && k != Key.None)
                    ToggleKey = k;
                else
                    LoggerInstance.Warning("[NotesHUD] Unknown key '" + ToggleKeyEntry.Value + "', defaulting to F10.");

                NotesOverlay.EnsureRegistered();

                if (GregHost.HasCore)
                {
                    try { RegisterCoreExtras(); } catch { }
                }

                LoggerInstance.Msg("[NotesHUD] Loaded. Press " + ToggleKey + " for the notebook (per-save notes).");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error("[NotesHUD] OnInitializeMelon failed: " + ex.GetBaseException().Message);
            }
        }

        // Mod contract + key HUD + F1 hub. ONLY with gregCore (JIT split).
        private void RegisterCoreExtras()
        {
            try
            {
                NotesCoreBridge.RegisterMenu();
                NotesCoreBridge.RegisterExtras(ToggleKey.ToString());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[NotesHUD] Hub registration failed: " + ex.GetBaseException().Message);
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                ModSaveScope.NotifySceneLoaded();
                NotesStore.OnScopeInvalidated();
            }
            catch { }
        }

        public override void OnUpdate()
        {
            try
            {
                ModSaveScope.TickCapture();
                if (ModSaveScope.HasScope) ModSaveScope.EnsureBindingChecked();

                var kb = Keyboard.current;

                // Toggle (function key — safe while typing; letter keys would collide).
                try
                {
                    if (kb != null && kb[ToggleKey].wasPressedThisFrame && !IsPauseMenuActive())
                        NotesOverlay.Toggle();
                }
                catch { }

                if (NotesOverlay.IsVisible)
                {
                    try { NotesOverlay.RouteClicks(); } catch { }
                    try { NotesOverlay.TickEditing(); } catch { }
                    try { NotesPanel.Tick(); } catch { }
                    try { Input.NotesInputLock.Tick(true); } catch { }
                    try
                    {
                        if (Time.unscaledTime >= _statusRefreshAt)
                        {
                            _statusRefreshAt = Time.unscaledTime + 0.5f;
                            NotesOverlay.RefreshStatus();
                        }
                    }
                    catch { }
                }
                else
                {
                    // Escape closed the notebook this frame: keep the central
                    // input lock held briefly so the game does not see the same
                    // Escape press and open the pause menu (IPAM pattern:
                    // report the cooldown state every frame while hidden).
                    // Standalone: no central lock — pause menu may open (known quirk, see README).
                    try
                    {
                        if (GregHost.HasCore)
                            NotesCoreBridge.SetOpen(NotesOverlay.IsInEscapeCooldown());
                    }
                    catch { }
                }

                try { NotesStore.TickAutosave(); } catch { }
            }
            catch { /* best-effort per frame */ }
        }

        public override void OnApplicationQuit()
        {
            try { NotesStore.SaveNow(); } catch { }
            try { Input.NotesInputLock.Release(); } catch { }
        }

        internal static bool IsPauseMenuActive()
        {
            try
            {
                var all = Resources.FindObjectsOfTypeAll<Canvas>();
                if (all == null) return false;
                foreach (var c in all)
                {
                    if (c == null || !c.isActiveAndEnabled) continue;
                    var go = c.gameObject;
                    if (go == null) continue;
                    try { if (!go.scene.IsValid() || !go.scene.isLoaded) continue; } catch { continue; }
                    if (c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    string n = go.name ?? "";
                    if (n.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("EscapeMenu", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("InGameMenu", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("SystemMenu", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("OptionsMenu", StringComparison.OrdinalIgnoreCase) >= 0
                        || n.IndexOf("SettingsMenu", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { }
            return false;
        }
    }
}
