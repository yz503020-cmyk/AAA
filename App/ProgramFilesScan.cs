using System.Security.AccessControl;
using System.Security.Principal;

namespace AAA;

/// <summary>
/// מצב "רגיל" (כל Program Files מותר): מוצא תיקיות בתוך Program Files שמשתמש רגיל יכול לכתוב אליהן –
/// אחרת הוא מעתיק לשם כל EXE ומריץ. תיקייה כזו (עם כל מה שמתחתיה) מוחרגת מההיתר.
/// </summary>
static class ProgramFilesScan
{
  /// <summary>תוכנות לא רצויות בתוך Program Files – לא מותרות גם במצב רגיל (נתיבים בתחביר AppLocker)</summary>
  public static readonly string[] Unwanted =
  {
    @"%PROGRAMFILES%\Microsoft\Edge\*",
    @"%PROGRAMFILES%\Microsoft\EdgeCore\*",
    @"%PROGRAMFILES%\Microsoft\EdgeUpdate\*",
    @"%PROGRAMFILES%\Windows Media Player\*",
    @"%PROGRAMFILES%\Internet Explorer\*",
    @"%PROGRAMFILES%\Windows Mail\*",
    @"%PROGRAMFILES%\WindowsApps\*",
    @"%PROGRAMFILES%\WindowsPowerShell\*",
    @"%PROGRAMFILES%\PowerShell\*",
    @"%PROGRAMFILES%\Git\*",
  };

  static readonly string[] BroadSids = { "S-1-5-32-545", "S-1-1-0", "S-1-5-11", "S-1-5-4" };

  const FileSystemRights WriteMask = FileSystemRights.WriteData | FileSystemRights.AppendData | FileSystemRights.CreateDirectories |
    FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

  public static List<string> FindWritable(IEnumerable<string> targetSids)
  {
    var sids = new HashSet<string>(BroadSids.Concat(targetSids), StringComparer.OrdinalIgnoreCase);
    var result = new List<string>();
    var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) }
      .Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    var wa = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps");
    var stack = new Stack<(string dir, int depth)>();
    foreach (var r in roots) stack.Push((r, 0));
    int count = 0;
    while (stack.Count > 0 && count < 200_000)
    {
      var (dir, depth) = stack.Pop();
      count++;
      if (dir.Equals(wa, StringComparison.OrdinalIgnoreCase)) continue;
      if (depth > 0 && Writable(dir, sids)) { result.Add(dir); continue; } // כל מה שמתחתיה מוחרג ממילא
      if (depth >= 12) continue;
      try
      {
        foreach (var sub in Directory.EnumerateDirectories(dir))
        {
          try { if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
          stack.Push((sub, depth + 1));
        }
      }
      catch { }
    }
    Log.Write($"PF scan: {count} folders, {result.Count} writable");
    return result.OrderBy(x => x).ToList();
  }

  static bool Writable(string dir, HashSet<string> sids)
  {
    try
    {
      var rules = new DirectoryInfo(dir).GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier));
      bool allow = false, deny = false;
      foreach (FileSystemAccessRule r in rules)
      {
        if (!sids.Contains(r.IdentityReference.Value) || (r.FileSystemRights & WriteMask) == 0) continue;
        // כלל שחל רק על תתי-תיקיות/קבצים ולא על התיקייה עצמה – עדיין מאפשר כתיבה בפנים
        if (r.AccessControlType == AccessControlType.Deny) deny = true; else allow = true;
      }
      return allow && !deny;
    }
    catch { return false; }
  }
}
