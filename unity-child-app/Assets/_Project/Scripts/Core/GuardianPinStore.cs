using System;
using System.Security.Cryptography;
using UnityEngine;

namespace SmartLife.Core
{
    /// <summary>Small key-value seam so PIN storage can be tested without touching PlayerPrefs.</summary>
    public interface IKeyValueStore
    {
        string GetString(string key);
        void SetString(string key, string value);
        void Delete(string key);
    }

    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public string GetString(string key) => PlayerPrefs.GetString(key, string.Empty);
        public void SetString(string key, string value) { PlayerPrefs.SetString(key, value); PlayerPrefs.Save(); }
        public void Delete(string key) { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
    }

    public enum PinResult { Ok, Wrong, LockedOut }

    /// <summary>
    /// Guardian PIN, kept on this tablet only as a salted PBKDF2 hash (docs/SECURITY.md). Never stored
    /// in RTDB or in source. A 4-digit PIN stays guessable if the hash is stolen, so the lockout is what
    /// stops a child or passer-by from trying every value at the screen.
    /// </summary>
    public sealed class GuardianPinStore
    {
        public const int PinLength = 4;

        private const string HashKey = "smartlife.guardian.pinHash";
        private const string FailuresKey = "smartlife.guardian.pinFailures";
        private const string LockedUntilKey = "smartlife.guardian.pinLockedUntil";
        private const int FreeAttempts = 5;
        private const int BaseLockoutSeconds = 30;
        private const int MaxLockoutSeconds = 900;
        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        private readonly IKeyValueStore _store;
        private readonly Func<long> _nowSeconds;
        private readonly int _iterations;

        public GuardianPinStore(IKeyValueStore store, Func<long> nowSeconds = null, int iterations = 20000)
        {
            _store = store;
            _nowSeconds = nowSeconds ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _iterations = iterations;
        }

        public bool HasPin => !string.IsNullOrEmpty(_store.GetString(HashKey));

        public long LockoutSecondsRemaining
        {
            get
            {
                long.TryParse(_store.GetString(LockedUntilKey), out long until);
                return Math.Max(0, until - _nowSeconds());
            }
        }

        public static bool IsValidFormat(string pin)
        {
            if (pin == null || pin.Length != PinLength) return false;
            foreach (char c in pin) if (c < '0' || c > '9') return false;
            return true;
        }

        public void SetPin(string pin)
        {
            if (!IsValidFormat(pin)) throw new ArgumentException("PIN must be 4 digits.", nameof(pin));
            byte[] salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            byte[] hash = Derive(pin, salt, _iterations);
            _store.SetString(HashKey, $"{_iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}");
            ClearFailures();
        }

        public PinResult Verify(string pin)
        {
            if (LockoutSecondsRemaining > 0) return PinResult.LockedOut;
            if (Matches(pin))
            {
                ClearFailures();
                return PinResult.Ok;
            }

            int.TryParse(_store.GetString(FailuresKey), out int failures);
            failures++;
            _store.SetString(FailuresKey, failures.ToString());
            if (failures < FreeAttempts) return PinResult.Wrong;

            long seconds = Math.Min(MaxLockoutSeconds, (long)BaseLockoutSeconds << Math.Min(failures - FreeAttempts, 10));
            _store.SetString(LockedUntilKey, (_nowSeconds() + seconds).ToString());
            return PinResult.LockedOut;
        }

        private bool Matches(string pin)
        {
            if (!IsValidFormat(pin)) return false;
            string[] parts = _store.GetString(HashKey).Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int iterations)) return false;
            byte[] expected = Convert.FromBase64String(parts[2]);
            byte[] actual = Derive(pin, Convert.FromBase64String(parts[1]), iterations);
            int diff = expected.Length ^ actual.Length;
            for (int i = 0; i < expected.Length && i < actual.Length; i++) diff |= expected[i] ^ actual[i];
            return diff == 0;
        }

        private void ClearFailures()
        {
            _store.Delete(FailuresKey);
            _store.Delete(LockedUntilKey);
        }

        private static byte[] Derive(string pin, byte[] salt, int iterations)
        {
            using var kdf = new Rfc2898DeriveBytes(pin, salt, iterations, HashAlgorithmName.SHA256);
            return kdf.GetBytes(HashBytes);
        }
    }
}
