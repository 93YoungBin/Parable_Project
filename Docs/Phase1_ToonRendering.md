# Phase 1 — URP Toon Rendering Pipeline

> 패러블엔터테인먼트 기술 인터뷰 기반 URP 커스텀 렌더 패스 실증 구현.
> 인터뷰에서 언급된 "ScriptableRendererFeature로 커스텀 렌더 패스 삽입" 패턴을
> 레이어 기반 오버라이드 구조로 재해석하여 구현한 기록.

---

## 1. 목표와 설계 기준

### 1.1 패러블 인터뷰 인용 핵심

> "URP ScriptableRendererFeature로 커스텀 렌더 패스를 삽입하고,
>  ShadowMap·Cascade를 직접 제어해 시네마틱 렌더링을 구성"

위 문장에서 추출한 설계 요건:

| 요건 | 의미 |
|---|---|
| **ScriptableRendererFeature 진입** | URP 내부 파이프라인에 Hook, 머티리얼/씬 파일 수정 없이 효과 주입 |
| **레이어 기반 구분** | 오브젝트 속성(레이어)으로 렌더링 경로 분기 (머티리얼 교체 아님) |
| **ShadowMap·Cascade 제어** | URP 기본 설정을 런타임 스냅샷/오버라이드/복원 패턴으로 다룸 |

### 1.2 비기능 요건 (스스로 추가한 품질 기준)

- **GC allocation 0**: 렌더 프레임 중 메모리 할당 금지
- **SRP Batcher 호환**: 모든 셰이더 `CBUFFER_START(UnityPerMaterial)` 유지
- **Early-out**: 조건 미충족 시 DrawCall 발생 금지
- **복구 가능성**: 런타임에 변경한 URP 설정은 Frame 종료 시 원상 복구

---

## 2. 구조 개요

```
URP Frame
│
├─ [RenderFeature] ToonRendererFeature
│   ├─ Pass 1: ToonGlobalParamsPass
│   │        └ Shader.SetGlobalFloat / SetGlobalColor (Ramp·Outline·Shadow)
│   │
│   └─ Pass 2: ToonOutlinePass          ← "레이어 기반 Override"의 핵심
│            ├ FilteringSettings(layerMask = Toon)
│            ├ CreateDrawingSettings(ShaderTags["UniversalForward",...])
│            ├ overrideMaterial = M_ToonOutlineOverride
│            └ DrawRenderers(cullResults, ...)
│
├─ [RenderFeature] ToonShadowControlFeature
│   └ Reflection으로 UniversalRenderPipelineAsset.m_Cascade4Split 스냅샷→오버라이드→복원
│
└─ [RenderFeature] ToonPostProcessFeature
    └ VolumeManager.instance.stack.GetComponent<Bloom>().threshold.Override(...)
```

---

## 3. 레이어 기반 Outline — 핵심 패턴

### 3.1 이전 방식의 문제

최초 구현은 `ToonLit.shader` 안에 4개 패스(ToonLit/Outline/ShadowCaster/DepthOnly)를
모두 포함시키고, 머티리얼을 오브젝트에 할당하는 방식이었다.

문제점:
- 오브젝트가 PBR 룩을 유지하면서 툰 아웃라인만 얹을 수 없음
- 런타임 토글이 `material.SetFloat("_OutlineWidth", 0)` 같은 미봉책
- 셰이더에 책임이 몰림 (Ramp + Outline 한 파일)
- URP의 `DrawRenderers + overrideMaterial` 설계 의도를 쓰지 못함

### 3.2 레이어 기반으로의 전환

> **"오브젝트의 레이어를 바꾸면 렌더링 경로가 바뀐다"**

| 단계 | 동작 |
|---|---|
| 1 | 오브젝트 `gameObject.layer = Toon` 로 설정 |
| 2 | `ToonOutlinePass`가 `CullResults`에서 `Toon` 레이어만 추출 |
| 3 | 그 렌더러들의 원본 머티리얼을 **무시**하고 `M_ToonOutlineOverride`로 재드로잉 |
| 4 | Inverted Hull (Cull Front) 으로 두꺼운 외곽선 생성 |

셰이더는 두 개로 분리:
- `Parable/ToonLit.shader` — 순수 Ramp/Shadow/Depth (3 pass)
- `Parable/ToonOutlineReplace.shader` — Outline 전용 (1 pass, override용)

### 3.3 구현 요점

```csharp
// ToonOutlinePass — 핵심 로직
static readonly List<ShaderTagId> s_ShaderTags = new List<ShaderTagId>
{
    new ShaderTagId("UniversalForward"),
    new ShaderTagId("SRPDefaultUnlit"),
    new ShaderTagId("LightweightForward"),
};
static readonly ProfilingSampler s_Sampler = new ProfilingSampler("Toon Outline");

public override void Execute(ScriptableRenderContext context, ref RenderingData data)
{
    _filtering.layerMask = _settings.outlineLayerMask;            // runtime 변경 반영
    if (_settings.outlineOverrideMaterial == null ||
        _settings.outlineLayerMask == 0) return;                  // early-out

    var cmd = CommandBufferPool.Get();
    using (new ProfilingScope(cmd, s_Sampler))
    {
        var draw = CreateDrawingSettings(s_ShaderTags, ref data, SortingCriteria.CommonOpaque);
        draw.overrideMaterial          = _settings.outlineOverrideMaterial;
        draw.overrideMaterialPassIndex = 0;
        context.DrawRenderers(data.cullResults, ref draw, ref _filtering);
    }
    context.ExecuteCommandBuffer(cmd);
    CommandBufferPool.Release(cmd);
}
```

### 3.4 스크린 스페이스 균일 두께 (카메라 거리 불변)

```hlsl
float4 posCS    = TransformObjectToHClip(IN.positionOS.xyz);
float3 normalVS = mul((float3x3)UNITY_MATRIX_IT_MV, IN.normalOS);

// UNITY_MATRIX_P 직접 접근 (URP 14에는 TransformViewToProjection 없음)
float2 normalCS = normalize(float2(
    UNITY_MATRIX_P[0][0] * normalVS.x,
    UNITY_MATRIX_P[1][1] * normalVS.y));

posCS.xy += normalCS * width * posCS.w;  // posCS.w 곱 = 원근 보정
```

---

## 4. Shadow Cascade 런타임 제어

### 4.1 문제: URP가 비공개로 감춰둔 설정

`UniversalRenderPipelineAsset.shadowCascadeCount`, `m_Cascade4Split` 등은
`private` 필드. 에디터 Inspector에서만 조작하도록 설계되어 있다.

### 4.2 해결: Reflection + 스냅샷/복원 패턴

```csharp
static readonly FieldInfo s_Cascade4SplitField =
    typeof(UniversalRenderPipelineAsset)
        .GetField("m_Cascade4Split", BindingFlags.NonPublic | BindingFlags.Instance);

struct ShadowSnapshot
{
    public int    cascadeCount;
    public float  shadowDistance;
    public Vector3 cascade4Split;
    public float  depthBias;
    public float  normalBias;
    public bool   softShadows;
}

void OnBeginCameraRendering(ScriptableRenderContext ctx, Camera cam)
{
    _snapshot = TakeSnapshot(_urp);        // ① Save
    ApplyOverride(_urp, _settings);        // ② Override
}

void OnEndCameraRendering(ScriptableRenderContext ctx, Camera cam)
{
    RestoreSnapshot(_urp, _snapshot);      // ③ Restore
}
```

**중요**: `ScriptableRenderPass.Execute()`는 이미 CullResults가 확정된 이후라
Shadow 설정을 바꿔도 반영되지 않는다. 반드시 `RenderPipelineManager.beginCameraRendering`
타이밍에서 조작해야 유효하다.

---

## 5. Post-Processing 런타임 연동

### 5.1 문제: `volume.profile` 직접 수정은 에셋을 영구 변경

```csharp
// ❌ 이렇게 하면 ProjectSettings의 Volume Profile이 영구 변경됨
volume.profile.Get<Bloom>().threshold.value = 0.5f;
```

### 5.2 해결: `VolumeManager.instance.stack` 사용

```csharp
var stack = VolumeManager.instance.stack;
var bloom = stack.GetComponent<Bloom>();
if (bloom != null)
{
    bloom.threshold.Override(_settings.bloomThreshold);   // 런타임 전용 오버라이드
    bloom.intensity.Override(_settings.bloomIntensity);
}
```

`VolumeManager.stack`은 모든 Volume의 블렌드 결과이자 런타임 스크래치 공간.
여기를 건드리면 프레임 한정으로 적용되고 Volume Profile 에셋은 그대로 유지된다.

---

## 6. 성능 특성

### 6.1 프레임당 GC 할당: 0 byte

모든 동적 구조를 사전 캐싱:
- `ShaderTagId` 배열 → `static readonly List<>`
- `ProfilingSampler` → `static readonly`
- `FilteringSettings` → 필드로 보관, `layerMask`만 갱신 (struct이므로 값 재할당 없음)
- `Material.PropertyToID` → `static readonly int`

### 6.2 Draw Call 증가: 레이어 오브젝트 × 1

- 기본 Opaque Pass: 모든 오브젝트 N회
- `ToonOutlinePass`: Toon 레이어 오브젝트 K회 (K ≤ N)
- 총 N + K drawcall. 인터뷰에서 언급된 "다중 패스" 그대로의 비용.

### 6.3 SRP Batcher 유지

`CBUFFER_START(UnityPerMaterial)` 프로퍼티 순서를 셰이더 전체에서 동일 유지 →
URP 자동 배칭 조건 만족. Stats 창에서 "SRP Batcher: ON" 확인 완료.

---

## 7. 시행착오 기록

| 이슈 | 원인 | 해결 |
|---|---|---|
| `TransformViewToProjection` 미정의 | URP 14에서 제거된 매크로 | `UNITY_MATRIX_P` 직접 접근 |
| ShadowCaster에서 `ApplyShadowBias` 미정의 | `Shadows.hlsl` 의존 → `LerpWhiteTo` 파생 에러 | 커스텀 `GetShadowClipPos()` 작성, `UNITY_NEAR_CLIP_VALUE` 로 clamp |
| Volume 설정이 Play 종료 후에도 남음 | `volume.profile` 직접 수정 | `VolumeManager.stack.Override()` 전환 |
| Shadow Cascade 변경이 반영 안 됨 | `Execute()` 는 Culling 이후 타이밍 | `beginCameraRendering` 콜백으로 이동 |
| Outline 토글 시 머티리얼 일일이 수정 필요 | 쉐이더-내장 방식의 구조적 한계 | **레이어 기반 override로 전면 리팩토링** |

---

## 8. 확장 가능 지점

### 8.1 Ramp 셰이더 레이어별 교체 (미구현)

현재 레이어 override는 Outline만 대상. Ramp Lit까지 레이어로 전환하려면:

- 기본 Opaque Pass에서 `opaqueLayerMask &= ~ToonLayer` 로 Toon 레이어 제외
- `ToonLitReplacePass` 가 Toon 레이어만 `M_ToonLitOverride` 로 재드로잉
- Stencil 기반 전략도 가능하나 URP Renderer 설정 쪽이 단순

### 8.2 기능별 Feature 분리

단일 `ToonRendererFeature` 가 모든 역할을 담당 중. 재사용성을 위해:

- `ToonOutlineFeature` — 아웃라인만
- `ToonRampFeature` — 램프 교체만
- `ToonGlobalFeature` — 글로벌 파라미터만

으로 쪼개면 프로젝트 단위로 필요한 feature만 조합 가능.

### 8.3 Per-Material 두께/색 Variation

현재 `_ToonGlobalOutlineWidth > 0` 분기로 글로벌 vs 머티리얼 중 택일.
머티리얼별 두께를 유지하고 **곱셈 스케일**로 글로벌을 적용하면 더 유연:

```hlsl
float finalWidth = _OutlineWidth * (_ToonGlobalOutlineScale > 0 ? _ToonGlobalOutlineScale : 1.0);
```

---

## 9. 파일 맵

```
Assets/ParablePortfolio/
├─ Runtime/
│  ├─ Rendering/
│  │  ├─ ToonRendererFeature.cs           [RendererFeature 진입점]
│  │  ├─ ToonGlobalParamsPass.cs          [글로벌 셰이더 파라미터]
│  │  ├─ ToonOutlinePass.cs               [레이어 기반 override 드로잉]
│  │  ├─ ToonShadowControlFeature.cs      [Cascade Save/Override/Restore]
│  │  ├─ ToonShadowSettings.cs            [ScriptableObject]
│  │  ├─ ToonPostProcessFeature.cs
│  │  ├─ ToonPostProcessPass.cs
│  │  ├─ ToonPostProcessSettings.cs
│  │  └─ ToonDemoController.cs            [UI 연동, 레이어 토글]
│  └─ Shaders/
│     ├─ ToonLit.shader                   [Ramp + Specular + Shadow + Depth]
│     ├─ ToonOutlineReplace.shader        [Outline 전용 override]
│     └─ Includes/ToonLitInput.hlsl
├─ Settings/
│  ├─ Parable_URPPipelineAsset.asset
│  ├─ Parable_URPRenderer.asset
│  ├─ ToonVolumeProfile.asset
│  └─ M_ToonOutlineOverride.mat           [Feature에 할당되는 override material]
└─ Editor/
   └─ ToonUIPrefabBuilder.cs              [Canvas 자동 생성]
```

---

## 10. 포폴 시연 시나리오

1. **씬 상태**: 큐브/스피어 여러 개, 각기 다른 머티리얼 (PBR + ToonLit 혼재)
2. **Outline ON**: 버튼 클릭 → 오브젝트들의 `layer`가 `Toon`으로 이동 → 외곽선 즉시 나타남
3. **Outline OFF**: 레이어를 원래대로 → 외곽선 사라짐 (머티리얼은 전혀 건드리지 않음)
4. **Outline Width 슬라이더**: Feature의 글로벌 값 조작 → 모든 Toon 레이어 오브젝트에 즉시 반영
5. **Shader Swap (→ ToonLit)**: 머티리얼을 ToonLit으로 교체 → Ramp 룩 변화
6. **Color Cycle**: 팔레트 5종 순환 → Base + Shadow 색 동시 변경

핵심 메시지: "**머티리얼을 건드리지 않고, 레이어와 RendererFeature만으로 렌더링 파이프라인을 제어한다**" — 바로 이 지점이 패러블 인터뷰에서 시사한 구조와 맞닿아 있음.
