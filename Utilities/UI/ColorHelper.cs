namespace APPID.Utilities.UI;

/// <summary>
///     Helper for color conversions and effects
/// </summary>
public static class ColorHelper
{
    /// <summary>
    ///     Converts HSL color values to RGB
    /// </summary>
    /// <param name="h">Hue (0-360)</param>
    /// <param name="s">Saturation (0-1)</param>
    /// <param name="l">Lightness (0-1)</param>
    /// <returns>RGB Color</returns>
    public static Color HslToRgb(double h, double s, double l)
    {
        h = h / 360.0;
        double r, g, b;

        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double HueToRgb(double pVal, double qVal, double t)
            {
                if (t < 0)
                {
                    t += 1;
                }

                if (t > 1)
                {
                    t -= 1;
                }

                if (t < 1.0 / 6)
                {
                    return pVal + (qVal - pVal) * 6 * t;
                }

                if (t < 1.0 / 2)
                {
                    return qVal;
                }

                if (t < 2.0 / 3)
                {
                    return pVal + (qVal - pVal) * (2.0 / 3 - t) * 6;
                }

                return pVal;
            }

            var qHsl = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var pHsl = 2 * l - qHsl;
            r = HueToRgb(pHsl, qHsl, h + 1.0 / 3);
            g = HueToRgb(pHsl, qHsl, h);
            b = HueToRgb(pHsl, qHsl, h - 1.0 / 3);
        }

        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    /// <summary>
    ///     Creates a cycling color effect for smooth animations
    /// </summary>
    /// <param name="currentHue">Current hue value (0-360), will be updated</param>
    /// <param name="incrementBy">Amount to increment hue by (default 3)</param>
    /// <param name="saturation">Saturation (0-1, default 1.0)</param>
    /// <param name="lightness">Lightness (0-1, default 0.75)</param>
    /// <returns>Next color in the cycle and updated hue</returns>
    public static (Color color, double newHue) GetNextCycleColor(double currentHue, double incrementBy = 3,
        double saturation = 1.0, double lightness = 0.75)
    {
        double newHue = (currentHue + incrementBy) % 360;
        Color color = HslToRgb(newHue, saturation, lightness);
        return (color, newHue);
    }
}
