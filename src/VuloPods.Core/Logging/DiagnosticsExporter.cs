using System.IO.Compression;
using System.Text;

namespace VuloPods.Core.Logging;

/// <summary>Packt Logs, Roh-Pakete und Systeminfo in ein ZIP zum Weitergeben bei Problemen.</summary>
public static class DiagnosticsExporter
{
    public static void Export(string logDirectory, IReadOnlyList<string> rawPackets, string systemInfo, string targetZipPath)
    {
        if (File.Exists(targetZipPath)) File.Delete(targetZipPath);

        using var zip = ZipFile.Open(targetZipPath, ZipArchiveMode.Create);
        AddLogFile(zip, logDirectory, "log.txt");
        AddLogFile(zip, logDirectory, "log.1.txt");
        AddText(zip, "packets.txt", string.Join("\n", rawPackets));
        AddText(zip, "system.txt", systemInfo);
    }

    private static void AddLogFile(ZipArchive zip, string directory, string name)
    {
        var path = Path.Combine(directory, name);
        if (!File.Exists(path)) return;

        var entry = zip.CreateEntry(name);
        using var target = entry.Open();
        // Das Log kann gerade beschrieben werden, daher mit FileShare.ReadWrite öffnen.
        using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        source.CopyTo(target);
    }

    private static void AddText(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}