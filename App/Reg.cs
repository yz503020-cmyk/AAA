using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AAA;

/// <summary>
/// גישה לענף הרישום של משתמש יעד: אם הוא מחובר – HKEY_USERS\SID, אחרת טוענים את NTUSER.DAT שלו זמנית.
/// </summary>
sealed class UserHive : IDisposable
{
  const uint HKEY_USERS = 0x80000003;
  [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegLoadKey(UIntPtr hKey, string subKey, string file);
  [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int RegUnLoadKey(UIntPtr hKey, string subKey);

  public RegistryKey Root { get; }
  readonly string? _tempName;

  UserHive(RegistryKey root, string? temp) { Root = root; _tempName = temp; }

  public static UserHive? Open(string sid, string profilePath)
  {
    var live = Registry.Users.OpenSubKey(sid, true);
    if (live != null) return new UserHive(live, null);
    var dat = Path.Combine(profilePath, "NTUSER.DAT");
    if (string.IsNullOrEmpty(profilePath) || !File.Exists(dat)) return null;
    Privileges.Enable("SeBackupPrivilege");
    Privileges.Enable("SeRestorePrivilege");
    var name = "AAA_" + sid.Replace('-', '_');
    int rc = RegLoadKey(new UIntPtr(HKEY_USERS), name, dat);
    if (rc != 0) { Log.Write($"RegLoadKey {sid} failed {rc}: {new Win32Exception(rc).Message}"); return null; }
    var k = Registry.Users.OpenSubKey(name, true);
    if (k == null) { RegUnLoadKey(new UIntPtr(HKEY_USERS), name); return null; }
    return new UserHive(k, name);
  }

  public void Dispose()
  {
    Root.Dispose();
    if (_tempName != null)
    {
      GC.Collect(); GC.WaitForPendingFinalizers();
      for (int i = 0; i < 5; i++)
      {
        int rc = RegUnLoadKey(new UIntPtr(HKEY_USERS), _tempName);
        if (rc == 0) return;
        Thread.Sleep(300);
      }
      Log.Write("RegUnLoadKey failed for " + _tempName);
    }
  }
}

static class Privileges
{
  [StructLayout(LayoutKind.Sequential)] struct LUID { public uint Low; public int High; }
  [StructLayout(LayoutKind.Sequential)] struct TOKEN_PRIVILEGES { public int Count; public LUID Luid; public int Attr; }
  [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr h, int access, out IntPtr tok);
  [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool LookupPrivilegeValue(string? sys, string name, out LUID luid);
  [DllImport("advapi32.dll", SetLastError = true)] static extern bool AdjustTokenPrivileges(IntPtr tok, bool disableAll, ref TOKEN_PRIVILEGES p, int len, IntPtr prev, IntPtr retLen);
  [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

  public static void Enable(string name)
  {
    if (!OpenProcessToken(GetCurrentProcess(), 0x0020 | 0x0008, out var tok)) return;
    try
    {
      if (!LookupPrivilegeValue(null, name, out var luid)) return;
      var tp = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attr = 2 };
      AdjustTokenPrivileges(tok, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
    }
    finally { CloseHandle(tok); }
  }
}

/// <summary>כתיבת ערך עם גיבוי, ושחזור מהגיבוי</summary>
static class RegOps
{
  public static RegistryKey Hklm() => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

  public static void SetWithBackup(Config cfg, string hiveTag, RegistryKey root, string key, string name, object value, RegistryValueKind kind)
  {
    var bk = $"{hiveTag}|{key}|{name}";
    using var k = root.CreateSubKey(key, true);
    if (!cfg.Backups.ContainsKey(bk))
    {
      var cur = k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
      var be = new BackupEntry { Existed = cur != null };
      if (cur != null)
      {
        var ck = k.GetValueKind(name);
        be.Kind = ck.ToString();
        be.Data = ck switch
        {
          RegistryValueKind.DWord or RegistryValueKind.QWord => Convert.ToInt64(cur).ToString(),
          RegistryValueKind.String or RegistryValueKind.ExpandString => (string)cur,
          RegistryValueKind.MultiString => string.Join("\u0001", (string[])cur),
          RegistryValueKind.Binary => Convert.ToBase64String((byte[])cur),
          _ => cur.ToString() ?? "",
        };
      }
      cfg.Backups[bk] = be;
    }
    k.SetValue(name, value, kind);
  }

  public static void Restore(Config cfg, string hiveTag, RegistryKey root, string key, string name)
  {
    var bk = $"{hiveTag}|{key}|{name}";
    if (!cfg.Backups.TryGetValue(bk, out var be)) return;
    try
    {
      using var k = root.CreateSubKey(key, true);
      if (!be.Existed) k.DeleteValue(name, false);
      else
      {
        var kind = Enum.Parse<RegistryValueKind>(be.Kind);
        object v = kind switch
        {
          RegistryValueKind.DWord => unchecked((int)long.Parse(be.Data)),
          RegistryValueKind.QWord => long.Parse(be.Data),
          RegistryValueKind.MultiString => be.Data.Split('\u0001'),
          RegistryValueKind.Binary => Convert.FromBase64String(be.Data),
          _ => be.Data,
        };
        k.SetValue(name, v, kind);
      }
      cfg.Backups.Remove(bk);
    }
    catch (Exception ex) { Log.Write($"Restore {bk}: {ex.Message}"); }
  }

  public static object? Get(RegistryKey root, string key, string name)
  {
    try { using var k = root.OpenSubKey(key); return k?.GetValue(name); } catch { return null; }
  }
}
