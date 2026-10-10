using UnityEngine;

namespace LumiWorld.Acs.TimeSystem
{
    // Optional bridge for a scene Button's OnClick; the runtime itself is created only once.
    public sealed class GameplayPauseControl : MonoBehaviour
    {
        public void Pause() => GameTimeRuntime.Instance?.Pause();
        public void Resume() => GameTimeRuntime.Instance?.Resume();
        public void TogglePause() => GameTimeRuntime.Instance?.TogglePause();
        public void SetPaused(bool paused) => GameTimeRuntime.Instance?.SetPaused(paused);
    }
}
