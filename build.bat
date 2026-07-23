@echo off
setlocal

echo ============================================
echo   Build SrtToSpeechApp (VB.NET - SRT to TTS)
echo ============================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [Loi] Khong tim thay .NET SDK.
    echo Cai dat tai: https://dotnet.microsoft.com/download
    echo Can ban .NET 9 SDK tro len.
    pause
    exit /b 1
)

echo Dang restore NuGet packages...
dotnet restore
if errorlevel 1 goto :error

echo.
echo Dang build (Release)...
dotnet build -c Release
if errorlevel 1 goto :error

echo.
echo ============================================
echo   Build thanh cong!
echo ============================================
echo File .exe nam trong: bin\Release\net9.0-windows\SrtToSpeechApp.exe
echo.
echo Cach chay:
echo   run.bat
echo   hoac chay truc tiep: bin\Release\net9.0-windows\SrtToSpeechApp.exe
echo.
pause
exit /b 0

:error
echo.
echo [Loi] Build that bai. Xem chi tiet loi o tren.
pause
exit /b 1
