@echo off
setlocal
title MyFantasy - Launcher
cd /d "%~dp0"

echo ============================================
echo   MyFantasy - arrancando backend + frontend
echo ============================================
echo.

REM --- Comprobacion de credenciales (variables de entorno persistentes) ---
if "%Fantasy__Auth__RefreshToken%"=="" (
  echo [AVISO] No encuentro Fantasy__Auth__RefreshToken en el entorno.
  echo         Ejecuta una vez el bloque de PowerShell del README para
  echo         guardar el token, el ClientId y la cadena de conexion MySQL.
  echo.
)

REM --- Backend (.NET) en su propia ventana ---
echo Iniciando BACKEND (API) en http://localhost:5298 ...
start "MyFantasy API" cmd /k "cd /d "%~dp0backend\MyFantasy.Api" && dotnet run"

REM --- Frontend (Nuxt) en su propia ventana ---
echo Iniciando FRONTEND (web) en http://localhost:3000 ...
start "MyFantasy Web" cmd /k "cd /d "%~dp0frontend" && npm run dev"

REM --- Esperar a que el frontend levante y abrir el navegador ---
echo.
echo Esperando a que arranque el frontend...
timeout /t 14 /nobreak >nul
start "" http://localhost:3000

echo.
echo Listo. Se han abierto dos ventanas (API y Web).
echo Para PARAR la app, cierra esas dos ventanas.
echo.
pause
endlocal
