using System.Globalization;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace QuestBell;

internal static class L
{
    internal static string Language = "en";
    internal static string T(string en, string el) => Language == "el" ? el : en;
    internal static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "el" ? "el-GR" : "en-GB");
    internal static string Date(DateTime date) => date.ToString("ddd d MMM · HH:mm", Culture);
    internal static string Unit(string unit, int value) => Language == "el" ? unit switch
    { "minutes" => value == 1 ? "λεπτό" : "λεπτά", "hours" => value == 1 ? "ώρα" : "ώρες", "days" => value == 1 ? "ημέρα" : "ημέρες", "weeks" => value == 1 ? "εβδομάδα" : "εβδομάδες", _ => value == 1 ? "μήνας" : "μήνες" }
    : unit switch { "minutes" => value == 1 ? "minute" : "minutes", "hours" => value == 1 ? "hour" : "hours", "days" => value == 1 ? "day" : "days", "weeks" => value == 1 ? "week" : "weeks", _ => value == 1 ? "month" : "months" };
    internal static string Delay(Delay delay) => $"{delay.Value} {Unit(delay.Unit, delay.Value)}";
    internal static string Repeat(string value) => value switch
    { "daily" => T("Daily", "Καθημερινά"), "weekly" => T("Weekly", "Κάθε εβδομάδα"), "monthly" => T("Monthly", "Κάθε μήνα"), "yearly" => T("Yearly", "Κάθε χρόνο"), "weekdays" => T("Selected weekdays", "Επιλεγμένες ημέρες"), _ => T("One time", "Μία φορά") };
}

internal static class Ui
{
    internal static SolidColorBrush Brush(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }
    internal static readonly Brush Mint = Brush("#6BE8C5"), Muted = Brush("#92A3B8"), Lavender = Brush("#BDAAFF"), Gold = Brush("#F3CC7A");
    internal static TextBlock Text(string text, double size = 14, Brush? color = null, bool bold = false) => new()
    { Text = text, FontSize = size, Foreground = color ?? Brush("#EDF3F8"), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
      TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    internal static Button Button(string text, Action action, bool primary = false)
    {
        var b = new Button { Content = text };
        if (primary) { b.Background = Mint; b.Foreground = Brush("#0B2823"); b.BorderBrush = Mint; }
        b.Click += (_, _) => action(); return b;
    }
    internal static FrameworkElement ActionIcon(string action)
    {
        // Vector geometry avoids missing-font glyphs and remains sharp at every DPI.
        string data = action switch
        {
            "edit" => "M4,16 L4,20 L8,20 L20,8 L16,4 Z M13,7 L17,11",
            "preview" => "M2,12 C7,4 17,4 22,12 C17,20 7,20 2,12 Z M15,12 A3,3 0 1 1 9,12 A3,3 0 1 1 15,12",
            "snooze" => "M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M12,7 L12,12 L16,14",
            "skip" => "M5,6 L13,12 L5,18 M13,6 L21,12 L13,18",
            "disable" => "M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M9,8 L9,16 M15,8 L15,16",
            "delete" => "M4,6 L20,6 M9,6 L9,3 L15,3 L15,6 M6,6 L7,21 L17,21 L18,6 M10,10 L10,17 M14,10 L14,17",
            _ => throw new ArgumentException("Unknown action icon.", nameof(action))
        };
        var geometry = Geometry.Parse(data); geometry.Freeze();
        var stroke = new System.Windows.Shapes.Path
        {
            Data = geometry, Stroke = action == "delete" ? Brush("#FFB5A7") : Mint,
            StrokeThickness = 1.75, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        var canvas = new Canvas { Width = 24, Height = 24 }; canvas.Children.Add(stroke);
        return new Viewbox { Width = 20, Height = 20, Child = canvas, IsHitTestVisible = false };
    }
    internal static Border Card(UIElement content, string background = "#151F2E", double padding = 22) => new()
    { Child = content, Background = Brush(background), CornerRadius = new CornerRadius(16), Padding = new Thickness(padding),
      BorderBrush = Brush("#29374B"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 16) };
    internal static StackPanel Row(params UIElement[] items) { var row = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var x in items) row.Children.Add(x); return row; }
    internal static StackPanel Col(params UIElement[] items) { var col = new StackPanel(); foreach (var x in items) col.Children.Add(x); return col; }
    internal static TextBlock Eyebrow(string text) => Text(text.ToUpper(L.Culture), 11, Mint, true);
    internal static ComboBox Combo(IEnumerable<string> options, int selected = 0, double width = double.NaN)
    {
        var c = new ComboBox { Width = width }; foreach (var s in options) c.Items.Add(s); c.SelectedIndex = selected; return c;
    }
    internal static FrameworkElement Field(string label, FrameworkElement control) => Col(Text(label, 12, Muted, true), control);
    internal static Grid Columns(FrameworkElement left, FrameworkElement right, double leftWeight = 1, double rightWeight = 1)
    {
        var g = new Grid(); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(leftWeight, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(rightWeight, GridUnitType.Star) });
        Grid.SetColumn(right, 1); g.Children.Add(left); g.Children.Add(right); return g;
    }
    internal static void ApplyWindow(Window window)
    {
        // Implicit WPF styles match the exact type, not our Window subclasses.
        window.SetResourceReference(FrameworkElement.StyleProperty, typeof(Window));
        window.Language = System.Windows.Markup.XmlLanguage.GetLanguage(L.Culture.IetfLanguageTag);
        window.SourceInitialized += (_, _) => { Native.DarkTitle(window); Native.FitToWorkArea(window); };
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
    }
    internal static void Float(FrameworkElement element, bool reduced)
    {
        if (reduced) return;
        var t = new TranslateTransform(); element.RenderTransform = t;
        element.Loaded += (_, _) => t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-7, 5, TimeSpan.FromSeconds(2.4))
        { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
        element.Unloaded += (_, _) => t.BeginAnimation(TranslateTransform.YProperty, null);
    }
    internal static void Error(Window owner, Exception error) => MessageBox.Show(owner, error.Message, "QuestBell", MessageBoxButton.OK, MessageBoxImage.Warning);
}

internal static class Art
{
    private static readonly Dictionary<string, BitmapSource> Cache = new();
    private static readonly Dictionary<(string, int), Int32Rect> Regions = new();
    internal static void TrimCache() => Cache.Clear();
    internal static BitmapSource Sprite(string category, int variant)
    {
        var art = Artwork.Get(category, variant);
        if (!Cache.TryGetValue(art.Sheet, out var source))
        {
            var image = new BitmapImage(); image.BeginInit(); image.UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", art.Sheet + ".png"));
            image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 1024; image.EndInit(); image.Freeze();
            // Decode on demand, retain at most six sheets while browsing the larger library.
            if (Cache.Count >= 6) Cache.Remove(Cache.Keys.First());
            Cache[art.Sheet] = source = image;
        }
        int w = source.PixelWidth / 2, h = source.PixelHeight / 2;
        var region = new Int32Rect(art.Cell % 2 * w, art.Cell / 2 * h, w, h);
        if (art.Variant >= 4)
        {
            if (!Regions.TryGetValue((art.Sheet, art.Cell), out var trimmed))
                Regions[(art.Sheet, art.Cell)] = trimmed = SpriteRegion(source, region);
            region = trimmed;
        }
        var crop = new CroppedBitmap(source, region); crop.Freeze(); return crop;
    }
    // Frame each new sprite around its main shapes. Generated sheets can contain a few
    // pixels from a neighboring cell at the edge; those must not appear beside a pet.
    // This only selects a display rectangle. The original transparent PNG is untouched.
    private static Int32Rect SpriteRegion(BitmapSource sheet, Int32Rect cell)
    {
        var source = new FormatConvertedBitmap(new CroppedBitmap(sheet, cell), PixelFormats.Bgra32, null, 0);
        int w = cell.Width, h = cell.Height; var pixels = new byte[w * h * 4]; source.CopyPixels(pixels, w * 4, 0);
        var visited = new bool[w * h]; var queue = new int[w * h];
        var shapes = new List<(int Count, int Left, int Top, int Right, int Bottom)>();
        for (int start = 0; start < visited.Length; start++)
        {
            if (visited[start] || pixels[start * 4 + 3] < 96) continue;
            int head = 0, tail = 1, left = w, top = h, right = 0, bottom = 0; queue[0] = start; visited[start] = true;
            while (head < tail)
            {
                int pos = queue[head++], x = pos % w, y = pos / w;
                left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy; if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int n = ny * w + nx;
                    if (!visited[n] && pixels[n * 4 + 3] >= 96) { visited[n] = true; queue[tail++] = n; }
                }
            }
            shapes.Add((tail, left, top, right, bottom));
        }
        if (shapes.Count == 0) return cell;
        var main = shapes.MaxBy(s => s.Count);
        var keep = shapes.Where(s => s == main || s.Count >= main.Count * .025 &&
            (s.Left + s.Right) / 2.0 > w * .08 && (s.Left + s.Right) / 2.0 < w * .92 &&
            (s.Top + s.Bottom) / 2.0 > h * .08 && (s.Top + s.Bottom) / 2.0 < h * .92).ToArray();
        int l = Math.Max(0, keep.Min(s => s.Left) - 5), t = Math.Max(0, keep.Min(s => s.Top) - 5);
        int r = Math.Min(w - 1, keep.Max(s => s.Right) + 5), b = Math.Min(h - 1, keep.Max(s => s.Bottom) + 5);
        return new Int32Rect(cell.X + l, cell.Y + t, r - l + 1, b - t + 1);
    }
    internal static Image Image(string category, int variant, double size)
    {
        var image = new Image { Source = Sprite(category, variant), Width = size, Height = size, Stretch = Stretch.Uniform, Margin = new Thickness(8) };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor); return image;
    }
    internal static FrameworkElement Stage(string category, int variant, double size, bool animated)
    {
        var grid = new Grid { Width = size, Height = size };
        grid.Children.Add(new Ellipse { Width = size * .9, Height = size * .9, Fill = new RadialGradientBrush(Ui.Brush("#30465D").Color, Colors.Transparent) });
        var stars = new Canvas { Width = size, Height = size, IsHitTestVisible = false };
        var rng = new Random(123 + variant);
        for (int i = 0; i < 21; i++)
        {
            var star = new Rectangle { Width = i % 3 == 0 ? 5 : 3, Height = i % 3 == 0 ? 5 : 3, Fill = i % 2 == 0 ? Ui.Mint : Ui.Gold, Opacity = .25 + rng.NextDouble() * .45 };
            Canvas.SetLeft(star, rng.NextDouble() * size); Canvas.SetTop(star, rng.NextDouble() * size); stars.Children.Add(star);
        }
        grid.Children.Add(stars);
        var image = Image(category, variant, size * .85); grid.Children.Add(image); Ui.Float(image, !animated); return grid;
    }
}
