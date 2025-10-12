// SPDX-License-Identifier: MIT
#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using UnityEngine;

namespace Aim2Pro.AIGG.Terminal
{
    internal sealed class A2P_TerminalProcess : IDisposable
    {
        public readonly ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();
        private Process _proc;
        private bool _running;

        public bool IsRunning => _running && _proc != null && !_proc.HasExited;

        public void Start(string shellPath, string command, string workingDir = null)
        {
            Stop(); // ensure clean

            var psi = new ProcessStartInfo
            {
                FileName = string.IsNullOrWhiteSpace(shellPath) ? "/bin/bash" : shellPath,
#if UNITY_EDITOR_WIN
                Arguments = "/c " + command, // fallback for Windows editor
                FileName = "cmd.exe",
#else
                Arguments = "-lc \"" + EscapeForBash(command ?? string.Empty) + "\"",
#endif
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? Environment.CurrentDirectory : workingDir
            };

            _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc.OutputDataReceived += (_, e) => { if (e.Data != null) Lines.Enqueue(e.Data); };
            _proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) Lines.Enqueue(e.Data); };
            _proc.Exited += (_, __) => { _running = false; Lines.Enqueue("[AIGG Terminal] process exited."); };

            _running = _proc.Start();
            _proc.BeginOutputReadLine();
            _proc.BeginErrorReadLine();
            Lines.Enqueue("[AIGG Terminal] started.");
        }

        public void Stop()
        {
            if (_proc == null) return;
            try
            {
                if (!_proc.HasExited)
                {
                    try { _proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
                }
                _proc.CancelOutputRead(); _proc.CancelErrorRead();
            }
            catch { /* ignore */ }
            finally
            {
                try { _proc.Dispose(); } catch { }
                _proc = null;
                _running = false;
                Lines.Enqueue("[AIGG Terminal] stopped.");
            }
        }

        public void Dispose() { Stop(); }

        private static string EscapeForBash(string cmd)
        {
            // Minimal safe escape for embedding in "..."
            return cmd.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
