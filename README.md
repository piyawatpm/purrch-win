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

**Essentially at parity with the macOS app.** Done: wander/rest/sleep, drag-drop
with gravity, click reactions, meow/bark, cat/dog, size, launch-at-login, in-app
updates, the to-do list + feeding, speech bubbles + spontaneous chatter, jump,
toys (mouse/ball/feather — chase & pounce), a right-click control panel,
follow-cursor mode, collar styles, eye/ear/collar/bell colour customisation, and
the **photo-likeness** feature — upload your pet and an image model (Gemini,
bring-your-own key) redraws it as the companion, with your pet's colours pulled
across the whole rig. Minor macOS flourishes not yet ported: perching on window
title bars, time-of-day sleepiness, and the animation tester.

## Assets

`Purrch/Assets/` (sprites + sounds) is copied from the macOS app
(`Sources/PetApp/Resources`). Regenerate art there with `tools/spritegen.py`,
then re-copy — never hand-edit the PNGs.
