using VuloPods.Core.Settings;

namespace VuloPods.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "VuloPodsTests-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void MissingFile_YieldsDefaults()
    {
        var store = new SettingsStore(FilePath);

        store.Load();

        Assert.Equal(new AppSettings(), store.Current);
    }

    [Fact]
    public void Roundtrip_PreservesAllFields()
    {
        var settings = new AppSettings
        {
            PopupOnLidOpen = false,
            PopupSeconds = 10,
            AutoPause = false,
            LowBatteryWarning = false,
            LowBatteryThreshold = 35,
            TrayIcon = TrayIconStyle.Logo,
            Theme = ThemeMode.Dark,
        };
        new SettingsStore(FilePath).Save(settings);

        var reloaded = new SettingsStore(FilePath);
        reloaded.Load();

        Assert.Equal(settings, reloaded.Current);
    }

    [Fact]
    public void CorruptFile_YieldsDefaultsAndMovesFileToBad()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");
        var store = new SettingsStore(FilePath);

        store.Load();

        Assert.Equal(new AppSettings(), store.Current);
        Assert.True(File.Exists(FilePath + ".bad"));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void MissingFields_KeepDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{\"AutoPause\": false}");
        var store = new SettingsStore(FilePath);

        store.Load();

        Assert.Equal(new AppSettings { AutoPause = false }, store.Current);
    }

    [Theory]
    [InlineData(99, 15)]
    [InlineData(1, 3)]
    [InlineData(8, 8)]
    public void PopupSeconds_IsClamped(int input, int expected)
    {
        var store = new SettingsStore(FilePath);

        store.Save(new AppSettings { PopupSeconds = input });

        Assert.Equal(expected, store.Current.PopupSeconds);
    }

    [Theory]
    [InlineData(33, 35)]
    [InlineData(7, 10)]
    [InlineData(80, 50)]
    [InlineData(25, 25)]
    public void Threshold_IsClampedAndRoundedToFive(int input, int expected)
    {
        var store = new SettingsStore(FilePath);

        store.Save(new AppSettings { LowBatteryThreshold = input });

        Assert.Equal(expected, store.Current.LowBatteryThreshold);
    }

    [Fact]
    public void Changed_FiresOnceOnChange_NotOnIdenticalSave()
    {
        var store = new SettingsStore(FilePath);
        var fired = new List<AppSettings>();
        store.Changed += fired.Add;

        store.Save(new AppSettings { AutoPause = false });
        store.Save(new AppSettings { AutoPause = false });

        var single = Assert.Single(fired);
        Assert.False(single.AutoPause);
    }

    [Fact]
    public void Save_WritesEnumsAsStrings()
    {
        new SettingsStore(FilePath).Save(new AppSettings { TrayIcon = TrayIconStyle.Percent, AutoPause = false });

        var json = File.ReadAllText(FilePath);

        Assert.Contains("\"Percent\"", json);
    }
}
