using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.WebRTC;
using UnityEngine;

namespace SmartLife.Core
{
    /// <summary>
    /// One WebRTC call, answering a supervisor's offer. Media is DTLS-SRTP encrypted by WebRTC and nothing
    /// is recorded. Camera and microphone are touched only inside Answer, which EmergencyCoordinator calls
    /// after StreamPolicy allows it, and both are released in End. Sends the AR camera's output, because
    /// Vuforia holds the device camera. See docs/clinical_architecture.md section 2.4.
    /// </summary>
    public class EmergencyStream : MonoBehaviour
    {
        private const int VideoWidth = 640;
        private const int VideoHeight = 480;
        private const int MicSampleRate = 48000;
        private const int MicBufferSeconds = 1;
        private const float MaxStreamSeconds = 900f;
        private const string StunServer = "stun:stun.l.google.com:19302";
        private const string EndMessage = "end";

        [Serializable]
        private class CandidateJson
        {
            public string candidate;
            public string sdpMid;
            public int sdpMLineIndex;
        }

        /// <summary>Why the call stopped, for the coordinator to report. The camera and mic are already released.</summary>
        public event Action OnEnded;
        /// <summary>The peers connected.</summary>
        public event Action OnConnected;

        public bool IsActive { get; private set; }
        public bool IsSendingVideo => _videoTrack != null;
        public bool IsConnected { get; private set; }

        private readonly List<string> _pendingCandidates = new List<string>();
        private RTCPeerConnection _peer;
        private RTCDataChannel _control;
        private VideoStreamTrack _videoTrack;
        private AudioStreamTrack _micTrack;
        private AudioSource _micSource;
        private AudioSource _remoteSource;
        private Camera _capturedCamera;
        private Coroutine _webRtcUpdate;
        private Action<string> _sendCandidate;
        private bool _remoteDescriptionSet;
        private float _startedAt;

        private void Update()
        {
            if (IsActive && Time.unscaledTime - _startedAt >= MaxStreamSeconds) End();
        }

        private void OnDestroy() => End();

        /// <summary>Starts capture and answers. Only EmergencyCoordinator may call this, after StreamPolicy allows it.</summary>
        public void Answer(string offerSdp, bool allowVideo, Action<string> sendAnswer, Action<string> sendCandidate)
        {
            if (IsActive) return;
            IsActive = true;
            IsConnected = false;
            _startedAt = Time.unscaledTime;
            _sendCandidate = sendCandidate;
            _remoteDescriptionSet = false;
            _pendingCandidates.Clear();
            StartCoroutine(AnswerRoutine(offerSdp, allowVideo, sendAnswer));
        }

        /// <summary>Adds a supervisor candidate, as the JSON the browser produced.</summary>
        public void AddRemoteCandidate(string json)
        {
            if (!IsActive) return;
            if (!_remoteDescriptionSet)
            {
                _pendingCandidates.Add(json);
                return;
            }
            ApplyCandidate(json);
        }

        /// <summary>Ends the call and releases camera and microphone. Safe to call repeatedly.</summary>
        public void End()
        {
            if (!IsActive) return;
            IsActive = false;
            IsConnected = false;

            if (_webRtcUpdate != null) StopCoroutine(_webRtcUpdate);
            _webRtcUpdate = null;

            if (_control != null)
            {
                _control.OnMessage = null;
                _control.Dispose();
                _control = null;
            }
            if (_peer != null)
            {
                _peer.OnIceCandidate = null;
                _peer.OnConnectionStateChange = null;
                _peer.OnTrack = null;
                _peer.OnDataChannel = null;
                _peer.Close();
                _peer.Dispose();
                _peer = null;
            }
            ReleaseCapture();
            OnEnded?.Invoke();
        }

        private IEnumerator AnswerRoutine(string offerSdp, bool allowVideo, Action<string> sendAnswer)
        {
            _webRtcUpdate = StartCoroutine(WebRTC.Update());

            RTCConfiguration config = default;
            config.iceServers = new[] { new RTCIceServer { urls = new[] { StunServer } } };
            _peer = new RTCPeerConnection(ref config);
            _peer.OnIceCandidate = candidate => _sendCandidate?.Invoke(ToJson(candidate));
            _peer.OnConnectionStateChange = HandleConnectionState;
            _peer.OnTrack = HandleRemoteTrack;
            _peer.OnDataChannel = HandleDataChannel;

            var offer = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = offerSdp };
            RTCSetSessionDescriptionAsyncOperation setRemote = _peer.SetRemoteDescription(ref offer);
            yield return setRemote;
            if (setRemote.IsError) { Fail("offer"); yield break; }
            _remoteDescriptionSet = true;

            StartCapture(allowVideo);

            RTCSessionDescriptionAsyncOperation create = _peer.CreateAnswer();
            yield return create;
            if (create.IsError) { Fail("answer"); yield break; }

            RTCSessionDescription answer = create.Desc;
            RTCSetSessionDescriptionAsyncOperation setLocal = _peer.SetLocalDescription(ref answer);
            yield return setLocal;
            if (setLocal.IsError) { Fail("local description"); yield break; }

            sendAnswer?.Invoke(answer.sdp);

            foreach (string json in _pendingCandidates) ApplyCandidate(json);
            _pendingCandidates.Clear();
        }

        // Tracks are added after the offer is set, so they attach to the transceivers the supervisor offered.
        private void StartCapture(bool allowVideo)
        {
            if (allowVideo)
            {
                _capturedCamera = Camera.main;
                if (_capturedCamera != null) _videoTrack = _capturedCamera.CaptureStreamTrack(VideoWidth, VideoHeight);
                else Debug.LogWarning("[EmergencyStream] No main camera, so this call is audio only.", this);
                if (_videoTrack != null) _peer.AddTrack(_videoTrack);
            }

            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[EmergencyStream] No microphone, so the supervisor will not hear the room.", this);
                return;
            }

            _micSource = gameObject.AddComponent<AudioSource>();
            _micSource.clip = Microphone.Start(null, true, MicBufferSeconds, MicSampleRate);
            _micSource.loop = true;
            _micSource.Play();
            _micTrack = new AudioStreamTrack(_micSource) { Loopback = false };
            _peer.AddTrack(_micTrack);
        }

        private void ReleaseCapture()
        {
            if (_videoTrack != null)
            {
                _videoTrack.Dispose();
                _videoTrack = null;
            }
            // Capturing redirects this camera, so give the tablet's screen its picture back.
            if (_capturedCamera != null) _capturedCamera.targetTexture = null;
            _capturedCamera = null;

            if (_micTrack != null)
            {
                _micTrack.Dispose();
                _micTrack = null;
            }
            if (_micSource != null)
            {
                _micSource.Stop();
                Microphone.End(null);
                Destroy(_micSource);
                _micSource = null;
            }
            if (_remoteSource != null)
            {
                _remoteSource.Stop();
                Destroy(_remoteSource);
                _remoteSource = null;
            }
        }

        private void HandleConnectionState(RTCPeerConnectionState state)
        {
            switch (state)
            {
                case RTCPeerConnectionState.Connected:
                    IsConnected = true;
                    OnConnected?.Invoke();
                    break;
                case RTCPeerConnectionState.Disconnected:
                case RTCPeerConnectionState.Failed:
                case RTCPeerConnectionState.Closed:
                    End();
                    break;
            }
        }

        // A supervisor's voice is played. Their video is not shown to the child (clinical_architecture.md 2.4).
        private void HandleRemoteTrack(RTCTrackEvent e)
        {
            if (!(e.Track is AudioStreamTrack audio)) return;
            if (_remoteSource == null) _remoteSource = gameObject.AddComponent<AudioSource>();
            _remoteSource.SetTrack(audio);
            _remoteSource.loop = true;
            _remoteSource.Play();
        }

        private void HandleDataChannel(RTCDataChannel channel)
        {
            _control = channel;
            _control.OnMessage = bytes =>
            {
                if (Encoding.UTF8.GetString(bytes) == EndMessage) End();
            };
        }

        private void ApplyCandidate(string json)
        {
            CandidateJson parsed = JsonUtility.FromJson<CandidateJson>(json);
            if (parsed == null || string.IsNullOrEmpty(parsed.candidate) || _peer == null) return;
            _peer.AddIceCandidate(new RTCIceCandidate(new RTCIceCandidateInit
            {
                candidate = parsed.candidate,
                sdpMid = parsed.sdpMid,
                sdpMLineIndex = parsed.sdpMLineIndex,
            }));
        }

        private static string ToJson(RTCIceCandidate candidate) => JsonUtility.ToJson(new CandidateJson
        {
            candidate = candidate.Candidate,
            sdpMid = candidate.SdpMid,
            sdpMLineIndex = candidate.SdpMLineIndex ?? 0,
        });

        private void Fail(string step)
        {
            Debug.LogWarning($"[EmergencyStream] Could not set up the call ({step}).", this);
            End();
        }
    }
}
