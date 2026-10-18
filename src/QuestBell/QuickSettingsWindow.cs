namespace QuestBell;

internal sealed class QuickSettingsWindow : Window
{
    internal static string TimeLabel(QuickTime time) => time.Kind switch
    {
        "clock" => TimeSpan.FromMinutes(time.Value).ToString(@"hh\:mm"),
        "date" => time.Value switch { 0 => L.T("Today", "Σήμερα"), 1 => L.T("Tomorrow", "Αύριο"), 7 => L.T("Next week", "Σε μία εβδομάδα"), _ => L.T("In ", "Σε ") + time.Value + " " + L.Unit("days", time.Value) },
        _ => L.T("In ", "Σε ") + time.Value + " " + L.Unit(time.Unit, time.Value)
    };
    internal QuickSettingsWindow()
    {
        var host = App.Instance; var preferences = host.Data.Settings;
        Title = L.T("Your quick choices · QuestBell", "Οι γρήγορες επιλογές σου · QuestBell");
        Width = 1000; Height = 900; MinWidth = 800; MinHeight = 600; Ui.ApplyWindow(this);
        var outer = new DockPanel { Margin = new Thickness(24) }; Content = outer;
        var close = Ui.Button(L.T("Close", "Κλείσιμο"), Close); DockPanel.SetDock(close, Dock.Bottom); outer.Children.Add(close);
        var error = Ui.Text("", 13, Ui.Gold); DockPanel.SetDock(error, Dock.Bottom); outer.Children.Add(error);
        var body = new StackPanel(); outer.Children.Add(new ScrollViewer { Content = body });
        body.Children.Add(Ui.Text(L.T("Make the shortcuts yours.", "Φτιάξε τις δικές σου συντομεύσεις."), 26, null, true));
        body.Children.Add(Ui.Text(L.T("Changes save immediately on this PC and are included in backups. Removing a shortcut keeps reminders you already created.", "Οι αλλαγές αποθηκεύονται αμέσως σε αυτό το PC και περιλαμβάνονται στα αντίγραφα. Η αφαίρεση συντόμευσης διατηρεί τις υπενθυμίσεις που έχεις ήδη δημιουργήσει."), 13, Ui.Muted));
        var reminderList = new StackPanel(); var timeList = new WrapPanel();
        void Try(Action action) { try { action(); error.Text = ""; } catch (Exception e) { error.Text = e.Message; } }
        void Reminders()
        {
            reminderList.Children.Clear();
            foreach (var reminder in preferences.QuickReminders)
            {
                var remove = Ui.Button(L.T("Remove", "Αφαίρεση"), () => Try(() => { preferences.QuickReminders.Remove(reminder); host.Save(); Reminders(); }));
                var text = Ui.Col(Ui.Text(reminder.Title, 15, null, true), Ui.Text((reminder.Work ? L.T("Work", "Εργασία") : L.T("Personal", "Προσωπικά")) + " · " + L.Repeat(reminder.Repeat), 12, Ui.Muted));
                reminderList.Children.Add(Ui.Columns(text, remove, 4, 1));
            }
            if (preferences.QuickReminders.Count == 0) reminderList.Children.Add(Ui.Text(L.T("No custom quick reminders yet.", "Δεν υπάρχουν ακόμη δικές σου γρήγορες υπενθυμίσεις."), 13, Ui.Muted));
        }
        void Times()
        {
            timeList.Children.Clear();
            foreach (var time in preferences.QuickTimes)
                timeList.Children.Add(Ui.Button(TimeLabel(time) + "  ×", () => Try(() => { preferences.QuickTimes.Remove(time); host.Save(); Times(); })));
            if (preferences.QuickTimes.Count == 0) timeList.Children.Add(Ui.Text(L.T("No quick time buttons. The date/time fields remain available.", "Δεν υπάρχουν γρήγορα κουμπιά ώρας. Τα πεδία ημερομηνίας και ώρας παραμένουν διαθέσιμα."), 13, Ui.Muted));
        }
        var title = new TextBox { MaxLength = 500, Name = "QuickReminderTitle" };
        var mode = Ui.Combo(new[] { L.T("Personal", "Προσωπικά"), L.T("Work", "Εργασία") });
        var category = Ui.Combo(Catalog.Categories.Select(c => c.Label(L.Language)), 6);
        var repeats = Schedule.Repeats.Where(r => r != "weekdays").ToArray();
        var repeat = Ui.Combo(repeats.Select(L.Repeat));
        mode.Name = "QuickReminderMode"; repeat.Name = "QuickReminderRepeat";
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("CUSTOM QUICK REMINDERS", "ΔΙΚΕΣ ΣΟΥ ΓΡΗΓΟΡΕΣ ΥΠΕΝΘΥΜΙΣΕΙΣ")), reminderList,
            Ui.Field(L.T("Reminder text", "Κείμενο υπενθύμισης"), title), Ui.Columns(Ui.Field(L.T("Mode", "Λειτουργία"), mode), Ui.Field(L.T("Artwork family", "Οικογένεια εικόνας"), category)), Ui.Field(L.T("Default repeat", "Προεπιλεγμένη επανάληψη"), repeat),
            Ui.Button(L.T("Add quick reminder", "Προσθήκη γρήγορης υπενθύμισης"), () => Try(() =>
            {
                if (string.IsNullOrWhiteSpace(title.Text)) throw new ArgumentException(L.T("Enter reminder text first.", "Γράψε πρώτα το κείμενο της υπενθύμισης."));
                if (preferences.QuickReminders.Count >= 100) throw new ArgumentException(L.T("You can keep up to 100 custom reminders.", "Μπορείς να κρατήσεις έως 100 δικές σου υπενθυμίσεις."));
                preferences.QuickReminders.Add(new QuickReminder { Title = title.Text.Trim(), Work = mode.SelectedIndex == 1, Category = Catalog.CategoryIds[category.SelectedIndex], Repeat = repeats[repeat.SelectedIndex] });
                host.Save(); title.Text = ""; Reminders();
            }), true))));
        var kind = Ui.Combo(new[] { L.T("Time of day", "Ώρα ημέρας"), L.T("Remind me in…", "Θύμισέ το σε…"), L.T("Days from today", "Ημέρες από σήμερα") });
        var clock = new TextBox { Text = "09:00", MaxLength = 5 };
        var value = new TextBox { Text = "15", MaxLength = 4 };
        var units = new[] { "minutes", "hours", "days", "weeks", "months" };
        var unit = Ui.Combo(units.Select(u => L.Unit(u, 2)));
        kind.Name = "QuickTimeKind"; clock.Name = "QuickTimeClock"; value.Name = "QuickTimeValue"; unit.Name = "QuickTimeUnit";
        var clockField = Ui.Field(L.T("24-hour time · HH:mm", "24ωρη ώρα · ΩΩ:λλ"), clock);
        var valueField = Ui.Field(L.T("Number · days: 0–365; delays: 1–1200", "Αριθμός · ημέρες: 0–365, διαστήματα: 1–1200"), value);
        var unitField = Ui.Field(L.T("Unit", "Μονάδα"), unit);
        void Kind() { clockField.Visibility = kind.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed; valueField.Visibility = kind.SelectedIndex != 0 ? Visibility.Visible : Visibility.Collapsed; unitField.Visibility = kind.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed; }
        kind.SelectionChanged += (_, _) => Kind(); Kind();
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("QUICK TIME BUTTONS", "ΓΡΗΓΟΡΑ ΚΟΥΜΠΙΑ ΩΡΑΣ")),
            Ui.Text(L.T("Tap × to remove a button. Date buttons appear beside the date; times and delays appear under More timing choices.", "Πάτησε × για αφαίρεση. Τα κουμπιά ημερομηνίας εμφανίζονται δίπλα στην ημερομηνία· οι ώρες και τα διαστήματα στις Περισσότερες επιλογές ώρας."), 13, Ui.Muted), timeList,
            Ui.Field(L.T("Button type", "Τύπος κουμπιού"), kind), clockField, valueField, unitField,
            Ui.Button(L.T("Add time button", "Προσθήκη κουμπιού ώρας"), () => Try(() =>
            {
                if (preferences.QuickTimes.Count >= 30) throw new ArgumentException(L.T("You can keep up to 30 time buttons.", "Μπορείς να κρατήσεις έως 30 κουμπιά ώρας."));
                var time = new QuickTime { Kind = kind.SelectedIndex switch { 0 => "clock", 1 => "delay", _ => "date" }, Unit = units[unit.SelectedIndex] };
                if (kind.SelectedIndex == 0)
                {
                    if (!TimeSpan.TryParseExact(clock.Text.Trim(), @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var span) || span.TotalMinutes >= 1440) throw new ArgumentException(L.T("Use a valid 24-hour time, for example 09:30.", "Δώσε έγκυρη 24ωρη ώρα, π.χ. 09:30."));
                    time.Value = (int)span.TotalMinutes;
                }
                else if (!int.TryParse(value.Text, out int number) || number < (kind.SelectedIndex == 2 ? 0 : 1) || number > (kind.SelectedIndex == 2 ? 365 : 1200)) throw new ArgumentException(L.T("Choose a number within the displayed range.", "Διάλεξε αριθμό μέσα στο εμφανιζόμενο διάστημα."));
                else time.Value = number;
                if (preferences.QuickTimes.Any(t => t.Kind == time.Kind && t.Value == time.Value && (t.Kind != "delay" || t.Unit == time.Unit))) throw new ArgumentException(L.T("That button already exists.", "Αυτό το κουμπί υπάρχει ήδη."));
                preferences.QuickTimes.Add(time); host.Save(); Times();
            }), true),
            Ui.Button(L.T("Restore default time buttons", "Επαναφορά προεπιλεγμένων κουμπιών ώρας"), () => Try(() => { preferences.QuickTimes = QuickOptions.DefaultTimes(); host.Save(); Times(); })))));
        Reminders(); Times();
    }
}
