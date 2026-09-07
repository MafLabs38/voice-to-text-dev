using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace VoiceToText.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0xB000;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private readonly HwndSource _messageSource;
    private bool _registered;
    private bool _disposed;

    public event EventHandler? HotkeyPressed;

    public GlobalHotkeyService()
    {
        var parameters = new HwndSourceParameters("VoiceToTextHotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ExtendedWindowStyle = 0x80, // WS_EX_TOOLWINDOW: pas d'entrée dans la barre des tâches / alt-tab
        };
        _messageSource = new HwndSource(parameters);
        _messageSource.AddHook(WndProc);
    }

    public void Register(ModifierKeys modifiers, Key key)
    {
        Unregister();

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        var nativeModifiers = ToNativeModifiers(modifiers);

        _registered = RegisterHotKey(_messageSource.Handle, HotkeyId, nativeModifiers, virtualKey);
        if (!_registered)
        {
            throw new InvalidOperationException(
                "Impossible d'enregistrer le raccourci global : il est peut-être déjà utilisé par une autre application.");
        }
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_messageSource.Handle, HotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= MOD_WIN;
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        _messageSource.RemoveHook(WndProc);
        _messageSource.Dispose();
        _disposed = true;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
