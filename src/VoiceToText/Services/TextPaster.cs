using System.Runtime.InteropServices;

namespace VoiceToText.Services;

public sealed class TextPaster
{
    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private IntPtr _rememberedForegroundWindow = IntPtr.Zero;

    public void RememberForegroundWindow()
    {
        _rememberedForegroundWindow = GetForegroundWindow();
    }

    /// <summary>
    /// Copie toujours le texte dans le presse-papiers. Tente en plus de réactiver la fenêtre
    /// mémorisée au début de l'enregistrement et d'y émettre Ctrl+V.
    /// Retourne false si la réactivation ou le collage n'a pas pu être tenté (le texte reste
    /// néanmoins disponible dans le presse-papiers).
    /// </summary>
    public bool PasteToRememberedWindow(string text)
    {
        System.Windows.Clipboard.SetText(text);

        if (_rememberedForegroundWindow == IntPtr.Zero || !SetForegroundWindow(_rememberedForegroundWindow))
        {
            return false;
        }

        SendCtrlV();
        return true;
    }

    private static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyInput(VK_CONTROL, isKeyUp: false),
            KeyInput(VK_V, isKeyUp: false),
            KeyInput(VK_V, isKeyUp: true),
            KeyInput(VK_CONTROL, isKeyUp: true),
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyInput(byte virtualKey, bool isKeyUp) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = virtualKey,
                dwFlags = isKeyUp ? KEYEVENTF_KEYUP : 0,
            },
        },
    };

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    // Taille forcée à 32 octets pour correspondre à l'union native (le plus grand membre est
    // MOUSEINPUT) : sans ce padding explicite, la taille totale de INPUT ne vaudrait pas les
    // 40 octets attendus par SendInput en x64, et l'appel échouerait silencieusement.
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }
}
