@echo off
chcp 65001 > nul
title Ölçü Atölyesi PC Başlatıcı
echo ======================================================================
echo              ÖLÇÜ ATÖLYESİ PC — BAŞLATICI
echo ======================================================================
echo.

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [HATA] Sisteminizde 'dotnet' komutu (.NET SDK) bulunamadı!
    echo.
    echo Uygulamayı kaynak koddan çalıştırmak için .NET 10 SDK gereklidir:
    echo İndirme Bağlantısı: https://dotnet.microsoft.com/download/dotnet/10.0
    echo.
    echo Eğer SDK kurmak istemiyorsanız, GitHub 'Releases' sekmesinden
    echo kurulum gerektirmeyen hazır taşınabilir paketi (.zip) indirebilirsiniz.
    echo ======================================================================
    pause
    exit /b 1
)

echo [.NET 10 SDK Algılandı]
echo Uygulama derleniyor ve başlatılıyor, lütfen bekleyin...
echo.

dotnet run --project "%~dp0src\OlcuAtolyesi.PC\OlcuAtolyesi.PC.csproj" -c Release

if %errorlevel% neq 0 (
    echo.
    echo [BİLGİ] Release kipinde başlatılamadı, Debug kipi deneniyor...
    dotnet run --project "%~dp0src\OlcuAtolyesi.PC\OlcuAtolyesi.PC.csproj" -c Debug
)

if %errorlevel% neq 0 (
    echo.
    echo [HATA] Uygulama başlatılamadı. Lütfen hata mesajlarını inceleyin.
    pause
)
