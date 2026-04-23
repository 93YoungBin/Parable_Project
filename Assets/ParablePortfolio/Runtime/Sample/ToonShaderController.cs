using System.Collections.Generic;
using UnityEngine;

namespace Parable.Sample
{
    public class ToonShaderController : MonoBehaviour
    {
        [Tooltip("NiloCat 또는 Parable/ToonLit 셰이더를 쓰는 베이스 머티리얼")]
        public Material toonMaterial;

        // 아바타별 원본 sharedMaterials 캐시
        readonly Dictionary<int, Material[][]> _originalMats  = new Dictionary<int, Material[][]>();
        // 아바타별 NiloCat 인스턴스 캐시 (Awake 타이밍에 미리 생성, OnDestroy에서만 해제)
        readonly Dictionary<int, Material[][]> _instancedMats = new Dictionary<int, Material[][]>();
        // 상태 추적
        readonly HashSet<int> _toonOn = new HashSet<int>();

        AvatarToonTarget _current;

        public void OnAvatarSelected(AvatarToonTarget target)
        {
            _current = target;
            // 선택 시점에 인스턴스 미리 준비 → 토글 타이밍에 생성/소멸 없음
            if (target != null) PrepareIfNeeded(target);
        }

        /// <summary>UI 버튼 → 현재 선택된 아바타 셰이더 토글</summary>
        public void ToggleCurrent()
        {
            if (_current == null || toonMaterial == null) return;

            int id = _current.GetInstanceID();
            if (_toonOn.Contains(id)) RestoreOriginal(_current);
            else                      ApplyToon(_current);
        }

        // ── 사전 준비 (선택 시 1회) ───────────────────────────────────

        void PrepareIfNeeded(AvatarToonTarget target)
        {
            int id = target.GetInstanceID();
            if (_instancedMats.ContainsKey(id)) return; // 이미 준비됨

            var origCache = new Material[target.renderers.Length][];
            var instCache = new Material[target.renderers.Length][];

            for (int i = 0; i < target.renderers.Length; i++)
            {
                var r = target.renderers[i];
                if (r == null)
                {
                    origCache[i] = System.Array.Empty<Material>();
                    instCache[i] = System.Array.Empty<Material>();
                    continue;
                }

                origCache[i] = r.sharedMaterials;

                // runtime instance mats: 텍스처 읽기용, 이후 정리
                var runtimeMats = r.materials;
                var newMats     = new Material[origCache[i].Length];

                for (int m = 0; m < origCache[i].Length; m++)
                {
                    var inst = new Material(toonMaterial);
                    var orig = origCache[i][m];

                    if (orig != null)
                    {
                        // 텍스처 탐색: _MainTex(UniUnlit/MToon) → _BaseMap(URP) 순
                        Texture tex = null;
                        if (orig.HasProperty("_MainTex"))  tex = orig.GetTexture("_MainTex");
                        if (tex == null && orig.HasProperty("_BaseMap")) tex = orig.GetTexture("_BaseMap");

                        // sharedMat에 없으면 runtime instance에서 재시도
                        if (tex == null && m < runtimeMats.Length && runtimeMats[m] != null)
                        {
                            var rt = runtimeMats[m];
                            if (rt.HasProperty("_MainTex")) tex = rt.GetTexture("_MainTex");
                            if (tex == null && rt.HasProperty("_BaseMap")) tex = rt.GetTexture("_BaseMap");
                        }

                        if (tex != null)
                        {
                            inst.SetTexture("_BaseMap", tex);
                            string stProp = orig.HasProperty("_MainTex") ? "_MainTex" : "_BaseMap";
                            inst.SetTextureScale ("_BaseMap", orig.GetTextureScale(stProp));
                            inst.SetTextureOffset("_BaseMap", orig.GetTextureOffset(stProp));
                            inst.SetColor("_BaseColor", Color.white);
                        }
                        else if (orig.HasProperty("_Color"))
                        {
                            inst.SetColor("_BaseColor", orig.GetColor("_Color"));
                        }

                        // Shadow Color
                        var shadowCol = new Color(0.20f, 0.22f, 0.38f, 1f);
                        if (inst.HasProperty("_ShadowMapColor")) inst.SetColor("_ShadowMapColor", shadowCol);
                        else if (inst.HasProperty("_ShadowColor")) inst.SetColor("_ShadowColor", shadowCol);
                    }

                    // NiloCat 파라미터
                    if (inst.HasProperty("_CelShadeMidPoint"))
                    {
                        inst.SetFloat("_CelShadeMidPoint",          -0.5f);
                        inst.SetFloat("_CelShadeSoftness",           0.05f);
                        inst.SetFloat("_DirectLightMultiplier",      0.9f);
                        inst.SetFloat("_IndirectLightMultiplier",    1.0f);
                        inst.SetFloat("_ReceiveShadowMappingAmount", 0.65f);
                        inst.SetFloat("_MainLightIgnoreCelShade",    0f);
                        inst.SetFloat("_OutlineWidth",               0f);
                    }
                    // 커스텀 ToonLit 파라미터
                    if (inst.HasProperty("_RampThreshold"))
                    {
                        inst.SetFloat("_RampThreshold",     0.3f);
                        inst.SetFloat("_RampSmooth",        0.05f);
                        inst.SetFloat("_SpecularSize",      0.08f);
                        inst.SetFloat("_SpecularIntensity", 0.4f);
                    }

                    newMats[m] = inst;
                }

                // 텍스처 읽기용 runtime 인스턴스 정리
                foreach (var rm in runtimeMats)
                    if (rm != null) Destroy(rm);

                instCache[i] = newMats;
            }

            _originalMats[id] = origCache;
            _instancedMats[id] = instCache;
        }

        // ── 적용 (assign만, 생성 없음) ───────────────────────────────

        void ApplyToon(AvatarToonTarget target)
        {
            int id = target.GetInstanceID();
            if (!_instancedMats.TryGetValue(id, out var instCache)) return;

            for (int i = 0; i < target.renderers.Length; i++)
            {
                var r = target.renderers[i];
                if (r == null || i >= instCache.Length) continue;
                r.sharedMaterials = instCache[i];
            }
            _toonOn.Add(id);
        }

        // ── 복원 (restore만, Destroy 없음) ───────────────────────────

        void RestoreOriginal(AvatarToonTarget target)
        {
            int id = target.GetInstanceID();
            if (!_originalMats.TryGetValue(id, out var origCache)) return;

            for (int i = 0; i < target.renderers.Length; i++)
            {
                var r = target.renderers[i];
                if (r == null || i >= origCache.Length) continue;
                r.sharedMaterials = origCache[i];
            }
            _toonOn.Remove(id);
        }

        // ── 씬 종료 시 1회 해제 ──────────────────────────────────────

        void OnDestroy()
        {
            foreach (var all in _instancedMats.Values)
                foreach (var mats in all)
                    foreach (var m in mats)
                        if (m != null) Destroy(m);
        }
    }
}
