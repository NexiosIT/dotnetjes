// Sounds for the parked car, synthesized with Web Audio (no sound files needed).
let ctx;

function audio() {
    ctx ??= new AudioContext();
    if (ctx.state === "suspended") ctx.resume();
    return ctx;
}

/** "Vroom vroom": two revs while the car drives in (the CSS animation takes 1.6 s). */
export function vroom() {
    const a = audio();
    const t = a.currentTime;
    rev(a, t, 0.55, 70, 190);
    rev(a, t + 0.6, 0.9, 80, 230, 45); // second rev, then idle down as it parks
}

function rev(a, at, length, low, high, settle = low) {
    const engine = a.createOscillator();
    const rumble = a.createOscillator();
    const filter = a.createBiquadFilter();
    const volume = a.createGain();

    engine.type = "sawtooth";
    rumble.type = "square";
    for (const osc of [engine, rumble]) {
        const f = osc === engine ? 1 : 0.5;
        osc.frequency.setValueAtTime(low * f, at);
        osc.frequency.exponentialRampToValueAtTime(high * f, at + length * 0.45);
        osc.frequency.exponentialRampToValueAtTime(settle * f, at + length);
    }

    filter.type = "lowpass";
    filter.frequency.setValueAtTime(600, at);
    filter.frequency.linearRampToValueAtTime(1400, at + length * 0.45);
    filter.frequency.linearRampToValueAtTime(400, at + length);

    volume.gain.setValueAtTime(0.0001, at);
    volume.gain.exponentialRampToValueAtTime(0.25, at + 0.05);
    volume.gain.setValueAtTime(0.25, at + length * 0.6);
    volume.gain.exponentialRampToValueAtTime(0.0001, at + length);

    engine.connect(filter);
    rumble.connect(filter);
    filter.connect(volume).connect(a.destination);
    for (const osc of [engine, rumble]) {
        osc.start(at);
        osc.stop(at + length + 0.05);
    }
}

/** Goose honk: a loud nasal "HONK", then a shorter one. */
export function honk() {
    const a = audio();
    const t = a.currentTime;
    goose(a, t, 0.32, 1);
    goose(a, t + 0.4, 0.22, 0.8);
}

function goose(a, at, length, loudness) {
    const voice = a.createOscillator();
    const nasal = a.createBiquadFilter();
    const rasp = a.createBiquadFilter();
    const noise = a.createBufferSource();
    const noiseLevel = a.createGain();
    const volume = a.createGain();

    // A goose call jumps up, cracks and drops: fast pitch up, then a fall.
    voice.type = "square";
    voice.frequency.setValueAtTime(330, at);
    voice.frequency.exponentialRampToValueAtTime(560, at + 0.05);
    voice.frequency.exponentialRampToValueAtTime(470, at + length * 0.6);
    voice.frequency.exponentialRampToValueAtTime(300, at + length);

    // Strong resonance around 1.2 kHz makes it nasal.
    nasal.type = "bandpass";
    nasal.frequency.value = 1200;
    nasal.Q.value = 4;

    // A little breath noise for the rasp.
    const buffer = a.createBuffer(1, Math.ceil(a.sampleRate * length), a.sampleRate);
    const data = buffer.getChannelData(0);
    for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
    noise.buffer = buffer;
    rasp.type = "bandpass";
    rasp.frequency.value = 2500;
    noiseLevel.gain.value = 0.15;

    volume.gain.setValueAtTime(0.0001, at);
    volume.gain.exponentialRampToValueAtTime(0.5 * loudness, at + 0.02);
    volume.gain.setValueAtTime(0.5 * loudness, at + length * 0.7);
    volume.gain.exponentialRampToValueAtTime(0.0001, at + length);

    voice.connect(nasal).connect(volume);
    noise.connect(rasp).connect(noiseLevel).connect(volume);
    volume.connect(a.destination);

    voice.start(at);
    noise.start(at);
    voice.stop(at + length + 0.05);
    noise.stop(at + length + 0.05);
}
