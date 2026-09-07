using System.Runtime.InteropServices;
using VoiceToText.Models;

namespace VoiceToText.Services;

public sealed class MouseTriggerService : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_XBUTTONDOWN = 0x020B;

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    // Le délégué doit rester référencé (champ) tant que le hook est actif, sinon le GC peut le
    // collecter et Windows appellera un pointeur de fonction invalide.
    private LowLevelMouseProc? _proc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private MouseTriggerButton? _armedButton;

    public event EventHandler? TriggerActivated;

    public void Start(MouseTriggerButton button)
    {
        Stop();

        _armedButton = button;
        _proc = HookCallback;
        using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule!;
        _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(currentModule.ModuleName), 0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Impossible d'installer l'écoute globale du bouton de souris.");
        }
    }

    public void Stop()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        _proc = null;
        _armedButton = null;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _armedButton is { } armedButton)
        {
            var message = wParam.ToInt32();
            if (message == WM_MBUTTONDOWN && armedButton == MouseTriggerButton.Middle)
            {
                TriggerActivated?.Invoke(this, EventArgs.Empty);
            }
            else if (message == WM_XBUTTONDOWN)
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var xButton = (hookStruct.mouseData >> 16) & 0xFFFF;
                if ((xButton == 1 && armedButton == MouseTriggerButton.XButton1) ||
                    (xButton == 2 && armedButton == MouseTriggerButton.XButton2))
                {
                    TriggerActivated?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public int X;
        public int Y;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
