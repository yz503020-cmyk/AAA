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
    var card = Card();
    var inner = (StackPanel)card.Child;
    var me = Users.CurrentSid();
    if (UsersList.Count == 0) inner.Children.Add(Para("לא נמצאו חשבונות מקומיים.", Warn));
    foreach (var u in UsersList)
    {
      bool selectable = !u.IsAdmin && u.Sid != me;
      var label = u.Name + (u.IsAdmin ? "  (מנהל – לא ניתן לבחירה)" : "") + (u.Sid == me ? "  (החשבון הנוכחי)" : "") + (!u.HasProfile && !u.IsAdmin ? "  – עוד לא התחבר: יש להתחבר אליו פעם אחת לפני החלת חסימות המשתמש" : "");
      var sid = u.Sid;
      inner.Children.Add(Check(label, Cfg.TargetSids.Contains(sid), on =>
      {
        Cfg.TargetSids.Remove(sid);
        if (on) Cfg.TargetSids.Add(sid);
      }, selectable));
    }
    inner.Children.Add(Btn("רענן רשימה", () => { UsersList = Users.List(); Refresh(); }));
    sp.Children.Add(card);
    sp.Children.Add(Card(H2("אין חשבון רגיל?"),
      Para("צור חשבון מקומי רגיל דרך הגדרות ← חשבונות ← משתמשים אחרים, או בשורת פקודה של מנהל:"),
      new TextBox { Text = "net user תלמיד /add", IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 0, 0, 6) },
      Small("אחרי היצירה – להתחבר אליו פעם אחת (כדי שייווצר לו פרופיל), לחזור לחשבון המנהל ולרענן כאן.")));
    return sp;
  }

  // ===================== 2. מדיניות =====================
  UIElement PagePolicies()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("מדיניות (GPO ורישום)"));
    sp.Children.Add(Para("הגדרות מתוך עורך המדיניות (ועוד כמה ערכי רישום שאינם בו) שסוגרות את הדרכים לבטל או לעקוף חסימה. ביטול סימון מחזיר את הערך המקורי שהיה במחשב לפני שהתוכנה שינתה אותו."));
    sp.Children.Add(Row(
      Btn("סמן את המומלצים", () => { foreach (var p in PolicyCatalog.All.Where(p => p.Recommended)) Cfg.Policies.Add(p.Id); MarkDirty(); Refresh(); }),
      Btn("נקה הכל", () => { Cfg.Policies.Clear(); MarkDirty(); Refresh(); })));
    foreach (var g in PolicyCatalog.Groups)
    {
      var card = Card(H2(g));
      var inner = (StackPanel)card.Child;
      foreach (var p in PolicyCatalog.All.Where(p => p.Group == g))
      {
        var id = p.Id;
        var title = p.Title + (p.UserScoped ? "  [חשבון היעד]" : "  [כל המחשב]") + (p.Recommended ? "" : "  (לא מומלץ כברירת מחדל)");
        inner.Children.Add(Check(title, Cfg.Policies.Contains(id), on => { if (on) Cfg.Policies.Add(id); else Cfg.Policies.Remove(id); }));
        var d = Small(p.Desc); d.Margin = new Thickness(24, 0, 0, 4); inner.Children.Add(d);
        if (p.Warning != null) { var w = Small("⚠ " + p.Warning, Warn); w.Margin = new Thickness(24, 0, 0, 6); inner.Children.Add(w); }
      }
      sp.Children.Add(card);
    }
    sp.Children.Add(Card(H2("לא כאן – ובכל זאת מכוסה"),
      Small("PowerShell, הסקריפטים, WSL ועוד – נחסמים למשתמש היעד בלשונית \"רשימה לבנה\" (כלים חסומים), כי אין להם הגדרת GPO אמינה."),
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
    var reText = re switch { Recovery.ReState.Disabled => "מושבתת ✓", Recovery.ReState.Enabled => "פעילה ✗", _ => "לא ידוע" };
    var info = Card(H2("מצב נוכחי"),
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
        MessageBox.Show(res.msg, "AAA"); Refresh();
      }, true));
    else
      row.Children.Add(Btn("הפעל מחדש את WinRE", async () =>
      {
        (bool ok, string msg) res = default;
        var copy = Cfg;
        await Busy("מפעיל את WinRE…", () => Task.Run(() => res = Recovery.EnableWinRe(copy)));
        SaveImmediateFlags(c => { c.WinReDisabledByUs = copy.WinReDisabledByUs; c.BcdRecoveryDisabledByUs = copy.BcdRecoveryDisabledByUs; });
        MessageBox.Show(res.msg, "AAA"); Refresh();
      }));
    if (Recovery.IsUefi())
      row.Children.Add(Btn("הפעל מחדש לתוך הביוס", () =>
      {
        if (Confirm("המחשב יופעל מחדש בעוד 5 שניות ישירות למסך הגדרות הקושחה (UEFI). לשמור עבודה פתוחה קודם. להמשיך?")) Recovery.RebootToFirmware();
      }));
    infoSp.Children.Add(row);
    sp.Children.Add(info);

    var list = Card(H2("רשימת חובה – נעילת הביוס (לסמן אחרי ביצוע)"));
    var lsp = (StackPanel)list.Child;
    foreach (var it in Recovery.BiosChecklist)
    {
      var id = it.Id;
      bool auto = id == "winre";
      bool val = auto ? re == Recovery.ReState.Disabled : Cfg.BiosChecklist.GetValueOrDefault(id);
      var cb = Check(it.Title, val, on => SaveImmediateFlags(c => c.BiosChecklist[id] = on), !auto, dirty: false);
      cb.FontWeight = FontWeights.SemiBold;
      lsp.Children.Add(cb);
      var d = Small(it.Detail); d.Margin = new Thickness(24, 0, 0, 8); lsp.Children.Add(d);
    }
    lsp.Children.Add(Small("הסימונים כאן הם רשימת תיוג בשבילך בלבד ונשמרים מיד.", Muted));
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
        foreach (var it in Debloat.Items.Where(i => i.Recommended && (_installed?.Contains(i.Id) ?? false))) _toRemove.Add(i.Id);
        Refresh();
      }, true),
      Btn("הסר את המסומנים", async () =>
      {
        if (_installed == null || _toRemove.Count == 0) { MessageBox.Show("סרוק קודם וסמן מה להסיר.", "AAA"); return; }
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
        MessageBox.Show("ההסרה הסתיימה." + (left.Count > 0 ? "\n\nעדיין מותקנים (ייתכן שצריך הפעלה מחדש, או שהמערכת מונעת הסרה):\n" + string.Join("\n", left) : "") +
          (errs.Count > 0 ? "\n\nהודעות:\n" + string.Join("\n", errs) : ""), "AAA");
      })));

    var card = Card();
    var inner = (StackPanel)card.Child;
    foreach (var it in Debloat.Items)
    {
      var id = it.Id;
      bool? inst = _installed == null ? null : _installed.Contains(id);
      var state = inst == null ? "" : inst.Value ? "  — מותקן" : "  — לא קיים ✓";
      var cb = new CheckBox
      {
        Content = new TextBlock { Text = it.Title + state + (it.Warning != null ? "  ⚠ " + it.Warning : ""), TextWrapping = TextWrapping.Wrap },
        IsChecked = _toRemove.Contains(id), IsEnabled = inst == true, Margin = new Thickness(0, 4, 0, 0),
        Foreground = inst == false ? Good : Brushes.Black,
      };
      cb.Checked += (_, _) => _toRemove.Add(id);
      cb.Unchecked += (_, _) => _toRemove.Remove(id);
      inner.Children.Add(cb);
      if (!string.IsNullOrEmpty(it.Desc)) { var d = Small(it.Desc); d.Margin = new Thickness(24, 0, 0, 2); inner.Children.Add(d); }
    }
    sp.Children.Add(card);
    sp.Children.Add(Card(Small("OneDrive ותוכנות שמותקנות בתוך פרופיל המשתמש לא מוסרות כאן – הן לא ברשימה הלבנה ולכן לא ירוצו.")));
    return sp;
  }

  // ===================== 5. תוכנות מורשות =====================
  readonly Dictionary<string, List<string>> _found = new();

  UIElement PageApps()
  {
    var sp = new StackPanel();
    sp.Children.Add(H1("תוכנות מורשות"));
    sp.Children.Add(Para("זה המאגר המוגדר: רק מה שמופיע כאן (וחלקי Windows עצמו) ירוץ בחשבון היעד. לכל תוכנה מוכרת – \"חפש במחשב\" מוצא את כל התיקיות שהיא צריכה, כולל רכיבים נלווים, כדי שלא תקרוס."));

    foreach (var prof in AppProfiles.All)
    {
      var card = Card(H2(prof.Name), Small(prof.Notes));
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
          var r = Row(new TextBlock { Text = d, FlowDirection = FlowDirection.LeftToRight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
          if (exists) r.Children.Add(Small("✓ ברשימה", Good));
          else r.Children.Add(Btn("הוסף", () => { AddAllowed(dd, true, pname); }));
          inner.Children.Add(r);
        }
        if (dirs.Count > 1)
          inner.Children.Add(Btn("הוסף את כולן", () => { foreach (var d in dirs) AddAllowed(d, true, prof.Name, refresh: false); Refresh(); }, true));
      }
      sp.Children.Add(card);
    }

    // ----- הרשימה עצמה -----
    var list = Card(H2($"מה מותר כעת ({Cfg.Allowed.Count})"));
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
        if (dlg.ShowDialog(this) == true)
        {
          var dir = Path.GetDirectoryName(dlg.FileName)!;
          var ans = MessageBox.Show($"להתיר את כל התיקייה?\n\n{dir}\n\n\"כן\" = כל התיקייה (מומלץ – תוכנות רבות מפעילות כמה קבצים ואחרת קורסות)\n\"לא\" = רק הקובץ שנבחר", "AAA", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
          if (ans == MessageBoxResult.Yes) AddAllowed(dir, true, "ידני");
          else if (ans == MessageBoxResult.No) AddAllowed(dlg.FileName, false, "ידני");
        }
      })));
    if (Cfg.Allowed.Count == 0) lsp.Children.Add(Para("הרשימה ריקה – רק Windows עצמו ירוץ בחשבון היעד.", Warn));
    foreach (var a in Cfg.Allowed.ToList())
    {
      var entry = a;
      bool writable = Cfg.TargetSids.Count > 0 && FolderAcl.UserCanWrite(a.Path, Cfg.TargetSids);
      var r = Row(new TextBlock { Text = (a.IsFolder ? "📁 " : "📄 ") + a.Path, FlowDirection = FlowDirection.LeftToRight, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) },
                  Small($"({a.Source})"));
      r.Children.Add(Btn("הסר", () => { Cfg.Allowed.Remove(entry); MarkDirty(); Refresh(); }));
      lsp.Children.Add(r);
      if (writable)
      {
        var w = Row(Small("⚠ חשבון היעד יכול לכתוב לתיקייה הזו – הוא יוכל להעתיק אליה כל תוכנה ולהריץ אותה.", Warn),
          Btn("נעל כתיבה", () => { MessageBox.Show(FolderAcl.LockWrite(entry.Path, Cfg.TargetSids), "AAA"); Refresh(); }));
        w.Margin = new Thickness(24, 0, 0, 6);
        lsp.Children.Add(w);
      }
    }
    sp.Children.Add(list);

    var pk = Card(H2("אפליקציות חנות מותרות (במצב מחמיר)"),
      Small("רלוונטי רק אם בלשונית \"רשימה לבנה\" סומן \"הגבל גם אפליקציות חנות\". קידומות של שמות חבילות, מופרדות בפסיק. ברירת מחדל: Microsoft.Windows.Photos_ (תמונות)."));
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

  internal void AddAllowed(string path, bool folder, string source, bool refresh = true)
  {
    path = path.TrimEnd('\\');
    if (folder && AppProfiles.IsTooBroad(path))
    {
      MessageBox.Show($"התיקייה \"{path}\" רחבה מדי (כונן שלם / Program Files / תיקיית משתמש). בחר את התיקייה של התוכנה עצמה.", "AAA", MessageBoxButton.OK, MessageBoxImage.Warning);
      return;
    }
    if (Cfg.Allowed.Any(a => a.Path.Equals(path, StringComparison.OrdinalIgnoreCase))) return;
    Cfg.Allowed.Add(new AllowedEntry { Path = path, IsFolder = folder, Source = source });
    MarkDirty();
    if (refresh) Refresh();
  }
}
