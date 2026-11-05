using UnityEngine;

namespace SmartLife.Core
{
    /// <summary>
    /// Shared Time.timeScale freeze. The AAC board and the guardian menu can overlap, so the
    /// clock restarts only when the last holder lets go. Task state is untouched.
    /// </summary>
    public static class PauseLock
    {
        private static int _holders;

        public static bool IsHeld => _holders > 0;

        public static void Acquire()
        {
            _holders++;
            Time.timeScale = 0f;
        }

        public static void Release()
        {
            if (_holders == 0) return;
            _holders--;
            if (_holders == 0) Time.timeScale = 1f;
        }

        /// <summary>Clears every holder. Used on sign-out and scene reload.</summary>
        public static void Clear()
        {
            _holders = 0;
            Time.timeScale = 1f;
        }
    }
}
