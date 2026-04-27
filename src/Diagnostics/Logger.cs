using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ClaudeCodeVS.Diagnostics
{
    internal enum LogLevel
    {
        Info,
        Warn,
        Error,
    }

    internal sealed class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }

        public string Format()
        {
            return Timestamp.ToString("HH:mm:ss.fff") + " [" + Level.ToString().ToUpperInvariant() + "] " + Source + ": " + Message;
        }
    }

    internal static class Logger
    {
        private const int RingBufferCapacity = 50;
        private const long FileRotationBytes = 1024L * 1024L; // 1 MB

        private static readonly object _gate = new object();
        private static readonly LinkedList<LogEntry> _ring = new LinkedList<LogEntry>();
        private static IVsActivityLog _activity;
        private static string _logFilePath;
        private static bool _fileEnabled;

        public static event EventHandler EntryAdded;

        public static IReadOnlyList<LogEntry> Snapshot()
        {
            lock (_gate)
            {
                return _ring.ToArray();
            }
        }

        public static async Task InitAsync(IAsyncServiceProvider sp, CancellationToken ct)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
                _activity = await sp.GetServiceAsync(typeof(SVsActivityLog)) as IVsActivityLog;
            }
            catch
            {
                _activity = null;
            }

            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClaudeCodeVS");
                Directory.CreateDirectory(dir);
                _logFilePath = Path.Combine(dir, "diagnostics.log");
                RotateIfNeeded();
                _fileEnabled = true;
            }
            catch
            {
                _fileEnabled = false;
            }

            Info("Logger", "Diagnostics logger initialized. File: " + (_logFilePath ?? "(disabled)"));
        }

        public static void Info(string source, string message) => Write(LogLevel.Info, source, message, null);
        public static void Warn(string source, string message) => Write(LogLevel.Warn, source, message, null);
        public static void Error(string source, string message) => Write(LogLevel.Error, source, message, null);
        public static void Error(string source, string message, Exception ex)
            => Write(LogLevel.Error, source, message + (ex != null ? " :: " + ex : ""), ex);

        private static void Write(LogLevel level, string source, string message, Exception ex)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Source = source ?? "",
                Message = message ?? "",
            };

            lock (_gate)
            {
                _ring.AddLast(entry);
                while (_ring.Count > RingBufferCapacity) _ring.RemoveFirst();
            }

            WriteToActivityLog(entry);
            WriteToFile(entry);

            try { EntryAdded?.Invoke(null, EventArgs.Empty); } catch { }
        }

        private static void WriteToActivityLog(LogEntry entry)
        {
            var log = _activity;
            if (log == null) return;
            try
            {
                uint type = entry.Level == LogLevel.Error
                    ? (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR
                    : entry.Level == LogLevel.Warn
                        ? (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_WARNING
                        : (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_INFORMATION;
                log.LogEntry(type, "ClaudeCodeVS." + entry.Source, entry.Message);
            }
            catch
            {
            }
        }

        private static void WriteToFile(LogEntry entry)
        {
            if (!_fileEnabled || string.IsNullOrEmpty(_logFilePath)) return;
            try
            {
                lock (_gate)
                {
                    File.AppendAllText(_logFilePath, entry.Format() + Environment.NewLine);
                    RotateIfNeeded();
                }
            }
            catch
            {
                _fileEnabled = false;
            }
        }

        private static void RotateIfNeeded()
        {
            try
            {
                if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath)) return;
                var info = new FileInfo(_logFilePath);
                if (info.Length < FileRotationBytes) return;
                string archived = _logFilePath + ".1";
                if (File.Exists(archived)) File.Delete(archived);
                File.Move(_logFilePath, archived);
            }
            catch
            {
            }
        }
    }
}
