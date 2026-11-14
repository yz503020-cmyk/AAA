using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;

namespace AAA;

/// <summary>
/// הוואצ'דוג: שירות SYSTEM שבודק כל תהליך חדש בחשבונות היעד, ואם הקובץ שלו לא ברשימה הלבנה – סוגר אותו מיד.
/// במצב למידה הוא רק רושם (בשביל לגלות אילו קבצים נוספים התוכנות המאושרות צריכות).
/// </summary>
static class Watchdog
{
  [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(int access, bool inherit, int pid);
  [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
  [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr h, uint code);
  [DllImport("kernel32.dll")] static extern bool GetProcessTimes(IntPtr h, out long create, out long exit, out long kernel, out long user);
  [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr h, int access, out IntPtr tok);
  [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr tok, int cls, IntPtr buf, int len, out int ret);

  const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, PROCESS_TERMINATE = 0x0001;

  static string? ImagePath(IntPtr h)
  {
    var sb = new StringBuilder(1024); int size = sb.Capacity;
    return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
  }

  static string? UserSid(IntPtr h)
  {
    if (!OpenProcessToken(h, 0x0008 /*TOKEN_QUERY*/, out var tok)) return null;
    try
    {
      GetTokenInformation(tok, 1 /*TokenUser*/, IntPtr.Zero, 0, out int len);
      if (len <= 0) return null;
      var buf = Marshal.AllocHGlobal(len);
      try
      {
        if (!GetTokenInformation(tok, 1, buf, len, out _)) return null;
        var psid = Marshal.ReadIntPtr(buf);
        return new SecurityIdentifier(psid).Value;
      }
      finally { Marshal.FreeHGlobal(buf); }
    }
    finally { CloseHandle(tok); }
  }

  // ---------- לולאת הבדיקה ----------

  sealed class State
  {
    public Config Cfg = new();
    public Whitelist.Compiled Rules = new();
    public HashSet<string> Targets = new(StringComparer.OrdinalIgnoreCase);
    public DateTime CfgStamp;
    public readonly Dictionary<(int pid, long start), bool> Seen = new();
  }

  public static void Loop(CancellationToken ct)
  {
    var st = new State();
    Log.Write("Watchdog started");
    int tick = 0;
    while (!ct.IsCancellationRequested)
    {
      try
      {
        if (tick++ % 10 == 0) Reload(st);
        if (st.Cfg.WatchdogEnabled && st.Targets.Count > 0) Scan(st);
      }
      catch (Exception ex) { Log.Write("Watchdog loop: " + ex.Message); }
      ct.WaitHandle.WaitOne(250);
    }
    Log.Write("Watchdog stopped");
  }

  static void Reload(State st)
  {
    var fi = new FileInfo(AppPaths.ConfigFile);
    if (!fi.Exists || fi.LastWriteTimeUtc == st.CfgStamp) return;
    st.CfgStamp = fi.LastWriteTimeUtc;
    st.Cfg = Config.Load();
    st.Rules = Whitelist.Compile(st.Cfg);
    st.Targets = new HashSet<string>(st.Cfg.TargetSids, StringComparer.OrdinalIgnoreCase);
    st.Seen.Clear();
    Log.Write($"Watchdog config: enabled={st.Cfg.WatchdogEnabled} learning={st.Cfg.WatchdogLearning} targets={st.Targets.Count} allow={st.Rules.Allow.Count}");
  }

  static void Scan(State st)
  {
    var alive = new HashSet<(int, long)>();
    foreach (var p in Process.GetProcesses())
    {
      using (p)
      {
        int pid = p.Id;
        if (pid <= 4) continue;
        int session; try { session = p.SessionId; } catch { continue; }
        if (session == 0) continue;
        var h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE, false, pid);
        if (h == IntPtr.Zero) h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) continue;
        try
        {
          GetProcessTimes(h, out long create, out _, out _, out _);
          var key = (pid, create);
          alive.Add(key);
          if (st.Seen.ContainsKey(key)) continue;
          st.Seen[key] = true;
          var sid = UserSid(h);
          if (sid == null || !st.Targets.Contains(sid)) continue;
          var path = ImagePath(h);
          if (path == null) continue;
          var reason = st.Rules.Check(path);
          if (reason == null) continue;
          bool kill = !st.Cfg.WatchdogLearning;
          if (kill) TerminateProcess(h, 1260);
          WriteBlocked(sid, path, reason, kill);
        }
        finally { CloseHandle(h); }
      }
    }
    // ניקוי זיכרון של תהליכים שנסגרו
    if (st.Seen.Count > 5000)
      foreach (var k in st.Seen.Keys.Where(k => !alive.Contains(k)).ToList()) st.Seen.Remove(k);
  }

  static void WriteBlocked(string sid, string path, string reason, bool killed)
  {
    try
    {
      var fi = new FileInfo(AppPaths.BlockedLog);
      if (fi.Exists && fi.Length > 1_000_000) { File.Delete(AppPaths.BlockedLog + ".old"); fi.MoveTo(AppPaths.BlockedLog + ".old"); }
      File.AppendAllText(AppPaths.BlockedLog, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{sid}\t{(killed ? "נסגר" : "למידה")}\t{reason}\t{path}{Environment.NewLine}", Encoding.UTF8);
    }
    catch { }
  }

  public record Entry(DateTime Time, string User, bool Killed, string Reason, string Path);

  public static List<Entry> ReadBlocked(int max = 500)
  {
    var list = new List<Entry>();
    try
    {
      if (!File.Exists(AppPaths.BlockedLog)) return list;
      using var fs = new FileStream(AppPaths.BlockedLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      using var sr = new StreamReader(fs, Encoding.UTF8);
      var lines = Enumerable.Reverse(sr.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)).Take(max);
      var names = new Dictionary<string, string>();
      foreach (var l in lines)
      {
        var f = l.TrimEnd('\r').Split('\t');
        if (f.Length < 5) continue;
        if (!names.TryGetValue(f[1], out var nm)) names[f[1]] = nm = Users.NameOf(f[1]) ?? f[1];
        DateTime.TryParse(f[0], out var t);
        list.Add(new Entry(t, nm, f[2] == "נסגר", f[3], f[4]));
      }
    }
    catch (Exception ex) { Log.Write("ReadBlocked: " + ex.Message); }
    return list;
  }

  // ---------- התקנת השירות ----------

  public static bool IsInstalled() => ServiceController.GetServices().Any(s => s.ServiceName.Equals(AppPaths.ServiceName, StringComparison.OrdinalIgnoreCase));

  public static string Status()
  {
    try
    {
      using var sc = new ServiceController(AppPaths.ServiceName);
      return sc.Status == ServiceControllerStatus.Running ? "רץ" : sc.Status.ToString();
    }
    catch { return "לא מותקן"; }
  }

  public static string Install()
  {
    AppPaths.EnsureRoot();
    var self = Environment.ProcessPath!;
    bool same = string.Equals(Path.GetFullPath(self), Path.GetFullPath(AppPaths.ServiceExe), StringComparison.OrdinalIgnoreCase);
    if (IsInstalled()) Shell.Exe("sc.exe", $"stop {AppPaths.ServiceName}");
    if (!same)
    {
      for (int i = 0; i < 20; i++)
      {
        try { File.Copy(self, AppPaths.ServiceExe, true); break; }
        catch { Thread.Sleep(500); }
      }
    }
    if (!IsInstalled())
    {
      var r = Shell.Exe("sc.exe", $"create {AppPaths.ServiceName} binPath= \"\\\"{AppPaths.ServiceExe}\\\" --service\" start= auto DisplayName= \"AAA Watchdog\"");
      if (r.Exit != 0) return "התקנת השירות נכשלה: " + r.Output.Trim();
      Shell.Exe("sc.exe", $"description {AppPaths.ServiceName} \"AAA – סוגר תוכנות שאינן ברשימה הלבנה\"");
    }
    // הפעלה מחדש אוטומטית אם השירות נסגר
    Shell.Exe("sc.exe", $"failure {AppPaths.ServiceName} reset= 0 actions= restart/1000/restart/1000/restart/1000");
    Shell.Exe("sc.exe", $"failureflag {AppPaths.ServiceName} 1");
    var s = Shell.Exe("sc.exe", $"start {AppPaths.ServiceName}");
    return Status() == "רץ" ? "שירות הוואצ'דוג רץ." : "השירות הותקן אך לא עלה: " + s.Output.Trim();
  }

  public static string Uninstall()
  {
    if (!IsInstalled()) return "השירות לא מותקן.";
    Shell.Exe("sc.exe", $"stop {AppPaths.ServiceName}");
    Thread.Sleep(1000);
    var r = Shell.Exe("sc.exe", $"delete {AppPaths.ServiceName}");
    try { File.Delete(AppPaths.ServiceExe); } catch { }
    return r.Exit == 0 ? "שירות הוואצ'דוג הוסר." : r.Output.Trim();
  }
}

sealed class WatchdogService : ServiceBase
{
  CancellationTokenSource? _cts;
  Thread? _thread;

  public WatchdogService() { ServiceName = AppPaths.ServiceName; CanStop = true; CanShutdown = true; }

  protected override void OnStart(string[] args)
  {
    _cts = new CancellationTokenSource();
    _thread = new Thread(() => Watchdog.Loop(_cts.Token)) { IsBackground = true, Name = "watchdog" };
    _thread.Start();
  }

  protected override void OnStop() { _cts?.Cancel(); _thread?.Join(3000); }
  protected override void OnShutdown() => OnStop();
}
