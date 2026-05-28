using System.ComponentModel;
using System.Runtime.InteropServices;

using static DisplayBlackout.Platform.Win32.NativeMethods;

namespace DisplayBlackout.Platform.Win32;

internal sealed class Win32DisplayNumberOverlay : IDisposable
{
    private const string WindowClassName = "DisplayBlackoutDisplayNumber";
    private const int FontWeightBold = 700;

    private static readonly object _classLock = new();
    private static bool _classRegistered;
    private static WndProcDelegate? _wndProc;

    private readonly string _numberText;
    private nint _hwnd;
    private bool _disposed;

    public Win32DisplayNumberOverlay(DisplayInfo display, int displayNumber)
    {
        _numberText = displayNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
        EnsureWindowClassRegistered();

        _hwnd = CreateWindowExW(
            WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
            WINDOW_EX_STYLE.WS_EX_TOPMOST |
            WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
            WINDOW_EX_STYLE.WS_EX_LAYERED |
            WINDOW_EX_STYLE.WS_EX_TRANSPARENT,
            WindowClassName,
            _numberText,
            WINDOW_STYLE.WS_POPUP,
            display.Bounds.Left,
            display.Bounds.Top,
            display.Bounds.Width,
            display.Bounds.Height,
            0,
            0,
            0,
            0);

        if (_hwnd == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        if (!SetLayeredWindowAttributes(_hwnd, 0, 204, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        _windowText[_hwnd] = _numberText;
        ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
    }

    private static void EnsureWindowClassRegistered()
    {
        lock (_classLock)
        {
            if (_classRegistered)
            {
                return;
            }

            _wndProc = WndProc;

            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = GetModuleHandleW(null),
                hCursor = LoadCursorW(0, IDC_ARROW),
                hbrBackground = GetStockObject(GET_STOCK_OBJECT_FLAGS.BLACK_BRUSH),
                lpszClassName = WindowClassName
            };

            if (RegisterClassExW(ref wc) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            _classRegistered = true;
        }
    }

    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_ERASEBKGND)
        {
            return 1;
        }

        if (msg == WM_PAINT)
        {
            Paint(hwnd);
            return 0;
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private static void Paint(nint hwnd)
    {
        nint hdc = BeginPaint(hwnd, out var ps);
        try
        {
            if (!GetClientRect(hwnd, out var rect))
            {
                return;
            }

            FillRect(hdc, ref rect, GetStockObject(GET_STOCK_OBJECT_FLAGS.BLACK_BRUSH));
            SetBkMode(hdc, TRANSPARENT_BK);
            SetTextColor(hdc, RGB(255, 255, 255));

            int fontHeight = -Math.Max(72, rect.Height / 3);
            nint font = CreateFontW(
                fontHeight,
                0,
                0,
                0,
                FontWeightBold,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                "Segoe UI");

            nint oldFont = font != 0 ? SelectObject(hdc, font) : 0;
            try
            {
                string text = GetWindowText(hwnd);
                DrawTextW(hdc, text, text.Length, ref rect, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
            }
            finally
            {
                if (oldFont != 0)
                {
                    SelectObject(hdc, oldFont);
                }

                if (font != 0)
                {
                    DeleteObject(font);
                }
            }
        }
        finally
        {
            EndPaint(hwnd, ref ps);
        }
    }

    private static string GetWindowText(nint hwnd)
        => hwnd == 0 ? string.Empty : _windowText.TryGetValue(hwnd, out var text) ? text : string.Empty;

    private static readonly Dictionary<nint, string> _windowText = [];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hwnd != 0)
        {
            _windowText.Remove(_hwnd);
            DestroyWindow(_hwnd);
            _hwnd = 0;
        }
    }
}
