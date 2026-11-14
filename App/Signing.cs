using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace AAA;

/// <summary>חתימה דיגיטלית של קבצים: אימות (WinVerifyTrust) וקריאת פרטי המפרסם בפורמט של AppLocker</summary>
static class Signing
{
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  struct WINTRUST_FILE_INFO
  {
    public int cbStruct;
    public string pcwszFilePath;
    public IntPtr hFile;
    public IntPtr pgKnownSubject;
  }

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  struct WINTRUST_DATA
  {
    public int cbStruct;
    public IntPtr pPolicyCallbackData;
    public IntPtr pSIPClientData;
    public int dwUIChoice;
    public int fdwRevocationChecks;
    public int dwUnionChoice;
    public IntPtr pFile;
    public int dwStateAction;
    public IntPtr hWVTStateData;
    public IntPtr pwszURLReference;
    public int dwProvFlags;
    public int dwUIContext;
    public IntPtr pSignatureSettings;
  }

  [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
  static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, ref WINTRUST_DATA data);

  static readonly Guid GenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

  /// <summary>true אם הקובץ חתום בחתימה תקפה (לא שונה מאז החתימה)</summary>
  public static bool IsValid(string path)
  {
    var fi = new WINTRUST_FILE_INFO { cbStruct = Marshal.SizeOf<WINTRUST_FILE_INFO>(), pcwszFilePath = path };
    var pFile = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
    try
    {
      Marshal.StructureToPtr(fi, pFile, false);
      var wd = new WINTRUST_DATA
      {
        cbStruct = Marshal.SizeOf<WINTRUST_DATA>(), dwUIChoice = 2 /*WTD_UI_NONE*/, fdwRevocationChecks = 0,
        dwUnionChoice = 1 /*WTD_CHOICE_FILE*/, pFile = pFile, dwStateAction = 0, dwProvFlags = 0x1000 /*WTD_CACHE_ONLY_URL_RETRIEVAL*/,
      };
      return WinVerifyTrust(IntPtr.Zero, GenericVerifyV2, ref wd) == 0;
    }
    catch { return false; }
    finally { Marshal.DestroyStructure<WINTRUST_FILE_INFO>(pFile); Marshal.FreeHGlobal(pFile); }
  }

  /// <summary>נושא התעודה שחתמה על הקובץ (null אם לא חתום)</summary>
  public static string? SignerSubject(string path)
  {
    try
    {
#pragma warning disable SYSLIB0057
      using var c = X509Certificate.CreateFromSignedFile(path);
#pragma warning restore SYSLIB0057
      return c.Subject;
    }
    catch { return null; }
  }

  public static string ProductName(string path)
  {
    try { return FileVersionInfo.GetVersionInfo(path).ProductName?.Trim() ?? ""; } catch { return ""; }
  }

  public record Info(string Publisher, string Product, string Signer);

  /// <summary>פרטי המפרסם כפי ש-AppLocker רואה אותם (דרך Get-AppLockerFileInformation). null אם הקובץ לא חתום</summary>
  public static Info? Read(string path)
  {
    if (!IsValid(path)) return null;
    var signer = SignerSubject(path);
    if (signer == null) return null;
    var r = Shell.PowerShell($"Import-Module AppLocker; $i = Get-AppLockerFileInformation -Path '{path.Replace("'", "''")}'; if ($i.Publisher) {{ 'P=' + $i.Publisher.PublisherName; 'R=' + $i.Publisher.ProductName }}");
    string pub = "", prod = "";
    foreach (var l in r.Output.Split('\n'))
    {
      var t = l.Trim();
      if (t.StartsWith("P=")) pub = t[2..];
      else if (t.StartsWith("R=")) prod = t[2..];
    }
    if (pub.Length == 0) return null;
    return new Info(pub, prod.Length > 0 ? prod : "*", signer);
  }
}
