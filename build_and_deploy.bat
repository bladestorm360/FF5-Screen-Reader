@echo off
cd /d "%~dp0"
echo Building FFV Screen Reader Mod... > build_log.txt
echo Building...
dotnet build -c Debug >> build_log.txt 2>&1
set BUILD_ERROR=%ERRORLEVEL%

if %BUILD_ERROR% NEQ 0 (
    echo Build failed! See build_log.txt for details.
    exit /b %BUILD_ERROR%
)

echo. >> build_log.txt
echo Build successful! Deploying to Mods folder... >> build_log.txt
echo Deploying...
copy /Y "bin\Debug\net6.0\FFV_ScreenReader.dll" "d:\Games\SteamLibrary\steamapps\common\FINAL FANTASY V PR\Mods\" >> build_log.txt 2>&1
set DEPLOY_ERROR=%ERRORLEVEL%

if %DEPLOY_ERROR% NEQ 0 (
    echo Deployment failed! See build_log.txt for details.
    exit /b %DEPLOY_ERROR%
)

copy /Y "D:\Games\Dev\sdl\SDL3.dll" "d:\Games\SteamLibrary\steamapps\common\FINAL FANTASY V PR\Mods\" >> build_log.txt 2>&1

echo. >> build_log.txt
echo Mod deployed successfully! >> build_log.txt
echo Done.
exit /b 0

