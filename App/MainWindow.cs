using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace AAA;

/// <summary>החלון הראשי. כל השינויים נשמרים בזיכרון עד "החל שינויים" (חוץ מפעולות מיידיות שמסומנות כך)</summary>
public partial class MainWindow : Window
{
  internal Config Cfg;
  internal List<LocalUser> UsersList = new();
  readonly ContentControl _content = new();
  readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
  readonly Button _apply;
  readonly ListBox _nav = new();
  bool _dirty;
  bool _busy;

  static readonly Brush Bg = Hex("#F4F6FA");
  static readonly Brush NavBg = Hex("#1E2A44");
  static readonly Brush Accent = Hex("#2D6CDF");
  static readonly Brush Muted = Hex("#5B6475");
  static readonly Brush Warn = Hex("#B3261E");
  static readonly Brush Good = Hex("#1B7F3B");

  static Brush Hex(string h) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(h)!; b.Freeze(); return b; }

  record PageDef(string Title, Func<UIElement> Build);
  readonly List<PageDef> _pages;

  public MainWindow()
  {
    Title = "AAA – נעילת מחשב לפי רשימה לבנה";
    Width = 1150; Height = 780; MinWidth = 900; MinHeight = 600;
    FlowDirection = FlowDirection.RightToLeft;
    FontFamily = new FontFamily("Segoe UI");
    FontSize = 14;
    Background = Bg;
    WindowStartupLocation = WindowStartupLocation.CenterScreen;

    Cfg = Config.Load();
    try { UsersList = Users.List(); } catch (Exception ex) { Log.Write("Users.List: " + ex.Message); }

    _pages = new()
    {
      new("1. חשבונות יעד", PageUsers),
      new("2. מדיניות (GPO)", PagePolicies),
      new("3. ביוס ו-WinRE", PageRecovery),
      new("4. ניקוי Windows", PageDebloat),
      new("5. תוכנות מורשות", PageApps),
      new("6. רשימה לבנה", PageWhitelist),
      new("7. יומן חסימות", PageLog),
      new("ביטול הכל", PageRemoveAll),
    };

    // ----- ניווט -----
    _nav.Background = NavBg; _nav.BorderThickness = new Thickness(0); _nav.Width = 210;
    _nav.Foreground = Brushes.White; _nav.FontSize = 15;
    foreach (var p in _pages)
      _nav.Items.Add(new ListBoxItem { Content = p.Title, Padding = new Thickness(14, 10, 14, 10), Foreground = Brushes.White });
    _nav.SelectionChanged += (_, _) => ShowPage();

    var header = new StackPanel { Background = NavBg };
    header.Children.Add(new TextBlock { Text = "AAA", FontSize = 30, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(14, 14, 14, 0) });
    header.Children.Add(new TextBlock { Text = "נעילת מחשב לפי רשימה לבנה", Foreground = Hex("#AFC3E8"), Margin = new Thickness(14, 0, 14, 12), TextWrapping = TextWrapping.Wrap });
    var navPanel = new DockPanel { Background = NavBg };
    DockPanel.SetDock(header, Dock.Top);
    navPanel.Children.Add(header);
    navPanel.Children.Add(_nav);

    // ----- סרגל תחתון -----
    _apply = new Button { Content = "החל שינויים", Padding = new Thickness(22, 8, 22, 8), Margin = new Thickness(10), Background = Accent, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, BorderThickness = new Thickness(0) };
    _apply.Click += async (_, _) => await ApplyAll();
    var bottom = new DockPanel { Background = Brushes.White };
    DockPanel.SetDock(_apply, Dock.Right);
    bottom.Children.Add(_apply);
    bottom.Children.Add(_status);

    var main = new DockPanel();
    DockPanel.SetDock(navPanel, Dock.Left);
    DockPanel.SetDock(bottom, Dock.Bottom);
    main.Children.Add(navPanel);
    main.Children.Add(bottom);
    main.Children.Add(new ScrollViewer { Content = _content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(22, 16, 22, 16) });
    Content = main;

    _nav.SelectedIndex = Cfg.TargetSids.Count == 0 ? 0 : 1;
    SetStatus(null);
    Closing += (_, e) =>
    {
      if (_dirty && MessageBox.Show("יש שינויים שלא הוחלו. לצאת בלי להחיל?", "AAA", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        e.Cancel = true;
    };
  }

  void ShowPage()
  {
    if (_nav.SelectedIndex < 0) return;
    try { _content.Content = _pages[_nav.SelectedIndex].Build(); }
    catch (Exception ex) { _content.Content = Para("שגיאה בטעינת הדף: " + ex.Message, Warn); Log.Write("Page: " + ex); }
  }

  internal void Refresh() => ShowPage();

  internal void MarkDirty() { _dirty = true; SetStatus(null); }

  internal void SetStatus(string? text)
  {
    if (text != null) { _status.Text = text; _status.Foreground = Muted; return; }
    var targets = Cfg.TargetSids.Select(s => UsersList.FirstOrDefault(u => u.Sid == s)?.Name ?? s);
    _status.Text = (_dirty ? "● יש שינויים שלא הוחלו.  " : "") +
      (Cfg.TargetSids.Count == 0 ? "לא נבחר חשבון יעד" : "חשבונות יעד: " + string.Join(", ", targets));
    _status.Foreground = _dirty ? Warn : Muted;
  }

  // ===================== עזרי ממשק =====================

  internal static TextBlock H1(string t) => new() { Text = t, FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) };
  internal static TextBlock H2(string t) => new() { Text = t, FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 6) };
  internal static TextBlock Para(string t, Brush? color = null, double size = 14) =>
    new() { Text = t, TextWrapping = TextWrapping.Wrap, Foreground = color ?? Brushes.Black, FontSize = size, Margin = new Thickness(0, 0, 0, 6) };
  internal static TextBlock Small(string t, Brush? color = null) => Para(t, color ?? Muted, 12.5);

  internal static Border Card(params UIElement[] children)
  {
    var sp = new StackPanel();
    foreach (var c in children) sp.Children.Add(c);
    return new Border
    {
      Background = Brushes.White, CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12),
      Margin = new Thickness(0, 0, 0, 12), BorderBrush = Hex("#DDE3EC"), BorderThickness = new Thickness(1), Child = sp,
    };
  }

  internal static Button Btn(string text, Action onClick, bool primary = false)
  {
    var b = new Button { Content = text, Padding = new Thickness(14, 5, 14, 5), Margin = new Thickness(0, 4, 8, 4), HorizontalAlignment = HorizontalAlignment.Left };
    if (primary) { b.Background = Accent; b.Foreground = Brushes.White; b.BorderThickness = new Thickness(0); }
    b.Click += (_, _) => onClick();
    return b;
  }

  internal static StackPanel Row(params UIElement[] items)
  {
    var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
    foreach (var i in items) sp.Children.Add(i);
    return sp;
  }

  internal CheckBox Check(string text, bool isChecked, Action<bool> onChange, bool enabled = true, bool dirty = true)
  {
    var cb = new CheckBox { Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap }, IsChecked = isChecked, IsEnabled = enabled, Margin = new Thickness(0, 4, 0, 2), VerticalContentAlignment = VerticalAlignment.Center };
    cb.Checked += (_, _) => { onChange(true); if (dirty) MarkDirty(); };
    cb.Unchecked += (_, _) => { onChange(false); if (dirty) MarkDirty(); };
    return cb;
  }

  /// <summary>מריץ פעולה ארוכה ברקע ומציג סטטוס</summary>
  internal async Task Busy(string what, Func<Task> work)
  {
    if (_busy) { MessageBox.Show("פעולה אחרת עדיין רצה.", "AAA"); return; }
    _busy = true; _apply.IsEnabled = false;
    SetStatus("⏳ " + what);
    System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
    try { await work(); }
    catch (Exception ex) { Log.Write(what + ": " + ex); MessageBox.Show("שגיאה: " + ex.Message, "AAA", MessageBoxButton.OK, MessageBoxImage.Error); }
    finally { System.Windows.Input.Mouse.OverrideCursor = null; _busy = false; _apply.IsEnabled = true; SetStatus(null); }
  }

  internal static bool Confirm(string text) =>
    MessageBox.Show(text, "AAA", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

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
    if (Cfg.TargetSids.Contains(Users.CurrentSid()))
    { MessageBox.Show("החשבון שאתה מחובר אליו כעת מסומן כחשבון יעד. הסר אותו ברשימת חשבונות היעד.", "AAA"); return; }
    if (Cfg.AppLockerEnabled && !Cfg.AppLockerAuditOnly && Cfg.Allowed.Count == 0)
      if (!Confirm("AppLocker במצב אכיפה בלי שום תוכנה מורשית: משתמש היעד יוכל להריץ רק את Windows עצמו. להמשיך?")) return;

    var msgs = new List<string>();
    await Busy("מחיל שינויים…", () => Task.Run(() =>
    {
      msgs.AddRange(PolicyEngine.Apply(Cfg, UsersList));
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
    MessageBox.Show("השינויים הוחלו.\n\n" + string.Join("\n", msgs.Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => "• " + m)), "AAA", MessageBoxButton.OK, MessageBoxImage.Information);
  }
}
