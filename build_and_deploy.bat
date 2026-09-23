@echo off
cd /d "%~dp0"

call "%~dp0..\..\..\..\dev_env.bat"
if not defined DEV_ROOT (
    echo ERROR: dev_env.bat not found at %~dp0..\..\..\..\dev_env.bat
    exit /b 1
)

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
call "%DEV_ROOT%\dev_env.bat" find_game "FINAL FANTASY V PR" GAME_ROOT
if not defined GAME_ROOT (
    echo ERROR: "FINAL FANTASY V PR" not found in any Steam library.
    echo   Detected: %STEAM_LIBS%
    exit /b 1
)
set "GAME_DIR=%GAME_ROOT%\Mods"
copy /Y "bin\Debug\net6.0\FFV_ScreenReader.dll" "%GAME_DIR%\" >> build_log.txt 2>&1
set DEPLOY_ERROR=%ERRORLEVEL%

if %DEPLOY_ERROR% NEQ 0 (
    echo Deployment failed! See build_log.txt for details.
    exit /b %DEPLOY_ERROR%
)

echo. >> build_log.txt
echo Mod deployed successfully! >> build_log.txt
echo Done.
exit /b 0

