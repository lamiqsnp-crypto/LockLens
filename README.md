# LockLens

[English](#english) · [简体中文](#简体中文)

## English

LockLens is a portable Windows tool that finds which programs are using a file or folder. View related processes, open program locations, copy reports, or close a process with confirmation. No installation required. All checks run locally.

### Download and run

Download **[LockLens.zip](https://github.com/lamiqsnp-crypto/LockLens/releases/latest/download/LockLens.zip)** from the [latest release](https://github.com/lamiqsnp-crypto/LockLens/releases/latest), extract it, and run **LockLens.exe**.

**Version:** 1.0 · **Requirements:** Windows 10/11 x64, .NET Framework 4.5+ (included with Windows 10/11).

### Use

1. Drop a file/folder into the window, paste its full path, or choose **File / Folder**.
2. Click **Search**. Select a result to see the executable location and related paths.
3. Save your work and close the relevant application before retrying your file operation.

**More** contains the full report, Task Manager, and administrator restart. **End process** first requests normal closure; forcing a process to stop requires another confirmation and may lose unsaved work.

### Language

Choose **More → Language / 语言 → English / 简体中文**. Controls, reports, and application prompts update immediately. The preference is saved in **%LOCALAPPDATA%\LockLens\language.txt** for the next launch. On first launch, Chinese Windows uses Simplified Chinese; other systems use English.

The language menu is unavailable during scanning or process closure. Windows-provided dialogs, button captions, and OS error messages follow the system display language.

### Results and limits

- **Windows reports usage:** Windows Restart Manager reports that the process uses a file in the target.
- **Open handle found:** the process opened the target or a path inside it. This handle may still allow deletion.

Directory handles and files inside the selected folder are checked. Folder traversal for Windows verification is limited to 20,000 files or 10 seconds; inaccessible directories and internal directory links are skipped. Handle scanning independently checks accessible processes, using isolated workers with timeouts.

Protected processes may remain inaccessible even as administrator. Check network shares on the hosting computer. No results do not guarantee deletion is possible: permissions, drivers, or temporary usage may still block it. The full report lists incomplete checks.

### Build and verify

Open PowerShell in the source directory:

    powershell -File .\build.ps1

This builds **LockLens.exe**. Build and run file-lock, folder-handle, Unicode-path, cancellation, process identity, taskbar icon, language switching, and preference persistence checks:

    powershell -File .\build.ps1 -Test

No external NuGet packages are needed. LockLens.ico is included; optionally regenerate it with make_icon.py using Python and Pillow.

### Privacy

LockLens does not upload files, delete the target, uninstall software, or install a background service. Its only saved preference is the language. The executable is unsigned.

## 简体中文

LockLens 是免安装的 Windows 文件占用查找工具。文件或文件夹无法删除、重命名或移动时，可查看相关程序、进程 ID、程序位置及路径。

### 下载与使用

下载 **[LockLens.zip](https://github.com/lamiqsnp-crypto/LockLens/releases/latest/download/LockLens.zip)**，解压后运行 **LockLens.exe**。版本统一为 **1.0**，支持 Windows 10/11 64 位。

1. 拖入文件或文件夹，也可粘贴路径或点击“选文件 / 选文件夹”。
2. 点击“查找”，选中结果查看详情。
3. 先保存工作并正常关闭相关程序，再尝试操作目标。

“更多”提供完整报告、任务管理器、管理员重试和语言切换。结束进程先确认正常关闭，强制结束需再次确认，可能丢失未保存内容。

### 切换语言

**更多 → Language / 语言 → 简体中文 / English**，立即生效并保存到 **%LOCALAPPDATA%\LockLens\language.txt**。首次启动：中文 Windows 默认简体中文，其他系统默认英文。

扫描和关闭进程期间暂不可切换。Windows 系统提供的对话框、按钮及错误消息仍跟随系统显示语言。

### 检测限制

“系统报告占用”表示 Windows 报告程序使用目标内的文件；“发现打开句柄”不一定阻止删除。文件夹系统核实最多遍历 20,000 个文件或 10 秒，跳过无权限目录和内部链接；句柄扫描独立检查可访问进程。

受保护进程可能无法读取，网络共享应在共享所在电脑检查。未发现结果不代表一定可以删除；完整报告列出检查限制。所有检查在本机完成，不上传文件、不删除目标、不安装后台服务，仅保存语言偏好。

构建方法见上方英文部分。
