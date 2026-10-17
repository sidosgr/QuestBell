using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace QuestBell;

internal sealed class ReminderWindow : Window
{
    internal Forms.Screen TargetScreen = Forms.Screen.PrimaryScreen!;
    internal bool AllowClose;
    private readonly Reminder task;
    private readonly bool preview;
    private readonly DateTime opened = DateTime.Now;
    private readonly TextBlock banner;
    private readonly FrameworkElement artwork;
    private readonly StackPanel actions;
    private bool acting;
    private App Host => App.Instance;
    internal ReminderWindow(Reminder task, bool preview)
    {
        this.task = task; this.preview = preview;
        Title = "QuestBell · " + Catalog.Title(task, L.Language); WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; ShowActivated = false; Background = Ui.Brush("#0B131D");
        Width = 1100; Height = 800;
        var background = new Grid(); Content = background;
        background.Background = new RadialGradientBrush(Ui.Brush("#223B43").Color, Ui.Brush("#0A1019").Color) { Center = new Point(.5, .33), RadiusX = .8, RadiusY = .9 };
        var canvas = new Grid { Width = 1060, Height = 790 };
        var outer = new Viewbox { Stretch = Stretch.Uniform, Child = canvas, Margin = new Thickness(28) }; background.Children.Add(outer);
        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Width = 950 };
        canvas.Children.Add(content);
        banner = Ui.Eyebrow(preview ? L.T("PREVIEW · YOUR NEXT LITTLE ADVENTURE", "ΔΟΚΙΜΗ · Η ΕΠΟΜΕΝΗ ΜΙΚΡΗ ΠΕΡΙΠΕΤΕΙΑ") : L.T("QUESTBELL · A LITTLE NUDGE FROM YOUR FUTURE SELF", "QUESTBELL · ΜΙΑ ΜΙΚΡΗ ΥΠΕΝΘΥΜΙΣΗ ΑΠΟ ΤΟΝ ΜΕΛΛΟΝΤΙΚΟ ΣΟΥ ΕΑΥΤΟ")); banner.TextAlignment = TextAlignment.Center; content.Children.Add(banner);
        artwork = Art.Stage(task.Category, Artwork.DisplayVariant(task), 330, !Host.Data.Settings.ReduceMotion); artwork.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(artwork);
        var title = Ui.Text(Catalog.Title(task, L.Language), 43, null, true); title.TextAlignment = TextAlignment.Center; title.MaxWidth = 940; content.Children.Add(title);
        var subtitle = Ui.Text(L.T("One little thing. Then back to your world.", "Μια μικρή εκκρεμότητα. Και πίσω στον κόσμο σου."), 17, Ui.Muted); subtitle.TextAlignment = TextAlignment.Center; content.Children.Add(subtitle);
        if (task.Amount.Length > 0 || task.DueAt.HasValue)
        {
            var metadata = Ui.Text(task.Amount + (task.Amount.Length > 0 && task.DueAt.HasValue ? "    ·    " : "") + (task.DueAt.HasValue ? L.T("Due ", "Προθεσμία ") + task.DueAt.Value.ToString("d MMM yyyy", L.Culture) : ""), 20, Ui.Gold, true); metadata.TextAlignment = TextAlignment.Center; content.Children.Add(metadata);
        }
        if (task.Note.Length > 0)
        {
            var note = Ui.Text(task.Note, 15, Ui.Muted); note.TextAlignment = TextAlignment.Center;
            content.Children.Add(new ScrollViewer { Content = note, MaxHeight = 76, Margin = new Thickness(70, 0, 70, 10) });
        }
        actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 18, 0, 0) };
        var done = Ui.Button(L.T("Done ✓", "Έγινε ✓"), () => Act("done"), true); done.FontSize = 21; done.MinWidth = 205; done.Padding = new Thickness(24, 16, 24, 16);
        var snooze = Ui.Button(L.T("Snooze · ", "Αναβολή · ") + Host.Data.Settings.SnoozeMinutes + L.T(" min", " λεπτά"), () => Act("snooze")); snooze.FontSize = 21; snooze.MinWidth = 240; snooze.Padding = new Thickness(24, 16, 24, 16);
        actions.Children.Add(done); actions.Children.Add(snooze); content.Children.Add(actions);
        var extra = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        extra.Children.Add(Ui.Button(L.T("Another time…", "Άλλη ώρα…"), CustomSnooze));
        if (task.Url.Length > 0) extra.Children.Add(Ui.Button(L.T("Open website ↗", "Άνοιγμα ιστοτόπου ↗"), () => { if (preview) ClosePreview(); else Host.FollowLink(task); }));
        if (task.Repeat != "none") extra.Children.Add(Ui.Button(L.T("Skip this time", "Παράλειψη αυτή τη φορά"), () => Act("skip")));
        if (preview) extra.Children.Add(Ui.Button(L.T("Close preview", "Κλείσιμο δοκιμής"), ClosePreview));
        content.Children.Add(extra);
        int waiting = Host.Data.Reminders.Count(x => x.Active && x.Id != task.Id && x.NextAt <= DateTime.Now);
        var footer = Ui.Text(waiting > 0 ? waiting + L.T(" more waiting · One at a time. You’ve got this.", " ακόμη σε αναμονή · Ένα τη φορά. Μπορείς.") : L.T("Esc to snooze  ·  No rush, no guilt.", "Esc για αναβολή  ·  Χωρίς βιασύνη, χωρίς ενοχές."), 12, Ui.Muted); footer.TextAlignment = TextAlignment.Center; footer.Margin = new Thickness(0, 17, 0, 0); content.Children.Add(footer);
        Closing += OnClosing;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; if (preview) ClosePreview(); else Act("snooze"); } };
    }
    internal void SendBehind() { Topmost = false; ShowActivated = false; Native.Place(this, TargetScreen, true); }
    internal void BringForward() { if (!IsVisible) Show(); Topmost = true; Native.Place(this, TargetScreen, false); Activate(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (AllowClose) return;
        e.Cancel = true;
        if (preview) Dispatcher.BeginInvoke(ClosePreview);
        else Dispatcher.BeginInvoke(() => Act("snooze"));
    }
    private void ClosePreview() { AllowClose = true; Close(); }
    private void CustomSnooze()
    {
        if (acting || DateTime.Now - opened < TimeSpan.FromMilliseconds(650)) return;
        var dialog = new SnoozeWindow { Owner = this, Topmost = true };
        if (dialog.ShowDialog() == true) { if (preview) ClosePreview(); else Host.Snooze(task, dialog.Until); }
    }
    private void Act(string action)
    {
        if (acting || DateTime.Now - opened < TimeSpan.FromMilliseconds(650)) return;
        acting = true; actions.IsEnabled = false;
        banner.Text = action == "done" ? L.T("✦ QUEST COMPLETE! A LITTLE MORE ROOM IN YOUR DAY.", "✦ ΑΠΟΣΤΟΛΗ ΟΛΟΚΛΗΡΩΘΗΚΕ! ΜΙΑ ΕΓΝΟΙΑ ΛΙΓΟΤΕΡΗ.") : action == "skip" ? L.T("THIS ONE CAN PASS. ON TO THE NEXT.", "ΑΥΤΗ ΑΣ ΠΕΡΑΣΕΙ. ΠΑΜΕ ΣΤΗΝ ΕΠΟΜΕΝΗ.") : L.T("ZZZ… YOUR LITTLE QUEST IS RESTING.", "ZZZ… Η ΜΙΚΡΗ ΣΟΥ ΑΠΟΣΤΟΛΗ ΞΕΚΟΥΡΑΖΕΤΑΙ.");
        if (!Host.Data.Settings.ReduceMotion)
        {
            var scale = new ScaleTransform(1, 1); artwork.RenderTransformOrigin = new Point(.5, .5); artwork.RenderTransform = scale;
            var anim = new DoubleAnimation(1, action == "done" ? 1.12 : .88, TimeSpan.FromMilliseconds(270)) { AutoReverse = action == "done", EasingFunction = new CubicEase() };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim); scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Host.Data.Settings.ReduceMotion ? 80 : 540) };
        timer.Tick += (_, _) => { timer.Stop(); if (preview) ClosePreview(); else if (action == "snooze") Host.Snooze(task, DateTime.Now.AddMinutes(Host.Data.Settings.SnoozeMinutes)); else Host.Resolve(task, action); }; timer.Start();
    }
}
