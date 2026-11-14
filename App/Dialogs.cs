using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AAA;

/// <summary>חלונות הודעה ובורר מקטעים – באותו עיצוב כמו HGEC</summary>
static class Dialogs
{
  public const string AppName = "AAA";

  /// <summary>הודעה מעוצבת (במקום MessageBox). yesNo: מחזיר true רק אם נלחץ "כן"</summary>
  public static bool Say(string text, string? title = null, bool yesNo = false, bool warn = false)
  {
    var w = new Window
    {
      Title = title ?? AppName,
      SizeToContent = SizeToContent.WidthAndHeight,
      WindowStartupLocation = WindowStartupLocation.CenterScreen,
      FlowDirection = FlowDirection.RightToLeft,
      ResizeMode = ResizeMode.NoResize,
      Topmost = true,
      Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
      FontFamily = new FontFamily("Segoe UI"),
      FontSize = 14,
    };
    var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive && x.IsVisible && x != w);
    if (owner != null) w.Owner = owner;
    var sp = new StackPanel { Margin = new Thickness(16), MinWidth = 260, MaxWidth = 520 };
    var msg = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
    UIElement body = new ScrollViewer { Content = msg, MaxHeight = 460, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    if (warn)
    {
      var row = new DockPanel();
      var g = Theme.Glyph("", 22, new SolidColorBrush(Color.FromRgb(217, 119, 6)));
      g.Margin = new Thickness(0, 0, 10, 0);
      g.VerticalAlignment = VerticalAlignment.Top;
      DockPanel.SetDock(g, Dock.Left);
      row.Children.Add(g);
      row.Children.Add(body);
      body = row;
    }
    sp.Children.Add(body);
    bool res = false;
    var ok = new Button { Content = yesNo ? "כן" : "אישור", IsDefault = true, Padding = new Thickness(10, 4, 10, 4), Style = Theme.AccentButton() };
    ok.Click += (_, _) => { res = true; w.Close(); };
    if (yesNo)
    {
      var no = new Button { Content = "לא", IsCancel = true, Padding = new Thickness(10, 4, 10, 4), Style = Theme.Soft(typeof(Button)) };
      no.Click += (_, _) => w.Close();
      var g2 = new Grid { Margin = new Thickness(0, 12, 0, 0) };
      g2.ColumnDefinitions.Add(new ColumnDefinition());
      g2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
      g2.ColumnDefinitions.Add(new ColumnDefinition());
      Grid.SetColumn(no, 2);
      g2.Children.Add(ok);
      g2.Children.Add(no);
      sp.Children.Add(g2);
    }
    else
    {
      ok.IsCancel = true;
      ok.Margin = new Thickness(0, 12, 0, 0);
      sp.Children.Add(ok);
    }
    w.Content = sp;
    w.ShowDialog();
    return res;
  }

  /// <summary>בחירה בין 3 אפשרויות (מחזיר 0/1/2, או -1 לביטול)</summary>
  public static int Choose(string text, params string[] options)
  {
    var w = new Window
    {
      Title = AppName, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterScreen,
      FlowDirection = FlowDirection.RightToLeft, ResizeMode = ResizeMode.NoResize, Topmost = true,
      Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)), FontFamily = new FontFamily("Segoe UI"), FontSize = 14,
    };
    var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive && x.IsVisible && x != w);
    if (owner != null) w.Owner = owner;
    var sp = new StackPanel { Margin = new Thickness(16), MinWidth = 300, MaxWidth = 520 };
    sp.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
    int res = -1;
    for (int i = 0; i < options.Length; i++)
    {
      int k = i;
      var b = new Button { Content = new TextBlock { Text = options[i], TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(0, 4, 0, 4), HorizontalContentAlignment = HorizontalAlignment.Left, Style = i == 0 ? Theme.AccentButton() : Theme.Soft(typeof(Button)) };
      if (i != 0) Theme.Tint(b, Color.FromRgb(238, 243, 252), Color.FromRgb(52, 90, 158), Color.FromRgb(206, 220, 242));
      b.Click += (_, _) => { res = k; w.Close(); };
      sp.Children.Add(b);
    }
    var cancel = new Button { Content = "ביטול", IsCancel = true, Margin = new Thickness(0, 10, 0, 0), Style = Theme.Soft(typeof(Button)) };
    cancel.Click += (_, _) => w.Close();
    sp.Children.Add(cancel);
    w.Content = sp;
    w.ShowDialog();
    return res;
  }

  /// <summary>בורר מקטעים (במקום כפתורי רדיו)</summary>
  public static UIElement Segment(string[] labels, int sel, Action<int> changed)
  {
    var outer = new Border
    {
      Background = new SolidColorBrush(Color.FromRgb(238, 242, 247)), CornerRadius = new CornerRadius(10),
      Padding = new Thickness(3), Margin = new Thickness(0, 4, 12, 4), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left,
    };
    var sp = new StackPanel { Orientation = Orientation.Horizontal };
    var items = new List<Border>();
    void Paint()
    {
      for (int i = 0; i < items.Count; i++)
      {
        items[i].Background = i == sel ? Brushes.White : Brushes.Transparent;
        ((TextBlock)items[i].Child).Foreground = new SolidColorBrush(i == sel ? Color.FromRgb(30, 41, 59) : Color.FromRgb(100, 116, 139));
        ((TextBlock)items[i].Child).FontWeight = i == sel ? FontWeights.SemiBold : FontWeights.Normal;
      }
    }
    for (int i = 0; i < labels.Length; i++)
    {
      int k = i;
      var bd = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 5, 12, 5), Cursor = System.Windows.Input.Cursors.Hand, Background = Brushes.Transparent, Child = new TextBlock { Text = labels[i], FontSize = 13 } };
      bd.MouseLeftButtonUp += (_, _) => { if (sel == k) return; sel = k; Paint(); changed(k); };
      items.Add(bd);
      sp.Children.Add(bd);
    }
    Paint();
    outer.Child = sp;
    return outer;
  }
}
