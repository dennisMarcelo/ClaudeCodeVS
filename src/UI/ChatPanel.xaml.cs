using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ClaudeCodeVS.Commands;
using ClaudeCodeVS.Protocol;

namespace ClaudeCodeVS.UI
{
    public partial class ChatPanel : UserControl
    {
        private DispatcherTimer _poll;

        public ChatPanel()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _poll.Tick += (_, __) => Refresh();
            _poll.Start();
            Refresh();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _poll?.Stop();
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
                LockFileText.Text = "Lock file: " + System.IO.Path.Combine(LockFileManager.LockDirectory, server.Port + ".lock");
            }
            catch { }
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
            }
            catch { }
        }
    }
}
