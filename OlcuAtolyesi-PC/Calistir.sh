#!/usr/bin/env bash
# ======================================================================
#   ÖLÇÜ ATÖLYESİ ÇAPRAZ MASAÜSTÜ — LINUX BAŞLATICI
# ======================================================================
set -e

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
cd "$DIR"

echo "======================================================================"
echo "          ÖLÇÜ ATÖLYESİ ÇAPRAZ MASAÜSTÜ — LINUX BAŞLATICI"
echo "======================================================================"
echo ""

# .NET SDK kontrolü
if ! command -v dotnet &> /dev/null; then
    # Kullanıcı ev dizini altında kurulu mu kontrol et
    if [ -f "$HOME/.dotnet/dotnet" ]; then
        export PATH="$HOME/.dotnet:$PATH"
        export DOTNET_ROOT="$HOME/.dotnet"
    else
        echo "[BİLGİ] 'dotnet' komutu bulunamadı. Otomatik olarak kullanıcı dizinine .NET 10 kuruluyor..."
        curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0
        export PATH="$HOME/.dotnet:$PATH"
        export DOTNET_ROOT="$HOME/.dotnet"
    fi
fi

echo "[.NET SDK Hazır] Ölçü Atölyesi Çapraz Masaüstü başlatılıyor..."
dotnet run --project "$DIR/src/OlcuAtolyesi.Desktop/OlcuAtolyesi.Desktop.csproj" -c Release "$@"
