using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AAA;

public partial class MainWindow
{
  // ===================== 6. רשימה לבנה =====================
  UIElement PageWhitelist()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("רשימה לבנה – מה מותר לרוץ"));
    sp.Children.Add(Para("שתי שיטות שעובדות יחד: AppLocker (כלי מובנה של Windows – חוסם לפני שהתוכנה עולה בכלל) ו-Watchdog (שירות שסוגר מיד כל תהליך שאינו ברשימה). מה שמותר: תיקיית Windows (בלי התיקיות שמשתמש יכול לכתוב אליהן), ותוכנות מהלשונית \"תוכנות מורשות\"."));
    sp.Children.Add(Card(
      Para("⚠ סדר עבודה מומלץ: קודם מצב בדיקה / למידה לכמה ימים, לעבוד בכל התוכנות, לעבור על יומן החסימות ולהתיר מה שחסר – ורק אז אכיפה.", Warn),
      Small("שתי השיטות עמידות בפני משתמש רגיל בלבד. מנהל יכול לבטל אותן.")));

    // ----- AppLocker -----
    bool home = AppLocker.IsHome();
    var al = Card(H2("AppLocker"),
      Small($"מערכת: {AppLocker.Edition()}.  שירות AppIDSvc: {AppLocker.ServiceState()}.  כללים פעילים מהתוכנה: {(Cfg.AppLockerApplied ? "כן" : "לא")}"));
    var alsp = (StackPanel)al.Child;
    if (home) alsp.Children.Add(Para("זו גרסת Home. לפי Microsoft הוסרה בדיקת המהדורה, אבל ב-Home אין ממשק ניהול ל-AppLocker – חובה לבדוק במצב בדיקה שהאירועים נרשמים. אם לא – להסתמך על הוואצ'דוג.", Warn));
    alsp.Children.Add(Check("הפעל AppLocker", Cfg.AppLockerEnabled, on => Cfg.AppLockerEnabled = on));
    var audit = new RadioButton { Content = "מצב בדיקה בלבד (שום דבר לא נחסם – רק נרשם ביומן)", IsChecked = Cfg.AppLockerAuditOnly, GroupName = "almode", Margin = new Thickness(24, 4, 0, 0) };
    var enforce = new RadioButton { Content = "מצב אכיפה (חוסם)", IsChecked = !Cfg.AppLockerAuditOnly, GroupName = "almode", Margin = new Thickness(24, 4, 0, 0) };
    audit.Checked += (_, _) => { Cfg.AppLockerAuditOnly = true; MarkDirty(); };
    enforce.Checked += (_, _) => { Cfg.AppLockerAuditOnly = false; MarkDirty(); };
    alsp.Children.Add(audit); alsp.Children.Add(enforce);
    var strict = Check("הגבל גם אפליקציות חנות (רק רכיבי Windows + החבילות שבלשונית \"תוכנות מורשות\")", Cfg.AppLockerStrictPackaged, on => Cfg.AppLockerStrictPackaged = on);
    strict.Margin = new Thickness(0, 10, 0, 0);
    alsp.Children.Add(strict);
    alsp.Children.Add(Small("הכללים: מנהלים – הכל. כולם – תיקיית Windows ותיקיות מאושרות. קובצי התקנה (MSI) – רק מנהלים. סקריפטים – רק מתוך Windows ותיקיות מאושרות. DLL – לא נבדקים (כבד ולא נחוץ)."));
    alsp.Children.Add(Btn("הצג את קובץ הכללים שייווצר", () =>
    {
      var w = new Window { Title = "AppLocker XML", Width = 900, Height = 600, Owner = this, FlowDirection = FlowDirection.LeftToRight };
      var xml = AppLocker.BuildXml(Cfg).Replace("><", ">\n<");
      w.Content = new TextBox { Text = xml, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
      w.ShowDialog();
    }));
    sp.Children.Add(al);

    // ----- Watchdog -----
    var wd = Card(H2("Watchdog (שירות שמירה)"),
      Small($"מצב השירות: {Watchdog.Status()}. השירות רץ כ-SYSTEM, עולה עם המחשב ומופעל מחדש אוטומטית אם נסגר. בודק כל רבע שנייה את התהליכים של חשבונות היעד."));
    var wdsp = (StackPanel)wd.Child;
    wdsp.Children.Add(Check("הפעל את הוואצ'דוג", Cfg.WatchdogEnabled, on => Cfg.WatchdogEnabled = on));
    var learn = Check("מצב למידה – לא לסגור, רק לרשום ביומן", Cfg.WatchdogLearning, on => Cfg.WatchdogLearning = on);
    learn.Margin = new Thickness(24, 4, 0, 0);
    wdsp.Children.Add(learn);
    wdsp.Children.Add(Small("הוואצ'דוג עובד גם בגרסת Home וגם כש-AppLocker כבוי. הוא סוגר את התהליך עצמו (לא רק את החלון) – כולל תוכנות שרצות בלי חלון."));
    sp.Children.Add(wd);

    // ----- כלים חסומים -----
    var dt = Card(H2("כלים חסומים בתוך Windows (לחשבון היעד בלבד)"),
      Small("תיקיית Windows מותרת כולה – אבל אלה כלים שבהם אפשר לבטל חסימות. נחסמים גם ב-AppLocker (כלל דחייה לחשבון היעד) וגם בוואצ'דוג."));
    var dtsp = (StackPanel)dt.Child;
    dtsp.Children.Add(Row(
      Btn("סמן מומלצים", () => { foreach (var t in Whitelist.DenyTools.Where(t => t.Recommended)) Cfg.DenyTools.Add(t.Id); MarkDirty(); Refresh(); }),
      Btn("נקה", () => { Cfg.DenyTools.Clear(); MarkDirty(); Refresh(); })));
    foreach (var t in Whitelist.DenyTools)
    {
      var id = t.Id;
      dtsp.Children.Add(Check(t.Title + (t.Recommended ? "" : "  (לא מומלץ כברירת מחדל)"), Cfg.DenyTools.Contains(id), on => { if (on) Cfg.DenyTools.Add(id); else Cfg.DenyTools.Remove(id); }));
    }
    sp.Children.Add(dt);
    return sp;
  }

  // ===================== 7. יומן =====================
  List<AppLocker.Hit>? _alHits;

  UIElement PageLog()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("יומן חסימות"));
    sp.Children.Add(Para("כאן רואים מה נחסם (או היה נחסם במצב בדיקה / למידה). כך מגלים אילו קבצים נוספים תוכנה מאושרת צריכה – ומתירים בלחיצה."));
    sp.Children.Add(Row(Btn("רענן", Refresh), Btn("טען אירועי AppLocker", async () =>
    {
      List<AppLocker.Hit>? r = null;
      await Busy("קורא את יומן AppLocker…", () => Task.Run(() => r = AppLocker.RecentEvents()));
      _alHits = r; Refresh();
    })));

    var wd = Card(H2("Watchdog"));
    var wsp = (StackPanel)wd.Child;
    var entries = Watchdog.ReadBlocked(300);
    if (entries.Count == 0) wsp.Children.Add(Small("אין רשומות."));
    foreach (var g in entries.GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase).Take(150))
    {
      var e = g.First();
      wsp.Children.Add(LogRow($"{e.Time:dd/MM HH:mm}  {e.User}  {(e.Killed ? "נסגר" : "למידה")}  ×{g.Count()}  – {e.Reason}", e.Path));
    }
    sp.Children.Add(wd);

    if (_alHits != null)
    {
      var al = Card(H2("AppLocker"));
      var asp = (StackPanel)al.Child;
      if (_alHits.Count == 0) asp.Children.Add(Small("אין אירועים."));
      foreach (var g in _alHits.Where(h => !string.IsNullOrEmpty(h.Path)).GroupBy(h => h.Path, StringComparer.OrdinalIgnoreCase).Take(150))
      {
        var h = g.First();
        asp.Children.Add(LogRow($"{h.Time:dd/MM HH:mm}  {h.User}  {(h.WouldBlock ? "היה נחסם" : "נחסם")}  ×{g.Count()}", h.Path));
      }
      sp.Children.Add(al);
    }
    return sp;
  }

  UIElement LogRow(string head, string path)
  {
    var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
    sp.Children.Add(Small(head));
    var r = Row(new TextBlock { Text = path, FlowDirection = FlowDirection.LeftToRight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 560 });
    var dir = Path.GetDirectoryName(path) ?? "";
    bool already = Cfg.Allowed.Any(a => a.IsFolder ? path.StartsWith(a.Path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) : a.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
    if (already) r.Children.Add(Small("✓ מותר כעת", Good));
    else
    {
      if (dir.Length > 3 && !AppProfiles.IsTooBroad(dir)) r.Children.Add(Btn("התר תיקייה", () => AddAllowed(dir, true, "מהיומן")));
      r.Children.Add(Btn("התר קובץ", () => AddAllowed(path, false, "מהיומן")));
    }
    sp.Children.Add(r);
    return sp;
  }

  // ===================== ביטול הכל =====================
  UIElement PageRemoveAll()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("ביטול כל החסימות"));
    sp.Children.Add(Para("מחזיר את המחשב למצב שלפני התוכנה: כל ערכי המדיניות חוזרים לערכם המקורי, כללי AppLocker נמחקים ושירות הוואצ'דוג מוסר. רשימת התוכנות המורשות נשמרת (לשימוש חוזר)."));
    sp.Children.Add(Para("רכיבים שהוסרו בלשונית \"ניקוי\" לא חוזרים אוטומטית.", Muted));
    sp.Children.Add(Btn("בטל את כל החסימות עכשיו", async () =>
    {
      if (!Confirm("לבטל את כל החסימות עכשיו?")) return;
      Cfg.Policies.Clear(); Cfg.AppLockerEnabled = false; Cfg.WatchdogEnabled = false;
      await ApplyAllPublic();
      if (Recovery.WinReState() == Recovery.ReState.Disabled && Confirm("להפעיל מחדש גם את סביבת השחזור (WinRE)?"))
      {
        var res = Recovery.EnableWinRe(Cfg); Cfg.Save(); MessageBox.Show(res.msg, "AAA");
      }
    }, true));
    sp.Children.Add(Card(H2("קבצים"),
      Small("ההגדרות, היומנים וקובץ הכללים נמצאים ב:"),
      new TextBox { Text = AppPaths.Root, IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight }));
    return sp;
  }

  internal Task ApplyAllPublic() => ApplyAll();
}
