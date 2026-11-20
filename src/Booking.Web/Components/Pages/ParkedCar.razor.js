// Real recordings, see wwwroot/sounds/CREDITS.md for sources and licences.
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
