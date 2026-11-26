namespace RobloxLauncher.Core;

/// <summary>Tiny file logger. Does nothing until <see cref="Init"/> is called (so tests stay quiet).</summary>
public static class Log
{
    private const int LogsToKeep = 10;
    private static readonly Lock Gate = new();
    private static string? _file;

    public static string? FilePath => _file;

    public static void Init(string directory)
    {
        Directory.CreateDirectory(directory);
        var old = new DirectoryInfo(directory).GetFiles("*.log")
            .OrderByDescending(f => f.Name)
            .Skip(LogsToKeep - 1);
        foreach (var file in old)
        {
            try { file.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        _file = Path.Combine(directory, $"{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        if (_file is null) return;
        lock (Gate)
        {
            try
            {
                File.AppendAllText(_file, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
            catch (IOException) { }
        }
    }
}
