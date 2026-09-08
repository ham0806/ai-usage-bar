namespace AiUsageBar.Core;

public static class AppLog
{
    private static readonly object Gate = new();
    private const long MaxBytes = 1_000_000;

    public static void Setup()
    {
        Info("AI Usage Bar を起動しました");
    }

    public static void Info(string message) => Write("INFO", message, null);

    public static void Warning(string message) => Write("WARN", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            lock (Gate)
            {
                var path = AppPaths.LogPath;
                RotateIfNeeded(path);
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}";
                if (ex is not null)
                {
                    line += Environment.NewLine + ex;
                }

                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (IOException)
        {
        }
    }

    private static void RotateIfNeeded(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length < MaxBytes)
        {
            return;
        }

        var bak1 = path + ".1";
        var bak2 = path + ".2";
        if (File.Exists(bak2))
        {
            File.Delete(bak2);
        }

        if (File.Exists(bak1))
        {
            File.Move(bak1, bak2);
        }

        File.Move(path, bak1);
    }
}
