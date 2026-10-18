using System.Text.Json;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;

namespace QuestBell;

internal static class RenderTests
{
    internal static void Run(App app, string[] args)
    {
        app.Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                string path = Path.Combine(app.Storage.DirectoryPath, "screenshots"); Directory.CreateDirectory(path);
                var t = Catalog.Templates.First();
                var r = new Reminder { Title = "Pay phone bill", ActionId = t.Action, SubjectId = t.Subject, ObjectId = t.Object, Category = t.Category, NextAt = DateTime.Today.AddHours(18), AnchorAt = DateTime.Today.AddHours(18), OccurrenceAt = DateTime.Today.AddHours(18), LastArtVariant = 1, Amount = "€35", DueAt = DateTime.Today.AddDays(2), Repeat = "monthly" };
                app.Data.Reminders = [r,
                    new Reminder { Title = "Cancel streaming subscription", ActionId = "cancel", SubjectId = "streaming", ObjectId = "subscription", Category = "subscriptions", NextAt = DateTime.Today.AddDays(2).AddHours(12), AnchorAt = DateTime.Today.AddDays(2).AddHours(12), OccurrenceAt = DateTime.Today.AddDays(2).AddHours(12), LastArtVariant = 0 },
                    new Reminder { Title = "Replace air filter", ActionId = "replace", SubjectId = "air", ObjectId = "filter", Category = "home", NextAt = DateTime.Today.AddDays(4).AddHours(9), AnchorAt = DateTime.Today.AddDays(4).AddHours(9), OccurrenceAt = DateTime.Today.AddDays(4).AddHours(9), LastArtVariant = 2 }];
                app.Data.Settings.ReduceMotion = true;
                r.ArtVariant = 5;
                app.Data.Reminders[1].LastArtVariant = 21;
                app.Data.Reminders[2].LastArtVariant = 41;
                int artworkChecks = CheckArtwork(path);
                int cloudChecks = await CloudServiceTests.Run(app); int updateChecks = 0;
                app.Manager = new MainWindow();
                int themeChecks = 0, menuChecks = 0, composerChecks = 0, workChecks = 0, quickChecks = 0;
                void CheckWindow(Window window, double preferredWidth, double preferredHeight)
                {
                    if (window.Background is not SolidColorBrush brush || brush.Color != Ui.Brush("#0D121B").Color || window.FontSize != 14)
                        throw new Exception("Window frame/background theme missing: " + window.GetType().Name);
                    themeChecks++;
                    new WindowInteropHelper(window).EnsureHandle();
                    if (window.Width > preferredWidth || window.Height > preferredHeight || window.Width < window.MinWidth || window.Height < window.MinHeight)
                        throw new Exception("Window did not fit screen bounds: " + window.GetType().Name);
                    if (SystemParameters.WorkArea.Width >= preferredWidth + 32 && window.Width < preferredWidth)
                        throw new Exception("Window was not enlarged: " + window.GetType().Name);
                    themeChecks++;
                }
                CheckWindow(app.Manager, 1440, 980);
                var snooze = new SnoozeWindow(); CheckWindow(snooze, 650, 760);
                foreach (var lang in new[] { "en", "el" })
                {
                    L.Language = app.Data.Settings.Language = lang; updateChecks += CheckUpdateControls(app, lang, path);
                    artworkChecks += CheckComposerArtwork(lang);
                    composerChecks += CheckSimpleComposer(app, lang, path);
                    workChecks += CheckWorkComposer(app, lang, path);
                    quickChecks += CheckQuickChoices(app, lang, path);
                    menuChecks += MenuRenderTests.Run(app.Manager, r, path, lang);
                    app.Manager.Navigate("tasks"); Render(app.Manager, Path.Combine(path, "dashboard-" + lang + ".png"), 1440, 940);
                    var composer = new ComposerWindow(r); CheckWindow(composer, 1100, 1000); Render(composer, Path.Combine(path, "composer-" + lang + ".png"), 1100, 960);
                    Render(new CloudWindow(), Path.Combine(path, "cloud-" + lang + ".png"), 900, 1350);
                    var reminder = new ReminderWindow(r, true); Render(reminder, Path.Combine(path, "reminder-" + lang + ".png"), 1440, 900);
                    app.Manager.Navigate("settings"); Render(app.Manager, Path.Combine(path, "settings-" + lang + ".png"), 1440, 2200);
                    app.Manager.Navigate("collection"); Render(app.Manager, Path.Combine(path, "collection-" + lang + ".png"), 1440, 1100);
                }
                L.Language = app.Data.Settings.Language = "en";
                app.Save();
                var loaded = app.Storage.Load();
                if (loaded.Reminders.Count != 3) throw new Exception("Persistence round trip failed.");
                var entry = app.Data.Resolve(r, "done", DateTime.Now);
                if (!app.Data.Undo(entry)) throw new Exception("Undo flow failed.");
                await app.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                var process = System.Diagnostics.Process.GetCurrentProcess(); process.Refresh();
                var report = new { Result = "PASS", Screenshots = 46, UpdateChecks = updateChecks, QuickChoicesChecks = quickChecks, WorkChecks = workChecks, CloudChecks = cloudChecks, ComposerChecks = composerChecks, ArtworkChecks = artworkChecks, ArtworkCount = Artwork.Total, MenuLayoutAndContrastChecks = menuChecks, WindowThemeAndSizingChecks = themeChecks, Languages = new[] { "en", "el" }, PersistedReminders = loaded.Reminders.Count, WorkingSetMB = process.WorkingSet64 / 1048576.0, ManagedMB = GC.GetTotalMemory(false) / 1048576.0, Time = DateTime.Now };
                File.WriteAllText(Path.Combine(app.Storage.DirectoryPath, "smoke-report.json"), JsonSerializer.Serialize(report, Store.Json));
                app.Shutdown(0);
            }
            catch (Exception error) { File.WriteAllText(Path.Combine(app.Storage.DirectoryPath, "smoke-error.txt"), error.ToString()); app.Shutdown(3); }
        }, DispatcherPriority.ApplicationIdle);
    }
    private static int CheckSimpleComposer(App app, string language, string path)
    {
        T Field<T>(ComposerWindow window, string name) => (T)typeof(ComposerWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception("Composer: " + message + "/" + language); checks++; }
        var simple = new ComposerWindow();
        var more = Field<Expander>(simple, "moreOptions");
        Check(!more.IsExpanded, "new reminder starts simple");
        Render(simple, Path.Combine(path, "composer-simple-" + language + ".png"), 1100, 850);
        var root = (DockPanel)simple.Content; var scroll = root.Children.OfType<ScrollViewer>().Single();
        Check(scroll.ExtentHeight <= scroll.ViewportHeight + 1, "default form fits without scrolling at 1100x850");
        var compact = new ComposerWindow();
        Render(compact, Path.Combine(path, "composer-compact-" + language + ".png"), 850, 790);
        var compactScroll = ((DockPanel)compact.Content).Children.OfType<ScrollViewer>().Single();
        Check(compactScroll.ExtentHeight <= compactScroll.ViewportHeight + 1, $"default form fits without scrolling at 850x790 (extent {compactScroll.ExtentHeight}, viewport {compactScroll.ViewportHeight})");
        var custom = Field<CheckBox>(simple, "custom");
        string priorAction = Field<ComboBox>(simple, "action").Text;
        custom.IsChecked = true; custom.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        Check(Field<Grid>(simple, "puzzle").Visibility == Visibility.Collapsed && Field<TextBox>(simple, "title").Visibility == Visibility.Visible, "custom title mode hides unused phrase controls");
        custom.IsChecked = false; custom.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        Check(Field<Grid>(simple, "puzzle").Visibility == Visibility.Visible && Field<ComboBox>(simple, "action").Text == priorAction, "switching back keeps phrase choices");
        Field<ComboBox>(simple, "repeat").SelectedIndex = 5;
        Check(Field<Expander>(simple, "repeatOptions").IsExpanded, "choosing weekdays reveals day controls");
        Check(Field<List<CheckBox>>(simple, "weekdayChecks").Count == 7, "all weekday choices retained");
        void Click(ComposerWindow window, string name) => Field<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        int Step(ComposerWindow window) => Field<int>(window, "step");
        Check(Step(simple) == 0 && Field<Button>(simple, "save").Visibility == Visibility.Collapsed, "creation starts at What with no premature save");
        custom.IsChecked = true; custom.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        Click(simple, "next"); Check(Step(simple) == 0 && Field<TextBlock>(simple, "error").Text.Length > 0, "empty title blocks Next");
        Field<TextBox>(simple, "title").Text = "Wizard test / Δοκιμή οδηγού";
        Click(simple, "next"); Check(Step(simple) == 1, "Next advances to When");
        Field<ComboBox>(simple, "hour").Text = "25";
        Click(simple, "next"); Check(Step(simple) == 1, "invalid hour blocks Next");
        Field<ComboBox>(simple, "hour").Text = "12";
        foreach (var day in Field<List<CheckBox>>(simple, "weekdayChecks")) day.IsChecked = false;
        Click(simple, "next"); Check(Step(simple) == 1, "empty weekday selection blocks Next");
        Field<List<CheckBox>>(simple, "weekdayChecks")[0].IsChecked = true;
        Click(simple, "back"); Click(simple, "next");
        Render(simple, Path.Combine(path, "wizard-when-" + language + ".png"), 850, 790);
        Click(simple, "next"); Check(Step(simple) == 2 && Field<Button>(simple, "save").Visibility == Visibility.Visible, "third step allows save");
        Check(Field<TextBlock>(simple, "previewTitle").Text.Contains("Wizard test") && Field<TextBlock>(simple, "reviewTime").Text.Contains("12:"), "review reflects entered title and time");
        Render(simple, Path.Combine(path, "wizard-review-" + language + ".png"), 850, 790);
        Check(scroll.ExtentHeight <= scroll.ViewportHeight + 1, "review fits without scrolling");
        Click(simple, "back"); Click(simple, "back");
        Check(Step(simple) == 0 && Field<TextBox>(simple, "title").Text.Contains("Wizard test") && Field<ComboBox>(simple, "hour").Text == "12", "Back preserves title and schedule");
        Click(simple, "next"); Click(simple, "next"); more.IsExpanded = true;
        Render(simple, Path.Combine(path, "composer-expanded-" + language + ".png"), 1100, 2400);

        var previous = app.Data;
        try
        {
            var due = DateTime.Today.AddDays(20).AddHours(23).AddMinutes(59);
            var next = DateTime.Today.AddDays(3).AddHours(12).AddMinutes(34);
            var task = new Reminder { Title = "Custom task / Προσωπική εργασία", CustomTitle = true, Category = "bills", NextAt = next, AnchorAt = next, OccurrenceAt = next,
                Repeat = "monthly", Interval = 3, AfterCompletion = true, DueAt = due, Amount = "€42", Url = "https://example.com/bills", Note = "Saved notes / Σημειώσεις", ArtVariant = 5, LastArtVariant = 5, PinArt = true, Sound = false, FollowUpMinutes = 360 };
            app.Data = new AppData { Reminders = [task] }; app.Data.Settings.Language = language;
            var edit = new ComposerWindow(task) { Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
            var details = Field<Expander>(edit, "moreOptions");
            Check(!details.IsExpanded, "editing also starts simple");
            Check(Field<TextBlock>(edit, "optionsSummary").Text.Contains(language == "el" ? "Σε χρήση" : "In use"), "hidden configured values are summarized");
            Exception? failure = null;
            edit.Loaded += (_, _) =>
            {
                try
                {
                    details.IsExpanded = true; details.IsExpanded = false;
                    Click(edit, "next"); Click(edit, "next");
                    Check(Step(edit) == 2, "edit navigates to review");
                    Click(edit, "back"); Click(edit, "next");
                    Click(edit, "save");
                    Check(edit.DialogResult == true, "save works with settings collapsed: " + Field<TextBlock>(edit, "error").Text);
                }
                catch (Exception error) { failure = error; }
                finally { if (edit.IsVisible) edit.Close(); }
            };
            edit.ShowDialog(); if (failure is not null) throw failure;
            var saved = app.Storage.Load().Reminders.Single();
            Check(saved.Title == task.Title && saved.CustomTitle && saved.NextAt == next, "custom title and schedule survive save");
            Check(saved.Repeat == "monthly" && saved.Interval == 3 && saved.AfterCompletion, "repeat rules survive collapse and save");
            Check(saved.DueAt == due && saved.Amount == task.Amount && saved.Url == task.Url && saved.Note == task.Note, "extra details survive collapse and save");
            Check(saved.PinArt && saved.ArtVariant == 5 && !saved.Sound && saved.FollowUpMinutes == 360, "artwork and sound settings survive collapse and save");
            var create = new ComposerWindow(template: Catalog.Templates.First()) { Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
            create.Loaded += (_, _) =>
            {
                try
                {
                    Field<DatePicker>(create, "date").SelectedDate = DateTime.Today.AddDays(4);
                    Field<ComboBox>(create, "hour").Text = "09"; Field<ComboBox>(create, "minute").Text = "30";
                    Click(create, "next"); Click(create, "next");
                    Field<TextBox>(create, "url").Text = "file:///C:/test";
                    Click(create, "save");
                    Check(create.DialogResult != true && Field<Expander>(create, "moreOptions").IsExpanded && app.Data.Reminders.Count == 1, "invalid optional URL reveals details without saving");
                    Field<TextBox>(create, "url").Text = "https://example.com/pay";
                    Field<Expander>(create, "moreOptions").IsExpanded = false;
                    Click(create, "save");
                    Check(create.DialogResult == true, "new reminder saves from review");
                }
                catch (Exception error) { failure = error; }
                finally { if (create.IsVisible) create.Close(); }
            };
            create.ShowDialog(); if (failure is not null) throw failure;
            var created = app.Storage.Load().Reminders.Single(x => x.Id != task.Id);
            Check(created.NextAt == DateTime.Today.AddDays(4).AddHours(9).AddMinutes(30) && created.SubjectId == Catalog.Templates.First().Subject && created.Url == "https://example.com/pay", "new template reminder persists schedule and optional details");
            var cancel = new ComposerWindow { Opacity = 0, ShowActivated = false, ShowInTaskbar = false };
            cancel.Loaded += (_, _) => { Click(cancel, "next"); Click(cancel, "next"); cancel.Close(); };
            cancel.ShowDialog(); Check(app.Data.Reminders.Count == 2 && app.Storage.Load().Reminders.Count == 2, "closing the wizard discards the unsaved reminder");
        }
        finally { app.CloseReminder(); app.Data = previous; }
        return checks;
    }
    private static int CheckWorkComposer(App app, string language, string path)
    {
        T Field<T>(ComposerWindow window, string name) => (T)typeof(ComposerWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
        void Click(ComposerWindow window, string name) => Field<Button>(window, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        int checks = 0;
        void Check(bool value, string description) { if (!value) throw new Exception("Work: " + description + "/" + language); checks++; }
        var previous = app.Data;
        try
        {
            app.Data = new AppData(); app.Data.Settings.Language = language; app.Data.Settings.WorkMode = true;
            var window = new ComposerWindow();
            Check(Field<ComboBox>(window, "mode").SelectedIndex == 1, "work default opens work mode");
            Check(Field<ComboBox>(window, "workTemplates").Items.Count == 26, "all work templates available");
            for (int i = 0; i < WorkCatalog.Templates.Length; i++)
            {
                Field<ComboBox>(window, "workTemplates").SelectedIndex = i;
                string sheet = WorkCatalog.Templates[i].Topic + "-friends.png";
                var image = (Image)Field<ContentControl>(window, "picture").Content;
                Check(Path.GetFileName(((BitmapImage)((CroppedBitmap)image.Source).Source).UriSource.LocalPath) == sheet, "specific work artwork for " + WorkCatalog.Templates[i].Id);
                var variants = Field<List<int>>(window, "artworkVariants");
                int pet = variants.FindIndex(v => v >= 52 && Artwork.Get("documents", v).Cell % 2 == 1);
                Field<ComboBox>(window, "variant").SelectedIndex = pet;
                var petBitmap = (CroppedBitmap)((Image)Field<ContentControl>(window, "picture").Content).Source;
                Check(pet > 0 && Path.GetFileName(((BitmapImage)petBitmap.Source).UriSource.LocalPath) == sheet && Artwork.Get("documents", variants[pet]).Cell % 2 == 1, "named work pet selectable for " + WorkCatalog.Templates[i].Id);
                Field<ComboBox>(window, "variant").SelectedIndex = 0;
            }
            Field<ComboBox>(window, "workTemplates").SelectedIndex = Array.FindIndex(WorkCatalog.Templates, t => t.Id == "weekly-newsletter");
            Check(Field<ComboBox>(window, "repeat").SelectedIndex == Array.IndexOf(Schedule.Repeats, "weekly"), "weekly newsletter chooses weekly repeat");
            Field<ComboBox>(window, "workTemplates").SelectedIndex = Array.FindIndex(WorkCatalog.Templates, t => t.Id == "ship-order");
            Field<TextBox>(window, "workReference").Text = "XYZ-204";
            Check(Field<TextBlock>(window, "previewTitle").Text.Contains("XYZ-204") && Field<ComboBox>(window, "repeat").SelectedIndex == 0, "reference updates preview and shipping resets weekly preset");
            Render(window, Path.Combine(path, "work-what-" + language + ".png"), 850, 790);
            var scroll = Field<ScrollViewer>(window, "scroll"); Check(scroll.ExtentHeight <= scroll.ViewportHeight + 1, "work first step fits compact screen");
            Field<DatePicker>(window, "date").SelectedDate = DateTime.Today.AddDays(12);
            Field<ComboBox>(window, "hour").Text = "10"; Field<ComboBox>(window, "minute").Text = "15";
            Click(window, "next"); Click(window, "next");
            Render(window, Path.Combine(path, "work-review-" + language + ".png"), 850, 790);
            Check(Field<TextBlock>(window, "reviewTime").Text.Contains("10:15"), "shipping schedule displayed on review");
            Click(window, "back"); Click(window, "back");
            Check(Field<TextBox>(window, "workReference").Text == "XYZ-204", "reference survives navigation");
            window.Opacity = 0; window.ShowActivated = false; window.ShowInTaskbar = false;
            Exception? failure = null;
            window.Loaded += (_, _) => { try { Click(window, "next"); Click(window, "next"); Click(window, "save"); Check(window.DialogResult == true, "work reminder saves"); } catch (Exception e) { failure = e; } finally { if (window.IsVisible) window.Close(); } };
            window.ShowDialog(); if (failure is not null) throw failure;
            var task = app.Storage.Load().Reminders.Single();
            Check(task.IsWork && task.WorkTemplateId == "ship-order" && task.WorkReference == "XYZ-204" && task.NextAt == DateTime.Today.AddDays(12).AddHours(10).AddMinutes(15), "work fields and shipping date persist");
            app.Manager!.Navigate("tasks"); Render(app.Manager, Path.Combine(path, "work-dashboard-" + language + ".png"), 1440, 940);
            var edit = new ComposerWindow(app.Data.Reminders.Single());
            Check(Field<TextBox>(edit, "workReference").Text == "XYZ-204" && Field<ComboBox>(edit, "mode").SelectedIndex == 1, "editing restores work fields");
            Field<TextBox>(edit, "title").Text = "My custom work task";
            Check(!Field<bool>(edit, "workAuto"), "custom wording stops template regeneration");
            Field<ComboBox>(edit, "mode").SelectedIndex = 0;
            Check(Field<FrameworkElement>(edit, "workFields").Visibility == Visibility.Collapsed && Field<TextBox>(edit, "title").Text == "My custom work task", "personal switch preserves custom wording");
            Field<ComboBox>(edit, "mode").SelectedIndex = 1;
            edit.Opacity = 0; edit.ShowActivated = false; edit.ShowInTaskbar = false;
            edit.Loaded += (_, _) => { try { Click(edit, "next"); Click(edit, "next"); Click(edit, "save"); Check(edit.DialogResult == true, "custom work edit saves"); } catch (Exception e) { failure = e; } finally { if (edit.IsVisible) edit.Close(); } };
            edit.ShowDialog(); if (failure is not null) throw failure;
            var customWork = app.Storage.Load().Reminders.Single();
            Check(customWork.IsWork && customWork.CustomTitle && customWork.WorkTemplateId == "" && Catalog.Title(customWork, language) == "My custom work task", "custom work text stays custom after reload");
        }
        finally { app.Data = previous; app.Storage.Save(previous); }
        return checks;
    }
    private static int CheckQuickChoices(App app, string language, string path)
    {
        IEnumerable<DependencyObject> Walk(DependencyObject node)
        {
            yield return node;
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) foreach (var nested in Walk(child)) yield return nested;
        }
        T Named<T>(Window window, string name) where T : FrameworkElement => Walk(window).OfType<T>().Single(c => c.Name == name);
        T Field<T>(ComposerWindow window, string name) => (T)typeof(ComposerWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
        void Click(Window window, string label) => Walk(window).OfType<Button>().Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void Next(ComposerWindow window) => Field<Button>(window, "next").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        int checks = 0; void Check(bool value, string description) { if (!value) throw new Exception("Quick choices: " + description + "/" + language); checks++; }
        var previous = app.Data;
        try
        {
            app.Data = new AppData(); app.Data.Settings.Language = language;
            var settings = new QuickSettingsWindow();
            Click(settings, L.T("Add quick reminder", "Προσθήκη γρήγορης υπενθύμισης"));
            Check(app.Data.Settings.QuickReminders.Count == 0, "empty quick reminder is not added");
            Named<TextBox>(settings, "QuickReminderTitle").Text = "Email project team / Ομάδα έργου";
            Named<ComboBox>(settings, "QuickReminderMode").SelectedIndex = 1;
            Named<ComboBox>(settings, "QuickReminderRepeat").SelectedIndex = 2;
            Click(settings, L.T("Add quick reminder", "Προσθήκη γρήγορης υπενθύμισης"));
            Check(app.Storage.Load().Settings.QuickReminders.Single().Work && app.Storage.Load().Settings.QuickReminders.Single().Repeat == "weekly", "settings addition persists work mode and repeat");
            Named<TextBox>(settings, "QuickTimeClock").Text = "08:35";
            Click(settings, L.T("Add time button", "Προσθήκη κουμπιού ώρας"));
            Check(app.Storage.Load().Settings.QuickTimes.Any(t => t.Kind == "clock" && t.Value == 515), "custom clock button persists");
            Click(settings, L.T("Add time button", "Προσθήκη κουμπιού ώρας"));
            Check(app.Data.Settings.QuickTimes.Count == 8, "duplicate time button rejected");
            Named<ComboBox>(settings, "QuickTimeKind").SelectedIndex = 1; Named<TextBox>(settings, "QuickTimeValue").Text = "20";
            Click(settings, L.T("Add time button", "Προσθήκη κουμπιού ώρας"));
            Check(app.Data.Settings.QuickTimes.Count == 9, "relative quick button added");
            Render(settings, Path.Combine(path, "quick-settings-" + language + ".png"), 1000, 1550);
            var composer = new ComposerWindow(); Named<ComboBox>(composer, "UserQuickReminders").SelectedIndex = 1;
            Check(Field<TextBlock>(composer, "previewTitle").Text.StartsWith("Email project team") && Field<ComboBox>(composer, "mode").SelectedIndex == 1 && Field<ComboBox>(composer, "repeat").SelectedIndex == 2, "custom quick reminder fills the wizard");
            Next(composer);
            Field<DatePicker>(composer, "date").SelectedDate = DateTime.Today.AddDays(5);
            Click(composer, "08:35");
            Check(Field<DatePicker>(composer, "date").SelectedDate == DateTime.Today.AddDays(5) && Field<ComboBox>(composer, "hour").Text == "08" && Field<ComboBox>(composer, "minute").Text == "35", "clock button preserves selected date");
            var delay = app.Data.Settings.QuickTimes.Single(t => t.Kind == "delay");
            var before = DateTime.Now.AddMinutes(20); Click(composer, QuickSettingsWindow.TimeLabel(delay));
            var applied = Field<DatePicker>(composer, "date").SelectedDate!.Value.AddHours(int.Parse(Field<ComboBox>(composer, "hour").Text)).AddMinutes(int.Parse(Field<ComboBox>(composer, "minute").Text));
            Check(Math.Abs((applied - before).TotalSeconds) < 61, "relative button uses current time");
            Walk(composer).OfType<Expander>().Single(e => e.Header?.ToString() == L.T("More timing choices", "Περισσότερες επιλογές ώρας")).IsExpanded = true;
            Render(composer, Path.Combine(path, "quick-times-" + language + ".png"), 1000, 1000);
            composer.Opacity = 0; composer.ShowActivated = false; composer.ShowInTaskbar = false;
            Exception? failure = null;
            composer.Loaded += (_, _) => { try { Next(composer); Field<Button>(composer, "save").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(composer.DialogResult == true, "quick reminder creates a real reminder"); } catch (Exception e) { failure = e; } finally { if (composer.IsVisible) composer.Close(); } };
            composer.ShowDialog(); if (failure is not null) throw failure;
            Click(settings, L.T("Remove", "Αφαίρεση"));
            Check(app.Storage.Load().Settings.QuickReminders.Count == 0 && app.Storage.Load().Reminders.Count == 1, "removing a quick reminder keeps created reminders");
            Click(settings, "08:35  ×");
            Check(app.Storage.Load().Settings.QuickTimes.All(t => t.Kind != "clock" || t.Value != 515), "removing a time button persists");
            Click(settings, L.T("Restore default time buttons", "Επαναφορά προεπιλεγμένων κουμπιών ώρας"));
            Check(app.Storage.Load().Settings.QuickTimes.Count == 7 && app.Storage.Load().Reminders.Count == 1, "default reset keeps real reminders");
        }
        finally { app.CloseReminder(); app.Data = previous; app.Storage.Save(previous); }
        return checks;
    }
    private static int CheckArtwork(string path)
    {
        var regions = new Dictionary<string, object?[]>();
        foreach (var art in Artwork.All)
        {
            var sprite = (CroppedBitmap)Art.Sprite(art.Category, art.Variant);
            if (art.Variant >= 4)
            {
                if (!regions.ContainsKey(art.Sheet)) regions[art.Sheet] = new object?[4];
                regions[art.Sheet][art.Cell] = new { x = sprite.SourceRect.X, y = sprite.SourceRect.Y, w = sprite.SourceRect.Width, h = sprite.SourceRect.Height, sw = sprite.Source.PixelWidth, sh = sprite.Source.PixelHeight };
            }
            var source = new FormatConvertedBitmap(sprite, PixelFormats.Bgra32, null, 0);
            int stride = source.PixelWidth * 4; var pixels = new byte[stride * source.PixelHeight]; source.CopyPixels(pixels, stride, 0);
            int visible = 0, clear = 0;
            for (int i = 3; i < pixels.Length; i += 4) { if (pixels[i] > 128) visible++; if (pixels[i] == 0) clear++; }
            if (visible < source.PixelWidth * source.PixelHeight / 10 || clear < source.PixelWidth * source.PixelHeight / 20)
                throw new Exception("Artwork is empty or lacks transparent padding: " + art.Sheet + "/" + art.Cell);
        }
        File.WriteAllText(Path.Combine(path, "artwork-regions.json"), JsonSerializer.Serialize(regions));
        foreach (bool pets in new[] { true, false })
        {
            var illustrations = Artwork.All.Where(a => a.Variant >= 4 && (a.Cell % 2 == 1) == pets).ToArray();
            int rows = (illustrations.Length + 3) / 4;
            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Rows = rows };
            foreach (var art in illustrations)
            {
                var label = Ui.Text(art.En, 14, Ui.Mint, true); label.TextAlignment = TextAlignment.Center;
                grid.Children.Add(Ui.Card(Ui.Col(Art.Image(art.Category, art.Variant, 160), label), padding: 8));
            }
            var board = new Window { Background = Ui.Brush("#0D121B"), Content = grid };
            Render(board, Path.Combine(path, pets ? "new-pets.png" : "new-items.png"), 1080, rows * 240);
            var workGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4, Rows = 3 };
            foreach (var art in illustrations.Where(a => a.Variant >= 52))
            {
                var label = Ui.Text(art.En, 14, Ui.Mint, true); label.TextAlignment = TextAlignment.Center;
                workGrid.Children.Add(Ui.Card(Ui.Col(Art.Image(art.Category, art.Variant, 180), label), padding: 10));
            }
            var display = new DockPanel { Margin = new Thickness(24) };
            var heading = Ui.Col(Ui.Eyebrow("QUESTBELL · WORK COLLECTION"), Ui.Text(pets ? "Meet your work companions." : "A little magic for the working day.", 28, null, true));
            DockPanel.SetDock(heading, Dock.Top); display.Children.Add(heading); display.Children.Add(workGrid);
            Render(new Window { Background = Ui.Brush("#0D121B"), Content = display }, Path.Combine(path, pets ? "work-pets.png" : "work-items.png"), 1120, 900);
        }
        Art.TrimCache();
        return Artwork.Total;
    }
    private static int CheckComposerArtwork(string language)
    {
        T Field<T>(ComposerWindow window, string name) => (T)typeof(ComposerWindow).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
        int checks = 0;
        foreach (var template in Catalog.Templates)
        {
            var window = new ComposerWindow(template: template);
            var task = new Reminder { Title = Catalog.Title(template, language), ActionId = template.Action, SubjectId = template.Subject, ObjectId = template.Object, Category = template.Category };
            var expected = Artwork.Get(task.Category, Artwork.Preferred(task));
            void CheckPreview(Illustration art)
            {
                var image = (Image)Field<ContentControl>(window, "picture").Content;
                var crop = (CroppedBitmap)image.Source;
                if (Path.GetFileName(((BitmapImage)crop.Source).UriSource.LocalPath) != art.Sheet + ".png" || crop.SourceRect.X / (crop.Source.PixelWidth / 2) != art.Cell % 2 || crop.SourceRect.Y / (crop.Source.PixelHeight / 2) != art.Cell / 2)
                    throw new Exception("Wrong composer artwork for " + template.Id + "/" + language);
                checks++;
            }
            CheckPreview(expected);
            var variant = Field<ComboBox>(window, "variant");
            var relatedPet = Artwork.Choices(task).First(a => a.Variant >= 4 && a.Cell % 2 == 1);
            variant.SelectedItem = relatedPet.Label(language); CheckPreview(relatedPet);
            variant.SelectedIndex = 0; CheckPreview(expected);
        }
        return checks;
    }
    private static int CheckUpdateControls(App app, string language, string path)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        void Property(string name, object? value) => typeof(UpdateService).GetProperty(name, flags)!.SetValue(app.Updates, value);
        void Field(string name, object? value) => typeof(UpdateService).GetField(name, flags)!.SetValue(app.Updates, value);
        T Control<T>(string name) => (T)typeof(MainWindow).GetField(name, flags)!.GetValue(app.Manager)!;
        void Refresh() => typeof(MainWindow).GetMethod("RefreshUpdates", flags)!.Invoke(app.Manager, null);
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception("Updates: " + message + "/" + language); checks++; }
        var release = new UpdateRelease(new Version(9, 0, 0, 0), GitHubUpdates.Repository + "/releases/tag/v9.0.0", GitHubUpdates.Repository + "/releases/download/v9.0.0/QuestBell-Setup-9.0.0.exe", 100, new string('a', 64));
        using var cancellation = new CancellationTokenSource();
        try
        {
            Property("Available", release); Field("state", "available");
            app.Manager!.Navigate("settings");
            Check(Control<Button>("updateInstall").IsEnabled && Control<Button>("updateInstall").Visibility == Visibility.Visible, "settings offers verified update");
            Check(Control<Button>("updateInstall").Content.ToString() == L.T("Update now", "Ενημέρωση τώρα"), "localized install action");
            app.Manager.Navigate("tasks"); Render(app.Manager, Path.Combine(path, "update-ready-" + language + ".png"), 1440, 940);
            Check(Control<Button>("bannerInstall").Visibility == Visibility.Visible, "dashboard offers direct update");
            Property("Busy", true); Property("Percent", 40); Field("download", cancellation); Field("state", "downloading"); Refresh();
            Check(!Control<Button>("bannerInstall").IsEnabled && Control<Button>("bannerCancel").Visibility == Visibility.Visible && app.Updates.Status.Contains("40%"), "download progress disables duplicate install and offers cancel");
            Render(app.Manager, Path.Combine(path, "update-progress-" + language + ".png"), 1440, 940);
            Control<Button>("bannerCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(cancellation.IsCancellationRequested, "cancel action reaches active transfer");
            Field("download", null); Field("state", "installing"); Refresh();
            Check(Control<Button>("bannerCancel").Visibility == Visibility.Collapsed && !Control<Button>("bannerInstall").IsEnabled, "installer handoff cannot be cancelled or duplicated");
            Property("Busy", false); Field("state", "install-error"); Refresh();
            Check(Control<Button>("bannerInstall").IsEnabled, "failure can be retried");
            Property("Available", release with { Sha256 = null }); Refresh();
            Check(Control<Button>("bannerInstall").Visibility == Visibility.Collapsed, "unverifiable release cannot install");
        }
        finally { Field("download", null); Field("state", "idle"); Property("Available", null); Property("Busy", false); Property("Percent", 0); app.Manager!.Navigate("tasks"); }
        return checks;
    }
    private static void Render(Window window, string path, int width, int height)
    {
        window.Width = width; window.Height = height;
        var root = (FrameworkElement)window.Content;
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual(); using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, width, height));
        bitmap.Render(background); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(path); encoder.Save(stream);
    }
}
