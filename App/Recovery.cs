using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Win32;

namespace AAA;

/// <summary>סביבת השחזור (WinRE), הגדרות אתחול ומידע על הקושחה</summary>
static class Recovery
{
  public enum ReState { Enabled, Disabled, Unknown }

  public static ReState WinReState()
  {
    // ReAgent.xml: InstallState state="1" = פעיל. לא תלוי בשפת המערכת.
    try
    {
      var xml = Path.Combine(Environment.SystemDirectory, @"Recovery\ReAgent.xml");
      if (File.Exists(xml))
      {
        var st = XDocument.Load(xml).Descendants("InstallState").FirstOrDefault()?.Attribute("state")?.Value;
        if (st == "1") return ReState.Enabled;
        if (st == "0") return ReState.Disabled;
      }
    }
    catch (Exception ex) { Log.Write("ReAgent.xml: " + ex.Message); }
    var r = Shell.Exe("reagentc.exe", "/info");
    if (r.Output.Contains("Enabled", StringComparison.OrdinalIgnoreCase)) return ReState.Enabled;
    if (r.Output.Contains("Disabled", StringComparison.OrdinalIgnoreCase)) return ReState.Disabled;
    return ReState.Unknown;
  }

  public static (bool ok, string msg) DisableWinRe(Config cfg)
  {
    var r = Shell.Exe("reagentc.exe", "/disable");
    // תיקון אוטומטי באתחול (Automatic Repair) – גם הוא מוביל ל-WinRE
    var b = Shell.Exe("bcdedit.exe", "/set {current} recoveryenabled No");
    if (b.Exit == 0) cfg.BcdRecoveryDisabledByUs = true;
    bool ok = WinReState() == ReState.Disabled;
    if (ok) cfg.WinReDisabledByUs = true;
    return (ok, ok ? "סביבת השחזור הושבתה." : "ההשבתה נכשלה:\n" + r.Output.Trim());
  }

  public static (bool ok, string msg) EnableWinRe(Config cfg)
  {
    var r = Shell.Exe("reagentc.exe", "/enable");
    if (cfg.BcdRecoveryDisabledByUs) { Shell.Exe("bcdedit.exe", "/set {current} recoveryenabled Yes"); cfg.BcdRecoveryDisabledByUs = false; }
    bool ok = WinReState() == ReState.Enabled;
    if (ok) cfg.WinReDisabledByUs = false;
    return (ok, ok ? "סביבת השחזור הופעלה מחדש." : "ההפעלה נכשלה (ייתכן שקובץ Winre.wim חסר):\n" + r.Output.Trim());
  }

  [DllImport("kernel32.dll")] static extern bool GetFirmwareType(out int type);

  public static string FirmwareType()
  {
    try { if (GetFirmwareType(out int t)) return t switch { 1 => "BIOS ישן (Legacy)", 2 => "UEFI", _ => "לא ידוע" }; } catch { }
    return "לא ידוע";
  }

  public static bool IsUefi() { try { return GetFirmwareType(out int t) && t == 2; } catch { return false; } }

  public static string SecureBoot()
  {
    var v = RegOps.Get(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled");
    return v is int i ? (i == 1 ? "פעיל" : "כבוי") : "לא נתמך / לא ידוע";
  }

  /// <summary>הפעלה מחדש ישירות למסך הגדרות הקושחה (UEFI בלבד)</summary>
  public static void RebootToFirmware() => Shell.Exe("shutdown.exe", "/r /fw /t 5");

  public record CheckItem(string Id, string Title, string Detail);

  public static readonly CheckItem[] BiosChecklist =
  {
    new("bios_pwd", "הגדרת סיסמת מנהל לביוס (Supervisor / Administrator Password)",
      "בלי סיסמה – כל אחד נכנס לביוס ומשנה את סדר האתחול. לא להגדיר סיסמת הדלקה (Power-On) אלא אם רוצים."),
    new("boot_order", "סדר אתחול: רק הדיסק הפנימי (Windows Boot Manager)",
      "להסיר מהרשימה USB, CD/DVD ורשת (PXE / Network Boot). כך אי אפשר לעלות מדיסק און קי."),
    new("boot_menu", "ביטול תפריט האתחול המהיר (F12 / F11 / F8 / Esc)",
      "בהרבה מחשבים: Boot Menu / One-Time Boot Menu → Disabled. אחרת אפשר לבחור USB בלי להיכנס לביוס."),
    new("usb_boot", "ביטול אתחול מ-USB (USB Boot / Boot from External Device)",
      "אם יש אפשרות כזו – לכבות, בנוסף להסרה מסדר האתחול."),
    new("secure_boot", "Secure Boot פעיל",
      "מונע עלייה של מערכות לא חתומות (לינוקס חי וכו'). מצב נוכחי מוצג למעלה."),
    new("csm", "כיבוי CSM / Legacy Boot",
      "מצב UEFI בלבד. נדרש בשביל Secure Boot."),
    new("winre", "סביבת השחזור (WinRE) מושבתת – מתבצע על ידי התוכנה",
      "בלי WinRE אין \"איפוס המחשב\", \"התחלה מתקדמת\" ושורת פקודה לפני Windows. חשוב: להחזיק דיסק שחזור / התקנה חיצוני למקרה תקלה."),
    new("bitlocker", "מומלץ: הצפנת הדיסק (BitLocker / הצפנת מכשיר)",
      "מונע הוצאת הדיסק וחיבורו למחשב אחר כדי לערוך אותו. לשמור את מפתח השחזור במקום בטוח."),
    new("chassis", "אופציונלי: נעילת המארז",
      "הוצאת סוללת ה-CMOS / ג'אמפר מאפסת את סיסמת הביוס במחשבים רבים."),
  };
}
