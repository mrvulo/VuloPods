using System.IO.Compression;
using VuloPods.Core.Logging;

namespace VuloPods.Tests;

public class DiagnosticsExporterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "vulopods-diag-" + Guid.NewGuid().ToString("N"));

    public DiagnosticsExporterTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "logs"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string LogDir => Path.Combine(_root, "logs");

    private static string ReadEntry(ZipArchive zip, string name)
    {
        using var reader = new StreamReader(zip.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    [Fact]
    public void Export_ContainsLogPacketsAndSystem()
    {
        File.WriteAllText(Path.Combine(LogDir, "log.txt"), "hello log");
        File.WriteAllText(Path.Combine(LogDir, "log.1.txt"), "old log");
        var target = Path.Combine(_root, "out.zip");

        DiagnosticsExporter.Export(LogDir, new[] { "line1", "line2" }, "Windows 11", target);

        using var zip = ZipFile.OpenRead(target);
        Assert.Equal("hello log", ReadEntry(zip, "log.txt"));
        Assert.Equal("old log", ReadEntry(zip, "log.1.txt"));
        Assert.Equal("line1" + "\n" + "line2", ReadEntry(zip, "packets.txt"));
        Assert.Equal("Windows 11", ReadEntry(zip, "system.txt"));
    }

    [Fact]
    public void Export_SkipsMissingLogFiles()
    {
        File.WriteAllText(Path.Combine(LogDir, "log.txt"), "hello log");
        var target = Path.Combine(_root, "out.zip");

        DiagnosticsExporter.Export(LogDir, Array.Empty<string>(), "sys", target);

        using var zip = ZipFile.OpenRead(target);
        Assert.NotNull(zip.GetEntry("log.txt"));
        Assert.Null(zip.GetEntry("log.1.txt"));
        Assert.NotNull(zip.GetEntry("packets.txt"));
        Assert.NotNull(zip.GetEntry("system.txt"));
    }

    [Fact]
    public void Export_OverwritesExistingTarget()
    {
        File.WriteAllText(Path.Combine(LogDir, "log.txt"), "new");
        var target = Path.Combine(_root, "out.zip");
        File.WriteAllText(target, "not a zip");

        DiagnosticsExporter.Export(LogDir, new[] { "p" }, "sys", target);

        using var zip = ZipFile.OpenRead(target);
        Assert.Equal("new", ReadEntry(zip, "log.txt"));
    }

    [Fact]
    public void Export_ReadsLogWhileItIsOpenForWriting()
    {
        var logPath = Path.Combine(LogDir, "log.txt");
        using var writer = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        writer.Write("live"u8);
        writer.Flush();
        var target = Path.Combine(_root, "out.zip");

        DiagnosticsExporter.Export(LogDir, Array.Empty<string>(), "sys", target);

        using var zip = ZipFile.OpenRead(target);
        Assert.Equal("live", ReadEntry(zip, "log.txt"));
    }
}