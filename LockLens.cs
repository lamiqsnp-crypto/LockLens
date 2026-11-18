using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

[assembly: System.Reflection.AssemblyTitle("占用查找器")]
[assembly: System.Reflection.AssemblyDescription("查找正在使用文件或文件夹的 Windows 程序")]
[assembly: System.Reflection.AssemblyProduct("LockLens 占用查找器")]
[assembly: System.Reflection.AssemblyVersion("1.2.0.0")]

namespace LockLens {
    internal static class Program {
        internal static string Exe { get { return System.Reflection.Assembly.GetExecutingAssembly().Location; } }
        internal static string Encode(string s) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(s)); }
        internal static string Decode(string s) { return Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        internal static string QuotePath(string s) { return "\"" + s + new string('\\', s.Length - s.TrimEnd('\\').Length) + "\""; }
        [STAThread] static int Main(string[] args) {
            if (args.Length > 0 && args[0] == "--inspect") return Native.Inspect(args);
            if (args.Length > 0 && args[0] == "--hold") return SelfTest.Hold(args);
            if (args.Length > 0 && args[0] == "--self-test") return SelfTest.Run(args);
            if (args.Length > 0 && args[0] == "--ui-check") return SelfTest.CheckWindow(args);
            AppIdentity.Configure();
            try { Native.SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { MessageBox.Show(e.Exception.Message, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            Application.Run(new MainForm(args.Length > 0 ? args[0] : "")); return 0;
        }
    }

    internal static class AppIdentity {
        internal const string Id = "LockLens.Desktop.Portable";
        static readonly Guid PropertyFormat = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey {
            internal Guid Format; internal uint Pid;
            internal PropertyKey(uint pid) { Format = PropertyFormat; Pid = pid; }
        }
        [StructLayout(LayoutKind.Explicit, Size = 24)] struct PropVariant {
            [FieldOffset(0)] internal ushort Type;
            [FieldOffset(8)] internal IntPtr Pointer;
        }
        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index, out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
            [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
            [PreserveSig] int Commit();
        }
        [DllImport("shell32.dll")] static extern int SHGetPropertyStoreForWindow(IntPtr window, ref Guid iid, out IPropertyStore store);
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant value);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SetCurrentProcessExplicitAppUserModelID(string id);
        [DllImport("shell32.dll")] static extern int GetCurrentProcessExplicitAppUserModelID(out IntPtr id);
        internal static void Configure() { Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(Id)); }
        internal static string Current() {
            IntPtr value; Marshal.ThrowExceptionForHR(GetCurrentProcessExplicitAppUserModelID(out value));
            try { return Marshal.PtrToStringUni(value); } finally { Marshal.FreeCoTaskMem(value); }
        }
        internal static Icon LoadIcon(int size) {
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("LockLens.AppIcon"))
            using (var source = new Icon(stream, new Size(size, size))) return (Icon)source.Clone();
        }
        static IPropertyStore WindowProperties(IntPtr window) {
            Guid iid = typeof(IPropertyStore).GUID; IPropertyStore store;
            Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(window, ref iid, out store)); return store;
        }
        static void SetString(IPropertyStore store, uint pid, string text) {
            var key = new PropertyKey(pid); var value = new PropVariant { Type = 31, Pointer = Marshal.StringToCoTaskMemUni(text) };
            try { Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref value)); }
            finally { PropVariantClear(ref value); }
        }
        internal static string WindowString(IntPtr window, uint pid) {
            var store = WindowProperties(window);
            try {
                var key = new PropertyKey(pid); PropVariant value;
                Marshal.ThrowExceptionForHR(store.GetValue(ref key, out value));
                try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) : ""; }
                finally { PropVariantClear(ref value); }
            } finally { Marshal.ReleaseComObject(store); }
        }
        internal static void ConfigureWindow(IntPtr window) {
            var store = WindowProperties(window);
            try {
                // The taskbar may resolve a grouped application's icon from its relaunch
                // metadata instead of WM_GETICON. Portable apps have no installed shortcut.
                // Set all relaunch properties BEFORE publishing the window AppUserModelID.
                SetString(store, 2, Program.QuotePath(Program.Exe));
                SetString(store, 3, Program.Exe + ",0");
                SetString(store, 4, "占用查找器");
                SetString(store, 5, Id);
                Marshal.ThrowExceptionForHR(store.Commit());
            } finally { Marshal.ReleaseComObject(store); }
        }
        internal static bool ShellIconIsCustom() {
            var big = new IntPtr[1]; var small = new IntPtr[1];
            try {
                ExtractIconEx(Program.Exe, 0, big, small, 1);
                return IsBlueIcon(big[0]) && IsBlueIcon(small[0]);
            } finally { if (big[0] != IntPtr.Zero) DestroyIcon(big[0]); if (small[0] != IntPtr.Zero) DestroyIcon(small[0]); }
        }
        internal static bool IsBlueIcon(IntPtr handle) {
            if (handle == IntPtr.Zero) return false;
            using (var borrowed = Icon.FromHandle(handle))
            using (var bitmap = borrowed.ToBitmap()) {
                int blue = 0;
                for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++) {
                    var c = bitmap.GetPixel(x, y); if (c.A > 100 && c.B > 150 && c.B > c.R * 1.4) blue++;
                }
                return blue > bitmap.Width * bitmap.Height / 4;
            }
        }
        [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }

    internal sealed class Hit {
        internal int Pid; internal string Name = "", Image = ""; internal long Started;
        internal bool Confirmed; internal HashSet<string> Paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal string Kind { get { return Confirmed ? "系统报告占用" : "发现打开句柄"; } }
    }
    internal sealed class ScanResult {
        internal List<Hit> Hits = new List<Hit>(); internal int Denied, TimedOut, FileCount;
        internal bool Limited; internal List<string> Notes = new List<string>();
    }
    internal sealed class ScanJob { internal int Pid; internal List<ulong> Handles = new List<ulong>(); }

    internal static class Scanner {
        internal static ScanResult Scan(string input, CancellationToken token, Action<string> progress) {
            ScanResult result = new ScanResult(); bool folder = Directory.Exists(input);
            if (!folder && !File.Exists(input)) throw new IOException("路径不存在或当前账户没有访问权限。请检查路径。");
            string target = Native.ResolvePath(Path.GetFullPath(input));
            var hits = new ConcurrentDictionary<int, Hit>();
            progress("正在读取 Windows 文件句柄…");
            List<ScanJob> jobs = null;
            try { jobs = Native.GetJobs(); } catch (Exception ex) { result.Notes.Add("文件句柄扫描未完成：" + ex.Message); }
            if (jobs != null) {
                int done = 0;
                Parallel.ForEach(jobs, new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = 8 }, job => {
                    token.ThrowIfCancellationRequested();
                    // Avoid starting hundreds of helpers for system processes we cannot inspect.
                    IntPtr probe = Native.OpenProcess(0x0040, false, job.Pid);
                    if (probe == IntPtr.Zero) {
                        if (Marshal.GetLastWin32Error() == 5) Interlocked.Increment(ref result.Denied);
                        int skipped = Interlocked.Increment(ref done);
                        progress("正在检查进程 " + skipped + " / " + jobs.Count + " · 已找到 " + hits.Count + " 个程序");
                        return;
                    }
                    Native.CloseHandle(probe);
                    Process p = null;
                    try {
                        var si = new ProcessStartInfo(Program.Exe, "--inspect " + job.Pid + " " + Program.Encode(target) + " " + (folder ? "1" : "0"));
                        si.UseShellExecute = false; si.CreateNoWindow = true; si.RedirectStandardInput = true; si.RedirectStandardOutput = true;
                        p = Process.Start(si);
                        p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                            if (e.Data == "DENIED") { Interlocked.Increment(ref result.Denied); return; }
                            if (e.Data == null || !e.Data.StartsWith("H\t")) return;
                            try {
                                string[] parts = e.Data.Split('\t');
                                Hit hit = hits.GetOrAdd(job.Pid, id => ReadProcess(id));
                                lock (hit) { hit.Paths.Add(Program.Decode(parts[1])); }
                            } catch { }
                        };
                        p.BeginOutputReadLine();
                        p.StandardInput.WriteLine(String.Join(",", job.Handles.Select(h => h.ToString()).ToArray())); p.StandardInput.Close();
                        var watch = Stopwatch.StartNew();
                        while (!p.WaitForExit(80)) {
                            if (token.IsCancellationRequested || watch.ElapsedMilliseconds > 4500) {
                                try { p.Kill(); } catch { }
                                if (!token.IsCancellationRequested) Interlocked.Increment(ref result.TimedOut);
                                break;
                            }
                        }
                        if (p.HasExited) p.WaitForExit();
                    } catch (Exception ex) { if (!(ex is System.ComponentModel.Win32Exception) || ((System.ComponentModel.Win32Exception)ex).NativeErrorCode != 87) Interlocked.Increment(ref result.TimedOut); }
                    finally { if (p != null) { try { if (!p.HasExited) p.Kill(); } catch { } p.Dispose(); } }
                    int count = Interlocked.Increment(ref done);
                    progress("正在检查进程 " + count + " / " + jobs.Count + " · 已找到 " + hits.Count + " 个程序");
                });
            }
            token.ThrowIfCancellationRequested();
            progress("正在向 Windows 核实占用情况…");
            var files = new List<string>();
            if (folder) {
                var dirs = new Stack<string>(); dirs.Push(input); var watch = Stopwatch.StartNew();
                while (dirs.Count > 0 && files.Count < 20000 && watch.ElapsedMilliseconds < 10000) {
                    token.ThrowIfCancellationRequested(); string dir = dirs.Pop();
                    try {
                        foreach (string f in Directory.EnumerateFiles(dir)) { files.Add(f); if (files.Count >= 20000 || watch.ElapsedMilliseconds >= 10000) { result.Limited = true; break; } }
                        foreach (string d in Directory.EnumerateDirectories(dir)) {
                            if ((File.GetAttributes(d) & FileAttributes.ReparsePoint) == 0) dirs.Push(d);
                            else result.Limited = true;
                        }
                    } catch (UnauthorizedAccessException) { result.Limited = true; } catch (IOException) { result.Limited = true; }
                }
                if (dirs.Count > 0) result.Limited = true;
            } else files.Add(input);
            result.FileCount = files.Count;
            // Include mapped/open files found by the handle scan even when directory enumeration was capped.
            foreach (Hit hit in hits.Values) { lock (hit) { foreach (string path in hit.Paths) if (File.Exists(path)) files.Add(path); } }
            files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            for (int i = 0; i < files.Count; i += 256) {
                token.ThrowIfCancellationRequested();
                string[] batch = files.Skip(i).Take(256).ToArray();
                try {
                    foreach (var item in RestartManager.Query(batch)) {
                        Hit hit = hits.GetOrAdd(item.Item1, id => ReadProcess(id));
                        lock (hit) { hit.Confirmed = true; if (hit.Name == "" || hit.Name == "未知程序") hit.Name = item.Item2; }
                    }
                } catch (Exception ex) { result.Notes.Add("Windows 占用核实未完成：" + ex.Message); break; }
            }
            token.ThrowIfCancellationRequested();
            result.Hits = hits.Values.OrderByDescending(h => h.Confirmed).ThenBy(h => h.Name).ToList();
            return result;
        }
        internal static Hit ReadProcess(int pid) {
            Hit h = new Hit { Pid = pid, Name = "未知程序" };
            try { using (var p = Process.GetProcessById(pid)) {
                h.Name = p.ProcessName; try { h.Started = p.StartTime.ToUniversalTime().Ticks; } catch { }
                IntPtr handle = Native.OpenProcess(0x1000, false, pid);
                if (handle != IntPtr.Zero) { try { var b = new StringBuilder(32768); uint n = (uint)b.Capacity; if (Native.QueryFullProcessImageName(handle, 0, b, ref n)) h.Image = b.ToString(); } finally { Native.CloseHandle(handle); } }
            } } catch { }
            return h;
        }
        internal static bool IsSameProcess(Hit h, Process p) {
            return h.Started != 0 && !p.HasExited && p.StartTime.ToUniversalTime().Ticks == h.Started;
        }
    }

    internal static class Native {
        [StructLayout(LayoutKind.Sequential)] internal struct HandleEntry {
            internal IntPtr Object; internal UIntPtr Pid; internal UIntPtr Handle;
            internal uint Access; internal ushort Trace, Type; internal uint Attributes, Reserved;
        }
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("ntdll.dll")] static extern int NtQuerySystemInformation(int cls, IntPtr data, int length, out int needed);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool DuplicateHandle(IntPtr process, IntPtr source, IntPtr current, out IntPtr handle, uint access, bool inherit, uint options);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern uint GetFinalPathNameByHandle(IntPtr file, StringBuilder path, uint size, uint flags);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint size);
        [DllImport("kernel32.dll")] static extern uint GetFileType(IntPtr file);
        internal static string Normalize(string s) {
            if (s.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + s.Substring(8);
            if (s.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)) return s.Substring(4);
            return s;
        }
        internal static string ResolvePath(string path) {
            using (var h = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero)) {
                if (!h.IsInvalid) { var b = new StringBuilder(32768); uint n = GetFinalPathNameByHandle(h.DangerousGetHandle(), b, (uint)b.Capacity, 0); if (n > 0 && n < b.Capacity) return Normalize(b.ToString()); }
            }
            return path;
        }
        internal static bool Matches(string path, string target, bool folder) {
            return path.Equals(target, StringComparison.OrdinalIgnoreCase) || (folder && path.StartsWith(target.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
        }
        internal static List<ScanJob> GetJobs() {
            // Identify the File object type from our own known handle; type indexes vary by Windows boot.
            using (var own = CreateFile(Program.Exe, 0, 7, IntPtr.Zero, 3, 0, IntPtr.Zero)) {
                if (own.IsInvalid) throw new IOException("无法创建用于识别文件句柄的引用。");
                int size = 1024 * 1024, needed; IntPtr buffer = IntPtr.Zero;
                try {
                    while (true) {
                        buffer = Marshal.AllocHGlobal(size); int status = NtQuerySystemInformation(64, buffer, size, out needed);
                        if (status == 0) break;
                        Marshal.FreeHGlobal(buffer); buffer = IntPtr.Zero;
                        if (status != unchecked((int)0xC0000004)) throw new IOException("系统句柄接口返回 0x" + status.ToString("X8"));
                        size = Math.Max(size * 2, needed + 65536); if (size > 256 * 1024 * 1024) throw new IOException("系统句柄数量过多。");
                    }
                    long count = Marshal.ReadInt64(buffer); int stride = Marshal.SizeOf(typeof(HandleEntry)); int header = IntPtr.Size * 2;
                    if (count < 0 || count > (size - header) / stride) throw new IOException("系统句柄数据长度异常。");
                    int self = Process.GetCurrentProcess().Id; ulong ownHandle = unchecked((ulong)own.DangerousGetHandle().ToInt64()); ushort fileType = 0;
                    for (long i = 0; i < count; i++) {
                        var e = (HandleEntry)Marshal.PtrToStructure(IntPtr.Add(buffer, header + checked((int)(i * stride))), typeof(HandleEntry));
                        if (e.Pid.ToUInt64() == (ulong)self && e.Handle.ToUInt64() == ownHandle) { fileType = e.Type; break; }
                    }
                    if (fileType == 0) throw new IOException("无法识别此系统的文件句柄类型。");
                    var groups = new Dictionary<int, ScanJob>();
                    for (long i = 0; i < count; i++) {
                        var e = (HandleEntry)Marshal.PtrToStructure(IntPtr.Add(buffer, header + checked((int)(i * stride))), typeof(HandleEntry));
                        if (e.Type != fileType || e.Pid.ToUInt64() > int.MaxValue) continue;
                        int pid = (int)e.Pid.ToUInt64(); if (pid <= 4 || pid == self) continue;
                        ScanJob job; if (!groups.TryGetValue(pid, out job)) { job = new ScanJob { Pid = pid }; groups.Add(pid, job); }
                        job.Handles.Add(e.Handle.ToUInt64());
                    }
                    return groups.Values.ToList();
                } finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
            }
        }
        internal static int Inspect(string[] args) {
            try {
                int pid = int.Parse(args[1]); string target = Program.Decode(args[2]); bool folder = args[3] == "1";
                string line = Console.ReadLine() ?? ""; IntPtr process = OpenProcess(0x0040, false, pid);
                if (process == IntPtr.Zero) { if (Marshal.GetLastWin32Error() == 5) Console.WriteLine("DENIED"); return 0; }
                try {
                    foreach (string value in line.Split(',')) {
                        ulong raw; if (!ulong.TryParse(value, out raw)) continue; IntPtr dup;
                        if (!DuplicateHandle(process, new IntPtr(unchecked((long)raw)), GetCurrentProcess(), out dup, 0, false, 2)) continue;
                        try {
                            if (GetFileType(dup) != 1) continue;
                            var b = new StringBuilder(32768); uint n = GetFinalPathNameByHandle(dup, b, (uint)b.Capacity, 0);
                            if (n == 0 || n >= b.Capacity) continue;
                            string path = Normalize(b.ToString());
                            if (Matches(path, target, folder)) { Console.WriteLine("H\t" + Program.Encode(path)); Console.Out.Flush(); }
                        } finally { CloseHandle(dup); }
                    }
                } finally { CloseHandle(process); }
                return 0;
            } catch { return 1; }
        }
        internal static SafeFileHandle HoldDirectory(string path) { return CreateFile(path, 0x80000000, 3, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero); }
    }

    internal static class RestartManager {
        [StructLayout(LayoutKind.Sequential)] struct UniqueProcess { public int Pid; public System.Runtime.InteropServices.ComTypes.FILETIME Started; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct ProcessInfo {
            public UniqueProcess Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Service;
            public uint Type, Status, Session; [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
        }
        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)] static extern int RmStartSession(out uint session, int flags, StringBuilder key);
        [DllImport("rstrtmgr.dll")] static extern int RmEndSession(uint session);
        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)] static extern int RmRegisterResources(uint session, uint count, string[] files, uint appCount, IntPtr apps, uint serviceCount, IntPtr services);
        [DllImport("rstrtmgr.dll")] static extern int RmGetList(uint session, out uint needed, ref uint count, [In, Out] ProcessInfo[] processes, ref uint reason);
        internal static List<Tuple<int, string>> Query(string[] files) {
            var output = new List<Tuple<int, string>>(); if (files.Length == 0) return output;
            uint session; int error = RmStartSession(out session, 0, new StringBuilder(33));
            if (error != 0) throw new IOException("无法建立 Windows 占用核实会话（错误 " + error + "）：" + new System.ComponentModel.Win32Exception(error).Message);
            try {
                error = RmRegisterResources(session, (uint)files.Length, files, 0, IntPtr.Zero, 0, IntPtr.Zero);
                if (error != 0) throw new System.ComponentModel.Win32Exception(error);
                uint count = 0, needed, reason = 0; error = RmGetList(session, out needed, ref count, null, ref reason);
                for (int attempt = 0; error == 234 && attempt < 4; attempt++) {
                    count = needed; var list = new ProcessInfo[count]; error = RmGetList(session, out needed, ref count, list, ref reason);
                    if (error == 0) for (int i = 0; i < count; i++) output.Add(Tuple.Create(list[i].Process.Pid, list[i].Name));
                }
                if (error != 0) throw new System.ComponentModel.Win32Exception(error);
            } finally { RmEndSession(session); }
            return output;
        }
    }

    internal sealed class MainForm : Form {
        readonly Color Ink = Color.FromArgb(28, 40, 59), Muted = Color.FromArgb(98, 111, 131), Accent = Color.FromArgb(39, 99, 220);
        TextBox path, details; Button scan, end, locate, copy, file, folder; ListView list;
        ToolStripMenuItem admin, fullReport; ContextMenuStrip moreMenu; Icon smallIcon, largeIcon;
        Label status, count; ProgressBar bar; CancellationTokenSource cancellation; ScanResult last; string scannedPath = "";
        bool closing, ending; bool IsAdmin { get { using (var id = WindowsIdentity.GetCurrent()) return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator); } }
        internal MainForm(string initial) {
            Text = "占用查找器"; Font = new Font("Microsoft YaHei UI", 9F); ForeColor = Ink;
            BackColor = Color.White; StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(880, 550); MinimumSize = new Size(760, 480);
            smallIcon = AppIdentity.LoadIcon(16); largeIcon = AppIdentity.LoadIcon(48);
            Icon = largeIcon; ShowIcon = true; ShowInTaskbar = true; AllowDrop = true;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 10), ColumnCount = 1, RowCount = 6 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            Controls.Add(root);
            var selector = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, RowCount = 2, ColumnCount = 4 };
            selector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); selector.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88)); selector.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); selector.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            selector.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); selector.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
            var label = new Label { Text = "文件或文件夹  ·  支持拖入或粘贴路径", Dock = DockStyle.Fill, ForeColor = Muted, AutoSize = true, Margin = Padding.Empty };
            selector.Controls.Add(label, 0, 0); selector.SetColumnSpan(label, 4);
            path = new TextBox { Dock = DockStyle.Fill, Text = initial, Margin = new Padding(0, 5, 7, 0), BorderStyle = BorderStyle.FixedSingle, AccessibleName = "目标路径" };
            path.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && cancellation == null && scan.Enabled) { e.SuppressKeyPress = true; StartScan(); } };
            selector.Controls.Add(path, 0, 1);
            file = MakeButton("选文件", false); folder = MakeButton("选文件夹", false); scan = MakeButton("查找", true);
            file.Dock = folder.Dock = scan.Dock = DockStyle.Fill;
            selector.Controls.Add(file, 1, 1); selector.Controls.Add(folder, 2, 1); selector.Controls.Add(scan, 3, 1);
            file.Click += delegate { using (var d = new OpenFileDialog { Title = "选择需要检查的文件", CheckFileExists = true }) if (d.ShowDialog(this) == DialogResult.OK) path.Text = d.FileName; };
            folder.Click += delegate { using (var d = new FolderBrowserDialog { Description = "选择需要检查的文件夹", ShowNewFolderButton = false }) if (d.ShowDialog(this) == DialogResult.OK) path.Text = d.SelectedPath; };
            scan.Click += delegate {
                if (cancellation == null) StartScan();
                else { cancellation.Cancel(); scan.Enabled = false; scan.Text = "停止中"; status.Text = "正在停止…"; }
            }; root.Controls.Add(selector, 0, 0);
            count = new Label { Text = "占用程序", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }; root.Controls.Add(count, 0, 1);
            list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White, Margin = Padding.Empty };
            list.Columns.Add("程序", 170); list.Columns.Add("PID", 65); list.Columns.Add("检测结果", 130); list.Columns.Add("相关路径", 430);
            list.Resize += delegate { if (list.Columns.Count == 4) list.Columns[3].Width = Math.Max(140, list.ClientSize.Width - list.Columns[0].Width - list.Columns[1].Width - list.Columns[2].Width - 5); };
            list.SelectedIndexChanged += delegate { ShowSelection(); }; root.Controls.Add(list, 0, 2);
            details = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, BackColor = Color.FromArgb(248, 249, 251), BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 9, 0, 5), Text = "选中结果可查看程序位置和相关文件。", AccessibleName = "程序详情" };
            root.Controls.Add(details, 0, 3);
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 4), RowCount = 1, ColumnCount = 5 };
            foreach (float width in new float[] { 112, 112, 104 }) actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, width));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            locate = MakeButton("程序位置", false); locate.Enabled = false;
            end = MakeButton("结束进程…", false); end.ForeColor = Color.FromArgb(171, 48, 55); end.Enabled = false;
            copy = MakeButton("复制报告", false); copy.Enabled = false;
            var more = MakeButton("更多 ▾", false); locate.Dock = end.Dock = copy.Dock = more.Dock = DockStyle.Fill;
            actions.Controls.Add(locate, 0, 0); actions.Controls.Add(end, 1, 0); actions.Controls.Add(copy, 2, 0); actions.Controls.Add(more, 4, 0); root.Controls.Add(actions, 0, 4);
            moreMenu = new ContextMenuStrip { Font = Font };
            fullReport = new ToolStripMenuItem("查看完整报告…"); fullReport.Enabled = false;
            var task = new ToolStripMenuItem("打开任务管理器");
            admin = new ToolStripMenuItem(IsAdmin ? "已是管理员" : "以管理员身份重试"); admin.Enabled = !IsAdmin;
            moreMenu.Items.AddRange(new ToolStripItem[] { fullReport, task, new ToolStripSeparator(), admin });
            more.Click += delegate { moreMenu.Show(more, new Point(0, more.Height)); };
            admin.Click += Elevate;
            fullReport.Click += delegate { ShowReport(); };
            locate.Click += delegate { var h = Selected(); if (h != null && File.Exists(h.Image)) TryAction(() => Process.Start("explorer.exe", "/select,\"" + h.Image + "\"")); };
            task.Click += delegate { TryAction(() => Process.Start("taskmgr.exe")); };
            copy.Click += delegate { TryAction(() => Clipboard.SetText(Report())); };
            end.Click += EndProcess;
            var foot = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            status = new Label { Text = "就绪", Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, ForeColor = Muted, AutoEllipsis = true }; foot.Controls.Add(status);
            bar = new ProgressBar { Dock = DockStyle.Top, Height = 3, Visible = false, Style = ProgressBarStyle.Marquee }; foot.Controls.Add(bar); root.Controls.Add(foot, 0, 5);
            DragEnter += delegate(object s, DragEventArgs e) { e.Effect = cancellation == null && !ending && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; };
            DragDrop += delegate(object s, DragEventArgs e) { var names = e.Data.GetData(DataFormats.FileDrop) as string[]; if (cancellation == null && !ending && names != null && names.Length > 0) path.Text = names[0]; };
            FormClosing += delegate { closing = true; if (cancellation != null) cancellation.Cancel(); };
        }
        protected override void OnHandleCreated(EventArgs e) {
            base.OnHandleCreated(e);
            if (smallIcon != null) AppIdentity.SendMessage(Handle, 0x0080, IntPtr.Zero, smallIcon.Handle);
            if (largeIcon != null) AppIdentity.SendMessage(Handle, 0x0080, new IntPtr(1), largeIcon.Handle);
            AppIdentity.ConfigureWindow(Handle);
        }
        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
            if (disposing) { if (smallIcon != null) smallIcon.Dispose(); if (largeIcon != null) largeIcon.Dispose(); if (moreMenu != null) moreMenu.Dispose(); }
        }
        void ShowReport() {
            using (var dialog = new Form { Text = "检查报告", Size = new Size(760, 520), MinimumSize = new Size(550, 380), StartPosition = FormStartPosition.CenterParent, Font = Font, Icon = largeIcon, ShowInTaskbar = false }) {
                var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, Text = Report(), BackColor = Color.White };
                dialog.Padding = new Padding(12); dialog.Controls.Add(text); dialog.ShowDialog(this);
            }
        }
        Button MakeButton(string text, bool primary) {
            var b = new Button { Text = text, Height = 34, Dock = DockStyle.None, FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Color.White, ForeColor = primary ? Color.White : Ink, Cursor = Cursors.Hand, Margin = new Padding(3, 0, 3, 0), UseVisualStyleBackColor = false, AutoSize = false, Width = 112 };
            b.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(216, 223, 234); return b;
        }
        void TryAction(Action action) { try { action(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Information); } }
        void Ui(Action action) { if (closing || IsDisposed || !IsHandleCreated) return; try { BeginInvoke((MethodInvoker)delegate { if (!closing) action(); }); } catch (InvalidOperationException) { } }
        void Busy(bool busy) { scan.Enabled = !ending; scan.Text = busy ? "停止" : "查找"; path.Enabled = file.Enabled = folder.Enabled = !busy && !ending; bar.Visible = busy; admin.Enabled = !busy && !ending && !IsAdmin; fullReport.Enabled = copy.Enabled = !busy && !ending && last != null; ShowSelection(); }
        async void StartScan() {
            if (cancellation != null || ending) return;
            string input = Environment.ExpandEnvironmentVariables(path.Text.Trim().Trim('"'));
            if (String.IsNullOrWhiteSpace(input)) { MessageBox.Show(this, "先选择一个文件或文件夹，也可以直接拖入窗口。", "选择目标", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (input.StartsWith(@"\\", StringComparison.Ordinal)) { MessageBox.Show(this, "当前版本用于本机磁盘。网络共享的占用需要在共享所在电脑上检查。", "网络路径", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            try { input = Path.GetFullPath(input); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "路径无效"); return; }
            if (!Directory.Exists(input) && !File.Exists(input)) { MessageBox.Show(this, "路径不存在或当前账户没有访问权限。", "无法访问路径", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            scannedPath = input; path.Text = input; last = null; list.Items.Clear(); count.Text = "正在查找…"; details.Clear();
            cancellation = new CancellationTokenSource(); var source = cancellation; Busy(true);
            long lastUpdate = 0;
            try {
                var found = await Task.Run(() => Scanner.Scan(input, source.Token, text => {
                    long now = DateTime.UtcNow.Ticks; long prev = Interlocked.Read(ref lastUpdate);
                    if (now - prev > TimeSpan.TicksPerMillisecond * 150 && Interlocked.CompareExchange(ref lastUpdate, now, prev) == prev) Ui(() => { if (cancellation == source) status.Text = text; });
                }));
                if (closing) return;
                last = found;
                foreach (var hit in found.Hits) {
                    var row = new ListViewItem(new string[] { hit.Name, hit.Pid.ToString(), hit.Kind, hit.Paths.Count == 0 ? "Windows 报告涉及目标中的文件（未提供具体路径）" : String.Join(" ; ", hit.Paths.Take(3).ToArray()) });
                    row.Tag = hit; list.Items.Add(row);
                }
                count.Text = "占用程序  ·  " + found.Hits.Count + " 个结果";
                bool incomplete = found.Denied > 0 || found.TimedOut > 0 || found.Limited || found.Notes.Count > 0;
                status.Text = (found.Hits.Count > 0 ? "查找完成" : "未发现占用") + (incomplete ? " · 部分项目未能检查，详见“更多 → 查看完整报告”" : " · 先关闭相关程序，再尝试删除");
                details.Text = found.Hits.Count == 0 ? "本次未发现占用。也可能是权限、系统保护或短暂占用导致，具体信息可查看完整报告。" : "选中结果可查看程序位置和相关文件。";
                if (list.Items.Count > 0) list.Items[0].Selected = true;
            } catch (OperationCanceledException) { if (!closing) { status.Text = "已停止查找 · 可以重新开始"; count.Text = "查找已停止"; details.Text = "本次检查已取消，未生成完整结果。"; } }
            catch (Exception ex) { if (!closing) { status.Text = "查找未完成"; details.Text = ex.Message + "\r\n可尝试以管理员身份重新运行。"; count.Text = "查找未完成"; } }
            finally { cancellation = null; source.Dispose(); if (!closing) Busy(false); }
        }
        Hit Selected() { return list.SelectedItems.Count == 1 ? list.SelectedItems[0].Tag as Hit : null; }
        string Summary(ScanResult r) {
            var b = new StringBuilder();
            if (r.Hits.Count == 0) b.AppendLine("未发现占用不等于一定可以删除。权限、受保护文件、驱动、短暂占用或其他电脑的网络连接也可能导致失败。");
            b.AppendLine("系统报告占用：Windows Restart Manager 认为此进程正在使用目标中的文件。");
            b.AppendLine("发现打开句柄：此进程打开了目标或其内部路径；该句柄可能允许删除。");
            b.AppendLine("无法访问的进程：" + r.Denied + "；超时/退出等未完整检查：" + r.TimedOut + "。" + (!IsAdmin && r.Denied > 0 ? "可从“更多”菜单以管理员身份重试。" : ""));
            if (r.Limited) b.AppendLine("文件遍历受限：最多核实 20,000 个文件 / 10 秒；跳过无权限目录和内部链接目录。句柄扫描仍覆盖可读取的进程。");
            foreach (string note in r.Notes) b.AppendLine(note);
            return b.ToString();
        }
        void ShowSelection() {
            Hit h = Selected(); bool ready = cancellation == null && !ending && h != null;
            locate.Enabled = ready && !String.IsNullOrEmpty(h.Image); end.Enabled = ready && h.Pid > 4 && h.Started != 0 && h.Pid != Process.GetCurrentProcess().Id;
            if (h == null) return;
            details.Text = "程序：" + (h.Image == "" ? h.Name + "（无法读取程序位置）" : h.Image) + "\r\n相关路径：" + (h.Paths.Count == 0 ? "Windows 未提供具体路径" : String.Join("；", h.Paths.ToArray())) + (h.Confirmed ? "" : "\r\n提示：打开句柄不一定阻止删除。");
        }
        string Report() {
            var b = new StringBuilder("占用查找器检查报告\r\n"); b.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); b.AppendLine("目标：" + scannedPath);
            if (last == null) return b.ToString(); b.AppendLine(Summary(last));
            foreach (var h in last.Hits) { b.AppendLine(h.Name + " | PID " + h.Pid + " | " + h.Kind); b.AppendLine("程序：" + h.Image); foreach (string p in h.Paths) b.AppendLine("  " + p); b.AppendLine(); }
            return b.ToString();
        }
        void Elevate(object sender, EventArgs e) {
            try { var si = new ProcessStartInfo(Program.Exe); si.UseShellExecute = true; si.Verb = "runas"; if (!String.IsNullOrWhiteSpace(path.Text)) si.Arguments = Program.QuotePath(path.Text.Trim().Trim('"')); Process.Start(si); Close(); }
            catch (System.ComponentModel.Win32Exception ex) { if (ex.NativeErrorCode != 1223) MessageBox.Show(this, ex.Message, "无法以管理员身份启动"); }
        }
        async void EndProcess(object sender, EventArgs e) {
            Hit h = Selected(); if (h == null || cancellation != null || ending) return;
            ending = true; Busy(false);
            try {
                using (var p = Process.GetProcessById(h.Pid)) {
                    if (!Scanner.IsSameProcess(h, p)) { MessageBox.Show(this, "该进程已退出或发生变化，请重新查找。", "请刷新结果"); return; }
                    bool critical; if (IsCritical(p, out critical) && critical) { MessageBox.Show(this, "这是 Windows 关键进程，不能在本工具中结束。", "受保护的进程"); return; }
                    if (MessageBox.Show(this, "准备关闭 " + h.Name + "（PID " + h.Pid + "）。\r\n请先保存该程序中尚未保存的内容。\r\n\r\n继续发送正常关闭请求？", "关闭选中程序", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                    end.Enabled = false; bool requested = p.CloseMainWindow();
                    if (requested) await Task.Run(() => p.WaitForExit(1800));
                    if (!p.HasExited) {
                        if (MessageBox.Show(this, (requested ? "程序尚未退出，可能正在提示保存文件。" : "该进程没有可正常关闭的主窗口。") + "\r\n\r\n是否强制结束 " + h.Name + "（PID " + h.Pid + "）？\r\n未保存的内容可能丢失，相关服务也可能中断。", "确认强制结束", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                        if (!Scanner.IsSameProcess(h, p)) return;
                        p.Kill(); await Task.Run(() => p.WaitForExit(2500));
                    }
                    status.Text = "已发送结束请求 · 请重新查找后再尝试删除";
                }
            } catch (ArgumentException) { status.Text = "进程已经退出，请重新查找"; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message + "\r\n可尝试管理员重试，或手动关闭该程序。", "未能结束进程", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            finally { ending = false; if (!closing) Busy(false); }
        }
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool IsProcessCritical(IntPtr process, out bool critical);
        static bool IsCritical(Process p, out bool critical) { critical = false; try { return IsProcessCritical(p.Handle, out critical); } catch { return false; } }
    }

    internal static class SelfTest {
        internal static int CheckWindow(string[] args) {
            var log = new StringBuilder();
            try {
                AppIdentity.Configure(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Assert(AppIdentity.Current() == AppIdentity.Id, "explicit taskbar application identity", log);
                using (var form = new MainForm("")) {
                    IntPtr window = form.Handle;
                    Assert(form.ShowInTaskbar && form.ShowIcon, "window appears in taskbar with an icon", log);
                    Assert(AppIdentity.SendMessage(window, 0x007F, IntPtr.Zero, IntPtr.Zero) != IntPtr.Zero, "native small window icon is set", log);
                    Assert(AppIdentity.SendMessage(window, 0x007F, new IntPtr(1), IntPtr.Zero) != IntPtr.Zero, "native large taskbar icon is set", log);
                    Assert(AppIdentity.IsBlueIcon(AppIdentity.SendMessage(window, 0x007F, new IntPtr(1), IntPtr.Zero)), "large window icon contains the custom blue artwork", log);
                    Assert(AppIdentity.ShellIconIsCustom(), "Windows Shell extracts custom artwork from the executable", log);
                    Assert(AppIdentity.WindowString(window, 2) == Program.QuotePath(Program.Exe), "taskbar relaunch command points to this executable", log);
                    Assert(AppIdentity.WindowString(window, 3) == Program.Exe + ",0", "taskbar relaunch icon points to this executable's icon", log);
                    Assert(AppIdentity.WindowString(window, 4) == "占用查找器", "taskbar relaunch display name is set", log);
                    Assert(AppIdentity.WindowString(window, 5) == AppIdentity.Id, "window and process use the same explicit taskbar identity", log);
                }
                log.AppendLine("ALL WINDOW CHECKS PASSED"); return 0;
            } catch (Exception ex) { log.AppendLine("FAIL " + ex); return 1; }
            finally { if (args.Length > 1) File.WriteAllText(args[1], log.ToString(), new UTF8Encoding(true)); }
        }
        internal static int Hold(string[] args) {
            try {
                using (var file = new FileStream(args[1], FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var dir = Native.HoldDirectory(args[2])) {
                    File.WriteAllText(args[3], dir.IsInvalid ? "BAD" : "READY"); Thread.Sleep(120000);
                }
                return 0;
            } catch (Exception ex) { File.WriteAllText(args[3], ex.ToString()); return 1; }
        }
        internal static int Run(string[] args) {
            var log = new StringBuilder(); string root = Path.Combine(Path.GetTempPath(), "LockLens-Test-" + Guid.NewGuid().ToString("N"));
            string folder = Path.Combine(root, "中文 文件夹"), sibling = folder + "-other", ready = Path.Combine(root, "ready"); Process holder = null;
            try {
                Directory.CreateDirectory(folder); Directory.CreateDirectory(sibling); string f = Path.Combine(folder, "测试 占用.txt"); File.WriteAllText(f, "fixture");
                holder = Process.Start(new ProcessStartInfo(Program.Exe, "--hold \"" + f + "\" \"" + folder + "\" \"" + ready + "\"") { UseShellExecute = false, CreateNoWindow = true });
                var watch = Stopwatch.StartNew(); while (!File.Exists(ready) && watch.ElapsedMilliseconds < 10000) Thread.Sleep(50);
                Assert(File.Exists(ready) && File.ReadAllText(ready) == "READY", "fixture holds file and directory", log);
                bool blocked = false; try { using (File.Open(f, FileMode.Open, FileAccess.Write, FileShare.None)) { } } catch (IOException) { blocked = true; }
                Assert(blocked, "fixture really blocks exclusive access", log);
                var fileScan = Scanner.Scan(f, CancellationToken.None, s => { });
                log.AppendLine("INFO file scan: " + fileScan.Hits.Count + " results; " + String.Join(" | ", fileScan.Notes.ToArray()));
                foreach (var info in fileScan.Hits) log.AppendLine("INFO PID " + info.Pid + " confirmed=" + info.Confirmed);
                Assert(fileScan.Hits.Any(h => h.Pid == holder.Id && h.Paths.Contains(f)), "file scan finds holder and exact Unicode path", log);
                Assert(fileScan.Hits.Any(h => h.Pid == holder.Id && h.Confirmed), "Restart Manager confirms holder", log);
                var dirScan = Scanner.Scan(folder, CancellationToken.None, s => { });
                Assert(dirScan.Hits.Any(h => h.Pid == holder.Id && h.Paths.Contains(folder)), "directory handle is found", log);
                Assert(dirScan.Hits.Any(h => h.Pid == holder.Id && h.Paths.Contains(f)), "recursive folder scan finds child file", log);
                Assert(!Native.Matches(Path.Combine(sibling, "file"), folder, true), "sibling prefix does not match", log);
                Assert(Native.Matches("D:\\abc\\file", "D:\\", true), "drive root descendant matches", log);
                Assert(Native.Normalize(@"\\?\UNC\server\share\x") == @"\\server\share\x", "UNC normalization", log);
                var cts = new CancellationTokenSource(); cts.Cancel(); bool canceled = false;
                try { Scanner.Scan(f, cts.Token, s => { }); } catch (OperationCanceledException) { canceled = true; } catch (AggregateException ex) { canceled = ex.InnerExceptions.All(e => e is OperationCanceledException); }
                Assert(canceled, "cancel stops scan", log);
                var old = Scanner.ReadProcess(holder.Id); Assert(old.Started != 0 && Scanner.IsSameProcess(old, holder), "process identity verifies", log); old.Started++;
                Assert(!Scanner.IsSameProcess(old, holder), "changed process identity rejected", log);
                holder.Kill(); holder.WaitForExit();
                var free = Scanner.Scan(f, CancellationToken.None, s => { }); Assert(!free.Hits.Any(h => h.Pid == holder.Id), "released lock is absent after rescan", log);
                using (File.Open(f, FileMode.Open, FileAccess.Write, FileShare.None)) { } log.AppendLine("PASS exclusive access restored after fixture exits");
                log.AppendLine("ALL TESTS PASSED"); return 0;
            } catch (Exception ex) { log.AppendLine("FAIL " + ex); return 1; }
            finally {
                if (holder != null) { try { if (!holder.HasExited) { holder.Kill(); holder.WaitForExit(); } } catch { } holder.Dispose(); }
                try { Directory.Delete(root, true); } catch { }
                if (args.Length > 1) File.WriteAllText(args[1], log.ToString(), new UTF8Encoding(true));
            }
        }
        static void Assert(bool condition, string name, StringBuilder log) { if (!condition) throw new Exception(name); log.AppendLine("PASS " + name); }
    }
}
