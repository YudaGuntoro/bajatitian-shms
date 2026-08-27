@echo off
SETLOCAL EnableExtensions EnableDelayedExpansion

SET "ROOT_DIR=%~dp0"
SET "ROOT_DIR=%ROOT_DIR:~0,-1%"
SET "FRONTEND_DIR=%ROOT_DIR%\Frontend"
SET "REMOTE=origin"
SET "BRANCH=main"
SET "PM2_APP_NAME=LeakTesterFrontend"
SET "PORT=3000"
SET "HOST=0.0.0.0"

IF NOT "%~1"=="" SET "BRANCH=%~1"
IF NOT "%~2"=="" SET "PORT=%~2"

IF EXIST "%ROOT_DIR%\package.json" (
    SET "FRONTEND_DIR=%ROOT_DIR%"
    SET "PROJECT_ROOT=%ROOT_DIR%\.."
) ELSE (
    SET "PROJECT_ROOT=%ROOT_DIR%"
)

echo ============================================================
echo LeakTester Frontend Only
echo Root: %ROOT_DIR%
echo Frontend: %FRONTEND_DIR%
echo PM2 App: %PM2_APP_NAME%
echo Port: %PORT%
echo Host: %HOST%
echo ============================================================

cd /d "%ROOT_DIR%"
IF ERRORLEVEL 1 (
    echo ERROR: Failed to open project folder.
    pause
    exit /b 1
)

IF NOT EXIST "%FRONTEND_DIR%\package.json" (
    echo ERROR: Frontend package.json was not found:
    echo %FRONTEND_DIR%\package.json
    echo.
    echo Put this BAT in either:
    echo   1. Project root that contains the Frontend folder, or
    echo   2. The actual Frontend folder that contains package.json.
    GOTO Failed
)

CALL :CheckCommand node
IF ERRORLEVEL 1 GOTO Failed
CALL :CheckCommand npm
IF ERRORLEVEL 1 GOTO Failed
CALL :CheckCommand pm2
IF ERRORLEVEL 1 GOTO Failed

where git >nul 2>&1
IF ERRORLEVEL 1 (
    echo.
    echo WARNING: git is not available in PATH. Skipping git pull.
) ELSE (
    IF EXIST "%PROJECT_ROOT%\.git" (
        echo.
        echo Pulling latest source from GitHub...
        cd /d "%PROJECT_ROOT%"
        IF ERRORLEVEL 1 GOTO Failed
        git fetch "%REMOTE%" "%BRANCH%"
        IF ERRORLEVEL 1 GOTO Failed

        git pull --ff-only "%REMOTE%" "%BRANCH%"
        IF ERRORLEVEL 1 (
            echo.
            echo ERROR: Pull failed. Commit or stash local changes, then run this BAT again.
            GOTO Failed
        )
    ) ELSE (
        echo.
        echo WARNING: This folder is not a Git repository. Skipping git pull.
    )
)

echo.
echo Installing frontend packages...
cd /d "%FRONTEND_DIR%"
IF ERRORLEVEL 1 GOTO Failed

IF EXIST "package-lock.json" (
    npm ci
) ELSE (
    npm install
)
IF ERRORLEVEL 1 GOTO Failed

echo.
echo Building frontend...
npm run build
IF ERRORLEVEL 1 GOTO Failed

echo.
echo Restarting frontend with PM2...
pm2 describe "%PM2_APP_NAME%" >nul 2>&1
IF NOT ERRORLEVEL 1 (
    pm2 delete "%PM2_APP_NAME%"
)

SET "NODE_ENV=production"
SET "PORT=%PORT%"
SET "HOSTNAME=%HOST%"
SET "SERVER_API_BASE_URL=http://localhost:5241"
pm2 start ".next\standalone\server.js" --name "%PM2_APP_NAME%" --update-env
IF ERRORLEVEL 1 GOTO Failed

pm2 save
IF ERRORLEVEL 1 GOTO Failed

echo.
echo ============================================================
echo Frontend is running with PM2.
echo Local server URL: http://localhost:%PORT%
echo From another PC, use this server IP: http://SERVER-IP:%PORT%
echo Check status: pm2 status
echo View logs: pm2 logs %PM2_APP_NAME%
echo ============================================================
pause
exit /b 0

:CheckCommand
where %~1 >nul 2>&1
IF ERRORLEVEL 1 (
    echo ERROR: %~1 is not available in PATH.
    exit /b 1
)
exit /b 0

:Failed
cd /d "%ROOT_DIR%" >nul 2>&1
echo.
echo ============================================================
echo Frontend update failed.
echo Please check the error message above.
echo ============================================================
pause
exit /b 1
