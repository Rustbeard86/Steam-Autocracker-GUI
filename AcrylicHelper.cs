using System.Runtime.InteropServices;

namespace APPID;

[StructLayout(LayoutKind.Sequential)]
public struct WindowCompositionAttribData
{
    public int Attribute;
    public IntPtr Data;
    public int SizeOfData;
}

[StructLayout(LayoutKind.Sequential)]
public struct AccentPolicy
{
    public int AccentState;
    public int AccentFlags;
    public int GradientColor;
    public int AnimationId;
}

/// <summary>
///     Native Windows API methods for window composition and styling.
/// </summary>
public static class NativeMethods
{
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    public const int DWMWCP_ROUND = 2;
    public const int WCA_ACCENT_POLICY = 19;
    public const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;
    public const int ACCENT_ENABLE_TRANSPARENTGRADIENT = 2;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    public const int DWMWA_NCRENDERING_POLICY = 2;
    public const int DWMNCRP_DISABLED = 1;

    [DllImport("user32.dll")]
    public static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttribData data);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}

/// <summary>
///     Helper class for applying Windows acrylic effects and modern styling to forms.
/// </summary>
public static class AcrylicHelper
{
    /// <summary>
    ///     Applies acrylic effect, rounded corners, and dark mode to a Windows Form.
    /// </summary>
    /// <param name="form">The form to style.</param>
    /// <param name="roundedCorners">Enable rounded corners (Windows 11).</param>
    /// <param name="disableShadow">Disable window shadow.</param>
    public static void ApplyAcrylic(Form form, bool roundedCorners = true, bool disableShadow = false)
    {
        // Apply rounded corners (Windows 11)
        if (roundedCorners)
        {
            try
            {
                int preference = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref preference, sizeof(int));
            }
            catch
            {
                /* Ignore Windows 10 or API failures */
            }
        }

        // Disable shadow if requested
        if (disableShadow)
        {
            try
            {
                int policy = NativeMethods.DWMNCRP_DISABLED;
                NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DWMWA_NCRENDERING_POLICY, ref policy,
                    sizeof(int));
            }
            catch
            {
                /* Ignore API failures */
            }
        }

        // Enable dark mode
        try
        {
            int darkMode = 1;
            NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode,
                sizeof(int));
        }
        catch
        {
            /* Ignore API failures */
        }

        // Set opacity for tinted glass effect
        form.Opacity = 0.95;
    }
}
