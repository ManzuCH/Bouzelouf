@echo off
set MINGW_BIN=C:\Users\Manzu\Desktop\mingw\w64devkit\bin
set PATH=%MINGW_BIN%;%PATH%

echo [+] Compiling VoltNative.dll...
g++.exe -shared -static -s -O2 -o VoltNative.dll VoltNative.cpp -luser32 -lwinmm
if %errorlevel% neq 0 (
    echo [-] Compilation failed!
    pause
    exit /b %errorlevel%
)
echo [OK] VoltNative.dll built!

echo [+] Copying to bin output...
if not exist "..\bin\Release\net8.0-windows\" mkdir "..\bin\Release\net8.0-windows\"
copy /Y VoltNative.dll "..\bin\Release\net8.0-windows\VoltNative.dll" >nul 2>nul
if not exist "..\bin\Debug\net8.0-windows\" mkdir "..\bin\Debug\net8.0-windows\"
copy /Y VoltNative.dll "..\bin\Debug\net8.0-windows\VoltNative.dll" >nul 2>nul

echo [OK] Done!
pause
