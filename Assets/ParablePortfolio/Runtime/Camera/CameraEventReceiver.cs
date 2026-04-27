using UnityEngine;

namespace Parable.Camera
{
    /// <summary>
    /// Timeline Signal → CameraDirector 브릿지.
    ///
    /// SignalReceiver 는 파라미터 없는 UnityEvent 만 지원하므로
    /// 카메라 인덱스별 메서드를 명시적으로 분리.
    ///
    /// 사용법:
    ///   - Timeline Signal Emitter 의 SignalAsset 을 SignalReceiver 반응에 연결
    ///   - 반응 메서드로 CutToWide / CutToMedium 등 지정
    ///   - forceCut = true 이면 블렌드 없이 즉시 컷
    /// </summary>
    public class CameraEventReceiver : MonoBehaviour
    {
        public CameraDirector director;

        [Tooltip("true면 모든 Signal 컷이 블렌드 없이 즉시 전환")]
        public bool forceCut = false;

        // ── 카메라 인덱스별 메서드 (SignalReceiver 연결용) ──────────

        public void CutToWide()     => director?.TriggerCut(0);
        public void CutToMedium()   => director?.TriggerCut(1);
        public void CutToCloseUp()  => director?.TriggerCut(2);
        public void CutToLeftSide() => director?.TriggerCut(3);

        // forceCut 버전
        public void CutToWideHard()     => director?.SwitchTo(0, true);
        public void CutToMediumHard()   => director?.SwitchTo(1, true);
        public void CutToCloseUpHard()  => director?.SwitchTo(2, true);
        public void CutToLeftSideHard() => director?.SwitchTo(3, true);
    }
}
