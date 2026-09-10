using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CyberRider.Unity
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
        private CanvasScaler _scaler;
        private RectTransform _safeArea;
        private int _lastWidth, _lastHeight;
        private Rect _lastSafe;
        private static bool _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (_installed) return;
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            var go = new GameObject("Cyber Rider");
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
            // Phones and tablets get finger-sized controls, and the screen stays on while drawing.
            UiKit.Compact = Application.isMobilePlatform;
            if (Application.isMobilePlatform) Screen.sleepTimeout = SleepTimeout.NeverSleep;
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
                InputBridge.AddUiModule(es);
            }

            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            _scaler = canvasGo.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _scaler.scaleFactor = UiKit.ComputeScale();
            canvasGo.AddComponent<GraphicRaycaster>();

            _game = new GameController(new FileStorage());
            _audio = gameObject.AddComponent<AudioSynth>();
            _audio.Enabled = _game.Progress.Music;
            _game.Audio = _audio;

            var worldRoot = new GameObject("World").transform;
            _renderer = new WorldRenderer(cam, worldRoot, UiKit.Font);
            // The HUD keeps clear of notches and rounded corners; menus dim the whole screen.
            _safeArea = UiKit.Rect("SafeArea", canvas.transform);
            UiKit.ApplySafeArea(_safeArea);
            _hud = new Hud(_safeArea, _game, () => _screens.Pause());
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
            RememberScreen();
            _screens.Title();
        }

        private void RememberScreen()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastSafe = Screen.safeArea;
        }

        private void Update()
        {
            if (_game == null) return;
            Rect safe = Screen.safeArea;
            if (Screen.width != _lastWidth || Screen.height != _lastHeight || safe.x != _lastSafe.x || safe.y != _lastSafe.y || safe.width != _lastSafe.width || safe.height != _lastSafe.height)
            {
                // Rotation, window resize or a notch change: rescale the UI and re-fit the safe area.
                RememberScreen();
                _scaler.scaleFactor = UiKit.ComputeScale();
                UiKit.ApplySafeArea(_safeArea);
            }
            double dt = Time.unscaledDeltaTime;
            _input.Update();
            _game.Tick(dt);
            _hud.Update(dt);
            _renderer.Render(_game);
        }
    }
}
