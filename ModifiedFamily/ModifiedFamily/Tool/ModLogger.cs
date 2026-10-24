// Fault-tolerant dual-destination logger for the campaign feed and persistent log file.
using System;
using System.IO;
using System.Text;
using MCM.Abstractions.Base.Global;
using ModifiedFamily.Utils;
using TaleWorlds.Library;

namespace ModifiedFamily.Tool
{
    public enum LogLevel { Debug = 0, Info = 1, Notice = 2, Warn = 3, Error = 4, Disabled = 5 }

    public static class ModLogger
    {
        private static readonly object FileLock = new object();
        private static StreamWriter _writer;
        private static bool _fileUnavailable;
        private static readonly Color DebugColor = new Color(0.6f, 0.95f, 0.7f);
        private static readonly Color InfoColor = new Color(0.7f, 0.9f, 1f);
        private static readonly Color NoticeColor = new Color(1f, 1f, 0.6f);
        private static readonly Color WarnColor = new Color(1f, 0.8f, 0.2f);
        private static readonly Color ErrorColor = new Color(1f, 0.3f, 0.3f);

        private static LogLevel ScreenLevel => GlobalSettings<Settings>.Instance?.MinLogLevel?.SelectedValue ?? LogLevel.Notice;
        private static LogLevel FileLevel => GlobalSettings<Settings>.Instance?.FileMinLogLevel?.SelectedValue ?? LogLevel.Info;
        public static void Debug(string message) => Display(LogLevel.Debug, message, DebugColor);
        public static void Info(string message) => Display(LogLevel.Info, message, InfoColor);
        public static void Notice(string message) => Display(LogLevel.Notice, message, NoticeColor);
        public static void Warn(string message) => Display(LogLevel.Warn, message, WarnColor);
        public static void Error(string message) => Display(LogLevel.Error, message, ErrorColor);

        private static void Display(LogLevel level, string message, Color color)
        {
            // Each destination applies its own threshold to the same event.
            WriteToFile(level, message);
            LogLevel threshold = ScreenLevel;
            if (threshold == LogLevel.Disabled || level < threshold) return;
            try { InformationManager.DisplayMessage(new InformationMessage("[ModifiedFamily] " + message, color)); } catch { }
        }

        private static void WriteToFile(LogLevel level, string message)
        {
            LogLevel threshold = FileLevel;
            if (_fileUnavailable || threshold == LogLevel.Disabled || level < threshold) return;
            // Logging failures are isolated so they cannot break campaign simulation.
            try
            {
                lock (FileLock)
                {
                    if (_writer == null) _writer = CreateWriter();
                    _writer.WriteLine("[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}", DateTime.Now, level, message ?? string.Empty);
                }
            }
            catch { _fileUnavailable = true; }
        }

        private static StreamWriter CreateWriter()
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord", "Configs", "ModLogs");
            Directory.CreateDirectory(directory);
            return new StreamWriter(new FileStream(Path.Combine(directory, "ModifiedFamily.log"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false)) { AutoFlush = true };
        }
    }
}
