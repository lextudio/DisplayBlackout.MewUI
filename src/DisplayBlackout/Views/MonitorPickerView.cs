using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

using DisplayBlackout.Platform;
using DisplayBlackout.Services;

namespace DisplayBlackout.Views;

/// <summary>
/// Visual monitor layout where each monitor is a toggle button.
/// Monitors are positioned using their actual spatial coordinates.
/// </summary>
internal sealed class MonitorPickerView : UserControl
{
    private readonly BlackoutService _blackoutService;
    private readonly IDisplayPowerService _displayPowerService;
    private readonly Canvas _canvas;
    private readonly List<MonitorToggle> _toggles = [];

    public MonitorPickerView(BlackoutService blackoutService, IDisplayPowerService displayPowerService)
    {
        _blackoutService = blackoutService;
        _displayPowerService = displayPowerService;

        Content = new Canvas().Ref(out _canvas);

        BuildMonitors();
    }

    public void Rebuild()
    {
        _toggles.Clear();
        _canvas.Clear();
        BuildMonitors();
    }

    private void BuildMonitors()
    {
        var monitors = _blackoutService.GetDisplays().ToList();

        if (monitors.Count == 0)
            return;

        var displayNumbers = new Dictionary<string, int>();
        var sorted = monitors
            .OrderBy(static m => m.SortOrder)
            .ThenBy(static m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (int i = 0; i < sorted.Count; i++)
            displayNumbers[sorted[i].Id] = i + 1;

        int minX = monitors.Min(static m => m.Bounds.Left);
        int minY = monitors.Min(static m => m.Bounds.Top);
        int maxX = monitors.Max(static m => m.Bounds.Right);
        int maxY = monitors.Max(static m => m.Bounds.Bottom);

        int totalWidth = maxX - minX;
        int totalHeight = maxY - minY;

        const double targetWidth = 300;
        const double targetHeight = 160;
        double scale = Math.Min(
            totalWidth > 0 ? targetWidth / totalWidth : 1,
            totalHeight > 0 ? targetHeight / totalHeight : 1);

        _canvas
            .Width(totalWidth * scale)
            .Height(totalHeight * scale);

        var selectedIds = _blackoutService.SelectedMonitorIds;

        foreach (var monitor in monitors)
        {
            double left = (monitor.Bounds.Left - minX) * scale;
            double top = (monitor.Bounds.Top - minY) * scale;
            double w = monitor.Bounds.Width * scale;
            double h = monitor.Bounds.Height * scale;

            bool isSelected = selectedIds != null
                ? selectedIds.Contains(monitor.Id)
                : !monitor.IsPrimary;

            bool canPower = _displayPowerService.CanSleep(monitor.Id);

            var toggle = new MonitorToggle(
                displayNumbers[monitor.Id], monitor.Id, monitor.IsPrimary,
                isSelected, monitor.IsEnabled,
                canPower ? _displayPowerService : null);

            toggle.Tile.Width(w).Height(h);
            toggle.Button.CheckedChanged += _ => UpdateSelection();

            Canvas.SetLeft(toggle.Tile, left);
            Canvas.SetTop(toggle.Tile, top);

            _toggles.Add(toggle);
            _canvas.Add(toggle.Tile);
        }
    }

    private void UpdateSelection()
    {
        var selected = new HashSet<string>();
        foreach (var toggle in _toggles)
        {
            if (toggle.Button.IsChecked)
                selected.Add(toggle.DisplayId);
        }
        _blackoutService.UpdateSelectedMonitors(selected);
    }
}

internal sealed class MonitorToggle
{
    private bool _isEnabled;

    public string DisplayId { get; }
    public bool IsPrimary { get; }
    public ToggleButton Button { get; }

    /// <summary>Root element placed on the Canvas.</summary>
    public FrameworkElement Tile { get; }

    public MonitorToggle(int displayNumber, string displayId, bool isPrimary, bool isSelected,
        bool isEnabled, IDisplayPowerService? powerService)
    {
        DisplayId = displayId;
        IsPrimary = isPrimary;
        _isEnabled = isEnabled;

        var label = new TextBlock()
            .Text(displayNumber.ToString())
            .CenterHorizontal()
            .CenterVertical()
            .FontSize(18)
            .Bold();

        Button = new ToggleButton()
            .IsChecked(isSelected)
            .BorderThickness(0)
            .CornerRadius(4)
            .Padding(0)
            .Content(label)
            .IsEnabled(isEnabled);

        void ApplyColors(Theme t)
        {
            if (!_isEnabled)
            {
                Button.Background(t.IsDark
                    ? Color.FromArgb(255, 60, 60, 60)
                    : Color.FromArgb(255, 180, 180, 180));
                label.Foreground(t.IsDark ? Color.DimGray : Color.Gray);
                return;
            }
            if (Button.IsChecked)
            {
                Button.Background(Color.Black);
                label.Foreground(Color.White);
            }
            else
            {
                Button.Background(t.IsDark ? Color.Gray : Color.LightGray);
                label.Foreground(t.IsDark ? Color.White : Color.Black);
            }
        }

        Button.WithTheme((t, _) => ApplyColors(t));
        Button.CheckedChanged += _ => Button.WithTheme((t, _) => ApplyColors(t));

        FrameworkElement inner;
        if (powerService != null)
        {
            TextBlock powerIcon = null!;
            Button powerBtn = null!;

            powerIcon = new TextBlock()
                .Text(_isEnabled ? "✕" : "+")
                .FontSize(12)
                .Bold()
                .CenterHorizontal()
                .CenterVertical();

            powerBtn = new Button()
                .Content(powerIcon)
                .Width(16)
                .Height(16)
                .Padding(0)
                .ToolTip(_isEnabled ? "Disable display" : "Enable display")
                .HorizontalAlignment(HorizontalAlignment.Right)
                .VerticalAlignment(VerticalAlignment.Top)
                .Margin(0, 3, 3, 0)
                .OnClick(() =>
                {
                    bool success = _isEnabled
                        ? powerService.Sleep(displayId)
                        : powerService.Wake(displayId);
                    if (success)
                        SetEnabled(!_isEnabled, powerIcon, powerBtn, ApplyColors);
                });

            powerBtn.WithTheme((t, c) => c
                .Background(t.IsDark
                    ? Color.FromArgb(160, 50, 50, 50)
                    : Color.FromArgb(160, 200, 200, 200))
                .BorderThickness(0));

            inner = new Grid().Children(Button, powerBtn);
        }
        else
        {
            inner = Button;
        }

        // Wrap in a Border to show spatial separation between monitors
        var border = new Border()
            .Padding(2)
            .CornerRadius(4)
            .Child(inner);

        border.WithTheme((t, c) => c
            .BorderThickness(2)
            .BorderBrush(isPrimary
                ? t.Palette.Accent
                : t.Palette.ControlBorder));

        Tile = border;
    }

    private void SetEnabled(bool enabled, TextBlock powerIcon, Button powerBtn, Action<Theme> applyColors)
    {
        _isEnabled = enabled;
        Button.IsEnabled = enabled;
        powerIcon.Text(enabled ? "✕" : "+");
        powerBtn.ToolTip(enabled ? "Disable display" : "Enable display");
        Button.WithTheme((t, _) => applyColors(t));
    }
}
