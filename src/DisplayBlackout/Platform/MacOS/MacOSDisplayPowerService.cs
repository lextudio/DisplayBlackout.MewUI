using System.Globalization;
using System.Runtime.InteropServices;

namespace DisplayBlackout.Platform.MacOS;

internal sealed class MacOSDisplayPowerService : IDisplayPowerService
{
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const uint kCGConfigurePermanently = 0;

    public bool CanSleep(string displayId)
    {
        if (!ParseId(displayId, out uint id)) return false;
        // Primary display cannot be disabled
        return !CGDisplayIsMain(id);
    }

    public bool Sleep(string displayId)
    {
        if (!ParseId(displayId, out uint id)) return false;
        if (CGDisplayIsMain(id)) return false;
        return SetEnabled(id, false);
    }

    public bool Wake(string displayId)
    {
        if (!ParseId(displayId, out uint id)) return false;
        return SetEnabled(id, true);
    }

    public bool IsAsleep(string displayId)
    {
        if (!ParseId(displayId, out uint id)) return false;
        return !CGDisplayIsActive(id);
    }

    private static bool SetEnabled(uint displayId, bool enabled)
    {
        int result = CGBeginDisplayConfiguration(out nint config);
        if (result != 0) return false;

        result = CGSConfigureDisplayEnabled(config, displayId, enabled);
        if (result != 0) return false;

        result = CGCompleteDisplayConfiguration(config, kCGConfigurePermanently);
        return result == 0;
    }

    private static bool ParseId(string displayId, out uint id)
        => uint.TryParse(displayId, NumberStyles.None, CultureInfo.InvariantCulture, out id);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGDisplayIsMain(uint display);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGDisplayIsActive(uint display);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CGBeginDisplayConfiguration(out nint config);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CGCompleteDisplayConfiguration(nint config, uint options);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl, EntryPoint = "CGSConfigureDisplayEnabled")]
    private static extern int CGSConfigureDisplayEnabled(
        nint config,
        uint displayId,
        [MarshalAs(UnmanagedType.I1)] bool enabled);
}
