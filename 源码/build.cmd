@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo === 奶蛙桌宠 构建脚本（需要 .NET SDK 6.0+） ===
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [错误] 没找到 dotnet 命令。请安装 .NET SDK 6.0 及以上版本：
    echo        https://dotnet.microsoft.com/download
    echo        或直接使用 Visual Studio 2022 打开 奶蛙桌宠.csproj 生成。
    pause
    exit /b 1
)

dotnet build 奶蛙桌宠.csproj -c Release -o "%~dp0_build"
if errorlevel 1 (
    echo [错误] 编译失败，请查看上方日志。
    pause
    exit /b 1
)

copy /y "%~dp0_build\奶蛙桌宠.exe" "%~dp0..\奶蛙桌宠.exe" >nul
if errorlevel 1 (
    echo [警告] 编译成功，但复制 exe 到根目录失败，请手动从 源码\_build 取 奶蛙桌宠.exe。
    pause
    exit /b 1
)

echo.
echo 构建成功：..\奶蛙桌宠.exe
echo 请确保 素材.nwp、idle_gif.nwp、drag.nwp、dance.mp3 等素材与 exe 在同一目录。
pause
