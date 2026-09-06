using System.Windows.Interop;
using MetroHub.Core.Models;

namespace MetroHub.Core.Services;

public class HotkeyService : IDisposable
{
    private const int HotkeyId = 9001;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private bool _isRegistered;

    public event Action? HotkeyPressed;

    public bool Register(IntPtr handle, AppSettings settings)
    {
        _windowHandle = handle;
        Unregister();

        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(HwndHook);

        _isRegistered = NativeMethods.RegisterHotKey(
            _windowHandle,
            HotkeyId,
            settings.HotkeyModifiers | NativeMethods.MOD_NOREPEAT,
            settings.HotkeyKey);

        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered && _windowHandle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_windowHandle, HotkeyId);
            _isRegistered = false;
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource = null;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
    }
}
