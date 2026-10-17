using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

internal static class SetupRegressionTests
{
    private static readonly List<string> Checks = new List<string>();
    private static readonly List<SetupContext> Contexts = new List<SetupContext>();
    private static string root;
    private static void Check(string name, bool value)
    { if (!value) throw new Exception("FAIL " + name); Checks.Add(name); Console.WriteLine("PASS " + name); }

    [STAThread]
    private static int Main()
    {
        root = Path.Combine(Path.GetTempPath(), "QuestBell-install-test", "regression-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            SetupWorker.Run(delegate { Check("Installer worker uses STA for Windows Shell objects", Thread.CurrentThread.GetApartmentState() == ApartmentState.STA); }).GetAwaiter().GetResult();
            string target; SetupDiagnostics log; var context = Scenario("healthy Unicode - Ελληνικά", out target, out log);
            Install(target, context, log);
            Check("Real Windows shortcut creation completes with no warnings", log.Warnings.Count == 0);
            string menu = context.StartMenuDirectory(), desktop = context.DesktopDirectory();
            Check("Start-menu application shortcut points to installed executable", Same(ShellShortcut.ReadTarget(Path.Combine(menu,"QuestBell.lnk")), Path.Combine(target,"QuestBell.exe")));
            Check("Greek guide shortcut points to installed offline guide", Same(ShellShortcut.ReadTarget(Path.Combine(menu,"Field guide - Οδηγός.lnk")),Path.Combine(target,"Guide","index.html")));
            Check("Uninstall shortcut points to installed uninstaller", Same(ShellShortcut.ReadTarget(Path.Combine(menu,"Uninstall QuestBell.lnk")),Path.Combine(target,"QuestBell.Uninstall.exe")));
            Check("Redirected Unicode desktop shortcut is created and readable", Same(ShellShortcut.ReadTarget(Path.Combine(desktop,"QuestBell.lnk")),Path.Combine(target,"QuestBell.exe")));
            using (var key = Registry.CurrentUser.OpenSubKey(context.UninstallRegistryPath)) Check("Windows app registration exercised in isolated registry key", key != null && (string)key.GetValue("DisplayName") == "QuestBell");
            using (var key = Registry.CurrentUser.OpenSubKey(context.StartupRegistryPath)) Check("Startup integration exercised in isolated registry key", key != null && ((string)key.GetValue(context.StartupValueName)).Contains("--background"));
            string data = Path.Combine(context.DataDirectory,"questbell.json"); File.WriteAllText(data,"existing reminders must stay unchanged");
            context.OpenPayload = delegate { return Payload("updated version"); };
            Install(target,context,log);
            Check("Upgrade replaces app files and keeps existing reminder data", File.ReadAllText(Path.Combine(target,"QuestBell.exe")) == "updated version" && File.ReadAllText(data) == "existing reminders must stay unchanged");
            Check("Successful upgrade removes previous-version staging folder", Directory.GetDirectories(Path.GetDirectoryName(target),"QuestBell.previous-*").Length == 0);

            context = Scenario("desktop denied",out target,out log);
            context.BeforeOptionalStep = delegate(string step) { if (step == "desktop shortcut") throw new TargetInvocationException(new UnauthorizedAccessException("Simulated redirected Desktop access denied")); };
            Install(target,context,log);
            Check("Denied desktop shortcut does not fail installation", File.Exists(Path.Combine(target,"QuestBell.exe")) && log.Warnings.Count == 1);
            Check("Other shortcuts survive a desktop-only failure", File.Exists(Path.Combine(context.StartMenuDirectory(),"QuestBell.lnk")));
            Check("Diagnostic log preserves wrapped underlying error", File.ReadAllText(log.LogPath).Contains("Simulated redirected Desktop access denied"));

            context = Scenario("native shortcut save failure",out target,out log);
            Directory.CreateDirectory(context.DesktopDirectory());
            using (var locked = new FileStream(Path.Combine(context.DesktopDirectory(),"QuestBell.lnk"),FileMode.Create,FileAccess.ReadWrite,FileShare.None))
                Install(target,context,log);
            Check("A real Windows Shell shortcut save failure is nonfatal", File.Exists(Path.Combine(target,"QuestBell.exe")) && log.Warnings.Count == 1 && log.Warnings[0] == "desktop shortcut");

            context = Scenario("all shortcuts blocked",out target,out log);
            context.CreateShortcut = delegate { throw new COMException("Simulated shortcut policy restriction",unchecked((int)0x80070005)); };
            Install(target,context,log);
            Check("Policy blocking all shortcuts leaves the app installed", File.Exists(Path.Combine(target,"questbell.install")) && log.Warnings.Count == 4);
            Check("Policy error details include native error code", File.ReadAllText(log.LogPath).Contains("80070005"));

            context = Scenario("desktop unavailable",out target,out log); context.DesktopDirectory = delegate { return ""; };
            Install(target,context,log);
            Check("Missing Desktop special folder is nonfatal", File.Exists(Path.Combine(target,"QuestBell.exe")) && log.Warnings.Count == 1);
            context = Scenario("start menu unavailable",out target,out log); context.StartMenuDirectory = delegate { throw new DirectoryNotFoundException("Simulated unavailable Start menu"); };
            Install(target,context,log);
            Check("Missing Start-menu folder keeps app and desktop shortcut", log.Warnings.Count == 3 && File.Exists(Path.Combine(context.DesktopDirectory(),"QuestBell.lnk")));

            context = Scenario("registry denied",out target,out log);
            context.BeforeOptionalStep = delegate(string step) { if (step == "Windows app registration" || step == "Windows startup") throw new UnauthorizedAccessException("Simulated registry policy restriction"); };
            Install(target,context,log);
            Check("Denied optional registry integration does not fail installation", log.Warnings.Count == 2 && File.Exists(Path.Combine(context.StartMenuDirectory(),"QuestBell.lnk")));

            context = Scenario("mandatory data failure",out target,out log); Existing(target);
            File.WriteAllText(context.DataDirectory,"a file blocks the mandatory data directory");
            bool failed = false; try { Install(target,context,log); } catch (IOException) { failed = true; }
            Check("Mandatory data failure remains a real installation failure", failed);
            Check("Mandatory failure preserves previously installed version", File.ReadAllText(Path.Combine(target,"QuestBell.exe")) == "old version");
            Check("Mandatory failure writes full diagnostic information", File.ReadAllText(log.LogPath).Contains("FAILURE"));

            context = Scenario("corrupt payload",out target,out log); Existing(target);
            context.OpenPayload = delegate { return new MemoryStream(Encoding.UTF8.GetBytes("not a zip")); };
            failed = false; try { Install(target,context,log); } catch (InvalidDataException) { failed = true; }
            Check("Corrupt payload does not replace the previous installation", failed && File.ReadAllText(Path.Combine(target,"QuestBell.exe")) == "old version");

            context = Scenario("archive traversal",out target,out log);
            context.OpenPayload = delegate { return Payload("bad",true); };
            failed = false; try { Install(target,context,log); } catch (InvalidDataException) { failed = true; }
            Check("Unsafe archive paths are rejected before writing outside staging", failed && !File.Exists(Path.Combine(Path.GetDirectoryName(target),"escaped.txt")));

            var cause = new UnauthorizedAccessException("Specific Windows error");
            Check("Error display unwraps generic reflection exception", SetupDiagnostics.Cause(new TargetInvocationException(cause)) == cause);
            Check("Error display unwraps single task exception", SetupDiagnostics.Cause(new AggregateException(new TargetInvocationException(cause))) == cause);
            var safeLog = new SetupDiagnostics(Path.Combine(root,"denied-log-parent","setup.log")); Directory.CreateDirectory(Path.Combine(root,"denied-log-parent"));
            File.WriteAllText(Path.Combine(root,"not-a-directory"),"block"); safeLog = new SetupDiagnostics(Path.Combine(root,"not-a-directory","setup.log"));
            safeLog.Info("This log location is unavailable"); Check("Unavailable logging location does not crash setup", true);

            File.WriteAllText(Path.Combine(Environment.CurrentDirectory,"artifacts","setup-regression-report.txt"), "PASS: " + Checks.Count + " installer checks\n" + string.Join("\n",Checks) + "\nIsolated files: " + root);
            Console.WriteLine("\n" + Checks.Count + " installer checks passed."); return 0;
        }
        catch (Exception error)
        { Console.Error.WriteLine(error); File.WriteAllText(Path.Combine(root,"failure.txt"),error.ToString()); return 1; }
        finally { foreach (var context in Contexts) context.CleanTestRegistry(); }
    }
    private static SetupContext Scenario(string name,out string target,out SetupDiagnostics log)
    {
        string directory = Path.Combine(root,name); Directory.CreateDirectory(directory);
        var context = SetupContext.Isolated(directory); context.OpenPayload = delegate { return Payload("first version"); }; Contexts.Add(context);
        target = Path.Combine(directory,"QuestBell"); log = new SetupDiagnostics(Path.Combine(directory,"setup.log")); return context;
    }
    private static void Install(string target,SetupContext context,SetupDiagnostics log)
    { SetupWorker.Run(delegate { Program.Install(target,false,true,true,delegate { },context,log); }).GetAwaiter().GetResult(); }
    private static Stream Payload(string content,bool unsafePath = false)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream,ZipArchiveMode.Create,true))
        foreach (string name in unsafePath ? new[] { "../escaped.txt" } : new[] { "QuestBell.exe","QuestBell.Uninstall.exe","Guide/index.html" })
        { using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write(content); }
        stream.Position = 0; return stream;
    }
    private static void Existing(string target)
    { Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target,"questbell.install"),Program.AppId); File.WriteAllText(Path.Combine(target,"QuestBell.exe"),"old version"); }
    private static bool Same(string a,string b) { return string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase); }
}
