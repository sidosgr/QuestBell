using Microsoft.Win32;
using System.Diagnostics;
using System.Media;
using System.Text.Json;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace QuestBell;

public partial class App : Application
{
    internal static App Instance => (App)Current;
    internal AppData Data = new();
    internal Store Storage = null!;
    internal CloudService Cloud = null!;
    internal UpdateService Updates = null!;
    internal MainWindow? Manager;
    internal bool TestMode, Exiting;
    internal event Action? Changed;
    internal Reminder? CurrentReminder;
    internal bool IsPresenting => presented;
    internal int WaitingBlockers => blockers.Count;
    internal Func<HashSet<IntPtr>> ScanFullscreen = Native.FullscreenWindows;
    private readonly List<ReminderWindow> reminders = [];
    private readonly HashSet<IntPtr> blockers = [];
    private DispatcherTimer timer = null!;
    private Forms.NotifyIcon? tray;
    private Mutex? mutex;
    private EventWaitHandle? activate;
    private EventWaitHandle? exitRequest;
    private bool locked, presented;
    private DateTime nextFollowup;
    private SoundPlayer? sound;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        TestMode = e.Args.Contains("--render-samples") || e.Args.Contains("--smoke-test") || e.Args.Contains("--integration-test") || e.Args.Contains("--idle-test");
        DispatcherUnhandledException += (_, a) =>
        {
            File.AppendAllText(Path.Combine(Storage?.DirectoryPath ?? Path.GetTempPath(), "questbell-error.log"), DateTime.Now + " " + a.Exception + Environment.NewLine);
            MessageBox.Show(L.T("QuestBell encountered an error. Your saved data is kept. Details are in questbell-error.log.", "Παρουσιάστηκε σφάλμα. Τα αποθηκευμένα δεδομένα διατηρούνται. Λεπτομέρειες στο questbell-error.log."), "QuestBell");
            a.Handled = true;
        };
        var argPath = Array.IndexOf(e.Args, "--data-dir");
        string dir = argPath >= 0 && argPath + 1 < e.Args.Length ? e.Args[argPath + 1] :
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuestBell");
        Storage = new Store(dir);
        try { Data = Storage.Load(); }
        catch (Exception error) { MessageBox.Show(error.Message, "QuestBell · Data recovery"); Shutdown(2); return; }
        L.Language = Data.Settings.Language;
        Cloud = new CloudService(this);
        Updates = new UpdateService(this);
        if (!TestMode)
        {
            mutex = new Mutex(true, "Local\\QuestBell-" + Environment.UserName, out bool created);
            activate = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\QuestBell-Open-" + Environment.UserName);
            if (!created) { activate.Set(); Shutdown(); return; }
            ThreadPool.RegisterWaitForSingleObject(activate, (_, _) => Dispatcher.BeginInvoke(ShowManager), null, -1, false);
            exitRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\QuestBell-Quit-" + Environment.UserName);
            ThreadPool.RegisterWaitForSingleObject(exitRequest, (_, _) => Dispatcher.BeginInvoke(ExitApp), null, -1, false);
            SyncStartup();
            BuildTray();
            SystemEvents.SessionSwitch += SessionSwitch;
            SystemEvents.PowerModeChanged += PowerChanged;
        }
        sound = new SoundPlayer(Path.Combine(AppContext.BaseDirectory, "Assets", "chime.wav"));
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; timer.Tick += (_, _) => Tick();
        if (TestMode)
        {
            if (e.Args.Contains("--integration-test")) IntegrationTests.Run(this, e.Args);
            else if (e.Args.Contains("--idle-test")) IntegrationTests.MeasureIdle(this);
            else RenderTests.Run(this, e.Args);
            return;
        }
        timer.Start();
        Updates.Start();
        if (!e.Args.Contains("--background")) ShowManager();
        if (Storage.Recovered) MessageBox.Show(L.T("A damaged data file was preserved and the previous backup was recovered.", "Το προβληματικό αρχείο διατηρήθηκε και ανακτήθηκε το προηγούμενο αντίγραφο."), "QuestBell");
        Tick();
    }

    internal void Save()
    {
        Storage.Save(Data);
        L.Language = Data.Settings.Language;
        Changed?.Invoke();
        BuildTray();
    }
    internal void NotifyCloudChange() { Changed?.Invoke(); BuildTray(); }
    internal void ShowManager()
    {
        if (Manager is null) Manager = new MainWindow();
        Manager.Show(); Manager.WindowState = WindowState.Normal; Manager.Activate();
    }
    internal void Compose(Reminder? reminder = null)
    {
        ShowManager(); var dialog = new ComposerWindow(reminder) { Owner = Manager }; dialog.ShowDialog();
    }
    internal void OpenGuide()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Guide", "index.html");
        Process.Start(new ProcessStartInfo(new Uri(file).AbsoluteUri + "?lang=" + L.Language) { UseShellExecute = true });
    }
    internal void Chime(Reminder reminder)
    {
        if (!Data.Settings.Mute && reminder.Sound) { try { sound?.Play(); } catch (Exception) { } }
    }
    internal void SyncStartup()
    {
        if (TestMode) return;
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
            if (Data.Settings.AutoStart) key.SetValue("QuestBell", "\"" + Environment.ProcessPath + "\" --background");
            else key.DeleteValue("QuestBell", false);
        }
        catch (Exception error) { MessageBox.Show(L.T("Windows startup could not be updated: ", "Δεν ενημερώθηκε η εκκίνηση με τα Windows: ") + error.Message, "QuestBell"); }
    }
    private void SessionSwitch(object sender, SessionSwitchEventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        locked = e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.SessionLogoff;
        if (locked) foreach (var w in reminders) w.Hide();
        else { CloseReminder(); Tick(); }
    });
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    { if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(() => { CloseReminder(); Tick(); }); }

    internal void Tick()
    {
        var now = DateTime.Now;
        timer.Interval = TimeSpan.FromSeconds(CurrentReminder is null ? 10 : 1);
        if (locked) return;
        if (Data.Settings.PausedUntil > now) { CloseReminder(); return; }
        if (CurrentReminder is not null)
        {
            blockers.RemoveWhere(h => !Native.Fullscreen(h, true));
            foreach (var h in ScanFullscreen()) blockers.Add(h);
            if (blockers.Count > 0)
            {
                if (presented) { foreach (var w in reminders) w.SendBehind(); presented = false; }
                return;
            }
            if (!presented) Present();
            if (CurrentReminder.FollowUpMinutes > 0 && now >= nextFollowup)
            { Chime(CurrentReminder); nextFollowup = now.AddMinutes(CurrentReminder.FollowUpMinutes); }
            return;
        }
        var next = Data.Reminders.Where(r => r.Active && r.NextAt <= now).OrderBy(r => r.NextAt).FirstOrDefault();
        if (next is null)
        {
            var first = Data.Reminders.Where(r => r.Active).Select(r => (DateTime?)r.NextAt).Min();
            timer.Interval = TimeSpan.FromSeconds(first.HasValue ? Math.Clamp((first.Value - now).TotalSeconds, 1, 30) : 30);
            return;
        }
        // Scan before changing delivery state so an OS/API error cannot strand a task.
        var detected = ScanFullscreen();
        try
        {
            CurrentReminder = next;
            next.LastArtVariant = Artwork.NextVariant(next);
            if (!next.PinArt) next.ArtVariant = -1;
            Save();
            foreach (var h in detected) blockers.Add(h);
            foreach (var screen in Screens())
            {
                var window = new ReminderWindow(next, false) { TargetScreen = screen };
                reminders.Add(window); window.Show(); window.SendBehind();
            }
            if (blockers.Count == 0) Present();
        }
        catch { CloseReminder(); throw; }
    }
    private IEnumerable<Forms.Screen> Screens()
    {
        if (Data.Settings.Monitor == "all") return Forms.Screen.AllScreens;
        return [Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == Data.Settings.Monitor) ?? Forms.Screen.PrimaryScreen!];
    }
    private void Present()
    {
        if (CurrentReminder is null) return;
        presented = true;
        if (!TestMode) foreach (var w in reminders) w.BringForward();
        nextFollowup = DateTime.Now.AddMinutes(Math.Max(1, CurrentReminder.FollowUpMinutes));
        if (!TestMode) Chime(CurrentReminder);
    }
    internal void CloseReminder()
    {
        foreach (var window in reminders.ToArray()) { window.AllowClose = true; window.Close(); }
        reminders.Clear(); blockers.Clear(); CurrentReminder = null; presented = false;
    }
    internal void Resolve(Reminder task, string action)
    {
        var entry = Data.Resolve(task, action, DateTime.Now);
        if (CurrentReminder?.Id == task.Id) CloseReminder();
        Save(); Manager?.ShowUndo(entry); Tick();
    }
    internal void Snooze(Reminder task, DateTime until)
    {
        task.NextAt = Schedule.Wall(until); task.Snoozed = true; task.FollowUpAt = null; task.Revision++;
        if (CurrentReminder?.Id == task.Id) CloseReminder(); Save(); Tick();
    }
    internal void FollowLink(Reminder task)
    {
        if (!Schedule.SafeUrl(task.Url, out var uri)) return;
        try { Process.Start(new ProcessStartInfo(uri!.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception error) { if (Manager is not null) Ui.Error(Manager, error); return; }
        Snooze(task, DateTime.Now.AddMinutes(Data.Settings.LinkReturnMinutes));
    }
    internal void Preview(Reminder? task = null)
    {
        task ??= new Reminder { Title = L.T("Pay phone bill", "Πλήρωσε λογαριασμό τηλεφώνου"), Category = "bills", ArtVariant = 5, Amount = "€35", NextAt = DateTime.Now, CustomTitle = true };
        var preview = new ReminderWindow(task, true); preview.Show(); preview.BringForward();
    }
    internal void Pause(DateTime? until)
    {
        Data.Settings.PausedUntil = until; if (until.HasValue) CloseReminder(); Save(); Tick();
    }
    private void BuildTray()
    {
        if (TestMode) return;
        if (tray is null)
        {
            tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "questbell.ico")), Visible = true };
            tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowManager);
        }
        var old = tray.ContextMenuStrip;
        var menu = new Forms.ContextMenuStrip { ShowImageMargin = false, BackColor = System.Drawing.Color.FromArgb(23,34,52), ForeColor = System.Drawing.Color.FromArgb(237,243,248), Renderer = new Forms.ToolStripProfessionalRenderer(new TrayPalette()) };
        void Add(string title, Action action) => menu.Items.Add(title, null, (_, _) => Dispatcher.Invoke(action));
        Add(L.T("Open QuestBell", "Άνοιγμα QuestBell"), ShowManager);
        Add(L.T("New reminder…", "Νέα υπενθύμιση…"), () => Compose());
        Add(L.T("Return to reminder", "Επιστροφή στην υπενθύμιση"), () =>
        {
            if (CurrentReminder is not null && Native.FullscreenWindows().Count == 0 && blockers.Count == 0) Present();
            else { var due = Data.Reminders.Where(r => r.Active && r.Snoozed).OrderBy(r => r.NextAt).FirstOrDefault(); if (due is not null) { due.NextAt = DateTime.Now; Save(); Tick(); } }
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        var next = Data.Reminders.Where(r => r.Active).OrderBy(r => r.NextAt).FirstOrDefault();
        menu.Items.Add(next is null ? L.T("No reminders scheduled", "Δεν υπάρχουν υπενθυμίσεις") : L.T("Next: ", "Επόμενη: ") + L.Date(next.NextAt)).Enabled = false;
        Add(L.T("Pause 30 minutes", "Παύση 30 λεπτών"), () => Pause(DateTime.Now.AddMinutes(30)));
        Add(L.T("Pause 1 hour", "Παύση 1 ώρας"), () => Pause(DateTime.Now.AddHours(1)));
        Add(L.T("Pause until tomorrow 09:00", "Παύση έως αύριο στις 09:00"), () => Pause(DateTime.Today.AddDays(1).AddHours(9)));
        if (Data.Settings.PausedUntil > DateTime.Now) Add(L.T("Resume reminders", "Συνέχιση υπενθυμίσεων"), () => Pause(null));
        Add(L.T("User guide / Οδηγός", "Οδηγός / User guide"), OpenGuide);
        menu.Items.Add(new Forms.ToolStripSeparator());
        Add(L.T("Exit (stops reminders)", "Έξοδος (διακοπή υπενθυμίσεων)"), ExitApp);
        tray.ContextMenuStrip = menu; old?.Dispose();
        tray.Text = Data.Settings.PausedUntil > DateTime.Now ? "QuestBell · " + L.T("Paused", "Σε παύση") : "QuestBell · " + L.T("Your little quest companion", "Ο μικρός σου σύντροφος");
    }
    internal void ExitApp()
    {
        Exiting = true; Cloud?.Stop(); CloseReminder(); tray?.Dispose(); Shutdown();
    }
    private sealed class TrayPalette : Forms.ProfessionalColorTable
    {
        public override System.Drawing.Color ToolStripDropDownBackground => System.Drawing.Color.FromArgb(23,34,52);
        public override System.Drawing.Color MenuItemSelected => System.Drawing.Color.FromArgb(44,72,83);
        public override System.Drawing.Color MenuItemBorder => System.Drawing.Color.FromArgb(65,96,99);
        public override System.Drawing.Color MenuBorder => System.Drawing.Color.FromArgb(64,83,107);
        public override System.Drawing.Color SeparatorDark => System.Drawing.Color.FromArgb(52,68,87);
        public override System.Drawing.Color SeparatorLight => System.Drawing.Color.FromArgb(23,34,52);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        Updates?.Dispose(); Cloud?.Stop(); tray?.Dispose(); sound?.Dispose(); mutex?.Dispose(); activate?.Dispose(); exitRequest?.Dispose();
        SystemEvents.SessionSwitch -= SessionSwitch; SystemEvents.PowerModeChanged -= PowerChanged;
        base.OnExit(e);
    }
}
