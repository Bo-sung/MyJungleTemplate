using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// 게임 분석 로그의 단일 기록 경로. LogKit의 콘솔·오류 수집과 목적이 다르며, 같은 사건을 두 경로에 다시 기록하지 않는다.
// 규칙:
//   1) 한 세션의 sessionId·sequence·게임 시간 시계는 이 파일의 한 소유자만 만든다. 개별 시스템이 자체 runId·Time.time 시계를 만들지 않는다.
//   2) 파일은 Application.persistentDataPath/Telemetry/{sessionId}.jsonl. LogKit 서버 전송에 자동으로 포함되지 않는다(서버 수집기는 별도 작업).
//   3) 같은 summaryId에서는 revision이 가장 큰 레코드만 채택한다. 요약은 WriteSummary로 쓰고 revision은 여기서 증가시킨다.
//   4) session_end 이후에는 기록하지 않는다. 종료는 한 곳에서만 처리해 이중 종료·이중 집계를 막는다.
// 기록은 메인 스레드에서 줄 단위로 바로 쓴다. 프레임에 민감한 고빈도 사건은 개별 이벤트로 남기지 말고 요약으로 집계한다.
public static class TelemetryLog
{
    public const string FOLDER_NAME = "Telemetry";
    public const string SESSION_START_EVENT = "session_start";
    public const string SESSION_END_EVENT = "session_end";
    public const string SUMMARY_EVENT = "summary";

    const string RESOURCE_NAME = "LogData";
    const string FILE_EXTENSION = ".jsonl";

    // 값 상수는 UPPER_SNAKE, 재대입이 있는 정적 상태는 camelCase(작업지침 코딩 스타일).
    static Session session;
    static bool warnedUnavailable;

    public static bool IsOpen => session != null && !session.closed;
    public static string SessionId => session == null ? string.Empty : session.id;
    public static long Sequence => session == null ? 0L : session.sequence;
    // FixedUpdate로 쌓는 게임 시간. 일시정지·선택 화면에서 멈추는 기준을 이 값 하나로 통일한다.
    public static double GameTime => session == null ? 0.0 : session.gameTime;
    public static string FilePath => session == null ? string.Empty : session.path;
    public static string FolderPath => Path.Combine(Application.persistentDataPath, FOLDER_NAME);

    // 기동 시 한 번 열린다. 이미 열려 있으면 아무것도 하지 않는다(재호출 안전).
    public static void Open(string _reason)
    {
        if (IsOpen)
        {
            return;
        }
        LogData settings = LoadSettings();
        if (settings == null)
        {
            return;
        }
        if (!settings.enabled)
        {
            Debug.Log("분석 로그 비활성(LogData.enabled=false). 콘솔·오류 수집은 LogKit이 계속한다.");
            return;
        }

        var next = new Session();
        next.id = NewSessionId();
        next.bufferLines = settings.bufferLines < 1 ? 1 : settings.bufferLines;
        try
        {
            Directory.CreateDirectory(FolderPath);
            Prune(settings.retentionDays);
            next.path = Path.Combine(FolderPath, next.id + FILE_EXTENSION);
            next.writer = new StreamWriter(next.path, false, new UTF8Encoding(false));
        }
        catch (Exception error)
        {
            next.writer?.Dispose();
            Debug.LogError($"분석 로그를 열 수 없습니다({next.path}): {error.Message}");
            return;
        }

        session = next;
        Append(SESSION_START_EVENT, string.Empty, 0, TelemetryPayload.Session("app_start"));
    }

    // 이벤트 한 줄. payload는 [Serializable] 자료형만 직렬화된다(익명 타입은 JsonUtility가 처리하지 못한다).
    public static bool Write(string _eventName, object _payload = null)
    {
        if (!IsOpen)
        {
            WarnUnavailableOnce();
            return false;
        }
        return Append(_eventName, string.Empty, 0, _payload);
    }

    // 요약 기록. summaryId마다 revision을 1씩 올린다. 분석 쪽은 같은 summaryId의 가장 큰 revision만 남기면 된다.
    public static bool WriteSummary(string _summaryId, object _payload)
    {
        if (!IsOpen)
        {
            WarnUnavailableOnce();
            return false;
        }
        if (string.IsNullOrEmpty(_summaryId))
        {
            Debug.LogError("요약 기록에는 summaryId가 필요합니다.");
            return false;
        }
        session.revisions.TryGetValue(_summaryId, out int revision);
        revision++;
        session.revisions[_summaryId] = revision;
        return Append(SUMMARY_EVENT, _summaryId, revision, _payload);
    }

    // 세션을 닫는다. 여러 곳에서 불러도 종료 기록은 한 번만 남는다.
    public static void Close(string _reason)
    {
        Session current = session;
        if (current == null || current.closed)
        {
            return;
        }
        // 종료 표시를 먼저 세워 이후 쓰기가 이 세션에 들어오지 못하게 한다.
        current.closed = true;
        try
        {
            Append(SESSION_END_EVENT, string.Empty, 0, TelemetryPayload.Session(_reason));
        }
        finally
        {
            Flush();
            current.writer.Dispose();
        }
    }

    public static void Flush()
    {
        session?.writer?.Flush();
    }

    static bool Append(string _eventName, string _summaryId, int _revision, object _payload)
    {
        var line = new TelemetryLine
        {
            sessionId = session.id,
            sequence = ++session.sequence,
            time = session.gameTime,
            timestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            eventName = _eventName,
            summaryId = _summaryId,
            revision = _revision,
            dataJson = Serialize(_payload)
        };
        try
        {
            session.writer.WriteLine(JsonUtility.ToJson(line));
            if (++session.buffered >= session.bufferLines)
            {
                Flush();
                session.buffered = 0;
            }
        }
        catch (Exception error)
        {
            Debug.LogError($"분석 로그 쓰기 실패({_eventName}): {error.Message}");
            return false;
        }
        return true;
    }

    static string Serialize(object _payload)
    {
        if (_payload == null)
        {
            return string.Empty;
        }
        if (_payload is string json)
        {
            return json;
        }
        try
        {
            return JsonUtility.ToJson(_payload);
        }
        catch (Exception error)
        {
            Debug.LogError($"분석 payload 직렬화 실패({error.Message}). [Serializable] 자료형만 지원합니다: {_payload.GetType().Name}");
            return string.Empty;
        }
    }

    // sessionId는 파일 이름에 쓰인다. 재시작이면 새 id를 쓰므로 판 경계가 파일로 드러난다.
    static string NewSessionId()
    {
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string tail = Guid.NewGuid().ToString("N").Substring(0, 8);
        return stamp + "-" + tail;
    }

    static LogData LoadSettings()
    {
        var asset = Resources.Load<TextAsset>(RESOURCE_NAME);
        if (asset == null)
        {
            WarnUnavailableOnce($"분석 로그 설정 누락: Resources/{RESOURCE_NAME}.json");
            return null;
        }
        try
        {
            return JsonUtility.FromJson<LogData>(asset.text) ?? new LogData();
        }
        catch (Exception error)
        {
            Debug.LogError($"분석 로그 설정 읽기 실패: {error.Message}");
            return null;
        }
    }

    // 전송·정리 자동화가 없는 경로이므로 기동에 오래된 파일을 정리한다(0 이하이면 건너뜀).
    static void Prune(int _retentionDays)
    {
        if (_retentionDays <= 0)
        {
            return;
        }
        DateTime cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
        foreach (string path in Directory.GetFiles(FolderPath, "*" + FILE_EXTENSION))
        {
            if (File.GetLastWriteTimeUtc(path) >= cutoff)
            {
                continue;
            }
            try
            {
                File.Delete(path);
            }
            catch (Exception error)
            {
                Debug.LogWarning($"오래된 분석 로그 정리 실패({Path.GetFileName(path)}): {error.Message}");
            }
        }
    }

    static void WarnUnavailableOnce(string _message = null)
    {
        if (warnedUnavailable)
        {
            return;
        }
        warnedUnavailable = true;
        Debug.LogWarning(_message ?? "분석 로그 세션이 없어 기록을 건너뜁니다. session_start 이후 종료 전까지만 기록합니다.");
    }

    // LogKitSetup의 종료 훅에 붙어 큐가 닫히기 전에 판 종료 기록을 남긴다.
    // 씬이 열리기 전에 세션을 열어 GameEvents의 씬 진입 이벤트(AfterSceneLoad)부터 놓치지 않는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var host = new GameObject("Telemetry session");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<TelemetryDriver>();
        LogKitSetup.BeforeShutdown += CloseOnShutdown;
        Open("app_start");
    }

    static void CloseOnShutdown()
    {
        Close("shutdown");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        LogKitSetup.BeforeShutdown -= CloseOnShutdown;
        if (session != null && !session.closed)
        {
            // 도메인 리로드 없이 재진입할 때 이전 판의 파일 핸들을 남기지 않는다.
            session.closed = true;
            session.writer?.Dispose();
        }
        session = null;
        warnedUnavailable = false;
    }

    // 고정 시간 시계의 소유자. 분석에 쓰는 시간은 전부 여기서 나온다.
    sealed class TelemetryDriver : MonoBehaviour
    {
        void FixedUpdate()
        {
            if (session != null && !session.closed)
            {
                session.gameTime += Time.fixedDeltaTime;
            }
        }

        void OnApplicationQuit()
        {
            Close("application_quit");
        }
    }

    sealed class Session
    {
        public string id;
        public string path;
        public long sequence;
        public double gameTime;
        public StreamWriter writer;
        public int buffered;
        public int bufferLines = 1;
        public bool closed;
        public readonly Dictionary<string, int> revisions = new Dictionary<string, int>(StringComparer.Ordinal);
    }
}
