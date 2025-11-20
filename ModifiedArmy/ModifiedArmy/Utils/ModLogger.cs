using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.Library;

namespace ModifiedArmy.Tool
{
    /// <summary>
    /// 通用 Mod 日志工具类，适用于所有子系统。
    /// 可通过 EnableLogging 全局开关控制输出。
    /// </summary>
    /// 

    public static class ModLogger
    {
        /// <summary>
        /// 是否启用日志输出。设为 false 可关闭所有 Info/Debug/Warn 日志。
        /// </summary>
        public static bool EnableLogging = true;

        /// <summary>
        /// 输出普通信息日志。
        /// </summary>
        public static void Info(string message, Color? color = null)
        {
            if (!EnableLogging) return;
            InformationManager.DisplayMessage(new InformationMessage(message, color ?? Color.White));
        }

        /// <summary>
        /// 输出调试信息。
        /// </summary>
        public static void Debug(string message, Color? color = null)
        {
            if (!EnableLogging) return;
            InformationManager.DisplayMessage(new InformationMessage(message,
                color ?? new Color(0.8f, 0.9f, 0.4f)));
        }

        /// <summary>
        /// 输出警告信息。
        /// </summary>
        public static void Warn(string message, Color? color = null)
        {
            if (!EnableLogging) return;
            InformationManager.DisplayMessage(new InformationMessage(message,
                color ?? new Color(1f, 0.7f, 0.2f)));
        }

        /// <summary>
        /// 输出错误信息（建议始终显示）。
        /// </summary>
        public static void Error(string message, Color? color = null)
        {
            // 错误日志通常应始终可见，便于排查问题
            InformationManager.DisplayMessage(new InformationMessage(message,
                color ?? new Color(1f, 0.3f, 0.3f)));
        }
    }
}
