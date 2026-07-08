@echo off
setlocal EnableExtensions EnableDelayedExpansion

if "%~1"=="" (
  echo Drag and drop a converted log file onto %~nx0
  echo.
  echo Example:
  echo   %~nx0 pcan_log.txt
  exit /b 2
)

set "IN=%~1"

if not exist "%IN%" (
  echo Input file not found:
  echo   %IN%
  exit /b 3
)

set "OUT=%~dpn1.rtt.txt"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0PcanRttAnalyzer.ps1" -InputPath "%IN%" -OutputPath "%OUT%"
set "EC=%ERRORLEVEL%"
if not "%EC%"=="0" (
  echo.
  echo Failed with exit code %EC%
  exit /b %EC%
)

if exist "%OUT%" (
  echo.
  echo Output:
  echo   %OUT%
) else (
  echo.
  echo Done, but output file was not created:
  echo   %OUT%
  exit /b 4
)

exit /b 0
