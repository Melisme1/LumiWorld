using System;
using UnityEngine;

namespace LumiWorld.Acs.TimeSystem
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class GameTimeRuntime : MonoBehaviour
    {
        private static GameTimeRuntime instance;
        private static bool shuttingDown;
        private GameClock clock;
        private TimerService timers;
        private float resumeTimeScale = 1;
        private bool ownsPause;

        public static GameTimeRuntime Instance
        {
            get
            {
                if (instance == null && !shuttingDown)
                {
                    instance = FindAnyObjectByType<GameTimeRuntime>();
                    if (instance == null) instance = new GameObject("GameTimeSystem").AddComponent<GameTimeRuntime>();
                    instance.Initialize();
                }
                return instance;
            }
        }
        public GameClock Clock { get { Initialize(); return clock; } }
        public TimerService Timers { get { Initialize(); return timers; } }
        public bool IsPaused => clock != null && clock.IsPaused;
        public event Action<bool> PauseChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; shuttingDown = false; }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            Initialize();
            if (transform.parent == null) DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
        }

        private void Initialize()
        {
            if (clock != null) return;
            clock = new GameClock(new SystemTimeSource());
            timers = new TimerService(clock);
        }

        private void Update()
        {
            // Unity timeScale pauses animation/physics; it does not drive gameplay timers.
            if (ownsPause) UnityEngine.Time.timeScale = 0;
            Timers.Tick();
        }

        [ContextMenu("Pause Gameplay")]
        public void Pause() => SetPaused(true);
        [ContextMenu("Resume Gameplay")]
        public void Resume() => SetPaused(false);
        public void TogglePause() => SetPaused(!IsPaused);

        public void SetPaused(bool paused)
        {
            if (!Application.isPlaying) return;
            Timers.Tick();
            if (!Clock.SetPaused(paused)) return;
            if (paused)
            {
                resumeTimeScale = UnityEngine.Time.timeScale;
                ownsPause = true;
                UnityEngine.Time.timeScale = 0;
            }
            else RestoreTimeScale();
            // One presentation subscriber must not prevent other systems observing pause.
            var handlers = PauseChanged;
            if (handlers != null)
                foreach (Action<bool> handler in handlers.GetInvocationList())
                    try { handler(paused); } catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void RestoreTimeScale()
        {
            if (!ownsPause) return;
            UnityEngine.Time.timeScale = resumeTimeScale;
            ownsPause = false;
        }
        // Losing focus/app suspension is not a manual pause. Elapsed time catches up on return.
        private void OnApplicationQuit() { shuttingDown = true; RestoreTimeScale(); }
        private void OnDestroy() { if (instance == this) { RestoreTimeScale(); instance = null; } }
    }
}
