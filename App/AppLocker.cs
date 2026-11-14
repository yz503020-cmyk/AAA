using System.Diagnostics.Eventing.Reader;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace AAA;

/// <summary>בניית מדיניות AppLocker לפי נתיב והחלתה כמדיניות מקומית</summary>
static class AppLocker
{
  const string Everyone = "S-1-1-0";
  const string Admins = "S-1-5-32-544";

  static string X(string s) => SecurityElement.Escape(s) ?? s;

  static string PathRule(string name, string sid, string action, string path, IEnumerable<string>? exceptions = null)
  {
    var sb = new StringBuilder();
    sb.Append($"<FilePathRule Id=\"{Guid.NewGuid()}\" Name=\"{X(name)}\" Description=\"AAA\" UserOrGroupSid=\"{sid}\" Action=\"{action}\">");
    sb.Append($"<Conditions><FilePathCondition Path=\"{X(path)}\" /></Conditions>");
    var ex = exceptions?.ToList();
    if (ex is { Count: > 0 })
    {
      sb.Append("<Exceptions>");
      foreach (var e in ex) sb.Append($"<FilePathCondition Path=\"{X(e)}\" />");
      sb.Append("</Exceptions>");
    }
    sb.Append("</FilePathRule>");
    return sb.ToString();
  }

  static string PublisherRule(string name, string sid, string publisher, string product)
    => $"<FilePublisherRule Id=\"{Guid.NewGuid()}\" Name=\"{X(name)}\" Description=\"AAA\" UserOrGroupSid=\"{sid}\" Action=\"Allow\">" +
       $"<Conditions><FilePublisherCondition PublisherName=\"{X(publisher)}\" ProductName=\"{X(product)}\" BinaryName=\"*\">" +
       "<BinaryVersionRange LowSection=\"0.0.0.0\" HighSection=\"*\" /></FilePublisherCondition></Conditions></FilePublisherRule>";

  public static string BuildXml(Config cfg)
  {
    string mode = cfg.AppLockerAuditOnly ? "AuditOnly" : "Enabled";
    var allowPaths = Whitelist.BaselineAllowed
      .Concat(cfg.Allowed.Select(a => a.IsFolder ? a.Path.TrimEnd('\\') + "\\*" : a.Path))
      .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    var targets = cfg.TargetSids.ToList();
    var deny = Whitelist.DenyTools.Where(t => cfg.DenyTools.Contains(t.Id)).SelectMany(t => t.Paths).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    var sb = new StringBuilder();
    sb.Append("<AppLockerPolicy Version=\"1\">");

    // ---- קובצי הפעלה ----
    sb.Append($"<RuleCollection Type=\"Exe\" EnforcementMode=\"{mode}\">");
    sb.Append(PathRule("AAA: מנהלים – הכל", Admins, "Allow", "*"));
    sb.Append(PathRule("AAA: תיקיית Windows", Everyone, "Allow", "%WINDIR%\\*", Whitelist.WindowsWritableExceptions));
    foreach (var p in allowPaths) sb.Append(PathRule("AAA: " + p, Everyone, "Allow", p));
    foreach (var sid in targets)
      foreach (var p in deny) sb.Append(PathRule("AAA חסום: " + p, sid, "Deny", p));
    sb.Append("</RuleCollection>");

    // ---- קובצי התקנה (MSI) – רק מנהלים ----
    sb.Append($"<RuleCollection Type=\"Msi\" EnforcementMode=\"{mode}\">");
    sb.Append(PathRule("AAA: מנהלים – MSI", Admins, "Allow", "*"));
    sb.Append(PathRule("AAA: Windows Installer", Everyone, "Allow", "%WINDIR%\\Installer\\*"));
    sb.Append("</RuleCollection>");

    // ---- סקריפטים (ps1, bat, cmd, vbs, js) ----
    sb.Append($"<RuleCollection Type=\"Script\" EnforcementMode=\"{mode}\">");
    sb.Append(PathRule("AAA: מנהלים – סקריפטים", Admins, "Allow", "*"));
    sb.Append(PathRule("AAA: סקריפטים של Windows", Everyone, "Allow", "%WINDIR%\\*", Whitelist.WindowsWritableExceptions));
    foreach (var p in allowPaths) sb.Append(PathRule("AAA: " + p, Everyone, "Allow", p));
    sb.Append("</RuleCollection>");

    // ---- אפליקציות חנות ----
    sb.Append($"<RuleCollection Type=\"Appx\" EnforcementMode=\"{mode}\">");
    if (!cfg.AppLockerStrictPackaged)
      sb.Append(PublisherRule("AAA: כל האפליקציות החתומות", Everyone, "*", "*"));
    else
    {
      sb.Append(PublisherRule("AAA: מנהלים – כל האפליקציות", Admins, "*", "*"));
      sb.Append(PublisherRule("AAA: רכיבי Windows", Everyone, "CN=Microsoft Windows, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", "*"));
      foreach (var pkg in cfg.AllowedPackages)
        sb.Append(PublisherRule("AAA: " + pkg, Everyone, "CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", pkg.TrimEnd('_')));
    }
    sb.Append("</RuleCollection>");

    sb.Append("</AppLockerPolicy>");
    return sb.ToString();
  }

  public static (bool ok, string msg) Apply(Config cfg)
  {
    AppPaths.EnsureRoot();
    File.WriteAllText(AppPaths.AppLockerXml, BuildXml(cfg), new UTF8Encoding(false));
    var r = Shell.PowerShell($"Import-Module AppLocker; Set-AppLockerPolicy -XmlPolicy '{AppPaths.AppLockerXml.Replace("'", "''")}' -ErrorAction Stop; 'OK'");
    if (!r.Output.Contains("OK")) return (false, "החלת AppLocker נכשלה:\n" + r.Output.Trim());
    EnsureService();
    cfg.AppLockerApplied = true;
    return (true, cfg.AppLockerAuditOnly
      ? "AppLocker הוחל במצב בדיקה בלבד: שום דבר לא נחסם, וכל מה שהיה נחסם נרשם ביומן."
      : "AppLocker הוחל במצב אכיפה.");
  }

  public static (bool ok, string msg) Clear(Config cfg)
  {
    AppPaths.EnsureRoot();
    var empty = Path.Combine(AppPaths.Root, "applocker-empty.xml");
    File.WriteAllText(empty, "<AppLockerPolicy Version=\"1\" />", new UTF8Encoding(false));
    var r = Shell.PowerShell($"Import-Module AppLocker; Set-AppLockerPolicy -XmlPolicy '{empty.Replace("'", "''")}' -ErrorAction Stop; 'OK'");
    if (!r.Output.Contains("OK")) return (false, "ניקוי AppLocker נכשל:\n" + r.Output.Trim());
    cfg.AppLockerApplied = false;
    return (true, "כללי AppLocker הוסרו.");
  }

  /// <summary>שירות זיהוי היישומים (AppIDSvc) חייב לרוץ כדי שהכללים ייאכפו</summary>
  static void EnsureService()
  {
    try
    {
      using var k = RegOps.Hklm().OpenSubKey(@"SYSTEM\CurrentControlSet\Services\AppIDSvc", true);
      k?.SetValue("Start", 2, RegistryValueKind.DWord);
    }
    catch (Exception ex) { Log.Write("AppIDSvc start type: " + ex.Message); }
    Shell.Exe("sc.exe", "start AppIDSvc");
    Shell.Exe("appidtel.exe", "start");
  }

  public static string ServiceState()
  {
    var r = Shell.Exe("sc.exe", "query AppIDSvc");
    return r.Output.Contains("RUNNING") ? "רץ" : r.Output.Contains("STOPPED") ? "עצור" : "לא ידוע";
  }

  public static string Edition()
  {
    using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
    var name = k?.GetValue("ProductName") as string ?? "";
    var ed = k?.GetValue("EditionID") as string ?? "";
    var build = k?.GetValue("CurrentBuild") as string ?? "";
    if (int.TryParse(build, out int b) && b >= 22000) name = name.Replace("Windows 10", "Windows 11");
    return $"{name} ({ed}, build {build})";
  }

  public static bool IsHome()
  {
    using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
    return (k?.GetValue("EditionID") as string ?? "").StartsWith("Core", StringComparison.OrdinalIgnoreCase);
  }

  public record Hit(DateTime Time, string User, string Path, bool WouldBlock);

  /// <summary>אירועי AppLocker אחרונים: 8003 (היה נחסם – מצב בדיקה), 8004 (נחסם), 8006/8007 (סקריפטים/MSI)</summary>
  public static List<Hit> RecentEvents(int max = 300)
  {
    var list = new List<Hit>();
    foreach (var logName in new[] { "Microsoft-Windows-AppLocker/EXE and DLL", "Microsoft-Windows-AppLocker/MSI and Script" })
    {
      try
      {
        var q = new EventLogQuery(logName, PathType.LogName, "*[System[(EventID=8003 or EventID=8004 or EventID=8006 or EventID=8007)]]") { ReverseDirection = true };
        using var rd = new EventLogReader(q);
        for (int i = 0; i < max; i++)
        {
          using var e = rd.ReadEvent();
          if (e == null) break;
          string path = "";
          try
          {
            // FilePath הוא אחד המאפיינים; מחפשים ערך שנראה כמו נתיב
            foreach (var p in e.Properties)
              if (p.Value is string s && (s.Contains('\\') || s.StartsWith('%')) && s.Length > path.Length) path = s;
          }
          catch { }
          string user = "";
          try { user = e.UserId != null ? (Users.NameOf(e.UserId.Value) ?? e.UserId.Value) : ""; } catch { }
          list.Add(new Hit(e.TimeCreated ?? DateTime.MinValue, user, ExpandAppLockerPath(path), e.Id == 8003 || e.Id == 8006));
        }
      }
      catch (Exception ex) { Log.Write($"AppLocker events {logName}: {ex.Message}"); }
    }
    return list.OrderByDescending(h => h.Time).ToList();
  }

  static string ExpandAppLockerPath(string p)
  {
    var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    return p.Replace("%OSDRIVE%", Path.GetPathRoot(win)!.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
            .Replace("%WINDIR%", win, StringComparison.OrdinalIgnoreCase)
            .Replace("%SYSTEM32%", Path.Combine(win, "System32"), StringComparison.OrdinalIgnoreCase)
            .Replace("%PROGRAMFILES%", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), StringComparison.OrdinalIgnoreCase);
  }
}
