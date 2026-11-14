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
    sp.Children.Add(Para("ברירת המחדל: הכל חסום. מותר רק מה שמוגדר כאן. שתי שיטות שעובדות יחד: AppLocker (כלי מובנה של Windows – חוסם לפני שהתוכנה עולה) ו-Watchdog (שירות שסוגר מיד כל תהליך שאינו ברשימה). המנהל לא מושפע – הוא מתקין ומעדכן כרגיל."));
    sp.Children.Add(Card(
      Para("⚠ סדר עבודה מומלץ: קודם מצב בדיקה / למידה לכמה ימים, לעבוד בכל התוכנות, לעבור על יומן החסימות ולהתיר מה שחסר – ורק אז אכיפה.", Warn),
      Small("שתי השיטות עמידות בפני משתמש רגיל בלבד. מנהל יכול לבטל אותן.")));

    // ----- מצב -----
    var mode = Card(H2("מה מותר", ""),
      Dialogs.Segment(new[] { "מחמיר – רק תוכנות מאושרות", "רגיל – כל Program Files" }, Cfg.AllowProgramFiles ? 1 : 0,
        k => { Cfg.AllowProgramFiles = k == 1; MarkDirty(); Refresh(); }));
    var msp = (StackPanel)mode.Child;
    msp.Children.Add(Small("תמיד מותר: תיקיית Windows – חוץ מתתי-התיקיות שמשתמש רגיל יכול לכתוב אליהן (Temp, Tasks, Tracing, System32\\Tasks, spool\\drivers\\color ועוד 14), שמוחרגות כדי שלא יעתיק לשם EXE ויריץ."));
    if (!Cfg.AllowProgramFiles)
      msp.Children.Add(Small("מחמיר: מותרות רק התיקיות והחתימות שבלשונית \"תוכנות מורשות\". כל תוכנה שתותקן בעתיד – חסומה עד שתתיר אותה."));
    else
    {
      msp.Children.Add(Small("רגיל: כל Program Files ו-Program Files (x86) מותרים, כי משתמש רגיל לא יכול לכתוב לשם. כל תוכנה שהמנהל מתקין – תרוץ."));
      msp.Children.Add(Small("חריגים אוטומטיים: Edge, נגן המדיה הישן, Internet Explorer, PowerShell 7, Git ועוד – וגם כל תיקייה ב-Program Files שמשתמש רגיל יכול לכתוב אליה (נסרקת בכל \"החל שינויים\")."));
      if (Cfg.PfWritable.Count > 0)
      {
        msp.Children.Add(Para($"תיקיות פתוחות לכתיבה שנמצאו בסריקה האחרונה ({Cfg.PfWritable.Count}) – מוחרגות:", Warn, 13));
        foreach (var w in Cfg.PfWritable.Take(30)) msp.Children.Add(PathText(w));
        if (Cfg.PfWritable.Count > 30) msp.Children.Add(Small($"ועוד {Cfg.PfWritable.Count - 30}…"));
      }
      msp.Children.Add(Btn("סרוק עכשיו תיקיות פתוחות לכתיבה", async () =>
      {
        List<string>? r = null;
        await Busy("סורק את Program Files…", () => Task.Run(() => r = ProgramFilesScan.FindWritable(Cfg.TargetSids)));
        Cfg.PfWritable = r ?? new(); MarkDirty(); Refresh();
        Say(Cfg.PfWritable.Count == 0 ? "לא נמצאו תיקיות פתוחות לכתיבה ב-Program Files." : $"נמצאו {Cfg.PfWritable.Count} תיקיות. הן יוחרגו בהחלה הבאה.");
      }));
    }
    sp.Children.Add(mode);

    // ----- AppLocker -----
    bool home = AppLocker.IsHome();
    var al = Card(H2("AppLocker", ""),
      Small($"מערכת: {AppLocker.Edition()}.  שירות AppIDSvc: {AppLocker.ServiceState()}.  כללים פעילים מהתוכנה: {(Cfg.AppLockerApplied ? "כן" : "לא")}"));
    var alsp = (StackPanel)al.Child;
    if (home) alsp.Children.Add(Para("זו גרסת Home. לפי Microsoft הוסרה בדיקת המהדורה, אבל ב-Home אין ממשק ניהול ל-AppLocker – חובה לבדוק במצב בדיקה שהאירועים נרשמים. אם לא – להסתמך על הוואצ'דוג.", Warn));
    alsp.Children.Add(Toggle("הפעל AppLocker", null, Cfg.AppLockerEnabled, on => Cfg.AppLockerEnabled = on));
    alsp.Children.Add(Dialogs.Segment(new[] { "מצב בדיקה בלבד (רק נרשם ביומן)", "מצב אכיפה (חוסם)" }, Cfg.AppLockerAuditOnly ? 0 : 1,
      k => { Cfg.AppLockerAuditOnly = k == 0; MarkDirty(); }));
    alsp.Children.Add(Toggle("הגבל גם אפליקציות חנות", "רק רכיבי Windows + החבילות שבלשונית \"תוכנות מורשות\" (ברירת מחדל: תמונות).", Cfg.AppLockerStrictPackaged, on => Cfg.AppLockerStrictPackaged = on));
    alsp.Children.Add(Small("הכללים: מנהלים – הכל. כולם – תיקיית Windows (בלי התיקיות הפתוחות לכתיבה), תיקיות מאושרות, היתרים לפי חתימה" + (Cfg.AllowProgramFiles ? ", Program Files (עם חריגים)" : "") + ". קובצי התקנה (MSI) – רק מנהלים. סקריפטים (bat, ps1, vbs, js) – רק מתוך המקומות המותרים. DLL – לא נבדקים."));
    alsp.Children.Add(Btn("הצג את קובץ הכללים שייווצר", () =>
    {
      var w = new Window { Title = "AppLocker XML", Width = 900, Height = 600, Owner = this, FlowDirection = FlowDirection.LeftToRight };
      var xml = AppLocker.BuildXml(Cfg).Replace("><", ">\n<");
      w.Content = new TextBox { Text = xml, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Style = null };
      w.ShowDialog();
    }, glyph: ""));
    sp.Children.Add(al);

    // ----- Watchdog -----
    var wd = Card(H2("Watchdog (שירות שמירה)", ""),
      Small($"מצב השירות: {Watchdog.Status()}. השירות רץ כ-SYSTEM, עולה עם המחשב ומופעל מחדש אוטומטית אם נסגר. בודק כל רבע שנייה את התהליכים של חשבונות היעד."));
    var wdsp = (StackPanel)wd.Child;
    wdsp.Children.Add(Toggle("הפעל את הוואצ'דוג", "עובד גם בגרסת Home וגם כש-AppLocker כבוי. סוגר את התהליך עצמו (לא רק את החלון) – כולל תוכנות שרצות בלי חלון.", Cfg.WatchdogEnabled, on => Cfg.WatchdogEnabled = on));
    wdsp.Children.Add(Toggle("מצב למידה – לא לסגור, רק לרשום ביומן", "כדי לגלות אילו קבצים נוספים התוכנות המאושרות צריכות, לפני שעוברים לחסימה.", Cfg.WatchdogLearning, on => Cfg.WatchdogLearning = on));
    sp.Children.Add(wd);

    // ----- כלים חסומים -----
    var dt = Card(H2("כלים חסומים בתוך Windows (לחשבון היעד בלבד)", ""),
      Small("תיקיית Windows מותרת כולה – אבל אלה כלים שבהם אפשר לבטל חסימות. נחסמים גם ב-AppLocker (כלל דחייה לחשבון היעד) וגם בוואצ'דוג."));
    var dtsp = (StackPanel)dt.Child;
    dtsp.Children.Add(Row(
      Btn("סמן מומלצים", () => { foreach (var t in Whitelist.DenyTools.Where(t => t.Recommended)) Cfg.DenyTools.Add(t.Id); MarkDirty(); Refresh(); }, true, ""),
      Btn("כבה הכל", () => { Cfg.DenyTools.Clear(); MarkDirty(); Refresh(); }, glyph: "")));
    foreach (var t in Whitelist.DenyTools)
    {
      var id = t.Id;
      dtsp.Children.Add(Toggle(t.Title, t.Recommended ? null : "לא מומלץ כברירת מחדל", Cfg.DenyTools.Contains(id), on => { if (on) Cfg.DenyTools.Add(id); else Cfg.DenyTools.Remove(id); }));
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
    }, true, "")));

    var wd = Card(H2("Watchdog", ""));
    var wsp = (StackPanel)wd.Child;
    var entries = Watchdog.ReadBlocked(300);
    if (entries.Count == 0) wsp.Children.Add(Small("אין רשומות."));
    foreach (var g in entries.GroupBy(e => e.Path, StringComparer.OrdinalIgnoreCase).Take(150))
    {
      var e = g.First();
      wsp.Children.Add(LogRow($"{e.Time:dd/MM HH:mm}  ·  {e.User}  ·  {(e.Killed ? "נסגר" : "למידה")}  ·  ×{g.Count()}  ·  {e.Reason}", e.Path));
    }
    sp.Children.Add(wd);

    if (_alHits != null)
    {
      var al = Card(H2("AppLocker", ""));
      var asp = (StackPanel)al.Child;
      if (_alHits.Count == 0) asp.Children.Add(Small("אין אירועים."));
      foreach (var g in _alHits.Where(h => !string.IsNullOrEmpty(h.Path)).GroupBy(h => h.Path, StringComparer.OrdinalIgnoreCase).Take(150))
      {
        var h = g.First();
        asp.Children.Add(LogRow($"{h.Time:dd/MM HH:mm}  ·  {h.User}  ·  {(h.WouldBlock ? "היה נחסם" : "נחסם")}  ·  ×{g.Count()}", h.Path));
      }
      sp.Children.Add(al);
    }
    return sp;
  }

  UIElement LogRow(string head, string path)
  {
    var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
    sp.Children.Add(Small(head));
    var r = Row(PathText(path));
    var dir = Path.GetDirectoryName(path) ?? "";
    bool already = Cfg.Allowed.Any(a => !a.IsPublisher && (a.IsFolder ? path.StartsWith(a.Path.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) : a.Path.Equals(path, StringComparison.OrdinalIgnoreCase)));
    if (already) r.Children.Add(Small("✓ מותר כעת", Good));
    else
    {
      bool risky = InUserProfile(path) || (Cfg.TargetSids.Count > 0 && FolderAcl.UserCanWrite(dir, Cfg.TargetSids));
      if (risky) r.Children.Add(Btn("התר (בדיקת חתימה)", () => AddFile(path)));
      else
      {
        if (dir.Length > 3 && !AppProfiles.IsTooBroad(dir)) r.Children.Add(Btn("התר תיקייה", () => AddAllowed(dir, true, "מהיומן")));
        r.Children.Add(Btn("התר קובץ", () => AddAllowed(path, false, "מהיומן")));
      }
    }
    sp.Children.Add(r);
    return sp;
  }

  // ===================== ביטול הכל =====================
  UIElement PageRemoveAll()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("ביטול כל החסימות"));
    sp.Children.Add(Card(
      Para("מחזיר את המחשב למצב שלפני התוכנה: כל ערכי המדיניות חוזרים לערכם המקורי, כללי AppLocker נמחקים ושירות הוואצ'דוג מוסר. רשימת התוכנות המורשות נשמרת (לשימוש חוזר)."),
      Small("רכיבים שהוסרו בלשונית \"ניקוי\" לא חוזרים אוטומטית."),
      Btn("בטל את כל החסימות עכשיו", async () =>
      {
        if (!Confirm("לבטל את כל החסימות עכשיו?")) return;
        Cfg.Policies.Clear(); Cfg.AppLockerEnabled = false; Cfg.WatchdogEnabled = false;
        await ApplyAll();
        if (Recovery.WinReState() == Recovery.ReState.Disabled && Confirm("להפעיל מחדש גם את סביבת השחזור (WinRE)?"))
        {
          var res = Recovery.EnableWinRe(Cfg); Cfg.Save(); Say(res.msg);
        }
      }, true, "")));
    sp.Children.Add(Card(H2("קבצים", ""),
      Small("ההגדרות, היומנים וקובץ הכללים נמצאים ב:"),
      Code(AppPaths.Root)));
    return sp;
  }
}
