using Microsoft.Win32;
using System.Diagnostics;

namespace QuestBell;

internal sealed class CloudWindow : Window
{
    internal CloudWindow()
    {
        var host = App.Instance; var cloud = host.Cloud;
        Title = L.T("Cloud sync & backups · QuestBell", "Συγχρονισμός & αντίγραφα cloud · QuestBell");
        Width = 900; Height = 850; MinWidth = 700; MinHeight = 600; Ui.ApplyWindow(this);
        var body = new StackPanel { Margin = new Thickness(28) }; Content = new ScrollViewer { Content = body };
        body.Children.Add(Ui.Eyebrow(L.T("YOUR QUESTS, ACROSS YOUR PCS", "ΟΙ ΑΠΟΣΤΟΛΕΣ ΣΟΥ, ΣΕ ΟΛΑ ΤΑ PC")));
        body.Children.Add(Ui.Text(L.T("A shared home for your reminders.", "Ένα κοινό σπίτι για τις υπενθυμίσεις σου."), 25, null, true));
        body.Children.Add(Ui.Text(L.T("Optional. QuestBell keeps working offline. Choose a private folder managed by your cloud desktop app, then choose the same folder on your other PCs.", "Προαιρετικό. Το QuestBell λειτουργεί και εκτός σύνδεσης. Διάλεξε ιδιωτικό φάκελο της εφαρμογής cloud και τον ίδιο φάκελο στα άλλα PC σου."), 14, Ui.Muted));
        var folder = new TextBox { Text = cloud.Options.Folder, IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
        var sync = new CheckBox { Content = L.T("Sync reminders between PCs", "Συγχρονισμός υπενθυμίσεων μεταξύ PC"), IsChecked = cloud.Options.Sync };
        var backups = new CheckBox { Content = L.T("Keep automatic cloud backups · latest 30 per PC", "Αυτόματα αντίγραφα cloud · τα 30 πιο πρόσφατα ανά PC"), IsChecked = cloud.Options.Backups };
        var error = Ui.Text("", 13, Ui.Gold);
        var choose = Ui.Button(L.T("Choose cloud folder…", "Επιλογή φακέλου cloud…"), () =>
        {
            var picker = new OpenFolderDialog { Title = L.T("Choose your locally available cloud folder", "Διάλεξε τον τοπικά διαθέσιμο φάκελο cloud") };
            if (picker.ShowDialog(this) != true) return;
            folder.Text = Path.GetFileName(picker.FolderName).Equals("QuestBell Cloud", StringComparison.OrdinalIgnoreCase) ? picker.FolderName : Path.Combine(picker.FolderName, "QuestBell Cloud");
            if (!cloud.Enabled) { sync.IsChecked = true; backups.IsChecked = true; }
        });
        var apply = Ui.Button(L.T("Apply choices", "Εφαρμογή επιλογών"), () =>
        {
            try
            {
                if (cloud.Busy) throw new InvalidOperationException(L.T("Wait for the folder check to finish.", "Περίμενε να ολοκληρωθεί ο έλεγχος."));
                if (sync.IsChecked == true || backups.IsChecked == true)
                {
                    if (string.IsNullOrWhiteSpace(folder.Text)) throw new InvalidOperationException(L.T("Choose a cloud folder first.", "Διάλεξε πρώτα φάκελο cloud."));
                    if (!Directory.Exists(folder.Text)) Directory.CreateDirectory(folder.Text);
                }
                cloud.Configure(folder.Text, sync.IsChecked == true, backups.IsChecked == true); error.Text = "";
                host.NotifyCloudChange(); _ = cloud.Run(true);
            }
            catch (Exception e) { error.Text = e.Message; }
        }, true);
        body.Children.Add(Ui.Card(Ui.Col(Ui.Field(L.T("Shared QuestBell folder", "Κοινός φάκελος QuestBell"), folder), choose,
            Ui.Text(L.T("OneDrive: right-click this folder in Explorer → Always keep on this device. With another provider, select its equivalent offline setting. Let your cloud app finish syncing before connecting another PC.", "OneDrive: δεξί κλικ στον φάκελο → Να διατηρείται πάντα σε αυτή τη συσκευή. Σε άλλο πάροχο, διάλεξε την αντίστοιχη ρύθμιση εκτός σύνδεσης. Περίμενε να ολοκληρωθεί ο συγχρονισμός πριν συνδέσεις άλλο PC."), 12, Ui.Muted),
            sync, backups, apply, error)));
        var status = Ui.Text("", 15, Ui.Mint, true); var details = Ui.Text("", 12, Ui.Gold);
        void Refresh() { status.Text = cloud.Status; details.Text = cloud.Error; }
        cloud.Changed += Refresh; Closed += (_, _) => cloud.Changed -= Refresh; Refresh();
        body.Children.Add(Ui.Card(Ui.Col(status, details,
            Ui.Text(L.T("Checked every 30 seconds while QuestBell is running. Folder updates are local; your cloud app controls upload/download and confirms cloud delivery.", "Έλεγχος κάθε 30 δευτερόλεπτα όσο λειτουργεί το QuestBell. Οι ενημερώσεις φακέλου είναι τοπικές· η εφαρμογή cloud ελέγχει και επιβεβαιώνει τη μεταφορά."), 12, Ui.Muted),
            Ui.Row(Ui.Button(L.T("Check now", "Έλεγχος τώρα"), () => { _ = cloud.Run(true); }), Ui.Button(L.T("Turn both off", "Απενεργοποίηση και των δύο"), () =>
            { try { cloud.Pause(); sync.IsChecked = backups.IsChecked = false; host.NotifyCloudChange(); } catch (Exception e) { error.Text = e.Message; } })),
            Ui.Text(L.T("Turning off keeps all local reminders and existing cloud files.", "Η απενεργοποίηση διατηρεί τις τοπικές υπενθυμίσεις και τα υπάρχοντα αρχεία cloud."), 12, Ui.Muted))));
        var restoreButtons = new WrapPanel();
        restoreButtons.Children.Add(Ui.Button(L.T("Restore a backup…", "Επαναφορά αντιγράφου…"), () =>
        {
            host.Manager?.Import(Directory.Exists(Path.Combine(folder.Text, "backups")) ? Path.Combine(folder.Text, "backups") : host.Storage.DirectoryPath);
            sync.IsChecked = cloud.Options.Sync; backups.IsChecked = cloud.Options.Backups;
        }));
        restoreButtons.Children.Add(Ui.Button(L.T("Open recovery copies ↗", "Αντίγραφα ανάκτησης ↗"), () =>
        {
            try { string path = Path.Combine(host.Storage.DirectoryPath, "sync-recovery"); Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception e) { error.Text = e.Message; }
        }));
        body.Children.Add(Ui.Card(Ui.Col(Ui.Eyebrow(L.T("WHAT TRAVELS WITH YOU", "ΤΙ ΜΕΤΑΦΕΡΕΤΑΙ")),
            Ui.Text(L.T("Reminders, completion, snooze, history and custom phrases sync. Language, screen choice, startup and pause settings stay on each PC. Backups also include preferences. Files contain readable task details; use a private cloud folder.", "Συγχρονίζονται υπενθυμίσεις, ολοκλήρωση, αναβολή, ιστορικό και δικές σου φράσεις. Γλώσσα, οθόνη, εκκίνηση και παύση μένουν σε κάθε PC. Τα αντίγραφα περιλαμβάνουν και προτιμήσεις. Τα αρχεία περιέχουν αναγνώσιμες λεπτομέρειες· χρησιμοποίησε ιδιωτικό φάκελο."), 13, Ui.Muted),
            Ui.Text(L.T("Offline PCs may both show a reminder until changes arrive. Simultaneous edits to the same reminder choose one whole version consistently; local recovery copies preserve the previous data. Restore pauses cloud activity for review.", "Τα PC εκτός σύνδεσης μπορεί να εμφανίσουν και τα δύο μια υπενθύμιση μέχρι να φτάσουν οι αλλαγές. Σε ταυτόχρονες αλλαγές της ίδιας υπενθύμισης επιλέγεται σταθερά μία ολόκληρη έκδοση· τοπικά αντίγραφα κρατούν τα προηγούμενα δεδομένα. Η επαναφορά θέτει το cloud σε παύση για έλεγχο."), 13, Ui.Muted), restoreButtons)));
        body.Children.Add(Ui.Button(L.T("Close", "Κλείσιμο"), Close));
    }
}
