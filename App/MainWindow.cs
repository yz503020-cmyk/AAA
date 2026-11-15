using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace AAA;

/// <summary>החלון הראשי – באותו מבנה ועיצוב כמו HGEC: סרגל עליון, תפריט צד עם סמלים, כרטיסים מעוגלים, מתגים.</summary>
public partial class MainWindow : Window
{
  internal Config Cfg;
  internal List<LocalUser> UsersList = new();
  readonly ContentControl _host = new();
  readonly TextBlock _status = new() { FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
  readonly Button _apply;
  bool _dirty;
  bool _busy;
  bool _adminWarned;
  int _sel;
  string _glyph = "";

  static readonly Brush Ink = Rgb(30, 41, 59);
  static readonly Brush Muted = Rgb(100, 116, 139);
  static readonly Brush Warn = Rgb(185, 28, 28);
  static readonly Brush Good = Rgb(21, 128, 61);
  static readonly Brush Line = Rgb(229, 231, 235);

  static Brush Rgb(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }

  record PageDef(string Title, string Glyph, Func<UIElement> Build);
  readonly List<PageDef> _pages;
  readonly List<Action> _paints = new();

  public MainWindow()
  {
    Title = "AAA – נעילת מחשב לפי רשימה לבנה";
    Width = 1100; Height = 760; MinWidth = 880; MinHeight = 560;
    FlowDirection = FlowDirection.RightToLeft;
    FontSize = 14;
    Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
    FontFamily = new FontFamily("Segoe UI");
    Foreground = Ink;
    WindowStartupLocation = WindowStartupLocation.CenterScreen;

    Cfg = Config.Load();
    try { UsersList = Users.List(); } catch (Exception ex) { Log.Write("Users.List: " + ex.Message); }

    _pages = new()
    {
      new("חשבונות יעד", "", PageUsers),
      new("מדיניות GPO", "", PagePolicies),
      new("ביוס ו-WinRE", "", PageRecovery),
      new("ניקוי Windows", "", PageDebloat),
      new("תוכנות מורשות", "", PageApps),
      new("רשימה לבנה", "", PageWhitelist),
      new("יומן חסימות", "", PageLog),
      new("ביטול הכל", "", PageRemoveAll),
    };

    // ----- סרגל עליון -----
    _apply = Soft("החל שינויים", "", async () => await ApplyAll());
    Theme.Tipped(_apply, "מחיל על המחשב את כל החסימות וההגדרות שבחרת. יש ללחוץ עליו אחרי שינוי.");
    var top = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.White };
    top.Children.Add(_apply);
    top.Children.Add(_status);
    foreach (FrameworkElement ch in top.Children) { ch.Margin = new Thickness(8, 10, 0, 10); ch.VerticalAlignment = VerticalAlignment.Center; }
    var topBar = new Border { Child = top, Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1) };

    // ----- תפריט צד -----
    var side = new Border { Width = 92, Background = Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(0, 0, 1, 0) };
    var sideSp = new StackPanel();
    side.Child = new ScrollViewer { Content = sideSp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    var logoImg = new Image { Source = Logo.Src, Width = 54, Height = 54, Margin = new Thickness(0, 18, 0, 16), HorizontalAlignment = HorizontalAlignment.Center };
    RenderOptions.SetBitmapScalingMode(logoImg, BitmapScalingMode.HighQuality);
    sideSp.Children.Add(logoImg);
    var mute = Rgb(107, 114, 128);
    var selBg = Rgb(239, 246, 255);
    var hoverBg = Rgb(243, 244, 246);
    for (int i = 0; i < _pages.Count; i++)
    {
      int k = i;
      var p = _pages[i];
      var ic = Theme.Glyph(p.Glyph, 20, mute);
      var tx = new TextBlock { Text = p.Title, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = mute, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
      var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
      col.Children.Add(ic); col.Children.Add(tx);
      var item = new Border { CornerRadius = new CornerRadius(12), Padding = new Thickness(4, 10, 4, 10), Margin = new Thickness(8, 2, 8, 2), Background = Brushes.Transparent, Child = col };
      var bar = new Border { Width = 3, CornerRadius = new CornerRadius(2), Background = Theme.SideAccent, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 14), Visibility = Visibility.Collapsed };
      var cell = new Grid { Background = Brushes.Transparent, Cursor = System.Windows.Input.Cursors.Hand };
      cell.Children.Add(item); cell.Children.Add(bar);
      void Paint()
      {
        bool sel = _sel == k;
        item.Background = sel ? selBg : Brushes.Transparent;
        ic.Foreground = tx.Foreground = sel ? Theme.SideAccent : mute;
        bar.Visibility = sel ? Visibility.Visible : Visibility.Collapsed;
      }
      _paints.Add(Paint);
      cell.MouseEnter += (_, _) => { if (_sel != k) item.Background = hoverBg; };
      cell.MouseLeave += (_, _) => Paint();
      cell.MouseLeftButtonUp += (_, _) => Select(k);
      sideSp.Children.Add(cell);
    }

    var foot = new TextBlock
    {
      Text = "האחריות על המשתמש בלבד, בעז\"ה שישמש לדברים חיוביים.", FontSize = 12, Foreground = Rgb(148, 163, 184),
      HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 7, 0, 9),
    };

    var root = new DockPanel();
    DockPanel.SetDock(foot, Dock.Bottom);
    DockPanel.SetDock(topBar, Dock.Top);
    DockPanel.SetDock(side, Dock.Left); // ב-RTL: מופיע מימין
    root.Children.Add(foot);
    root.Children.Add(topBar);
    root.Children.Add(side);
    root.Children.Add(_host);
    Content = root;

    Select(Cfg.TargetSids.Count == 0 ? 0 : 1);
    SetStatus(null);
    Closing += (_, e) =>
    {
      if (_busy) { e.Cancel = true; Dialogs.Say("פעולה עדיין מתבצעת. המתן שתסתיים ואז סגור את החלון."); return; }
      if (_dirty && !Dialogs.Say("יש שינויים שלא הוחלו. לצאת בלי להחיל?", yesNo: true, warn: true)) e.Cancel = true;
    };
  }

  void Select(int k)
  {
    _sel = k;
    foreach (var p in _paints) p();
    Refresh();
  }

  internal void Refresh()
  {
    var p = _pages[_sel];
    _glyph = p.Glyph;
    UIElement body;
    try { body = p.Build(); }
    catch (Exception ex) { body = Para("שגיאה בטעינת הדף: " + ex.Message, Warn); Log.Write("Page: " + ex); }
    double off = (_host.Content as ScrollViewer)?.VerticalOffset ?? 0;
    var sv = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    if (body is FrameworkElement fe) fe.Margin = new Thickness(28, 22, 28, 22);
    _host.Content = sv;
    if (off > 0) sv.Loaded += (_, _) => sv.ScrollToVerticalOffset(off);
  }

  internal void MarkDirty() { _dirty = true; SetStatus(null); }

  internal void SetStatus(string? text)
  {
    if (text != null) { _status.Text = text; _status.Foreground = Muted; return; }
    var targets = Cfg.TargetSids.Select(s => UsersList.FirstOrDefault(u => u.Sid == s)?.Name ?? s);
    _status.Text = (_dirty ? "● יש שינויים שלא הוחלו – לחץ \"החל שינויים\".   " : "") +
      (Cfg.TargetSids.Count == 0 ? "לא נבחר חשבון יעד" : "חשבונות יעד: " + string.Join(", ", targets));
    _status.Foreground = _dirty ? Warn : Muted;
  }

  // ===================== עזרי ממשק (כמו ב-HGEC) =====================

  /// <summary>כותרת הדף עם סמל בתוך ריבוע מעוגל</summary>
  internal UIElement H1(string t)
  {
    var h = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
    h.Children.Add(Theme.Badge(_glyph));
    h.Children.Add(new TextBlock { Text = t, FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Ink, VerticalAlignment = VerticalAlignment.Center });
    return h;
  }

  internal static UIElement H2(string t, string? glyph = null)
  {
    var tb = new TextBlock { Text = t, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Ink, VerticalAlignment = VerticalAlignment.Center };
    if (glyph == null) { tb.Margin = new Thickness(0, 0, 0, 8); return tb; }
    var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
    var g = Theme.Glyph(glyph, 16, Theme.Accent); g.Margin = new Thickness(0, 0, 8, 0);
    sp.Children.Add(g); sp.Children.Add(tb);
    return sp;
  }

  internal static TextBlock Para(string t, Brush? color = null, double size = 14) =>
    new() { Text = t, TextWrapping = TextWrapping.Wrap, Foreground = color ?? Ink, FontSize = size, Margin = new Thickness(0, 0, 0, 8), LineHeight = size * 1.45 };
  internal static TextBlock Small(string t, Brush? color = null) => Para(t, color ?? Muted, 12.5);

  internal static Border Card(params UIElement[] children)
  {
    var sp = new StackPanel();
    foreach (var c in children) sp.Children.Add(c);
    return new Border
    {
      Child = sp, Background = Brushes.White, CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 12, 16, 12),
      Margin = new Thickness(0, 0, 0, 12), BorderBrush = new SolidColorBrush(Color.FromRgb(230, 235, 242)), BorderThickness = new Thickness(1),
      Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 2, Opacity = 0.05, Color = Color.FromRgb(30, 41, 59) },
    };
  }

  /// <summary>כפתור: primary = כחול מלא; אחרת – הסגנון העדין של HGEC (רקע תכלת, מסגרת דקה)</summary>
  internal static Button Btn(string text, Action onClick, bool primary = false, string? glyph = null)
  {
    glyph ??= text.Contains("הוסף") || text.Contains("התר") ? "" : text.Contains("הסר") ? "" : text.Contains("סרוק") || text.Contains("חפש") ? "" : text.Contains("רענן") ? "" : null;
    Button b;
    if (primary)
    {
      b = new Button { Content = glyph == null ? text : Theme.IconText(glyph, text), HorizontalAlignment = HorizontalAlignment.Left };
      b.Click += (_, _) => onClick();
    }
    else b = Soft(text, glyph, onClick);
    b.Margin = new Thickness(0, 4, 8, 4);
    return b;
  }

  internal static Button Soft(string text, string? glyph, Action onClick)
  {
    var fg = Color.FromRgb(52, 90, 158);
    var b = new Button { Style = Theme.Soft(typeof(Button)), HorizontalAlignment = HorizontalAlignment.Left };
    Theme.Tint(b, Color.FromRgb(238, 243, 252), fg, Color.FromRgb(206, 220, 242));
    b.Content = glyph == null ? new TextBlock { Text = text, Foreground = new SolidColorBrush(fg) } : Theme.IconText(glyph, text, new SolidColorBrush(fg));
    b.Click += (_, _) => onClick();
    return b;
  }

  internal static StackPanel Row(params UIElement[] items)
  {
    var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
    foreach (var i in items) sp.Children.Add(i);
    return sp;
  }

  /// <summary>מתג הפעלה/כיבוי (CheckBox בסגנון מתג של HGEC)</summary>
  internal CheckBox Check(string text, bool isChecked, Action<bool> onChange, bool enabled = true, bool dirty = true, string? tip = null)
  {
    var cb = new CheckBox
    {
      Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 700 },
      IsChecked = isChecked, IsEnabled = enabled, Margin = new Thickness(0, 6, 0, 2), Opacity = enabled ? 1 : 0.55,
    };
    cb.Checked += (_, _) => { onChange(true); if (dirty) MarkDirty(); };
    cb.Unchecked += (_, _) => { onChange(false); if (dirty) MarkDirty(); };
    if (tip != null) Theme.Tipped(cb, tip);
    return cb;
  }

  /// <summary>מתג + שורת הסבר מתחתיו</summary>
  internal UIElement Toggle(string text, string? desc, bool isChecked, Action<bool> onChange, bool enabled = true, bool dirty = true, string? warn = null)
  {
    var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
    sp.Children.Add(Check(text, isChecked, onChange, enabled, dirty));
    if (!string.IsNullOrEmpty(desc)) { var d = Small(desc); d.Margin = new Thickness(52, 0, 0, 2); sp.Children.Add(d); }
    if (warn != null) { var w = Small("⚠ " + warn, Warn); w.Margin = new Thickness(52, 0, 0, 2); sp.Children.Add(w); }
    return sp;
  }

  internal static TextBox Code(string text) => new()
  {
    Text = text, IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(0, 2, 0, 8),
  };

  /// <summary>מריץ פעולה ארוכה ברקע ומציג סטטוס</summary>
  internal async Task Busy(string what, Func<Task> work)
  {
    if (_busy) { Dialogs.Say("פעולה אחרת עדיין רצה."); return; }
    _busy = true; _apply.IsEnabled = false;
    SetStatus("⏳ " + what);
    System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
    try { await work(); }
    catch (Exception ex) { Log.Write(what + ": " + ex); Dialogs.Say("שגיאה: " + ex.Message, warn: true); }
    finally { System.Windows.Input.Mouse.OverrideCursor = null; _busy = false; _apply.IsEnabled = true; SetStatus(null); }
  }

  internal static bool Confirm(string text) => Dialogs.Say(text, yesNo: true, warn: true);
  internal static void Say(string text) => Dialogs.Say(text);

  /// <summary>שומר מיד דגלים של פעולות מיידיות, בלי להחיל את שאר השינויים שבהמתנה</summary>
  internal void SaveImmediateFlags(Action<Config> set)
  {
    var saved = Config.Load();
    set(saved);
    set(Cfg);
    saved.Save();
  }

  // ===================== החלה =====================

  async Task ApplyAll()
  {
    if (Cfg.TargetSids.Count == 0 && (Cfg.Policies.Any(id => PolicyCatalog.All.FirstOrDefault(p => p.Id == id)?.UserScoped == true) || Cfg.WatchdogEnabled || Cfg.DenyTools.Count > 0))
      if (!Confirm("לא נבחר חשבון יעד: חסימות המשתמש, הוואצ'דוג והכלים החסומים לא יחולו על אף אחד. להמשיך?")) return;
    var adminTargets = UsersList.Where(u => u.IsAdmin && Cfg.TargetSids.Contains(u.Sid)).Select(u => u.Name).ToList();
    if (adminTargets.Count > 0 && !_adminWarned)
    {
      bool meToo = Cfg.TargetSids.Contains(Users.CurrentSid());
      if (!Confirm($"חשבונות מנהל מסומנים כחשבון יעד: {string.Join(", ", adminTargets)}" + (meToo ? "\n(כולל החשבון שאתה מחובר אליו עכשיו)" : "") +
        $"\n\nהחסימות יחולו עליהם. התוכנה עצמה תישאר פתוחה תמיד מ-{Install.Dir} (קיצור דרך בתפריט התחל ובשולחן העבודה), כך שתמיד אפשר לחזור ול\"ביטול הכל\".\n\nלהמשיך?")) return;
      _adminWarned = true;
    }
    if (!Install.IsRunningInstalled() && Cfg.TargetSids.Contains(Users.CurrentSid()))
    { Say($"החשבון שלך מסומן כחשבון יעד, אבל התוכנה לא רצה מתיקיית ההתקנה ({Install.Exe}). סגור ופתח אותה משם, ואז החל שוב."); return; }
    if (Cfg.AppLockerEnabled && !Cfg.AppLockerAuditOnly && !Cfg.AllowProgramFiles && Cfg.Allowed.Count == 0)
      if (!Confirm("AppLocker במצב אכיפה בלי שום תוכנה מורשית: משתמש היעד יוכל להריץ רק את Windows עצמו. להמשיך?")) return;

    var msgs = new List<string>();
    await Busy("מחיל שינויים…", () => Task.Run(() =>
    {
      msgs.AddRange(PolicyEngine.Apply(Cfg, UsersList));
      if (Cfg.AllowProgramFiles && (Cfg.AppLockerEnabled || Cfg.WatchdogEnabled))
      {
        Dispatcher.Invoke(() => SetStatus("⏳ סורק תיקיות ב-Program Files שמשתמש רגיל יכול לכתוב אליהן…"));
        Cfg.PfWritable = ProgramFilesScan.FindWritable(Cfg.TargetSids);
        if (Cfg.PfWritable.Count > 0) msgs.Add($"נמצאו {Cfg.PfWritable.Count} תיקיות פתוחות לכתיבה ב-Program Files – הן הוחרגו מההיתר.");
      }
      if (Cfg.AppLockerEnabled) msgs.Add(AppLocker.Apply(Cfg).msg);
      else if (Cfg.AppLockerApplied) msgs.Add(AppLocker.Clear(Cfg).msg);
      Cfg.Save();
      if (Cfg.WatchdogEnabled) msgs.Add(Watchdog.Install());
      else if (Watchdog.IsInstalled()) msgs.Add(Watchdog.Uninstall());
      Cfg.Save();
    }));
    _dirty = false;
    SetStatus(null);
    Refresh();
    Say("השינויים הוחלו.\n\n" + string.Join("\n", msgs.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => "• " + m)));
  }
}
