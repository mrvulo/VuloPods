using VuloPods.Core.Logging;

namespace VuloPods.Tests;

public class LogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "VuloPodsLogTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Log.Shutdown();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Info_WritesLineWithLevel()
    {
        Log.Initialize(_dir);
        Log.Info("hallo");

        var text = File.ReadAllText(Log.FilePath!);
        Assert.Contains("[INFO] hallo", text);
    }

    [Fact]
    public void Error_IncludesException()
    {
        Log.Initialize(_dir);
        Log.Error("kaputt", new InvalidOperationException("boom"));

        var text = File.ReadAllText(Log.FilePath!);
        Assert.Contains("[ERROR] kaputt", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void RotatesWhenMaxBytesReached()
    {
        Log.Initialize(_dir, maxBytes: 200);
        for (var i = 0; i < 20; i++) Log.Info(new string('x', 40));

        Assert.True(File.Exists(Path.Combine(_dir, "log.1.txt")));
        Assert.True(new FileInfo(Log.FilePath!).Length < 400);
    }

    [Fact]
    public void WithoutInitialize_IsNoop()
    {
        Log.Shutdown();
        Log.Info("nichts");
        Assert.Null(Log.FilePath);
    }
}
