// Built with the Windows .NET Framework compiler. The app payload includes .NET 10.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("QuestBell Setup")]
[assembly: AssemblyDescription("QuestBell offline per-user Windows installer")]
[assembly: AssemblyVersion("1.5.1.0")]
[assembly: AssemblyFileVersion("1.5.1.0")]

internal static class Program
{
    internal const string AppId = "QuestBell-3FD7B807-48DD-4FC6-9434-D7286E15D63F";
    internal const string UninstallKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\QuestBell";
    internal static readonly string DefaultTarget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "QuestBell");
    internal static bool Greek;
    internal static readonly SetupDiagnostics Diagnostics = new SetupDiagnostics();
    internal static string T(string en, string el) { return Greek ? el : en; }

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            if (args.Length >= 2 && args[0] == "/cleanup")
            {
                string cleanupTarget = Path.GetFullPath(args[1]);
                ValidateInstalled(cleanupTarget);
                if (!cleanupTarget.Equals(DefaultTarget, StringComparison.OrdinalIgnoreCase) &&
                    !cleanupTarget.StartsWith(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "QuestBell-install-test")), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unrecognized uninstall location.");
                for (int attempt = 0; attempt < 40; attempt++)
                { try { Directory.Delete(cleanupTarget, true); return 0; } catch (IOException) { Thread.Sleep(250); } catch (UnauthorizedAccessException) { Thread.Sleep(250); } }
                throw new IOException("Some files remain in " + cleanupTarget);
            }
#if UNINSTALL
            return Uninstall(args);
#else
            if (args.Length >= 2 && args[0] == "/render")
            {
                Greek = args.Contains("/greek");
                using (var form = new SetupForm(DefaultTarget))
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.RenderOnly = true; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000,-30000); form.ShowInTaskbar = false;
                    form.Show(); Application.DoEvents(); form.DrawToBitmap(bitmap, new Rectangle(0,0,bitmap.Width,bitmap.Height)); form.Hide();
                    bitmap.Save(args[1], System.Drawing.Imaging.ImageFormat.Png);
                }
                return 0;
            }
            string target = DefaultTarget;
            foreach (string arg in args) if (arg.StartsWith("/dir=", StringComparison.OrdinalIgnoreCase)) target = Path.GetFullPath(arg.Substring(5));
            if (args.Contains("/silent"))
            { Install(target, false, false, !args.Contains("/no-register"), delegate(int p, string s) { }); return 0; }
            bool update = args.Contains("/update");
            if (update) ValidateInstalled(target);
            Greek = args.Contains("/greek");
            using (var form = new SetupForm(target, update, !args.Contains("/autostart=0"), !args.Contains("/no-register")))
            { Application.Run(form); if (update && !form.Succeeded) return 1; }
            return 0;
#endif
        }
        catch (Exception error)
        {
            Diagnostics.Failure(error);
            if (!args.Contains("/silent")) ShowFailure(null, error);
            return 1;
        }
    }

    internal static void Install(string target, bool start, bool autoStart, bool register, Action<int, string> progress, SetupContext context = null, SetupDiagnostics diagnostics = null)
    {
        context = context ?? new SetupContext(); diagnostics = diagnostics ?? Diagnostics;
        diagnostics.Warnings.Clear();
        diagnostics.Info("Starting QuestBell setup " + Assembly.GetExecutingAssembly().GetName().Version + " | " + Environment.OSVersion + " | 64-bit OS: " + Environment.Is64BitOperatingSystem + " | Thread: " + Thread.CurrentThread.GetApartmentState() + " | Target: " + target);
        if (!Environment.Is64BitOperatingSystem || Environment.OSVersion.Version.Major < 10 || Environment.OSVersion.Version.Build < 19045)
            throw new PlatformNotSupportedException(T("QuestBell requires 64-bit Windows 10 22H2 or Windows 11.", "Το QuestBell απαιτεί Windows 10 22H2 64-bit ή Windows 11."));
        target = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        if (!target.Equals(DefaultTarget, StringComparison.OrdinalIgnoreCase) && !target.StartsWith(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "QuestBell-install-test")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only the per-user location or the isolated test directory is supported.");
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any()) ValidateInstalled(target);
        CloseApp(target);
        string parent = Path.GetDirectoryName(target);
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, ".QuestBell-staging-" + Guid.NewGuid().ToString("N"));
        string previous = target + ".previous-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        bool swapped = false;
        try
        {
            using (Stream payload = context.OpenPayload != null ? context.OpenPayload() : Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip"))
            {
                if (payload == null) throw new InvalidDataException("Installer payload is missing.");
                using (var zip = new ZipArchive(payload, ZipArchiveMode.Read))
                {
                    int count = 0;
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string destination = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                        if (!destination.StartsWith(staging + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe archive path.");
                        if (entry.FullName.EndsWith("/")) { Directory.CreateDirectory(destination); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        using (Stream input = entry.Open()) using (Stream output = File.Create(destination)) input.CopyTo(output);
                        count++; progress(5 + count * 80 / zip.Entries.Count, T("Unpacking a little magic…", "Αποσυσκευασία λίγης μαγείας…"));
                    }
                }
            }
            if (!File.Exists(Path.Combine(staging, "QuestBell.exe")) || !File.Exists(Path.Combine(staging, "QuestBell.Uninstall.exe"))) throw new InvalidDataException("Incomplete installer payload.");
            File.WriteAllText(Path.Combine(staging, "questbell.install"), AppId + Environment.NewLine + (Greek ? "el" : "en"));
            if (register)
            {
                // Prepare required writable app data before replacing an existing app.
                Directory.CreateDirectory(context.DataDirectory);
                string file = Path.Combine(context.DataDirectory, "questbell.json");
                if (!File.Exists(file)) File.WriteAllText(file, "{\"Settings\":{\"Language\":\"" + (Greek ? "el" : "en") + "\",\"AutoStart\":" + (autoStart ? "true" : "false") + "}}");
            }
            if (Directory.Exists(target)) Directory.Move(target, previous);
            Directory.Move(staging, target); swapped = true;
            diagnostics.Info("Application files installed successfully.");
            if (register)
            {
                progress(90, T("Making a home on your desktop…", "Ετοιμασία της νέας σου παρέας…"));
                Register(target, autoStart, context, diagnostics);
            }
            if (Directory.Exists(previous)) Optional(context, diagnostics, "previous-version cleanup", delegate { Directory.Delete(previous, true); });
            progress(100, T("Your next little adventure is ready.", "Η επόμενη μικρή περιπέτεια είναι έτοιμη."));
            diagnostics.Info("Setup complete. Optional warnings: " + diagnostics.Warnings.Count);
            if (start) Optional(context, diagnostics, "launch", delegate { Process.Start(Path.Combine(target, "QuestBell.exe")); });
        }
        catch (Exception error)
        {
            diagnostics.Failure(error);
            if (!swapped && Directory.Exists(previous) && !Directory.Exists(target)) Directory.Move(previous, target);
            throw;
        }
        finally { if (Directory.Exists(staging)) Optional(context, diagnostics, "staging cleanup", delegate { Directory.Delete(staging, true); }); }
    }

    internal static void Optional(SetupContext context, SetupDiagnostics diagnostics, string step, Action action)
    {
        try
        {
            diagnostics.Info("Configuring " + step);
            if (context.BeforeOptionalStep != null) context.BeforeOptionalStep(step);
            action();
        }
        catch (Exception error) { diagnostics.Warning(step, error); }
    }
    private static void Register(string target, bool autoStart, SetupContext context, SetupDiagnostics diagnostics)
    {
        Optional(context, diagnostics, "Windows app registration", delegate
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(context.UninstallRegistryPath))
            {
                key.SetValue("DisplayName", "QuestBell"); key.SetValue("DisplayVersion", "1.5.1"); key.SetValue("Publisher", "QuestBell");
                key.SetValue("InstallLocation", target); key.SetValue("DisplayIcon", Path.Combine(target, "QuestBell.exe"));
                key.SetValue("UninstallString", "\"" + Path.Combine(target, "QuestBell.Uninstall.exe") + "\"");
                key.SetValue("QuietUninstallString", "\"" + Path.Combine(target, "QuestBell.Uninstall.exe") + "\" /silent");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", (int)(Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) / 1024), RegistryValueKind.DWord);
            }
        });
        Optional(context, diagnostics, "Windows startup", delegate
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(context.StartupRegistryPath))
            {
                if (autoStart) key.SetValue(context.StartupValueName, "\"" + Path.Combine(target, "QuestBell.exe") + "\" --background");
                else key.DeleteValue(context.StartupValueName, false);
            }
        });
        Action<string, Func<string>, string, string> link = delegate(string step, Func<string> directory, string name, string destination)
        {
            Optional(context, diagnostics, step, delegate
            {
                string folder = directory();
                if (string.IsNullOrWhiteSpace(folder)) throw new DirectoryNotFoundException("Windows did not provide a usable shortcut folder.");
                Directory.CreateDirectory(folder);
                context.CreateShortcut(Path.Combine(folder, name), destination, "");
            });
        };
        link("Start menu shortcut", context.StartMenuDirectory, "QuestBell.lnk", Path.Combine(target, "QuestBell.exe"));
        link("guide shortcut", context.StartMenuDirectory, "Field guide - Οδηγός.lnk", Path.Combine(target, "Guide", "index.html"));
        link("uninstaller shortcut", context.StartMenuDirectory, "Uninstall QuestBell.lnk", Path.Combine(target, "QuestBell.Uninstall.exe"));
        link("desktop shortcut", context.DesktopDirectory, "QuestBell.lnk", Path.Combine(target, "QuestBell.exe"));
    }
    internal static void ShowFailure(IWin32Window owner, Exception error)
    {
        var cause = SetupDiagnostics.Cause(error);
        MessageBox.Show(owner, T("Setup could not finish.\n\n", "Η εγκατάσταση δεν ολοκληρώθηκε.\n\n") + cause.Message +
            T("\n\nSetup details: ", "\n\nΛεπτομέρειες εγκατάστασης: ") + Diagnostics.LogPath, "QuestBell", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
    private static void ValidateInstalled(string path)
    {
        string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        if (full == Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar) || full.Equals(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(Path.Combine(full, "questbell.install")) || File.ReadLines(Path.Combine(full, "questbell.install")).FirstOrDefault() != AppId)
            throw new InvalidOperationException("This folder is not a QuestBell installation: " + full);
    }
    private static void CloseApp(string target)
    {
        var processes = Process.GetProcessesByName("QuestBell").Where(p => { try { return string.Equals(p.MainModule.FileName, Path.Combine(target, "QuestBell.exe"), StringComparison.OrdinalIgnoreCase); } catch { return false; } }).ToArray();
        if (processes.Length == 0) return;
        try { using (var quit = EventWaitHandle.OpenExisting("Local\\QuestBell-Quit-" + Environment.UserName)) quit.Set(); } catch (WaitHandleCannotBeOpenedException) { }
        foreach (var process in processes) if (!process.WaitForExit(10000)) throw new IOException(T("Please exit QuestBell from its tray menu, then try again.", "Κλείσε το QuestBell από την περιοχή ειδοποιήσεων και δοκίμασε ξανά."));
    }
    private static int Uninstall(string[] args)
    {
        string target = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); ValidateInstalled(target);
        string[] marker = File.ReadAllLines(Path.Combine(target, "questbell.install")); Greek = marker.Length > 1 && marker[1] == "el";
        if (!args.Contains("/silent") && MessageBox.Show(T("Remove QuestBell? Your reminders and backups will be kept.", "Να αφαιρεθεί το QuestBell; Οι υπενθυμίσεις και τα αντίγραφα θα διατηρηθούν."), "QuestBell", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return 0;
        CloseApp(target);
        bool test = args.Contains("/no-register");
        if (!test)
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                if (key != null && ((string)key.GetValue("QuestBell", "")).Contains(Path.Combine(target, "QuestBell.exe"))) key.DeleteValue("QuestBell", false);
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "QuestBell");
            if (Directory.Exists(folder)) foreach (string name in new[] { "QuestBell.lnk", "Field guide - Οδηγός.lnk", "Uninstall QuestBell.lnk" })
            { string shortcut = Path.Combine(folder, name); if (File.Exists(shortcut)) File.Delete(shortcut); }
            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "QuestBell.lnk"); if (File.Exists(desktop)) File.Delete(desktop);
        }
        string temporary = Path.Combine(Path.GetTempPath(), "QuestBell-remove-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(Assembly.GetExecutingAssembly().Location, temporary);
        Process.Start(new ProcessStartInfo(temporary, "/cleanup \"" + target + "\"") { UseShellExecute = false, CreateNoWindow = true });
        return 0;
    }
}

internal sealed class SetupForm : Form
{
    internal bool RenderOnly { get; set; }
    protected override bool ShowWithoutActivation { get { return RenderOnly; } }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int size);
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); int dark = 1; DwmSetWindowAttribute(Handle,20,ref dark,4); }
    private readonly string target;
    private readonly bool updateMode, register;
    internal bool Succeeded { get { return finished; } }
    private Label heading, description, status, features, pathLabel;
    private Button install, en, el;
    private CheckBox run, startup;
    private ProgressBar progress;
    private LinkLabel details;
    private bool busy, finished;
    internal SetupForm(string target, bool updateMode = false, bool autoStart = true, bool register = true)
    {
        this.updateMode = updateMode; this.register = register;
        this.target = target; Text = "QuestBell · Setup / Εγκατάσταση"; ClientSize = new Size(690, 530); FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(13,18,27); ForeColor = Color.FromArgb(238,244,248); Font = new Font("Segoe UI", 10);
        using (Stream icon = Assembly.GetExecutingAssembly().GetManifestResourceStream("questbell.ico")) if (icon != null) Icon = new Icon(icon);
        var brand = new Label { Text = "✦  QuestBell", Font = new Font("Segoe UI", 25, FontStyle.Bold), ForeColor = Color.FromArgb(118,237,200), Location = new Point(35,25), Size = new Size(400,55) }; Controls.Add(brand);
        en = MakeButton("EN", new Point(540, 35), new Size(48,30)); el = MakeButton("ΕΛ", new Point(593,35), new Size(48,30));
        en.Click += delegate { Program.Greek = false; Translate(); }; el.Click += delegate { Program.Greek = true; Translate(); };
        heading = LabelAt(new Point(35,100), new Size(465,80), 23, true); description = LabelAt(new Point(35,187), new Size(450,55), 11, false);
        features = LabelAt(new Point(35,251), new Size(620,35), 10, false); features.ForeColor = Color.FromArgb(189,170,255);
        using (Stream art = Assembly.GetExecutingAssembly().GetManifestResourceStream("personal.png"))
        if (art != null)
        {
            using (var atlas = new Bitmap(art))
            {
                var sprite = atlas.Clone(new Rectangle(0,0,atlas.Width/2,atlas.Height/2), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var illustration = new PictureBox { Image = sprite, Location = new Point(505,98), Size = new Size(155,155), SizeMode = PictureBoxSizeMode.Zoom };
                Controls.Add(illustration); Disposed += delegate { sprite.Dispose(); };
            }
        }
        pathLabel = LabelAt(new Point(35,295), new Size(620,40), 9, false); pathLabel.ForeColor = Color.FromArgb(146,163,184);
        run = new CheckBox { Checked = true, Location = new Point(35,350), Size = new Size(610,25), ForeColor = ForeColor, FlatStyle = FlatStyle.Flat }; Controls.Add(run);
        startup = new CheckBox { Checked = true, Location = new Point(35,379), Size = new Size(610,25), ForeColor = ForeColor, FlatStyle = FlatStyle.Flat }; Controls.Add(startup);
        if (updateMode) { startup.Checked = autoStart; run.Enabled = startup.Enabled = false; }
        progress = new ProgressBar { Location = new Point(35,422), Size = new Size(620,6), Style = ProgressBarStyle.Continuous }; Controls.Add(progress);
        status = LabelAt(new Point(35,449), new Size(400,35), 9, false); status.ForeColor = Color.FromArgb(146,163,184);
        details = new LinkLabel { Location = new Point(35,490), Size = new Size(400,22), LinkColor = Color.FromArgb(118,237,200), ActiveLinkColor = Color.FromArgb(189,170,255), VisitedLinkColor = Color.FromArgb(118,237,200), Visible = false };
        details.LinkClicked += delegate { ShowLog(); }; Controls.Add(details);
        install = MakeButton("", new Point(470,451), new Size(185,46)); install.BackColor = Color.FromArgb(118,237,200); install.ForeColor = Color.FromArgb(13,45,35);
        install.Font = new Font("Segoe UI", 11, FontStyle.Bold); install.Click += async delegate { await BeginInstall(); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) e.Cancel = true; };
        Translate();
        if (updateMode) Shown += async delegate { if (!RenderOnly) await BeginInstall(); };
    }
    private Label LabelAt(Point p, Size size, int points, bool bold)
    { var label = new Label { Location = p, Size = size, Font = new Font("Segoe UI", points, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = ForeColor }; Controls.Add(label); return label; }
    private Button MakeButton(string text, Point p, Size size)
    { var b = new Button { Text = text, Location = p, Size = size, BackColor = Color.FromArgb(32,45,64), ForeColor = ForeColor, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = Color.FromArgb(58,76,100); Controls.Add(b); return b; }
    private void Translate()
    {
        heading.Text = Program.T("Small quests. Lighter days.", "Μικρές αποστολές.\nΠιο ανάλαφρες μέρες.");
        description.Text = Program.T("A little magic for bills, subscriptions and everything you mean to remember.", "Λίγη μαγεία για λογαριασμούς, συνδρομές και όσα θέλεις να θυμάσαι.");
        features.Text = Program.T("100 pixel-art illustrations   ·   English + Greek   ·   Offline reminders", "100 εικόνες pixel art   ·   Ελληνικά + Αγγλικά   ·   Υπενθυμίσεις εκτός σύνδεσης");
        if (updateMode) { heading.Text = Program.T("A little upgrade is on its way.", "Έρχεται μια μικρή αναβάθμιση."); description.Text = Program.T("Updating QuestBell. Your reminders and settings stay with you.", "Ενημέρωση QuestBell. Οι υπενθυμίσεις και οι ρυθμίσεις διατηρούνται."); }
        pathLabel.Text = Program.T("Installs for your Windows user. No administrator required.\n", "Εγκατάσταση για τον χρήστη σου. Δεν απαιτείται διαχειριστής.\n") + target;
        run.Text = Program.T("Open QuestBell when setup finishes", "Άνοιγμα QuestBell μετά την εγκατάσταση"); startup.Text = Program.T("Start quietly with Windows", "Διακριτική εκκίνηση με τα Windows");
        install.Text = finished ? Program.T("All set! Close", "Έτοιμο! Κλείσιμο") : Program.T("Install QuestBell  ✦", "Εγκατάσταση  ✦");
        details.Text = Program.T("View setup details ↗", "Λεπτομέρειες εγκατάστασης ↗");
        if (!busy) status.Text = finished ? (Program.Diagnostics.Warnings.Count > 0 ? Program.T("Installed. See setup notes below.", "Εγκαταστάθηκε. Δες τις σημειώσεις παρακάτω.") : Program.T("Your next little adventure is ready.", "Η επόμενη μικρή περιπέτεια είναι έτοιμη.")) : Program.T("Includes everything needed. No extra downloads.", "Περιλαμβάνει ό,τι χρειάζεται. Χωρίς άλλες λήψεις.");
    }
    private async Task BeginInstall()
    {
        if (finished) { Close(); return; } if (busy) return;
        bool launch = run.Checked && register, autoStart = startup.Checked; busy = true; install.Enabled = en.Enabled = el.Enabled = run.Enabled = startup.Enabled = false;
        try
        {
            await SetupWorker.Run(delegate { Program.Install(target, false, autoStart, register, delegate(int percent, string text) { BeginInvoke((Action)delegate { progress.Value = Math.Min(100,percent); status.Text = text; }); }); });
            finished = true; busy = false; Translate(); install.Enabled = true;
            if (launch) Program.Optional(new SetupContext(), Program.Diagnostics, "launch", delegate { Process.Start(Path.Combine(target, "QuestBell.exe")); });
            details.Visible = Program.Diagnostics.Warnings.Count > 0;
            Translate();
            if (updateMode && Program.Diagnostics.Warnings.Count == 0) Close();
        }
        catch (Exception error)
        {
            Program.Diagnostics.Failure(error);
            busy = false; install.Enabled = en.Enabled = el.Enabled = run.Enabled = startup.Enabled = true;
            if (updateMode) run.Enabled = startup.Enabled = false;
            status.Text = Program.T("Setup stopped. See details below.", "Η εγκατάσταση σταμάτησε. Δες τις λεπτομέρειες.");
            details.Visible = true; Program.ShowFailure(this, error);
        }
    }
    private void ShowLog()
    {
        using (var dialog = new Form { Text = Program.T("QuestBell · Setup details", "QuestBell · Λεπτομέρειες εγκατάστασης"), Size = new Size(840,600), StartPosition = FormStartPosition.CenterParent, BackColor = BackColor, ForeColor = ForeColor, Icon = Icon })
        {
            var text = new TextBox { Multiline = true, ReadOnly = true, WordWrap = false, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, BackColor = Color.FromArgb(13,18,27), ForeColor = Color.FromArgb(238,244,248), Font = new Font("Consolas",10) };
            try { text.Text = File.ReadAllText(Program.Diagnostics.LogPath); }
            catch (Exception error) { text.Text = Program.Diagnostics.LogPath + Environment.NewLine + error.Message; }
            dialog.Controls.Add(text); dialog.ShowDialog(this);
        }
    }
}
