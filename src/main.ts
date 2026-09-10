import './style.css';
import { Game } from './game/game';
import { Hud } from './ui/hud';
import { bindInput } from './ui/input';
import { Screens } from './ui/screens';
import { $ } from './ui/dom';

const canvas = $('game') as HTMLCanvasElement;
const game = new Game(canvas);
let hud: Hud;
const screens = new Screens(game, () => {
  hud.setVisible(true);
  hud.rebuild();
});
hud = new Hud(game, () => screens.pause());
hud.setVisible(false);

game.callbacks = {
  onResults: (info) => screens.results(info),
  onStateChange: () => hud.rebuild(),
  onMessage: (text, color, big) => hud.showMessage(text, color, big),
};

bindInput(canvas, game, screens);
screens.title();

let last = performance.now();
function frame(now: number): void {
  const dt = (now - last) / 1000;
  last = now;
  game.tick(now);
  hud.update(dt);
  requestAnimationFrame(frame);
}
requestAnimationFrame(frame);

// Expose for debugging in the console.
(window as unknown as { game: Game }).game = game;
