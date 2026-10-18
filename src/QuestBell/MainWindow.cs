using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace QuestBell;

internal sealed class MainWindow : Window
{
    private App Host => App.Instance;
    private string page = "tasks", query = "";
    private int collectionPage;
    private int taskFilter;
    private Grid shell = null!;
    private StackPanel body = null!;
    private Border toast = null!;
    private DispatcherTimer? undoTimer;
    private TextBlock? cloudStatus, cloudFooter;
    private TextBlock? updateStatus;
    private Button? updateCheck, updateLink;
    private Border updateBanner = null!;
    internal MainWindow()
    {
        Title = "QuestBell · " + L.T("Small quests. Lighter days.", "Μικρές αποστολές. Πιο ανάλαφρες μέρες.");
        Width = 1440; Height = 980; MinWidth = 1040; MinHeight = 720;
        Ui.ApplyWindow(this); WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Host.Changed += Refresh;
        Host.Cloud.Changed += RefreshCloudStatus;
        Host.Updates.Changed += RefreshUpdates;
        Closing += OnClosing;
        Build();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        undoTimer?.Stop(); Host.Changed -= Refresh;
        Host.Cloud.Changed -= RefreshCloudStatus;
        Host.Updates.Changed -= RefreshUpdates;
        if (Host.Manager == this) Host.Manager = null;
        // Shutdown is explicit: destroying this window leaves the tray/scheduler alive
        // and releases the dashboard's images instead of retaining an invisible UI.
        Art.TrimCache();
    }
    internal void Navigate(string destination) { page = destination; Refresh(); }
    internal void Refresh() { if (Dispatcher.CheckAccess()) Build(); else Dispatcher.Invoke(Build); }
    private void RefreshCloudStatus()
    {
        if (cloudStatus is not null) cloudStatus.Text = Host.Cloud.Status;
        if (cloudFooter is not null) cloudFooter.Text = !Host.Cloud.Enabled ? L.T("Offline · On this PC", "Εκτός σύνδεσης · Σε αυτό το PC") : Host.Cloud.Error.Length > 0 ? L.T("Cloud · needs attention", "Cloud · χρειάζεται έλεγχο") : L.T("Cloud folder connected", "Συνδεδεμένος φάκελος cloud");
    }
    private void Build()
    {
        cloudStatus = null;
        updateStatus = null; updateCheck = updateLink = null;
        Title = "QuestBell · " + L.T("Small quests. Lighter days.", "Μικρές αποστολές. Πιο ανάλαφρες μέρες.");
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag);
        shell = new Grid { Background = Ui.Brush("#0D121B") }; shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); shell.ColumnDefinitions.Add(new ColumnDefinition());
        Content = shell;
        var side = new DockPanel { Margin = new Thickness(18, 24, 14, 20) };
        var sidebar = new Border { Background = Ui.Brush("#101924"), BorderBrush = Ui.Brush("#243043"), BorderThickness = new Thickness(0, 0, 1, 0), Child = side };
        shell.Children.Add(sidebar);
        var brand = Ui.Col(Ui.Text("✦  QuestBell", 25, Ui.Mint, true), Ui.Text(L.T("SMALL QUESTS. LIGHTER DAYS.", "ΜΙΚΡΕΣ ΑΠΟΣΤΟΛΕΣ. ΑΝΕΤΕΣ ΜΕΡΕΣ."), 9, Ui.Muted));
        brand.Margin = new Thickness(8, 0, 0, 35); DockPanel.SetDock(brand, Dock.Top); side.Children.Add(brand);
        var footer = Ui.Col(Ui.Text(Host.Data.Settings.PausedUntil > DateTime.Now ? L.T("◌  Reminders paused", "◌  Υπενθυμίσεις σε παύση") : L.T("●  Quietly on your side", "●  Διακριτικά στο πλευρό σου"), 12, Ui.Mint),
            cloudFooter = Ui.Text("", 11, Ui.Muted), Ui.Button(L.T("Open the field guide ↗", "Άνοιγμα οδηγού ↗"), Host.OpenGuide));
        RefreshCloudStatus();
        DockPanel.SetDock(footer, Dock.Bottom); side.Children.Add(footer);
        var navigation = new StackPanel();
        foreach (var (id, label) in new[] {
            ("tasks", L.T("▤   My reminders", "▤   Υπενθυμίσεις")), ("history", L.T("◷   History", "◷   Ιστορικό")),
            ("collection", L.T("✧   Keepsakes", "✧   Συλλογή")), ("settings", L.T("⚙   Settings", "⚙   Ρυθμίσεις")) })
        {
            var button = Ui.Button(label, () => Navigate(id)); button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Padding = new Thickness(15, 14, 15, 14);
            button.Background = Ui.Brush(page == id ? "#243C40" : "#101924"); button.Foreground = page == id ? Ui.Mint : Ui.Muted;
            button.BorderBrush = Ui.Brush(page == id ? "#3B6B62" : "#101924"); navigation.Children.Add(button);
        }
        side.Children.Add(navigation);
        var content = new DockPanel { Margin = new Thickness(32, 26, 28, 16) }; Grid.SetColumn(content, 1); shell.Children.Add(content);
        var header = new DockPanel();
        var actions = Ui.Row(Ui.Button(L.T("Preview", "Δοκιμή"), () => Host.Preview()), Ui.Button(L.T("＋ New reminder", "＋ Νέα υπενθύμιση"), () => Host.Compose(), true));
        DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions);
        string title = page switch { "history" => L.T("A little progress, recorded.", "Η πρόοδός σου, καταγραμμένη."), "collection" => L.T("Your cabinet of little wonders.", "Το ντουλάπι των μικρών θαυμάτων."), "settings" => L.T("Make yourself at home.", "Κάν’ το στα μέτρα σου."), _ => L.T("Hello, quest keeper.", "Γεια σου, φύλακα αποστολών.") };
        var h = Ui.Col(Ui.Eyebrow(DateTime.Now.ToString("dddd, d MMMM", L.Culture)), Ui.Text(title, 26, null, true)); header.Children.Add(h);
        DockPanel.SetDock(header, Dock.Top); content.Children.Add(header);
        updateBanner = Ui.Card(Ui.Row(Ui.Text(Host.Updates.Status, 14, Ui.Mint, true), Ui.Button(L.T("View update ↗", "Προβολή ενημέρωσης ↗"), OpenUpdate)), padding: 12);
        DockPanel.SetDock(updateBanner, Dock.Top); content.Children.Add(updateBanner);
        RefreshUpdates();
        toast = new Border { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 10, 0, 4), Background = Ui.Brush("#25473E"), Padding = new Thickness(14), CornerRadius = new CornerRadius(10) };
        DockPanel.SetDock(toast, Dock.Bottom); content.Children.Add(toast);
        body = new StackPanel { Margin = new Thickness(0, 18, 6, 0) }; content.Children.Add(new ScrollViewer { Content = body });
        switch (page) { case "history": History(); break; case "collection": Collection(); break; case "settings": Settings(); break; default: Tasks(); break; }
    }
    private void OpenUpdate() { try { Host.Updates.OpenRelease(); } catch (Exception e) { Ui.Error(this, e); } }
    private void RefreshUpdates()
    {
        if (updateStatus is not null) updateStatus.Text = Host.Updates.Status;
        if (updateCheck is not null) updateCheck.IsEnabled = !Host.Updates.Busy;
        if (updateLink is not null) updateLink.Content = Host.Updates.Available is null ? L.T("GitHub releases ↗", "Εκδόσεις στο GitHub ↗") : L.T("View update / download ↗", "Ενημέρωση / λήψη ↗");
        if (updateBanner is not null)
        {
            updateBanner.Visibility = Host.Updates.Available is null ? Visibility.Collapsed : Visibility.Visible;
            if (updateBanner.Child is StackPanel row && row.Children[0] is TextBlock label) label.Text = Host.Updates.Status;
        }
    }
    private void Tasks()
    {
        var tasks = Host.Data.Reminders.Where(r => r.Active).OrderBy(r => r.NextAt).ToList();
        var heroLeft = Ui.Col(Ui.Eyebrow(L.T("REAL-LIFE QUESTS", "ΑΠΟΣΤΟΛΕΣ ΤΗΣ ΚΑΘΗΜΕΡΙΝΟΤΗΤΑΣ")),
            Ui.Text(L.T("Less on your mind.\nMore room for your day.", "Λιγότερες έγνοιες.\nΠερισσότερος χώρος για τη μέρα σου."), 29, null, true),
            Ui.Text(Host.Data.Settings.WorkMode ? L.T("Clients, orders, content. Keep your working day in order.", "Πελάτες, παραγγελίες, περιεχόμενο. Οργάνωσε την εργάσιμη μέρα σου.") : L.T("Bills, renewals, little things. We’ll bring them back when it’s time.", "Λογαριασμοί, ανανεώσεις, μικροδουλειές. Θα στα θυμίσουμε στην ώρα τους."), 14, Ui.Muted),
            Ui.Text($"{tasks.Count} " + L.T("active quests", "ενεργές αποστολές") + "    ·    " + Host.Data.CompletedCount + " " + L.T("completed", "ολοκληρωμένες"), 13, Ui.Mint, true));
        var hero = Ui.Columns(heroLeft, Art.Stage("personal", 0, 205, !Host.Data.Settings.ReduceMotion), 2.3, 1);
        body.Children.Add(Ui.Card(hero, "#172A32", 25));
        if (Host.Data.Settings.PausedUntil > DateTime.Now)
            body.Children.Add(Ui.Card(Ui.Row(Ui.Text(L.T("Paused until ", "Παύση έως ") + L.Date(Host.Data.Settings.PausedUntil.Value), 14, Ui.Gold), Ui.Button(L.T("Resume", "Συνέχιση"), () => Host.Pause(null)))));
        var search = new TextBox { Text = query, ToolTip = L.T("Search reminders", "Αναζήτηση υπενθυμίσεων") };
        var results = new StackPanel();
        void Fill()
        {
            results.Children.Clear();
            var filtered = tasks.Where(r => taskFilter == 0 || (taskFilter == 1 ? r.IsWork : !r.IsWork)).Where(r => string.IsNullOrWhiteSpace(query) || Catalog.Title(r, L.Language).Contains(query, StringComparison.CurrentCultureIgnoreCase) || r.Note.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
            foreach (var group in new[] { "overdue", "today", "upcoming" })
            {
                var list = filtered.Where(r => Schedule.Group(r, DateTime.Now) == group).ToList(); if (list.Count == 0) continue;
                var heading = group switch { "overdue" => L.T("OVERDUE", "ΕΚΠΡΟΘΕΣΜΕΣ"), "today" => L.T("TODAY", "ΣΗΜΕΡΑ"), _ => L.T("UPCOMING", "ΠΡΟΣΕΧΩΣ") };
                results.Children.Add(Ui.Text(heading + "  ·  " + list.Count, 11, group == "overdue" ? Ui.Gold : Ui.Muted, true));
                foreach (var task in list) results.Children.Add(TaskCard(task));
            }
            if (filtered.Count == 0 && tasks.Count > 0) results.Children.Add(Ui.Text(L.T("No matching quests.", "Δεν βρέθηκαν αποστολές."), 17, Ui.Muted));
        }
        if (tasks.Count > 0)
        {
            var filter = Ui.Combo(new[] { L.T("All reminders", "Όλες οι υπενθυμίσεις"), L.T("Work", "Εργασία"), L.T("Personal", "Προσωπικά") }, taskFilter);
            filter.SelectionChanged += (_, _) => { taskFilter = filter.SelectedIndex; Fill(); };
            body.Children.Add(Ui.Columns(Ui.Field(L.T("FIND A REMINDER", "ΒΡΕΣ ΜΙΑ ΥΠΕΝΘΥΜΙΣΗ"), search), Ui.Field(L.T("SHOW", "ΕΜΦΑΝΙΣΗ"), filter), 3, 1));
            search.TextChanged += (_, _) => { query = search.Text; Fill(); };
        }
        body.Children.Add(results); Fill();
        if (tasks.Count == 0)
        {
            var empty = Ui.Col(Ui.Text(L.T("Your next little adventure starts here.", "Η επόμενη μικρή περιπέτεια ξεκινά εδώ."), 21, null, true),
                Ui.Text(L.T("Pick a ready-made quest. Choose a time. We’ll handle the nudge.", "Διάλεξε έτοιμη αποστολή και ώρα. Εμείς θα σου τη θυμίσουμε."), 14, Ui.Muted));
            var templates = new WrapPanel();
            foreach (var id in new[] { "phone-bill", "cancel-stream", "passport" })
            {
                var t = Catalog.Templates.First(x => x.Id == id);
                templates.Children.Add(Ui.Button(Catalog.Title(t, L.Language) + "  ↗", () => { var d = new ComposerWindow(null, t) { Owner = this }; d.ShowDialog(); }));
            }
            empty.Children.Add(templates); body.Children.Add(Ui.Card(empty));
        }
        var disabled = Host.Data.Reminders.Where(r => !r.Finished && !r.Enabled).ToList();
        if (disabled.Count > 0)
        {
            var list = new StackPanel(); foreach (var r in disabled) list.Children.Add(Ui.Row(Ui.Text(Catalog.Title(r, L.Language)), Ui.Button(L.T("Enable", "Ενεργοποίηση"), () => { r.Enabled = true; r.Revision++; Host.Save(); Host.Tick(); })));
            body.Children.Add(new Expander { Header = L.T("Disabled reminders", "Απενεργοποιημένες υπενθυμίσεις") + " · " + disabled.Count, Content = list });
        }
    }
    private Border TaskCard(Reminder task)
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(Art.Image(task.Category, Artwork.DisplayVariant(task), 66));
        var details = Ui.Col(Ui.Text(Catalog.Title(task, L.Language), 17, null, true), Ui.Text(L.Date(task.NextAt) + "  ·  " + L.Repeat(task.Repeat) + (task.Snoozed ? L.T(" · Snoozed", " · Σε αναβολή") : ""), 12, Ui.Muted));
        if (task.IsWork) details.Children.Insert(0, Ui.Eyebrow(L.T("Work", "Εργασία")));
        if (task.DueAt.HasValue || task.Amount.Length > 0) details.Children.Add(Ui.Text((task.DueAt.HasValue ? L.T("Due ", "Προθεσμία ") + task.DueAt.Value.ToString("d MMM yyyy", L.Culture) + "  " : "") + task.Amount, 12, Ui.Gold));
        details.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(details, 1); grid.Children.Add(details);
        var edit = Ui.Button("···", () => { }); edit.FontSize = 20; edit.Padding = new Thickness(12, 6, 12, 6);
        edit.ToolTip = L.T("More actions", "Περισσότερες ενέργειες");
        System.Windows.Automation.AutomationProperties.SetName(edit, L.T("More actions", "Περισσότερες ενέργειες"));
        var menu = CreateTaskMenu(task);
        edit.Click += (_, _) => { menu.PlacementTarget = edit; menu.IsOpen = true; };
        var actions = Ui.Row(Ui.Button(L.T("Mark as done ✓", "Σήμανση ως ολοκληρωμένη ✓"), () => Host.Resolve(task, "done")), edit); actions.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(actions, 2); grid.Children.Add(actions); return Ui.Card(grid, padding: 13);
    }
    internal ContextMenu CreateTaskMenu(Reminder task)
    {
        var menu = new ContextMenu { Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, VerticalOffset = 4 };
        void Add(string id, string text, Action action)
        {
            var item = new MenuItem { Header = text, Icon = Ui.ActionIcon(id), Tag = id };
            if (id == "delete") item.Foreground = Ui.Brush("#FFB5A7");
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        Add("edit", L.T("Edit", "Επεξεργασία"), () => Host.Compose(task)); Add("preview", L.T("Preview", "Δοκιμή"), () => Host.Preview(task));
        Add("snooze", L.T("Snooze…", "Αναβολή…"), () => { var d = new SnoozeWindow { Owner = this }; if (d.ShowDialog() == true) Host.Snooze(task, d.Until); });
        if (task.Repeat != "none") Add("skip", L.T("Skip this time", "Παράλειψη αυτή τη φορά"), () => Host.Resolve(task, "skip"));
        Add("disable", L.T("Disable", "Απενεργοποίηση"), () => { task.Enabled = false; task.Revision++; if (Host.CurrentReminder?.Id == task.Id) Host.CloseReminder(); Host.Save(); Host.Tick(); });
        Add("delete", L.T("Delete", "Διαγραφή"), () =>
        {
            if (MessageBox.Show(this, L.T("Delete this reminder and its future schedule?", "Να διαγραφεί η υπενθύμιση και το μελλοντικό πρόγραμμά της;"), "QuestBell", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (Host.CurrentReminder?.Id == task.Id) Host.CloseReminder(); Host.Data.Reminders.Remove(task); Host.Save(); Host.Tick();
        });
        return menu;
    }
    internal void ShowUndo(HistoryEntry entry)
    {
        toast.Child = Ui.Row(Ui.Text(entry.Action == "done" ? L.T("✦ Quest complete!", "✦ Αποστολή ολοκληρώθηκε! ") : L.T("Occurrence skipped", "Η εμφάνιση παραλείφθηκε"), 14, Ui.Mint, true), Ui.Button(L.T("Undo", "Αναίρεση"), () => Undo(entry)));
        toast.Visibility = Visibility.Visible;
        undoTimer?.Stop(); undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(9) }; undoTimer.Tick += (_, _) => { toast.Visibility = Visibility.Collapsed; undoTimer.Stop(); }; undoTimer.Start();
    }
    private void Undo(HistoryEntry entry)
    {
        if (!Host.Data.Undo(entry)) MessageBox.Show(this, L.T("This task has changed since completion. Edit the current reminder instead.", "Η εργασία έχει αλλάξει από τότε. Επεξεργάσου την τρέχουσα υπενθύμιση."), "QuestBell");
        else { Host.CloseReminder(); Host.Save(); Host.Tick(); }
    }
    private void History()
    {
        body.Children.Add(Ui.Text(L.T("Every finished little thing makes room for something else.", "Κάθε μικρή ολοκλήρωση αφήνει χώρο για κάτι άλλο."), 15, Ui.Muted));
        if (Host.Data.History.Count == 0) { body.Children.Add(Ui.Card(Ui.Col(Art.Image("documents", 1, 130), Ui.Text(L.T("A fresh page. Your completed quests will live here.", "Μια νέα σελίδα. Οι ολοκληρωμένες αποστολές θα εμφανίζονται εδώ."), 19)))); return; }
        foreach (var entry in Host.Data.History.Take(300))
        {
            var label = entry.Undone ? L.T("Undone", "Αναιρέθηκε") : entry.Action == "done" ? L.T("Completed", "Ολοκληρώθηκε") : L.T("Skipped", "Παραλείφθηκε");
            var row = new DockPanel(); var undo = Ui.Button(L.T("Undo", "Αναίρεση"), () => Undo(entry));
            undo.IsEnabled = !entry.Undone && Host.Data.Reminders.Any(x => x.Id == entry.Before.Id && x.Revision == entry.ResultRevision);
            DockPanel.SetDock(undo, Dock.Right); row.Children.Add(undo); row.Children.Add(Ui.Col(Ui.Text(Catalog.Title(entry.Before, L.Language), 17, null, true), Ui.Text(label + " · " + L.Date(entry.At), 12, Ui.Muted)));
            body.Children.Add(Ui.Card(row, padding: 17));
        }
        if (Host.Data.History.Count > 300) body.Children.Add(Ui.Text(L.T("Showing the latest 300 entries. All entries are kept in your backup.", "Εμφανίζονται οι 300 πιο πρόσφατες εγγραφές. Όλες διατηρούνται στο αντίγραφο."), 12, Ui.Muted));
    }
    private void Collection()
    {
        if (!Host.Data.Settings.Collection) { body.Children.Add(Ui.Text(L.T("Keepsakes are turned off. Enable them in Settings whenever you like.", "Η συλλογή είναι απενεργοποιημένη. Ενεργοποίησέ τη στις Ρυθμίσεις όποτε θέλεις."), 18, Ui.Muted)); return; }
        int count = Host.Data.CompletedCount; int unlocked = Math.Min(Artwork.Total, count / 2);
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("YOUR LITTLE TREASURY", "Ο ΜΙΚΡΟΣ ΣΟΥ ΘΗΣΑΥΡΟΣ")), Ui.Text($"{unlocked} / {Artwork.Total} " + L.T("keepsakes discovered", "κειμήλια ανακαλύφθηκαν"), 26, Ui.Mint, true), Ui.Text(L.T("Every two completed quests reveal a keepsake. No streaks. No rush. All reminder artwork is always available.", "Κάθε δύο ολοκληρωμένες αποστολές αποκαλύπτουν ένα κειμήλιο. Χωρίς σερί. Χωρίς βιασύνη. Όλες οι εικόνες υπενθυμίσεων είναι πάντα διαθέσιμες."), 14, Ui.Muted))));
        const int perPage = 16;
        int pages = (Artwork.Total + perPage - 1) / perPage;
        collectionPage = Math.Clamp(collectionPage, 0, pages - 1);
        var back = Ui.Button(L.T("← Previous", "← Προηγούμενα"), () => { collectionPage--; Refresh(); }); back.IsEnabled = collectionPage > 0;
        var next = Ui.Button(L.T("Next →", "Επόμενα →"), () => { collectionPage++; Refresh(); }); next.IsEnabled = collectionPage < pages - 1;
        body.Children.Add(Ui.Row(back, Ui.Text($"{collectionPage + 1} / {pages}", 13, Ui.Muted), next));
        var wrap = new WrapPanel(); int index = collectionPage * perPage;
        foreach (var art in Artwork.All.Skip(index).Take(perPage))
        {
            bool open = index++ < unlocked;
            var card = Ui.Col(Art.Image(art.Category, art.Variant, 130), Ui.Text(open ? art.Label(L.Language) : L.T("A little mystery", "Ένα μικρό μυστήριο"), 12, open ? Ui.Mint : Ui.Muted), Ui.Text(open ? L.T("DISCOVERED ✦", "ΑΝΑΚΑΛΥΦΘΗΚΕ ✦") : "◇ " + (index * 2) + " " + L.T("quests", "αποστολές"), 10, Ui.Muted));
            var b = Ui.Card(card, padding: 13); b.Width = 180; b.Margin = new Thickness(0, 0, 12, 12); if (!open) card.Opacity = .35; wrap.Children.Add(b);
        }
        body.Children.Add(wrap);
    }
    private void Settings()
    {
        var preferences = Host.Data.Settings;
        var language = Ui.Combo(new[] { "English", "Ελληνικά" }, preferences.Language == "el" ? 1 : 0);
        language.SelectionChanged += (_, _) => { preferences.Language = language.SelectedIndex == 1 ? "el" : "en"; Host.Save(); };
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("YOUR SPACE", "Ο ΧΩΡΟΣ ΣΟΥ")), Ui.Field(L.T("Language", "Γλώσσα"), language))));
        CheckBox Toggle(string en, string el, bool initial, Action<bool> update)
        {
            var check = new CheckBox { Content = L.T(en, el), IsChecked = initial };
            check.Click += (_, _) => { update(check.IsChecked == true); Host.Save(); }; return check;
        }
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("WORK MODE", "ΛΕΙΤΟΥΡΓΙΑ ΕΡΓΑΣΙΑΣ")),
            Toggle("Start new reminders in Work mode", "Νέες υπενθυμίσεις σε λειτουργία εργασίας", preferences.WorkMode, v => preferences.WorkMode = v),
            Ui.Text(L.T("26 templates for clients, orders, shipping, supplies and content. Switch Personal / Work in the reminder wizard at any time. All reminders still run normally.", "26 πρότυπα για πελάτες, παραγγελίες, αποστολές, προμήθειες και περιεχόμενο. Άλλαξε Προσωπικά / Εργασία στον οδηγό όποτε θέλεις. Όλες οι υπενθυμίσεις λειτουργούν κανονικά."), 13, Ui.Muted))));
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("YOUR QUICK CHOICES", "ΟΙ ΓΡΗΓΟΡΕΣ ΕΠΙΛΟΓΕΣ ΣΟΥ")),
            Ui.Text(L.T("Create reusable reminders and add or remove date, time and delay buttons in the reminder wizard.", "Δημιούργησε επαναχρησιμοποιήσιμες υπενθυμίσεις και πρόσθεσε ή αφαίρεσε κουμπιά ημερομηνίας, ώρας και διαστήματος στον οδηγό."), 13, Ui.Muted),
            Ui.Button(L.T("Customize quick reminders & times…", "Προσαρμογή γρήγορων υπενθυμίσεων & ωρών…"), () => new QuickSettingsWindow { Owner = this }.ShowDialog()))));
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("QUIETLY IN THE BACKGROUND", "ΔΙΑΚΡΙΤΙΚΑ ΣΤΟ ΠΑΡΑΣΚΗΝΙΟ")),
            Toggle("Start with Windows", "Εκκίνηση με τα Windows", preferences.AutoStart, v => { preferences.AutoStart = v; Host.SyncStartup(); }),
            Toggle("Mute reminder sounds", "Σίγαση ήχων υπενθύμισης", preferences.Mute, v => preferences.Mute = v),
            Toggle("Reduce motion", "Περιορισμός κίνησης", preferences.ReduceMotion, v => preferences.ReduceMotion = v),
            Toggle("Collect pixel-art keepsakes", "Συλλογή κειμηλίων pixel art", preferences.Collection, v => preferences.Collection = v))));
        var monitorIds = new List<string> { "primary", "all" }; monitorIds.AddRange(Forms.Screen.AllScreens.Select(x => x.DeviceName));
        var monitorLabels = new List<string> { L.T("Primary monitor", "Κύρια οθόνη"), L.T("All monitors", "Όλες οι οθόνες") }; monitorLabels.AddRange(Forms.Screen.AllScreens.Select((x, i) => L.T("Monitor ", "Οθόνη ") + (i + 1) + " · " + x.Bounds.Width + "×" + x.Bounds.Height));
        var monitor = Ui.Combo(monitorLabels, Math.Max(0, monitorIds.IndexOf(preferences.Monitor)));
        monitor.SelectionChanged += (_, _) => { preferences.Monitor = monitorIds[monitor.SelectedIndex]; Host.CloseReminder(); Host.Save(); Host.Tick(); };
        var defaultSnooze = Ui.Combo(new[] { "5", "10", "15", "30", "60" }, new[] { 5, 10, 15, 30, 60 }.ToList().IndexOf(preferences.SnoozeMinutes));
        defaultSnooze.SelectionChanged += (_, _) => { preferences.SnoozeMinutes = int.Parse((string)defaultSnooze.SelectedItem); Host.Save(); };
        var link = Ui.Combo(new[] { "5", "10", "15", "30", "60" }, new[] { 5, 10, 15, 30, 60 }.ToList().IndexOf(preferences.LinkReturnMinutes));
        link.SelectionChanged += (_, _) => { preferences.LinkReturnMinutes = int.Parse((string)link.SelectedItem); Host.Save(); };
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("WHEN A QUEST ARRIVES", "ΟΤΑΝ ΦΤΑΝΕΙ ΜΙΑ ΑΠΟΣΤΟΛΗ")), Ui.Field(L.T("Display reminders on", "Εμφάνιση υπενθυμίσεων σε"), monitor),
            Ui.Columns(Ui.Field(L.T("Escape snooze · minutes", "Αναβολή με Escape · λεπτά"), defaultSnooze), Ui.Field(L.T("Return after opening link · minutes", "Επιστροφή μετά από σύνδεσμο · λεπτά"), link)),
            Ui.Text(L.T("Full-screen videos and apps are always respected. A waiting reminder appears only when that same app leaves full-screen mode.", "Τα βίντεο και οι εφαρμογές πλήρους οθόνης δεν διακόπτονται. Η υπενθύμιση εμφανίζεται όταν η ίδια εφαρμογή βγει από πλήρη οθόνη."), 13, Ui.Muted))));
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("YOUR DATA, YOUR PC", "ΤΑ ΔΕΔΟΜΕΝΑ ΣΟΥ, ΣΤΟ PC ΣΟΥ")), Ui.Text(L.T("Backups contain task text, amounts, links, history and preferences. Keep them somewhere private.", "Τα αντίγραφα περιέχουν εργασίες, ποσά, συνδέσμους, ιστορικό και προτιμήσεις. Φύλαξέ τα σε ιδιωτικό χώρο."), 13, Ui.Muted),
            Ui.Row(Ui.Button(L.T("Export backup", "Εξαγωγή αντιγράφου"), Export), Ui.Button(L.T("Import backup", "Εισαγωγή αντιγράφου"), () => Import()), Ui.Button(L.T("Open data folder ↗", "Φάκελος δεδομένων ↗"), () => Process.Start(new ProcessStartInfo(Host.Storage.DirectoryPath) { UseShellExecute = true }))),
            Ui.Text(L.T("Custom phrase entries", "Προσαρμοσμένες φράσεις"), 15, null, true), PhraseEditor())));
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("OPTIONAL CLOUD", "ΠΡΟΑΙΡΕΤΙΚΟ CLOUD")), cloudStatus = Ui.Text(Host.Cloud.Status, 14, Ui.Muted),
            Ui.Button(L.T("Cloud sync & backups…", "Συγχρονισμός & αντίγραφα cloud…"), () => new CloudWindow { Owner = this }.ShowDialog()))));
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("APP UPDATES", "ΕΝΗΜΕΡΩΣΕΙΣ ΕΦΑΡΜΟΓΗΣ")),
            Ui.Text(L.T("Installed version: ", "Εγκατεστημένη έκδοση: ") + UpdateService.Installed.ToString(3), 14, Ui.Muted),
            Toggle("Check GitHub automatically once a day", "Αυτόματος έλεγχος στο GitHub μία φορά την ημέρα", preferences.CheckUpdates, v => preferences.CheckUpdates = v),
            updateStatus = Ui.Text(Host.Updates.Status, 14, Ui.Mint),
            Ui.Row(updateCheck = Ui.Button(L.T("Check for updates", "Έλεγχος ενημερώσεων"), () => { _ = Host.Updates.Check(); }), updateLink = Ui.Button(L.T("GitHub releases ↗", "Εκδόσεις στο GitHub ↗"), OpenUpdate)),
            Ui.Text(L.T("Checks published stable releases from sidosgr/QuestBell. Updates appear quietly here; open the release page to download and run its installer when ready. Checks send no reminder data. You control installation.", "Ελέγχει δημοσιευμένες σταθερές εκδόσεις από το sidosgr/QuestBell. Οι ενημερώσεις εμφανίζονται διακριτικά εδώ· άνοιξε τη σελίδα έκδοσης για λήψη και εκτέλεση εγκατάστασης όταν θέλεις. Δεν αποστέλλονται δεδομένα υπενθυμίσεων. Εσύ ελέγχεις την εγκατάσταση."), 13, Ui.Muted))));
        RefreshUpdates();
        body.Children.Add(Ui.Text("QuestBell " + typeof(App).Assembly.GetName().Version!.ToString(3) + " · Windows x64 · " + L.T("Built for a lighter day.", "Για μια πιο ανάλαφρη μέρα."), 12, Ui.Muted));
    }
    private FrameworkElement PhraseEditor()
    {
        var stack = new StackPanel();
        foreach (var kind in new[] { "action", "subject", "object" })
        foreach (var value in Host.Data.CustomPhrases[kind].ToList())
        {
            var input = new TextBox { Text = value, Width = 240, MaxLength = 100 };
            stack.Children.Add(Ui.Row(input, Ui.Button(L.T("Rename", "Μετονομασία"), () => { if (string.IsNullOrWhiteSpace(input.Text)) return; int index = Host.Data.CustomPhrases[kind].IndexOf(value); if (index >= 0) Host.Data.CustomPhrases[kind][index] = input.Text.Trim(); Host.Save(); }),
                Ui.Button(L.T("Delete", "Διαγραφή"), () => { Host.Data.CustomPhrases[kind].Remove(value); Host.Save(); })));
        }
        if (stack.Children.Count == 0) stack.Children.Add(Ui.Text(L.T("Custom words you save in the composer will appear here.", "Οι λέξεις που αποθηκεύεις στη δημιουργία υπενθύμισης θα εμφανίζονται εδώ."), 12, Ui.Muted));
        return stack;
    }
    private void Export()
    {
        var dialog = new SaveFileDialog { Filter = "QuestBell backup (*.json)|*.json", FileName = "QuestBell-" + DateTime.Today.ToString("yyyy-MM-dd") + ".json" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, System.Text.Json.JsonSerializer.Serialize(CloudSync.Backup(Host.Data), Store.Json), System.Text.Encoding.UTF8); MessageBox.Show(this, L.T("Backup saved.", "Το αντίγραφο αποθηκεύτηκε."), "QuestBell"); } catch (Exception e) { Ui.Error(this, e); }
    }
    internal void Import(string? initialDirectory = null)
    {
        Window dialogOwner = Application.Current.Windows.OfType<CloudWindow>().FirstOrDefault(w => w.IsVisible) as Window ?? this;
        var dialog = new OpenFileDialog { Filter = "QuestBell backup (*.json)|*.json", InitialDirectory = initialDirectory ?? "" }; if (dialog.ShowDialog(dialogOwner) != true) return;
        try
        {
            var imported = Store.Read(dialog.FileName);
            if (MessageBox.Show(dialogOwner, L.T("Replace your current reminders, history and settings with this backup? A copy of current data will be kept. Cloud sync and backups will be turned off so you can review the restored data before reconnecting.", "Αντικατάσταση υπενθυμίσεων, ιστορικού και ρυθμίσεων με το αντίγραφο; Θα διατηρηθεί αντίγραφο των τρεχόντων δεδομένων. Ο συγχρονισμός και τα αντίγραφα cloud θα απενεργοποιηθούν για να ελέγξεις την επαναφορά πριν συνδεθείς ξανά."), "QuestBell", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            Host.Storage.Save(Host.Data); File.Copy(Host.Storage.FilePath, Host.Storage.FilePath + ".before-import-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));
            Host.Cloud.Pause(); imported.SyncState = Host.Data.SyncState;
            Host.CloseReminder(); Host.Data = imported; Host.Save(); Host.SyncStartup(); Host.Tick();
        }
        catch (Exception e) { Ui.Error(this, e); }
    }
}
