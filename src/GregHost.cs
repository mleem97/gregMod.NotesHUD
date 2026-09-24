using System;

namespace GregMod.NotesHUD;

// Soft-Dependency-Probe (reiner Typname-Lookup): gregCore-beruehrende Methoden
// duerfen NUR laufen, wenn HasCore true ist (sonst JIT-TypeLoad ohne DLL).
internal static class GregHost
{
    private const string ProbeType = "gregCore.UI.GregNotificationManager, gregCore";
    private static bool? _hasCore;

    public static bool HasCore
    {
        get
        {
            if (_hasCore == null)
            {
                try { _hasCore = Type.GetType(ProbeType) != null; }
                catch { _hasCore = false; }
            }
            return _hasCore.Value;
        }
    }
}
