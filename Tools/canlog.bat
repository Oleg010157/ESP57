@echo off
setlocal

REM Drag-and-drop a can_*.log file onto this BAT to generate a TSV next to it.
REM Output: <inputname>.tsv

REM If no argument given, pick the newest can_*.log in this script folder.
if "%~1"=="" (
  set "IN="
  for /f "delims=" %%F in ('dir /b /a-d /o-d "%~dp0can_*.log" 2^>nul') do (
    set "IN=%~dp0%%F"
    goto FOUND_IN
  )
) else (
  set "IN=%~f1"
)

:FOUND_IN
if "%IN%"=="" (
  echo Usage: drag-and-drop a can_*.log file onto this script or run without args to convert the latest can_*.log in this folder.
  exit /b 2
)

for %%P in ("%IN%") do set "OUT=%%~dpnP.tsv"

if not exist "%IN%" (
  echo.
  echo Input file not found: "%IN%"
  exit /b 2
)

REM Prefer local copy first (same folder as this BAT), then repo build locations
set "EXE=%~dp0CanLogTool.exe"
if not exist "%EXE%" set "EXE=%~dp0..\CanLogTool\bin\Debug\net48\CanLogTool.exe"
if not exist "%EXE%" set "EXE=%~dp0..\CanLogTool\bin\Release\net48\CanLogTool.exe"

echo Input : "%IN%"
echo Output: "%OUT%"

if not exist "%EXE%" (
  echo.
  echo CanLogTool.exe not found.
  echo Build the `CanLogTool` project in Visual Studio.
  echo.
  exit /b 2
)

"%EXE%" --in "%IN%" --out "%OUT%"
set "EC=%ERRORLEVEL%"

if not "%EC%"=="0" (
  echo.
  echo Failed with exit code %EC%.
  exit /b %EC%
)

echo.
echo Wrote: "%OUT%"
exit /b 0
