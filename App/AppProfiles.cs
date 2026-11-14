using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32;

namespace AAA;

/// <summary>פרופיל של תוכנה מאושרת: איך מוצאים את כל התיקיות שהיא צריכה</summary>
public record AppProfile(string Id, string Name, string[] Keywords, string[] Candidates, string[] Companions, string Notes);

static class AppProfiles
{
  static string PF => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
  static string PF86 => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

  public static readonly AppProfile[] All =
  {
    new("word", "Microsoft Word (Office)",
      new[] { "Microsoft Office", "Microsoft 365", "Office 16", "Word" },
      new[] { @"%PF%\Microsoft Office", @"%PF86%\Microsoft Office" },
      new[] { @"%PF%\Common Files\microsoft shared", @"%PF86%\Common Files\microsoft shared", @"%PF86%\Microsoft\EdgeWebView" },
      "Office כולל כמה קובצי הפעלה (WINWORD, OfficeClickToRun, תיבות שמירה). EdgeWebView נדרש לחלוניות של וורד. אם וורד מבקש תוכנה נוספת – היא תופיע ביומן החסימות במצב למידה."),
    new("otzaria", "אוצריא",
      new[] { "otzaria", "אוצריא" },
      new[] { @"%PF%\otzaria", @"%PF86%\otzaria", @"%PF%\Otzaria", @"%LOCALAPPDATA%\Programs\otzaria", @"C:\otzaria" },
      Array.Empty<string>(),
      "אם אוצריא מותקנת בתוך AppData של המשתמש – התיקייה ניתנת לכתיבה על ידו. מומלץ להתקין לכל המשתמשים (Program Files) או להשתמש ב\"נעל כתיבה\"."),
    new("otzar", "אוצר החכמה",
      new[] { "אוצר החכמה", "Otzar HaChochma", "OtzarHaChochma", "Otzar Hachochma", "otzar" },
      new[] { @"%PF%\Otzar*", @"%PF86%\Otzar*", @"C:\Otzar*", @"%PF86%\אוצר*", @"%PF%\אוצר*" },
      Array.Empty<string>(),
      "הקיוסק של אוצר החכמה מפעיל כמה קובצי EXE – לכן מתירים את כל התיקייה ולא קובץ בודד. אם חסר משהו – מצב למידה יראה בדיוק מה."),
    new("barilan", "שו\"ת בר אילן (פרויקט השו\"ת)",
      new[] { "בר אילן", "בר-אילן", "Bar Ilan", "Bar-Ilan", "Responsa", "השו\"ת" },
      new[] { @"%PF86%\Responsa*", @"%PF%\Responsa*", @"C:\Responsa*", @"%PF86%\Bar*Ilan*", @"%PF%\Bar*Ilan*", @"C:\BarIlan*" },
      Array.Empty<string>(),
      "גרסאות שונות מתקינות לנתיבים שונים – אם לא נמצא, הוסף את התיקייה ידנית."),
    new("pdf", "מציג PDF",
      new[] { "SumatraPDF", "Adobe Acrobat", "Acrobat Reader", "PDF-XChange", "Foxit" },
      new[] { @"%PF%\SumatraPDF", @"%LOCALAPPDATA%\SumatraPDF", @"%PF%\Adobe\Acrobat DC", @"%PF86%\Adobe\Acrobat Reader DC", @"%PF%\Tracker Software", @"%PF86%\Foxit Software" },
      Array.Empty<string>(),
      "כל מציג PDF שמותקן יימצא כאן. מציג נפרד אחר – הוסף ידנית."),
    new("photos", "תמונות",
      new[] { "Windows Photo Viewer" },
      new[] { @"%PF%\Windows Photo Viewer", @"%PF86%\Windows Photo Viewer" },
      Array.Empty<string>(),
      "אפליקציית \"תמונות\" של Windows היא אפליקציית חנות – מותרת דרך \"חבילות מותרות\" (Microsoft.Windows.Photos). מציג התמונות הישן רץ דרך rundll32 מתוך Windows ולכן עובד ממילא."),
    new("explorer", "סייר הקבצים",
      Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
      "הסייר נמצא בתוך תיקיית Windows ולכן מותר תמיד. ההגבלות עליו באות מלשונית המדיניות (לוח בקרה, Win+R, אפשרויות תיקיה)."),
  };

  public static string Expand(string p, string? userProfile = null)
  {
    p = p.Replace("%PF86%", PF86, StringComparison.OrdinalIgnoreCase).Replace("%PF%", PF, StringComparison.OrdinalIgnoreCase);
    if (userProfile != null)
      p = p.Replace("%LOCALAPPDATA%", Path.Combine(userProfile, @"AppData\Local"), StringComparison.OrdinalIgnoreCase);
    return Environment.ExpandEnvironmentVariables(p);
  }

  /// <summary>מחפש במחשב את התיקיות של התוכנה: רשומות ההתקנה, נתיבים ידועים ופרופילי המשתמשים</summary>
  public static List<string> Discover(AppProfile prof, IEnumerable<LocalUser> users)
  {
    var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // 1) רשומות "הוספה/הסרה של תוכניות" (מחשב + כל משתמש)
    var roots = new List<RegistryKey>();
    roots.Add(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64));
    roots.Add(RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32));
    foreach (var sid in Registry.Users.GetSubKeyNames().Where(s => s.StartsWith("S-1-5-21-") && !s.EndsWith("_Classes")))
    { var k = Registry.Users.OpenSubKey(sid); if (k != null) roots.Add(k); }
    foreach (var root in roots)
    {
      using (root)
      using (var un = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
      {
        if (un == null) continue;
        foreach (var sub in un.GetSubKeyNames())
        {
          try
          {
            using var k = un.OpenSubKey(sub);
            var name = k?.GetValue("DisplayName") as string;
            if (string.IsNullOrEmpty(name) || !prof.Keywords.Any(w => name.Contains(w, StringComparison.OrdinalIgnoreCase))) continue;
            // מניעת התאמות שגויות של "Word" (למשל WordPad / Wordweb)
            if (prof.Id == "word" && name.Contains("Word", StringComparison.OrdinalIgnoreCase) && !name.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)) continue;
            var loc = k!.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(loc)) AddDir(found, loc);
            var icon = k.GetValue("DisplayIcon") as string;
            if (!string.IsNullOrWhiteSpace(icon)) AddDir(found, Path.GetDirectoryName(CleanExePath(icon)) ?? "");
            var unst = k.GetValue("UninstallString") as string;
            if (!string.IsNullOrWhiteSpace(unst) && !unst.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
              AddDir(found, Path.GetDirectoryName(CleanExePath(unst)) ?? "");
          }
          catch { }
        }
      }
    }

    // 2) נתיבים ידועים
    var profiles = users.Where(u => u.HasProfile).Select(u => (string?)u.ProfilePath).Append(null);
    foreach (var c in prof.Candidates.Concat(prof.Companions))
      foreach (var up in profiles)
      {
        if (up == null && c.Contains("%LOCALAPPDATA%")) continue;
        if (up != null && !c.Contains("%LOCALAPPDATA%")) continue;
        foreach (var d in Glob(Expand(c, up))) AddDir(found, d);
      }

    // ניקוי: תיקיות שמכילות תיקייה אחרת ברשימה – נשארת רק העליונה; לא מתירים שורש כונן או תיקיות מערכת שלמות
    var list = found.Where(d => Directory.Exists(d) && !IsTooBroad(d)).Select(d => d.TrimEnd('\\')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    list = list.Where(d => !list.Any(o => !o.Equals(d, StringComparison.OrdinalIgnoreCase) && d.StartsWith(o + "\\", StringComparison.OrdinalIgnoreCase))).ToList();
    return list.OrderBy(x => x).ToList();
  }

  static void AddDir(HashSet<string> set, string d)
  {
    d = d.Trim().Trim('"').TrimEnd('\\');
    if (d.Length > 3) set.Add(d);
  }

  static string CleanExePath(string s)
  {
    s = s.Trim();
    if (s.StartsWith('"')) { int e = s.IndexOf('"', 1); return e > 0 ? s[1..e] : s.Trim('"'); }
    int exe = s.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
    if (exe > 0) return s[..(exe + 4)];
    int comma = s.LastIndexOf(',');
    return comma > 0 ? s[..comma] : s;
  }

  static IEnumerable<string> Glob(string pattern)
  {
    try
    {
      if (!pattern.Contains('*')) return Directory.Exists(pattern) ? new[] { pattern } : Array.Empty<string>();
      var parent = Path.GetDirectoryName(pattern)!;
      var leaf = Path.GetFileName(pattern);
      if (!Directory.Exists(parent)) return Array.Empty<string>();
      return Directory.GetDirectories(parent, leaf);
    }
    catch { return Array.Empty<string>(); }
  }

  public static bool IsTooBroad(string d)
  {
    d = d.TrimEnd('\\');
    if (d.Length <= 3) return true;
    string[] broad = { PF, PF86, Environment.GetFolderPath(Environment.SpecialFolder.Windows),
      Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"C:\Users", @"C:\Program Files\WindowsApps",
      Path.Combine(PF, "Common Files"), Path.Combine(PF86, "Common Files") };
    if (broad.Any(b => b.TrimEnd('\\').Equals(d, StringComparison.OrdinalIgnoreCase))) return true;
    // C:\Users\X או C:\Users\X\AppData\Local וכו'
    var parts = d.Split('\\');
    if (parts.Length <= 3 && d.StartsWith(@"C:\Users\", StringComparison.OrdinalIgnoreCase)) return true;
    if (parts.Length <= 5 && d.Contains(@"\AppData\", StringComparison.OrdinalIgnoreCase) && !d.Contains(@"\Programs\", StringComparison.OrdinalIgnoreCase)) return true;
    return false;
  }
}

/// <summary>בדיקת הרשאות: האם משתמש היעד יכול לכתוב לתיקייה מותרת (פרצה: מעתיק לשם כל EXE ומריץ)</summary>
static class FolderAcl
{
  const FileSystemRights WriteMask = FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.CreateDirectories |
    FileSystemRights.Write | FileSystemRights.Modify | FileSystemRights.FullControl | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

  /// <summary>מחזיר true אם אחת הזהויות (המשתמש, Users, Everyone, Authenticated Users, INTERACTIVE) מקבלת הרשאת כתיבה</summary>
  public static bool UserCanWrite(string path, IEnumerable<string> targetSids, string? ownerProfile = null)
  {
    try
    {
      var dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
      if (dir == null || !Directory.Exists(dir)) return false;
      var sids = new HashSet<string>(targetSids, StringComparer.OrdinalIgnoreCase)
      {
        "S-1-5-32-545", "S-1-1-0", "S-1-5-11", "S-1-5-4", "S-1-3-0" /* CREATOR OWNER – נבדק דרך הבעלים */
      };
      var sec = new DirectoryInfo(dir).GetAccessControl();
      var rules = sec.GetAccessRules(true, true, typeof(SecurityIdentifier));
      bool allow = false, deny = false;
      foreach (FileSystemAccessRule r in rules)
      {
        if (!sids.Contains(r.IdentityReference.Value)) continue;
        if ((r.FileSystemRights & WriteMask) == 0) continue;
        if (r.AccessControlType == AccessControlType.Deny && r.IdentityReference.Value != "S-1-3-0") deny = true;
        else if (r.AccessControlType == AccessControlType.Allow && r.IdentityReference.Value != "S-1-3-0") allow = true;
      }
      // תיקייה שבבעלות המשתמש עצמו
      var owner = sec.GetOwner(typeof(SecurityIdentifier))?.Value;
      if (owner != null && targetSids.Contains(owner, StringComparer.OrdinalIgnoreCase)) allow = true;
      return allow && !deny;
    }
    catch { return false; }
  }

  /// <summary>מוסיף כלל "דחה כתיבה" לחשבונות היעד על התיקייה (בירושה). קריאה והרצה נשארות.</summary>
  public static string LockWrite(string path, IEnumerable<string> targetSids)
  {
    try
    {
      var di = new DirectoryInfo(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);
      var sec = di.GetAccessControl();
      var rights = FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.WriteExtendedAttributes |
                   FileSystemRights.WriteAttributes | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles |
                   FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
      foreach (var sid in targetSids)
        sec.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), rights,
          InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Deny));
      di.SetAccessControl(sec);
      Log.Write("LockWrite " + di.FullName);
      return "התיקייה ננעלה לכתיבה עבור חשבונות היעד.";
    }
    catch (Exception ex) { return "הנעילה נכשלה: " + ex.Message; }
  }

  public static string UnlockWrite(string path, IEnumerable<string> targetSids)
  {
    try
    {
      var di = new DirectoryInfo(Directory.Exists(path) ? path : Path.GetDirectoryName(path)!);
      var sec = di.GetAccessControl();
      var set = new HashSet<string>(targetSids, StringComparer.OrdinalIgnoreCase);
      foreach (FileSystemAccessRule r in sec.GetAccessRules(true, false, typeof(SecurityIdentifier)))
        if (r.AccessControlType == AccessControlType.Deny && set.Contains(r.IdentityReference.Value))
          sec.RemoveAccessRuleSpecific(r);
      di.SetAccessControl(sec);
      return "הנעילה הוסרה.";
    }
    catch (Exception ex) { return "ההסרה נכשלה: " + ex.Message; }
  }
}
