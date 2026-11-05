# 테이블 규칙

수치의 원본은 `Assets/Resources/Tables`에만 있다. 코드·씬·UI에 사본을 만들지 않는다.

## 4계층

| 계층 | 파일 | 책임 |
|---|---|---|
| 원본 | `Resources/Tables/<DTO명>.json` | 사람이 편집하는숫자값. 파일명은 데이터 클래스명과 일치 |
| DTO | `Runtime/Data/GameTables.cs`, `GameTableRecords.cs` | `[Serializable]` 필드만. 로직·기본값·계산 없음 |
| 로더·조회 | `Runtime/Data/TableManager.cs` | 지연 로드, ID 인덱스, `IsValid`, `RequireValid`, `Reload` |
| 규칙 | `Runtime/Data/GameTableRules.cs` | 항목별 검증, 참조 무결성, 파생 값 계산 |

## 추가 순서
1. JSON 파일을 만들고 `status`, `note`를 첫 필드로 둔다(운영 상태·메모).
2. `GameTables.cs`에 `[Serializable] public sealed class XxxTableData`를 정의하고, 배열 필드는 `Array.Empty<T>()`로 초기화한다.
3. `TableManager`에 프로퍼티와 `Xxx = Load<XxxTableData>(ref loaded);` 한 줄, 필요한 조회 메서드를 더한다.
4. `GameTableRules.Validate`에 목록 non-empty·키 참조·범위 검사를 더한다. 숫자 유한성과 누락 중첩 객체는 `Inspect`가 자동으로 커버한다.
5. 같은 값을 코드·씬에 복사하지 않는다. UI는 해당 테이블을 읽어 표시한다.

## 검증 원칙
- 오류는 항목별로 전부 모은다(첫 오류에서 멈추지 않는다). 메시지는 `테이블 오류 [경로]: 규칙` 형식.
- 잘못된 값으로 조용히 동작하지 않게, 데이터를 쓰는 초기화는 `TableManager.Instance.RequireValid()`로 차단한다.
- 모르는 키 조회는 기본값을 주지 않고 예외(`KeyNotFoundException`)로 막는다. 선택 조회만 `TryGet*`를 쓴다.
- 키는 id·씬 이름·자산 GUID처럼 순서 무관 값. 배열 순서에 의미를 두지 않고, 노출 목록은 결정적으로 정렬한다.

## 편집 도구 동작
- JSON 저장 시 `Editor/Tables/TableEditor.cs`가 `TableManager.Reload()`로 캐시를 무너뜨린다(추가 임포트·씬 수정 없음 → 도메인 리로드 안전).
- `Tools/MyJG/Tables/Reload Tables`는 편집 중 수동 갱신용. 플레이 중에는 실행 중 상태를 재구성하지 못해 막는다.
- 에디터에서는 `Resources.Load` 대신 디스크 원본을 우선 읽어, 저장 직후 값으로 검증한다.

## 현재 테이블
- `GameplayTableData` — `player`(플레이어 수치), `sequence`/`upgrades`(해금 사슬과 `category`).
- `FlowTableData` — `scenes`(씬 진행 경로: `scene` → `nextScene`, `restartScene`).

둘 다 샘플 값이다. 프로젝트를 시작하면 실제 도메인(Gameplay/Enemies/Stage… 같은 묶음)으로 바꾸되 위 4계층 방식은 그대로 유지한다.

## 테이블이 아닌 JSON
`Assets/Resources/LogData.json`은 분석 로그(JSONL)의 운영 설정(`enabled`·`bufferLines`·`retentionDays`)이다. 게임 규칙 수치가 아니라 기록 계통의 스위치라 따로 두며, `TableManager`가 아니라 `TelemetryLog`가 읽는다. 자세한 설명은 `Docs/TELEMETRY.md`.
