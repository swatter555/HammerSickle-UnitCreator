using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Threading;

namespace HammerAndSickle.Services
{
    /// <summary>
    /// Lightweight, Avalonia‑friendly replacement for the Unity‑centric AppService.
    /// 
    /// Drop this class into the editor project and leave every existing call site
    /// untouched — the public static surface (HandleException) is identical to the
    /// original.  In the editor it writes exceptions to Debug/Trace, appends a simple
    /// rolling log file beside the executable, and (optionally) forwards the message
    /// to the UI thread so the host shell can show a toast/dialog.
    /// </summary>
    public static class AppService
    {
        private const string LogFileName = "editor.log";
        private static readonly object _sync = new();

        /// <summary>
        /// Optional hook; assign from App.axaml.cs after the main window is ready.
        /// </summary>
        public static Action<string /*class*/, string /*method*/, Exception>? UiBridge { get; set; }

        /// <summary>
        /// Mirrors the legacy signature used throughout the game code‑base.
        /// </summary>
        public static void HandleException(string cls, string method, Exception ex)
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string message = $"[{stamp}] {cls}.{method}: {ex}";

            // 1) Debug + Trace windows for immediate developer feedback.
            Debug.WriteLine(message);
            Trace.WriteLine(message);

            // 2) Append to disk so testers can attach logs to bug reports.
            TryWriteToFile(message);

            // 3) Bubble up to the UI (non‑blocking, marshalled to UI thread).
            if (UiBridge is not null)
                Dispatcher.UIThread.Post(() => UiBridge.Invoke(cls, method, ex));
        }

        /// <summary>
        /// Logs a UI-related message with a timestamp to multiple outputs for debugging and tracking purposes.
        /// </summary>
        /// <remarks>The method writes the message to the debug and trace outputs for immediate feedback
        /// during development. Additionally, it attempts to append the message to a log file on disk, enabling testers
        /// to include logs in bug reports.</remarks>
        /// <param name="msg">The message to log. Cannot be null or empty.</param>
        public static void CaptureUiMessage(string msg)
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"[{stamp}] UI: {msg}";
            // 1) Debug + Trace windows for immediate developer feedback.
            Debug.WriteLine(line);
            Trace.WriteLine(line);
            // 2) Append to disk so testers can attach logs to bug reports.
            TryWriteToFile(line);
        }

        private static void TryWriteToFile(string line)
        {
            try
            {
                lock (_sync)
                {
                    File.AppendAllText(LogFileName, line + Environment.NewLine);
                }
            }
            catch
            {
                // Never allow logging failures to crash the editor.
            }
        }
    }
}
