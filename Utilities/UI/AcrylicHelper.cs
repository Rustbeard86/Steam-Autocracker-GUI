using System.Runtime.InteropServices;

namespace APPID.Utilities.UI;

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
    public const int DwmwaWindowCornerPreference = 33;
    public const int DwmwcpRound = 2;
    public const int WcaAccentPolicy = 19;
    public const int AccentEnableAcrylicblurbehind = 4;
    public const int AccentEnableTransparentgradient = 2;
    public const int DwmwaUseImmersiveDarkMode = 20;
    public const int DwmwaSystembackdropType = 38;
    public const int DwmwaNcrenderingPolicy = 2;
    public const int DwmncrpDisabled = 1;

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
                int preference = NativeMethods.DwmwcpRound;
                NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DwmwaWindowCornerPreference,
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
                int policy = NativeMethods.DwmncrpDisabled;
                NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DwmwaNcrenderingPolicy, ref policy,
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
            NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DwmwaUseImmersiveDarkMode, ref darkMode,
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
