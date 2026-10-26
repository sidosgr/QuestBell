using System.Windows.Controls.Primitives;

namespace QuestBell;

internal sealed class ComposerWindow : Window
{
    private readonly App host = App.Instance;
    private readonly Reminder? original;
    private readonly ComboBox action, subject, obj, category, hour, minute, repeat, variant, followDelay;
    private readonly DatePicker date, dueDate;
    private readonly CheckBox custom, hasDue, after, pin, sound, follow;
    private readonly TextBox title, amount, url, note, interval;
    private readonly List<CheckBox> weekdayChecks = [];
    private readonly TextBlock previewTitle, error;
    private readonly ContentControl picture;
    private readonly Expander moreOptions;
    private readonly TextBlock optionsSummary;
    private readonly Grid puzzle;
    private readonly TextBlock phraseHint;
    private readonly StackPanel[] pages = [new(), new(), new()];
    private readonly TextBlock[] stepLabels = new TextBlock[3];
    private readonly TextBlock stepHeading, stepHint, reviewTime;
    private readonly Button next, back, save;
    private readonly ScrollViewer scroll;
    private readonly Expander repeatOptions;
    private readonly ComboBox mode, workTemplates;
    private readonly TextBox workReference;
    private readonly FrameworkElement personalTemplates, workFields;
    private bool workAuto;
    private int step;
    private bool ready, categoryManual, choosing;
    private string? selectedTemplate;
    private readonly Dictionary<ComboBox, (string Kind, Phrase[] Phrases)> phraseCombos = new();
    private readonly List<int> artworkVariants = [-1];
    private string artworkContext = "";

    internal ComposerWindow(Reminder? edit = null, Template? template = null)
    {
        original = edit;
        Title = edit is null ? L.T("Build a little quest · QuestBell", "Φτιάξε μια μικρή αποστολή · QuestBell") : L.T("Edit quest · QuestBell", "Επεξεργασία αποστολής · QuestBell");
        Width = 1100; Height = 1000; MinWidth = 850; MinHeight = 680; Ui.ApplyWindow(this);
        var outer = new DockPanel { Margin = new Thickness(27, 20, 20, 12), Background = Ui.Brush("#0D121B") }; Content = outer;
        var footer = new DockPanel { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false }; DockPanel.SetDock(footer, Dock.Bottom); outer.Children.Add(footer);
        save = Ui.Button(edit is null ? L.T("Create reminder ✦", "Δημιουργία υπενθύμισης ✦") : L.T("Save changes ✦", "Αποθήκευση αλλαγών ✦"), Save, true); save.MinWidth = 180;
        next = Ui.Button(L.T("Next →", "Επόμενο →"), () => GoToStep(step + 1), true); next.MinWidth = 160;
        back = Ui.Button(L.T("← Back", "← Πίσω"), () => GoToStep(step - 1));
        DockPanel.SetDock(save, Dock.Right); footer.Children.Add(save);
        DockPanel.SetDock(next, Dock.Right); footer.Children.Add(next);
        footer.Children.Add(Ui.Button(L.T("Cancel", "Ακύρωση"), Close)); footer.Children.Add(back);
        error = Ui.Text("", 13, Ui.Brush("#FFB5A7")); DockPanel.SetDock(error, Dock.Bottom); outer.Children.Add(error);
        var fields = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        scroll = new ScrollViewer { Content = fields }; outer.Children.Add(scroll);
        var progress = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 14) };
        var labels = new[] { L.T("1 · What", "1 · Τι"), L.T("2 · When", "2 · Πότε"), L.T("3 · Review", "3 · Έλεγχος") };
        for (int i = 0; i < 3; i++) { stepLabels[i] = Ui.Text(labels[i], 14, Ui.Muted, true); progress.Children.Add(stepLabels[i]); }
        fields.Children.Add(progress);
        stepHeading = Ui.Text("", 26, null, true); stepHint = Ui.Text("", 14, Ui.Muted);
        fields.Children.Add(stepHeading); fields.Children.Add(stepHint);
        previewTitle = Ui.Text("", 22, null, true); picture = new ContentControl();
        fields.Children.Add(Ui.Card(Ui.Columns(Ui.Col(Ui.Text(L.T("YOUR QUEST", "Η ΑΠΟΣΤΟΛΗ ΣΟΥ"), 10, Ui.Mint, true), previewTitle), picture, 3, 1), "#192E34", 12));
        foreach (var page in pages) fields.Children.Add(page);
        fields = pages[0];
        bool initialWork = edit?.IsWork ?? (template is null && host.Data.Settings.WorkMode);
        mode = Ui.Combo(new[] { L.T("Personal", "Προσωπικά"), L.T("Work", "Εργασία") }, initialWork ? 1 : 0);
        fields.Children.Add(Ui.Field(L.T("Reminder mode", "Λειτουργία υπενθύμισης"), mode));
        var quickReminders = host.Data.Settings.QuickReminders.ToArray();
        var quick = Ui.Combo(new[] { L.T("Choose your own quick reminder…", "Διάλεξε δική σου γρήγορη υπενθύμιση…") }.Concat(quickReminders.Select(r => (r.Work ? L.T("Work · ", "Εργασία · ") : L.T("Personal · ", "Προσωπικά · ")) + r.Title)));
        quick.Name = "UserQuickReminders";
        quick.MaxDropDownHeight = 320;
        var quickField = Ui.Field(L.T("YOUR QUICK REMINDERS", "ΟΙ ΓΡΗΓΟΡΕΣ ΥΠΕΝΘΥΜΙΣΕΙΣ ΣΟΥ"), quick);
        quickField.Visibility = quickReminders.Length > 0 ? Visibility.Visible : Visibility.Collapsed; fields.Children.Add(quickField);
        quick.SelectionChanged += (_, _) => { if (ready && quick.SelectedIndex > 0) ChooseQuick(quickReminders[quick.SelectedIndex - 1]); };
        var templates = new ComboBox();
        var sorted = Catalog.Templates.OrderBy(t => { int i = host.Data.RecentTemplates.IndexOf(t.Id); return i < 0 ? 999 : i; }).ToList();
        templates.Items.Add(L.T("Choose a ready-made reminder…", "Διάλεξε έτοιμη υπενθύμιση…"));
        foreach (var t in sorted) templates.Items.Add(Catalog.Title(t, L.Language)); templates.SelectedIndex = 0;
        personalTemplates = Ui.Field(L.T("QUICK START · templates & recent choices", "ΓΡΗΓΟΡΗ ΕΚΚΙΝΗΣΗ · πρότυπα & πρόσφατα"), templates); fields.Children.Add(personalTemplates);
        workTemplates = Ui.Combo(WorkCatalog.Templates.Select(t => t.Label(L.Language)), Math.Max(0, Array.FindIndex(WorkCatalog.Templates, t => t.Id == edit?.WorkTemplateId)));
        workTemplates.MaxDropDownHeight = 350;
        workReference = new TextBox { Text = edit?.WorkReference ?? "", MaxLength = 200 };
        workFields = Ui.Col(Ui.Field(L.T("WORK QUICK START", "ΓΡΗΓΟΡΗ ΕΚΚΙΝΗΣΗ ΕΡΓΑΣΙΑΣ"), workTemplates),
            Ui.Field(L.T("Client, order number, product or topic (optional)", "Πελάτης, αριθμός παραγγελίας, προϊόν ή θέμα (προαιρετικό)"), workReference),
            Ui.Button(L.T("Use this template", "Χρήση αυτού του προτύπου"), ChooseWork),
            Ui.Text(L.T("Edit the final text below if you like. Use this template rebuilds it from your reference.", "Μπορείς να αλλάξεις το τελικό κείμενο παρακάτω. Η Χρήση προτύπου το συνθέτει ξανά με τα στοιχεία σου."), 11, Ui.Muted));
        fields.Children.Add(workFields);
        action = PhraseCombo("action", Catalog.Actions); subject = PhraseCombo("subject", Catalog.Subjects); obj = PhraseCombo("object", Catalog.Objects);
        action.Text = Catalog.Label("action", edit?.ActionId ?? "pay", L.Language); subject.Text = Catalog.Label("subject", edit?.SubjectId ?? "phone", L.Language); obj.Text = Catalog.Label("object", edit?.ObjectId ?? "bill", L.Language);
        puzzle = new Grid(); for (int i = 0; i < 3; i++) puzzle.ColumnDefinitions.Add(new ColumnDefinition());
        var pieces = new[] { Ui.Field(L.T("1 · Action", "1 · Ενέργεια"), action), Ui.Field(L.T("2 · Subject", "2 · Θέμα"), subject), Ui.Field(L.T("3 · Item", "3 · Αντικείμενο"), obj) };
        for (int i = 0; i < 3; i++) { Grid.SetColumn(pieces[i], i); puzzle.Children.Add(pieces[i]); } fields.Children.Add(puzzle);
        phraseHint = Ui.Text(L.T("Choose words or type your own. We’ll save them for next time.", "Διάλεξε λέξεις ή πρόσθεσε δικές σου. Θα αποθηκευτούν για την επόμενη φορά."), 11, Ui.Muted); fields.Children.Add(phraseHint);
        custom = new CheckBox { Content = L.T("Write a fully custom reminder instead", "Γράψε μια εντελώς δική σου υπενθύμιση"), IsChecked = initialWork || (edit?.CustomTitle ?? false) };
        title = new TextBox { Text = edit is null ? "" : Catalog.Title(edit, L.Language), MaxLength = 500, Visibility = custom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed };
        fields.Children.Add(custom); fields.Children.Add(title);
        UpdateTitleMode();
        custom.Click += (_, _) => { UpdateTitleMode(); Update(); }; title.TextChanged += (_, _) => { if (ready && !choosing) workAuto = false; Update(); };
        fields = pages[1];
        var initial = edit?.NextAt ?? DateTime.Now.AddHours(1);
        date = new DatePicker { SelectedDate = initial.Date, Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.Name) };
        hour = Ui.Combo(Enumerable.Range(0, 24).Select(x => x.ToString("00")), initial.Hour, 77); hour.IsEditable = true;
        minute = Ui.Combo(Enumerable.Range(0, 60).Select(x => x.ToString("00")), initial.Minute, 77); minute.IsEditable = true;
        repeat = Ui.Combo(Schedule.Repeats.Select(L.Repeat), Math.Max(0, Array.IndexOf(Schedule.Repeats, edit?.Repeat ?? "none")));
        fields.Children.Add(Ui.TimingFields(date, Ui.Row(hour, Ui.Text(":", 21, Ui.Muted), minute), host.Data.Settings.QuickTimes, ApplyQuickTime,
            Ui.Field(L.T("Repeat", "Επανάληψη"), repeat, L.T("Choose One time for a single reminder. For repeating reminders, use Repeat options to set the interval or weekdays.", "Διάλεξε Μία φορά για μία υπενθύμιση. Για επανάληψη, όρισε διάστημα ή ημέρες στις Επιλογές επανάληψης."))));
        var delayPresets = new WrapPanel();
        foreach (var time in host.Data.Settings.QuickTimes.Where(t => t.Kind == "delay")) delayPresets.Children.Add(Ui.Button(QuickSettingsWindow.TimeLabel(time), () => ApplyQuickTime(time)));
        var delays = Catalog.Delays; var relative = Ui.Combo(delays.Select(L.Delay), 0, 195);
        var relativeRow = Ui.Row(Ui.Button(L.T("Remind me in…", "Θύμισέ το σε…"), () => { var d = delays[relative.SelectedIndex]; SetTime(Schedule.Add(DateTime.Now, d.Value, d.Unit)); }), relative,
            Ui.Help(L.T("Remind me in…", "Θύμισέ το σε…"), L.T("Choose a delay on the right, then click the button to set the date and time from now.", "Διάλεξε διάστημα δεξιά και πάτησε το κουμπί για να ορίσεις ημερομηνία και ώρα από τώρα.")));
        ((Button)relativeRow.Children[0]).Name = "RelativeDelayAction";
        var customValue = new TextBox { Text = "1", Width = 80, MaxLength = 4 }; var units = new[] { "minutes", "hours", "days", "weeks", "months" }; var customUnit = Ui.Combo(units.Select(u => L.Unit(u, 2)), 1, 150);
        var customRelative = Ui.Row(Ui.Button(L.T("Remind me in…", "Θύμισέ το σε…"), () =>
        { try { if (!int.TryParse(customValue.Text, out int n) || n < 1 || n > 1200) throw new ArgumentException(L.T("Choose a number from 1 to 1200.", "Διάλεξε αριθμό από 1 έως 1200.")); SetTime(Schedule.Add(DateTime.Now, n, units[customUnit.SelectedIndex])); } catch (Exception e) { if (error is not null) error.Text = e.Message; } }), customValue, customUnit,
            Ui.Help(L.T("Custom interval", "Προσαρμοσμένο διάστημα"), L.T("Enter 1–1200 and choose a unit on the right. Click Remind me in… to set the reminder that far from now.", "Δώσε 1–1200 και διάλεξε μονάδα δεξιά. Πάτησε Θύμισέ το σε… για να ορίσεις την υπενθύμιση με αφετηρία τώρα.")));
        ((Button)customRelative.Children[0]).Name = "CustomDelayAction";
        interval = new TextBox { Text = (edit?.Interval ?? 1).ToString(), MaxLength = 3, Width = 80, HorizontalAlignment = HorizontalAlignment.Left };
        var weekdayRow = new WrapPanel();
        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday })
        { var check = new CheckBox { Content = L.Culture.DateTimeFormat.GetAbbreviatedDayName(day), Tag = day, IsChecked = edit?.Weekdays.Contains(day) ?? (day != DayOfWeek.Saturday && day != DayOfWeek.Sunday) }; weekdayChecks.Add(check); weekdayRow.Children.Add(check); }
        weekdayRow.Visibility = repeat.SelectedIndex == 5 ? Visibility.Visible : Visibility.Collapsed;
        interval.IsEnabled = repeat.SelectedIndex is > 0 and < 5;
        after = new CheckBox { Content = L.T("Repeat after completion instead of the original schedule", "Επανάληψη μετά την ολοκλήρωση αντί του αρχικού προγράμματος"), IsChecked = edit?.AfterCompletion ?? false };
        hasDue = new CheckBox { Content = L.T("Add a separate due date", "Πρόσθεσε ξεχωριστή προθεσμία"), IsChecked = edit?.DueAt.HasValue ?? false };
        dueDate = new DatePicker { SelectedDate = edit?.DueAt?.Date ?? initial.Date, IsEnabled = hasDue.IsChecked == true };
        hasDue.Click += (_, _) => dueDate.IsEnabled = hasDue.IsChecked == true;
        amount = new TextBox { Text = edit?.Amount ?? "", MaxLength = 100 }; url = new TextBox { Text = edit?.Url ?? "", MaxLength = 2000 };
        note = new TextBox { Text = edit?.Note ?? "", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 85, MaxLength = 10000, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var optional = Ui.Col(hasDue, dueDate, Ui.Field(L.T("Amount · e.g. €35 (optional)", "Ποσό · π.χ. €35 (προαιρετικό)"), amount), Ui.Field(L.T("Website · https://… (optional)", "Ιστότοπος · https://… (προαιρετικό)"), url), Ui.Field(L.T("Notes (optional)", "Σημειώσεις (προαιρετικό)"), note));
        category = Ui.Combo(Catalog.Categories.Select(x => x.Label(L.Language)), Math.Max(0, Array.IndexOf(Catalog.CategoryIds, edit?.Category ?? "bills")));
        variant = Ui.Combo(new[] { L.T("Automatic · match my task", "Αυτόματα · ταίριασμα με την εργασία") });
        variant.MaxDropDownHeight = 330;
        if (edit?.ArtVariant >= 0) { artworkVariants.Add(edit.ArtVariant); variant.Items.Add(Artwork.Get(edit.Category, edit.ArtVariant).Label(L.Language)); variant.SelectedIndex = 1; }
        pin = new CheckBox { Content = L.T("Pin this artwork for every occurrence", "Κράτησε αυτή την εικόνα σε κάθε εμφάνιση"), IsChecked = edit?.PinArt ?? false };
        category.SelectionChanged += (_, _) => { if (!choosing) categoryManual = true; Update(); }; variant.SelectionChanged += (_, _) => Update();
        sound = new CheckBox { Content = L.T("Play a gentle chime", "Απαλός ήχος υπενθύμισης"), IsChecked = edit?.Sound ?? true };
        follow = new CheckBox { Content = L.T("Automatic follow-up chime while still pending", "Αυτόματη ηχητική επανάληψη όσο εκκρεμεί"), IsChecked = edit?.FollowUpMinutes > 0 };
        var followValues = new[] { 60, 360, 1440, 10080 }; followDelay = Ui.Combo(new[] { L.T("Every hour", "Κάθε ώρα"), L.T("Every 6 hours", "Κάθε 6 ώρες"), L.T("Every day", "Κάθε ημέρα"), L.T("Every week", "Κάθε εβδομάδα") }, edit?.FollowUpMinutes > 0 ? Math.Max(0, Array.IndexOf(followValues, edit.FollowUpMinutes)) : 2);
        var artwork = Ui.Col(Ui.Columns(Ui.Field(L.T("Illustration family", "Οικογένεια εικόνας"), category), Ui.Field(L.T("Item or companion", "Αντικείμενο ή μικρή παρέα"), variant)), Ui.Text(L.T("Task matches appear first. Automatic rotates related items and pets without repeating the last one.", "Οι σχετικές εικόνες εμφανίζονται πρώτες. Η αυτόματη επιλογή εναλλάσσει σχετικά αντικείμενα και ζωάκια χωρίς άμεση επανάληψη."), 12, Ui.Muted), pin, sound, follow, followDelay);
        Border Section(string en, string el, UIElement content) => Ui.Card(Ui.Col(Ui.Eyebrow(L.T(en, el)), content), padding: 16);
        var timingAlternative = Ui.Text(L.T("OR", "Ή"), 28, Ui.Muted, true);
        timingAlternative.Name = "TimingAlternative"; timingAlternative.TextAlignment = TextAlignment.Center; timingAlternative.Margin = new Thickness(0, 8, 0, 18);
        fields.Children.Add(new Expander { Header = L.T("More timing choices", "Περισσότερες επιλογές ώρας"), Content = Ui.Col(timingAlternative, delayPresets, relativeRow, customRelative), Margin = new Thickness(0, 12, 0, 12) });
        repeatOptions = new Expander { Header = L.T("Repeat options", "Επιλογές επανάληψης"), Content = Ui.Col(Ui.Field(L.T("Every X periods · 1–365", "Κάθε Χ περιόδους · 1–365"), interval,
            L.T("Uses the repeat unit above: 2 with Weekly means every two weeks. Selected weekdays uses the checked days instead.", "Χρησιμοποιεί τη μονάδα επανάληψης: 2 με Κάθε εβδομάδα σημαίνει κάθε δύο εβδομάδες. Οι Επιλεγμένες ημέρες χρησιμοποιούν τις ημέρες που επέλεξες.")), weekdayRow,
            Ui.Row(after, Ui.Help(L.T("Repeat after completion", "Επανάληψη μετά την ολοκλήρωση"), L.T("Start the next interval when you mark the reminder Done. Otherwise, repeats stay tied to the original schedule.", "Το επόμενο διάστημα ξεκινά όταν πατήσεις Έγινε. Διαφορετικά, η επανάληψη ακολουθεί το αρχικό πρόγραμμα.")))), IsExpanded = repeat.SelectedIndex == 5 };
        fields.Children.Add(repeatOptions);
        fields = pages[2];
        reviewTime = Ui.Text("", 16, Ui.Mint);
        fields.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("Your schedule", "Το πρόγραμμά σου")), reviewTime, Ui.Row(Ui.Button(L.T("Change reminder", "Αλλαγή υπενθύμισης"), () => GoToStep(0)), Ui.Button(L.T("Change time", "Αλλαγή ώρας"), () => GoToStep(1)))), padding: 16));
        var advanced = Ui.Col(
            Section("Extra details", "Πρόσθετες λεπτομέρειες", optional),
            Section("Artwork & sound", "Εικόνα & ήχος", artwork));
        advanced.Margin = new Thickness(0, 12, 0, 0);
        optionsSummary = Ui.Text("", 12, Ui.Muted); optionsSummary.MaxWidth = 680;
        moreOptions = new Expander { Header = Ui.Col(Ui.Text(L.T("More options", "Περισσότερες επιλογές"), 15, Ui.Mint, true), optionsSummary), Content = advanced, IsExpanded = false, Margin = new Thickness(0, 4, 0, 4) };
        fields.Children.Add(moreOptions);
        repeat.SelectionChanged += (_, _) =>
        {
            weekdayRow.Visibility = repeat.SelectedIndex == 5 ? Visibility.Visible : Visibility.Collapsed; interval.IsEnabled = repeat.SelectedIndex is > 0 and < 5;
            if (ready && repeat.SelectedIndex == 5) { repeatOptions.IsExpanded = true; Dispatcher.BeginInvoke(() => weekdayRow.BringIntoView()); }
            UpdateOptionsSummary();
        };
        foreach (var input in new[] { interval, amount, url, note }) input.TextChanged += (_, _) => UpdateOptionsSummary();
        foreach (var check in new[] { after, hasDue, pin, sound, follow }.Concat(weekdayChecks))
        { check.Checked += (_, _) => UpdateOptionsSummary(); check.Unchecked += (_, _) => UpdateOptionsSummary(); }
        templates.SelectionChanged += (_, _) => { if (templates.SelectedIndex > 0) Choose(sorted[templates.SelectedIndex - 1]); };
        foreach (var combo in new[] { action, subject, obj })
        { combo.SelectionChanged += (_, _) => Dispatcher.BeginInvoke(Update); combo.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => Update())); }
        action.SelectionChanged += (_, _) =>
        {
            if (!ready || choosing || custom.IsChecked == true) return;
            string a = Id(action); string preferred = a switch { "pay" => "bill", "cancel" => "subscription", "book" => "appointment", "buy" => "supplies", "collect" => "parcel", "replace" => "filter", "submit" => "documents", _ => Id(obj) };
            obj.Text = Catalog.Label("object", preferred, L.Language); Update();
        };
        categoryManual = edit is not null; ready = true;
        workAuto = initialWork && edit is not null && !edit.CustomTitle && WorkCatalog.Find(edit.WorkTemplateId) is not null;
        mode.SelectionChanged += (_, _) =>
        {
            if (mode.SelectedIndex == 1) { custom.IsChecked = true; if (string.IsNullOrWhiteSpace(title.Text)) ChooseWork(); }
            UpdateTitleMode(); Update();
        };
        workTemplates.SelectionChanged += (_, _) => { if (ready && mode.SelectedIndex == 1) ChooseWork(); };
        workReference.TextChanged += (_, _) =>
        {
            if (!ready || mode.SelectedIndex != 1 || !workAuto) return;
            choosing = true; title.Text = WorkCatalog.Title(WorkCatalog.Templates[workTemplates.SelectedIndex], workReference.Text, L.Language); choosing = false; Update();
        };
        if (initialWork && edit is null) ChooseWork(); else if (template is not null) Choose(template); else Update();
        ShowStep();
    }
    private void GoToStep(int target)
    {
        try
        {
            if (target > step)
            {
                if (step == 0) ValidateTitle();
                if (step == 1) ValidateTime();
            }
            step = Math.Clamp(target, 0, 2); error.Text = ""; ShowStep();
        }
        catch (Exception e) { error.Text = e.Message; }
    }
    private void ShowStep()
    {
        for (int i = 0; i < 3; i++) { pages[i].Visibility = i == step ? Visibility.Visible : Visibility.Collapsed; stepLabels[i].Foreground = i == step ? Ui.Mint : Ui.Muted; }
        stepHeading.Text = step switch { 0 => L.T("What should we remember?", "Τι να σου θυμίσουμε;"), 1 => L.T("Pick the right moment.", "Διάλεξε την κατάλληλη στιγμή."), _ => L.T("Ready when you are.", "Όλα έτοιμα για σένα.") };
        stepHint.Text = step switch { 0 => L.T("Start with a template, build a phrase, or use your own words.", "Διάλεξε πρότυπο, σύνθεσε μια φράση ή γράψε κάτι δικό σου."), 1 => L.T("Choose a date and time. Repeat only if you need to.", "Διάλεξε ημερομηνία και ώρα. Πρόσθεσε επανάληψη αν τη χρειάζεσαι."), _ => L.T("Check your reminder. Add details or a favorite companion if you like.", "Έλεγξε την υπενθύμιση. Πρόσθεσε λεπτομέρειες ή την αγαπημένη σου παρέα αν θέλεις.") };
        if (step == 1 && mode.SelectedIndex == 1 && workAuto && WorkCatalog.Templates[workTemplates.SelectedIndex].Id == "ship-order")
            stepHint.Text = L.T("Choose the shipping date and the time you want to be reminded.", "Διάλεξε την ημερομηνία αποστολής και την ώρα υπενθύμισης.");
        back.Visibility = step > 0 ? Visibility.Visible : Visibility.Collapsed;
        next.Visibility = step < 2 ? Visibility.Visible : Visibility.Collapsed;
        save.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        next.IsDefault = step < 2; save.IsDefault = step == 2;
        if (step == 2)
        {
            var when = date.SelectedDate!.Value.Date.AddHours(int.Parse(hour.Text)).AddMinutes(int.Parse(minute.Text));
            reviewTime.Text = when.ToString("ddd d MMM yyyy · HH:mm", L.Culture) + " · " + L.Repeat(Schedule.Repeats[repeat.SelectedIndex]);
            if (repeat.SelectedIndex is > 0 and < 5 && interval.Text != "1") reviewTime.Text += L.T(" · Every ", " · Κάθε ") + interval.Text + L.T(" periods", " περιόδους");
            if (repeat.SelectedIndex == 5) reviewTime.Text += "\n" + string.Join(", ", weekdayChecks.Where(c => c.IsChecked == true).Select(c => c.Content));
            if (repeat.SelectedIndex > 0 && after.IsChecked == true) reviewTime.Text += "\n" + L.T("Repeats after completion", "Επανάληψη μετά την ολοκλήρωση");
            if (when <= DateTime.Now) reviewTime.Text += "\n" + L.T("This time has passed — it will be ready to remind you immediately.", "Η ώρα έχει περάσει — θα είναι έτοιμη για άμεση υπενθύμιση.");
        }
        scroll.ScrollToTop();
    }
    private void ValidateTitle()
    {
        if (custom.IsChecked == true ? string.IsNullOrWhiteSpace(title.Text) : new[] { action.Text, subject.Text, obj.Text }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(L.T("Give your quest a title or choose all three phrase pieces.", "Δώσε τίτλο ή συμπλήρωσε και τα τρία κομμάτια της φράσης."));
        if (new[] { action.Text, subject.Text, obj.Text }.Any(t => t.Length > 100))
            throw new ArgumentException(L.T("Each phrase may contain up to 100 characters.", "Κάθε φράση μπορεί να έχει έως 100 χαρακτήρες."));
    }
    private void ValidateTime()
    {
        if (!date.SelectedDate.HasValue || !int.TryParse(hour.Text, out int h) || h is < 0 or > 23 || !int.TryParse(minute.Text, out int m) || m is < 0 or > 59)
            throw new ArgumentException(L.T("Choose a valid date and time.", "Διάλεξε έγκυρη ημερομηνία και ώρα."));
        if (!int.TryParse(interval.Text, out int n) || n is < 1 or > 365)
        { repeatOptions.IsExpanded = true; throw new ArgumentException(L.T("Repeat interval must be between 1 and 365.", "Το διάστημα επανάληψης πρέπει να είναι από 1 έως 365.")); }
        if (repeat.SelectedIndex == 5 && !weekdayChecks.Any(c => c.IsChecked == true))
        { repeatOptions.IsExpanded = true; throw new ArgumentException(L.T("Select at least one weekday.", "Διάλεξε τουλάχιστον μία ημέρα.")); }
    }
    private ComboBox PhraseCombo(string kind, Phrase[] phrases)
    {
        var c = Ui.Combo(phrases.Select(p => p.Label(L.Language))); c.IsEditable = true; c.IsTextSearchEnabled = false;
        foreach (var word in host.Data.CustomPhrases[kind]) c.Items.Add(word);
        phraseCombos[c] = (kind, phrases); return c;
    }
    private string Id(ComboBox combo)
    {
        var text = combo.Text.Trim(); var list = phraseCombos[combo].Phrases;
        return list.FirstOrDefault(p => p.Label(L.Language).Equals(text, StringComparison.CurrentCultureIgnoreCase))?.Id ?? "=" + text;
    }
    private void Choose(Template template)
    {
        choosing = true; selectedTemplate = template.Id; custom.IsChecked = false; UpdateTitleMode();
        action.Text = Catalog.Label("action", template.Action, L.Language); subject.Text = Catalog.Label("subject", template.Subject, L.Language); obj.Text = Catalog.Label("object", template.Object, L.Language);
        category.SelectedIndex = Array.IndexOf(Catalog.CategoryIds, template.Category); variant.SelectedIndex = 0; artworkContext = ""; categoryManual = false; choosing = false; Update();
    }
    private void UpdateTitleMode()
    {
        bool work = mode.SelectedIndex == 1;
        workFields.Visibility = work ? Visibility.Visible : Visibility.Collapsed;
        personalTemplates.Visibility = custom.Visibility = work ? Visibility.Collapsed : Visibility.Visible;
        bool useCustom = custom.IsChecked == true;
        title.Visibility = useCustom ? Visibility.Visible : Visibility.Collapsed;
        puzzle.Visibility = phraseHint.Visibility = useCustom ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ChooseWork()
    {
        var work = WorkCatalog.Templates[workTemplates.SelectedIndex];
        choosing = true; workAuto = true; custom.IsChecked = true; selectedTemplate = null;
        title.Text = WorkCatalog.Title(work, workReference.Text, L.Language);
        category.SelectedIndex = Array.IndexOf(Catalog.CategoryIds, work.Category); categoryManual = true;
        repeat.SelectedIndex = Array.IndexOf(Schedule.Repeats, work.Repeat); interval.Text = "1"; after.IsChecked = false;
        variant.SelectedIndex = 0; artworkContext = ""; choosing = false; UpdateTitleMode(); Update();
    }
    private void ChooseQuick(QuickReminder quick)
    {
        choosing = true; workAuto = false; selectedTemplate = null;
        title.Text = quick.Title; mode.SelectedIndex = quick.Work ? 1 : 0; custom.IsChecked = true; workReference.Text = "";
        category.SelectedIndex = Array.IndexOf(Catalog.CategoryIds, quick.Category); categoryManual = true;
        repeat.SelectedIndex = Array.IndexOf(Schedule.Repeats, quick.Repeat); interval.Text = "1"; after.IsChecked = false;
        variant.SelectedIndex = 0; artworkContext = ""; choosing = false; UpdateTitleMode(); Update();
    }
    private void ApplyQuickTime(QuickTime time)
    {
        try
        {
            DateTime current = (date.SelectedDate ?? DateTime.Today).Date;
            if (int.TryParse(hour.Text, out int h) && h is >= 0 and <= 23 && int.TryParse(minute.Text, out int m) && m is >= 0 and <= 59) current = current.AddHours(h).AddMinutes(m);
            else current = current.Add(DateTime.Now.TimeOfDay);
            SetTime(QuickOptions.Apply(time, current, DateTime.Now)); error.Text = "";
        }
        catch (Exception e) { error.Text = e.Message; }
    }
    private void Update()
    {
        if (!ready || choosing) return;
        workReference.IsEnabled = workAuto;
        var text = custom.IsChecked == true ? title.Text.Trim() : Catalog.Title(Id(action), Id(subject), Id(obj), L.Language);
        previewTitle.Text = string.IsNullOrWhiteSpace(text) ? L.T("Your next little quest", "Η επόμενη μικρή αποστολή") : text;
        if (!categoryManual && !choosing)
        {
            choosing = true;
            category.SelectedIndex = Array.IndexOf(Catalog.CategoryIds, custom.IsChecked == true ? Catalog.MatchCategory("", "", "", text) : Catalog.MatchCategory(Id(action), Id(subject), Id(obj), text));
            choosing = false;
        }
        var task = new Reminder { Title = text, CustomTitle = custom.IsChecked == true, ActionId = Id(action), SubjectId = Id(subject), ObjectId = Id(obj), Category = Catalog.CategoryIds[Math.Max(0, category.SelectedIndex)] };
        task.IsWork = mode.SelectedIndex == 1; task.WorkTemplateId = task.IsWork && workAuto ? WorkCatalog.Templates[workTemplates.SelectedIndex].Id : "";
        string context = task.Category + ":" + Artwork.Topic(task);
        if (context != artworkContext)
        {
            choosing = true;
            int selected = artworkVariants[Math.Max(0, variant.SelectedIndex)];
            var suggested = Artwork.Candidates(task);
            var options = Artwork.Choices(task).OrderBy(a => Array.IndexOf(suggested, a.Variant) < 0 ? 1 : 0).ToList();
            // An existing pinned choice remains available even if its family was changed.
            if (selected >= 0 && options.All(a => a.Variant != selected)) options.Add(Artwork.Get(task.Category, selected));
            variant.Items.Clear(); artworkVariants.Clear(); artworkVariants.Add(-1);
            variant.Items.Add(L.T("Automatic · match my task", "Αυτόματα · ταίριασμα με την εργασία"));
            foreach (var art in options) { artworkVariants.Add(art.Variant); variant.Items.Add(art.Label(L.Language)); }
            variant.SelectedIndex = Math.Max(0, artworkVariants.IndexOf(selected)); artworkContext = context;
            choosing = false;
        }
        int chosen = artworkVariants[Math.Max(0, variant.SelectedIndex)];
        picture.Content = Art.Image(task.Category, chosen >= 0 ? chosen : Artwork.Preferred(task), 80);
        UpdateOptionsSummary();
    }
    private void UpdateOptionsSummary()
    {
        if (!ready) return;
        var parts = new List<string>();
        if (repeat.SelectedIndex is > 0 and < 5 && interval.Text != "1") parts.Add(L.T("Every ", "Κάθε ") + interval.Text + L.T(" periods", " περιόδους"));
        if (repeat.SelectedIndex == 5) parts.Add(string.Join(", ", weekdayChecks.Where(c => c.IsChecked == true).Select(c => c.Content)));
        if (after.IsChecked == true) parts.Add(L.T("After completion", "Μετά την ολοκλήρωση"));
        if (hasDue.IsChecked == true) parts.Add(L.T("Due date", "Προθεσμία"));
        if (!string.IsNullOrWhiteSpace(amount.Text)) parts.Add(L.T("Amount", "Ποσό"));
        if (!string.IsNullOrWhiteSpace(url.Text)) parts.Add(L.T("Link", "Σύνδεσμος"));
        if (!string.IsNullOrWhiteSpace(note.Text)) parts.Add(L.T("Notes", "Σημειώσεις"));
        if (variant.SelectedIndex > 0 || pin.IsChecked == true) parts.Add(L.T("Chosen artwork", "Επιλεγμένη εικόνα"));
        if (sound.IsChecked != true) parts.Add(L.T("Silent", "Χωρίς ήχο"));
        if (follow.IsChecked == true) parts.Add(L.T("Follow-up chime", "Επαναληπτικός ήχος"));
        optionsSummary.Text = parts.Count > 0 ? L.T("In use: ", "Σε χρήση: ") + string.Join(" · ", parts) : L.T("Due date, amount, link, notes, artwork & sound", "Προθεσμία, ποσό, σύνδεσμος, σημειώσεις, εικόνα & ήχος");
    }
    private void SetTime(DateTime time) { date.SelectedDate = time.Date; hour.Text = time.Hour.ToString("00"); minute.Text = time.Minute.ToString("00"); }
    private void Save()
    {
        try
        {
            string text = custom.IsChecked == true ? title.Text.Trim() : Catalog.Title(Id(action), Id(subject), Id(obj), L.Language);
            if (string.IsNullOrWhiteSpace(text) || text.Length > 500 || (custom.IsChecked != true && new[] { action.Text, subject.Text, obj.Text }.Any(string.IsNullOrWhiteSpace)))
                throw new ArgumentException(L.T("Give your quest a title or choose all three phrase pieces.", "Δώσε τίτλο ή συμπλήρωσε και τα τρία κομμάτια της φράσης."));
            if (new[] { action.Text, subject.Text, obj.Text }.Any(t => t.Length > 100)) throw new ArgumentException(L.T("Each phrase may contain up to 100 characters.", "Κάθε φράση μπορεί να έχει έως 100 χαρακτήρες."));
            if (!date.SelectedDate.HasValue || !int.TryParse(hour.Text, out int h) || h is < 0 or > 23 || !int.TryParse(minute.Text, out int m) || m is < 0 or > 59)
                throw new ArgumentException(L.T("Choose a valid date and time.", "Διάλεξε έγκυρη ημερομηνία και ώρα."));
            if (!int.TryParse(interval.Text, out int n) || n is < 1 or > 365) throw new ArgumentException(L.T("Repeat interval must be between 1 and 365.", "Το διάστημα επανάληψης πρέπει να είναι από 1 έως 365."));
            var days = weekdayChecks.Where(x => x.IsChecked == true).Select(x => (DayOfWeek)x.Tag).ToList();
            if (repeat.SelectedIndex == 5 && days.Count == 0) throw new ArgumentException(L.T("Select at least one weekday.", "Διάλεξε τουλάχιστον μία ημέρα."));
            if (hasDue.IsChecked == true && !dueDate.SelectedDate.HasValue) throw new ArgumentException(L.T("Choose a due date.", "Διάλεξε προθεσμία."));
            string link = url.Text.Trim(); if (link.Length > 0) { if (!Schedule.SafeUrl(link, out var safe)) throw new ArgumentException(L.T("Use an http or https website address.", "Χρησιμοποίησε διεύθυνση ιστοτόπου http ή https.")); link = safe!.AbsoluteUri; }
            if (pin.IsChecked == true && variant.SelectedIndex == 0) variant.SelectedIndex = 1;
            var task = original?.Clone() ?? new Reminder();
            var scheduled = Schedule.Wall(date.SelectedDate.Value.Date.AddHours(h).AddMinutes(m));
            bool scheduleChanged = original is null || scheduled != original.NextAt || Schedule.Repeats[repeat.SelectedIndex] != original.Repeat || n != original.Interval || after.IsChecked != original.AfterCompletion || !days.SequenceEqual(original.Weekdays);
            task.Title = text; task.CustomTitle = custom.IsChecked == true; task.ActionId = Id(action); task.SubjectId = Id(subject); task.ObjectId = Id(obj);
            task.IsWork = mode.SelectedIndex == 1;
            task.WorkTemplateId = task.IsWork && workAuto ? WorkCatalog.Templates[workTemplates.SelectedIndex].Id : "";
            task.WorkReference = task.IsWork ? workReference.Text.Trim() : "";
            if (task.WorkTemplateId.Length > 0) task.CustomTitle = false;
            task.Category = Catalog.CategoryIds[Math.Max(0, category.SelectedIndex)]; task.NextAt = scheduled;
            if (scheduleChanged) { task.AnchorAt = task.OccurrenceAt = scheduled; task.Snoozed = false; }
            task.Repeat = Schedule.Repeats[repeat.SelectedIndex]; task.Interval = n; task.Weekdays = days; task.AfterCompletion = after.IsChecked == true;
            task.DueAt = hasDue.IsChecked == true ? dueDate.SelectedDate!.Value.Date.AddHours(23).AddMinutes(59) : null;
            task.Amount = amount.Text.Trim(); task.Url = link; task.Note = note.Text.Trim(); task.Sound = sound.IsChecked == true;
            task.FollowUpMinutes = follow.IsChecked == true ? new[] { 60, 360, 1440, 10080 }[followDelay.SelectedIndex] : 0;
            task.ArtVariant = artworkVariants[Math.Max(0, variant.SelectedIndex)]; task.PinArt = pin.IsChecked == true;
            if (original is not null && (task.Category != original.Category || task.SubjectId != original.SubjectId || task.ObjectId != original.ObjectId || task.Title != original.Title)) task.LastArtVariant = -1;
            task.Revision++; task.Finished = false;
            new AppData { Reminders = [task] }.Validate();
            if (original is null) host.Data.Reminders.Add(task); else host.Data.Reminders[host.Data.Reminders.IndexOf(original)] = task;
            foreach (var (combo, info) in phraseCombos)
            { string id = Id(combo); if (id.StartsWith('=') && !host.Data.CustomPhrases[info.Kind].Contains(id[1..])) host.Data.CustomPhrases[info.Kind].Add(id[1..]); }
            if (selectedTemplate is not null) { host.Data.RecentTemplates.Remove(selectedTemplate); host.Data.RecentTemplates.Insert(0, selectedTemplate); }
            if (host.CurrentReminder?.Id == task.Id) host.CloseReminder(); host.Save(); DialogResult = true; host.Tick();
        }
        catch (Exception e) { moreOptions.IsExpanded = true; error.Text = e.Message; }
    }
}

internal sealed class SnoozeWindow : Window
{
    internal DateTime Until { get; private set; }
    internal SnoozeWindow()
    {
        Title = L.T("A little later · QuestBell", "Λίγο αργότερα · QuestBell"); Width = 850; Height = 760; MinWidth = 850; MinHeight = 550; Ui.ApplyWindow(this);
        var outer = new StackPanel { Margin = new Thickness(26) }; Content = new ScrollViewer { Content = outer };
        outer.Children.Add(Ui.Eyebrow(L.T("LET IT REST A LITTLE", "ΑΣ ΠΕΡΙΜΕΝΕΙ ΛΙΓΟ"))); outer.Children.Add(Ui.Text(L.T("When shall we return?", "Πότε να επιστρέψουμε;"), 27, null, true));
        foreach (var group in Catalog.Delays.GroupBy(x => x.Unit))
        {
            var wrap = new WrapPanel();
            foreach (var delay in group) wrap.Children.Add(Ui.Button(L.Delay(delay), () => Pick(Schedule.Add(DateTime.Now, delay.Value, delay.Unit))));
            outer.Children.Add(wrap);
        }
        var initial = DateTime.Now.AddHours(1);
        var customDate = new DatePicker { SelectedDate = initial.Date, Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.Name) };
        var customTime = new TextBox { Text = initial.ToString("HH:mm"), Width = 95, HorizontalAlignment = HorizontalAlignment.Left };
        var error = Ui.Text("", 12, Ui.Gold);
        outer.Children.Add(Ui.Text(L.T("Or choose a date and time", "Ή διάλεξε ημερομηνία και ώρα"), 14, Ui.Muted, true));
        outer.Children.Add(Ui.TimingFields(customDate, customTime, App.Instance.Data.Settings.QuickTimes, choice =>
        {
            if (!customDate.SelectedDate.HasValue || !TimeSpan.TryParse(customTime.Text, out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
            { error.Text = L.T("Use a valid date and HH:mm time.", "Δώσε έγκυρη ημερομηνία και ώρα ΩΩ:λλ."); return; }
            var selected = QuickOptions.Apply(choice, customDate.SelectedDate.Value.Date + time, DateTime.Now);
            customDate.SelectedDate = selected.Date; customTime.Text = selected.ToString("HH:mm"); error.Text = "";
        }));
        outer.Children.Add(Ui.Button(L.T("Snooze until then", "Αναβολή έως τότε"), () =>
        {
            if (!customDate.SelectedDate.HasValue || !TimeSpan.TryParse(customTime.Text, out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) { error.Text = L.T("Use a valid date and HH:mm time.", "Δώσε έγκυρη ημερομηνία και ώρα ΩΩ:λλ."); return; }
            var until = Schedule.Wall(customDate.SelectedDate.Value.Date + time);
            if (until <= DateTime.Now) { error.Text = L.T("Choose a time in the future.", "Διάλεξε μελλοντική ώρα."); return; } Pick(until);
        }, true));
        var count = new TextBox { Text = "1", Width = 70 }; var units = new[] { "minutes", "hours", "days", "weeks", "months" }; var unit = Ui.Combo(units.Select(u => L.Unit(u, 2)), 1, 145);
        outer.Children.Add(Ui.Row(count, unit, Ui.Button(L.T("Custom delay", "Άλλο διάστημα"), () =>
        { try { if (!int.TryParse(count.Text, out int n) || n < 1 || n > 1200) throw new ArgumentException(); Pick(Schedule.Add(DateTime.Now, n, units[unit.SelectedIndex])); } catch { error.Text = L.T("Choose a number from 1 to 1200.", "Διάλεξε αριθμό από 1 έως 1200."); } }),
            Ui.Help(L.T("Custom delay", "Άλλο διάστημα"), L.T("Enter 1–1200 and choose a unit. Custom delay immediately snoozes the reminder for that long from now.", "Δώσε 1–1200 και διάλεξε μονάδα. Το Άλλο διάστημα αναβάλλει αμέσως την υπενθύμιση με αφετηρία τώρα."))));
        outer.Children.Add(error); outer.Children.Add(Ui.Button(L.T("Back", "Πίσω"), Close));
    }
    private void Pick(DateTime time) { Until = time; DialogResult = true; }
}
