using System;
using System.IO;

namespace TrussModelBuilder.Utils
{
  /// <summary>
  /// 프로그램 전반의 실행 상태와 오류를 파일로 기록하는 정적 로그 유틸리티 클래스입니다.
  /// </summary>
  public static class Logger
  {
    private static string logFilePath;

    // 🔹 디버그 로그 출력 여부를 결정하는 플래그
    public static bool IsDebugMode { get; set; } = false;

    public static void Initialize(string logDirectory)
    {
      if (!Directory.Exists(logDirectory))
      {
        Directory.CreateDirectory(logDirectory);
      }

      string timestamp = DateTime.Now.ToString("yyyyMMdd");
      logFilePath = Path.Combine(logDirectory, $"SystemLog_{timestamp}.txt");

      LogInfo("로거가 초기화되었습니다. 프로그램 실행을 시작합니다.");
    }

    public static void LogInfo(string message)
    {
      WriteLog("INFO ", message);
    }

    /// <summary>
    /// 상세한 디버그용 메시지를 기록합니다. IsDebugMode가 true일 때만 작동합니다.
    /// </summary>
    public static void LogDebug(string message)
    {
      if (IsDebugMode)
      {
        WriteLog("DEBUG", message);
      }
    }

    public static void LogError(string message, Exception ex = null)
    {
      string errorMessage = ex != null ? $"{message} | Exception: {ex.Message}\nStackTrace: {ex.StackTrace}" : message;
      WriteLog("ERROR", errorMessage);
    }

    private static void WriteLog(string level, string message)
    {
      if (string.IsNullOrEmpty(logFilePath)) return;

      try
      {
        string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        File.AppendAllText(logFilePath, logEntry + Environment.NewLine);
        Console.WriteLine(logEntry);
      }
      catch { }
    }
  }
}
