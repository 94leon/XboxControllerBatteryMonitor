namespace XboxBatteryMonitor.Tests;

using XboxBatteryMonitor.Core;
using Xunit;

public class SettingsStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"xbm-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void MissingFile_ReturnsDefaults()
    {
        Assert.Equal(new AppSettings(), SettingsStore.Load(_path));
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        SettingsStore.Save(_path, new AppSettings(35, 18, false));
        Assert.Equal(new AppSettings(35, 18, false), SettingsStore.Load(_path));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTrayIconStyle()
    {
        SettingsStore.Save(_path, new AppSettings(35, 18, false, TrayIconStyle.BatteryAndPercent));
        Assert.Equal(new AppSettings(35, 18, false, TrayIconStyle.BatteryAndPercent), SettingsStore.Load(_path));
    }

    [Fact]
    public void UndefinedTrayIconStyle_FallsBackToBattery()
    {
        // 旧版本 settings.json 无此字段 → 默认 Battery（MissingFile_ReturnsDefaults 已覆盖）；
        // 手改出越界枚举值 → 消毒回 Battery，不连累其他合法设置
        SettingsStore.Save(_path, new AppSettings(20, 10, true, (TrayIconStyle)99));
        Assert.Equal(new AppSettings(20, 10, true, TrayIconStyle.Battery), SettingsStore.Load(_path));
    }

    [Fact]
    public void CorruptFile_ReturnsDefaults()
    {
        File.WriteAllText(_path, "{{{ 不是 JSON");
        Assert.Equal(new AppSettings(), SettingsStore.Load(_path));
    }

    [Fact]
    public void InvalidValues_ReturnsDefaults()
    {
        File.WriteAllText(_path, """{"WarningThreshold":5,"CriticalThreshold":50,"RumbleEnabled":true}""");
        Assert.Equal(new AppSettings(), SettingsStore.Load(_path));
    }

    [Fact]
    public void Save_RejectsInvalidByWritingDefaults()
    {
        SettingsStore.Save(_path, new AppSettings(10, 90, true));  // 非法
        Assert.Equal(new AppSettings(), SettingsStore.Load(_path));
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
