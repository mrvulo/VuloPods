namespace VuloPods.Core.Logging;

public static class Log
{
    private static readonly object Gate = new();
    private static string? _directory;
    private static long _maxBytes = 1_000_000;

    public static string? FilePath { get; private set; }

    public static void Initialize(string directory, long maxBytes = 1_000_000)
    {
        lock (Gate)
        {
            Directory.CreateDirectory(directory);
            _directory = directory;
            _maxBytes = maxBytes;
            FilePath = Path.Combine(directory, "log.txt");
        }
    }

    public static void Shutdown()
    {
        lock (Gate)
        {
            _directory = null;
            FilePath = null;
        }
    }

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VuloPods");

    public static void Info(string message) => Write("INFO", message);
    public static void Debug(string message) => Write("DEBUG", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}: {exception}");

    private static void Write(string level, string message)
    {
        lock (Gate)
        {
            if (FilePath is null || _directory is null) return;
            try
            {
                RotateIfNeeded();
                File.AppendAllText(FilePath,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Logging darf die App nie zum Absturz bringen.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void RotateIfNeeded()
    {
        var info = new FileInfo(FilePath!);
        if (!info.Exists || info.Length < _maxBytes) return;
        File.Move(FilePath!, Path.Combine(_directory!, "log.1.txt"), overwrite: true);
    }
}
