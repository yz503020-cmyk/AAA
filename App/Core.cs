using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AAA;

/// <summary>נתיבים קבועים של התוכנה</summary>
static class AppPaths
{
  public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AAA");
  public static string ConfigFile => Path.Combine(Root, "config.json");
  public static string LogFile => Path.Combine(Root, "aaa.log");
  public static string BlockedLog => Path.Combine(Root, "blocked.log");
  public static string AppLockerXml => Path.Combine(Root, "applocker.xml");
  public static string ServiceExe => Path.Combine(Root, "AAAWatch.exe");
  public const string ServiceName = "AAAWatchdog";

  /// <summary>יוצר את תיקיית הנתונים ונועל אותה: מנהלים ו-SYSTEM מלא, משתמשים קריאה בלבד</summary>
  public static void EnsureRoot()
  {
    Directory.CreateDirectory(Root);
    try
    {
      var di = new DirectoryInfo(Root);
      var sec = new DirectorySecurity();
      sec.SetAccessRuleProtection(true, false);
      var inh = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
      sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inh, PropagationFlags.None, AccessControlType.Allow));
      sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inh, PropagationFlags.None, AccessControlType.Allow));
      sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, inh, PropagationFlags.None, AccessControlType.Allow));
      di.SetAccessControl(sec);
    }
    catch (Exception ex) { Log.Write("EnsureRoot ACL: " + ex.Message); }
  }
}

static class Log
{
  static readonly object Gate = new();
  public static void Write(string msg)
  {
    try
    {
      lock (Gate)
      {
        Directory.CreateDirectory(AppPaths.Root);
        var fi = new FileInfo(AppPaths.LogFile);
        if (fi.Exists && fi.Length > 2_000_000) { File.Delete(AppPaths.LogFile + ".old"); fi.MoveTo(AppPaths.LogFile + ".old"); }
        File.AppendAllText(AppPaths.LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}", Encoding.UTF8);
      }
    }
    catch { }
  }
}

static class Shell
{
  public record Result(int Exit, string Output);

  public static Result Run(string file, string args, int timeoutMs = 120_000)
  {
    try
    {
      var psi = new ProcessStartInfo(file, args)
      {
        UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true,
        StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
      };
      using var p = Process.Start(psi)!;
      var so = p.StandardOutput.ReadToEndAsync();
      var se = p.StandardError.ReadToEndAsync();
      if (!p.WaitForExit(timeoutMs)) { try { p.Kill(true); } catch { } return new Result(-1, "timeout"); }
      var outp = so.Result + se.Result;
      Log.Write($"RUN {Path.GetFileName(file)} {Trunc(args, 300)} => {p.ExitCode}");
      return new Result(p.ExitCode, outp);
    }
    catch (Exception ex) { Log.Write($"RUN {file} failed: {ex.Message}"); return new Result(-1, ex.Message); }
  }

  static string Sys32(string exe) => Path.Combine(Environment.SystemDirectory, exe);

  /// <summary>מריץ פקודת PowerShell (של המנהל – לא מושפע מחסימות משתמש היעד)</summary>
  public static Result PowerShell(string script, int timeoutMs = 300_000)
  {
    // פלט ב-UTF8 כדי ששמות בעברית יגיעו תקינים
    var full = "[Console]::OutputEncoding=[Text.Encoding]::UTF8;$ProgressPreference='SilentlyContinue';" + script;
    var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
    return Run(Sys32(@"WindowsPowerShell\v1.0\powershell.exe"), "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + enc, timeoutMs);
  }

  public static Result Exe(string sys32Exe, string args, int timeoutMs = 120_000) => Run(Sys32(sys32Exe), args, timeoutMs);

  public static string Trunc(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}

// ---------------- הגדרות ----------------

public class BackupEntry
{
  public bool Existed { get; set; }
  public string Kind { get; set; } = "";
  public string Data { get; set; } = "";
}

public class AllowedEntry
{
  public string Path { get; set; } = "";
  public bool IsFolder { get; set; }
  /// <summary>מאיזה פרופיל תוכנה הגיע (או "ידני")</summary>
  public string Source { get; set; } = "ידני";
}

public class Config
{
  /// <summary>חשבונות היעד (SID) – עליהם חלות החסימות</summary>
  public List<string> TargetSids { get; set; } = new();
  /// <summary>מזהי המדיניות המסומנים</summary>
  public HashSet<string> Policies { get; set; } = new();
  /// <summary>גיבוי ערכי הרישום שלפני השינוי, כדי שביטול יחזיר בדיוק את המצב הקודם</summary>
  public Dictionary<string, BackupEntry> Backups { get; set; } = new();
  public List<AllowedEntry> Allowed { get; set; } = new();

  public bool AppLockerEnabled { get; set; }
  public bool AppLockerAuditOnly { get; set; } = true;
  public bool AppLockerStrictPackaged { get; set; }
  public bool AppLockerApplied { get; set; }

  public bool WatchdogEnabled { get; set; }
  public bool WatchdogLearning { get; set; } = true;

  /// <summary>חסימות "כלים מסוכנים" בתוך תיקיית Windows (לפי מזהה)</summary>
  public HashSet<string> DenyTools { get; set; } = new();
  /// <summary>קידומות שמות חבילות (WindowsApps) שמותרות במצב מחמיר</summary>
  public List<string> AllowedPackages { get; set; } = new() { "Microsoft.Windows.Photos_" };

  public Dictionary<string, bool> BiosChecklist { get; set; } = new();
  public bool WinReDisabledByUs { get; set; }
  public bool BcdRecoveryDisabledByUs { get; set; }

  static readonly JsonSerializerOptions Opts = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

  public static Config Load()
  {
    try
    {
      if (File.Exists(AppPaths.ConfigFile))
        return JsonSerializer.Deserialize<Config>(File.ReadAllText(AppPaths.ConfigFile), Opts) ?? new Config();
    }
    catch (Exception ex) { Log.Write("Config load failed: " + ex.Message); }
    return new Config();
  }

  public void Save()
  {
    AppPaths.EnsureRoot();
    var tmp = AppPaths.ConfigFile + ".tmp";
    File.WriteAllText(tmp, JsonSerializer.Serialize(this, Opts), Encoding.UTF8);
    File.Move(tmp, AppPaths.ConfigFile, true);
  }

  public Config Clone() => JsonSerializer.Deserialize<Config>(JsonSerializer.Serialize(this, Opts), Opts)!;
}
