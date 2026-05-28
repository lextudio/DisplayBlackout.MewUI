using System.Runtime.InteropServices;

namespace DisplayBlackout.Platform.MacOS;

internal sealed class MacOSDisplayService : IDisplayService
{
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        CGGetOnlineDisplayList(0, null, out uint count);
        if (count == 0)
            return [];

        var displayIds = new uint[count];
        CGGetOnlineDisplayList(count, displayIds, out _);

        CGGetActiveDisplayList(0, null, out uint activeCount);
        var activeIds = new HashSet<uint>();
        if (activeCount > 0)
        {
            var activeArr = new uint[activeCount];
            CGGetActiveDisplayList(activeCount, activeArr, out _);
            foreach (var id in activeArr) activeIds.Add(id);
        }

        var displays = new List<DisplayInfo>((int)count);
        int displayNumber = 1;
        for (int i = 0; i < displayIds.Length; i++)
        {
            uint displayId = displayIds[i];
            if (!CGDisplayIsOnline(displayId))
                continue;

            bool isEnabled = activeIds.Contains(displayId);
            var bounds = CGDisplayBounds(displayId);
            int width = (int)Math.Round(bounds.size.width);
            int height = (int)Math.Round(bounds.size.height);
            bool isPrimary = CGDisplayIsMain(displayId);

            // Include disabled displays even with zero bounds so the caller can patch
            // them from a bounds cache and show an "Enable" button in the UI.
            displays.Add(new DisplayInfo(
                displayId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new DisplayBounds(
                    (int)Math.Round(bounds.origin.x),
                    (int)Math.Round(bounds.origin.y),
                    width,
                    height),
                isPrimary,
                isPrimary ? "Primary Display" : $"Display {displayNumber}",
                displayNumber,
                isEnabled));

            displayNumber++;
        }

        return displays;
    }

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CGGetActiveDisplayList(
        uint maxDisplays,
        [Out] uint[]? displays,
        out uint displayCount);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CGGetOnlineDisplayList(
        uint maxDisplays,
        [Out] uint[]? displays,
        out uint displayCount);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGDisplayIsMain(uint display);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGDisplayIsActive(uint display);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGDisplayIsOnline(uint display);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern CGRect CGDisplayBounds(uint display);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGRect
    {
        public readonly CGPoint origin;
        public readonly CGSize size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGPoint
    {
        public readonly double x;
        public readonly double y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGSize
    {
        public readonly double width;
        public readonly double height;
    }
}
