const { contextBridge, ipcRenderer } = require('electron');
const fs = require('fs');
const path = require('path');
const url = require('url');

// The sprite sheets + manifest are reused verbatim from the macOS build.
const spritesDir = path.join(__dirname, '..', 'assets', 'sprites');
let manifest = {};
try {
  manifest = JSON.parse(fs.readFileSync(path.join(spritesDir, 'sprites.json'), 'utf8'));
} catch (e) {
  console.error('Purrch: failed to load sprites.json', e);
}

contextBridge.exposeInMainWorld('purrch', {
  manifest,
  spriteUrl: (name) => url.pathToFileURL(path.join(spritesDir, name)).href,
  setIgnore: (ignore) => ipcRenderer.send('set-ignore', ignore),
});
