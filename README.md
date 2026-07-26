# Purrch for Windows

A **native** Windows desktop pet — the same little cat and fluffy Pom from the
macOS build, riding the same pixel-art sprite sheets. Written in C# / WinForms
(Win32 under the hood), no web runtime.

## How it works

- A **per-pixel-alpha layered window** (`UpdateLayeredWindow`) draws the pet with
  true transparency and always sits on top. Fully-transparent pixels let clicks
  fall straight through to the desktop, so only the pet is interactive — no
  hit-test hacks.
- The pet **wanders** the bottom of the screen and pauses to sit, groom, stretch,
  yawn, or curl up and **nap** after a long idle.
- **Pick it up** with the mouse and drop it — it falls back to the floor with
  gravity. A quick click just **pokes** it (a happy wiggle).
- A **tray icon** switches cat/dog, sets the size, toggles launch-at-login, and
  quits.

Sprite sheets are embedded, so the build is a single self-contained `Purrch.exe`
that needs no .NET install.

## Build

Native Windows binaries are built by CI (`.github/workflows/build-windows.yml`)
on a Windows runner — the **build-windows** workflow publishes a self-contained
single-file exe and uploads it as the `purrch-windows` artifact. Push a `v*` tag
to build a release.

Locally on Windows with the .NET 8 SDK:

```powershell
dotnet publish Purrch/Purrch.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
.\publish\Purrch.exe
```

Quit from the tray icon.

## Assets

`Purrch/Assets/Sprites/` is copied from the macOS app
(`Sources/PetApp/Resources/Sprites`). Regenerate art there with
`tools/spritegen.py`, then re-copy — never hand-edit the PNGs.

## Roadmap

First cut covers wandering, resting/sleeping, drag-and-drop, click reactions,
cat/dog, size, and launch-at-login. Next: the to-do list + feeding, toys, the
richer sleep/mood behaviours, and the photo-likeness feature from the macOS app.
