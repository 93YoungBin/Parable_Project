using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Parable.Camera
{
    /// <summary>
    /// CameraDirector에 바인딩되는 커스텀 Timeline 트랙.
    ///
    /// 사용법:
    ///   1. PlayableDirector가 있는 GameObject에 Timeline 에셋 연결
    ///   2. Add Track → CameraCutTrack
    ///   3. 트랙 바인딩 슬롯에 CameraDirector 드래그
    ///   4. 클립 추가 → CameraIndex 지정
    /// </summary>
    [TrackColor(0.2f, 0.6f, 1.0f)]
    [TrackClipType(typeof(CameraCutClip))]
    [TrackBindingType(typeof(CameraDirector))]
    public class CameraCutTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            // 각 클립의 Behaviour에 CameraDirector를 주입
            var director = go.GetComponent<PlayableDirector>();
            if (director != null)
            {
                var binding = director.GetGenericBinding(this) as CameraDirector;
                if (binding != null)
                {
                    foreach (var clip in GetClips())
                    {
                        var asset = clip.asset as CameraCutClip;
                        if (asset == null) continue;

                        // Playable이 아직 생성되기 전이므로 asset의 cameraIndex/forceCut은
                        // CreatePlayable → CameraCutBehaviour.Bind 로 전달.
                        // Bind 호출은 아래 믹서 PlayableBehaviour에서 처리.
                    }
                }
            }

            return ScriptPlayable<CameraCutMixerBehaviour>.Create(graph, inputCount);
        }
    }

    /// <summary>
    /// 트랙 믹서. 활성 클립의 Behaviour에 CameraDirector를 주입하고 전환을 위임.
    /// </summary>
    public class CameraCutMixerBehaviour : PlayableBehaviour
    {
        CameraDirector _director;

        public override void OnPlayableCreate(Playable playable) { }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            _director = playerData as CameraDirector;
            if (_director == null) return;

            int inputCount = playable.GetInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight <= 0f) continue;

                var inputPlayable = (ScriptPlayable<CameraCutBehaviour>)playable.GetInput(i);
                var behaviour     = inputPlayable.GetBehaviour();
                behaviour.Bind(_director);
            }
        }
    }
}
