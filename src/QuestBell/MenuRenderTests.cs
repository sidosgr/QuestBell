using System.Windows.Media.Imaging;

namespace QuestBell;

internal static class MenuRenderTests
{
    internal static int Run(MainWindow owner, Reminder reminder, string directory, string language)
    {
        var menu = owner.CreateTaskMenu(reminder);
        menu.ApplyTemplate();
        menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = menu.DesiredSize;
        menu.Arrange(new Rect(new Point(), size)); menu.UpdateLayout();
        if (size.Width < 230 || size.Height < 200) throw new Exception("Reminder menu was not sized to its contents.");
        if (menu.Template.FindName("MenuSurface", menu) is not Border surface || surface.Background != menu.Background)
            throw new Exception("Reminder menu is using the default system frame.");
        int checks = 2;
        int count = reminder.Repeat == "none" ? 5 : 6;
        if (menu.Items.Count != count) throw new Exception("Reminder menu has incorrect actions.");
        foreach (MenuItem item in menu.Items)
        {
            item.ApplyTemplate();
            var icon = item.Template.FindName("IconPresenter", item) as FrameworkElement;
            var label = item.Template.FindName("HeaderPresenter", item) as FrameworkElement;
            if (item.Icon is not FrameworkElement graphic || icon is null || label is null || icon.ActualWidth < 20 || graphic.ActualHeight < 19)
                throw new Exception("Menu icon missing or not laid out: " + item.Tag);
            var iconBounds = icon.TransformToAncestor(menu).TransformBounds(new Rect(icon.RenderSize));
            var textBounds = label.TransformToAncestor(menu).TransformBounds(new Rect(label.RenderSize));
            if (iconBounds.Right >= textBounds.Left || textBounds.Right > size.Width || textBounds.Left < 32)
                throw new Exception("Menu label overlaps the icon or popup edge: " + item.Tag);
            checks++;
        }
        foreach (double scale in new[] { 1.0, 1.5, 2.0 })
        {
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(menu);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(directory, $"menu-{language}-{scale * 100:0}.png"))) encoder.Save(stream);
            // Sample the former gutter: it must be dark, not the default Windows light strip.
            int x = (int)(8 * scale), y = (int)(size.Height / 2 * scale);
            byte[] pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            if (pixel[3] < 240 || pixel[0] > 120 || pixel[1] > 120 || pixel[2] > 120)
                throw new Exception("Reminder menu contains a light/transparent gutter at scale " + scale);
            checks++;
        }
        return checks;
    }
}
