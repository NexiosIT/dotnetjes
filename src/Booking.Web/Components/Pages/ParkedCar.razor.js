// Clown horn, synthesized with Web Audio (no sound file needed): two squeaky "honk"s.
let ctx;

export function honk() {
    ctx ??= new AudioContext();
    if (ctx.state === "suspended") ctx.resume();
    const start = ctx.currentTime;
    tone(start, 0.22);
    tone(start + 0.28, 0.3);
}

function tone(at, length) {
    const osc = ctx.createOscillator();
    const wobble = ctx.createOscillator();
    const wobbleDepth = ctx.createGain();
    const volume = ctx.createGain();

    osc.type = "sawtooth";
    osc.frequency.setValueAtTime(520, at);
    osc.frequency.exponentialRampToValueAtTime(380, at + length); // the squeeze

    wobble.frequency.value = 28;          // rubber-bulb flutter
    wobbleDepth.gain.value = 25;
    wobble.connect(wobbleDepth).connect(osc.frequency);

    volume.gain.setValueAtTime(0.0001, at);
    volume.gain.exponentialRampToValueAtTime(0.3, at + 0.02);
    volume.gain.exponentialRampToValueAtTime(0.0001, at + length);

    osc.connect(volume).connect(ctx.destination);
    osc.start(at);
    wobble.start(at);
    osc.stop(at + length + 0.05);
    wobble.stop(at + length + 0.05);
}
