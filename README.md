# LockLens
LockLens is a portable Windows tool that finds which programs are using a file or folder. View related processes, open program locations, copy reports, or close a process with confirmation. No installation required. All checks run locally.

## Download

Get the portable Windows x64 package from [Releases](https://github.com/lamiqsnp-crypto/Program-Resource-Monitor/releases/latest).

Extract the ZIP, then double-click `占用查找器.exe`. The current interface is in Simplified Chinese.

**Requirements:** 64-bit Windows 10 or Windows 11 with the built-in .NET Framework (4.5 or later).

## Use

1. Drag in a file or folder, paste its full path, or use **选文件 / 选文件夹**.
2. Click **查找** to find programs using the target. Click **停止** to cancel a scan.
3. Select a result to see the process ID, executable location, and related paths.
4. Save your work and close the relevant application before retrying your file operation.

**更多** contains the full report, Task Manager, and an option to restart as administrator. Ending a process requires confirmation; forcing it to stop requires a second confirmation.

## What the results mean

- **系统报告占用:** Windows Restart Manager reports that the process is using a file in the target.
- **发现打开句柄:** The process has an open handle to the target or a path inside it. An open handle may still allow deletion.

Folder scans check directory handles and files inside the folder. Some protected processes cannot be inspected, even as administrator. Network shares should be checked on the computer hosting them. The complete report lists access restrictions, timeouts, and traversal limits. A scan with no results does not guarantee that a file can be deleted.

## Build from source

No external NuGet packages are required. Open PowerShell in the source folder and run:

```powershell
powershell -File .\build.ps1
```

This creates `LockLens.exe`. To compile and run the real file-lock regression tests:

```powershell
powershell -File .\build.ps1 -Test
```

The icon is already included. Regenerating it with `make_icon.py` is optional and requires Python with Pillow.

## Privacy and behavior

Scans run locally. The tool does not upload files, delete the target, install a background service, or uninstall applications. Force-ending a process can lose unsaved work or interrupt a service.

The executable is currently unsigned.

## Version 1.0

- Compact interface with advanced actions in the More menu.
- File and folder handle inspection alongside Windows usage reports.
- Fixed the taskbar icon association for the portable application.
