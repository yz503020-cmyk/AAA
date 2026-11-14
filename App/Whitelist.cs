namespace AAA;

/// <summary>כלי מסוכן בתוך Windows שנחסם למשתמש היעד למרות שתיקיית Windows מותרת</summary>
public record DenyTool(string Id, string Title, string[] Paths, bool Recommended = true);

/// <summary>
/// הלוגיקה המשותפת לאפפלוקר ולוואצ'דוג: מה מותר ומה אסור.
/// נתיבים בתחביר אפפלוקר: %WINDIR%, %SYSTEM32%, %PROGRAMFILES%, %OSDRIVE%. "\*" בסוף = כל התיקייה.
/// </summary>
static class Whitelist
{
  /// <summary>תיקיות בתוך Windows שמשתמש רגיל יכול לכתוב אליהן – לא מותרות (אחרת מעתיקים לשם EXE ומריצים)</summary>
  public static readonly string[] WindowsWritableExceptions =
  {
    @"%WINDIR%\Temp\*", @"%WINDIR%\Tasks\*", @"%WINDIR%\tracing\*", @"%WINDIR%\Registration\CRMLog\*",
    @"%WINDIR%\PLA\*", @"%WINDIR%\debug\*", @"%WINDIR%\ServiceProfiles\*",
    @"%SYSTEM32%\Tasks\*", @"%SYSTEM32%\Tasks_Migrated\*", @"%SYSTEM32%\com\dmp\*", @"%SYSTEM32%\FxsTmp\*",
    @"%SYSTEM32%\spool\drivers\color\*", @"%SYSTEM32%\spool\PRINTERS\*", @"%SYSTEM32%\spool\SERVERS\*",
    @"%SYSTEM32%\Microsoft\Crypto\RSA\MachineKeys\*", @"%SYSTEM32%\LogFiles\WMI\*",
    @"%WINDIR%\SysWOW64\Tasks\*", @"%WINDIR%\SysWOW64\com\dmp\*", @"%WINDIR%\SysWOW64\FxsTmp\*",
  };

  /// <summary>תיקיות מערכת מחוץ ל-Windows שתמיד מותרות (אבטחת Windows, מקלדת מגע, WebView2)</summary>
  public static readonly string[] BaselineAllowed =
  {
    @"%PROGRAMFILES%\Windows Defender\*", @"%PROGRAMFILES%\Windows Security\*",
    @"%PROGRAMFILES%\Common Files\microsoft shared\ink\*", @"%PROGRAMFILES%\Windows Photo Viewer\*",
    @"%OSDRIVE%\ProgramData\Microsoft\Windows Defender\Platform\*",
  };

  static string Sys(string exe) => $@"%SYSTEM32%\{exe}";
  static string Wow(string exe) => $@"%WINDIR%\SysWOW64\{exe}";

  public static readonly DenyTool[] DenyTools =
  {
    new("powershell", "PowerShell", new[] { @"%SYSTEM32%\WindowsPowerShell\*", @"%WINDIR%\SysWOW64\WindowsPowerShell\*", Sys("powershell_ise.exe") }),
    new("cmd", "שורת הפקודה (cmd)", new[] { Sys("cmd.exe"), Wow("cmd.exe") }),
    new("regedit", "עורך הרישום ו-reg.exe", new[] { @"%WINDIR%\regedit.exe", Sys("reg.exe"), Wow("reg.exe"), Sys("regini.exe"), Wow("regedit.exe") }),
    new("scripts", "סקריפטים: wscript, cscript, mshta", new[] { Sys("wscript.exe"), Sys("cscript.exe"), Sys("mshta.exe"), Wow("wscript.exe"), Wow("cscript.exe"), Wow("mshta.exe") }),
    new("mmc", "כלי ניהול: mmc, msconfig, compmgmt", new[] { Sys("mmc.exe"), Wow("mmc.exe"), Sys("msconfig.exe"), Sys("gpedit.msc") }),
    new("taskmgr", "מנהל המשימות", new[] { Sys("taskmgr.exe"), Wow("taskmgr.exe") }),
    new("lolbins", "כלי עקיפה מוכרים: certutil, bitsadmin, wmic, msbuild, installutil, regsvr32, ftp", new[] {
        Sys("certutil.exe"), Sys("bitsadmin.exe"), Sys(@"wbem\wmic.exe"), Wow(@"wbem\wmic.exe"), @"%WINDIR%\Microsoft.NET\*",
        Sys("regsvr32.exe"), Wow("regsvr32.exe"), Sys("ftp.exe"), Sys("forfiles.exe"), Sys("curl.exe"), Wow("curl.exe"), Sys("tar.exe") }),
    new("control", "לוח הבקרה ו-MSC", new[] { Sys("control.exe"), Wow("control.exe") }, Recommended: false),
    new("installers", "כלי התקנה: msiexec, winget, wsreset", new[] { Sys("msiexec.exe"), Wow("msiexec.exe"), Sys("wsreset.exe") }, Recommended: false),
    new("remote", "שליטה מרחוק: mstsc, quickassist, msra", new[] { Sys("mstsc.exe"), Sys("quickassist.exe"), Sys("msra.exe") }),
    new("wsl", "WSL / bash", new[] { Sys("wsl.exe"), Sys("bash.exe"), Sys("wslconfig.exe") }),
  };

  // ----- הרחבת נתיבים (בשביל הוואצ'דוג) -----

  public static IEnumerable<string> ExpandPattern(string p)
  {
    var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    var s = p.Replace("%WINDIR%", win, StringComparison.OrdinalIgnoreCase)
             .Replace("%SYSTEM32%", Path.Combine(win, "System32"), StringComparison.OrdinalIgnoreCase)
             .Replace("%OSDRIVE%", Path.GetPathRoot(win)!.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    if (s.Contains("%PROGRAMFILES%", StringComparison.OrdinalIgnoreCase))
    {
      yield return s.Replace("%PROGRAMFILES%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase);
      yield return s.Replace("%PROGRAMFILES%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), StringComparison.OrdinalIgnoreCase);
    }
    else yield return s;
  }

  /// <summary>התאמת נתיב קובץ לתבנית: "X\*" = כל מה שמתחת ל-X, אחרת נתיב מדויק</summary>
  public static bool Match(string file, string expandedPattern)
  {
    if (expandedPattern.EndsWith("\\*"))
      return file.StartsWith(expandedPattern[..^1], StringComparison.OrdinalIgnoreCase);
    return file.Equals(expandedPattern, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>אוסף הכללים המורחב שהוואצ'דוג בודק</summary>
  public sealed class Compiled
  {
    public List<string> Allow = new();
    public List<string> WinExceptions = new();
    public List<string> Deny = new();
    public List<string> Packages = new();
    public List<string> PfRoots = new();
    public List<string> PfExceptions = new();
    public List<AllowedEntry> Publishers = new();
    public bool StrictPackaged;
    public bool AllowProgramFiles;
    public string WinDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
    public string WindowsApps = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps") + "\\";
    readonly Dictionary<string, (DateTime stamp, bool ok)> _sigCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>null = מותר; אחרת סיבת החסימה</summary>
    public string? Check(string path)
    {
      if (Deny.Any(d => Match(path, d))) return "כלי חסום";
      if (path.StartsWith(WinDir, StringComparison.OrdinalIgnoreCase))
        return WinExceptions.Any(e => Match(path, e)) ? "תיקייה ניתנת לכתיבה בתוך Windows" : null;
      if (Allow.Any(a => Match(path, a))) return null;
      if (Publishers.Count > 0 && PublisherOk(path)) return null;
      if (path.StartsWith(WindowsApps, StringComparison.OrdinalIgnoreCase))
      {
        if (!StrictPackaged) return null;
        var pkg = path[WindowsApps.Length..];
        return Packages.Any(p => pkg.StartsWith(p, StringComparison.OrdinalIgnoreCase)) ? null : "אפליקציית חנות שאינה ברשימה";
      }
      if (AllowProgramFiles && PfRoots.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
        return PfExceptions.Any(e => Match(path, e)) ? "תיקייה פתוחה לכתיבה / תוכנה לא רצויה ב-Program Files" : null;
      return "לא ברשימה הלבנה";
    }

    /// <summary>קובץ חתום בתוקף, על ידי אותו מפרסם ואותו מוצר כמו אחד ההיתרים לפי חתימה</summary>
    bool PublisherOk(string path)
    {
      DateTime stamp;
      try { stamp = File.GetLastWriteTimeUtc(path); } catch { return false; }
      if (_sigCache.TryGetValue(path, out var c) && c.stamp == stamp) return c.ok;
      bool ok = false;
      var subj = Signing.SignerSubject(path);
      if (subj != null)
      {
        var prod = Signing.ProductName(path);
        var match = Publishers.Any(p => p.Signer.Equals(subj, StringComparison.OrdinalIgnoreCase) &&
                                       (p.Product == "*" || p.Product.Equals(prod, StringComparison.OrdinalIgnoreCase)));
        ok = match && Signing.IsValid(path);
      }
      if (_sigCache.Count > 2000) _sigCache.Clear();
      _sigCache[path] = (stamp, ok);
      return ok;
    }
  }

  public static Compiled Compile(Config cfg)
  {
    var c = new Compiled { StrictPackaged = cfg.AppLockerStrictPackaged, Packages = cfg.AllowedPackages.ToList(), AllowProgramFiles = cfg.AllowProgramFiles };
    foreach (var b in BaselineAllowed) c.Allow.AddRange(ExpandPattern(b));
    foreach (var a in cfg.Allowed)
    {
      if (a.IsPublisher) c.Publishers.Add(a);
      else c.Allow.Add(a.IsFolder ? a.Path.TrimEnd('\\') + "\\*" : a.Path);
    }
    foreach (var e in WindowsWritableExceptions) c.WinExceptions.AddRange(ExpandPattern(e));
    foreach (var t in DenyTools.Where(t => cfg.DenyTools.Contains(t.Id)))
      foreach (var p in t.Paths) c.Deny.AddRange(ExpandPattern(p));
    if (cfg.AllowProgramFiles)
    {
      c.PfRoots.AddRange(ExpandPattern(@"%PROGRAMFILES%\").Select(r => r.TrimEnd('\\') + "\\"));
      foreach (var u in ProgramFilesScan.Unwanted) c.PfExceptions.AddRange(ExpandPattern(u));
      foreach (var w in cfg.PfWritable) c.PfExceptions.Add(w.TrimEnd('\\') + "\\*");
    }
    return c;
  }
}
