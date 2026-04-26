using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ClaudeCodeVS.Protocol
{
    internal sealed class LockFileManager
    {
        private string _path;

        public string Path => _path;

        public static string LockDirectory
        {
            get
            {
                var home = Environment.GetEnvironmentVariable("CLAUDE_CODE_CONFIG_DIR");
                if (string.IsNullOrEmpty(home))
                {
                    home = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "ide");
                }
                return home;
            }
        }

        public async Task WriteAsync(int port, string authToken, string[] workspaceFolders, string ideName, CancellationToken ct)
        {
            Directory.CreateDirectory(LockDirectory);
            _path = System.IO.Path.Combine(LockDirectory, port + ".lock");

            var payload = new
            {
                pid = Process.GetCurrentProcess().Id,
                workspaceFolders = workspaceFolders ?? Array.Empty<string>(),
                ideName,
                transport = "ws",
                authToken,
                runningInWindows = true,
            };

            string json = JsonConvert.SerializeObject(payload, Formatting.Indented);
            string tmp = _path + ".tmp";

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var sw = new StreamWriter(fs))
            {
                await sw.WriteAsync(json).ConfigureAwait(false);
                await sw.FlushAsync().ConfigureAwait(false);
                fs.Flush(true);
            }

            if (File.Exists(_path)) File.Delete(_path);
            File.Move(tmp, _path);
        }

        public void UpdateWorkspaceFolders(string[] folders, string authToken, string ideName)
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) return;
            try
            {
                string text = File.ReadAllText(_path);
                dynamic obj = JsonConvert.DeserializeObject(text);
                obj.workspaceFolders = Newtonsoft.Json.Linq.JArray.FromObject(folders ?? Array.Empty<string>());
                File.WriteAllText(_path, JsonConvert.SerializeObject(obj, Formatting.Indented));
            }
            catch
            {
            }
        }

        public void Delete()
        {
            try
            {
                if (!string.IsNullOrEmpty(_path) && File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            catch
            {
            }
        }

        public static void SweepStale()
        {
            try
            {
                if (!Directory.Exists(LockDirectory)) return;
                foreach (var file in Directory.GetFiles(LockDirectory, "*.lock"))
                {
                    try
                    {
                        string text = File.ReadAllText(file);
                        dynamic obj = JsonConvert.DeserializeObject(text);
                        int pid = (int)obj.pid;
                        try
                        {
                            Process.GetProcessById(pid);
                        }
                        catch (ArgumentException)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
