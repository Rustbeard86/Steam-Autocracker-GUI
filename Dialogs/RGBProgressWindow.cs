using System.ComponentModel;
using System.Runtime.InteropServices;
using APPID.Utilities.UI;
using Timer = System.Windows.Forms.Timer;

namespace APPID.Dialogs;

/// <summary>
///     RGB-animated progress window for upload/compression operations
/// </summary>
public class RgbProgressWindow : Form
{
    private const int WmNclbuttondown = 0xA1;
    private const int Htcaption = 0x2;

    private Button _btnCancel;
    private int _colorStep;
    private DateTime _countdownStartTime;
    private long _currentFileSize;
    private Label _lblScrollingInfo;
    private Timer _rgbTimer;
    private int _scrollOffset;
    private string _scrollText = "";
    private Timer _scrollTimer;
    private int _totalCountdownMinutes;

    internal Label LblStatus;
    internal ProgressBar ProgressBar;

    public RgbProgressWindow(string gameName, string type, Form parent = null)
    {
        GameName = gameName;
        InitializeWindow(gameName, type);

        // Set parent relationship and centering
        if (parent != null)
        {
            Owner = parent;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = parent.TopMost; // Match parent's TopMost state
        }

        // Enable dragging the form
        MouseDown += RGBProgressWindow_MouseDown;
    }

    public bool WasCancelled { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string OneFichierUrl { get; set; }

    public string GameName { get; private set; }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void InitializeWindow(string gameName, string type)
    {
        Text = $"Sharing {gameName} ({type})";
        Size = new Size(500, 260);
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(5, 8, 20);
        ShowInTaskbar = false;

        // Apply rounded corners when form loads
        Load += (s, e) =>
        {
            try
            {
                int preference = NativeMethods.DwmwcpRound;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DwmwaWindowCornerPreference,
                    ref preference, sizeof(int));
            }
            catch { }

            ApplyAcrylicToProgressWindow();
        };

        // Scrolling info label - hidden by default
        _lblScrollingInfo = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 9, FontStyle.Italic),
            ForeColor = Color.FromArgb(120, 192, 255),
            Location = new Point(25, 25),
            Size = new Size(450, 20),
            AutoSize = false,
            Visible = false
        };

        LblStatus = new Label
        {
            Text = "Preparing...",
            Font = new Font("Segoe UI", 11),
            ForeColor = Color.Cyan,
            Location = new Point(25, 70),
            Size = new Size(450, 30)
        };

        ProgressBar = new ModernProgressBar
        {
            Location = new Point(25, 110),
            Size = new Size(450, 30),
            Style = ProgressBarStyle.Continuous,
            BackColor = Color.FromArgb(15, 15, 15),
            Value = 0,
            Minimum = 0,
            Maximum = 100
        };

        _btnCancel = new Button
        {
            Text = "✖ Cancel",
            Location = new Point(200, 165),
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Standard,
            Font = new Font("Segoe UI", 9)
        };
        _btnCancel.Click += (s, e) =>
        {
            WasCancelled = true;
            LblStatus.Text = "Cancelled by user";
            LblStatus.ForeColor = Color.Orange;
            _btnCancel.Enabled = false;
            _rgbTimer?.Stop();
            _scrollTimer?.Stop();
            Close();
        };

        Controls.AddRange(_lblScrollingInfo, LblStatus, ProgressBar, _btnCancel);

        // RGB effect
        SetupRgbEffect();
    }

    private void ApplyAcrylicToProgressWindow()
    {
        AcrylicHelper.ApplyAcrylic(this, false);
    }

    private void SetupRgbEffect()
    {
        _rgbTimer = new Timer { Interval = 50 };
        _rgbTimer.Tick += (s, e) =>
        {
            _colorStep = (_colorStep + 5) % 360;
            var color = HslToRgb(_colorStep, 1.0, 0.72);

            LblStatus.ForeColor = color;

            if (_lblScrollingInfo is { Visible: true })
            {
                _lblScrollingInfo.ForeColor = color;
            }

            try
            {
                ProgressBar.ForeColor = color;
                ProgressBar.Style = ProgressBarStyle.Continuous;
            }
            catch { }
        };
        _rgbTimer.Start();
    }

    private Color HslToRgb(double h, double s, double l)
    {
        double r, g, b;
        h = h / 360.0;

        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3);
        }

        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }

    private double HueToRgb(double p, double q, double t)
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
            return p + (q - p) * 6 * t;
        }

        if (t < 1.0 / 2)
        {
            return q;
        }

        if (t < 2.0 / 3)
        {
            return p + (q - p) * (2.0 / 3 - t) * 6;
        }

        return p;
    }

    public void UpdateStatus(string status)
    {
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                LblStatus.Text = status;
                ProgressBar.Value = Math.Min(ProgressBar.Value + 20, 100);
            });
        }
    }

    public void SetProgress(int percentage, string status)
    {
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                LblStatus.Text = status;
                ProgressBar.Value = Math.Max(0, Math.Min(100, percentage));
            });
        }
    }

    public void Complete(string url)
    {
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                LblStatus.Text = "✅ Complete! Upload URL copied to clipboard.";
                LblStatus.ForeColor = Color.Lime;
                ProgressBar.Value = 100;
                Clipboard.SetText(url);
                _rgbTimer.Stop();
                BackColor = Color.FromArgb(0, 50, 0);
            });
        }
    }

    public void ShowLargeFileWarning(long fileSizeBytes, int estimatedMinutes)
    {
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                _countdownStartTime = DateTime.Now;
                _totalCountdownMinutes = estimatedMinutes;
                _currentFileSize = fileSizeBytes;

                UpdateCountdownText();
                _lblScrollingInfo.Visible = true;

                if (_scrollTimer == null)
                {
                    _scrollTimer = new Timer { Interval = 1000 };
                    _scrollTimer.Tick += (s, e) =>
                    {
                        UpdateCountdownText();

                        if (!string.IsNullOrEmpty(_scrollText))
                        {
                            _scrollOffset = (_scrollOffset + 2) % _scrollText.Length;
                            string displayText = _scrollText.Substring(_scrollOffset) +
                                                 _scrollText.Substring(0, _scrollOffset);
                            _lblScrollingInfo.Text =
                                displayText.Substring(0, Math.Min(displayText.Length, 60));
                        }
                    };
                }

                _scrollTimer.Start();
            });
        }
    }

    private void UpdateCountdownText()
    {
        var elapsed = DateTime.Now - _countdownStartTime;
        var remaining = _totalCountdownMinutes - (int)elapsed.TotalMinutes;
        if (remaining < 1)
        {
            remaining = 1;
        }

        string minuteText = remaining == 1 ? "minute" : "minutes";

        string compressionTip = "";
        if (_currentFileSize > 10L * 1024 * 1024 * 1024)
        {
            compressionTip = "     💡 Tip: 7z ultra compression can make processing up to 40% faster!";
        }

        _scrollText =
            $"     Waiting for 1fichier to scan the file...     This will take roughly {remaining} more {minuteText}...{compressionTip}     Cancel anytime to get the 1fichier link...     ";
    }

    public void ShowError(string error)
    {
        if (IsHandleCreated)
        {
            Invoke(() =>
            {
                LblStatus.Text = error;
                LblStatus.ForeColor = Color.Red;
                _rgbTimer.Stop();
                BackColor = Color.FromArgb(50, 0, 0);
            });
        }
    }

    private void RGBProgressWindow_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, WmNclbuttondown, Htcaption, 0);
        }
    }
}
