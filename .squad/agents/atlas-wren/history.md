# Atlas Wren — History & Learnings

## Core Context

**Project:** SquadUI — WPF dashboard for Squad CLI AI agent management  
**Stack:** C# / WPF / .NET 10, NUnit 4.4+, TypeScript SDK  
**Key paths:**
- `SquadDash/` — main application (WPF, Windows-only today)
- `SquadDash.Tests/` — NUnit test suite
- `.squad/decisions.md` — architectural decision log

**Mac-specific context:**
- SquadDash currently targets `net10.0-windows` with `<UseWPF>true</UseWPF>` — WPF is a Windows-only framework
- Cross-platform porting requires either a UI framework swap (Avalonia, MAUI) or a thin platform abstraction layer
- The TypeScript SDK (`Squad.SDK/`) and Node.js subprocess are already cross-platform — the WPF shell is the only hard Windows dependency
- macOS targets will need `.app` bundle packaging; code signing and notarization are required for Gatekeeper

---

## Learnings

📌 Hired (2026-05-15): Atlas Wren joins SquadDash as Mac & Cross-Platform Specialist.
