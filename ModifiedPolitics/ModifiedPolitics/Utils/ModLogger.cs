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
            if ((int)level < (int)CurrentMinLogLevel)
            {
                return;
            }

            InformationManager.DisplayMessage(
                new InformationMessage(message, color));
        }
    }
}
