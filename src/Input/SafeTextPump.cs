// SafeTextPump — IL2CPP-safe keyboard input for the notebook.
// WHY: the game strips TextEditor APIs used by GUI.TextField and UIToolkit
// TextField (hard crash; see TrainerOverlay + IPAM IpamFormInput). So the
// notebook renders text in Labels and pumps keys manually via InputSystem.
//
// Supports: A-Z (shift for caps), 0-9 (shift row symbols), numpad digits,
// space, enter/newline, tab (2 spaces), backspace (hold-to-repeat),
// delete-all via Ctrl+Backspace, focus switch via Tab between title/body is
// handled by NotesOverlay, NOT here. While editing, letter/number keys are
// consumed by the overlay (game hotkeys must ignore them — caller checks
// NotesOverlay.IsEditing before toggling anything on letter keys; our toggle
// is F10 so there is no conflict by default).
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace GregMod.NotesHUD.Input
{
    internal static class SafeTextPump
    {
        private const float BackspaceInitialDelay = 0.45f;
        private const float BackspaceSlowInterval = 0.07f;
        private const float BackspaceFastInterval = 0.03f;

        private static float _backspaceHeldSince = -1f;
        private static float _lastBackspaceRepeat;

        internal static void ResetBackspaceLatch()
        {
            _backspaceHeldSince = -1f;
        }

        /// <summary>
        /// Pumps one frame of keyboard input into the buffer. Returns true if
        /// the text changed. Caller sanitizes + clamps length afterwards.
        /// </summary>
        internal static bool Pump(ref string buffer, int maxLen, bool allowNewline)
        {
            bool changed = false;
            Keyboard kb;
            try { kb = Keyboard.current; } catch { return false; }
            if (kb == null) return false;

            try
            {
                // Backspace incl. hold-to-repeat.
                try
                {
                    if (kb.backspaceKey.wasReleasedThisFrame) _backspaceHeldSince = -1f;
                    else if (kb.backspaceKey.wasPressedThisFrame)
                    {
                        if (BackspaceOnce(ref buffer)) changed = true;
                        _backspaceHeldSince = Time.realtimeSinceStartup;
                        _lastBackspaceRepeat = Time.realtimeSinceStartup;
                    }
                    else if (_backspaceHeldSince >= 0f && kb.backspaceKey.isPressed)
                    {
                        float held = Time.realtimeSinceStartup - _backspaceHeldSince;
                        if (held >= BackspaceInitialDelay && !string.IsNullOrEmpty(buffer))
                        {
                            float interval = held >= 1.2f ? BackspaceFastInterval : BackspaceSlowInterval;
                            if (Time.realtimeSinceStartup - _lastBackspaceRepeat >= interval)
                            {
                                _lastBackspaceRepeat = Time.realtimeSinceStartup;
                                if (BackspaceOnce(ref buffer)) changed = true;
                            }
                        }
                        else if (string.IsNullOrEmpty(buffer)) _backspaceHeldSince = -1f;
                    }
                }
                catch { }

                // Ctrl+Backspace = clear whole field.
                try
                {
                    if ((kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && kb.backspaceKey.wasPressedThisFrame)
                    {
                        if (!string.IsNullOrEmpty(buffer)) { buffer = ""; changed = true; }
                        return changed;
                    }
                }
                catch { }

                if (buffer != null && buffer.Length >= maxLen) return changed;

                bool shift = false;
                try { shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed; } catch { }

                // Enter / numpad-enter.
                if (allowNewline)
                {
                    try
                    {
                        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                        {
                            buffer = (buffer ?? "") + "\n";
                            return true;
                        }
                    }
                    catch { }
                }

                // Tab -> two spaces (focus-switch handled by overlay via explicit Tab logic).
                try
                {
                    if (kb.tabKey.wasPressedThisFrame && allowNewline)
                    {
                        buffer = (buffer ?? "") + "  ";
                        changed = true;
                    }
                }
                catch { }

                // Space.
                try { if (kb.spaceKey.wasPressedThisFrame) { buffer = (buffer ?? "") + " "; changed = true; } } catch { }

                // Letters A-Z.
                PumpLetter(kb.aKey, shift ? 'A' : 'a', ref buffer, ref changed, maxLen);
                PumpLetter(kb.bKey, shift ? 'B' : 'b', ref buffer, ref changed, maxLen);
                PumpLetter(kb.cKey, shift ? 'C' : 'c', ref buffer, ref changed, maxLen);
                PumpLetter(kb.dKey, shift ? 'D' : 'd', ref buffer, ref changed, maxLen);
                PumpLetter(kb.eKey, shift ? 'E' : 'e', ref buffer, ref changed, maxLen);
                PumpLetter(kb.fKey, shift ? 'F' : 'f', ref buffer, ref changed, maxLen);
                PumpLetter(kb.gKey, shift ? 'G' : 'g', ref buffer, ref changed, maxLen);
                PumpLetter(kb.hKey, shift ? 'H' : 'h', ref buffer, ref changed, maxLen);
                PumpLetter(kb.iKey, shift ? 'I' : 'i', ref buffer, ref changed, maxLen);
                PumpLetter(kb.jKey, shift ? 'J' : 'j', ref buffer, ref changed, maxLen);
                PumpLetter(kb.kKey, shift ? 'K' : 'k', ref buffer, ref changed, maxLen);
                PumpLetter(kb.lKey, shift ? 'L' : 'l', ref buffer, ref changed, maxLen);
                PumpLetter(kb.mKey, shift ? 'M' : 'm', ref buffer, ref changed, maxLen);
                PumpLetter(kb.nKey, shift ? 'N' : 'n', ref buffer, ref changed, maxLen);
                PumpLetter(kb.oKey, shift ? 'O' : 'o', ref buffer, ref changed, maxLen);
                PumpLetter(kb.pKey, shift ? 'P' : 'p', ref buffer, ref changed, maxLen);
                PumpLetter(kb.qKey, shift ? 'Q' : 'q', ref buffer, ref changed, maxLen);
                PumpLetter(kb.rKey, shift ? 'R' : 'r', ref buffer, ref changed, maxLen);
                PumpLetter(kb.sKey, shift ? 'S' : 's', ref buffer, ref changed, maxLen);
                PumpLetter(kb.tKey, shift ? 'T' : 't', ref buffer, ref changed, maxLen);
                PumpLetter(kb.uKey, shift ? 'U' : 'u', ref buffer, ref changed, maxLen);
                PumpLetter(kb.vKey, shift ? 'V' : 'v', ref buffer, ref changed, maxLen);
                PumpLetter(kb.wKey, shift ? 'W' : 'w', ref buffer, ref changed, maxLen);
                PumpLetter(kb.xKey, shift ? 'X' : 'x', ref buffer, ref changed, maxLen);
                PumpLetter(kb.yKey, shift ? 'Y' : 'y', ref buffer, ref changed, maxLen);
                PumpLetter(kb.zKey, shift ? 'Z' : 'z', ref buffer, ref changed, maxLen);

                // Digits (top row with shift symbols, US layout) + numpad.
                PumpDigit(kb.digit1Key, shift ? '!' : '1', ref buffer, ref changed, maxLen);
                PumpDigit(kb.digit2Key, shift ? '@' : '2', ref buffer, ref changed, maxLen);
                PumpDigit(kb.digit3Key, shift ? '#' : '3', ref buffer, ref changed, maxLen);
                PumpDigit(kb.digit4Key, shift ? '$' : '4', ref buffer, ref changed, maxLen);
                PumpDigit(kb.digit5Key, shift ? '%' : '5', ref buffer, ref changed, maxLen);
                PumpDigit(kb.digit6Key, shift ? '^' : '6', ref buffer, ref changed, maxLen);
                PumpDigit(kb.digit7Key, shift ? '&' : '7', ref buffer, ref changed, maxLen);
                PumpDigit(kb.digit8Key, shift ? '*' : '8', ref buffer, ref changed, maxLen);
                PumpDigit(kb.digit9Key, shift ? '(' : '9', ref buffer, ref changed, maxLen);
                PumpDigit(kb.digit0Key, shift ? ')' : '0', ref buffer, ref changed, maxLen);
                PumpNumpad(kb.numpad1Key, '1', ref buffer, ref changed, maxLen);
                PumpNumpad(kb.numpad2Key, '2', ref buffer, ref changed, maxLen);
                PumpNumpad(kb.numpad3Key, '3', ref buffer, ref changed, maxLen);
                PumpNumpad(kb.numpad4Key, '4', ref buffer, ref changed, maxLen);
                PumpNumpad(kb.numpad5Key, '5', ref buffer, ref changed, maxLen);
                PumpNumpad(kb.numpad6Key, '6', ref buffer, ref changed, maxLen);
                PumpNumpad(kb.numpad7Key, '7', ref buffer, ref changed, maxLen);
                PumpNumpad(kb.numpad8Key, '8', ref buffer, ref changed, maxLen);
                PumpNumpad(kb.numpad9Key, '9', ref buffer, ref changed, maxLen);
                PumpNumpad(kb.numpad0Key, '0', ref buffer, ref changed, maxLen);

                // Punctuation row (US layout, shift variants).
                PumpKey(kb.minusKey, shift ? '_' : '-', ref buffer, ref changed, maxLen);
                PumpKey(kb.equalsKey, shift ? '+' : '=', ref buffer, ref changed, maxLen);
                PumpKey(kb.leftBracketKey, shift ? '{' : '[', ref buffer, ref changed, maxLen);
                PumpKey(kb.rightBracketKey, shift ? '}' : ']', ref buffer, ref changed, maxLen);
                PumpKey(kb.semicolonKey, shift ? ':' : ';', ref buffer, ref changed, maxLen);
                PumpKey(kb.quoteKey, shift ? '"' : '\'', ref buffer, ref changed, maxLen);
                PumpKey(kb.commaKey, shift ? '<' : ',', ref buffer, ref changed, maxLen);
                PumpKey(kb.periodKey, shift ? '>' : '.', ref buffer, ref changed, maxLen);
                PumpKey(kb.slashKey, shift ? '?' : '/', ref buffer, ref changed, maxLen);
                PumpKey(kb.backslashKey, shift ? '|' : '\\', ref buffer, ref changed, maxLen);
                PumpKey(kb.backquoteKey, shift ? '~' : '`', ref buffer, ref changed, maxLen);
            }
            catch { /* best-effort */ }
            return changed;
        }

        private static bool BackspaceOnce(ref string buffer)
        {
            try
            {
                if (string.IsNullOrEmpty(buffer)) return false;
                buffer = buffer.Substring(0, buffer.Length - 1);
                return true;
            }
            catch { return false; }
        }

        private static void PumpLetter(KeyControl key, char c, ref string buffer, ref bool changed, int maxLen)
        {
            try
            {
                if (key != null && key.wasPressedThisFrame)
                    AppendChar(c, ref buffer, ref changed, maxLen);
            }
            catch { }
        }

        private static void PumpDigit(KeyControl key, char c, ref string buffer, ref bool changed, int maxLen)
        {
            try
            {
                if (key != null && key.wasPressedThisFrame)
                    AppendChar(c, ref buffer, ref changed, maxLen);
            }
            catch { }
        }

        private static void PumpNumpad(KeyControl key, char c, ref string buffer, ref bool changed, int maxLen)
        {
            try
            {
                if (key != null && key.wasPressedThisFrame)
                    AppendChar(c, ref buffer, ref changed, maxLen);
            }
            catch { }
        }

        private static void PumpKey(KeyControl key, char c, ref string buffer, ref bool changed, int maxLen)
        {
            try
            {
                if (key != null && key.wasPressedThisFrame)
                    AppendChar(c, ref buffer, ref changed, maxLen);
            }
            catch { }
        }

        private static void AppendChar(char c, ref string buffer, ref bool changed, int maxLen)
        {
            try
            {
                if (buffer == null) buffer = "";
                if (buffer.Length >= maxLen) return;
                buffer += c;
                changed = true;
            }
            catch { }
        }
    }
}
