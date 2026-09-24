# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Fixed

- Replaced removed gregCore `GregMenuBinding` API with `GregMenuRegistry` opener/closer (+ open-state reporting) — builds against current gregCore again.


### Fixed

- Escape-close keeps the GregCore input lock held for 0.35 s so the same keypress no longer opens the pause menu (IPAM pattern; standalone quirk documented in README).
- Scope switch (menu → loaded save) persists in-memory edits to the old scope first — typing before capture is never lost.

### Added

- Post-it notebook HUD (`F10`): editable title + multiline body for IPs, shop item #s, and general notes.
- IL2CPP-safe keyboard pump (no `TextField`/`GUI.TextField` — stripped `TextEditor` crash); Tab switches title/body, Enter = newline, Ctrl+Backspace clears field, Esc closes.
- Per-save persistence (`notes_<scope>.json`) + best-effort live-save mirror via `GregModSave` int-array encoding (gregCore only).
- Full GregCore wiring behind soft probe: F1 menu + key HUD + toggle binding + input locks; standalone fallback without gregCore.dll.
- Docs: `docs/NOTES_HUD_SPEC.md`, `docs/GREGCORE_GAPS.md` (missing GregCore SDK pieces proposed upstream).
