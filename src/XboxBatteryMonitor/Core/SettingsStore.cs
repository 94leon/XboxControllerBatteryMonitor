namespace XboxBatteryMonitor.Core;

using System.Text.Json;

/// <summary>设置持久化：JSON 文件，任何读取/解析/校验失败都回退默认值（规格 §8）。</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            AppSettings? parsed = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
            return AppSettings.Sanitized(parsed ?? new AppSettings());
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(string path, AppSettings settings) =>
        File.WriteAllText(path, JsonSerializer.Serialize(AppSettings.Sanitized(settings), Options));
}
