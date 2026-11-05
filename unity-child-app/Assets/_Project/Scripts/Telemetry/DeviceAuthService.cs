using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine;
using SmartLife.Core;

namespace SmartLife.Telemetry
{
    /// <summary>The signed-in tablet account and the learner it is mapped to in /devices/{uid}.</summary>
    public sealed class DeviceSession
    {
        public readonly string Uid;
        public readonly string StudentId;
        public DeviceSession(string uid, string studentId) { Uid = uid; StudentId = studentId; }
    }

    /// <summary>Seam for the guardian UI, so its flow is testable without Firebase.</summary>
    public interface IDeviceAuth
    {
        /// <summary>False once a check has shown that Firebase cannot run on this device.</summary>
        bool FirebaseAvailable { get; }
        /// <summary>Returns the cached session, or null when a guardian has to sign in.</summary>
        void Restore(Action<DeviceSession> done);
        /// <summary>Returns a session, or null plus a guardian-readable message.</summary>
        void SignIn(string email, string password, Action<DeviceSession, string> done);
        void SignOut();
        /// <summary>The published routine for this route, or null if it cannot be read in time.</summary>
        void FetchRoutine(string routeId, Action<IReadOnlyList<RoutineEntry>> done);
    }

    /// <summary>Applies RTDB settings that must be set before the first database call.</summary>
    internal static class DatabaseSetup
    {
        private static bool _done;

        public static void Ensure()
        {
            if (_done) return;
            _done = true;
            FirebaseDatabase.DefaultInstance.SetPersistenceEnabled(true);
        }
    }

    /// <summary>
    /// Firebase Auth for the tablet's dedicated account. The Firebase SDK keeps the refresh token on
    /// the device, so later boots need no password. This class stores only the learner's opaque ID.
    /// </summary>
    public class DeviceAuthService : MonoBehaviour, IDeviceAuth
    {
        private const string StudentKeyPrefix = "smartlife.device.studentId.";
        private const float RestoreWaitSeconds = 2f;
        private const int LookupTimeoutMs = 8000;
        private const int RoutineTimeoutMs = 3000;

        private readonly IKeyValueStore _store = new PlayerPrefsStore();

        public bool FirebaseAvailable { get; private set; } = true;

        public void Restore(Action<DeviceSession> done)
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(check =>
            {
                if (this == null) return;
                if (!check.IsCompletedSuccessfully || check.Result != DependencyStatus.Available)
                {
                    FirebaseAvailable = false;
                    Debug.LogWarning("[DeviceAuth] Firebase is not available on this device.", this);
                    done(null);
                    return;
                }
                StartCoroutine(WaitForSavedUser(done));
            });
        }

        // The SDK can load its saved user just after startup, so give it a moment before asking for a sign-in.
        private IEnumerator WaitForSavedUser(Action<DeviceSession> done)
        {
            FirebaseAuth auth;
            try
            {
                auth = FirebaseAuth.DefaultInstance;
            }
            catch (Exception e)
            {
                FirebaseAvailable = false;
                Debug.LogWarning($"[DeviceAuth] Firebase Auth could not start. {e.GetType().Name}", this);
                done(null);
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + RestoreWaitSeconds;
            while (auth.CurrentUser == null && Time.realtimeSinceStartup < deadline) yield return null;

            string uid = auth.CurrentUser?.UserId;
            if (uid == null)
            {
                done(null);
                yield break;
            }
            ResolveStudent(uid, (session, _) => done(session));
        }

        public void SignIn(string email, string password, Action<DeviceSession, string> done)
        {
            FirebaseAuth.DefaultInstance.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
            {
                if (this == null) return;
                if (task.IsFaulted || task.IsCanceled || task.Result?.User == null)
                {
                    done(null, DescribeFailure(task));
                    return;
                }
                ResolveStudent(task.Result.User.UserId, done);
            });
        }

        public void SignOut()
        {
            string uid = FirebaseAuth.DefaultInstance.CurrentUser?.UserId;
            if (uid != null) _store.Delete(StudentKeyPrefix + uid);
            FirebaseAuth.DefaultInstance.SignOut();
        }

        // A short wait: the learner is looking at the app, and the authored task is a safe fallback.
        public void FetchRoutine(string routeId, Action<IReadOnlyList<RoutineEntry>> done)
        {
            Task<DataSnapshot> read;
            try
            {
                DatabaseSetup.Ensure();
                read = FirebaseDatabase.DefaultInstance.RootReference.Child(DbPaths.RouteCheckpoints(routeId)).GetValueAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DeviceAuth] Using the built-in routine. {e.GetType().Name}", this);
                done(null);
                return;
            }

            Task.WhenAny(read, Task.Delay(RoutineTimeoutMs)).ContinueWithOnMainThread(first =>
            {
                if (this == null) return;
                if (first.Result != read || read.IsFaulted || read.IsCanceled || !read.Result.Exists)
                {
                    Debug.LogWarning("[DeviceAuth] Using the built-in routine: the published one could not be read.", this);
                    done(null);
                    return;
                }

                var entries = new List<RoutineEntry>();
                foreach (DataSnapshot checkpoint in read.Result.Children)
                {
                    object order = checkpoint.Child("order").Value;
                    if (order == null) continue;
                    bool enabled = !(checkpoint.Child("enabled").Value is bool on) || on;
                    entries.Add(new RoutineEntry(checkpoint.Key, Convert.ToInt32(order), enabled));
                }
                done(entries);
            });
        }

        // The cached ID lets the tablet boot straight into the task while offline. An RTDB read with
        // no network can wait forever, so a first-time lookup has a timeout.
        private void ResolveStudent(string uid, Action<DeviceSession, string> done)
        {
            string cached = _store.GetString(StudentKeyPrefix + uid);
            if (!string.IsNullOrEmpty(cached))
            {
                done(new DeviceSession(uid, cached), null);
                return;
            }

            Task<DataSnapshot> lookup;
            try
            {
                DatabaseSetup.Ensure();
                lookup = FirebaseDatabase.DefaultInstance.RootReference.Child(DbPaths.DeviceStudent(uid)).GetValueAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DeviceAuth] Could not read the device record. {e.GetType().Name}", this);
                done(null, "This tablet could not reach the database.");
                return;
            }

            Task.WhenAny(lookup, Task.Delay(LookupTimeoutMs)).ContinueWithOnMainThread(first =>
            {
                if (this == null) return;
                if (first.Result != lookup || lookup.IsFaulted || lookup.IsCanceled)
                {
                    done(null, "This tablet could not reach the database. Check the internet connection.");
                    return;
                }
                string studentId = lookup.Result.Value as string;
                if (string.IsNullOrEmpty(studentId))
                {
                    done(null, "This account is not linked to a learner yet. Ask an admin to add it under /devices.");
                    return;
                }
                _store.SetString(StudentKeyPrefix + uid, studentId);
                done(new DeviceSession(uid, studentId), null);
            });
        }

        // Generic on purpose: the message must not reveal whether an email has an account.
        private static string DescribeFailure(Task task)
        {
            var firebase = task.Exception?.GetBaseException() as FirebaseException;
            return firebase != null && firebase.ErrorCode == (int)AuthError.NetworkRequestFailed
                ? "No internet connection. Try again when the tablet is online."
                : "Those sign-in details did not work.";
        }
    }
}
