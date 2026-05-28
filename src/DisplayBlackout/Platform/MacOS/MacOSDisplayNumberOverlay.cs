using System.Runtime.InteropServices;

namespace DisplayBlackout.Platform.MacOS;

internal sealed class MacOSDisplayNumberOverlay : IDisposable
{
    private const string ObjCLib = "/usr/lib/libobjc.A.dylib";
    private const string CoreGraphicsLib = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

    private const int WindowLevel = 1000; // NSScreenSaverWindowLevel
    private const int CollectionBehavior = 1; // NSWindowCollectionBehaviorCanJoinAllSpaces

    private nint _window;
    private bool _disposed;

    public MacOSDisplayNumberOverlay(DisplayInfo display, int displayNumber)
    {
        double mainH = GetMainDisplayHeight();
        double nsX = display.Bounds.Left;
        double nsY = mainH - display.Bounds.Top - display.Bounds.Height;
        double w = display.Bounds.Width;
        double h = display.Bounds.Height;

        nint win = objc_msgSend_ret(objc_getClass("NSWindow"), sel("alloc"));
        win = objc_msgSend_init_window(win, sel("initWithContentRect:styleMask:backing:defer:"),
            nsX, nsY, w, h, 0, 2, 0);

        nint black = objc_msgSend_ret(objc_getClass("NSColor"), sel("blackColor"));
        objc_msgSend_void_nint(win, sel("setBackgroundColor:"), black);
        objc_msgSend_void_bool(win, sel("setOpaque:"), false);
        objc_msgSend_void_double(win, sel("setAlphaValue:"), 0.80);
        objc_msgSend_void_int(win, sel("setLevel:"), WindowLevel);
        objc_msgSend_void_int(win, sel("setCollectionBehavior:"), CollectionBehavior);
        objc_msgSend_void_bool(win, sel("setIgnoresMouseEvents:"), true);

        double fontSize = h / 3.0;
        nint font = objc_msgSend_ret_double(objc_getClass("NSFont"), sel("boldSystemFontOfSize:"), fontSize);

        // Size the label to one line height and center it vertically.
        // NSTextField draws text at the top of its frame; NS coordinates have y=0 at bottom.
        double labelH = fontSize * 1.4;
        double labelY = (h - labelH) / 2.0;

        nint label = objc_msgSend_ret(objc_getClass("NSTextField"), sel("alloc"));
        label = objc_msgSend_init_frame(label, sel("initWithFrame:"), 0, labelY, w, labelH);

        nint nsStr = MakeNSString(displayNumber.ToString());
        objc_msgSend_void_nint(label, sel("setStringValue:"), nsStr);

        objc_msgSend_void_bool(label, sel("setEditable:"), false);
        objc_msgSend_void_bool(label, sel("setBordered:"), false);
        objc_msgSend_void_bool(label, sel("setSelectable:"), false);

        nint clearColor = objc_msgSend_ret(objc_getClass("NSColor"), sel("clearColor"));
        objc_msgSend_void_nint(label, sel("setBackgroundColor:"), clearColor);

        nint whiteColor = objc_msgSend_ret(objc_getClass("NSColor"), sel("whiteColor"));
        objc_msgSend_void_nint(label, sel("setTextColor:"), whiteColor);

        // NSTextAlignmentCenter = 1
        objc_msgSend_void_int(label, sel("setAlignment:"), 1);
        objc_msgSend_void_nint(label, sel("setFont:"), font);

        nint contentView = objc_msgSend_ret(win, sel("contentView"));
        objc_msgSend_void_nint(contentView, sel("addSubview:"), label);

        _window = win;
        objc_msgSend_void_nint(win, sel("orderFrontRegardless"), 0);
    }

    public void Dispose()
    {
        if (_disposed || _window == 0)
            return;
        _disposed = true;
        var win = _window;
        _window = 0;
        objc_msgSend_void(win, sel("close"));
    }

    private static double GetMainDisplayHeight()
    {
        uint mainId = CGMainDisplayID();
        CGRect bounds = CGDisplayBounds(mainId);
        return bounds.size.height;
    }

    private static nint MakeNSString(string text)
    {
        nint cls = objc_getClass("NSString");
        return objc_msgSend_ret_str(cls, sel("stringWithUTF8String:"), text);
    }

    private static nint sel(string name) => sel_registerName(name);

    [DllImport(ObjCLib)] private static extern nint objc_getClass(string name);
    [DllImport(ObjCLib)] private static extern nint sel_registerName(string name);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern nint objc_msgSend_ret(nint r, nint s);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void(nint r, nint s);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_nint(nint r, nint s, nint a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_bool(nint r, nint s, [MarshalAs(UnmanagedType.I1)] bool a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_int(nint r, nint s, int a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern void objc_msgSend_void_double(nint r, nint s, double a1);
    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")] private static extern nint objc_msgSend_ret_double(nint r, nint s, double a1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_ret_str(nint r, nint s,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string a1);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_init_window(nint r, nint s,
        double x, double y, double w, double h,
        nint styleMask, nint backing, nint defer);

    [DllImport(ObjCLib, EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend_init_frame(nint r, nint s,
        double x, double y, double w, double h);

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint CGMainDisplayID();

    [DllImport(CoreGraphicsLib, CallingConvention = CallingConvention.Cdecl)]
    private static extern CGRect CGDisplayBounds(uint display);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGRect
    {
        public readonly CGPoint origin;
        public readonly CGSize size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGPoint { public readonly double x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CGSize { public readonly double width, height; }
}
