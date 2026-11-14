using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AAA;

public partial class MainWindow
{
  // ===================== 1. חשבונות יעד =====================
  UIElement PageUsers()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("חשבונות יעד"));
    sp.Children.Add(Para("בחר את החשבונות שעליהם חלות החסימות. רק חשבון רגיל (לא מנהל) יכול להיות חשבון יעד – כל הכלים כאן עמידים בפני משתמש רגיל, לא בפני מנהל."));
    var card = Card(H2("חשבונות במחשב", "\uE716"));
    var inner = (StackPanel)card.Child;
    var me = Users.CurrentSid();
    if (UsersList.Count == 0) inner.Children.Add(Para("לא נמצאו חשבונות מקומיים.", Warn));
    foreach (var u in UsersList)
    {
      bool selectable = !u.IsAdmin && u.Sid != me;
      string? desc = u.IsAdmin ? "מנהל – לא ניתן לבחירה" : u.Sid == me ? "החשבון הנוכחי" : !u.HasProfile ? "עוד לא התחבר: יש להתחבר אליו פעם אחת לפני החלת חסימות המשתמש" : "חשבון רגיל";
      var sid = u.Sid;
      inner.Children.Add(Toggle(u.Name, desc, Cfg.TargetSids.Contains(sid), on =>
      {
        Cfg.TargetSids.Remove(sid);
        if (on) Cfg.TargetSids.Add(sid);
      }, selectable));
    }
    inner.Children.Add(Btn("רענן רשימה", () => { UsersList = Users.List(); Refresh(); }));
    sp.Children.Add(card);
    sp.Children.Add(Card(H2("אין חשבון רגיל?", "\uE8FA"),
      Para("צור חשבון מקומי רגיל דרך הגדרות ← חשבונות ← משתמשים אחרים, או בשורת פקודה של מנהל:"),
      Code("net user תלמיד /add"),
      Small("אחרי היצירה – להתחבר אליו פעם אחת (כדי שייווצר לו פרופיל), לחזור לחשבון המנהל ולרענן כאן.")));
    return sp;
  }

  // ===================== 2. מדיניות =====================
  UIElement PagePolicies()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("מדיניות (GPO ורישום)"));
    sp.Children.Add(Para("הגדרות מתוך עורך המדיניות (ועוד כמה ערכי רישום שאינם בו) שסוגרות את הדרכים לבטל או לעקוף חסימה. כיבוי מתג מחזיר את הערך המקורי שהיה במחשב לפני שהתוכנה שינתה אותו."));
    sp.Children.Add(Row(
      Btn("סמן את המומלצים", () => { foreach (var p in PolicyCatalog.All.Where(p => p.Recommended)) Cfg.Policies.Add(p.Id); MarkDirty(); Refresh(); }, true, "\uE73E"),
      Btn("כבה הכל", () => { Cfg.Policies.Clear(); MarkDirty(); Refresh(); }, glyph: "\uE711")));
    var glyphs = new Dictionary<string, string> { ["כלי מערכת"] = "\uE90F", ["התקנות"] = "\uE896", ["חשבונות והרשאות"] = "\uE77B", ["רשת"] = "\uE774", ["התקנים"] = "\uE88E", ["עדכונים"] = "\uE895" };
    foreach (var g in PolicyCatalog.Groups)
    {
      var card = Card(H2(g, glyphs.GetValueOrDefault(g, "\uE713")));
      var inner = (StackPanel)card.Child;
      foreach (var p in PolicyCatalog.All.Where(p => p.Group == g))
      {
        var id = p.Id;
        var scope = p.UserScoped ? "חשבון היעד" : "כל המחשב";
        inner.Children.Add(Toggle(p.Title, $"[{scope}{(p.Recommended ? "" : " · לא מומלץ כברירת מחדל")}]  {p.Desc}", Cfg.Policies.Contains(id),
          on => { if (on) Cfg.Policies.Add(id); else Cfg.Policies.Remove(id); }, warn: p.Warning));
      }
      sp.Children.Add(card);
    }
    sp.Children.Add(Card(H2("לא כאן – ובכל זאת מכוסה", "\uE946"),
      Small("PowerShell, הסקריפטים, WSL ועוד – נחסמים לחשבון היעד בלשונית \"רשימה לבנה\" (כלים חסומים), כי אין להם הגדרת GPO אמינה."),
      Small("שינוי שעה ותאריך – משתמש רגיל ב-Windows לא מורשה לשנות שעה ממילא.")));
    return sp;
  }

  // ===================== 3. ביוס ו-WinRE =====================
  UIElement PageRecovery()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("ביוס וסביבת השחזור"));
    sp.Children.Add(Para("את הביוס התוכנה לא יכולה לנעול – זה נעשה ידנית במסך הגדרות הקושחה. התוכנה משביתה את סביבת השחזור של Windows (WinRE), שממנה אפשר לאפס את המחשב, לפתוח שורת פקודה לפני Windows ולעקוף כל חסימה."));

    var re = Recovery.WinReState();
    var reText = re switch { Recovery.ReState.Disabled => "מושבתת ✓", Recovery.ReState.Enabled => "פעילה – יש להשבית", _ => "לא ידוע" };
    var info = Card(H2("מצב נוכחי", "\uE7F4"),
      Para($"קושחה: {Recovery.FirmwareType()}"),
      Para($"Secure Boot: {Recovery.SecureBoot()}"),
      Para($"סביבת השחזור (WinRE): {reText}", re == Recovery.ReState.Disabled ? Good : Warn));
    var infoSp = (StackPanel)info.Child;
    var row = Row();
    if (re != Recovery.ReState.Disabled)
      row.Children.Add(Btn("השבת את WinRE עכשיו", async () =>
      {
        if (!Confirm("להשבית את סביבת השחזור?\n\nאחרי זה לא תהיה אפשרות \"איפוס המחשב\" או \"התחלה מתקדמת\" מתוך Windows. במקרה של תקלה תצטרך דיסק התקנה / שחזור חיצוני.\n\nזו פעולה מיידית (לא מחכה ל\"החל שינויים\").")) return;
        (bool ok, string msg) res = default;
        var copy = Cfg;
        await Busy("משבית את WinRE…", () => Task.Run(() => res = Recovery.DisableWinRe(copy)));
        SaveImmediateFlags(c => { c.WinReDisabledByUs = copy.WinReDisabledByUs; c.BcdRecoveryDisabledByUs = copy.BcdRecoveryDisabledByUs; });
        Say(res.msg); Refresh();
      }, true, "\uE72E"));
    else
      row.Children.Add(Btn("הפעל מחדש את WinRE", async () =>
      {
        (bool ok, string msg) res = default;
        var copy = Cfg;
        await Busy("מפעיל את WinRE…", () => Task.Run(() => res = Recovery.EnableWinRe(copy)));
        SaveImmediateFlags(c => { c.WinReDisabledByUs = copy.WinReDisabledByUs; c.BcdRecoveryDisabledByUs = copy.BcdRecoveryDisabledByUs; });
        Say(res.msg); Refresh();
      }, glyph: "\uE785"));
    if (Recovery.IsUefi())
      row.Children.Add(Btn("הפעל מחדש לתוך הביוס", () =>
      {
        if (Confirm("המחשב יופעל מחדש בעוד 5 שניות ישירות למסך הגדרות הקושחה (UEFI). לשמור עבודה פתוחה קודם. להמשיך?")) Recovery.RebootToFirmware();
      }, glyph: "\uE777"));
    infoSp.Children.Add(row);
    sp.Children.Add(info);

    var list = Card(H2("רשימת חובה – נעילת הביוס (לסמן אחרי ביצוע)", "\uE73A"));
    var lsp = (StackPanel)list.Child;
    foreach (var it in Recovery.BiosChecklist)
    {
      var id = it.Id;
      bool auto = id == "winre";
      bool val = auto ? re == Recovery.ReState.Disabled : Cfg.BiosChecklist.GetValueOrDefault(id);
      lsp.Children.Add(Toggle(it.Title, it.Detail, val, on => SaveImmediateFlags(c => c.BiosChecklist[id] = on), !auto, dirty: false));
    }
    lsp.Children.Add(Small("הסימונים כאן הם רשימת תיוג בשבילך בלבד ונשמרים מיד."));
    sp.Children.Add(list);
    return sp;
  }

  // ===================== 4. ניקוי =====================
  HashSet<string>? _installed;
  readonly HashSet<string> _toRemove = new();

  UIElement PageDebloat()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("ניקוי Windows"));
    sp.Children.Add(Para("הסרת כלים מיותרים ומסכני חסימה: דפדפן, נגני מדיה, שליטה מרחוק, מכונות וירטואליות ועוד. ההסרה מיידית ולכל המשתמשים. גם מה שלא יוסר ייחסם ממילא ברשימה הלבנה – אבל עדיף שלא יהיה במחשב בכלל."));
    sp.Children.Add(Row(
      Btn(_installed == null ? "סרוק מה מותקן" : "סרוק שוב", async () =>
      {
        HashSet<string>? r = null;
        await Busy("סורק רכיבים מותקנים (עד דקה)…", () => Task.Run(() => r = Debloat.Scan()));
        _installed = r; _toRemove.Clear();
        foreach (var it in Debloat.Items.Where(i => i.Recommended && (_installed?.Contains(i.Id) ?? false))) _toRemove.Add(it.Id);
        Refresh();
      }, true),
      Btn("הסר את המסומנים", async () =>
      {
        if (_installed == null || _toRemove.Count == 0) { Say("סרוק קודם וסמן מה להסיר."); return; }
        var items = Debloat.Items.Where(i => _toRemove.Contains(i.Id)).ToList();
        if (!Confirm("להסיר עכשיו את הרכיבים הבאים?\n\n" + string.Join("\n", items.Select(i => "• " + i.Title)) + "\n\nחלק מההסרות דורשות הפעלה מחדש.")) return;
        var errs = new List<string>();
        await Busy("מסיר…", () => Task.Run(() =>
        {
          int n = 0;
          foreach (var it in items)
          {
            n++;
            Dispatcher.Invoke(() => SetStatus($"⏳ מסיר ({n}/{items.Count}): {it.Title}"));
            var r = Debloat.Remove(it);
            if (!string.IsNullOrWhiteSpace(r.Output) && r.Output.Contains("Exception", StringComparison.OrdinalIgnoreCase)) errs.Add(it.Title + ": " + Shell.Trunc(r.Output.Trim(), 200));
          }
          _installed = Debloat.Scan();
        }));
        _toRemove.Clear();
        Refresh();
        var left = items.Where(i => _installed?.Contains(i.Id) ?? false).Select(i => i.Title).ToList();
        Say("ההסרה הסתיימה." + (left.Count > 0 ? "\n\nעדיין מותקנים (ייתכן שצריך הפעלה מחדש, או שהמערכת מונעת הסרה):\n" + string.Join("\n", left) : "") +
          (errs.Count > 0 ? "\n\nהודעות:\n" + string.Join("\n", errs) : ""));
      })));

    var card = Card(H2(_installed == null ? "רכיבים (לחץ \"סרוק\" כדי לראות מה מותקן)" : "רכיבים – מתג דולק = יוסר", "\uE74C"));
    var inner = (StackPanel)card.Child;
    foreach (var it in Debloat.Items)
    {
      var id = it.Id;
      bool? inst = _installed == null ? null : _installed.Contains(id);
      var state = inst == null ? "" : inst.Value ? "  — מותקן" : "  — לא קיים ✓";
      var desc = string.IsNullOrEmpty(it.Desc) ? null : it.Desc;
      inner.Children.Add(Toggle(it.Title + state, desc, _toRemove.Contains(id), on => { if (on) _toRemove.Add(id); else _toRemove.Remove(id); }, inst == true, dirty: false, warn: it.Warning));
    }
    sp.Children.Add(card);
    sp.Children.Add(Card(Small("OneDrive ותוכנות שמותקנות בתוך פרופיל המשתמש לא מוסרות כאן – הן לא ברשימה הלבנה ולכן לא ירוצו.")));
    return sp;
  }

  // ===================== 5. תוכנות מורשות =====================
  readonly Dictionary<string, List<string>> _found = new();

  static bool InUserProfile(string p) =>
    p.StartsWith(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows))!, "Users") + "\\", StringComparison.OrdinalIgnoreCase);

  UIElement PageApps()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("תוכנות מורשות"));
    sp.Children.Add(Para(Cfg.AllowProgramFiles
      ? "מצב רגיל פעיל (לשונית \"רשימה לבנה\"): כל מה שמותקן ב-Program Files מותר ממילא. כאן מוסיפים רק תוכנות שמותקנות במקום אחר."
      : "מצב מחמיר: רק מה שמופיע כאן (וחלקי Windows עצמו) ירוץ בחשבון היעד. לכל תוכנה מוכרת – \"חפש במחשב\" מוצא את כל התיקיות שהיא צריכה, כולל רכיבים נלווים, כדי שלא תקרוס."));

    foreach (var prof in AppProfiles.All)
    {
      var card = Card(H2(prof.Name, "\uE71D"), Small(prof.Notes));
      var inner = (StackPanel)card.Child;
      if (prof.Candidates.Length > 0 || prof.Keywords.Length > 0)
      {
        var pr = prof;
        inner.Children.Add(Btn("חפש במחשב", async () =>
        {
          List<string>? res = null;
          await Busy("מחפש את " + pr.Name + "…", () => Task.Run(() => res = AppProfiles.Discover(pr, UsersList)));
          _found[pr.Id] = res ?? new();
          Refresh();
        }));
      }
      if (_found.TryGetValue(prof.Id, out var dirs))
      {
        if (dirs.Count == 0) inner.Children.Add(Para("לא נמצא. אם התוכנה מותקנת – הוסף את התיקייה שלה ידנית למטה.", Warn));
        foreach (var d in dirs)
        {
          bool exists = Cfg.Allowed.Any(a => a.Path.Equals(d, StringComparison.OrdinalIgnoreCase));
          var dd = d; var pname = prof.Name;
          var r = Row(PathText(d));
          if (exists) r.Children.Add(Small("✓ ברשימה", Good));
          else r.Children.Add(Btn("הוסף", () => AddAllowed(dd, true, pname)));
          inner.Children.Add(r);
          if (InUserProfile(d))
            inner.Children.Add(Row(Small("⚠ מותקנת בתוך פרופיל משתמש (AppData)", Warn), Btn("איך להעביר ל-Program Files", () => Say(AppDataGuide(dd)), glyph: "\uE946")));
        }
        if (dirs.Count > 1)
          inner.Children.Add(Btn("הוסף את כולן", () => { foreach (var d in dirs) AddAllowed(d, true, prof.Name, refresh: false); Refresh(); }, true));
      }
      sp.Children.Add(card);
    }

    // ----- הרשימה עצמה -----
    var list = Card(H2($"מה מותר כעת ({Cfg.Allowed.Count})", "\uE73E"));
    var lsp = (StackPanel)list.Child;
    lsp.Children.Add(Row(
      Btn("הוסף תיקייה…", () =>
      {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "בחר תיקיית תוכנה להתרה" };
        if (dlg.ShowDialog(this) == true) AddAllowed(dlg.FolderName, true, "ידני");
      }, true),
      Btn("הוסף קובץ…", () =>
      {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "בחר קובץ EXE להתרה", Filter = "תוכנות (*.exe)|*.exe|הכל|*.*" };
        if (dlg.ShowDialog(this) == true) AddFile(dlg.FileName);
      })));
    if (Cfg.Allowed.Count == 0) lsp.Children.Add(Para(Cfg.AllowProgramFiles ? "הרשימה ריקה – מותר רק Program Files ו-Windows." : "הרשימה ריקה – רק Windows עצמו ירוץ בחשבון היעד.", Warn));
    foreach (var a in Cfg.Allowed.ToList())
    {
      var entry = a;
      var icon = a.IsPublisher ? "\uE8D7" : a.IsFolder ? "\uE8B7" : "\uE7C3";
      var kind = a.IsPublisher ? $"חתימה: {a.Product}" : a.IsFolder ? "תיקייה" : "קובץ";
      var g = Theme.Glyph(icon, 15, Theme.Accent); g.Margin = new Thickness(0, 0, 8, 0);
      var r = Row(g, PathText(a.Path), Small($"({kind} · {a.Source})"));
      r.Children.Add(Btn("הסר", () => { Cfg.Allowed.Remove(entry); MarkDirty(); Refresh(); }));
      lsp.Children.Add(r);
      bool writable = !a.IsPublisher && ((Cfg.TargetSids.Count > 0 && FolderAcl.UserCanWrite(a.Path, Cfg.TargetSids)) || InUserProfile(a.Path));
      if (writable)
      {
        var w = Row(Small("⚠ חשבון היעד יכול לכתוב לכאן – הוא יוכל להחליף/להעתיק לכאן כל תוכנה ולהריץ אותה.", Warn),
          Btn("נעל כתיבה", () => { Say(FolderAcl.LockWrite(entry.Path, Cfg.TargetSids)); Refresh(); }, glyph: "\uE72E"),
          Btn("איך להעביר ל-Program Files", () => Say(AppDataGuide(entry.Path)), glyph: "\uE946"));
        w.Margin = new Thickness(24, 0, 0, 8);
        lsp.Children.Add(w);
      }
    }
    sp.Children.Add(list);

    var pk = Card(H2("אפליקציות חנות מותרות (במצב מחמיר)", "\uE719"),
      Small("רלוונטי רק אם בלשונית \"רשימה לבנה\" הופעל \"הגבל גם אפליקציות חנות\". קידומות של שמות חבילות, מופרדות בפסיק. ברירת מחדל: Microsoft.Windows.Photos_ (תמונות)."));
    var tb = new TextBox { Text = string.Join(", ", Cfg.AllowedPackages), FlowDirection = FlowDirection.LeftToRight, Margin = new Thickness(0, 4, 0, 4) };
    tb.LostFocus += (_, _) =>
    {
      var v = tb.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
      if (!v.SequenceEqual(Cfg.AllowedPackages)) { Cfg.AllowedPackages = v; MarkDirty(); }
    };
    ((StackPanel)pk.Child).Children.Add(tb);
    sp.Children.Add(pk);
    return sp;
  }

  static TextBlock PathText(string p) => new() { Text = p, FlowDirection = FlowDirection.LeftToRight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };

  internal static string AppDataGuide(string path) =>
    $"\"{path}\"\nנמצא במקום שהמשתמש יכול לכתוב אליו (כמו AppData). היתר לפי נתיב שם = פרצה: אפשר להחליף את הקובץ בכל תוכנה אחרת עם אותו שם.\n\n" +
    "הפתרונות (מהנקי ביותר):\n\n" +
    "1. התקנה לכל המשתמשים: לרוב התוכנות שמותקנות ב-AppData (Zoom, Chrome, Discord וכו') יש קובץ התקנה רשמי \"לכל המשתמשים\" (System / Enterprise / MSI). מתקינים אותו פעם אחת מחשבון המנהל – והוא נכנס ל-Program Files.\n\n" +
    "2. העברה ידנית: מחשבון המנהל, להעתיק את כל התיקייה מ-AppData ל-C:\\Program Files, וליצור קיצור דרך חדש בשולחן העבודה. רוב התוכנות יעבדו משם; הן רק לא יתעדכנו לבד עד שתעדכן כמנהל.\n\n" +
    "3. winget: בשורת פקודה של מנהל, למשל:\n   winget install Zoom.Zoom --scope machine\n   מוריד את גרסת המחשב ומתקין ב-Program Files.\n\n" +
    "אם אי אפשר להעביר: להתיר לפי חתימה דיגיטלית (\"הוסף קובץ…\" מציע זאת אוטומטית לקבצים חתומים), או \"נעל כתיבה\" על התיקייה.";

  /// <summary>הוספת קובץ: בתיקייה שהמשתמש יכול לכתוב אליה – מציע היתר לפי חתימה במקום לפי נתיב</summary>
  void AddFile(string file)
  {
    var dir = Path.GetDirectoryName(file)!;
    bool risky = InUserProfile(file) || (Cfg.TargetSids.Count > 0 && FolderAcl.UserCanWrite(dir, Cfg.TargetSids));
    if (!risky)
    {
      int c = Dialogs.Choose($"מה להתיר?\n\n{file}", "את כל התיקייה (מומלץ – תוכנות רבות מפעילות כמה קבצים ואחרת קורסות)", "רק את הקובץ הזה");
      if (c == 0) AddAllowed(dir, true, "ידני");
      else if (c == 1) AddAllowed(file, false, "ידני");
      return;
    }
    Signing.Info? info;
    SetStatus("⏳ בודק חתימה דיגיטלית…");
    try { info = Signing.Read(file); } finally { SetStatus(null); }
    if (info != null)
    {
      int c = Dialogs.Choose($"הקובץ נמצא בתיקייה שהמשתמש יכול לכתוב אליה.\n\n{file}\n\nהוא חתום דיגיטלית:\nמפרסם: {info.Publisher}\nמוצר: {info.Product}\n\nהיתר לפי חתימה מאשר רק קבצים שהמפרסם הזה חתם עליהם – גם אם יחליפו את הקובץ, תוכנה אחרת לא תרוץ.",
        "התר לפי חתימה (מומלץ)", "התר לפי נתיב בכל זאת (פחות בטוח)", "הצג איך להעביר ל-Program Files");
      if (c == 0)
      {
        if (Cfg.Allowed.Any(a => a.IsPublisher && a.Publisher == info.Publisher && a.Product == info.Product)) { Say("ההיתר הזה כבר קיים."); return; }
        Cfg.Allowed.Add(new AllowedEntry { Path = file, IsFolder = false, Source = "חתימה", Publisher = info.Publisher, Product = info.Product, Signer = info.Signer });
        MarkDirty(); Refresh();
      }
      else if (c == 1) AddAllowed(file, false, "ידני");
      else if (c == 2) Say(AppDataGuide(file));
    }
    else
    {
      int c = Dialogs.Choose($"הקובץ נמצא בתיקייה שהמשתמש יכול לכתוב אליה, והוא לא חתום דיגיטלית – אי אפשר להתיר אותו לפי חתימה.\n\n{file}",
        "הצג איך להעביר ל-Program Files (מומלץ)", "התר לפי נתיב בכל זאת (פרצה)");
      if (c == 0) Say(AppDataGuide(file));
      else if (c == 1) AddAllowed(file, false, "ידני");
    }
  }

  internal void AddAllowed(string path, bool folder, string source, bool refresh = true)
  {
    path = path.TrimEnd('\\');
    if (folder && AppProfiles.IsTooBroad(path))
    {
      Dialogs.Say($"התיקייה \"{path}\" רחבה מדי (כונן שלם / Program Files / תיקיית משתמש). בחר את התיקייה של התוכנה עצמה.", warn: true);
      return;
    }
    if (Cfg.Allowed.Any(a => !a.IsPublisher && a.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) return;
    Cfg.Allowed.Add(new AllowedEntry { Path = path, IsFolder = folder, Source = source });
    MarkDirty();
    if (refresh) Refresh();
  }
}
