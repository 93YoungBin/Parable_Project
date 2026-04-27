using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// Python MediaPipe 스크립트에서 UDP로 전송된 pose landmark 를 수신.
    ///
    /// 데이터 포맷: 33 landmarks × 4 floats (x, y, z, visibility) = 528 bytes
    ///
    /// 스레드 모델:
    ///   수신 스레드(백그라운드) → double buffer 스왑 → 메인 스레드(LateUpdate)에서 읽기
    ///   락 범위를 최소화해 메인 스레드 블로킹 방지.
    ///
    /// 사용법:
    ///   1. 이 컴포넌트를 씬에 추가 (MotionPipeline과 같은 GameObject 또는 별도)
    ///   2. Python 스크립트 실행: python mediapipe_sender.py
    ///   3. MotionPipeline.mediaPipe 필드에 이 컴포넌트 할당
    /// </summary>
    public class MediaPipeReceiver : MonoBehaviour
    {
        [Header("UDP 설정")]
        [Tooltip("Python 스크립트와 동일한 포트 (기본 7777)")]
        public int port = 7777;

        [Header("좌표 보정")]
        [Tooltip("MediaPipe world coord → Unity coord 위치 스케일")]
        public float positionScale = 1f;

        [Tooltip("미러링: 셀프카메라(전면)는 true, 외부 카메라는 false")]
        public bool mirrorX = true;

        // ── 상수 ──────────────────────────────────────────────────
        public const int LANDMARK_COUNT = 33;
        const int FLOATS_PER_LANDMARK   = 4; // x, y, z, visibility
        const int PACKET_SIZE = LANDMARK_COUNT * FLOATS_PER_LANDMARK * sizeof(float);

        // ── Double Buffer (스레드 안전) ───────────────────────────
        readonly float[] _buf0 = new float[LANDMARK_COUNT * FLOATS_PER_LANDMARK];
        readonly float[] _buf1 = new float[LANDMARK_COUNT * FLOATS_PER_LANDMARK];
        float[] _readBuf;
        float[] _writeBuf;
        readonly object _swapLock = new object();
        bool _hasNewFrame;

        // ── 공개 접근 (메인 스레드 전용) ─────────────────────────
        public Vector3[] Landmarks    { get; } = new Vector3[LANDMARK_COUNT];
        public float[]   Visibilities { get; } = new float[LANDMARK_COUNT];
        public bool IsConnected { get; private set; }
        public int FrameCount   { get; private set; }

        UdpClient _udp;
        Thread    _thread;
        bool      _running;

        // ── 생명주기 ─────────────────────────────────────────────

        void Awake()
        {
            _readBuf  = _buf0;
            _writeBuf = _buf1;
        }

        void Start()
        {
            _running = true;
            _udp = new UdpClient(port);
            _udp.Client.ReceiveTimeout = 1000;

            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "MediaPipeUDP" };
            _thread.Start();

            Debug.Log($"[MediaPipeReceiver] UDP 수신 대기 중 (port {port})");
        }

        void OnDestroy()
        {
            _running = false;
            _udp?.Close();
            _thread?.Join(500);
        }

        // ── UDP 수신 스레드 ──────────────────────────────────────

        void ReceiveLoop()
        {
            var ep = new IPEndPoint(IPAddress.Any, 0);
            while (_running)
            {
                try
                {
                    byte[] data = _udp.Receive(ref ep);
                    if (data.Length != PACKET_SIZE) continue;

                    Buffer.BlockCopy(data, 0, _writeBuf, 0, data.Length);

                    lock (_swapLock)
                    {
                        (_readBuf, _writeBuf) = (_writeBuf, _readBuf);
                        _hasNewFrame = true;
                        IsConnected  = true;
                        FrameCount++;
                    }
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.TimedOut)
                    {
                        IsConnected = false; // 타임아웃 → 연결 끊김으로 간주
                    }
                    else if (_running)
                    {
                        Debug.LogWarning($"[MediaPipeReceiver] {e.Message}");
                    }
                }
                catch (Exception e)
                {
                    if (_running) Debug.LogError($"[MediaPipeReceiver] {e}");
                }
            }
        }

        // ── 메인 스레드 API ──────────────────────────────────────

        /// <summary>
        /// 메인 스레드(LateUpdate)에서 호출.
        /// 새 프레임이 있으면 Landmarks / Visibilities 를 갱신하고 true 반환.
        /// </summary>
        public bool TryParseLandmarks()
        {
            float[] data;
            bool hadNew;

            lock (_swapLock)
            {
                hadNew    = _hasNewFrame;
                _hasNewFrame = false;
                data      = _readBuf;
            }

            if (!hadNew) return false;

            float xSign = mirrorX ? -1f : 1f;

            for (int i = 0; i < LANDMARK_COUNT; i++)
            {
                int b = i * FLOATS_PER_LANDMARK;
                float mp_x = data[b + 0];
                float mp_y = data[b + 1];
                float mp_z = data[b + 2];
                float vis  = data[b + 3];

                // MediaPipe world: 오른손계, Y-up, Z = 카메라 방향(앞이 +)
                // Unity:            왼손계,  Y-up, Z = 앞이 +
                // → X 반전 (mirrorX 고려), Z 유지
                Landmarks[i]    = new Vector3(xSign * mp_x, mp_y, -mp_z) * positionScale;
                Visibilities[i] = vis;
            }

            return true;
        }
    }
}
