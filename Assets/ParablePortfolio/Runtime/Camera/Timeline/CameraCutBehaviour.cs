using UnityEngine.Playables;

namespace Parable.Camera
{
    /// <summary>
    /// Timeline 클립 재생 시 CameraDirector.SwitchTo를 호출하는 PlayableBehaviour.
    /// forceCut은 클립 경계에서 즉시 컷할지 여부.
    /// </summary>
    public class CameraCutBehaviour : PlayableBehaviour
    {
        public int  cameraIndex = 0;
        public bool forceCut    = false;

        CameraDirector _director;
        bool           _fired;

        public override void OnGraphStart(Playable playable)
        {
            _fired = false;
        }

        public override void OnBehaviourPlay(Playable playable, FrameData info)
        {
            if (_director == null || _fired) return;

            _director.SwitchTo(cameraIndex, forceCut);
            _fired = true;
        }

        /// <summary>Track 바인딩에서 CameraDirector 주입.</summary>
        public void Bind(CameraDirector director)
        {
            _director = director;
            _fired    = false;
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            // 클립이 끝나거나 멈출 때 재발화 방지용 리셋
            _fired = false;
        }
    }
}
