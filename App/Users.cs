using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace AAA;

public record LocalUser(string Sid, string Name, string ProfilePath, bool IsAdmin, bool HasProfile);

static class Users
{
  [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
  static extern int NetLocalGroupGetMembers(string? server, string group, int level, out IntPtr buf, int prefmax, out int read, out int total, IntPtr resume);
  [DllImport("netapi32.dll")] static extern int NetApiBufferFree(IntPtr buf);
  [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
  static extern int NetUserEnum(string? server, int level, int filter, out IntPtr buf, int prefmax, out int read, out int total, IntPtr resume);

  [StructLayout(LayoutKind.Sequential)] struct LOCALGROUP_MEMBERS_INFO_0 { public IntPtr sid; }
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct USER_INFO_0 { public string name; }

  /// <summary>SID-ים של חברי קבוצת המנהלים המקומית (שם הקבוצה מתורגם לפי שפת המערכת)</summary>
  public static HashSet<string> AdminSids()
  {
    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    try
    {
      var grp = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Translate(typeof(NTAccount)).Value;
      grp = grp.Contains('\\') ? grp[(grp.IndexOf('\\') + 1)..] : grp;
      if (NetLocalGroupGetMembers(null, grp, 0, out var buf, -1, out int read, out _, IntPtr.Zero) == 0)
      {
        try
        {
          int sz = Marshal.SizeOf<LOCALGROUP_MEMBERS_INFO_0>();
          for (int i = 0; i < read; i++)
          {
            var m = Marshal.PtrToStructure<LOCALGROUP_MEMBERS_INFO_0>(buf + i * sz);
            set.Add(new SecurityIdentifier(m.sid).Value);
          }
        }
        finally { NetApiBufferFree(buf); }
      }
    }
    catch (Exception ex) { Log.Write("AdminSids: " + ex.Message); }
    return set;
  }

  /// <summary>כל החשבונות המקומיים, כולל חשבונות שעוד לא נכנסו (בלי פרופיל)</summary>
  public static List<LocalUser> List()
  {
    var admins = AdminSids();
    var result = new Dictionary<string, LocalUser>(StringComparer.OrdinalIgnoreCase);
    using (var pl = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList"))
    {
      foreach (var sid in pl?.GetSubKeyNames() ?? Array.Empty<string>())
      {
        if (!sid.StartsWith("S-1-5-21-") || sid.EndsWith(".bak")) continue;
        using var k = pl!.OpenSubKey(sid);
        var path = Environment.ExpandEnvironmentVariables(k?.GetValue("ProfileImagePath") as string ?? "");
        result[sid] = new LocalUser(sid, NameOf(sid) ?? Path.GetFileName(path), path, admins.Contains(sid), Directory.Exists(path));
      }
    }
    // חשבונות מקומיים בלי פרופיל
    try
    {
      if (NetUserEnum(null, 0, 2 /*FILTER_NORMAL_ACCOUNT*/, out var buf, -1, out int read, out _, IntPtr.Zero) == 0)
      {
        try
        {
          int sz = Marshal.SizeOf<USER_INFO_0>();
          for (int i = 0; i < read; i++)
          {
            var u = Marshal.PtrToStructure<USER_INFO_0>(buf + i * sz);
            try
            {
              var sid = ((SecurityIdentifier)new NTAccount(Environment.MachineName, u.name).Translate(typeof(SecurityIdentifier))).Value;
              if (sid.EndsWith("-500") || sid.EndsWith("-501") || sid.EndsWith("-503") || sid.EndsWith("-504")) continue; // מובנים
              if (!result.ContainsKey(sid)) result[sid] = new LocalUser(sid, u.name, "", admins.Contains(sid), false);
            }
            catch { }
          }
        }
        finally { NetApiBufferFree(buf); }
      }
    }
    catch (Exception ex) { Log.Write("NetUserEnum: " + ex.Message); }
    return result.Values.OrderBy(u => u.IsAdmin).ThenBy(u => u.Name).ToList();
  }

  public static string? NameOf(string sid)
  {
    try
    {
      var n = new SecurityIdentifier(sid).Translate(typeof(NTAccount)).Value;
      return n.Contains('\\') ? n[(n.IndexOf('\\') + 1)..] : n;
    }
    catch { return null; }
  }

  public static string CurrentSid() => WindowsIdentity.GetCurrent().User?.Value ?? "";
}
