# Purrch for Windows (Electron)

A desktop pet for Windows — the same little cat/dog from the macOS build, riding
the same pixel-art sprite sheets. Built on Electron so it can run and be tested
on macOS and packaged into a Windows `.exe`.

## Status

First cut. The pet lives on a transparent, always-on-top, click-through overlay:
it wanders the bottom of the screen, pauses to sit, groom, stretch, yawn, or nap,
and perks up when you click it. A tray icon quits it. More of the macOS
behaviour (tasks, toys, sleep model, moods, the photo likeness) ports next.

## Run it (macOS or Windows)

```bash
npm install
npm start          # the real overlay pet
npm test           # a bordered window on a backdrop, for a quick look
```

Quit from the tray, or press <kbd>Ctrl/Cmd</kbd>+<kbd>Alt</kbd>+<kbd>Q</kbd>.

## Build the Windows installer

Windows binaries are produced by CI (`.github/workflows/build-windows.yml`) on a
Windows runner — run the **build-windows** workflow (or push a `v*` tag) and grab
the `purrch-windows` artifact. Locally on Windows: `npm run dist`.

## Assets

`assets/sprites/` is copied from the macOS app
(`Sources/PetApp/Resources/Sprites`). Regenerate art there with
`tools/spritegen.py`, then re-copy — never hand-edit the PNGs.
