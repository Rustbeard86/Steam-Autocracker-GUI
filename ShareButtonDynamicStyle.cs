using Timer = System.Windows.Forms.Timer;

namespace APPID;

public class DynamicShareButton : Button
{
    public enum ShareStatus
    {
        Normal, // No requests - Blue
        HasRequested, // Has games people want - Hot Pink
        Urgent // Many people requesting - Bright Red
    }

    private ShareStatus _currentStatus = ShareStatus.Normal;
    private int _pulseStep;
    private Timer _pulseTimer;
    private bool _pulsing;
    private int _requestCount;

    public DynamicShareButton()
    {
        InitializeButton();
        SetupPulseEffect();
    }

    private void InitializeButton()
    {
        Text = "🤝 Share My Games";
        Font = new Font("Segoe UI", 11, FontStyle.Bold);
        Size = new Size(200, 50);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 2;
        Cursor = Cursors.Hand;

        // Default blue state
        SetNormalStyle();
    }

    // Call this after checking database on app launch
    public async Task UpdateStatusFromRequests(List<string> userAppIds)
    {
        try
        {
            // Check which of user's games are requested
            var requestedGames = await CheckRequestedGames(userAppIds);

            if (requestedGames.Count > 10)
            {
                SetUrgentStyle(requestedGames.Count);
            }
            else if (requestedGames.Count > 0)
            {
                SetRequestedStyle(requestedGames.Count);
            }
            else
            {
                SetNormalStyle();
            }
        }
        catch
        {
            SetNormalStyle();
        }
    }

    private void SetNormalStyle()
    {
        _currentStatus = ShareStatus.Normal;
        _requestCount = 0;

        // Bright Electric Blue
        BackColor = Color.FromArgb(0, 150, 255);
        ForeColor = Color.White;
        FlatAppearance.BorderColor = Color.FromArgb(0, 100, 200);
        Text = "🤝 Share My Games";

        StopPulse();
    }

    private void SetRequestedStyle(int count)
    {
        _currentStatus = ShareStatus.HasRequested;
        _requestCount = count;

        // HOT PINK - Someone needs your stuff!
        BackColor = Color.FromArgb(255, 20, 147); // Deep Pink
        ForeColor = Color.White;
        FlatAppearance.BorderColor = Color.FromArgb(255, 105, 180); // Hot Pink
        Text = $"🔥 Share ({count} needed)";

        // Subtle pulse effect
        StartPulse(false);
    }

    private void SetUrgentStyle(int count)
    {
        _currentStatus = ShareStatus.Urgent;
        _requestCount = count;

        // BRIGHT RED - Many people need your games!
        BackColor = Color.FromArgb(255, 0, 50);
        ForeColor = Color.Yellow;
        FlatAppearance.BorderColor = Color.FromArgb(255, 100, 100);
        Text = $"🚨 SHARE! ({count} NEEDED)";

        // Aggressive pulse
        StartPulse(true);
    }

    private void SetupPulseEffect()
    {
        _pulseTimer = new Timer();
        _pulseTimer.Interval = 100;
        _pulseTimer.Tick += (s, e) =>
        {
            if (!_pulsing)
            {
                return;
            }

            _pulseStep = (_pulseStep + 1) % 20;

            // Calculate pulse brightness
            double pulse = Math.Sin(_pulseStep * Math.PI / 10) * 0.3 + 0.7;

            if (_currentStatus == ShareStatus.HasRequested)
            {
                int r = (int)(255 * pulse);
                int g = (int)(20 * pulse);
                int b = (int)(147 * pulse);
                BackColor = Color.FromArgb(r, g, b);
            }
            else if (_currentStatus == ShareStatus.Urgent)
            {
                int r = 255;
                int g = (int)(50 * pulse);
                int b = (int)(50 * pulse);
                BackColor = Color.FromArgb(r, g, b);
            }
        };
    }

    private void StartPulse(bool aggressive)
    {
        _pulsing = true;
        _pulseTimer.Interval = aggressive ? 50 : 100;
        _pulseTimer.Start();
    }

    private void StopPulse()
    {
        _pulsing = false;
        _pulseTimer.Stop();
    }

    private async Task<List<RequestedGameInfo>> CheckRequestedGames(List<string> userAppIds)
    {
        // Call API to check which games are requested
        // Mock implementation
        await Task.Delay(100);

        var requested = new List<RequestedGameInfo>();

        // Simulate checking
        foreach (var appId in userAppIds)
        {
            // Random simulation - replace with actual API call
            if (new Random().Next(100) < 10) // 10% chance
            {
                requested.Add(new RequestedGameInfo { AppId = appId, RequestCount = new Random().Next(1, 5) });
            }
        }

        return requested;
    }

    private class RequestedGameInfo
    {
        public string AppId { get; set; }
        public int RequestCount { get; set; }
    }
}

// Integration with main form
public static class ShareButtonIntegration
{
    public static DynamicShareButton CreateShareButton(Form mainForm, List<string> userGameAppIds)
    {
        var shareButton = new DynamicShareButton();
        shareButton.Location = new Point(mainForm.Width - 220, 20);
        shareButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        // Update button style based on requests
        Task.Run(async () =>
        {
            await shareButton.UpdateStatusFromRequests(userGameAppIds);

            // Add tooltip with details
            mainForm.Invoke(() =>
            {
                var tooltip = new ToolTip();

                if (shareButton.Text.Contains("needed"))
                {
                    tooltip.SetToolTip(shareButton,
                        "🔥 You have games that the community needs!\n" +
                        "Click to see which games and share them.");
                }
                else
                {
                    tooltip.SetToolTip(shareButton,
                        "Share your Steam games with the community");
                }
            });
        });

        shareButton.Click += async (s, e) =>
        {
            // Open share dialog
            // If has requested games, highlight them
            await Task.CompletedTask; // Placeholder for future async operation
        };

        return shareButton;
    }

    // Small notification badge
    public static Label CreateRequestBadge(int count)
    {
        var badge = new Label
        {
            Text = count.ToString(),
            Size = new Size(25, 25),
            BackColor = Color.Red,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = count > 0
        };

        // Make it circular
        badge.Paint += (s, e) =>
        {
            e.Graphics.Clear(badge.Parent.BackColor);
            using (var brush = new SolidBrush(Color.Red))
            {
                e.Graphics.FillEllipse(brush, 0, 0, badge.Width - 1, badge.Height - 1);
            }

            using (var brush = new SolidBrush(Color.White))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(badge.Text, badge.Font, brush,
                    new RectangleF(0, 0, badge.Width, badge.Height), sf);
            }
        };

        return badge;
    }
}

// Status bar with live colors
public class CommunityStatusBar : StatusStrip
{
    private readonly ToolStripLabel _filledLabel;
    private readonly ToolStripLabel _honorLabel;
    private readonly ToolStripLabel _requestLabel;

    public CommunityStatusBar()
    {
        BackColor = Color.FromArgb(30, 30, 30);

        _requestLabel =
            new ToolStripLabel("Requests: --") { ForeColor = Color.White, Margin = new Padding(5, 3, 5, 3) };

        _honorLabel = new ToolStripLabel("Honored: --") { ForeColor = Color.White, Margin = new Padding(5, 3, 5, 3) };

        _filledLabel = new ToolStripLabel("Filled: --") { ForeColor = Color.White, Margin = new Padding(5, 3, 5, 3) };

        Items.AddRange(_requestLabel, _honorLabel, _filledLabel);

        // Auto-update every minute
        var timer = new Timer { Interval = 60000 };
        timer.Tick += async (s, e) => await UpdateStats();
        timer.Start();

        Task.Run(async () => await UpdateStats());
    }

    private async Task UpdateStats()
    {
        try
        {
            var stats = await RequestApi.GetGlobalStats();

            Invoke(() =>
            {
                _requestLabel.Text = $"Requests: {stats.TotalRequests}";
                _honorLabel.Text = $"Honored: {stats.TotalHonored}";
                _filledLabel.Text = $"Filled: {stats.TotalFilled}";

                // Color code based on fulfillment rate
                double fillRate = (double)stats.TotalFilled / Math.Max(1, stats.TotalRequests);

                if (fillRate > 0.8)
                {
                    // Good - greenish
                    _requestLabel.ForeColor = Color.FromArgb(100, 255, 100);
                    _filledLabel.ForeColor = Color.FromArgb(100, 255, 100);
                }
                else if (fillRate > 0.5)
                {
                    // OK - yellowish
                    _requestLabel.ForeColor = Color.FromArgb(255, 255, 100);
                    _filledLabel.ForeColor = Color.FromArgb(255, 255, 100);
                }
                else
                {
                    // Many unfilled - pinkish
                    _requestLabel.ForeColor = Color.FromArgb(255, 100, 150);
                    _filledLabel.ForeColor = Color.FromArgb(255, 100, 150);
                }
            });
        }
        catch { }
    }
}
