@echo off
chcp 65001 > nul
echo ======================================================================
echo   Ölçü Atölyesi - Linux x64 Bağımsız Paketleme Scripti
echo ======================================================================
echo.

set OUT_DIR=dist\linux-x64
if exist "%OUT_DIR%" (
    echo Eski paket temizleniyor...
    rmdir /s /q "%OUT_DIR%"
)

echo Linux x64 için derleme ve paketleme başlatılıyor...
dotnet publish src\OlcuAtolyesi.Desktop\OlcuAtolyesi.Desktop.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o "%OUT_DIR%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [HATA] Paketleme başarısız oldu!
    pause
    exit /b %ERRORLEVEL%
)

echo #!/bin/bash > "%OUT_DIR%\Calistir.sh"
echo DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )" >> "%OUT_DIR%\Calistir.sh"
echo cd "$DIR" >> "%OUT_DIR%\Calistir.sh"
echo chmod +x ./OlcuAtolyesi.Desktop >> "%OUT_DIR%\Calistir.sh"
echo ./OlcuAtolyesi.Desktop "$@" >> "%OUT_DIR%\Calistir.sh"

echo.
echo ======================================================================
echo   Paketleme Başarılı!
echo   Çıktı Klasörü: %~dp0%OUT_DIR%
echo ======================================================================
pause
