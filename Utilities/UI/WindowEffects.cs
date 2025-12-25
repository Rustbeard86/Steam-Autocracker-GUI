using System.Runtime.InteropServices;

namespace APPID.Utilities.UI;

/// <summary>
///     Windows API structures and P/Invoke declarations for UI effects.
///     These are used to apply visual effects like Acrylic blur and rounded corners on Windows 11.
/// </summary>
public static class WindowEffects
{
    // === Constants for DWM (Desktop Window Manager) ===

    /// <summary>
    ///     DWM attribute for window corner preference (Windows 11).
    /// </summary>
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    /// <summary>
    ///     Window corner preference: Rounded corners.
    /// </summary>
    public const int DWMWCP_ROUND = 2;

    /// <summary>
    ///     Window composition attribute for accent policy.
    /// </summary>
    public const int WCA_ACCENT_POLICY = 19;

    /// <summary>
    ///     Accent state: Enable acrylic blur behind window.
    /// </summary>
    public const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

    // === P/Invoke Declarations ===

    /// <summary>
    ///     Sets Desktop Window Manager (DWM) attributes for a window.
    ///     Used for applying effects like rounded corners on Windows 11.
    /// </summary>
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attr,
        ref int attrValue,
        int attrSize);

    /// <summary>
    ///     Sets window composition attributes (used for acrylic blur effects).
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int SetWindowCompositionAttribute(
        IntPtr hwnd,
        ref WindowCompositionAttribData data);

    // === Helper Methods ===

    /// <summary>
    ///     Applies rounded corners to a window (Windows 11 only).
    /// </summary>
    /// <param name="handle">Window handle.</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool ApplyRoundedCorners(IntPtr handle)
    {
        try
        {
            int preference = DWMWCP_ROUND;
            int result = DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            return result == 0; // 0 indicates success
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Applies acrylic blur effect to a window.
    /// </summary>
    /// <param name="handle">Window handle.</param>
    /// <param name="opacity">Opacity level (0-255).</param>
    /// <returns>True if successful, false otherwise.</returns>
    public static bool ApplyAcrylicEffect(IntPtr handle, byte opacity = 230)
    {
        try
        {
            var accent = new AccentPolicy
            {
                AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND,
                GradientColor = (opacity << 24) | 0x000000 // ARGB: opacity + black
            };

            int accentStructSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);

            try
            {
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttribData
                {
                    Attribute = WCA_ACCENT_POLICY, Data = accentPtr, SizeOfData = accentStructSize
                };

                int result = SetWindowCompositionAttribute(handle, ref data);
                return result != 0; // Non-zero indicates success
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }
        catch
        {
            return false;
        }
    }

    // === Structures ===

    /// <summary>
    ///     Window composition attribute data structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowCompositionAttribData
    {
        /// <summary>
        ///     The attribute type (e.g., WCA_ACCENT_POLICY).
        /// </summary>
        public int Attribute;

        /// <summary>
        ///     Pointer to the attribute data.
        /// </summary>
        public IntPtr Data;

        /// <summary>
        ///     Size of the data pointed to by Data.
        /// </summary>
        public int SizeOfData;
    }

    /// <summary>
    ///     Accent policy structure for window composition effects.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AccentPolicy
    {
        /// <summary>
        ///     The accent state (e.g., ACCENT_ENABLE_ACRYLICBLURBEHIND).
        /// </summary>
        public int AccentState;

        /// <summary>
        ///     Flags for the accent policy.
        /// </summary>
        public int AccentFlags;

        /// <summary>
        ///     Gradient color (ARGB format).
        /// </summary>
        public int GradientColor;

        /// <summary>
        ///     Animation ID.
        /// </summary>
        public int AnimationId;
    }
}
