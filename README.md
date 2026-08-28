# YKeys

A hotkey daemon for Windows. C#, .NET 10, NativeAOT. One job: turn global
hotkeys into commands.

Built as the companion to [YTile](https://github.com/AegiosOT/YTile) — YTile
releases bundle `ykeys.exe` and `ytile start` brings it up automatically — but
it is a standalone tool: any chord can run any program.

## How it works

`~/.config/ykeys/ykeys.json` maps chords to command lines:

```json
{
  "hotkeys": {
    "alt+1": "ytile workspace 1",
    "alt+shift+h": "ytile move left",
    "alt+enter": "wt"
  }
}
```

Chords are `modifier+...+key`: modifiers `alt`, `ctrl`, `shift`, `win`; keys
are letters, digits, `f1`-`f24` (`f12` is debugger-reserved), arrows, and
named keys like `space`, `enter`, `minus`, `plus` (punctuation names follow
US key positions). Commands run as detached processes with no shell in
between — quote the program path if it contains spaces. Config changes are
picked up automatically; no restart needed.

Hotkeys are registered with `RegisterHotKey`, so they keep working even while
an elevated window has focus, and a chord some other program already owns is
skipped with a log line naming it rather than silently swallowed.

## Install

Bundled with YTile — installing YTile gives you ykeys. Standalone: grab
`ykeys.exe` from the [latest release](https://github.com/AegiosOT/YKeys/releases)
and put it on your PATH.

## Running

```
ykeys            # run in the foreground, Ctrl+C to exit
ykeys --log      # log to %LOCALAPPDATA%\ykeys\ykeys.log instead (what `ytile start` uses)
ykeys --version
```

One instance per session; a second start exits with a message. To stop a
background instance: `ytile stop` (when managed by YTile) or kill `ykeys.exe`.

## Building

Requires the .NET 10 SDK, plus VS Build Tools with the C++ workload for the
NativeAOT linker.

```
dotnet build                                        # dev build (JIT)
dotnet publish src/YKeys -r win-x64 -c Release -o publish   # NativeAOT ykeys.exe
```

## Code signing

Free code signing provided by [SignPath.io](https://about.signpath.io/),
certificate by [SignPath Foundation](https://signpath.org/), once the project
is onboarded alongside YTile.

- Committers and reviewers: [AegiosOT](https://github.com/AegiosOT). Pull
  requests from outside contributors are reviewed by a committer before merging.
- Approvers: [AegiosOT](https://github.com/AegiosOT) — each release's signing
  request is approved manually.

This program will not transfer any information to other networked systems
unless specifically requested by the user or the person installing or
operating it. (It only runs the commands you bind, locally.)

## License

GPL-3.0 — see [LICENSE](LICENSE).
