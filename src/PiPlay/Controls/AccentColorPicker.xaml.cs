using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PiPlay.Theme;

namespace PiPlay.Controls;

public partial class AccentColorPicker : UserControl
{
    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(nameof(SelectedColor), typeof(string), typeof(AccentColorPicker),
            new FrameworkPropertyMetadata(ThemeCatalog.DefaultAccentColor,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    private static readonly DependencyPropertyKey IsSelectedReadablePropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(IsSelectedReadable), typeof(bool), typeof(AccentColorPicker),
            new PropertyMetadata(true));

    public static readonly DependencyProperty IsSelectedReadableProperty = IsSelectedReadablePropertyKey.DependencyProperty;

    private bool _updating;
    private bool _suppressPreview;
    // True only while the change originated from the disc/brightness (HSV) controls, so the
    // SelectedColor round-trip must NOT re-derive _h/_s/_v from the lossy 8-bit RGB (that collapses
    // hue toward red at low saturation/value).
    private bool _hsvOriginated;
    // The text box the user is actively editing, so its text is not rewritten out from under them.
    private TextBox? _editingTextBox;
    private double _h;
    private double _s = 1.0;
    private double _v = 1.0;

    public AccentColorPicker()
    {
        InitializeComponent();
        PresetRow.ItemsSource = ThemeCatalog.AccentOptions;
        Loaded += (_, _) => RenderDisc();
        SizeChanged += (_, _) => RenderDisc();
        HueSatDisc.MouseLeftButtonDown += HueSatDisc_MouseLeftButtonDown;
        HueSatDisc.MouseLeftButtonUp += HueSatDisc_MouseLeftButtonUp;
        HueSatDisc.MouseMove += HueSatDisc_MouseMove;
        Unloaded += (_, _) => ReleaseHueSatCapture();

        _suppressPreview = true;
        SelectedColor = ThemeCatalog.DefaultAccentColor;
        RefreshFromSelectedColor(SelectedColor, recomputeHsv: true);
        _suppressPreview = false;
    }

    public event Action<string>? PreviewColorChanged;
    public event Action<bool>? ReadabilityChanged;

    public string SelectedColor
    {
        get => (string)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public bool IsSelectedReadable => (bool)GetValue(IsSelectedReadableProperty);

    public void UseNearestReadable() => SelectedColor = AccentReadabilityPolicy.NearestReadable(SelectedColor);

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        RenderDisc();
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (AccentColorPicker)d;
        if (picker._updating) return;

        var value = e.NewValue as string ?? "";
        if (ThemeCatalog.IsValidHex(value))
        {
            var normalized = ThemeCatalog.NormalizeAccentColor(value);
            if (!string.Equals(normalized, value, StringComparison.Ordinal))
            {
                picker._updating = true;
                picker.SetCurrentValue(SelectedColorProperty, normalized);
                picker._updating = false;
                value = normalized;
            }
        }

        // HSV-originated changes (disc/brightness) keep the authoritative _h/_s/_v; only genuine
        // RGB/hex/host edits re-derive HSV from the new color.
        picker.RefreshFromSelectedColor(value, recomputeHsv: !picker._hsvOriginated);
    }

    private void RefreshFromSelectedColor(string value, bool recomputeHsv)
    {
        var readable = AccentReadabilityPolicy.Evaluate(value).IsReadable;
        SetReadability(readable);

        _updating = true;
        try
        {
            if (!ReferenceEquals(_editingTextBox, HexInput)) HexInput.Text = value;
            if (ThemeCatalog.IsValidHex(value))
            {
                var color = ThemeColors.ParseColor(value);
                if (recomputeHsv)
                {
                    var hsv = ColorMath.RgbToHsv(color);
                    _h = hsv.H;
                    _s = hsv.S;
                    _v = hsv.V;
                }

                if (!ReferenceEquals(_editingTextBox, RInput)) RInput.Text = color.R.ToString();
                if (!ReferenceEquals(_editingTextBox, GInput)) GInput.Text = color.G.ToString();
                if (!ReferenceEquals(_editingTextBox, BInput)) BInput.Text = color.B.ToString();
                ValueSlider.Value = _v;
                PreviewSwatch.Background = ThemeColors.Brush(value);
                MoveThumb();
            }
        }
        finally
        {
            _updating = false;
        }

        if (readable && !_suppressPreview)
            PreviewColorChanged?.Invoke(ThemeCatalog.NormalizeAccentColor(value));
    }

    private void ValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updating) return;
        _v = e.NewValue;
        SetSelectedFromHsv();
    }

    private void RgbInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        _editingTextBox = sender as TextBox;
        try
        {
            if (!byte.TryParse(RInput.Text, out var r)
                || !byte.TryParse(GInput.Text, out var g)
                || !byte.TryParse(BInput.Text, out var b))
            {
                SetReadability(false);
                return;
            }

            var color = Color.FromRgb(r, g, b);
            var (h, s, v) = ColorMath.RgbToHsv(color);
            _h = h;
            _s = s;
            _v = v;
            SelectedColor = Hex(color);
        }
        finally
        {
            _editingTextBox = null;
        }
    }

    private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        _editingTextBox = HexInput;
        try
        {
            SelectedColor = HexInput.Text.Trim();
        }
        finally
        {
            _editingTextBox = null;
        }
    }

    private void Preset_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is ThemeAccentOption option)
            SelectedColor = option.HexColor;
    }

    private void UseNearestReadableButton_Click(object sender, RoutedEventArgs e) => UseNearestReadable();

    private void HueSatDisc_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HueSatDisc.CaptureMouse();
        SetHueSaturationFromPoint(e.GetPosition(HueSatDisc));
        e.Handled = true;
    }

    private void HueSatDisc_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (HueSatDisc.IsMouseCaptured)
            SetHueSaturationFromPoint(e.GetPosition(HueSatDisc));
        ReleaseHueSatCapture();
        e.Handled = true;
    }

    private void HueSatDisc_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            ReleaseHueSatCapture();
            return;
        }
        SetHueSaturationFromPoint(e.GetPosition(HueSatDisc));
    }

    private void ReleaseHueSatCapture()
    {
        if (HueSatDisc.IsMouseCaptured)
            HueSatDisc.ReleaseMouseCapture();
    }

    private void SetReadability(bool readable)
    {
        var changed = IsSelectedReadable != readable;
        SetValue(IsSelectedReadablePropertyKey, readable);
        ReadabilityRow.Visibility = readable ? Visibility.Collapsed : Visibility.Visible;
        ReadabilityWarning.Visibility = readable ? Visibility.Collapsed : Visibility.Visible;
        UseNearestReadableButton.Visibility = readable ? Visibility.Collapsed : Visibility.Visible;
        if (changed)
            ReadabilityChanged?.Invoke(readable);
    }

    private void SetHueSaturationFromPoint(Point point)
    {
        var size = Math.Min(HueSatDisc.ActualWidth, HueSatDisc.ActualHeight);
        if (size <= 0.0) return;
        var radius = size / 2.0;
        var dx = point.X - radius;
        var dy = point.Y - radius;
        var distance = Math.Sqrt(dx * dx + dy * dy);

        _s = Math.Clamp(distance / radius, 0.0, 1.0);
        _h = ((Math.Atan2(dy, dx) * 180.0 / Math.PI) + 360.0) % 360.0;
        SetSelectedFromHsv();
    }

    private void SetSelectedFromHsv()
    {
        var color = ColorMath.HsvToRgb(_h, _s, _v);
        _hsvOriginated = true;
        try
        {
            SelectedColor = Hex(color);
        }
        finally
        {
            _hsvOriginated = false;
        }
    }

    private void RenderDisc()
    {
        var logicalSize = Math.Max(1, (int)Math.Round(Math.Min(
            HueSatDisc.ActualWidth > 0 ? HueSatDisc.ActualWidth : HueSatDisc.Width,
            HueSatDisc.ActualHeight > 0 ? HueSatDisc.ActualHeight : HueSatDisc.Height)));
        var dpi = VisualTreeHelper.GetDpi(this);
        var discScale = dpi.DpiScaleX;
        var pixelSize = Math.Max(1, (int)Math.Round(logicalSize * discScale));
        var bitmap = new WriteableBitmap(pixelSize, pixelSize, 96.0 * discScale, 96.0 * discScale, PixelFormats.Pbgra32, null);
        var pixels = new int[pixelSize * pixelSize];
        var center = (pixelSize - 1) / 2.0;
        var radius = center;

        for (var y = 0; y < pixelSize; y++)
        {
            for (var x = 0; x < pixelSize; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance > radius)
                {
                    pixels[y * pixelSize + x] = 0x00000000;
                    continue;
                }

                var hue = ((Math.Atan2(dy, dx) * 180.0 / Math.PI) + 360.0) % 360.0;
                var saturation = Math.Clamp(distance / radius, 0.0, 1.0);
                var color = ColorMath.HsvToRgb(hue, saturation, 1.0);
                pixels[y * pixelSize + x] = unchecked((int)(0xFF000000u | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B));
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, pixelSize, pixelSize), pixels, pixelSize * 4, 0);
        bitmap.Freeze();
        HueSatDisc.Source = bitmap;
        MoveThumb();
    }

    private void MoveThumb()
    {
        var size = Math.Min(HueSatDisc.ActualWidth > 0 ? HueSatDisc.ActualWidth : HueSatDisc.Width,
            HueSatDisc.ActualHeight > 0 ? HueSatDisc.ActualHeight : HueSatDisc.Height);
        if (size <= 0.0 || double.IsNaN(size)) return;
        var radius = size / 2.0;
        var angle = _h * Math.PI / 180.0;
        var x = radius + Math.Cos(angle) * _s * radius - HueSatThumb.Width / 2.0;
        var y = radius + Math.Sin(angle) * _s * radius - HueSatThumb.Height / 2.0;
        Canvas.SetLeft(HueSatThumb, x);
        Canvas.SetTop(HueSatThumb, y);
    }

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
