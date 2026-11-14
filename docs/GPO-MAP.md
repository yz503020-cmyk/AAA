# שלב א' – מיפוי הגדרות GPO ורישום

מקור: קובץ `All_GP_Settings.txt` (רשימת שמות בלבד, בלי נתיבים). כאן – ההגדרות הרלוונטיות לחסימה, עם ערכי הרישום שהתוכנה כותבת.
**U** = חל על חשבון היעד (HKCU שלו), **M** = כל המחשב (HKLM).
`...\Policies\Explorer` = `Software\Microsoft\Windows\CurrentVersion\Policies\Explorer` (וכן `\System`).

## כלי מערכת

| שם ההגדרה ב-GPO | היקף | רישום | בתוכנה |
|---|---|---|---|
| Prohibit access to Control Panel and PC settings | U | `...\Policies\Explorer\NoControlPanel=1` | ✓ |
| Remove Task Manager | U | `...\Policies\System\DisableTaskMgr=1` | ✓ |
| Prevent access to registry editing tools | U | `...\Policies\System\DisableRegistryTools=2` | ✓ |
| Prevent access to the command prompt | U | `Software\Policies\Microsoft\Windows\System\DisableCMD=1` | ✓ |
| Remove Run menu from Start Menu | U | `...\Policies\Explorer\NoRun=1` | ✓ |
| Restrict users to the explicitly permitted list of snap-ins | U | `Software\Policies\Microsoft\MMC\RestrictToPermittedSnapins=1` | ✓ |
| Remove "Folder Options" | U | `...\Policies\Explorer\NoFolderOptions=1` | ✓ (לא מומלץ כברירת מחדל) |
| Configure the behavior of the sudo command | M | `Software\Policies\Microsoft\Windows\Sudo\EnableSudo=0` | ✓ |
| *(לא GPO)* Windows Script Host | M | `Software\Microsoft\Windows Script Host\Settings\Enabled=0` | ✓ |
| Settings Page Visibility | M | `...\Policies\Explorer\SettingsPageVisibility` | – מכוסה ע"י NoControlPanel |

## התקנות

| שם | היקף | רישום | בתוכנה |
|---|---|---|---|
| Prohibit User Installs | M | `Software\Policies\Microsoft\Windows\Installer\DisableUserInstalls=1` | ✓ |
| Always install with elevated privileges | M+U | `...\Installer\AlwaysInstallElevated=0` | ✓ (סוגר פרצה) |
| Turn off Windows Installer | M | `...\Installer\DisableMSI=2` | ✓ (כבוי כברירת מחדל – חוסם גם מנהל) |
| Turn off the Store application | M | `Software\Policies\Microsoft\WindowsStore\RemoveWindowsStore=1` | ✓ |
| Enable App Installer / ms-appinstaller / winget CLI | M | `Software\Policies\Microsoft\Windows\AppInstaller\EnableAppInstaller=0` ועוד 2 | ✓ |
| Allow all trusted apps to install / Allow development… | M | `Software\Policies\Microsoft\Windows\Appx\AllowAllTrustedApps=0`, `AllowDevelopmentWithoutDevLicense=0` | ✓ |
| Disable all apps from Microsoft Store | M | `...\WindowsStore\DisableStoreApps` | ✗ – עלול לשבור את "תמונות" |

## חשבונות והרשאות

| שם | היקף | רישום | בתוכנה |
|---|---|---|---|
| UAC: Behavior of the elevation prompt for standard users → Automatically deny | M | `...\Policies\System\ConsentPromptBehaviorUser=0` | ✓ |
| Accounts: Block Microsoft accounts | M | `...\Policies\System\NoConnectedUser=3` | ✓ |
| Hide entry points for Fast User Switching | M | `...\Policies\System\HideFastUserSwitching=1` | ✓ |
| Prevent the use of security questions for local accounts | M | `Software\Policies\Microsoft\Windows\System\NoLocalPasswordResetQuestions=1` | ✓ |
| Enumerate administrator accounts on elevation | M | `...\Policies\CredUI\EnumerateAdministrators=0` | ✓ |
| *(לא GPO)* Safe Mode – block non-admins | M | `...\Policies\System\SafeModeBlockNonAdmins=1` | ✓ |

## רשת

| שם | היקף | רישום | בתוכנה |
|---|---|---|---|
| Prohibit access to properties of a LAN connection ועוד | U | `Software\Policies\Microsoft\Windows\Network Connections\NC_LanProperties=0` ועוד | ✓ |
| Enable Windows 2000 Network Connections settings for Administrators | M | `...\Network Connections\NC_EnableAdminProhibits=1` | ✓ |
| Prevent changing proxy settings | U | `Software\Policies\Microsoft\Internet Explorer\Control Panel\Proxy=1` | ✓ |
| Allow users to connect remotely (RDP) / Remote Assistance | M | `Software\Policies\Microsoft\Windows NT\Terminal Services\fDenyTSConnections=1`, `fAllowToGetHelp=0` | ✓ |

## התקנים

| שם | היקף | רישום | בתוכנה |
|---|---|---|---|
| All Removable Storage classes: Deny all access | U | `Software\Policies\Microsoft\Windows\RemovableStorageDevices\Deny_All=1` | ✓ |
| Turn off AutoPlay / Default behavior for AutoRun / Disallow Autoplay for non-volume devices | M | `NoDriveTypeAutoRun=255`, `NoAutorun=1`, `NoAutoplayfornonVolume=1` | ✓ |

## עדכונים

| שם | היקף | רישום | בתוכנה |
|---|---|---|---|
| Remove access to use all Windows Update features | M | `Software\Policies\Microsoft\Windows\WindowsUpdate\SetDisableUXWUAccess=1` | ✓ (לא מומלץ כברירת מחדל) |

## מה **אין** לו GPO – ומכוסה בדרך אחרת

| מה | איך |
|---|---|
| PowerShell, wscript/cscript/mshta, regedit, reg.exe, certutil, msbuild, wmic… | כלל דחייה ב-AppLocker לחשבון היעד + Watchdog ("כלים חסומים") |
| WSL, Sandbox, Hyper-V, PowerShell 2.0 | הסרה בלשונית "ניקוי" + חסימת wsl.exe |
| Safe Mode / סביבת שחזור | SafeModeBlockNonAdmins + השבתת WinRE + `bcdedit recoveryenabled No` |
| שינוי שעה | משתמש רגיל לא מורשה ממילא (SeSystemtimePrivilege) |
| "Run only specified Windows applications" (RestrictRun / DisallowRun) | **לא בשימוש** – חל רק על מה שנפתח דרך הסייר, קל לעקוף. במקומו AppLocker + Watchdog |
| App Control for Business (WDAC) | חזק יותר מ-AppLocker ועובד גם ב-Home. מועמד לשלב הבא |
