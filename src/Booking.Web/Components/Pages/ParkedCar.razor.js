// See wwwroot/sounds/CREDITS.md for sources and licences.
const sounds = {
    vroom: new Audio("sounds/car-vroom.mp3"),
    honk: new Audio("sounds/goose-honk.mp3"),
    crash: new Audio("sounds/car-crash.mp3"),
    ambulance: new Audio("sounds/ambulance-siren.mp3"),
};

function play(name) {
    const audio = sounds[name];
    audio.currentTime = 0;               // restart on repeated clicks
    return audio.play().catch(() => {}); // browser blocked autoplay: stay silent, no error
}

export function vroom() { return play("vroom"); }

export function honk() { return play("honk"); }

/**
 * After a cancel: rev, pull out of the bay, fly to the Log out button and crash into it;
 * then an ambulance comes to take the wreck away.
 * The car is copied into a fixed-position layer so it can leave its bay and cross the page.
 */
export async function driveOutAndCrash(car, crashedTemplate, ambulanceTemplate) {
    const target = document.getElementById("logout-button");
    const from = car.getBoundingClientRect();

    const flyer = document.createElement("div");
    flyer.className = "flying-car";
    flyer.innerHTML = car.innerHTML;
    Object.assign(flyer.style, {
        position: "fixed", left: `${from.left}px`, top: `${from.top}px`,
        width: `${from.width}px`, height: `${from.height}px`, zIndex: 2000, pointerEvents: "none",
    });
    document.body.appendChild(flyer);
    car.style.visibility = "hidden";

    play("vroom");

    if (matchMedia("(prefers-reduced-motion: reduce)").matches || !target) {
        flyer.remove();
        return;
    }

    // Aim the nose (right edge, middle) at the middle of the button.
    const to = target.getBoundingClientRect();
    const dx = to.left + to.width / 2 - (from.left + from.width);
    const dy = to.top + to.height / 2 - (from.top + from.height / 2);

    const drive = flyer.animate([
        { transform: "translate(0, 0) rotate(0deg)" },
        { transform: "translate(60px, 0) rotate(0deg)", offset: 0.3 },      // pull out of the bay
        { transform: `translate(${dx}px, ${dy}px) rotate(${dy < 0 ? -12 : 12}deg)` },
    ], { duration: 1300, easing: "cubic-bezier(.5, 0, .9, .6)", fill: "forwards" });
    await drive.finished;

    // Impact: crash sound, swap in the wreck, bounce back a little, shake the button.
    sounds.vroom.pause();
    play("crash");
    flyer.innerHTML = crashedTemplate.innerHTML;
    target.classList.add("crash-shake");
    setTimeout(() => target.classList.remove("crash-shake"), 600);
    flyer.animate([
        { transform: `translate(${dx}px, ${dy}px) rotate(0deg)` },
        { transform: `translate(${dx - 18}px, ${dy + 6}px) rotate(-8deg)` },
    ], { duration: 250, easing: "ease-out", fill: "forwards" });

    // As soon as the crash has died down the ambulance comes in from the right, loads the wreck and drives off.
    await new Promise(r => setTimeout(r, 1300));
    play("ambulance");
    await takeAway(flyer, ambulanceTemplate);
}

async function takeAway(wreck, ambulanceTemplate) {
    const w = wreck.getBoundingClientRect();
    const ambulance = document.createElement("div");
    ambulance.innerHTML = ambulanceTemplate.innerHTML;
    const width = 150, height = 80;
    // Stop with its middle over the wreck, wheels on the wreck's ground line.
    const stopLeft = w.left + w.width / 2 - width / 2;
    const top = w.bottom - height;
    Object.assign(ambulance.style, {
        position: "fixed", left: `${stopLeft}px`, top: `${top}px`,
        width: `${width}px`, height: `${height}px`, zIndex: 2001, pointerEvents: "none",
    });
    document.body.appendChild(ambulance);

    // Drive in from just past the right edge of the window (it faces left).
    const fromRight = window.innerWidth - stopLeft + 20;
    await ambulance.animate([
        { transform: `translateX(${fromRight}px)` },
        { transform: "translateX(-6px)", offset: 0.9 },   // brakes...
        { transform: "translateX(0)" },                     // ...and stops
    ], { duration: 1000, easing: "ease-out", fill: "forwards" }).finished;

    // Load the wreck: it shrinks into the back of the ambulance.
    // A transform is relative to the element's own left/top (where the car started), not where it is now.
    const originX = parseFloat(wreck.style.left) + wreck.offsetWidth / 2;
    const originY = parseFloat(wreck.style.top) + wreck.offsetHeight / 2;
    const intoX = stopLeft + width * 0.65 - originX;
    const intoY = top + height * 0.45 - originY;
    await wreck.animate([
        { transform: getComputedStyle(wreck).transform, opacity: 1 },
        { transform: `translate(${intoX}px, ${intoY}px) scale(.3)`, opacity: 0 },
    ], { duration: 450, easing: "ease-in", fill: "forwards" }).finished;
    wreck.remove();

    // Off it goes, out of the left side of the window.
    await ambulance.animate([
        { transform: "translateX(0)" },
        { transform: `translateX(${-(stopLeft + width + 40)}px)` },
    ], { duration: 1600, easing: "ease-in", fill: "forwards" }).finished;
    ambulance.remove();
}
