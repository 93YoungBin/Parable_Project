using System.Collections.Generic;
using UnityEngine;

namespace Parable.Motion.Core
{
    /// <summary>
    /// T-2.5: Owns the ordered stage list and drives the pipeline entry point.
    /// Attach to the same GameObject as HumanoidRigStandardizer (the source avatar).
    /// Assign stages in Inspector order: Cleanup → Retargeting → IK.
    /// </summary>
    public class MotionPipelineRunner : MonoBehaviour
    {
        [Header("Pipeline")]
        [Tooltip("Ordered list of stages. Runner wires nextStage automatically on Start.")]
        public List<HumanoidPipelineStage> stages = new List<HumanoidPipelineStage>();

        [Header("Control")]
        public bool pipelineEnabled = true;

        HumanoidRigStandardizer _source;

        void Start()
        {
            _source = GetComponent<HumanoidRigStandardizer>();

            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] == null) continue;
                stages[i].nextStage = (i + 1 < stages.Count) ? stages[i + 1] : null;
            }

            if (_source != null && stages.Count > 0)
                _source.nextStage = stages[0];
        }

        public void SetEnabled(bool value)
        {
            pipelineEnabled = value;
            if (_source != null) _source.enabled = value;
            foreach (var s in stages)
                if (s != null) s.enabled = value;
        }

        public void RewireStages()
        {
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] == null) continue;
                stages[i].nextStage = (i + 1 < stages.Count) ? stages[i + 1] : null;
            }

            if (_source != null && stages.Count > 0)
                _source.nextStage = stages[0];
        }

        public HumanoidPipelineStage GetStage(int index)
            => (index >= 0 && index < stages.Count) ? stages[index] : null;

        public T GetStage<T>() where T : HumanoidPipelineStage
        {
            foreach (var s in stages)
                if (s is T t) return t;
            return null;
        }
    }
}
