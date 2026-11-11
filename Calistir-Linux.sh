#!/usr/bin/env bash
# ======================================================================
#   ÖLÇÜ ATÖLYESİ — LINUX TEK TIKLA BAŞLATICI (KÖK DİZİN)
# ======================================================================
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
cd "$DIR/OlcuAtolyesi-PC"
chmod +x ./Calistir.sh
./Calistir.sh "$@"
