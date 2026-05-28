using Aprillz.MewUI;

using DisplayBlackout.Platform;
using DisplayBlackout.Platform.MacOS;
using DisplayBlackout.Platform.Win32;

namespace DisplayBlackout.Services;

internal sealed class DisplayNumberService
{
    private readonly IDisplayService _displayService;
    private List<IDisposable>? _activeOverlays;

    public DisplayNumberService(IDisplayService displayService)
    {
        _displayService = displayService;
    }

    public void ShowFor(TimeSpan duration)
    {
        // Dismiss any existing overlays immediately
        DismissAll();

        var monitors = _displayService.GetDisplays();
        var displayNumbers = ComputeDisplayNumbers(monitors);

        var overlays = new List<IDisposable>();
        foreach (var monitor in monitors)
        {
            int number = displayNumbers[monitor.Id];
            IDisposable overlay = CreateOverlay(monitor, number);
            overlays.Add(overlay);
        }

        _activeOverlays = overlays;

        var dispatcher = Application.Current?.Dispatcher;
        _ = Task.Delay(duration).ContinueWith(_ =>
        {
            if (dispatcher != null)
                dispatcher.BeginInvoke(DismissAll);
            else
                DismissAll();
        });
    }

    private void DismissAll()
    {
        var overlays = _activeOverlays;
        _activeOverlays = null;
        if (overlays == null)
            return;
        foreach (var o in overlays)
            o.Dispose();
    }

    private static IDisposable CreateOverlay(DisplayInfo display, int number)
    {
        if (OperatingSystem.IsMacOS())
            return new MacOSDisplayNumberOverlay(display, number);
        if (OperatingSystem.IsWindows())
            return new Win32DisplayNumberOverlay(display, number);
        throw new PlatformNotSupportedException();
    }

    private static Dictionary<string, int> ComputeDisplayNumbers(IReadOnlyList<DisplayInfo> monitors)
    {
        var numbers = new Dictionary<string, int>();
        var sorted = monitors
            .OrderBy(static m => m.SortOrder)
            .ThenBy(static m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        for (int i = 0; i < sorted.Count; i++)
            numbers[sorted[i].Id] = i + 1;
        return numbers;
    }
}
