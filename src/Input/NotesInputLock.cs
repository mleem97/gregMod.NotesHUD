// NotesInputLock — cursor + game-input gating while the notebook is open.
// With gregCore: the F1 menu registry owns ref-counted locks (camera /
// movement / interact) — we only report open/closed. Standalone: best-effort
// cursor unlock while visible (same as TrainerPanel.RefreshCursor).
using System;
using MelonLoader;
using UnityEngine;
using GregMod.NotesHUD.Core;

namespace GregMod.NotesHUD.Input
{
    internal static class NotesInputLock
    {
        internal static void SetOpen(bool open)
        {
            try
            {
                if (GregHost.HasCore)
                {
                    try { NotesCoreBridge.SetOpen(open); } catch { }
                }
                else
                {
                    RefreshCursor(open);
                }
            }
            catch (Exception ex)
            {
                try { MelonLogger.Warning("[NotesHUD] InputLock failed: " + ex.GetBaseException().Message); } catch { }
            }
        }

        internal static void Tick(bool visible)
        {
            if (GregHost.HasCore) return;
            try
            {
                if (!visible) return;
                if (UnityEngine.Cursor.lockState != CursorLockMode.None || !UnityEngine.Cursor.visible)
                    RefreshCursor(true);
            }
            catch { }
        }

        internal static void Release()
        {
            try
            {
                if (GregHost.HasCore)
                {
                    try { NotesCoreBridge.SetOpen(false); } catch { }
                }
                else
                {
                    RefreshCursor(false);
                }
            }
            catch { }
        }

        private static void RefreshCursor(bool visible)
        {
            try
            {
                if (visible)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                    UnityEngine.Cursor.visible = true;
                }
                else
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.Locked;
                    UnityEngine.Cursor.visible = false;
                }
            }
            catch { }
        }
    }
}
