using UnityEngine;

namespace Parable.Motion
{
    /// <summary>
    /// 파이프라인 각 단계의 공통 베이스 클래스.
    /// T-2.2 Cleanup / T-2.3 Retarget / T-2.4 IK 모두 이를 상속.
    ///
    /// 설계 원칙 (CLAUDE.md):
    ///   각 단계는 독립적으로 테스트 가능해야 함.
    ///   Receive() 하나만 구현하면 파이프라인에 꽂을 수 있음.
    /// </summary>
    public abstract class HumanoidPipelineStage : MonoBehaviour
    {
        [Header("Pipeline")]
        public HumanoidPipelineStage nextStage;

        // 이전 단계에서 포즈를 받아 처리 후 다음 단계로 전달
        public void Receive(HumanoidPoseData pose)
        {
            var result = Process(pose);
            if (result != null)
                nextStage?.Receive(result);
        }

        // 각 단계가 구현할 처리 로직
        // null 반환 시 파이프라인 중단 (유효하지 않은 프레임 스킵 등)
        protected abstract HumanoidPoseData Process(HumanoidPoseData input);
    }
}