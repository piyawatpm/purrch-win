const { app, BrowserWindow, screen, Tray, Menu, ipcMain, nativeImage, globalShortcut } = require('electron');
const path = require('path');

// --test opens an ordinary bordered window (on a backdrop) so the pet can be
// seen and screenshotted in isolation. Normal mode is a full-screen transparent,
// click-through overlay that the pet walks across.
const TEST = process.argv.includes('--test');

let win = null;
let tray = null;

function createOverlay() {
  const { bounds } = screen.getPrimaryDisplay();
  win = new BrowserWindow({
    x: bounds.x, y: bounds.y, width: bounds.width, height: bounds.height,
    frame: false, transparent: true, resizable: false, movable: false,
    minimizable: false, maximizable: false, fullscreenable: false,
    skipTaskbar: true, focusable: false, hasShadow: false,
    enableLargerThanScreen: true, backgroundColor: '#00000000',
    webPreferences: { preload: path.join(__dirname, 'preload.js'), sandbox: false },
  });
  win.setAlwaysOnTop(true, 'screen-saver');
  win.setVisibleOnAllWorkspaces(true, { visibleOnFullScreen: true });
  win.setIgnoreMouseEvents(true, { forward: true });
  win.loadFile(path.join(__dirname, 'renderer', 'index.html'));
}

function createTestWindow() {
  win = new BrowserWindow({
    x: 80, y: 80, width: 480, height: 320,
    frame: true, transparent: false, resizable: false, alwaysOnTop: true,
    title: 'Purrch (test)',
    webPreferences: { preload: path.join(__dirname, 'preload.js'), sandbox: false },
  });
  win.loadFile(path.join(__dirname, 'renderer', 'index.html'), { search: 'test=1' });
}

function createTray() {
  let icon = nativeImage.createFromPath(path.join(__dirname, '..', 'assets', 'tray.png'));
  if (!icon.isEmpty()) icon = icon.resize({ width: 18, height: 18 });
  tray = new Tray(icon.isEmpty() ? nativeImage.createEmpty() : icon);
  tray.setToolTip('Purrch');
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: 'Purrch', enabled: false },
    { type: 'separator' },
    { label: 'Quit', click: () => app.quit() },
  ]));
}

// The renderer decides, per pointer move, whether the cursor is over the pet's
// pixels; the window is click-through everywhere else.
ipcMain.on('set-ignore', (_e, ignore) => {
  if (win && !TEST) win.setIgnoreMouseEvents(!!ignore, { forward: true });
});

app.whenReady().then(() => {
  if (process.platform === 'darwin' && app.dock) app.dock.hide();
  if (TEST) createTestWindow();
  else { createOverlay(); createTray(); }
  if (win) win.webContents.on('console-message', (_e, _level, message) => console.log('[renderer]', message));
  globalShortcut.register('CommandOrControl+Alt+Q', () => app.quit());
});

app.on('window-all-closed', () => app.quit());
app.on('will-quit', () => globalShortcut.unregisterAll());
