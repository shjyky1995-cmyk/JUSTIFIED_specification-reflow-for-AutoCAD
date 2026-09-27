@echo off
setlocal
cd /d %~dp0
where node >nul 2>nul
if errorlevel 1 (
  echo Node.js not found. Please install Node.js LTS, then run this file again.
  pause
  exit /b 1
)
if not exist "dist\index.html" (
  echo [1/2] Building the app for the first time...
  call npm run build
  if errorlevel 1 (
    echo Build failed. Run "npm run build" in this folder to see the error.
    pause
    exit /b 1
  )
)
echo [2/2] Starting the Design Note Workbench...
call node_modules\.bin\electron .
endlocal
