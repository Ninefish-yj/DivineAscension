// ============================================================

using UnityEngine;
using System.IO;
using System.Linq;

namespace Code.Core
{
    /// <summary>
    /// 登神长阶调试日志控制
    /// 全局开关，控制是否输出详细调试信息
    /// </summary>
    public static class DSDebug
    {
        /// <summary>是否启用详细调试日志（默认关闭，避免刷屏）</summary>
        public static bool EnableVerboseLog = false;

        /// <summary>是否启用基本日志（模组加载/错误等，默认开启）</summary>

        /// <summary>心跳日志输出间隔（帧），默认600帧约10秒</summary>
        public const int HeartbeatLogInterval = 600;

        /// <summary>文件日志路径</summary>
        private static string _logFilePath = null;

        /// <summary>文件日志是否已初始化</summary>
        private static bool _fileLogInitialized = false;

        /// <summary>初始化文件日志</summary>
        private static void InitFileLog()
        {
            if (_fileLogInitialized) return;

            try
            {
                // 获取模组目录
                string modDir = Path.Combine(Application.persistentDataPath, "DivineAscension");
                if (!Directory.Exists(modDir))
                {
                    Directory.CreateDirectory(modDir);
                }

                // 清理旧日志文件（只保留最近20个，删除超过7天的）
                CleanOldLogFiles(modDir);

                // 日志文件路径（按日期命名）
                string logFileName = $"debug_log_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
                _logFilePath = Path.Combine(modDir, logFileName);

                // 写入日志头
                File.WriteAllText(_logFilePath, 
                    $"=== DivineAscension Debug Log ===\n" +
                    $"时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"游戏版本: {Application.version}\n" +
                    $"==================================\n\n");

                _fileLogInitialized = true;
                Debug.Log($"[DivineAscension] 文件日志已初始化: {_logFilePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[DivineAscension] 文件日志初始化失败: {e.Message}");
                _fileLogInitialized = true; // 标记为已初始化，避免重复尝试
            }
        }

        /// <summary>清理旧日志文件（只保留最近20个，删除超过7天的）</summary>
        private static void CleanOldLogFiles(string modDir)
        {
            try
            {
                string[] logFiles = Directory.GetFiles(modDir, "debug_log_*.txt");
                if (logFiles.Length <= 20) return;

                // 按修改时间排序（最新的在前）
                var sortedFiles = logFiles
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(fi => fi.LastWriteTime)
                    .ToList();

                // 删除超过20个的旧文件
                for (int i = 20; i < sortedFiles.Count; i++)
                {
                    try
                    {
                        sortedFiles[i].Delete();
                    }
                    catch { }
                }

                // 同时删除超过7天的文件
                System.DateTime sevenDaysAgo = System.DateTime.Now.AddDays(-7);
                foreach (var fi in sortedFiles)
                {
                    if (fi.LastWriteTime < sevenDaysAgo)
                    {
                        try { fi.Delete(); } catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>写入文件日志</summary>
        private static void WriteToFile(string level, string message)
        {
            try
            {
                InitFileLog();

                if (string.IsNullOrEmpty(_logFilePath)) return;

                string timestamp = System.DateTime.Now.ToString("HH:mm:ss.fff");
                string logLine = $"[{timestamp}] [{level}] {message}\n";

                File.AppendAllText(_logFilePath, logLine);
            }
            catch (System.Exception)
            {
                // 文件日志失败时静默处理，不影响游戏
            }
        }

        /// <summary>输出详细调试日志（仅在DengShenConfig.EnableVerboseLog=true时输出）</summary>
        public static void Verbose(string message)
        {
            if (DengShenConfig.EnableVerboseLog)
            {
                Debug.Log("[DivineAscension] " + message);
                // WriteToFile("VERBOSE", message);
            }
        }

        /// <summary>输出基本日志（在EnableBasicLog=true时输出）</summary>

        /// <summary>输出警告日志（始终输出）</summary>
        public static void Warning(string message)
        {
            Debug.LogWarning("[DivineAscension] " + message);
            // WriteToFile("WARNING", message);
        }

        /// <summary>输出错误日志（始终输出）</summary>
        public static void Error(string message)
        {
            Debug.LogError("[DivineAscension] " + message);
            // WriteToFile("ERROR", message);
        }

        /// <summary>清空当前日志文件（保留文件与日志头，供后续继续写入）</summary>
        public static void ClearLogFile()
        {
            try
            {
                if (!_fileLogInitialized) InitFileLog();
                if (string.IsNullOrEmpty(_logFilePath) || !File.Exists(_logFilePath)) return;

                File.WriteAllText(_logFilePath,
                    $"=== DivineAscension Debug Log (已清空) ===\n" +
                    $"时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"==================================\n\n");
                Debug.Log("[DivineAscension] 调试日志已清空: " + _logFilePath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DivineAscension] 清空日志失败: " + e.Message);
            }
        }
    }
}
