using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

// 유니티 템플릿 패키지(.tgz) 생성 도구.
// unity CLI의 `templates create`는 Assets·ProjectSettings·Packages(와 Library)만 ProjectData~에 담는다.
// 그래서 저장소 루트의 AGENTS.md·Docs·.gitignore가 새 프로젝트로 옮겨지지 않는다.
// 이 도구는 그 파일들을 ProjectData~에 넣아 다시 싼다. ProjectData~의 내용은 새 프로젝트 루트에 그대로 복사된다.
public static class TemplatePackTool
{
    public const string TEMPLATE_ID = "com.myjg.template.myjg";
    public const string TEMPLATE_DISPLAY_NAME = "MyJG Template";
    public const string TEMPLATE_VERSION = "0.1.0";
    public const string TOOLS_MENU = "Tools/MyJG/Build Template Package";

    // 새 프로젝트 루트로 옮겨야 하는 저장소 루트 항목. 템플릿 골격이 자기 설명서를 잃지 않게 한다.
    static readonly string[] ROOT_ITEMS =
    {
        "AGENTS.md", "README.md", "Docs", ".gitignore", ".gitattributes"
    };

    [MenuItem(TOOLS_MENU)]
    public static void Build()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string cli = FindCommandLine();
        if (string.IsNullOrEmpty(cli))
        {
            Debug.LogError("unity CLI가 없습니다. " + ManualCommands(projectRoot));
            return;
        }

        // CLI가 직접 사용자 템플릿 폴더에 쓰게 하면 Hub/CLI 목록에 바로 등록된다.
        int code = Run(cli, "templates create \"" + projectRoot + "\" --name " + TEMPLATE_ID
            + " --display-name \"" + TEMPLATE_DISPLAY_NAME + "\" --version " + TEMPLATE_VERSION
            + " --keep-project-settings --overwrite --non-interactive", out string output);
        if (code != 0)
        {
            Debug.LogError("unity templates create 실패(" + code + "): " + output);
            return;
        }

        string archive = LastArchivePath(output);
        if (!File.Exists(archive))
        {
            Debug.LogError("템플릿 아카이브 경로를 찾지 못했습니다. CLI 출력: " + output);
            return;
        }

        if (!IncludeRootFiles(archive, out string error))
        {
            Debug.LogError("루트 파일 포함 실패: " + error);
            return;
        }

        Debug.Log("템플릿 준비 완료: " + archive + "\n"
            + "새 프로젝트 만들기: unity projects create <이름> --editor-version " + Application.unityVersion
            + " --template " + TEMPLATE_ID + "\n"
            + "확인: unity templates --editor-version " + Application.unityVersion + " (CUSTOM / ready)");
        EditorUtility.RevealInFinder(archive);
    }

    [MenuItem("Tools/MyJG/Template Info")]
    static void Info()
    {
        Debug.Log("저장소 루트 파일이 새 프로젝트에 전달되는지 확인하는 방법:\n"
            + "  1) " + TOOLS_MENU + " 실행\n"
            + "  2) unity projects create Probe --path <빈 폴더> --editor-version " + Application.unityVersion + " --template " + TEMPLATE_ID + " --no-cloud\n"
            + "  3) 생성된 프로젝트 루트에 AGENTS.md·Docs가 있는지 확인");
    }

    // 아카이브를 풀어 ProjectData~에 루트 항목을 넣은 뒤 다시 싼다.
    static bool IncludeRootFiles(string archive, out string error)
    {
        error = null;
        string work = Path.Combine(Path.GetTempPath(), "myjg-template-pack");
        try
        {
            if (Directory.Exists(work))
            {
                Directory.Delete(work, true);
            }
            Directory.CreateDirectory(work);

            if (Run("tar.exe", "xzf \"" + archive + "\" -C \"" + work + "\"", out string unpack) != 0)
            {
                error = "압축 해제 실패: " + unpack;
                return false;
            }

            string data = Path.Combine(work, "package", "ProjectData~");
            if (!Directory.Exists(data))
            {
                error = "ProjectData~가 없는 아카이브입니다: " + archive;
                return false;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            foreach (string item in ROOT_ITEMS)
            {
                string source = Path.Combine(projectRoot, item);
                if (File.Exists(source))
                {
                    File.Copy(source, Path.Combine(data, item), true);
                    continue;
                }
                if (Directory.Exists(source))
                {
                    CopyDirectory(source, Path.Combine(data, item));
                    continue;
                }
                Debug.LogWarning("템플릿에 넣을 루트 항목이 없습니다: " + item);
            }

            string packed = archive + ".packed";
            File.Delete(packed);
            if (Run("tar.exe", "czf \"" + packed + "\" -C \"" + work + "\" package", out string pack) != 0)
            {
                error = "재포장 실패: " + pack;
                return false;
            }
            File.Delete(archive);
            File.Move(packed, archive);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(work))
                {
                    Directory.Delete(work, true);
                }
            }
            catch (Exception cleanup)
            {
                Debug.LogWarning("임시 폴더 정리 실패(동작에는 영향 없음): " + cleanup.Message);
            }
        }
    }

    static void CopyDirectory(string _source, string _destination)
    {
        Directory.CreateDirectory(_destination);
        foreach (string file in Directory.GetFiles(_source))
        {
            if (file.EndsWith(".meta", StringComparison.Ordinal))
            {
                continue;       // 루트 문서에는 .meta가 없다(에셋 폴더가 아니므로)
            }
            File.Copy(file, Path.Combine(_destination, Path.GetFileName(file)), true);
        }
        foreach (string dir in Directory.GetDirectories(_source))
        {
            CopyDirectory(dir, Path.Combine(_destination, Path.GetFileName(dir)));
        }
    }

    static string FindCommandLine()
    {
        string local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Unity", "bin", "unity.exe");
        if (File.Exists(local))
        {
            return local;
        }
        foreach (string entry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(';'))
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }
            string candidate = Path.Combine(entry.Trim(), "unity.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    // CLI 표준 출력 마지막 줄에 아카이브 경로가 나온다.
    static string LastArchivePath(string _output)
    {
        string[] lines = (_output ?? string.Empty).Replace('\r', '\n').Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            string line = lines[i].Trim().Trim('"');
            if (line.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                return line;
            }
        }
        return string.Empty;
    }

    static string ManualCommands(string _projectRoot)
    {
        return "직접 실행: unity templates create \"" + _projectRoot + "\" --name " + TEMPLATE_ID
            + " --keep-project-settings --overwrite";
    }

    static int Run(string _fileName, string _arguments, out string _output)
    {
        var start = new ProcessStartInfo(_fileName, _arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(start);
        string stdout = process?.StandardOutput.ReadToEnd() ?? string.Empty;
        string stderr = process?.StandardError.ReadToEnd() ?? string.Empty;
        process?.WaitForExit();
        _output = (stdout + stderr).Trim();
        return process == null ? -1 : process.ExitCode;
    }
}
