using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OpenAIWatch
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)0x00000C00 | (SecurityProtocolType)0x00000300 | (SecurityProtocolType)0x000000C0;
                ServicePointManager.Expect100Continue = true;
            }
            catch
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
            }

            if (args.Length > 0)
            {
                if (String.Equals(args[0], "--install-startup", StringComparison.OrdinalIgnoreCase))
                {
                    StartupShortcut.Install();
                    return;
                }
                if (String.Equals(args[0], "--remove-startup", StringComparison.OrdinalIgnoreCase))
                {
                    StartupShortcut.Remove();
                    return;
                }
            }

            bool createdNew;
            using (var mutex = new Mutex(true, "OpenAIWatch.WindowsTray", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var app = new TrayApplication())
                {
                    Application.Run();
                }
            }
        }
    }

    internal sealed class TrayApplication : IDisposable
    {
        private readonly NotifyIcon notifyIcon;
        private readonly ContextMenuStrip menu;
        private readonly System.Windows.Forms.Timer timer;
        private readonly Icon okIcon;
        private readonly Icon badIcon;
        private readonly Icon mutedIcon;
        private readonly int timeoutSeconds;
        private readonly string proxyPortsRaw;
        private readonly string configDir;
        private readonly string configFile;

        private ToolStripMenuItem statusItem;
        private ToolStripMenuItem latencyItem;
        private ToolStripMenuItem thresholdItem;
        private ToolStripMenuItem intervalItem;
        private ToolStripMenuItem httpItem;
        private ToolStripMenuItem remoteIpItem;
        private ToolStripMenuItem proxyItem;
        private ToolStripMenuItem targetItem;
        private ToolStripMenuItem lastCheckItem;
        private ToolStripMenuItem errorItem;
        private ToolStripMenuItem[] thresholdChoices;
        private ToolStripMenuItem[] intervalChoices;
        private ToolStripMenuItem[] targetChoices;

        private int badMs;
        private int intervalSeconds;
        private int failureStreak;
        private bool isChecking;
        private bool disposed;
        private string configuredTargetUrl;
        private string targetUrl;

        public TrayApplication()
        {
            configuredTargetUrl = Nullify(Environment.GetEnvironmentVariable("OPENAI_WATCH_URL"));
            timeoutSeconds = ParseInt(Environment.GetEnvironmentVariable("OPENAI_WATCH_TIMEOUT"), 4);
            badMs = ParseInt(Environment.GetEnvironmentVariable("OPENAI_WATCH_BAD_MS"), 2000);
            proxyPortsRaw = NonEmpty(Environment.GetEnvironmentVariable("OPENAI_WATCH_PROXY_PORTS"), "7890 7897 1080 8080 6152");
            intervalSeconds = 5;
            configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenAI Watch");
            configFile = Path.Combine(configDir, "config.ini");

            ReadSettings();
            targetUrl = ResolveTargetUrl(NonEmpty(configuredTargetUrl, "https://api.openai.com/v1/models"), "https://api.openai.com/v1/models");

            okIcon = CreateIcon(Color.FromArgb(26, 152, 80), Color.FromArgb(15, 92, 49));
            badIcon = CreateIcon(Color.FromArgb(215, 48, 39), Color.FromArgb(128, 24, 19));
            mutedIcon = CreateIcon(Color.FromArgb(119, 119, 119), Color.FromArgb(75, 75, 75));

            menu = BuildMenu();
            notifyIcon = new NotifyIcon();
            notifyIcon.Icon = mutedIcon;
            notifyIcon.Text = "OpenAI Watch";
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += delegate { CheckNow(); };

            timer = new System.Windows.Forms.Timer();
            timer.Interval = intervalSeconds * 1000;
            timer.Tick += delegate { CheckNow(); };
            timer.Start();

            CheckNow();
        }

        private ContextMenuStrip BuildMenu()
        {
            var contextMenu = new ContextMenuStrip();

            statusItem = DisabledItem("Status: starting");
            latencyItem = DisabledItem("Latency: -");
            thresholdItem = DisabledItem("Red threshold: " + badMs + "ms");
            intervalItem = DisabledItem("Check interval: " + intervalSeconds + "s");
            httpItem = DisabledItem("HTTP: unknown");
            remoteIpItem = DisabledItem("Remote IP: unknown");
            proxyItem = DisabledItem("Local proxy ports open: unknown");
            targetItem = DisabledItem("Target: " + targetUrl);
            lastCheckItem = DisabledItem("Last check: -");
            errorItem = DisabledItem("Last error: -");
            errorItem.Visible = false;

            contextMenu.Items.AddRange(new ToolStripItem[]
            {
                statusItem,
                latencyItem,
                thresholdItem,
                intervalItem,
                httpItem,
                remoteIpItem,
                proxyItem,
                targetItem,
                lastCheckItem,
                errorItem
            });

            contextMenu.Items.Add(new ToolStripSeparator());

            var thresholdMenu = new ToolStripMenuItem("Red threshold");
            thresholdChoices = new[]
            {
                ThresholdChoice("1.5s strict", 1500),
                ThresholdChoice("2s normal", 2000),
                ThresholdChoice("3s relaxed", 3000),
                ThresholdChoice("5s very relaxed", 5000)
            };
            thresholdMenu.DropDownItems.AddRange(thresholdChoices);
            contextMenu.Items.Add(thresholdMenu);

            var intervalMenu = new ToolStripMenuItem("Check interval");
            intervalChoices = new[]
            {
                IntervalChoice("5s", 5),
                IntervalChoice("10s", 10),
                IntervalChoice("30s", 30),
                IntervalChoice("60s", 60)
            };
            intervalMenu.DropDownItems.AddRange(intervalChoices);
            contextMenu.Items.Add(intervalMenu);

            contextMenu.Items.Add(new ToolStripSeparator());

            var targetMenu = new ToolStripMenuItem("Target endpoint");
            targetChoices = new[]
            {
                TargetChoice("OpenAI API /v1/models", "https://api.openai.com/v1/models"),
                TargetChoice("OpenAI status JSON", "https://status.openai.com/api/v2/status.json"),
                TargetChoice("ChatGPT web", "https://chatgpt.com/")
            };
            targetMenu.DropDownItems.AddRange(targetChoices);
            contextMenu.Items.Add(targetMenu);

            contextMenu.Items.Add(new ToolStripSeparator());

            var refreshItem = new ToolStripMenuItem("Refresh");
            refreshItem.Click += delegate { CheckNow(); };
            contextMenu.Items.Add(refreshItem);

            var openStatusItem = new ToolStripMenuItem("Open OpenAI status");
            openStatusItem.Click += delegate { Process.Start("https://status.openai.com/"); };
            contextMenu.Items.Add(openStatusItem);

            var openTargetItem = new ToolStripMenuItem("Open target URL");
            openTargetItem.Click += delegate { Process.Start(targetUrl); };
            contextMenu.Items.Add(openTargetItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var installStartupItem = new ToolStripMenuItem("Install at startup");
            installStartupItem.Click += delegate { StartupShortcut.Install(); MessageBox.Show("OpenAI Watch will start with Windows.", "OpenAI Watch"); };
            contextMenu.Items.Add(installStartupItem);

            var removeStartupItem = new ToolStripMenuItem("Remove from startup");
            removeStartupItem.Click += delegate { StartupShortcut.Remove(); MessageBox.Show("OpenAI Watch startup shortcut was removed.", "OpenAI Watch"); };
            contextMenu.Items.Add(removeStartupItem);

            contextMenu.Items.Add(new ToolStripSeparator());

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += delegate { Dispose(); Application.Exit(); };
            contextMenu.Items.Add(exitItem);

            return contextMenu;
        }

        private ToolStripMenuItem DisabledItem(string text)
        {
            var item = new ToolStripMenuItem(text);
            item.Enabled = false;
            return item;
        }

        private ToolStripMenuItem ThresholdChoice(string text, int value)
        {
            var item = new ToolStripMenuItem(text);
            item.Tag = value;
            item.Click += delegate(object sender, EventArgs e)
            {
                badMs = (int)((ToolStripMenuItem)sender).Tag;
                WriteSettings();
                CheckNow();
            };
            return item;
        }

        private ToolStripMenuItem IntervalChoice(string text, int value)
        {
            var item = new ToolStripMenuItem(text);
            item.Tag = value;
            item.Click += delegate(object sender, EventArgs e)
            {
                intervalSeconds = (int)((ToolStripMenuItem)sender).Tag;
                timer.Interval = intervalSeconds * 1000;
                WriteSettings();
                UpdateMenu(statusItem.Text.Replace("Status: ", ""), latencyItem.Text.Replace("Latency: ", ""), httpItem.Text.Replace("HTTP: ", ""), remoteIpItem.Text.Replace("Remote IP: ", ""), proxyItem.Text.Replace("Local proxy ports open: ", ""), "");
                CheckNow();
            };
            return item;
        }

        private ToolStripMenuItem TargetChoice(string text, string value)
        {
            var item = new ToolStripMenuItem(text);
            item.Tag = value;
            item.Click += delegate(object sender, EventArgs e)
            {
                configuredTargetUrl = (string)((ToolStripMenuItem)sender).Tag;
                targetUrl = ResolveTargetUrl(configuredTargetUrl, "https://api.openai.com/v1/models");
                WriteSettings();
                UpdateMenu(statusItem.Text.Replace("Status: ", ""), latencyItem.Text.Replace("Latency: ", ""), httpItem.Text.Replace("HTTP: ", ""), remoteIpItem.Text.Replace("Remote IP: ", ""), proxyItem.Text.Replace("Local proxy ports open: ", ""), "");
                CheckNow();
            };
            return item;
        }

        private void CheckNow()
        {
            if (isChecking)
            {
                return;
            }

            isChecking = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                ProbeResult result;
                try
                {
                    result = ProbeOpenAI();
                }
                catch (Exception ex)
                {
                    result = new ProbeResult();
                    result.Success = false;
                    result.ErrorText = ex.Message;
                    result.RemoteIp = "unknown";
                }

                string proxySummary = GetProxySummary();

                if (!disposed)
                {
                    BeginUi(delegate
                    {
                        ApplyProbeResult(result, proxySummary);
                        isChecking = false;
                    });
                }
            });
        }

        private ProbeResult ProbeOpenAI()
        {
            var result = new ProbeResult();
            result.RemoteIp = ResolveHost(targetUrl);

            var request = (HttpWebRequest)WebRequest.Create(targetUrl);
            request.Method = "GET";
            request.UserAgent = "OpenAI-Watch-Windows/1.0";
            request.Timeout = timeoutSeconds * 1000;
            request.ReadWriteTimeout = timeoutSeconds * 1000;
            request.AllowAutoRedirect = true;

            var stopwatch = Stopwatch.StartNew();
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    stopwatch.Stop();
                    result.Success = true;
                    result.HttpCode = (int)response.StatusCode;
                    result.LatencyMs = (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds);
                    return result;
                }
            }
            catch (WebException ex)
            {
                stopwatch.Stop();
                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    using (response)
                    {
                        result.Success = true;
                        result.HttpCode = (int)response.StatusCode;
                        result.LatencyMs = (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds);
                        result.ErrorText = ShortError(ex.Message);
                        return result;
                    }
                }

                result.Success = false;
                result.HttpCode = 0;
                result.LatencyMs = -1;
                result.ErrorText = ShortError(ex.Message);
                return result;
            }
        }

        private void ApplyProbeResult(ProbeResult result, string proxySummary)
        {
            string title;
            string status;
            string state;
            string latencyText;

            if (!result.Success || result.HttpCode == 0)
            {
                failureStreak++;
                if (failureStreak >= 2)
                {
                    title = "AI DOWN";
                    status = "DOWN";
                    state = "bad";
                }
                else
                {
                    title = "AI OK";
                    status = "MISSED";
                    state = "muted";
                }
                latencyText = "-";
                UpdateMenu(status, latencyText, "unknown", result.RemoteIp, proxySummary, result.ErrorText);
                SetStatusIcon(state, title, status);
                return;
            }

            failureStreak = 0;
            latencyText = result.LatencyMs + "ms";

            if (result.HttpCode >= 500)
            {
                title = "AI " + latencyText;
                status = "OPENAI_5XX";
                state = "bad";
            }
            else if (result.LatencyMs < badMs)
            {
                title = "AI OK";
                status = "OK";
                state = "ok";
            }
            else
            {
                title = "AI " + latencyText;
                status = "TOO_SLOW";
                state = "bad";
            }

            UpdateMenu(status, latencyText, result.HttpCode.ToString(), result.RemoteIp, proxySummary, result.ErrorText);
            SetStatusIcon(state, title, "HTTP " + result.HttpCode + ", " + latencyText);
        }

        private void SetStatusIcon(string state, string title, string detail)
        {
            notifyIcon.Icon = state == "ok" ? okIcon : state == "bad" ? badIcon : mutedIcon;
            notifyIcon.Text = title.Length > 63 ? title.Substring(0, 63) : title;
            notifyIcon.BalloonTipTitle = title;
            notifyIcon.BalloonTipText = detail;
        }

        private void UpdateMenu(string status, string latency, string httpCode, string remoteIp, string proxySummary, string errorText)
        {
            statusItem.Text = "Status: " + status;
            latencyItem.Text = "Latency: " + latency;
            thresholdItem.Text = "Red threshold: " + badMs + "ms";
            intervalItem.Text = "Check interval: " + intervalSeconds + "s";
            httpItem.Text = "HTTP: " + httpCode;
            remoteIpItem.Text = "Remote IP: " + remoteIp;
            proxyItem.Text = "Local proxy ports open: " + proxySummary;
            targetItem.Text = "Target: " + targetUrl;
            lastCheckItem.Text = "Last check: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            errorItem.Visible = !String.IsNullOrWhiteSpace(errorText);
            errorItem.Text = "Last error: " + errorText;

            foreach (var item in thresholdChoices)
            {
                item.Checked = (int)item.Tag == badMs;
            }
            foreach (var item in intervalChoices)
            {
                item.Checked = (int)item.Tag == intervalSeconds;
            }
            foreach (var item in targetChoices)
            {
                item.Checked = String.Equals(ResolveTargetUrl((string)item.Tag, "https://api.openai.com/v1/models"), targetUrl, StringComparison.OrdinalIgnoreCase);
            }
        }

        private string GetProxySummary()
        {
            var found = "";
            var pieces = proxyPortsRaw.Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var piece in pieces)
            {
                int port;
                if (Int32.TryParse(piece, out port) && IsPortOpen(port))
                {
                    if (found.Length > 0)
                    {
                        found += ",";
                    }
                    found += port.ToString();
                }
            }

            return found.Length == 0 ? "none" : found;
        }

        private bool IsPortOpen(int port)
        {
            using (var client = new TcpClient())
            {
                try
                {
                    var async = client.BeginConnect("127.0.0.1", port, null, null);
                    if (!async.AsyncWaitHandle.WaitOne(300))
                    {
                        return false;
                    }
                    client.EndConnect(async);
                    return client.Connected;
                }
                catch
                {
                    return false;
                }
            }
        }

        private string ResolveHost(string url)
        {
            try
            {
                var host = new Uri(url).Host;
                var addresses = Dns.GetHostAddresses(host);
                return addresses.Length > 0 ? addresses[0].ToString() : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private void ReadSettings()
        {
            if (!System.IO.File.Exists(configFile))
            {
                return;
            }

            foreach (var line in System.IO.File.ReadAllLines(configFile))
            {
                var parts = line.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                {
                    continue;
                }

                string key = parts[0].Trim();
                string valueText = parts[1].Trim();
                if (key == "target_url" && string.IsNullOrWhiteSpace(configuredTargetUrl) && !string.IsNullOrWhiteSpace(valueText))
                {
                    configuredTargetUrl = valueText;
                    continue;
                }

                int value;
                if (!Int32.TryParse(parts[1], out value))
                {
                    continue;
                }

                if (key == "bad_ms")
                {
                    badMs = value;
                }
                else if (key == "interval_seconds" && (value == 5 || value == 10 || value == 30 || value == 60))
                {
                    intervalSeconds = value;
                }
            }
        }

        private void WriteSettings()
        {
            Directory.CreateDirectory(configDir);
            System.IO.File.WriteAllText(
                configFile,
                "bad_ms=" + badMs + Environment.NewLine +
                "interval_seconds=" + intervalSeconds + Environment.NewLine +
                "target_url=" + targetUrl + Environment.NewLine);
        }

        private void BeginUi(Action action)
        {
            if (notifyIcon.ContextMenuStrip != null && notifyIcon.ContextMenuStrip.IsHandleCreated)
            {
                notifyIcon.ContextMenuStrip.BeginInvoke(action);
            }
            else
            {
                action();
            }
        }

        private static Icon CreateIcon(Color fill, Color border)
        {
            using (var bitmap = new Bitmap(64, 64))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, 4))
            using (var font = new Font("Segoe UI", 17, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var textBrush = new SolidBrush(Color.White))
            using (var format = new StringFormat())
            {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(brush, 5, 5, 54, 54);
                graphics.DrawEllipse(pen, 5, 5, 54, 54);
                format.Alignment = StringAlignment.Center;
                format.LineAlignment = StringAlignment.Center;
                graphics.DrawString("AI", font, textBrush, new RectangleF(0, 0, 64, 62), format);
                IntPtr handle = bitmap.GetHicon();
                return (Icon)Icon.FromHandle(handle).Clone();
            }
        }

        private static string NonEmpty(string value, string fallback)
        {
            return String.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string Nullify(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string ResolveTargetUrl(string value, string fallback)
        {
            var normalized = Nullify(value);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = fallback;
            }

            normalized = normalized.Trim();
            if (!normalized.Contains("://"))
            {
                normalized = "https://" + normalized;
            }

            Uri uri;
            if (!Uri.TryCreate(normalized, UriKind.Absolute, out uri))
            {
                return fallback;
            }

            var path = uri.AbsolutePath;
            if (string.IsNullOrEmpty(path) || path == "/")
            {
                return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/v1/models";
            }

            return normalized;
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return Int32.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static string ShortError(string value)
        {
            if (String.IsNullOrEmpty(value))
            {
                return "";
            }
            value = value.Replace("\r", " ").Replace("\n", " ");
            return value.Length > 160 ? value.Substring(0, 160) : value;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            timer.Stop();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            menu.Dispose();
            okIcon.Dispose();
            badIcon.Dispose();
            mutedIcon.Dispose();
            timer.Dispose();
        }
    }

    internal sealed class ProbeResult
    {
        public bool Success;
        public int HttpCode;
        public int LatencyMs;
        public string RemoteIp;
        public string ErrorText;
    }

    internal static class StartupShortcut
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "OpenAI Watch";

        public static void Install()
        {
            string exePath = Application.ExecutablePath;
            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                key.SetValue(ValueName, "\"" + exePath + "\"", RegistryValueKind.String);
            }
        }

        public static void Remove()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                if (key != null)
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }
}
