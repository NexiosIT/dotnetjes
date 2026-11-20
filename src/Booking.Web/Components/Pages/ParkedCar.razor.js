// One goose honk and one engine rev, see wwwroot/sounds/CREDITS.md for sources and licences.
const sounds = {
    vroom: new Audio("sounds/car-vroom.mp3"),
    honk: new Audio("sounds/goose-honk.mp3"),
};

function play(name) {
    const audio = sounds[name];
    audio.currentTime = 0;               // restart on repeated clicks
    return audio.play().catch(() => {}); // browser blocked autoplay: stay silent, no error
}

export function vroom() { return play("vroom"); }

export function honk() { return play("honk"); }

/**
 * After a cancel: rev, pull out of the bay, fly to the Log out button and crash into it.
 * The car is copied into a fixed-position layer so it can leave its bay and cross the page.
 */
export async function driveOutAndCrash(car, crashedTemplate) {
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

    // Impact: swap in the wreck, bounce back a little, shake the button.
    flyer.innerHTML = crashedTemplate.innerHTML;
    target.classList.add("crash-shake");
    setTimeout(() => target.classList.remove("crash-shake"), 600);
    flyer.animate([
        { transform: `translate(${dx}px, ${dy}px) rotate(0deg)` },
        { transform: `translate(${dx - 18}px, ${dy + 6}px) rotate(-8deg)` },
    ], { duration: 250, easing: "ease-out", fill: "forwards" });

    // Let everyone admire the wreck, then fade it away.
    await new Promise(r => setTimeout(r, 2500));
    await flyer.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 600, fill: "forwards" }).finished;
    flyer.remove();
}
