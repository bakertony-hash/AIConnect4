@echo off
setlocal EnableExtensions
cd /d "%~dp0" || exit /b 1

echo [run] Pulling (ff-only)...
git pull --ff-only
if errorlevel 1 (
  echo [run] git pull failed.
  exit /b 1
)

echo [run] Building src\AIConnect4.App...
dotnet build "src\AIConnect4.App\AIConnect4.App.csproj" -c Debug
if errorlevel 1 (
  echo [run] build failed.
  exit /b 1
)

echo [run] Starting AIConnect4.App...
dotnet run --project "src\AIConnect4.App\AIConnect4.App.csproj" -c Debug --no-build
exit /b %ERRORLEVEL%
