namespace AiUsageBar.Core;

public static class StartupShortcut
{
    public const string StartupName = "AI Usage Bar.lnk";

    public static (string Target, string Arguments, string WorkingDirectory) LaunchCommand()
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("実行ファイルのパスが分かりません");
        return (exe, "", Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory);
    }

    public static string ShortcutPath
    {
        get
        {
            var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startup, StartupName);
        }
    }

    public static bool IsEnabled => File.Exists(ShortcutPath);

    public static void SetEnabled(bool enabled)
    {
        var path = ShortcutPath;
        if (!enabled)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        var (target, arguments, workingDirectory) = LaunchCommand();
        var type = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell を作れません");
        dynamic shell = Activator.CreateInstance(type) ?? throw new InvalidOperationException("WScript.Shell を作れません");
        var shortcut = shell.CreateShortCut(path);
        shortcut.Targetpath = target;
        shortcut.Arguments = arguments;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.WindowStyle = 7;
        shortcut.Description = "AI Usage Bar";
        shortcut.Save();
    }
}
