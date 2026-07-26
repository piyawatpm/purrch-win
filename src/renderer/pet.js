// Purrch — Electron renderer. Reuses the macOS sprite sheets and runs a small
// behaviour loop: the pet wanders the bottom of the screen, pauses to sit,
// groom, or nap, and reacts when clicked. The window is click-through except
// where the cursor is actually over the pet's pixels.

const P = window.purrch;
const M = P.manifest;
const canvas = document.getElementById('stage');
const ctx = canvas.getContext('2d');

const TEST = new URLSearchParams(location.search).get('test') === '1';
if (TEST) document.body.classList.add('test');

const SCALE = 3;
const FW = M.frameWidth || 40;
const FH = M.frameHeight || 32;

// Which sheets to load for this first cut. Motion + a few resting poses.
const ANIMS = ['idle', 'walk', 'sit', 'groom', 'sleep', 'happy', 'stretch', 'yawn'];
const sheets = {};
for (const name of ANIMS) {
  const meta = M.animations && M.animations[name];
  if (!meta) continue;
  const img = new Image();
  img.onerror = () => console.error('Purrch: sheet failed', name);
  img.src = P.spriteUrl(`cat__bell__${name}.png`);
  sheets[name] = { img, frames: meta.frames, ms: meta.msPerFrame };
}

function resize() {
  canvas.width = window.innerWidth;
  canvas.height = window.innerHeight;
}
window.addEventListener('resize', resize);
resize();

// --- state ---
const pet = {
  x: window.innerWidth * 0.4,
  dir: 1,                 // 1 = facing right, -1 = left
  state: 'idle',
  frame: 0,
  frameT: 0,
  stateT: 0,
  hold: 2.0,              // seconds before the next decision
  speed: 44,             // px per second while walking
};

function setState(s) {
  if (!sheets[s]) s = 'idle';
  if (pet.state !== s) { pet.state = s; pet.frame = 0; pet.frameT = 0; }
}

// Pick the next thing to do. Walking alternates with a resting pose.
function decide() {
  if (pet.state === 'walk') {
    const rest = ['idle', 'sit', 'groom', 'stretch', 'yawn'];
    setState(rest[(Math.random() * rest.length) | 0]);
    pet.hold = 2 + Math.random() * 4;
    if (Math.random() < 0.12) { setState('sleep'); pet.hold = 6 + Math.random() * 8; }
  } else {
    setState('walk');
    pet.dir = Math.random() < 0.5 ? -1 : 1;
    pet.hold = 2 + Math.random() * 5;
  }
}

function update(dt) {
  pet.stateT += dt;
  if (pet.stateT >= pet.hold) { pet.stateT = 0; decide(); }

  if (pet.state === 'walk') {
    pet.x += pet.dir * pet.speed * dt;
    const half = (FW * SCALE) / 2;
    if (pet.x < half) { pet.x = half; pet.dir = 1; }
    if (pet.x > canvas.width - half) { pet.x = canvas.width - half; pet.dir = -1; }
  }

  const sh = sheets[pet.state];
  if (sh) {
    pet.frameT += dt * 1000;
    while (pet.frameT >= sh.ms) { pet.frameT -= sh.ms; pet.frame = (pet.frame + 1) % sh.frames; }
  }
}

let placed = null;   // last draw rect, for hit-testing
function draw() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  const sh = sheets[pet.state] || sheets.idle;
  if (!sh || !sh.img.complete || sh.img.naturalWidth === 0) return;

  const w = FW * SCALE, h = FH * SCALE;
  const dx = Math.round(pet.x - w / 2);
  const dy = Math.round(canvas.height - h);   // sprite bottom on the floor
  ctx.imageSmoothingEnabled = false;
  ctx.save();
  if (pet.dir < 0) {
    ctx.translate(dx + w, dy);
    ctx.scale(-1, 1);
    ctx.drawImage(sh.img, pet.frame * FW, 0, FW, FH, 0, 0, w, h);
  } else {
    ctx.drawImage(sh.img, pet.frame * FW, 0, FW, FH, dx, dy, w, h);
  }
  ctx.restore();
  placed = { dx, dy, w, h };
}

let prev = performance.now();
function loop(now) {
  const dt = Math.min(0.05, (now - prev) / 1000);
  prev = now;
  update(dt);
  draw();
  requestAnimationFrame(loop);
}
requestAnimationFrame(loop);

// --- hit testing / click-through ---
const hit = document.createElement('canvas');
hit.width = FW; hit.height = FH;
const hitCtx = hit.getContext('2d', { willReadFrequently: true });

function overPet(mx, my) {
  if (!placed) return false;
  const { dx, dy, w, h } = placed;
  if (mx < dx || mx > dx + w || my < dy || my > dy + h) return false;
  const sh = sheets[pet.state];
  if (!sh || !sh.img.complete) return false;
  let fx = (mx - dx) / SCALE;
  const fy = (my - dy) / SCALE;
  if (pet.dir < 0) fx = FW - fx;      // account for the horizontal flip
  const px = Math.max(0, Math.min(FW - 1, fx | 0));
  const py = Math.max(0, Math.min(FH - 1, fy | 0));
  hitCtx.clearRect(0, 0, FW, FH);
  hitCtx.drawImage(sh.img, pet.frame * FW, 0, FW, FH, 0, 0, FW, FH);
  return hitCtx.getImageData(px, py, 1, 1).data[3] > 40;
}

if (!TEST) {
  let ignoring = true;   // matches the window's initial setIgnoreMouseEvents(true)
  window.addEventListener('mousemove', (e) => {
    const over = overPet(e.clientX, e.clientY);
    if (over !== ignoring) return;   // state already correct
    ignoring = !over;
    P.setIgnore(ignoring);
  });
  window.addEventListener('click', (e) => {
    if (overPet(e.clientX, e.clientY)) { setState('happy'); pet.stateT = 0; pet.hold = 1.2; }
  });
}
