using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace AAA;

/// <summary>
/// התקנה עצמית ל-C:\Program Files\AAA – התיקייה שמותרת תמיד ב-AppLocker וב-Watchdog,
/// כך שהתוכנה לא תחסום את עצמה גם כשהמנהל שמפעיל אותה נבחר כחשבון יעד.
/// </summary>
static class Install
{
  public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AAA");
  public static string Exe => Path.Combine(Dir, "AAA.exe");

  public static bool IsRunningInstalled() =>
    string.Equals(Path.GetFullPath(Environment.ProcessPath ?? ""), Path.GetFullPath(Exe), StringComparison.OrdinalIgnoreCase);

  /// <summary>מעתיק את עצמו לתיקיית ההתקנה ומפעיל משם. מחזיר true אם צריך לצאת (העותק המותקן הופעל)</summary>
  public static bool EnsureInstalledAndRelaunch(string[] args)
  {
    if (IsRunningInstalled() || args.Contains("--no-install")) return false;
    try
    {
      CopySelfTo(Exe);
      Shortcuts();
      Process.Start(new ProcessStartInfo(Exe) { UseShellExecute = true, Arguments = string.Join(" ", args.Select(a => "\"" + a + "\"")) });
      return true;
    }
    catch (Exception ex)
    {
      Log.Write("Self-install failed: " + ex.Message);
      // אם העותק המותקן פתוח כרגע (הקובץ נעול) – פשוט מפעילים אותו
      if (File.Exists(Exe))
      {
        try { Process.Start(new ProcessStartInfo(Exe) { UseShellExecute = true }); return true; } catch { }
      }
      return false;
    }
  }

  /// <summary>מחליף את הקובץ המותקן בקובץ אחר (גם כשהקובץ המותקן רץ כרגע – משנים את שמו קודם)</summary>
  public static void CopySelfTo(string target, string? source = null)
  {
    source ??= Environment.ProcessPath!;
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    if (File.Exists(target))
    {
      var fs = new FileInfo(source); var ft = new FileInfo(target);
      if (fs.Length == ft.Length && fs.LastWriteTimeUtc == ft.LastWriteTimeUtc) return;
      var old = target + ".old";
      try { File.Delete(old); } catch { }
      try { File.Move(target, old); } catch { }
    }
    File.Copy(source, target, true);
    File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(source));
    Log.Write("Installed to " + target);
  }

  /// <summary>עדכון גרסה מתוך התוכנה: הקובץ שנבחר חייב להיות של AAA</summary>
  public static string UpdateFrom(string file)
  {
    var vi = FileVersionInfo.GetVersionInfo(file);
    if (!(vi.ProductName ?? "").StartsWith("AAA", StringComparison.Ordinal))
      return "הקובץ שנבחר אינו AAA.";
    CopySelfTo(Exe, file);
    Process.Start(new ProcessStartInfo(Exe) { UseShellExecute = true });
    return "";
  }

  // ---------- קיצורי דרך (תפריט התחל ושולחן העבודה הציבורי) ----------

  [ComImport, Guid("00021401-0000-0000-C000-000000000046")] class CShellLink { }

  [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
  interface IShellLinkW
  {
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder f, int cch, IntPtr pfd, int flags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder s, int cch);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string s);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder s, int cch);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string s);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder s, int cch);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string s);
    void GetHotkey(out short h);
    void SetHotkey(short h);
    void GetShowCmd(out int c);
    void SetShowCmd(int c);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder s, int cch, out int i);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string s, int i);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string s, int r);
    void Resolve(IntPtr hwnd, int flags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string s);
  }

  static void Shortcuts()
  {
    foreach (var folder in new[] { Environment.SpecialFolder.CommonPrograms, Environment.SpecialFolder.CommonDesktopDirectory })
    {
      try
      {
        var lnk = Path.Combine(Environment.GetFolderPath(folder), "AAA.lnk");
        var sl = (IShellLinkW)new CShellLink();
        sl.SetPath(Exe);
        sl.SetWorkingDirectory(Dir);
        sl.SetDescription("AAA – נעילת מחשב לפי רשימה לבנה");
        sl.SetIconLocation(Exe, 0);
        ((IPersistFile)sl).Save(lnk, true);
      }
      catch (Exception ex) { Log.Write("Shortcut: " + ex.Message); }
    }
  }
}
