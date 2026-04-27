using System;
using System.Collections;
using System.Diagnostics;
using Mediapipe;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Core;
using Mediapipe.Unity;
using Mediapipe.Unity.Experimental;
using Mediapipe.Unity.Sample;
using Mediapipe.Unity.Sample.PoseLandmarkDetection;
using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// homuler MediaPipe Unity Plugin (Tasks API) 기반 실시간 포즈 트래커.
    ///
    /// 동작 방식:
    ///   WebCamTexture → TextureFramePool → PoseLandmarker (LIVE_STREAM)
    ///   → 콜백(백그라운드 스레드) → 더블 버퍼 → TryGetWorldLandmarks(메인 스레드)
    ///
    /// 의존성:
    ///   씬에 Bootstrap GameObject 필요 (MediaPipeUnity 샘플 씬 구조 참고).
    ///   Bootstrap이 AssetLoader / Glog / GpuManager를 초기화해야 함.
    ///
    /// 사용법:
    ///   1. 씬에 Bootstrap + AppSettings 설정 (샘플 씬에서 복사)
    ///   2. 이 컴포넌트를 씬에 추가
    ///   3. MotionPipeline.poseTracker 에 할당
    /// </summary>
    public class MediaPipePoseTracker : MonoBehaviour
    {
        [Header("웹캠 설정")]
        [Tooltip("사용할 카메라 장치 이름. 비워두면 아래 Index를 사용.\n" +
                 "Play 전에 PrintAvailableCameras()로 이름 목록 확인 가능.")]
        public string webcamDeviceName = "";

        [Tooltip("webcamDeviceName이 비어있을 때 사용할 카메라 인덱스 (0 = 첫 번째)")]
        public int webcamIndex = 0;
        public int targetWidth  = 640;
        public int targetHeight = 480;
        public int targetFPS    = 30;

        [Header("MediaPipe 모델")]
        [Tooltip("lite = 빠름 / full = 균형 / heavy = 정확")]
        public ModelType modelType = ModelType.BlazePoseFull;

        [Range(0f, 1f)] public float minPoseDetectionConfidence = 0.5f;
        [Range(0f, 1f)] public float minPosePresenceConfidence  = 0.5f;
        [Range(0f, 1f)] public float minTrackingConfidence      = 0.5f;

        [Header("좌표 보정")]
        [Tooltip("셀피(전면) 카메라 = true, 외부 카메라 = false")]
        public bool mirrorX = true;

        // ── 공개 상태 (메인 스레드 전용) ───────────────────────────
        public bool IsRunning  { get; private set; }
        public bool HasPose    { get; private set; }
        public int  FrameCount { get; private set; }

        // ── MediaPipe 33개 랜드마크 인덱스 상수 ────────────────────
        public const int LANDMARK_COUNT = 33;

        // ── 더블 버퍼 (백그라운드 콜백 → 메인 스레드 전송용) ─────────
        readonly Vector3[] _buf0 = new Vector3[LANDMARK_COUNT];
        readonly Vector3[] _buf1 = new Vector3[LANDMARK_COUNT];
        readonly float[]   _vis0 = new float[LANDMARK_COUNT];
        readonly float[]   _vis1 = new float[LANDMARK_COUNT];

        Vector3[] _readLm,  _writeLm;
        float[]   _readVis, _writeVis;

        readonly object _bufLock = new object();
        bool _hasNewFrame;

        // ── 스냅샷 (메인 스레드 전용, Update()에서 갱신) ─────────────
        // 여러 컴포넌트(Debugger, MotionPipeline)가 동일 프레임에 읽어도
        // 서로 소비(consume)하지 않음 → 프레임 누락 버그 방지
        readonly Vector3[] _snapshotLm  = new Vector3[LANDMARK_COUNT];
        readonly float[]   _snapshotVis = new float[LANDMARK_COUNT];

        // ── 내부 ───────────────────────────────────────────────────
        PoseLandmarker _poseLandmarker;
        WebCamTexture  _webcamTex;
        TextureFramePool _framePool;

        readonly Stopwatch _stopwatch = new Stopwatch();
        Coroutine _runCoroutine;

        // ── 생명주기 ────────────────────────────────────────────────

        void Awake()
        {
            _readLm   = _buf0; _writeLm  = _buf1;
            _readVis  = _vis0; _writeVis = _vis1;
        }

        void Update()
        {
            // 더블 버퍼 → 스냅샷으로 펌프 (메인 스레드, 1회/프레임)
            // 여기서 한 번만 소비하고 스냅샷에 저장 →
            // 이후 Debugger/MotionPipeline 등 여러 곳이 스냅샷을 공유 읽기
            if (!IsRunning) return;

            Vector3[] src; float[] srcVis; bool hadNew;
            lock (_bufLock)
            {
                hadNew = _hasNewFrame;
                _hasNewFrame = false;
                src    = _readLm;
                srcVis = _readVis;
            }

            if (hadNew)
            {
                Array.Copy(src,    _snapshotLm,  LANDMARK_COUNT);
                Array.Copy(srcVis, _snapshotVis, LANDMARK_COUNT);
            }
        }

        IEnumerator Start()
        {
            // Bootstrap이 초기화를 끝낼 때까지 대기
            var bootstrap = FindObjectOfType<Bootstrap>();
            if (bootstrap == null)
            {
                UnityEngine.Debug.LogError(
                    "[MediaPipePoseTracker] 씬에 Bootstrap GameObject가 없습니다. " +
                    "MediaPipeUnity 샘플 씬에서 Bootstrap 오브젝트를 복사해 주세요.");
                yield break;
            }

            yield return new WaitUntil(() => bootstrap.isFinished);

            yield return InitializeAsync();
        }

        IEnumerator InitializeAsync()
        {
            // 모델 파일 준비 (StreamingAssets 또는 로컬 캐시로 복사)
            var config = new PoseLandmarkDetectionConfig { Model = modelType };
            yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

            // PoseLandmarker 생성
            var delegateType =
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                BaseOptions.Delegate.CPU;
#else
                BaseOptions.Delegate.GPU;
#endif

            var options = new PoseLandmarkerOptions(
                new BaseOptions(delegateType, modelAssetPath: config.ModelPath),
                runningMode:                Mediapipe.Tasks.Vision.Core.RunningMode.LIVE_STREAM,
                numPoses:                   1,
                minPoseDetectionConfidence: minPoseDetectionConfidence,
                minPosePresenceConfidence:  minPosePresenceConfidence,
                minTrackingConfidence:      minTrackingConfidence,
                outputSegmentationMasks:    false,
                resultCallback:             OnPoseLandmarkResult
            );

            _poseLandmarker = PoseLandmarker.CreateFromOptions(options, GpuManager.GpuResources);

            // 웹캠 시작
            if (!OpenWebcam())
                yield break;

            // 웹캠 해상도가 확정될 때까지 대기
            yield return new WaitUntil(() => _webcamTex.width > 16);

            _framePool = new TextureFramePool(_webcamTex.width, _webcamTex.height, TextureFormat.RGBA32, 10);

            _stopwatch.Start();
            IsRunning     = true;
            _runCoroutine = StartCoroutine(RunLoop());

            UnityEngine.Debug.Log(
                $"[MediaPipePoseTracker] 시작 — 카메라 {_webcamTex.deviceName} " +
                $"{_webcamTex.width}×{_webcamTex.height}");
        }

        void OnDestroy()
        {
            IsRunning = false;
            if (_runCoroutine != null) StopCoroutine(_runCoroutine);

            _webcamTex?.Stop();
            _framePool?.Dispose();
            _poseLandmarker?.Close();
            _stopwatch.Stop();
        }

        // ── 웹캠 초기화 ─────────────────────────────────────────────

        bool OpenWebcam()
        {
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                UnityEngine.Debug.LogError("[MediaPipePoseTracker] 웹캠을 찾을 수 없습니다.");
                return false;
            }

            // 사용 가능한 카메라 목록 로그
            for (int i = 0; i < devices.Length; i++)
                UnityEngine.Debug.Log($"[MediaPipePoseTracker] 카메라 [{i}] {devices[i].name}" +
                                      $" (전면: {devices[i].isFrontFacing})");

            string targetName;
            if (!string.IsNullOrEmpty(webcamDeviceName))
            {
                // 이름으로 검색 (부분 일치)
                bool found = false;
                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].name.IndexOf(webcamDeviceName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetName = devices[i].name;
                        found = true;
                        UnityEngine.Debug.Log($"[MediaPipePoseTracker] 이름으로 선택: [{i}] {targetName}");
                        _webcamTex = new WebCamTexture(targetName, targetWidth, targetHeight, targetFPS);
                        break;
                    }
                }
                if (!found)
                {
                    UnityEngine.Debug.LogWarning(
                        $"[MediaPipePoseTracker] '{webcamDeviceName}' 카메라를 찾지 못했습니다. " +
                        $"Index {webcamIndex} 로 폴백합니다.");
                }
            }

            if (_webcamTex == null)
            {
                int idx = Mathf.Clamp(webcamIndex, 0, devices.Length - 1);
                targetName = devices[idx].name;
                UnityEngine.Debug.Log($"[MediaPipePoseTracker] 인덱스로 선택: [{idx}] {targetName}");
                _webcamTex = new WebCamTexture(targetName, targetWidth, targetHeight, targetFPS);
            }

            _webcamTex.Play();
            return true;
        }

        // ── 메인 루프 (코루틴) ──────────────────────────────────────

        IEnumerator RunLoop()
        {
            var waitEndOfFrame = new WaitForEndOfFrame();

            while (IsRunning)
            {
                if (!_webcamTex.didUpdateThisFrame)
                {
                    yield return null;
                    continue;
                }

                if (!_framePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return waitEndOfFrame;
                    continue;
                }

                yield return waitEndOfFrame;

                // WebCamTexture → TextureFrame (CPU)
                // 웹캠은 기본적으로 수직 반전이 없으므로 flipV = false
                textureFrame.ReadTextureOnCPU(_webcamTex, flipHorizontally: false, flipVertically: false);
                var image = textureFrame.BuildCPUImage();
                textureFrame.Release();

                long timestampMs = _stopwatch.ElapsedTicks / TimeSpan.TicksPerMillisecond;
                _poseLandmarker.DetectAsync(image, timestampMs, new ImageProcessingOptions(rotationDegrees: 0));
            }
        }

        // ── MediaPipe 결과 콜백 (백그라운드 스레드) ─────────────────

        void OnPoseLandmarkResult(PoseLandmarkerResult result, Image image, long timestamp)
        {
            if (result.poseWorldLandmarks == null || result.poseWorldLandmarks.Count == 0)
            {
                HasPose = false;
                return;
            }

            var wldList = result.poseWorldLandmarks[0].landmarks;
            if (wldList == null || wldList.Count < LANDMARK_COUNT)
            {
                HasPose = false;
                return;
            }

            float xSign = mirrorX ? -1f : 1f;

            for (int i = 0; i < LANDMARK_COUNT; i++)
            {
                var lm = wldList[i];

                // MediaPipe world → Unity 좌표계
                //   MediaPipe: 오른손계, Y-up, Z = 카메라 방향으로 갈수록 작아짐(depth)
                //   Unity: 왼손계, Y-up, Z = 앞이 +
                //   → X 반전(미러링 옵션), Z 반전
                _writeLm[i]  = new Vector3(xSign * lm.x, lm.y, -lm.z);
                _writeVis[i] = lm.visibility ?? 0f;
            }

            lock (_bufLock)
            {
                (_readLm,  _writeLm)  = (_writeLm,  _readLm);
                (_readVis, _writeVis) = (_writeVis, _readVis);
                _hasNewFrame = true;
                HasPose      = true;
                FrameCount++;
            }
        }

        // ── 메인 스레드 API ─────────────────────────────────────────

        /// <summary>
        /// 최신 포즈 스냅샷을 landmarks / visibilities 배열에 복사하고 true 반환.
        /// HasPose = false 이면 false 반환.
        ///
        /// 비소비(non-consuming): 여러 컴포넌트가 같은 프레임에 여러 번 호출해도 OK.
        /// Update()에서 더블버퍼 → 스냅샷 갱신이 먼저 일어나므로
        /// Debugger(Update) / MotionPipeline(LateUpdate) 모두 동일한 최신 데이터를 읽음.
        ///
        /// landmarks[i]    : Unity 좌표계 world landmark (미터, 힙 중심 기준)
        /// visibilities[i] : 0 ~ 1 가시성 점수
        /// </summary>
        public bool TryGetWorldLandmarks(Vector3[] landmarks, float[] visibilities)
        {
            if (landmarks == null || landmarks.Length < LANDMARK_COUNT) return false;
            if (visibilities == null || visibilities.Length < LANDMARK_COUNT) return false;
            if (!HasPose) return false;

            Array.Copy(_snapshotLm,  landmarks,    LANDMARK_COUNT);
            Array.Copy(_snapshotVis, visibilities, LANDMARK_COUNT);
            return true;
        }
    }
}
