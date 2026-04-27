using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Parable.Camera
{
    /// <summary>
    /// Timeline 클립 에셋. Inspector에서 카메라 슬롯 인덱스와 컷 방식을 지정.
    /// </summary>
    [Serializable]
    public class CameraCutClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("전환할 CameraDirector 슬롯 인덱스")]
        public int cameraIndex = 0;

        [Tooltip("true면 프로필 블렌드 무시하고 즉시 컷")]
        public bool forceCut = false;

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<CameraCutBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour();
            behaviour.cameraIndex = cameraIndex;
            behaviour.forceCut    = forceCut;
            return playable;
        }
    }
}
