using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LockLens {
    internal static class L10n {
        internal static bool English;
        internal static string SettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LockLens", "language.txt");
        static readonly Dictionary<string, string> Translations = new Dictionary<string, string> {
            { "占用查找器", "LockLens" },
            { "查找正在使用文件或文件夹的 Windows 程序", "Find Windows programs using a file or folder" },
            { "LockLens 占用查找器", "LockLens" },
            { "操作未完成", "Operation not completed" },
            { "系统报告占用", "Windows reports usage" },
            { "发现打开句柄", "Open handle found" },
            { "路径不存在或当前账户没有访问权限。请检查路径。", "The path does not exist or access is denied. Check the path." },
            { "正在读取 Windows 文件句柄…", "Reading Windows file handles…" },
            { "文件句柄扫描未完成：", "File handle scan incomplete: " },
            { "正在检查进程 ", "Checking process " },
            { " · 已找到 ", " · Found " },
            { " 个程序", " program(s)" },
            { "正在向 Windows 核实占用情况…", "Verifying usage with Windows…" },
            { "未知程序", "Unknown program" },
            { "Windows 占用核实未完成：", "Windows usage verification incomplete: " },
            { "无法创建用于识别文件句柄的引用。", "Could not open the reference file handle." },
            { "系统句柄接口返回 0x", "System handle API returned 0x" },
            { "系统句柄数量过多。", "Too many system handles." },
            { "系统句柄数据长度异常。", "Invalid system handle data length." },
            { "无法识别此系统的文件句柄类型。", "Could not identify the file handle type on this system." },
            { "无法建立 Windows 占用核实会话（错误 ", "Could not start Windows usage verification (error " },
            { "文件或文件夹  ·  支持拖入或粘贴路径", "File or folder · Drag an item here or paste its path" },
            { "目标路径", "Target path" },
            { "选文件", "File" },
            { "选文件夹", "Folder" },
            { "查找", "Search" },
            { "选择需要检查的文件", "Select a file to check" },
            { "选择需要检查的文件夹", "Select a folder to check" },
            { "停止中", "Stopping" },
            { "正在停止…", "Stopping…" },
            { "占用程序", "Programs using the target" },
            { "程序", "Program" },
            { "检测结果", "Result" },
            { "相关路径", "Related paths" },
            { "选中结果可查看程序位置和相关文件。", "Select a result to see the program location and related files." },
            { "程序详情", "Process details" },
            { "程序位置", "Location" },
            { "结束进程…", "End process…" },
            { "复制报告", "Copy report" },
            { "更多 ▾", "More ▾" },
            { "查看完整报告…", "View full report…" },
            { "打开任务管理器", "Open Task Manager" },
            { "已是管理员", "Running as administrator" },
            { "以管理员身份重试", "Restart as administrator" },
            { "就绪", "Ready" },
            { "检查报告", "Scan report" },
            { "停止", "Stop" },
            { "先选择一个文件或文件夹，也可以直接拖入窗口。", "Select a file or folder first, or drag it into this window." },
            { "选择目标", "Select a target" },
            { "当前版本用于本机磁盘。网络共享的占用需要在共享所在电脑上检查。", "This version checks local disks. Check network shares on the computer hosting them." },
            { "网络路径", "Network path" },
            { "路径无效", "Invalid path" },
            { "路径不存在或当前账户没有访问权限。", "The path does not exist or access is denied." },
            { "无法访问路径", "Cannot access path" },
            { "正在查找…", "Searching…" },
            { "Windows 报告涉及目标中的文件（未提供具体路径）", "Windows reports a file in the target (exact path unavailable)" },
            { "占用程序  ·  ", "Programs · " },
            { " 个结果", " result(s)" },
            { "查找完成", "Search complete" },
            { "未发现占用", "No usage found" },
            { " · 部分项目未能检查，详见“更多 → 查看完整报告”", " · Some items could not be checked. See More → View full report." },
            { " · 先关闭相关程序，再尝试删除", " · Close the related programs before retrying deletion." },
            { "本次未发现占用。也可能是权限、系统保护或短暂占用导致，具体信息可查看完整报告。", "No usage found. Permissions, system protection, or temporary usage may still block the operation. See the full report." },
            { "已停止查找 · 可以重新开始", "Search stopped · You can start again" },
            { "查找已停止", "Search stopped" },
            { "本次检查已取消，未生成完整结果。", "Scan canceled. No complete report was generated." },
            { "查找未完成", "Search incomplete" },
            { "\r\n可尝试以管理员身份重新运行。", "\r\nTry restarting as administrator." },
            { "未发现占用不等于一定可以删除。权限、受保护文件、驱动、短暂占用或其他电脑的网络连接也可能导致失败。", "No results do not guarantee deletion is possible. Permissions, protected files, drivers, temporary usage, or network clients may still block it." },
            { "系统报告占用：Windows Restart Manager 认为此进程正在使用目标中的文件。", "Windows reports usage: Windows Restart Manager reports this process is using a file in the target." },
            { "发现打开句柄：此进程打开了目标或其内部路径；该句柄可能允许删除。", "Open handle found: this process opened the target or a path inside it; the handle may allow deletion." },
            { "无法访问的进程：", "Processes with access denied: " },
            { "；超时/退出等未完整检查：", "; incomplete checks (timeout/exit): " },
            { "可从“更多”菜单以管理员身份重试。", "Use More → Restart as administrator to try again." },
            { "文件遍历受限：最多核实 20,000 个文件 / 10 秒；跳过无权限目录和内部链接目录。句柄扫描仍覆盖可读取的进程。", "Folder traversal limited to 20,000 files or 10 seconds; inaccessible directories and internal directory links are skipped. Handle scanning still checks accessible processes." },
            { "程序：", "Program: " },
            { "（无法读取程序位置）", " (program location unavailable)" },
            { "\r\n相关路径：", "\r\nRelated paths: " },
            { "Windows 未提供具体路径", "Windows did not provide the exact path" },
            { "\r\n提示：打开句柄不一定阻止删除。", "\r\nNote: an open handle does not always prevent deletion." },
            { "占用查找器检查报告\r\n", "LockLens scan report\r\n" },
            { "时间：", "Time: " },
            { "目标：", "Target: " },
            { "无法以管理员身份启动", "Could not restart as administrator" },
            { "该进程已退出或发生变化，请重新查找。", "This process has exited or changed. Search again." },
            { "请刷新结果", "Refresh the results" },
            { "这是 Windows 关键进程，不能在本工具中结束。", "This is a critical Windows process and cannot be ended by this tool." },
            { "受保护的进程", "Protected process" },
            { "准备关闭 ", "Close " },
            { "）。\r\n请先保存该程序中尚未保存的内容。\r\n\r\n继续发送正常关闭请求？", ")?\r\nSave any unsaved work in this program first.\r\n\r\nSend a normal close request?" },
            { "关闭选中程序", "Close selected program" },
            { "程序尚未退出，可能正在提示保存文件。", "The program has not exited. It may be asking you to save files." },
            { "该进程没有可正常关闭的主窗口。", "This process has no main window that can be closed normally." },
            { "\r\n\r\n是否强制结束 ", "\r\n\r\nForce-end " },
            { "）？\r\n未保存的内容可能丢失，相关服务也可能中断。", ")?\r\nUnsaved work may be lost and related services may be interrupted." },
            { "确认强制结束", "Confirm force end" },
            { "已发送结束请求 · 请重新查找后再尝试删除", "Close request sent · Search again before retrying deletion" },
            { "进程已经退出，请重新查找", "Process already exited. Search again." },
            { "\r\n可尝试管理员重试，或手动关闭该程序。", "\r\nTry restarting as administrator or close the program manually." },
            { "未能结束进程", "Could not end process" },
        };
        internal static string T(string chinese) {
            string translated;
            return English && Translations.TryGetValue(chinese, out translated) ? translated : chinese;
        }
        internal static string Source(string text) {
            foreach (var pair in Translations) if (pair.Value == text) return pair.Key;
            return text;
        }
        internal static void Load() {
            English = !CultureInfo.InstalledUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            try { if (File.Exists(SettingsPath)) { string code = File.ReadAllText(SettingsPath).Trim(); if (code == "en" || code == "zh-CN") English = code == "en"; } }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        internal static void Save() {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, English ? "en" : "zh-CN", new UTF8Encoding(false));
        }
        internal static bool HasTranslation(string text) { return Translations.ContainsKey(text); }
        internal static string TranslateMessage(string message) {
            foreach (var pair in Translations.OrderByDescending(p => p.Key.Length)) {
                string from = English ? pair.Key : pair.Value, to = English ? pair.Value : pair.Key;
                if (!String.IsNullOrEmpty(from) && message.StartsWith(from, StringComparison.Ordinal)) return to + message.Substring(from.Length);
            }
            return message;
        }
        internal static IEnumerable<string> Values { get { return Translations.Values; } }
    }
}
