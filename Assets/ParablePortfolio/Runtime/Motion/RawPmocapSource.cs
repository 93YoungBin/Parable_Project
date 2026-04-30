using System;
using System.IO;
using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// raw_arms_degrees.pmocap (version 2) 파일을 읽어 파이프라인에 공급.
    ///
    /// 출력 데이터 특성:
    ///   muscles[] 값이 -1~1 정규화 값이 아닌 관절 각도(degrees).
    ///   → 1차: MotionCleanupStage (노이즈 제거)
    ///   → 2차: NormalizationStage (degrees → -1~1 muscle 변환)
    ///   → 3차: IK Stage
    /// </summary>
    public class RawPmocapSource : MonoBehaviour
    {
        [Header("파일")]
        public string fileName = "raw_arms_degrees.pmocap";

        [Header("재생")]
        public bool playOnStart = true;
        public bool loop        = true;
        [Range(0.1f, 3f)] public float speed = 1f;

        [Header("파이프라인 연결")]
        public HumanoidPipelineStage nextStage;

        [Header("마스크")]
        public PoseMaskFlags activeMask = PoseMaskFlags.Arms;

        // ── 공개 상태 ─────────────────────────────────────────────────
        public bool  IsLoaded     { get; private set; }
        public bool  IsPlaying    { get; private set; }
        public int   FrameCount   { get; private set; }
        public int   CurrentFrame { get; private set; }
        public float Progress     => FrameCount > 0 ? (float)CurrentFrame / FrameCount : 0f;

        // ── 내부 ─────────────────────────────────────────────────────
        float[][]    _muscles;
        Vector3[]    _positions;
        Quaternion[] _rotations;
        int          _muscleCount;
        float        _fps = 30f;
        float        _timer;

        [System.NonSerialized] HumanoidPoseData _pose;

        void Awake()
        {
            _pose = new HumanoidPoseData();
        }

        void Start()
        {
            if (!string.IsNullOrEmpty(fileName))
                Load(fileName);
        }

        // Update에서 파이프라인 전단계(클린업·정규화·리타겟팅) 실행.
        // IKSolverStage는 자체 LateUpdate()에서 별도로 동작 → 패러블 아티클 구조 일치.
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space)) TogglePlay();
            if (Input.GetKeyDown(KeyCode.R))     Rewind();

            if (!IsLoaded || !IsPlaying) return;

            AdvanceTime();
            FillPose();
            nextStage?.Receive(_pose);
        }

        public void Load(string file)
        {
            string path = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Recordings", file));

            if (!File.Exists(path))
            {
                Debug.LogError($"[RawPmocapSource] 파일 없음: {path}");
                return;
            }

            try   { LoadBinary(path); }
            catch (Exception e) { Debug.LogError($"[RawPmocapSource] 로드 실패: {e.Message}"); }
        }

        void LoadBinary(string path)
        {
            using var r = new BinaryReader(File.Open(path, FileMode.Open));

            string magic = new string(r.ReadChars(8)).TrimEnd('\0');
            if (magic != "PMOCAP")
            {
                Debug.LogError("[RawPmocapSource] 잘못된 파일 형식");
                return;
            }

            int version = r.ReadInt32();
            if (version != 2)
                Debug.LogWarning($"[RawPmocapSource] version {version} — raw degrees 파일(v2)이 아닐 수 있음");

            _fps         = r.ReadSingle();
            FrameCount   = r.ReadInt32();
            _muscleCount = r.ReadInt32();

            _muscles   = new float[FrameCount][];
            _positions = new Vector3[FrameCount];
            _rotations = new Quaternion[FrameCount];

            for (int i = 0; i < FrameCount; i++)
            {
                _muscles[i] = new float[_muscleCount];
                for (int j = 0; j < _muscleCount; j++)
                    _muscles[i][j] = r.ReadSingle();

                float px = r.ReadSingle(), py = r.ReadSingle(), pz = r.ReadSingle();
                _positions[i] = new Vector3(px, py, pz);

                float rx = r.ReadSingle(), ry = r.ReadSingle(),
                      rz = r.ReadSingle(), rw = r.ReadSingle();
                _rotations[i] = new Quaternion(rx, ry, rz, rw);
            }

            IsLoaded     = true;
            CurrentFrame = 0;
            _timer       = 0f;

            Debug.Log($"[RawPmocapSource] 로드 완료 — {FrameCount}프레임 / raw degrees\n{path}");

            if (playOnStart) IsPlaying = true;
        }

        void FillPose()
        {
            int copyLen = Mathf.Min(_pose.muscles.Length, _muscleCount);
            Array.Copy(_muscles[CurrentFrame], _pose.muscles, copyLen);
            _pose.bodyPosition = _positions[CurrentFrame];
            _pose.bodyRotation = _rotations[CurrentFrame];
            _pose.timestamp    = Time.time;
            _pose.isValid      = true;
            _pose.activeMask   = activeMask;
        }

        void AdvanceTime()
        {
            _timer += Time.deltaTime * speed;
            float frameDuration = 1f / _fps;

            while (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                CurrentFrame++;

                if (CurrentFrame >= FrameCount)
                {
                    if (loop) CurrentFrame = 0;
                    else { CurrentFrame = FrameCount - 1; IsPlaying = false; }
                }
            }
        }

        public void TogglePlay() { if (IsLoaded) IsPlaying = !IsPlaying; }
        public void Rewind()     { CurrentFrame = 0; _timer = 0f; }
        /*
        void OnGUI()
        {
            if (!IsLoaded) return;

            var style = new GUIStyle(GUI.skin.box) { fontSize = 12 };
            style.normal.textColor = Color.cyan;

            string state = IsPlaying ? "▶" : "■";
            int barLen   = 20;
            int filled   = Mathf.RoundToInt(Progress * barLen);
            string bar   = "[" + new string('█', filled) + new string('─', barLen - filled) + "]";

            GUI.Box(new Rect(10, 70, 260, 55),
                $"Raw Pmocap  {state}  {bar}\n" +
                $"{CurrentFrame}/{FrameCount}  [Space]재생  [R]되감기", style);
        }*/
    }
}
