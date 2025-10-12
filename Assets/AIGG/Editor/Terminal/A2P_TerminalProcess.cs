// SPDX-License-Identifier: MIT
#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Aim2Pro.AIGG.Terminal
{
    internal sealed class A2P_TerminalProcess : IDisposable
    {
        public readonly ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();
        private Process _proc;
        private bool _running;
        private string _logPath;

        public bool IsRunning => _running && _proc != null && !_proc.HasExited;

        public void InitLog(string logPath)
        {
            _logPath = logPath;
            try { Directory.CreateDirectory(Path.GetDirectoryName(_logPath)); } catch { }
            SafeAppend("[AIGG Terminal] log init\n");
        }

        public static int GetBashMajorVersion(string shellPath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = string.IsNullOrWhiteSpace(shellPath) ? "/bin/bash" : shellPath,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    string errp = p.StandardError.ReadToEnd();
                    p.WaitForExit(2000);
                    var text = (outp + " " + errp).ToLowerInvariant();
                    // e.g. "GNU bash, version 3.2.57(1)-release ..."
                    int i = text.IndexOf("version ");
                    if (i >= 0)
                    {
                        i += "version ".Length;
                        var num = new StringBuilder();
                        while (i < text.Length && char.IsDigit(text[i])) { num.Append(text[i]); i++; }
                        if (int.TryParse(num.ToString(), out var major)) return major;
                    }
                }
            }
            catch { }
            return -1;
        }

        public void Start(string shellPath, string command, string workingDir)
        {
            Stop(); // clean slate

            var psi = new ProcessStartInfo
            {
#if UNITY_EDITOR_WIN
                FileName = "cmd.exe",
                Arguments = "/c " + command,
#else
                FileName = string.IsNullOrWhiteSpace(shellPath) ? "/bin/bash" : shellPath,
                Arguments = "-lc \"" + EscapeForBash(command ?? string.Empty) + "\"",
#endif
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? Environment.CurrentDirectory : workingDir
            };

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (_, e) => { if (e.Data != null) Enqueue(e.Data); };
            _proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) Enqueue(e.Data); };
            _proc.Exited += (_, __) => { _running = false; Enqueue("[AIGG Terminal] process exited."); };

            _running = _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            Enqueue("[AIGG Terminal] started.");
        }

        public void Stop()
        {
            if (_proc == null) return;
            try
            {
                if (!_proc.HasExited)
                {
                    try { _proc.Kill(); } catch { /* ignore */ }
                }
                try { _proc.CancelOutputRead(); } catch { }
                try { _proc.CancelErrorRead(); } catch { }
            }
            catch { /* ignore */ }
            finally
            {
                try { _proc.Dispose(); } catch { }
                _proc = null;
                _running = false;
                Enqueue("[AIGG Terminal] stopped.");
            }
        }

        public void Dispose() { Stop(); }

        private void Enqueue(string line)
        {
            Lines.Enqueue(line);
            SafeAppend(line + "\n");
        }

        private void SafeAppend(string text)
        {
            if (string.IsNullOrEmpty(_logPath)) return;
            try { File.AppendAllText(_logPath, text); } catch { }
        }

        private static string EscapeForBash(string cmd)
        {
            // minimal escaping for embedding within double quotes
            return cmd.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
