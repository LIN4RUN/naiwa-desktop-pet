; 奶蛙桌宠 安装脚本（Inno Setup 6）— 放到项目根目录，双击 build_setup.cmd 或 ISCC 编译
[Setup]
AppName=奶蛙桌宠
AppVersion=2.0.0
AppVerName=奶蛙桌宠 v2.0
AppId={{8F3A2C1E-6B4D-4E9A-B5C2-7D0A9E3F1A08}
DefaultDirName={autopf}\奶蛙桌宠
DefaultGroupName=奶蛙桌宠
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\奶蛙桌宠.exe
OutputDir=..\..
OutputBaseFilename=奶蛙桌宠_v2.0_Setup
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile=源码\naiwa.ico
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
VersionInfoVersion=2.0.0
VersionInfoProductName=奶蛙桌宠

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl,ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标："

[Files]
Source: "*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "self-test,源码,.gitignore,build.cmd,make_*.py,README.md,素材.nwp.bak,*.png,*.mp4,setup.iss,ChineseSimplified.isl"

[Icons]
Name: "{group}\奶蛙桌宠"; Filename: "{app}\奶蛙桌宠.exe"
Name: "{autodesktop}\奶蛙桌宠"; Filename: "{app}\奶蛙桌宠.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\奶蛙桌宠.exe"; Description: "立即运行奶蛙桌宠"; Flags: nowait postinstall skipifsilent