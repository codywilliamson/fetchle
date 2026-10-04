// synthesizes public/soundtrack.wav, scored to the hype video's frame timeline
import { mkdirSync, writeFileSync } from "node:fs";

const SR = 48000;
const FPS = 30;
const FRAMES = 994;
const LEN = Math.ceil((FRAMES / FPS) * SR);
const TAU = Math.PI * 2;

// scene starts in global frames, see Hype.tsx
const SCENE = { guess: 138, reveal: 206, search: 289, race: 432, meaning: 530, agents: 643, stats: 756, outro: 869 };
const DROP = SCENE.reveal + 16;
const BEAT = 0.5;

const bus = () => ({ l: new Float32Array(LEN), r: new Float32Array(LEN) });
const drums = bus();
const music = bus();
const sfx = bus();

const f = (frame) => Math.round((frame / FPS) * SR);
const s = (secs) => Math.round(secs * SR);

let seed = 7;
const rnd = () => ((seed = (seed * 1664525 + 1013904223) >>> 0) / 4294967296);
const noise = () => rnd() * 2 - 1;
const lpCoef = (hz) => 1 - Math.exp((-TAU * hz) / SR);

const put = (b, i, l, r = l) => {
  if (i < 0 || i >= LEN) return;
  b.l[i] += l;
  b.r[i] += r;
};

function kick(at, gain = 0.9) {
  let ph = 0;
  for (let i = 0; i < s(0.45); i++) {
    const t = i / SR;
    ph += (TAU * (45 + 110 * Math.exp(-t * 28))) / SR;
    const click = i < 240 ? noise() * 0.25 * (1 - i / 240) : 0;
    put(drums, at + i, (Math.sin(ph) * Math.exp(-t * 7) + click) * gain);
  }
}

function snare(at, gain = 0.3) {
  let lp = 0;
  for (let i = 0; i < s(0.22); i++) {
    const t = i / SR;
    const x = noise();
    lp += 0.25 * (x - lp);
    const v = ((x - lp) * Math.exp(-t * 20) + Math.sin(TAU * 185 * t) * Math.exp(-t * 35) * 0.5) * gain;
    put(drums, at + i, v * 0.9, v);
  }
}

function hat(at, gain = 0.07, decay = 90) {
  let lp = 0;
  const pan = rnd() * 0.4 - 0.2;
  for (let i = 0; i < s(0.12); i++) {
    const x = noise();
    lp += 0.6 * (x - lp);
    const v = (x - lp) * Math.exp((-i / SR) * decay) * gain;
    put(drums, at + i, v * (1 - pan), v * (1 + pan));
  }
}

function crash(at, gain = 0.25) {
  let lp = 0;
  for (let i = 0; i < s(1.6); i++) {
    const x = noise();
    lp += 0.5 * (x - lp);
    const v = (x - lp) * Math.exp((-i / SR) * 2.6) * gain;
    put(drums, at + i, v * (0.8 + rnd() * 0.4), v * (0.8 + rnd() * 0.4));
  }
}

function click(at, gain = 0.1) {
  let lp = 0;
  const pan = rnd() * 0.6 - 0.3;
  for (let i = 0; i < s(0.014); i++) {
    const x = noise();
    lp += 0.4 * (x - lp);
    const v = (x - lp) * Math.exp((-i / SR) * 380) * gain;
    put(sfx, at + i, v * (1 - pan), v * (1 + pan));
  }
}

function blip(at, freq, gain = 0.15, decay = 9) {
  for (let i = 0; i < s(0.6); i++) {
    const t = i / SR;
    const env = Math.min(1, t / 0.004) * Math.exp(-t * decay);
    put(sfx, at + i, (Math.sin(TAU * freq * t) + 0.4 * Math.sin(TAU * freq * 2.01 * t)) * env * gain);
  }
}

function impact(at, gain = 0.9) {
  let ph = 0;
  let lp = 0;
  for (let i = 0; i < s(2.4); i++) {
    const t = i / SR;
    ph += (TAU * (32 + 40 * Math.exp(-t * 3))) / SR;
    lp += 0.08 * (noise() - lp);
    put(sfx, at + i, (Math.sin(ph) * Math.exp(-t * 1.8) + lp * Math.exp(-t * 5) * 1.5) * gain);
  }
}

function whoosh(at, dur = 0.5, gain = 0.3, up = true) {
  let lp = 0;
  const n = s(dur);
  for (let i = 0; i < n; i++) {
    const p = i / n;
    const hz = up ? 300 * Math.pow(25, p) : 7500 * Math.pow(1 / 25, p);
    lp += lpCoef(hz) * (noise() - lp);
    const v = lp * Math.sin(Math.PI * p) ** 2 * gain * 2;
    put(sfx, at + i, v * (1 - p), v * p);
  }
}

function riser(from, to, gain = 0.25) {
  let lp = 0;
  let ph = 0;
  const n = to - from;
  for (let i = 0; i < n; i++) {
    const p = i / n;
    lp += lpCoef(200 * Math.pow(40, p)) * (noise() - lp);
    ph += (TAU * 110 * Math.pow(8, p)) / SR;
    put(sfx, from + i, (lp * 1.4 + Math.sin(ph) * 0.25) * p * p * gain);
  }
}

function glitch(at) {
  for (let k = 0; k < 7; k++) {
    const start = at + s(0.05 * k);
    const freq = 80 + rnd() * 900;
    for (let i = 0; i < s(0.035); i++) {
      const sq = Math.sign(Math.sin(TAU * freq * (i / SR)));
      put(sfx, start + i, sq * 0.12 * (k % 2 ? 1 : 0.6), sq * 0.12 * (k % 2 ? 0.6 : 1));
    }
  }
}

const saw = (ph) => 2 * (ph - Math.floor(ph)) - 1;

// detuned saw stack through a one-pole lowpass
function synth(b, from, to, freqs, gain, cutoff, attack = 0.25, release = 0.4) {
  const voices = freqs.flatMap((hz) => [hz * 0.996, hz, hz * 1.004]);
  const ph = voices.map(() => rnd());
  let lpl = 0;
  let lpr = 0;
  const c = lpCoef(cutoff);
  const n = to - from;
  for (let i = 0; i < n + s(release); i++) {
    const t = i / SR;
    let l = 0;
    let r = 0;
    voices.forEach((hz, k) => {
      ph[k] += hz / SR;
      const v = saw(ph[k]);
      if (k % 3 === 0) l += v;
      else if (k % 3 === 2) r += v;
      else {
        l += v * 0.5;
        r += v * 0.5;
      }
    });
    lpl += c * (l - lpl);
    lpr += c * (r - lpr);
    const env = Math.min(1, t / attack) * (i < n ? 1 : Math.exp(-(i - n) / (SR * release * 0.3)));
    put(b, from + i, (lpl / voices.length) * env * gain, (lpr / voices.length) * env * gain);
  }
}

const CHORDS = [
  { root: 55, pad: [220, 261.63, 329.63] },
  { root: 43.65, pad: [174.61, 220, 261.63] },
  { root: 65.41, pad: [196, 261.63, 329.63] },
  { root: 49, pad: [196, 246.94, 293.66] },
];

// tension: drone, typing, accelerating ticks, riser, failure
synth(music, 0, f(130), [55, 82.41], 0.55, 260, 0.6, 0.05);
for (let fr = 4; fr < 34; fr += 1.5) click(f(fr) + Math.floor(rnd() * 400), 0.09);
for (let t = 40 / FPS; t < 128 / FPS; t += 0.42 * Math.pow(1 - (t - 40 / FPS) / (88 / FPS), 1.6) + 0.045) hat(s(t), 0.12, 60);
riser(f(40), f(130), 0.3);
impact(f(130), 1);
glitch(f(130));

// the guess
kick(f(SCENE.guess + 2), 0.7);
blip(f(SCENE.guess + 2), 110, 0.2, 4);
impact(f(SCENE.guess + 24), 0.8);
glitch(f(SCENE.guess + 24));
riser(f(SCENE.guess + 40), f(DROP), 0.35);

// the drop
impact(f(DROP), 0.9);
crash(f(DROP), 0.3);
const end = f(SCENE.outro);
for (let t = DROP / FPS, beat = 0; s(t) < end; t += BEAT, beat++) {
  kick(s(t));
  if (beat % 2 === 1) snare(s(t));
  hat(s(t + BEAT / 2));
  if (beat % 4 === 3) hat(s(t + BEAT * 0.75), 0.05);
}
for (let t = DROP / FPS, bar = 0; s(t) < end; t += BEAT * 4, bar++) {
  const chord = CHORDS[bar % CHORDS.length];
  const to = Math.min(end, s(t + BEAT * 4));
  synth(music, s(t), to, chord.pad, 0.5, 1800, 0.08, 0.3);
  for (let k = 0; k < 8; k++) synth(music, s(t + (k * BEAT) / 2), s(t + (k * BEAT) / 2 + 0.2), [chord.root], 0.9, 420, 0.005, 0.08);
}

// transitions and on-screen beats
[SCENE.search, SCENE.race, SCENE.meaning, SCENE.agents, SCENE.stats].forEach((fr) => whoosh(f(fr) - s(0.15), 0.5, 0.3));
for (let fr = SCENE.search + 6; fr < SCENE.search + 42; fr += 2) click(f(fr), 0.08);
click(f(SCENE.search + 50), 0.2);
[54, 60, 66].forEach((d, i) => blip(f(SCENE.search + d), 880 * (1 + i * 0.25), 0.1, 14));
blip(f(SCENE.search + 80), 1760, 0.16, 7);
whoosh(f(SCENE.race + 14), 0.25, 0.35);
blip(f(SCENE.race + 18), 1568, 0.14, 8);
blip(f(SCENE.race + 95), 92, 0.25, 3);
whoosh(f(SCENE.meaning + 30), 1.3, 0.25);
[0, 4, 8].forEach((d, i) => blip(f(SCENE.meaning + 70 + d), [1318.5, 1975.5, 2637][i], 0.12, 6));
for (let fr = SCENE.agents + 4; fr < SCENE.agents + 34; fr += 2) click(f(fr), 0.07);
[50, 55, 60].forEach((d) => blip(f(SCENE.agents + d), 1046.5, 0.08, 16));
blip(f(SCENE.agents + 68), 2093, 0.12, 8);
[0, 1, 2, 3].forEach((k) => {
  const at = f(SCENE.stats + 20 + k * 14);
  kick(at, 1);
  impact(at, 0.35);
});

// outro: final hit and a held chord
impact(f(SCENE.outro + 2), 1);
crash(f(SCENE.outro + 2), 0.25);
synth(music, f(SCENE.outro), LEN, [110, 220, 261.63, 329.63, 440], 0.6, 1400, 0.05, 0.1);

// mix: sidechain music to the kicks, ping-pong delay on music and sfx, soft clip
const duck = new Float32Array(LEN).fill(1);
for (let t = DROP / FPS; s(t) < end; t += BEAT) {
  for (let i = 0; i < s(0.3); i++) {
    const j = s(t) + i;
    if (j < LEN) duck[j] = Math.min(duck[j], 1 - 0.65 * Math.exp((-i / SR) * 10));
  }
}
const D = s(0.375);
const outL = new Float32Array(LEN);
const outR = new Float32Array(LEN);
const dl = new Float32Array(LEN);
const dr = new Float32Array(LEN);
for (let i = 0; i < LEN; i++) {
  const ml = music.l[i] * duck[i] + sfx.l[i];
  const mr = music.r[i] * duck[i] + sfx.r[i];
  dl[i] = ml + (i >= D ? dr[i - D] * 0.38 : 0);
  dr[i] = mr + (i >= D ? dl[i - D] * 0.38 : 0);
  const fade = Math.min(1, (LEN - i) / s(0.6));
  outL[i] = Math.tanh((ml + drums.l[i] + (i >= D ? dl[i - D] : 0) * 0.22) * 0.75) * fade;
  outR[i] = Math.tanh((mr + drums.r[i] + (i >= D ? dr[i - D] : 0) * 0.22) * 0.75) * fade;
}

let peak = 0;
for (let i = 0; i < LEN; i++) peak = Math.max(peak, Math.abs(outL[i]), Math.abs(outR[i]));
const norm = 0.89 / peak;

const wav = Buffer.alloc(44 + LEN * 4);
wav.write("RIFF", 0);
wav.writeUInt32LE(36 + LEN * 4, 4);
wav.write("WAVEfmt ", 8);
wav.writeUInt32LE(16, 16);
wav.writeUInt16LE(1, 20);
wav.writeUInt16LE(2, 22);
wav.writeUInt32LE(SR, 24);
wav.writeUInt32LE(SR * 4, 28);
wav.writeUInt16LE(4, 32);
wav.writeUInt16LE(16, 34);
wav.write("data", 36);
wav.writeUInt32LE(LEN * 4, 40);
for (let i = 0; i < LEN; i++) {
  wav.writeInt16LE(Math.round(outL[i] * norm * 32767), 44 + i * 4);
  wav.writeInt16LE(Math.round(outR[i] * norm * 32767), 46 + i * 4);
}
mkdirSync(new URL("../public/", import.meta.url), { recursive: true });
writeFileSync(new URL("../public/soundtrack.wav", import.meta.url), wav);
console.log(`soundtrack.wav: ${(LEN / SR).toFixed(2)}s, peak ${peak.toFixed(2)} before normalize`);
