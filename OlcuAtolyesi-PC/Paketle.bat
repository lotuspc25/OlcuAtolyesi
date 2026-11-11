@echo off
chcp 65001 > nul
title Ölçü Atölyesi PC — Taşınabilir Paket Üretici
echo ======================================================================
echo       ÖLÇÜ ATÖLYESİ PC — TAŞINABİLİR (PORTABLE) PAKET ÜRETİCİ
echo ======================================================================
echo.

where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo [HATA] Sisteminizde 'dotnet' komutu (.NET SDK) bulunamadı!
    echo Lütfen .NET 10 SDK kurun: https://dotnet.microsoft.com/download/dotnet/10.0
    pause
    exit /b 1
)

echo Bağımsız (Self-Contained) Windows x64 sürümü derleniyor...
echo Bu işlem uygulamanın başka bir bilgisayarda .NET kurulu olmadan çalışmasını sağlar.
echo.

dotnet publish "%~dp0src\OlcuAtolyesi.PC\OlcuAtolyesi.PC.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o "%~dp0Publish\"

if %errorlevel% equ 0 (
    echo.
    echo ======================================================================
    echo [BAŞARILI] Paket başarıyla oluşturuldu!
    echo Çıktı Dizini: %~dp0Publish\
    echo Çalıştırmak için: %~dp0Publish\OlcuAtolyesi.PC.exe
    echo.
    echo Bu klasörü herhangi bir Windows bilgisayara kopyalayıp doğrudan çalıştırabilirsiniz.
    echo ======================================================================
) else (
    echo.
    echo [HATA] Paketleme işlemi sırasında bir hata oluştu!
)
pause
