# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

YKeys is a Windows hotkey daemon (C#, .NET 10, NativeAOT): it reads `~/.config/ykeys/ykeys.json`, registers its chord→command map as global hotkeys via `RegisterHotKey`, and runs each command as a detached process when pressed. Companion to YTile (`ytile start` launches `ykeys --log`), but standalone. MIT.

## Commands

```
dotnet build -c Release                                      # dev build (JIT)
dotnet test tests/YKeys.Tests -c Release                     # run tests (MSTest)
dotnet test tests/YKeys.Tests -c Release --filter "FullyQualifiedName~HotkeyParserTests.SomeTest"   # single test
dotnet publish src/YKeys -r win-x64 -c Release -o publish    # shipping NativeAOT ykeys.exe
```

NativeAOT publish needs VS Build Tools with the C++ workload; plain `dotnet build` does not.

## Architecture

Five source files in `src/YKeys`, one pipeline:

- **Program.cs** — entry point, `--version`/`--help`/`--log` flags, single-instance lock (named semaphore `Local\ykeys-instance` — the check must precede the `--log` redirect, see comment), and a debounced `FileSystemWatcher` that reloads the config on change.
- **Config.cs** (`YKeysConfig`) — loads/validates the JSON config. `Load` never throws; parse and per-binding problems are collected into the `out string? error` and bad bindings are skipped. Uses a source-generated `JsonSerializerContext` (AOT requirement — no reflection-based serialization anywhere).
- **HotkeyParser.cs** — `"alt+shift+1"` → `HOT_KEY_MODIFIERS` + virtual key. Enforces the rules: last token is the key, f12 rejected (debugger-reserved), bare keys need a modifier except f13–f24.
- **HotkeyListener.cs** — the concurrency-sensitive core. `RegisterHotKey` is thread-affine, so all registration happens on one dedicated hidden-window message-pump thread ("ykeys-pump"). Config reloads from the watcher thread hand the new binding list over via `Interlocked.Exchange` into `s_pending` plus a posted thread message (`WM_APP+1`); the pump thread unregisters everything and re-registers. Hotkey ids are never reused across applies (stale `WM_HOTKEY` messages must miss the lookup). `s_registered` is touched only on the pump thread — that invariant is what makes it lock-free.
- **CommandRunner.cs** — quote-aware first-token split, then `Process.Start` with no shell, fired via `Task.Run` so the message pump never blocks.
- **`ykeys signal <class>[#code]`** — one-shot verb in Program.cs that sends a signal and exits, sharing `SignalSender.TrySend` with the hotkey path so the diagnostic cannot drift from the thing it diagnoses. It is also the only way to exercise delivery end to end without synthesising a keypress.
- **SignalSender.cs** — the no-spawn path for `@signal:<window class>[#<code>]` bindings. Finds the target (message-only windows first: `FindWindow` cannot see those), calls `AllowSetForegroundWindow` on its process, then posts `RegisterWindowMessage("YKeysSignal")` with the code in `wParam`. Runs **inline on the pump thread**, unlike a spawn: both calls are non-blocking, and the foreground grant has to happen while the `WM_HOTKEY` that earned it is still the last input event. Targets are parsed and validated in `Config`, not on first press — a typo would otherwise register fine and silently do nothing. `MessageName` and the config syntax are a public contract with every app that accepts signals; changing either breaks them in the field with no error on either side.

Win32 interop is via **CsWin32** source generation (`PInvoke.*`, generated from usage) — don't write manual `DllImport`s.

## Constraints that shape changes

- **AOT is the shipping configuration** (`PublishAot=true`, `OptimizationPreference=Speed` — hotkey latency is the product). AOT/trim analyzers run on dev builds; keep code reflection-free and marshalling-free (`DisableRuntimeMarshalling` is applied assembly-wide from `build/AssemblyAttributes.cs`).
- Central package management: versions live in `Directory.Packages.props`; shared build settings in `Directory.Build.props`.
- The version lives in three places that must agree at release time: the `Version` const in `Program.cs`, the git tag `vX.Y.Z`, and `-p:Version` stamped by release.yml — the release smoke test fails if they diverge. Bump the const before tagging.
- Release binaries are signed via Azure Artifact Signing (OIDC, `release` environment — see `packaging/signing/README.md`); keep `Product`/`Company` in Directory.Build.props intact, they are the signed binaries' published metadata.
- Tests see internals via `InternalsVisibleTo`; everything in the app is `internal`.
- User-facing failure handling is "log and keep running": config errors, registration conflicts (chord owned by another program), and spawn failures each produce a log line and are skipped, never fatal.
