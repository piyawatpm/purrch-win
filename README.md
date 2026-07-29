# Purrch for Windows

A **native** Windows desktop pet — the same little cat and fluffy Pom from the
macOS build, riding the same pixel-art sprite sheets. Written in C# / WinForms
(Win32 under the hood), no web runtime, single self-contained `.exe`.

## What it does

- A **per-pixel-alpha layered window** (`UpdateLayeredWindow`) draws the pet with
  true transparency and always sits on top. Fully-transparent pixels let clicks
  fall through to the desktop, so only the pet is interactive.
- **Wanders** the bottom of the screen and pauses to sit, groom, stretch, yawn,
  loaf, scratch, sniff, wiggle, or curl up and **nap**.
- **Pick it up** and drop it — it falls back to the floor with gravity. A quick
  click **pokes** it (a happy wiggle) and it **meows / barks**.
- **Tray menu:** cat ⇄ dog, size, sound, launch-at-login, check for updates, quit.

## Get it

Download `Purrch.exe` from the [latest release](https://github.com/piyawatpm/purrch-win/releases/latest)
and run it. It's self-contained — no .NET install needed. Quit from the tray icon.

### Updates

The app checks GitHub Releases at startup and shows a tray notification when a
newer version exists; **Check for updates** in the tray menu opens the download.
(It never silently downloads or replaces an executable — that keeps it clear of
antivirus heuristics. One-click auto-update will come once the build is signed.)

### "Windows protected your PC" / antivirus flags it

The exe is currently **unsigned**, so Windows SmartScreen and some antivirus may
warn on first run. It's safe — the source and build are public. To run it:

1. On the SmartScreen dialog, click **More info → Run anyway**.
2. Verify the download if you like: the release includes `Purrch.exe.sha256.txt`;
   compare with `Get-FileHash Purrch.exe -Algorithm SHA256` in PowerShell.

We already minimise false positives (no compressed self-extractor, ReadyToRun,
real version/company metadata, a proper app manifest, no admin request). The only
thing that *fully* clears SmartScreen is **code signing**, and the CI is already
wired for it (SignPath — free for open source). See **[SIGNING.md](SIGNING.md)**
for the ~15-minute setup; once your token is added, every build signs itself.

## Build

CI (`.github/workflows/build-windows.yml`) builds on a Windows runner and, on a
`v*` tag, publishes a GitHub Release with the exe + checksum. Locally on Windows
with the .NET 8 SDK:

```powershell
dotnet publish Purrch/Purrch.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishReadyToRun=true -o publish
.\publish\Purrch.exe
```

## Parity with the macOS app

This is a growing port. **Done:** wander/rest/sleep, drag-drop with gravity,
click reactions, meow/bark, cat/dog, size, launch-at-login, in-app update checks,
**the to-do list + feeding**, **speech bubbles + spontaneous chatter**, and
**jump**. **Not yet ported from macOS:** toys (mouse/ball/feather + catch), the
right-click control panel, collar/eye-colour customisation, the photo-likeness
feature, and follow-cursor/window modes. The update mechanism above is what lets
these ship incrementally.

## Assets

`Purrch/Assets/` (sprites + sounds) is copied from the macOS app
(`Sources/PetApp/Resources`). Regenerate art there with `tools/spritegen.py`,
then re-copy — never hand-edit the PNGs.
