using Microsoft.Win32;

namespace AAA;

public enum Scope { Machine, User }

public record RegOp(Scope Scope, string Key, string Name, object Value, RegistryValueKind Kind = RegistryValueKind.DWord);

/// <summary>הגדרת חסימה אחת (מתג בממשק)</summary>
public record PolicyDef(string Id, string Group, string Title, string Desc, RegOp[] Ops, bool Recommended = true, string? Warning = null)
{
  public bool UserScoped => Ops.Any(o => o.Scope == Scope.User);
}

static class PolicyCatalog
{
  const string ExplorerPol = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
  const string SystemPol = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
  const string WinSys = @"Software\Policies\Microsoft\Windows\System";
  const string Installer = @"Software\Policies\Microsoft\Windows\Installer";
  const string NetConn = @"Software\Policies\Microsoft\Windows\Network Connections";

  static RegOp U(string key, string name, int v) => new(Scope.User, key, name, v);
  static RegOp M(string key, string name, int v) => new(Scope.Machine, key, name, v);
  static RegOp Ms(string key, string name, string v) => new(Scope.Machine, key, name, v, RegistryValueKind.String);

  public static readonly PolicyDef[] All =
  {
    // ---------- כלי מערכת ----------
    new("no_control_panel", "כלי מערכת", "חסימת לוח הבקרה והגדרות Windows",
      "Prohibit access to Control Panel and PC settings (NoControlPanel). חל רק על חשבון היעד.",
      new[] { U(ExplorerPol, "NoControlPanel", 1) }),
    new("no_task_mgr", "כלי מערכת", "חסימת מנהל המשימות",
      "Remove Task Manager (DisableTaskMgr). גם תפריט Ctrl+Alt+Del לא יציג אותו.",
      new[] { U(SystemPol, "DisableTaskMgr", 1) }),
    new("no_regedit", "כלי מערכת", "חסימת עורך הרישום",
      "Prevent access to registry editing tools (DisableRegistryTools=2: גם regedit וגם ייבוא קובצי REG בשקט).",
      new[] { U(SystemPol, "DisableRegistryTools", 2) }),
    new("no_cmd", "כלי מערכת", "חסימת שורת הפקודה (CMD) וקובצי BAT",
      "Prevent access to the command prompt (DisableCMD=1: חוסם גם הרצת קובצי .bat/.cmd).",
      new[] { U(@"Software\Policies\Microsoft\Windows\System", "DisableCMD", 1) }),
    new("no_run", "כלי מערכת", "הסרת חלון \"הפעלה\" (Win+R)",
      "Remove Run menu from Start Menu (NoRun).",
      new[] { U(ExplorerPol, "NoRun", 1) }),
    new("no_mmc", "כלי מערכת", "חסימת כל רכיבי MMC",
      "Restrict users to the explicitly permitted list of snap-ins – בלי שום רכיב מותר: gpedit, ניהול מחשב, משתמשים וקבוצות, ניהול דיסקים, מנהל ההתקנים, מציג האירועים.",
      new[] { U(@"Software\Policies\Microsoft\MMC", "RestrictToPermittedSnapins", 1) }),
    new("no_folder_options", "כלי מערכת", "הסתרת \"אפשרויות תיקיה\" בסייר",
      "NoFolderOptions – המשתמש לא יוכל להציג קבצים מוסתרים / סיומות דרך החלון.",
      new[] { U(ExplorerPol, "NoFolderOptions", 1) }, Recommended: false),
    new("no_sudo", "כלי מערכת", "ביטול sudo של Windows 11",
      "Configure the behavior of the sudo command → מושבת.",
      new[] { M(@"Software\Policies\Microsoft\Windows\Sudo", "EnableSudo", 0) }),
    new("no_wsh", "כלי מערכת", "ביטול Windows Script Host (VBS/JS)",
      "לא GPO – ערך רישום: Windows Script Host\\Settings\\Enabled=0. חל על כל המחשב (גם על המנהל).",
      new[] { M(@"Software\Microsoft\Windows Script Host\Settings", "Enabled", 0),
              M(@"Software\WOW6432Node\Microsoft\Windows Script Host\Settings", "Enabled", 0) }),

    // ---------- התקנות ----------
    new("no_user_installs", "התקנות", "חסימת התקנות MSI למשתמש",
      "Prohibit User Installs (DisableUserInstalls=1).",
      new[] { M(Installer, "DisableUserInstalls", 1) }),
    new("no_elevated_installs", "התקנות", "סגירת הפרצה AlwaysInstallElevated",
      "Always install with elevated privileges = 0 (במחשב ובמשתמש). אם זה פעיל – כל משתמש מתקין MSI כ-SYSTEM.",
      new[] { M(Installer, "AlwaysInstallElevated", 0), U(Installer, "AlwaysInstallElevated", 0) }),
    new("no_msi_at_all", "התקנות", "חסימת Windows Installer לגמרי",
      "DisableMSI=2. חוסם התקנת MSI גם לך כמנהל! לכבות לפני התקנת תוכנה ולהפעיל אחר כך.",
      new[] { M(Installer, "DisableMSI", 2) }, Recommended: false,
      Warning: "חל גם על המנהל – לכבות לפני התקנות ועדכוני Office"),
    new("no_store", "התקנות", "חסימת החנות (Microsoft Store)",
      "Turn off the Store application (RemoveWindowsStore=1).",
      new[] { M(@"Software\Policies\Microsoft\WindowsStore", "RemoveWindowsStore", 1) }),
    new("no_winget", "התקנות", "חסימת App Installer ו-winget",
      "EnableAppInstaller=0, EnableMSAppInstallerProtocol=0, EnableWindowsPackageManagerCommandLineInterfaces=0.",
      new[] { M(@"Software\Policies\Microsoft\Windows\AppInstaller", "EnableAppInstaller", 0),
              M(@"Software\Policies\Microsoft\Windows\AppInstaller", "EnableMSAppInstallerProtocol", 0),
              M(@"Software\Policies\Microsoft\Windows\AppInstaller", "EnableWindowsPackageManagerCommandLineInterfaces", 0) }),
    new("no_sideload", "התקנות", "חסימת התקנת אפליקציות מחוץ לחנות ומצב מפתחים",
      "AllowAllTrustedApps=0, AllowDevelopmentWithoutDevLicense=0.",
      new[] { M(@"Software\Policies\Microsoft\Windows\Appx", "AllowAllTrustedApps", 0),
              M(@"Software\Policies\Microsoft\Windows\Appx", "AllowDevelopmentWithoutDevLicense", 0) }),

    // ---------- חשבונות והרשאות ----------
    new("uac_auto_deny", "חשבונות והרשאות", "דחייה אוטומטית של בקשות הרשאת מנהל",
      "ConsentPromptBehaviorUser=0 – משתמש רגיל לא מקבל בכלל חלון להקלדת סיסמת מנהל.",
      new[] { M(SystemPol, "ConsentPromptBehaviorUser", 0) }),
    new("no_ms_accounts", "חשבונות והרשאות", "חסימת הוספת חשבונות Microsoft",
      "Block Microsoft accounts (NoConnectedUser=3).",
      new[] { M(SystemPol, "NoConnectedUser", 3) }),
    new("hide_fus", "חשבונות והרשאות", "הסתרת מעבר מהיר בין משתמשים",
      "Hide entry points for Fast User Switching.",
      new[] { M(SystemPol, "HideFastUserSwitching", 1) }),
    new("no_sec_questions", "חשבונות והרשאות", "ביטול שאלות אבטחה לאיפוס סיסמה",
      "Prevent the use of security questions for local accounts.",
      new[] { M(WinSys, "NoLocalPasswordResetQuestions", 1) }),
    new("no_enum_admins", "חשבונות והרשאות", "אל תציג את רשימת המנהלים בבקשת הרשאה",
      "Enumerate administrator accounts on elevation = מושבת.",
      new[] { M(@"Software\Microsoft\Windows\CurrentVersion\Policies\CredUI", "EnumerateAdministrators", 0) }),
    new("safemode_block", "חשבונות והרשאות", "חסימת מצב בטוח למי שאינו מנהל",
      "SafeModeBlockNonAdmins=1 (לא מופיע בעורך המדיניות). משתמש רגיל שנכנס למצב בטוח – מנותק מיד.",
      new[] { M(SystemPol, "SafeModeBlockNonAdmins", 1) }),

    // ---------- רשת ----------
    new("net_props", "רשת", "חסימת שינוי הגדרות רשת (IP / DNS)",
      "NC_LanProperties, NC_LanChangeProperties, NC_RasChangeProperties, NC_EnableAdminProhibits.",
      new[] { U(NetConn, "NC_LanProperties", 0), U(NetConn, "NC_LanChangeProperties", 0),
              U(NetConn, "NC_RasChangeProperties", 0), U(NetConn, "NC_AllowAdvancedTCPIPConfig", 0),
              M(NetConn, "NC_EnableAdminProhibits", 1) }),
    new("proxy_lock", "רשת", "נעילת הגדרות פרוקסי",
      "Prevent changing proxy settings.",
      new[] { U(@"Software\Policies\Microsoft\Internet Explorer\Control Panel", "Proxy", 1) }),
    new("no_rdp", "רשת", "ביטול שולחן עבודה מרוחק וסיוע מרחוק",
      "fDenyTSConnections=1, fAllowToGetHelp=0. מונע השתלטות מרחוק על המחשב.",
      new[] { M(@"Software\Policies\Microsoft\Windows NT\Terminal Services", "fDenyTSConnections", 1),
              M(@"Software\Policies\Microsoft\Windows NT\Terminal Services", "fAllowToGetHelp", 0) }),

    // ---------- התקנים ----------
    new("usb_deny", "התקנים", "חסימת התקני אחסון נשלפים (USB, כרטיסים, טלפון)",
      "All Removable Storage classes: Deny all access – למשתמש היעד בלבד.",
      new[] { U(@"Software\Policies\Microsoft\Windows\RemovableStorageDevices", "Deny_All", 1) }),
    new("no_autorun", "התקנים", "ביטול AutoRun / AutoPlay",
      "NoDriveTypeAutoRun=255, NoAutorun=1, NoAutoplayfornonVolume=1.",
      new[] { M(ExplorerPol, "NoDriveTypeAutoRun", 255), M(ExplorerPol, "NoAutorun", 1),
              M(@"Software\Policies\Microsoft\Windows\Explorer", "NoAutoplayfornonVolume", 1) }),

    // ---------- עדכונים ----------
    new("wu_ui", "עדכונים", "הסתרת Windows Update מהמשתמש",
      "Remove access to use all Windows Update features (SetDisableUXWUAccess). העדכונים ממשיכים לרוץ ברקע.",
      new[] { M(@"Software\Policies\Microsoft\Windows\WindowsUpdate", "SetDisableUXWUAccess", 1) }, Recommended: false),
  };

  public static IEnumerable<string> Groups => All.Select(p => p.Group).Distinct();
}

/// <summary>מחיל את המדיניות לפי ההגדרות, ומחזיר כל ערך שכבר אינו מסומן למצבו המקורי</summary>
static class PolicyEngine
{
  public static List<string> Apply(Config cfg, IReadOnlyList<LocalUser> users)
  {
    var msgs = new List<string>();
    var enabled = PolicyCatalog.All.Where(p => cfg.Policies.Contains(p.Id)).ToList();

    // ----- מחשב -----
    using (var hklm = RegOps.Hklm())
    {
      var desired = new Dictionary<string, RegOp>(StringComparer.OrdinalIgnoreCase);
      foreach (var op in enabled.SelectMany(p => p.Ops).Where(o => o.Scope == Scope.Machine))
        desired[op.Key + "|" + op.Name] = op;
      foreach (var op in desired.Values)
      {
        try { RegOps.SetWithBackup(cfg, "HKLM", hklm, op.Key, op.Name, op.Value, op.Kind); }
        catch (Exception ex) { msgs.Add($"שגיאה ב-{op.Name}: {ex.Message}"); }
      }
      foreach (var bk in cfg.Backups.Keys.Where(k => k.StartsWith("HKLM|")).ToList())
      {
        var parts = bk.Split('|');
        if (!desired.ContainsKey(parts[1] + "|" + parts[2])) RegOps.Restore(cfg, "HKLM", hklm, parts[1], parts[2]);
      }
    }

    // ----- משתמשים -----
    var userDesired = new Dictionary<string, RegOp>(StringComparer.OrdinalIgnoreCase);
    foreach (var op in enabled.SelectMany(p => p.Ops).Where(o => o.Scope == Scope.User))
      userDesired[op.Key + "|" + op.Name] = op;

    var sids = new HashSet<string>(cfg.TargetSids, StringComparer.OrdinalIgnoreCase);
    foreach (var bk in cfg.Backups.Keys) { var t = bk.Split('|')[0]; if (t.StartsWith("S-1-")) sids.Add(t); }

    foreach (var sid in sids)
    {
      var u = users.FirstOrDefault(x => x.Sid.Equals(sid, StringComparison.OrdinalIgnoreCase));
      bool isTarget = cfg.TargetSids.Contains(sid, StringComparer.OrdinalIgnoreCase) && u != null;
      using var hive = UserHive.Open(sid, u?.ProfilePath ?? "");
      if (hive == null)
      {
        if (isTarget) msgs.Add($"לחשבון \"{u?.Name ?? sid}\" עוד אין פרופיל. צריך להתחבר אליו פעם אחת, ואז ללחוץ שוב \"החל שינויים\".");
        continue;
      }
      if (isTarget)
        foreach (var op in userDesired.Values)
        {
          try { RegOps.SetWithBackup(cfg, sid, hive.Root, op.Key, op.Name, op.Value, op.Kind); }
          catch (Exception ex) { msgs.Add($"שגיאה ב-{op.Name} ({u?.Name}): {ex.Message}"); }
        }
      foreach (var bk in cfg.Backups.Keys.Where(k => k.StartsWith(sid + "|", StringComparison.OrdinalIgnoreCase)).ToList())
      {
        var parts = bk.Split('|');
        if (!isTarget || !userDesired.ContainsKey(parts[1] + "|" + parts[2])) RegOps.Restore(cfg, sid, hive.Root, parts[1], parts[2]);
      }
    }
    if (enabled.Any(p => p.UserScoped)) msgs.Add("חסימות של חשבון היעד נכנסות לתוקף אחרי שהוא מתנתק ומתחבר מחדש.");
    return msgs;
  }

  /// <summary>מצב בפועל של מדיניות (לתצוגה): כל הערכים שלה קיימים במחשב</summary>
  public static bool IsMachineActive(PolicyDef p)
  {
    using var hklm = RegOps.Hklm();
    return p.Ops.Where(o => o.Scope == Scope.Machine).All(o => Equals(RegOps.Get(hklm, o.Key, o.Name)?.ToString(), o.Value.ToString()));
  }
}
