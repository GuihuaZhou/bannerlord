using System;
using System.IO;
using System.Text;
using MCM.Abstractions.Base.Global;
using ModifiedPolitics.Utils;
using TaleWorlds.Library;

namespace ModifiedPolitics.Tool
{
    /// <summary>
    /// Severity levels used by the ModifiedPolitics message logger.
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Notice = 2,
        Warn = 3,
        Error = 4,
        Disabled = 5
    }

    /// <summary>
    /// Displays messages emitted by ModifiedPolitics without sharing
    /// ModifiedArmy's logging threshold.
    /// </summary>
    public static class ModLogger
    {
        private static readonly object FileLock = new object();
        private static StreamWriter _fileWriter;
        private static bool _fileLoggerUnavailable;

        private static readonly Color DebugColor =
            new Color(0.6f, 0.95f, 0.7f);
        private static readonly Color InfoColor =
            new Color(0.7f, 0.9f, 1.0f);
        private static readonly Color NoticeColor =
            new Color(1.0f, 1.0f, 0.6f);
        private static readonly Color WarnColor =
            new Color(1.0f, 0.8f, 0.2f);
        private static readonly Color ErrorColor =
            new Color(1.0f, 0.3f, 0.3f);

        private static LogLevel CurrentMinLogLevel
        {
            get
            {
                Settings settings = GlobalSettings<Settings>.Instance;
                return settings?.MinLogLevel.SelectedValue ?? LogLevel.Notice;
            }
        }

        private static LogLevel CurrentFileMinLogLevel
        {
            get
            {
                Settings settings = GlobalSettings<Settings>.Instance;
                return settings?.FileMinLogLevel?.SelectedValue
                    ?? LogLevel.Info;
            }
        }

        public static void Debug(string message, Color? color = null)
        {
            Display(LogLevel.Debug, message, color ?? DebugColor);
        }

        public static void Info(string message, Color? color = null)
        {
            Display(LogLevel.Info, message, color ?? InfoColor);
        }

        public static void Notice(string message, Color? color = null)
        {
            Display(LogLevel.Notice, message, color ?? NoticeColor);
        }

        public static void Warn(string message, Color? color = null)
        {
            Display(LogLevel.Warn, message, color ?? WarnColor);
        }

        public static void Error(string message, Color? color = null)
        {
            Display(LogLevel.Error, message, color ?? ErrorColor);
        }

        private static void Display(
            LogLevel level,
            string message,
            Color color)
        {
            WriteToFile(level, message);

            LogLevel displayThreshold = CurrentMinLogLevel;
            if (displayThreshold == LogLevel.Disabled
                || (int)level < (int)displayThreshold)
            {
                return;
            }

            InformationManager.DisplayMessage(
                new InformationMessage(message, color));
        }

        /// <summary>
        /// Writes through an auto-flushed UTF-8 stream that permits concurrent
        /// readers. This keeps diagnostics readable while the game is running.
        /// Any file failure is isolated from campaign execution.
        /// </summary>
        private static void WriteToFile(LogLevel level, string message)
        {
            LogLevel threshold = CurrentFileMinLogLevel;
            if (_fileLoggerUnavailable
                || threshold == LogLevel.Disabled
                || (int)level < (int)threshold)
            {
                return;
            }

            try
            {
                lock (FileLock)
                {
                    if (_fileWriter == null)
                    {
                        _fileWriter = CreateFileWriter(
                            "ModifiedPolitics.log");
                    }

                    string line = string.Format(
                        "[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}",
                        DateTime.Now,
                        level,
                        message ?? string.Empty);
                    _fileWriter.WriteLine(line);
                }
            }
            catch
            {
                _fileLoggerUnavailable = true;
            }
        }

        private static StreamWriter CreateFileWriter(string fileName)
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "Mount and Blade II Bannerlord",
                "Configs",
                "ModLogs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, fileName);
            FileStream stream = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);
            return new StreamWriter(
                stream,
                new UTF8Encoding(false))
            {
                AutoFlush = true
            };
        }
    }
}
