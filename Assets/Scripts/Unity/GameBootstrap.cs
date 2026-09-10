using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NeonLineRider.Unity
{
    /// <summary>
    /// Builds the whole game at runtime: camera, world renderer, UI canvas, audio and input. The
    /// scene only needs this component on any GameObject; it also self-installs if a scene has none.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private GameController _game;
        private WorldRenderer _renderer;
        private Hud _hud;
        private Screens _screens;
        private InputController _input;
        private AudioSynth _audio;
        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (_installed) return;
            if (FindObjectOfType<GameBootstrap>() != null) return;
            var go = new GameObject("Neon Line Rider");
            go.AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            if (_installed)
            {
                Destroy(gameObject);
                return;
            }
            _installed = true;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            UiKit.Init();

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 760);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _game = new GameController(new FileStorage());
            _audio = gameObject.AddComponent<AudioSynth>();
            _audio.Enabled = _game.Progress.Music;
            _game.Audio = _audio;

            var worldRoot = new GameObject("World").transform;
            _renderer = new WorldRenderer(cam, worldRoot, UiKit.Font);
            _hud = new Hud(canvas.transform, _game, () => _screens.Pause());
            _screens = new Screens(canvas.transform, _game, () =>
            {
                _hud.SetVisible(true);
                _hud.Rebuild();
            }, (text, color, big) => _hud.ShowMessage(text, color, big));
            _input = new InputController(_game, _screens);

            _game.OnResults = info => _screens.Results(info);
            _game.OnStateChange = () => _hud.Rebuild();
            _game.OnMessage = (text, color, big) => _hud.ShowMessage(text, color, big);

            _hud.SetVisible(false);
            _game.Camera.Resize(Screen.width, Screen.height);
            _screens.Title();
        }

        private void Update()
        {
            if (_game == null) return;
            double dt = Time.unscaledDeltaTime;
            _input.Update();
            _game.Tick(dt);
            _hud.Update(dt);
            _renderer.Render(_game);
        }
    }
}
