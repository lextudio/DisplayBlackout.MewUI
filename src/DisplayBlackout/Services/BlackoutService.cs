using Aprillz.MewUI;

using DisplayBlackout.Platform;

namespace DisplayBlackout.Services;

internal sealed partial class BlackoutService : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly IDisplayService _displayService;
    private readonly IBlackoutOverlayFactory _overlayFactory;
    private readonly Dictionary<string, IBlackoutOverlay> _blackoutOverlays = [];
    private HashSet<string>? _selectedMonitorIds;
    private bool _disposed;

    public ObservableValue<bool> IsBlackedOut { get; }

    public ObservableValue<int> Opacity { get; }

    public ObservableValue<bool> ClickThrough { get; }

    public BlackoutService(SettingsService settingsService, IDisplayService displayService, IBlackoutOverlayFactory overlayFactory)
    {
        _settingsService = settingsService;
        _displayService = displayService;
        _overlayFactory = overlayFactory;
        _selectedMonitorIds = _settingsService.LoadSelectedMonitorIds();

        IsBlackedOut = new(false);
        Opacity = new(settingsService.LoadOpacity());
        ClickThrough = new(settingsService.LoadClickThrough());

        IsBlackedOut.Subscribe(() =>
        {
            if (IsBlackedOut.Value)
                BlackOutInternal();
            else
                RestoreInternal();
        });

        Opacity.Subscribe(() =>
        {
            int value = Opacity.Value;
            _settingsService.SaveOpacity(value);
            foreach (var overlay in _blackoutOverlays.Values)
                overlay.SetOpacity(value);
        });

        ClickThrough.Subscribe(() =>
        {
            _settingsService.SaveClickThrough(ClickThrough.Value);
            foreach (var overlay in _blackoutOverlays.Values)
                overlay.SetClickThrough(ClickThrough.Value);
        });
    }

    /// <summary>
    /// Updates which monitors should be blacked out using their bounds as stable identifiers.
    /// Null means default (all non-primary).
    /// </summary>
    public void UpdateSelectedMonitors(HashSet<string>? monitorIds)
    {
        _selectedMonitorIds = monitorIds;
        _settingsService.SaveSelectedMonitorIds(monitorIds);

        if (IsBlackedOut.Value)
        {
            RefreshOverlays();
        }
    }

    private void RefreshOverlays()
    {
        var monitors = _displayService.GetDisplays();
        var liveIds = monitors.Select(static m => m.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var monitor in monitors)
        {
            bool shouldBlackOut = _selectedMonitorIds != null
                ? _selectedMonitorIds.Contains(monitor.Id)
                : !monitor.IsPrimary;

            bool hasOverlay = _blackoutOverlays.ContainsKey(monitor.Id);

            if (shouldBlackOut && !hasOverlay)
            {
                _blackoutOverlays[monitor.Id] = _overlayFactory.Create(monitor, Opacity.Value, ClickThrough.Value);
            }
            else if (!shouldBlackOut && hasOverlay)
            {
                _blackoutOverlays[monitor.Id].Dispose();
                _blackoutOverlays.Remove(monitor.Id);
            }
        }

        foreach (var id in _blackoutOverlays.Keys.Where(id => !liveIds.Contains(id)).ToArray())
        {
            _blackoutOverlays[id].Dispose();
            _blackoutOverlays.Remove(id);
        }
    }

    /// <summary>
    /// Gets the currently selected monitor bounds for UI initialization.
    /// </summary>
    public IReadOnlySet<string>? SelectedMonitorIds => _selectedMonitorIds;

    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        var displays = _displayService.GetDisplays();

        // On macOS, disabled displays may report zero bounds. Patch with last-known bounds
        // so they remain visible in the picker after a restart.
        var cache = _settingsService.LoadDisplayBoundsCache();
        bool cacheUpdated = false;
        var result = new List<DisplayInfo>(displays.Count);

        foreach (var d in displays)
        {
            if (d.Bounds.Width > 0 && d.Bounds.Height > 0)
            {
                string encoded = $"{d.Bounds.Left},{d.Bounds.Top},{d.Bounds.Width},{d.Bounds.Height}";
                if (!cache.TryGetValue(d.Id, out var existing) || existing != encoded)
                {
                    cache[d.Id] = encoded;
                    cacheUpdated = true;
                }
                result.Add(d);
            }
            else if (cache.TryGetValue(d.Id, out var saved) && TryParseBounds(saved, out var bounds))
            {
                result.Add(d with { Bounds = bounds });
            }
        }

        if (cacheUpdated)
            _settingsService.SaveDisplayBoundsCache(cache);

        return result;
    }

    private static bool TryParseBounds(string s, out DisplayBounds bounds)
    {
        bounds = default;
        var parts = s.Split(',');
        if (parts.Length != 4) return false;
        if (!int.TryParse(parts[0], out int l) || !int.TryParse(parts[1], out int t) ||
            !int.TryParse(parts[2], out int w) || !int.TryParse(parts[3], out int h))
            return false;
        bounds = new DisplayBounds(l, t, w, h);
        return true;
    }

    /// <summary>
    /// Brings all overlay windows to the front of the Z-order.
    /// </summary>
    public void BringAllToFront()
    {
        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.BringToFront();
        }
    }

    public void Toggle() => IsBlackedOut.Value = !IsBlackedOut.Value;

    public void BlackOut() => IsBlackedOut.Value = true;

    public void Restore() => IsBlackedOut.Value = false;

    private void BlackOutInternal()
    {
        var monitors = _displayService.GetDisplays();

        foreach (var monitor in monitors)
        {
            bool shouldBlackOut = _selectedMonitorIds != null
                ? _selectedMonitorIds.Contains(monitor.Id)
                : !monitor.IsPrimary;

            if (!shouldBlackOut)
            {
                continue;
            }

            _blackoutOverlays[monitor.Id] = _overlayFactory.Create(monitor, Opacity.Value, ClickThrough.Value);
        }
    }

    private void RestoreInternal()
    {
        foreach (var overlay in _blackoutOverlays.Values)
        {
            overlay.Dispose();
        }
        _blackoutOverlays.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Restore();
    }
}
