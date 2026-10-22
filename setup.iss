; 奶蛙桌宠 v2.5 安装脚本（Inno Setup 6）
; 用法：把本仓库完整克隆到本地，确保 素材.nwp / idle_gif.nwp / drag.nwp / dance.mp3 与 奶蛙桌宠.exe 已在仓库根目录，
; 然后用 Inno Setup 6 编译本文件（ISCC setup.iss），安装包输出到仓库的上一级目录。
[Setup]
AppName=奶蛙桌宠
AppVersion=2.5.0
AppVerName=奶蛙桌宠 v2.5
AppId={{8F3A2C1E-6B4D-4E9A-B5C2-7D0A9E3F1A08}
DefaultDirName={autopf}\奶蛙桌宠
DefaultGroupName=奶蛙桌宠
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\奶蛙桌宠.exe
OutputDir=..
OutputBaseFilename=奶蛙桌宠_v2.5_Setup
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile=源码\app.ico
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
VersionInfoVersion=2.5.0
VersionInfoProductName=奶蛙桌宠
VersionInfoDescription=奶蛙桌宠 v2.5 安装程序

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl,ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："

[Files]
Source: "*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "源码,.git,.gitignore,build.cmd,make_*.py,README.md,LICENSE,*.png,*.mp4,setup.iss,ChineseSimplified.isl,self-test,*.md"

[Icons]
Name: "{group}\奶蛙桌宠"; Filename: "{app}\奶蛙桌宠.exe"
Name: "{group}\使用说明"; Filename: "{app}\使用说明.txt"
Name: "{autodesktop}\奶蛙桌宠"; Filename: "{app}\奶蛙桌宠.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\奶蛙桌宠.exe"; Description: "立即运行奶蛙桌宠"; Flags: nowait postinstall skipifsilent
