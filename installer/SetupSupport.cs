using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

internal sealed class SetupDiagnostics
{
    internal readonly string LogPath;
    internal readonly List<string> Warnings = new List<string>();
    private readonly bool writeLegacyError;
    internal SetupDiagnostics(string path = null)
    { writeLegacyError = path == null; LogPath = path ?? Path.Combine(Path.GetTempPath(), "QuestBell-setup.log"); }
    internal void Info(string message)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)); File.AppendAllText(LogPath, DateTime.Now.ToString("o") + " " + message + Environment.NewLine); }
        catch (Exception) { /* Logging must not turn a recoverable setup step into a failure. */ }
    }
    internal void Warning(string step, Exception error)
    { Warnings.Add(step); Info("WARNING [" + step + "] " + error); }
    internal void Failure(Exception error)
    {
        Info("FAILURE " + error);
        if (!writeLegacyError) return;
        try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "QuestBell-setup-error.txt"), error.ToString()); } catch (Exception) { }
    }
    internal static Exception Cause(Exception error)
    {
        while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
        var aggregate = error as AggregateException;
        if (aggregate != null && aggregate.InnerExceptions.Count == 1) return Cause(aggregate.InnerExceptions[0]);
        return error;
    }
}

internal sealed class SetupContext
{
    internal string UninstallRegistryPath = Program.UninstallKey;
    internal string StartupRegistryPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    internal string StartupValueName = "QuestBell";
    internal string DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuestBell");
    internal Func<string> StartMenuDirectory = delegate
    {
        string folder = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        if (string.IsNullOrWhiteSpace(folder)) throw new DirectoryNotFoundException("Windows did not provide a Start menu folder.");
        return Path.Combine(folder, "QuestBell");
    };
    internal Func<string> DesktopDirectory = delegate { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); };
    internal Action<string, string, string> CreateShortcut = ShellShortcut.Create;
    internal Func<Stream> OpenPayload { get; set; }
    internal Action<string> BeforeOptionalStep { get; set; }
    internal string TestRegistryRoot;

    internal static SetupContext Isolated(string root)
    {
        root = Path.GetFullPath(root);
        string testBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "QuestBell-install-test")) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(testBase, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Test paths must stay in the isolated setup-test directory.");
        var context = new SetupContext();
        context.TestRegistryRoot = "Software\\QuestBell\\InstallerTests\\" + Guid.NewGuid().ToString("N");
        context.UninstallRegistryPath = context.TestRegistryRoot + "\\Uninstall";
        context.StartupRegistryPath = context.TestRegistryRoot + "\\Startup";
        context.DataDirectory = Path.Combine(root, "data");
        context.StartMenuDirectory = delegate { return Path.Combine(root, "Start Menu - Έναρξη"); };
        context.DesktopDirectory = delegate { return Path.Combine(root, "Desktop redirected - Επιφάνεια"); };
        return context;
    }
    internal void CleanTestRegistry()
    {
        if (TestRegistryRoot != null && TestRegistryRoot.StartsWith("Software\\QuestBell\\InstallerTests\\", StringComparison.Ordinal))
            Registry.CurrentUser.DeleteSubKeyTree(TestRegistryRoot, false);
    }
}

internal static class SetupWorker
{
    internal static Task Run(Action action)
    {
        var completion = new TaskCompletionSource<object>();
        var thread = new Thread(delegate()
        {
            try { action(); completion.SetResult(null); }
            catch (Exception error) { completion.SetException(error); }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
