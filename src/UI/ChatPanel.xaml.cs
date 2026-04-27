using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClaudeCodeVS.Commands;
using ClaudeCodeVS.Diagnostics;
using ClaudeCodeVS.Protocol;

namespace ClaudeCodeVS.UI
{
    public partial class ChatPanel : UserControl
    {
        private DispatcherTimer _poll;
        private readonly EventHandler _logHandler;

        public ChatPanel()
        {
            InitializeComponent();
            _logHandler = OnLoggerEntryAdded;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _poll.Tick += (_, __) => Refresh();
            _poll.Start();
            Logger.EntryAdded += _logHandler;
            Refresh();
            RefreshEvents();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _poll?.Stop();
            Logger.EntryAdded -= _logHandler;
        }

        private void OnLoggerEntryAdded(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.BeginInvoke(new Action(RefreshEvents));
                return;
            }
            RefreshEvents();
        }

        private void Refresh()
        {
            try
            {
                var pkg = VsixPackage.Instance;
                var server = pkg?.Server;
                if (server == null)
                {
                    StatusText.Text = "Starting...";
                    return;
                }
                StatusText.Text = "Running";
                PortText.Text = "Port: " + server.Port;
                ClientsText.Text = "Connected sessions: " + server.ClientCount;
                LockFileText.Text = "Lock file: " + Path.Combine(LockFileManager.LockDirectory, server.Port + ".lock");
                LogFilePathText.Text = LogFilePath ?? "";
            }
            catch (Exception ex)
            {
                Logger.Warn("ChatPanel", "Refresh failed: " + ex.Message);
            }
        }

        private void RefreshEvents()
        {
            try
            {
                var entries = Logger.Snapshot();
                if (entries.Count == 0)
                {
                    EventsText.Text = "(no events yet)";
                    return;
                }
                var sb = new StringBuilder(entries.Count * 80);
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    sb.AppendLine(entries[i].Format());
                }
                EventsText.Text = sb.ToString();
            }
            catch
            {
            }
        }

        private static string LogFilePath
        {
            get
            {
                try
                {
                    return Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ClaudeCodeVS",
                        "diagnostics.log");
                }
                catch
                {
                    return null;
                }
            }
        }

        private void OnStartTerminal(object sender, RoutedEventArgs e)
        {
            OpenTerminalWithClaudeCommand.Execute(VsixPackage.Instance);
        }

        private void OnCopyEnv(object sender, RoutedEventArgs e)
        {
            var server = VsixPackage.Instance?.Server;
            if (server == null) return;
            try
            {
                string cmd = "set CLAUDE_CODE_SSE_PORT=" + server.Port + " && set CLAUDECODE=1 && claude";
                Clipboard.SetText(cmd);
                Logger.Info("ChatPanel", "Copied env-var setup command to clipboard.");
            }
            catch (Exception ex)
            {
                Logger.Error("ChatPanel", "Clipboard write failed", ex);
            }
        }

        private void OnOpenLogFile(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = LogFilePath;
                if (string.IsNullOrEmpty(path)) return;
                if (!File.Exists(path))
                {
                    Logger.Info("ChatPanel", "Log file does not exist yet: " + path);
                    return;
                }
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Error("ChatPanel", "Failed to open log file", ex);
            }
        }
    }
}
