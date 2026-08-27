@echo off
SETLOCAL EnableExtensions

SET "ROOT_DIR=%~dp0"
SET "ROOT_DIR=%ROOT_DIR:~0,-1%"
SET "PM2_APP_NAME=LeakTesterFrontend"
SET "FRONTEND_URL=http://localhost:3000"
SET "API_PROXY_URL=http://localhost:3000/api/leaktester/work-records"
SET "BACKEND_PORT=5241"

echo ============================================================
echo LeakTester Frontend PM2 Status
echo App: %PM2_APP_NAME%
echo Frontend: %FRONTEND_URL%
echo Backend Port: %BACKEND_PORT%
echo ============================================================
echo Server LAN addresses to open from another PC:
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.PrefixOrigin -ne 'WellKnown' } | ForEach-Object { Write-Host ('  http://' + $_.IPAddress + ':3000') }"
echo ============================================================

where pm2 >nul 2>&1
IF ERRORLEVEL 1 (
    echo ERROR: pm2 is not available in PATH.
    pause
    exit /b 1
)

CALL pm2 list
echo.
echo Checking frontend HTTP...
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $r = Invoke-WebRequest -Uri '%FRONTEND_URL%' -UseBasicParsing -TimeoutSec 10; Write-Host ('Frontend HTTP: ' + [int]$r.StatusCode) } catch { Write-Host ('Frontend HTTP ERROR: ' + $_.Exception.Message); exit 1 }"
echo.
echo Checking backend port...
powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Get-NetTCPConnection -LocalPort %BACKEND_PORT% -ErrorAction SilentlyContinue) { Write-Host 'Backend port %BACKEND_PORT%: LISTENING' } else { Write-Host 'Backend port %BACKEND_PORT%: NOT LISTENING'; exit 1 }"
echo.
echo Checking API through frontend proxy...
powershell -NoProfile -ExecutionPolicy Bypass -Command "try { $r = Invoke-WebRequest -Uri '%API_PROXY_URL%' -UseBasicParsing -TimeoutSec 15; $code = [int]$r.StatusCode } catch { if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode } else { Write-Host ('API Proxy ERROR: ' + $_.Exception.Message); exit 1 } }; Write-Host ('API Proxy HTTP: ' + $code); if ($code -eq 502) { Write-Host 'API proxy cannot reach backend.'; exit 1 }"
echo.
echo Logs:
echo   pm2 logs %PM2_APP_NAME%
echo.
pause
exit /b 0
