using System.Text;
using UnityEngine;

namespace Parable.Motion.Core
{
    /// <summary>
    /// T-2.5: Realtime pipeline stats overlay. Toggle with Tab at runtime.
    /// </summary>
    public class MotionPipelineDebugUI : MonoBehaviour
    {
        [Header("References")]
        public MotionPipelineRunner runner;

        [Header("Display")]
        public KeyCode toggleKey   = KeyCode.Tab;
        public Vector2 windowPos   = new Vector2(10f, 10f);
        public float   windowWidth = 280f;

        bool  _visible = true;
        float _fps;
        int   _frameCount;
        float _fpsTimer;

        MotionCleanupStage _cleanup;
        RetargetingStage   _retarget;
        IKSolverStage      _ik;

        readonly StringBuilder _sb = new StringBuilder(512);

        void Start()
        {
            if (runner == null) runner = GetComponent<MotionPipelineRunner>();
            if (runner == null) return;

            _cleanup  = runner.GetStage<MotionCleanupStage>();
            _retarget = runner.GetStage<RetargetingStage>();
            _ik       = runner.GetStage<IKSolverStage>();
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                _visible = !_visible;

            _frameCount++;
            _fpsTimer += Time.unscaledDeltaTime;
            if (_fpsTimer >= 0.5f)
            {
                _fps        = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer   = 0f;
            }
        }

        void OnGUI()
        {
            if (!_visible) return;

            _sb.Clear();
            _sb.AppendLine("── Motion Pipeline ──────────────");
            _sb.AppendFormat("FPS       {0:F1}\n", _fps);
            _sb.AppendFormat("Pipeline  {0}\n",
                runner != null && runner.pipelineEnabled ? "RUNNING" : "STOPPED");

            _sb.AppendLine("\n[1] RigStandardizer");
            var src = runner != null ? runner.GetComponent<HumanoidRigStandardizer>() : null;
            _sb.AppendFormat("  active  {0}\n", src != null && src.enabled);

            _sb.AppendLine("[2] MotionCleanupStage");
            if (_cleanup != null)
            {
                _sb.AppendFormat("  active  {0}\n", _cleanup.enabled);
                _sb.AppendFormat("  jitter  {0:F4}\n", _cleanup.jitterThreshold);
            }
            else _sb.AppendLine("  (not assigned)");

            _sb.AppendLine("[3] RetargetingStage");
            if (_retarget != null)
            {
                _sb.AppendFormat("  active   {0}\n", _retarget.enabled);
                _sb.AppendFormat("  mask     {0}\n", _retarget.applyMask);
                _sb.AppendFormat("  profile  {0}\n",
                    _retarget.skeletonProfile != null ? _retarget.skeletonProfile.name : "none");
            }
            else _sb.AppendLine("  (not assigned)");

            _sb.AppendLine("[4] IKSolverStage");
            if (_ik != null)
                _sb.AppendFormat("  LH {0:F2}  RH {1:F2}  LF {2:F2}  RF {3:F2}  Hd {4:F2}\n",
                    _ik.leftHandWeight, _ik.rightHandWeight,
                    _ik.leftFootWeight, _ik.rightFootWeight, _ik.headLookWeight);
            else
                _sb.AppendLine("  (not assigned)");

            _sb.AppendFormat("\n[{0}] Toggle", toggleKey);

            string text = _sb.ToString();

            var boxStyle = new GUIStyle(GUI.skin.box);
            float height = boxStyle.CalcHeight(new GUIContent(text), windowWidth) + 20f;

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.Box(new Rect(windowPos.x, windowPos.y, windowWidth, height), GUIContent.none);
            GUI.color = Color.white;

            var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(windowPos.x + 8f, windowPos.y + 4f, windowWidth - 16f, height),
                      text, labelStyle);
        }
    }
}
