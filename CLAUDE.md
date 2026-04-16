# CLAUDE.md — Parable Entertainment 포트폴리오 프로젝트

## 프로젝트 목적
패러블엔터테인먼트 유니티 개발자 채용을 위한 포트폴리오.
유니티스퀘어 인터뷰 기사(https://www.unitysquare.co.kr/madewith/game/view?bidx=1&idx=1095)에서
언급된 핵심 기술 스택을 직접 구현하고 시행착오를 기록하는 것이 목표.

## 현재 환경
- Unity 2022.3.62f1 LTS
- URP 14.0.12 (설치 완료)
- Cinemachine 3.1.6 (설치 완료)
- VRM: 미설치 (아래 참고)
- 프로젝트 경로: C:\Users\Bin\Documents\Parable_Project

## VRM 설치 현황 및 주의사항
Git URL 방식, OpenUPM 방식, 로컬 복사 방식 모두 시도했으나
버전 불일치 및 어셈블리 중복 문제로 반복 실패.

**확인된 원인:**
- UniVRM GitHub 레포 v0.128.2 태그 기준 실제 경로:
  - com.vrmc.gltf  → Assets/UniGLTF
  - com.vrmc.univrm → Assets/VRM  (Assets/UniVRM 아님)
  - com.vrmc.vrm   → Assets/VRM10 (Assets/VRMShaders 별도 없음)
- Git URL ?path=/Assets/UniVRM, ?path=/Assets/VRMShaders 는 이 버전에 존재하지 않음
- PackageCache에 com.vrmc.gltf@b54537678f (v0.128.2) 는 기존에 남아있을 수 있음

**권장 설치 방법 (Claude Code가 시도할 것):**
1. https://github.com/vrm-c/UniVRM/releases/tag/v0.128.2 에서
   UniVRM-0.128.2_7b1b.unitypackage 다운로드 후
   Assets > Import Package > Custom Package 로 임포트 (가장 확실)
2. 또는 C:\Users\Bin\AppData\Local\Temp\univrm_extract\UniVRM-0.128.2\Assets\ 안에
   압축 해제된 소스가 이미 존재함 (UniGLTF, VRM, VRM10 폴더 각각 package.json 있음)
   이 폴더들을 Packages/ 안에 복사할 때 **UniGLTF, VRM, VRM10 세 폴더 모두** 복사해야 함
   (이전 시도에서 이 세 폴더만 복사하고 UniHumanoid 등 서브모듈 누락으로 실패했음)

**현재 manifest.json 상태:** 깨끗함. vrmc 관련 항목 없음. 에러 0개.

## Task 로드맵 (Phase 순서)

### Phase 1 — URP 기반 커스텀 Toon 렌더링 파이프라인 ← 지금 여기
- T-1.1: URP 프로젝트 세팅 및 ScriptableRendererFeature 기반 구조 구축
- T-1.2: Cel-Shading 셰이더 구현 (Outline + Ramp Lighting)
- T-1.3: ShadowMap 커스터마이즈 및 Cascade 구조 제어
- T-1.4: Post-Processing 스택 구성 (Bloom, Color Grading, DOF)

### Phase 2 — 3단계 모션 파이프라인 (VRM 필요)
- T-2.1: Humanoid Rig 표준화 및 Avatar Mask 설계
- T-2.2: 실시간 모션 클린업 (Jitter 감지 + 자동 보정)
- T-2.3: Humanoid 리타겟팅 + 아바타별 스켈레톤 보정
- T-2.4: IK 솔버 통합 및 프레임 라이프사이클 제어 (Update/LateUpdate 분리)
- T-2.5: 파이프라인 단계별 모듈화 및 독립 테스트 환경

### Phase 3 — 다중 카메라 시스템 및 라이팅 연동
- T-3.1: 물리 기반 카메라 파라미터 시스템
- T-3.2: 다중 카메라 전환 및 실시간 디렉팅 도구
- T-3.3: 실제 공연장 조명 연동 시뮬레이션 (DMX/MIDI)
- T-3.4: 실시간 공간 전환 연출 시스템

### Phase 4 — Photon Network 기반 실시간 멀티플레이어 협업
- T-4.1: Photon PUN2/Fusion 세팅 및 역할 기반 룸 관리
- T-4.2: 모션 데이터 네트워크 동기화 (저지연)
- T-4.3: 씬 상태 동기화 및 원격 카메라·라이팅 제어
- T-4.4: 네트워크 지연 보정 및 안정화 알고리즘

### Phase 5 — 통합 및 포트폴리오 정리
- T-5.1: 역할별 통합 Inspector / 운영 UI
- T-5.2: 성능 프로파일링 및 최적화
- T-5.3: 시행착오 기술 문서 작성
- T-5.4: 데모 씬 구성 및 영상 포트폴리오 제작

## 패러블 핵심 기술 레퍼런스 (구현 시 참고)
1. URP ScriptableRendererFeature로 커스텀 렌더 패스 삽입
2. ShadowMap + Cascade 커스터마이즈로 시네마틱 렌더링
3. Humanoid 리그를 중간 포맷으로 활용한 표준화 리타겟팅
4. Update(클린업·리타겟) → LateUpdate(IK) 실행 순서 분리
5. IK 솔버 명시적 호출로 동일 프레임 내 본 데이터 확정
6. Photon Network 기반 물리적 거리 무관 실시간 협업 환경
7. 실제 조명 시스템과 Unity Light 실시간 연동 (조도·색상 일치)
8. 모션 처리, 리타겟팅, 렌더링 각 단계 독립 테스트 가능하도록 모듈화

## 작업 원칙
- 이전 단계가 안정화된 뒤 다음 단계 진행
- 각 Phase는 독립 모듈로 설계 (한 Phase 문제가 다른 Phase에 영향 없도록)
- 시행착오와 의사결정 근거를 문서로 남길 것 (T-5.3 포폴 핵심 차별점)
