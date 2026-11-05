# MyJGTemplate 메모

Unity 6(URP) 프로젝트 시작 템플릿. 게임 콘텐츠 없고, 검증된 기반 구조만 둔다. 에이전트 작업 지침은 AGENTS.md.

## 현재 구현(템플릿 골격)
- **어셈블리**: asmdef·네임스페이스 없음. 런타임 `Assets/Scripts/Runtime`, 에디터 `Assets/Scripts/Editor`(폴더 규칙). 테스트 코드는 asmdef 없이는 컴파일되지 않으므로 두지 않고, 대신 `SmokeCheck`로 기동 검사를 대신한다.
- **테이블**: `Assets/Resources/Tables/*.json` → DTO → `TableManager.Instance`(지연 로드·`RequireValid`·`Reload`·에디터 디스크 원본 우선 읽기) → `GameTableRules`(인덱스 중복·참조 무결성·리플렉션 숫자 검사).`Tools/MyJG/Tables/Reload Tables`와 JSON 저장 시 자동 캐시 무효화.
- **이벤트 허브**: `GameEvents`(struct 페이로드 4종: Run/Scene/Upgrade/Panel, 구독자 예외 격리, `NextId()` 진단 순번, 씬 진입 이벤트 자동 발행).
- **확장 계약**: `IUpgrade`(Id/Apply/Remove) + `UpgradeManager`(Register/Grant/GrantNext/Capture/Restore, 씬 로컬 상태).
- **씬 로컬 자원**: `SceneServices.For<T>()`(씬 안 호스트 생성) + `ScenePool`(key별 Rent/Return/prewarm, 씬과 함께 사라짐).
- **표기 단일 출처**: `Presentation`(EffectCategory 색·제목, 리치 텍스트 Accent/Dim/Heading, 테이블 description 원본 참조).
- **UI 패널**: `PanelBase`(startOpen으로 시작 상태 명시, OnDisable 정리, `GameEvents.PanelVisibility` 발행).
- **레이어**: `GameLayers`(7 Ground / 8 Obstacle / 9 Player / 10 Enemy / 11 Prop / 12 Weapon / 13 Projectile)와 TagManager.asset 등록 완료.
- **LogKit**: git 패키지(v0.7.0) 설치, `LogKitSetup`이 `LOGKIT_SERVER_URL`/`LOGKIT_UPLOAD_KEY` 환경변수를 기동 시 적용. Debug.Log 계열은 자동 수집·전송.
- **분석 로그**: `TelemetryLog`가 sessionId·sequence·FixedUpdate 시간의 단일 소유자(JSONL은 `persistentDataPath/Telemetry`, LogKit 전송에 자동으로 포함되지 않음). `TelemetryRecorder`가 GameEvents를 구독해 한 곳에서 쓰고, 이벤트 이름·필드 원본은 `TelemetryEvents`/`TelemetryPayload`, 스위치는 `Resources/LogData.json`. 계약과 검증은 `Docs/TELEMETRY.md`.
- **설정**: Active Input Handling = Both(신규 Input System만 사용 권장, Validate Project가 안내), Enter Play Mode Options = 도메인 리로드 해제(정적 초기화 규약 필수).
- **도구**: `Tools/MyJG/Validate Project`(빈 스크립트·테이블·빌드 씬·레이어 동기화·입력 설정·LogData 유무), `Tools/MyJG/Verify Telemetry File`(최신 분석 파일의 session/sequence/revision 계약), `Tools/MyJG/Reveal Telemetry Folder`, `Tools/MyJG/Debug/Jump To Scene`, `Tools/MyJG/Build Windows (Development)`(활성 씬 사용 → `Artifacts/Windows`).
- **기동 검사**: 실행 인자 `-projectSmoke` → 테이블 검증·풀 왕복·해금/스냅샷 복원·이벤트 왕복·분석 로그 세션 계약까지 확인 후 `MYJG_SMOKE_PASS/FAIL`과 종료 코드.

## 프로젝트를 시작할 때 할 일
1. 샘플 데이터를 지운다: `GameplayTableData.json`의 `sample.*` 해금 항목과 예시 필드, `FlowTableData.json`의 SampleScene 경로를 실제 씬 구조로 바꾼다.
2. `GameLayers`와 TagManager 레이어를 실제 게임에 맞게 고치고 `Tools/MyJG/Validate Project`로 확인한다.
3. 메뉴·로그의 `MyJG` 접두사를 프로젝트명으로 바꾼다(에디터 스크립트 내 문자열, `MYJG_SMOKE_*`).
4. `IUpgrade` 구현체를 만들어 `UpgradeManager.Register`로 연결하고, 패널은 `PanelBase`를 상속해 `startOpen`을 명시한다.
5. 분석 서버가 있으면 `Docs/PLAY_LOG.md`의 이벤트 이름을 서버 측과 맞춘다. 분석 파일의 전송·수집은 별도 작업이라 필요하면 직접 붙인다(`Docs/TELEMETRY.md`).

## 검증 · 미정
- 검증: 23개 스크립트를 Unity 6000.3.22f1 어셈블리 참조로 Roslyn 컴파일 확인(에디터 / 플레이어 / 개발 빌드 3경우 모두 오류 없음). Play Mode 실행 확인은 하지 않았다(사용자 담당). LogKit git 패키지는 packages-lock.json에 resolved.
- 분석 파일의 서버 전송·수집기는 없다(JSONL은 LogKit 업로드 대상에 자동으로 포함되지 않음). 파일 생성과 계약 검증(`Verify Telemetry File`)까지 제공한다.
- 씬 진행 상태의 판 단위 인계(장비·체력 이어받기)는 템플릿에 없다. 필요하면 명시적으로 추가한다.
- 오디오·저장·씬 로더(페이드) 공용 서비스는 없다. 게임이 정하면 테이블 4계층 규약으로 수치를 옮긴다.
- 테스트 asmdef가 없어 EditMode/PlayMode Test Runner 항목은 비어 있다. 테스트가 필요해지면 asmdef 예외를 만들지 정한다.
- LogKit 수신 서버와 환경변수는 이 PC에서 확인하지 않았다. 빌드 시 값이 없으면 LogKit 빌드 단계가 실패한다(`LOGKIT_OPTIONAL=1`로 로컬 우회).
