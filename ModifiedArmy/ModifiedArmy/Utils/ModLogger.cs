using TaleWorlds.Library;

namespace ModifiedArmy.Tool
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Notice = 2,
        Warn = 3,
        Error = 4
    }

    /// <summary>
    /// 通用 Mod 日志工具类，适用于所有子系统。
    /// 可通过 EnableLogging 全局开关控制输出。
    /// </summary>
    public static class ModLogger
    {
        /// <summary>
        /// 最低显示级别。设为 Info，则 Info/Notice/Warn/Error 会显示，Debug 不会。
        /// 默认为 Notice。
        /// </summary>
        private static LogLevel CurrentMinLogLevel
        {
            get
            {
                //var settings = Main.ModSettings;
                //if (settings == null) 
                //    return LogLevel.Info;

                //return settings.MinLogLevel.SelectedValue;

                return LogLevel.Notice;
            }
        }

        // 预定义日志颜色（便于维护）
        private static readonly Color DebugColor = new Color(0.6f, 0.95f, 0.7f);   // 淡青绿
        private static readonly Color InfoColor = new Color(0.7f, 0.9f, 1.0f);     // 浅蓝（比纯白更友好）
        private static readonly Color NoticeColor = new Color(1.0f, 1.0f, 0.6f);   // 淡黄（用于提示性信息）
        private static readonly Color WarnColor = new Color(1.0f, 0.8f, 0.2f);     // 橙黄
        private static readonly Color ErrorColor = new Color(1.0f, 0.3f, 0.3f);    // 红色

        /// <summary>
        /// 输出调试信息。
        /// </summary>
        public static void Debug(string message, Color? color = null)
        {
            if ((int)LogLevel.Debug >= (int)CurrentMinLogLevel)
                InformationManager.DisplayMessage(new InformationMessage(message, color ?? DebugColor));
        }

        /// <summary>
        /// 输出普通信息日志。
        /// </summary>
        public static void Info(string message, Color? color = null)
        {
            if ((int)LogLevel.Info >= (int)CurrentMinLogLevel)
                InformationManager.DisplayMessage(new InformationMessage(message, color ?? InfoColor));
        }

        /// <summary>
        /// 输出提示性信息（比 Info 更重要，但非警告）。
        /// </summary>
        public static void Notice(string message, Color? color = null)
        {
            if ((int)LogLevel.Notice >= (int)CurrentMinLogLevel)
                InformationManager.DisplayMessage(new InformationMessage(message, color ?? NoticeColor));
        }

        /// <summary>
        /// 输出警告信息。
        /// </summary>
        public static void Warn(string message, Color? color = null)
        {
            if ((int)LogLevel.Warn >= (int)CurrentMinLogLevel)
                InformationManager.DisplayMessage(new InformationMessage(message, color ?? WarnColor));
        }

        /// <summary>
        /// 输出错误信息（建议始终显示）。
        /// </summary>
        public static void Error(string message, Color? color = null)
        {
            if ((int)LogLevel.Error >= (int)CurrentMinLogLevel)
                InformationManager.DisplayMessage(new InformationMessage(message, color ?? ErrorColor));
        }
    }
}