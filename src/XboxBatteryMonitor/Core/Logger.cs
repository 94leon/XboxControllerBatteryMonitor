namespace XboxBatteryMonitor.Core;

/// <summary>极简文件日志：%LocalAppData%\XboxBatteryMonitor\log.txt。日志失败静默，不影响主流程。</summary>
public static class Logger
{
    private static readonly object Gate = new();
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XboxBatteryMonitor");
    private static readonly string LogFile = Path.Combine(LogDir, "log.txt");
    private const int MaxLines = 2000;      // 超过 512KB 截断到最近 2000 行，防无限增长
    private const long TrimThreshold = 512 * 1024;

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}\r\n");
                TrimIfNeeded();
            }
            catch
            {
                // 日志写失败时无处可写，静默
            }
        }
    }

    private static void TrimIfNeeded()
    {
        if (!File.Exists(LogFile) || new FileInfo(LogFile).Length < TrimThreshold) return;
        string[] lines = File.ReadAllLines(LogFile);
        File.WriteAllLines(LogFile, lines[^Math.Min(lines.Length, MaxLines)..]);
    }
}
