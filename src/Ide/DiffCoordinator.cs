using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace ClaudeCodeVS.Ide
{
    internal sealed class DiffCoordinator
    {
        private readonly IdeServices _ide;
        private readonly ConcurrentDictionary<string, PendingDiff> _pending = new ConcurrentDictionary<string, PendingDiff>();

        public DiffCoordinator(IdeServices ide)
        {
            _ide = ide;
        }

        public async Task<DiffResult> OpenAsync(string oldPath, string newPath, string newContents, string tabName, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<DiffResult>();
            string tmp = Path.Combine(Path.GetTempPath(), "claudecode-" + Guid.NewGuid().ToString("N") + Path.GetExtension(oldPath ?? newPath ?? ".tmp"));
            File.WriteAllText(tmp, newContents ?? "");

            string leftLabel = oldPath != null ? Path.GetFileName(oldPath) + " (current)" : "(empty)";
            string rightLabel = tabName ?? "Claude proposed edit";

            string key = tabName ?? Guid.NewGuid().ToString("N");
            var pending = new PendingDiff { Tcs = tcs, TempFile = tmp, OldPath = oldPath, NewContents = newContents, NewPath = newPath };
            _pending[key] = pending;

            await _ide.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
            try
            {
                uint grfDiff = (uint)(__VSDIFFSERVICEOPTIONS.VSDIFFOPT_LeftFileIsTemporary
                    | __VSDIFFSERVICEOPTIONS.VSDIFFOPT_RightFileIsTemporary);
                string caption = rightLabel + " ↔ " + leftLabel;
                string leftFile = oldPath;
                if (string.IsNullOrEmpty(leftFile) || !File.Exists(leftFile))
                {
                    leftFile = Path.Combine(Path.GetTempPath(), "claudecode-empty-" + Guid.NewGuid().ToString("N") + ".txt");
                    File.WriteAllText(leftFile, "");
                    pending.SyntheticLeftFile = leftFile;
                }
                _ide.DiffService.OpenComparisonWindow2(
                    leftFile,
                    tmp,
                    caption,
                    caption,
                    leftLabel,
                    rightLabel,
                    null,
                    null,
                    grfDiff);
            }
            catch (Exception ex)
            {
                CleanupTemp(pending);
                _pending.TryRemove(key, out _);
                tcs.TrySetException(ex);
            }

            pending.Key = key;
            return await tcs.Task.ConfigureAwait(false);
        }

        public void AcceptTopmost()
        {
            if (_pending.IsEmpty) return;
            foreach (var kv in _pending)
            {
                Accept(kv.Key);
                return;
            }
        }

        public void RejectTopmost()
        {
            if (_pending.IsEmpty) return;
            foreach (var kv in _pending)
            {
                Reject(kv.Key);
                return;
            }
        }

        public void Accept(string key)
        {
            if (!_pending.TryRemove(key, out var pending)) return;
            try
            {
                string target = pending.NewPath ?? pending.OldPath;
                if (!string.IsNullOrEmpty(target))
                {
                    var dir = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(target, pending.NewContents ?? "");
                }
                pending.Tcs.TrySetResult(new DiffResult { Status = "FILE_SAVED", SavedPath = target, Contents = pending.NewContents });
            }
            catch (Exception ex)
            {
                pending.Tcs.TrySetException(ex);
            }
            finally
            {
                CleanupTemp(pending);
            }
        }

        public void Reject(string key)
        {
            if (!_pending.TryRemove(key, out var pending)) return;
            pending.Tcs.TrySetResult(new DiffResult { Status = "DIFF_REJECTED" });
            CleanupTemp(pending);
        }

        private static void CleanupTemp(PendingDiff p)
        {
            try { if (File.Exists(p.TempFile)) File.Delete(p.TempFile); } catch { }
            try { if (!string.IsNullOrEmpty(p.SyntheticLeftFile) && File.Exists(p.SyntheticLeftFile)) File.Delete(p.SyntheticLeftFile); } catch { }
        }

        private sealed class PendingDiff
        {
            public string Key;
            public TaskCompletionSource<DiffResult> Tcs;
            public string TempFile;
            public string SyntheticLeftFile;
            public string OldPath;
            public string NewPath;
            public string NewContents;
        }
    }

    internal sealed class DiffResult
    {
        public string Status { get; set; }
        public string SavedPath { get; set; }
        public string Contents { get; set; }
    }
}
