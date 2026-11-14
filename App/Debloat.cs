namespace AAA;

public enum DebloatKind { Appx, Capability, Feature, Edge }

public record DebloatItem(string Id, string Title, DebloatKind Kind, string Pattern, string Desc, bool Recommended = true, string? Warning = null);

/// <summary>שלב ד': הסרת כלים מיותרים ומסכני חסימה מ-Windows</summary>
static class Debloat
{
  static DebloatItem A(string id, string title, string pat, string desc, bool rec = true, string? warn = null) => new(id, title, DebloatKind.Appx, pat, desc, rec, warn);

  public static readonly DebloatItem[] Items =
  {
    new("edge", "דפדפן Edge", DebloatKind.Edge, "msedge", "הסרה דרך תוכנת ההתקנה של Edge. ב-Windows מחוץ לאיחוד האירופי ההסרה לעיתים נחסמת – אז הוא נשאר מותקן אבל הרשימה הלבנה חוסמת אותו (הוא לא ברשימה). רכיב WebView2 נשאר – וורד צריך אותו."),
    new("wmp_legacy", "נגן המדיה הישן (Windows Media Player)", DebloatKind.Capability, "Media.WindowsMediaPlayer", "רכיב אופציונלי של Windows."),
    new("wmp_feature", "נגן המדיה הישן – תכונת Windows", DebloatKind.Feature, "WindowsMediaPlayer", "בגרסאות שבהן הנגן הוא \"תכונת Windows\" ולא רכיב."),
    A("media_player", "נגן המדיה החדש (Media Player)", "Microsoft.ZuneMusic", "אפליקציית המדיה של Windows 11."),
    A("movies", "סרטים וטלוויזיה", "Microsoft.ZuneVideo", ""),
    A("clipchamp", "Clipchamp (עורך וידאו)", "Clipchamp.Clipchamp", ""),
    A("xbox", "Xbox וכל רכיביו", "Microsoft.Xbox", "Xbox App, Game Bar, Identity Provider וכו'."),
    A("gaming", "Gaming App", "Microsoft.GamingApp", ""),
    A("solitaire", "משחקי Solitaire", "Microsoft.MicrosoftSolitaireCollection", ""),
    A("quickassist", "Quick Assist (סיוע מהיר)", "MicrosoftCorporationII.QuickAssist", "מאפשר לאדם מבחוץ להשתלט על המחשב ולבטל חסימות."),
    A("phonelink", "קישור לטלפון (Phone Link)", "Microsoft.YourPhone", "העברת קבצים ותוכן מהטלפון."),
    A("crossdevice", "חוויות בין מכשירים", "MicrosoftWindows.CrossDevice", ""),
    A("teams", "Teams", "MSTeams", ""),
    A("teams_old", "Teams (ישן)", "MicrosoftTeams", ""),
    A("copilot", "Copilot", "Microsoft.Copilot", "צ'אט AI עם גישה לאינטרנט."),
    A("bing_news", "חדשות (Bing News)", "Microsoft.BingNews", ""),
    A("bing_weather", "מזג אוויר", "Microsoft.BingWeather", ""),
    A("bing_search", "חיפוש Bing", "Microsoft.BingSearch", ""),
    A("widgets", "ווידג'טים", "MicrosoftWindows.Client.WebExperience", "לוח הווידג'טים מציג חדשות ותוכן מהאינטרנט."),
    A("gethelp", "קבל עזרה", "Microsoft.GetHelp", ""),
    A("getstarted", "טיפים", "Microsoft.Getstarted", ""),
    A("feedback", "מרכז משוב", "Microsoft.WindowsFeedbackHub", ""),
    A("mail", "דואר ולוח שנה (ישן)", "microsoft.windowscommunicationsapps", ""),
    A("outlook_new", "Outlook החדש", "Microsoft.OutlookForWindows", "דפדפן בפועל (WebView)."),
    A("people", "אנשים", "Microsoft.People", ""),
    A("maps", "מפות", "Microsoft.WindowsMaps", ""),
    A("officehub", "Microsoft 365 (Office Hub)", "Microsoft.MicrosoftOfficeHub", "לא וורד עצמו – רק אפליקציית ההפניה."),
    A("todo", "To Do", "Microsoft.Todos", ""),
    A("powerautomate", "Power Automate", "Microsoft.PowerAutomateDesktop", "כלי אוטומציה – יכול להריץ כמעט הכל."),
    A("devhome", "Dev Home", "Microsoft.Windows.DevHome", ""),
    A("cortana", "Cortana", "Microsoft.549981C3F5F10", ""),
    A("camera", "מצלמה", "Microsoft.WindowsCamera", "", rec: false),
    A("recorder", "מקליט קול", "Microsoft.WindowsSoundRecorder", "", rec: false),
    A("terminal", "Windows Terminal", "Microsoft.WindowsTerminal", "מסוף פקודות.", rec: true),
    A("store", "החנות (Microsoft Store)", "Microsoft.WindowsStore", "קשה מאוד להחזיר אחרי הסרה.", rec: false, warn: "בלתי הפיך כמעט"),
    A("appinstaller", "App Installer / winget", "Microsoft.DesktopAppInstaller", "מנהל החבילות winget.", rec: false, warn: "תוכנות מסוימות משתמשות בו לעדכון"),
    new("ie", "Internet Explorer (רכיב)", DebloatKind.Capability, "Browser.InternetExplorer", ""),
    new("ps_ise", "PowerShell ISE", DebloatKind.Capability, "Microsoft.Windows.PowerShell.ISE", ""),
    new("steps", "מקליט צעדים", DebloatKind.Capability, "App.StepsRecorder", ""),
    new("ps2", "PowerShell 2.0 (עוקף הגבלות)", DebloatKind.Feature, "MicrosoftWindowsPowerShellV2Root", "גרסה ישנה שעוקפת הגבלות שפה של PowerShell."),
    new("wsl", "WSL – לינוקס בתוך Windows", DebloatKind.Feature, "Microsoft-Windows-Subsystem-Linux", "מערכת שלמה שלא כפופה לחסימות של Windows."),
    new("vmp", "פלטפורמת מכונות וירטואליות", DebloatKind.Feature, "VirtualMachinePlatform", "נדרשת ל-WSL2 ולאמולטורים."),
    new("sandbox", "ארגז החול של Windows (Sandbox)", DebloatKind.Feature, "Containers-DisposableClientVM", "Windows נקי בתוך חלון – בלי שום חסימה."),
    new("hyperv", "Hyper-V", DebloatKind.Feature, "Microsoft-Hyper-V-All", "מכונות וירטואליות."),
  };

  /// <summary>סריקת מה מותקן. מחזיר מזהי פריטים שמותקנים כעת</summary>
  public static HashSet<string> Scan()
  {
    const string script = @"
$a = @(); try { $a += Get-AppxPackage -AllUsers | ForEach-Object { $_.Name } } catch {}
try { $a += Get-AppxProvisionedPackage -Online | ForEach-Object { $_.DisplayName } } catch {}
$a | Sort-Object -Unique | ForEach-Object { 'A|' + $_ }
try { Get-WindowsCapability -Online | Where-Object { $_.State -eq 'Installed' } | ForEach-Object { 'C|' + $_.Name } } catch {}
try { Get-WindowsOptionalFeature -Online | Where-Object { $_.State -eq 'Enabled' } | ForEach-Object { 'F|' + $_.FeatureName } } catch {}
if (Get-ChildItem ""${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"",""$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"" -ErrorAction SilentlyContinue) { 'E|msedge' }
";
    var r = Shell.PowerShell(script, 600_000);
    var lines = r.Output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 2).ToList();
    var found = new HashSet<string>();
    foreach (var it in Items)
    {
      var prefix = it.Kind switch { DebloatKind.Appx => "A|", DebloatKind.Capability => "C|", DebloatKind.Feature => "F|", _ => "E|" };
      if (lines.Any(l => l.StartsWith(prefix + it.Pattern, StringComparison.OrdinalIgnoreCase))) found.Add(it.Id);
    }
    return found;
  }

  static string Q(string s) => s.Replace("'", "''");

  public static Shell.Result Remove(DebloatItem it)
  {
    Log.Write("Debloat remove: " + it.Id);
    string script = it.Kind switch
    {
      DebloatKind.Appx => $@"
Get-AppxPackage -AllUsers -Name '{Q(it.Pattern)}*' | ForEach-Object {{ try {{ Remove-AppxPackage -Package $_.PackageFullName -AllUsers -ErrorAction Stop }} catch {{ Write-Output $_.Exception.Message }} }}
Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -like '{Q(it.Pattern)}*' }} | ForEach-Object {{ try {{ Remove-AppxProvisionedPackage -Online -AllUsers -PackageName $_.PackageName -ErrorAction Stop | Out-Null }} catch {{ Write-Output $_.Exception.Message }} }}",
      DebloatKind.Capability => $@"
Get-WindowsCapability -Online -Name '{Q(it.Pattern)}*' | Where-Object {{ $_.State -eq 'Installed' }} | ForEach-Object {{ Remove-WindowsCapability -Online -Name $_.Name | Out-Null }}",
      DebloatKind.Feature => $@"
Get-WindowsOptionalFeature -Online -FeatureName '{Q(it.Pattern)}' -ErrorAction SilentlyContinue | Where-Object {{ $_.State -eq 'Enabled' }} | ForEach-Object {{ Disable-WindowsOptionalFeature -Online -FeatureName $_.FeatureName -NoRestart | Out-Null }}",
      _ => @"
Get-Process msedge -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$s = Get-ChildItem ""${env:ProgramFiles(x86)}\Microsoft\Edge\Application\*\Installer\setup.exe"",""$env:ProgramFiles\Microsoft\Edge\Application\*\Installer\setup.exe"" -ErrorAction SilentlyContinue
foreach ($x in $s) { Start-Process -FilePath $x.FullName -ArgumentList '--uninstall','--system-level','--verbose-logging','--force-uninstall' -Wait }",
    };
    return Shell.PowerShell(script, 900_000);
  }
}
