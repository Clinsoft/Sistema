#!/usr/bin/env bash
# Deploy do frontend (Vue/Vite) para o VPS SEM acúmulo de assets órfãos.
#
# Por que não é `rsync` direto daqui: o Git Bash no Windows não tem rsync, e o
# `rsync local→remoto` exige rsync nas DUAS pontas. Então subimos o build para
# uma pasta de staging no servidor (scp) e rodamos `rsync -a --delete` NO
# servidor (staging → produção). O `--delete` remove os chunks antigos que não
# existem mais no build atual — mesmo efeito do rsync --delete clássico.
#
# Uso:  bash scripts/deploy-frontend.sh
set -euo pipefail

# ── Config ────────────────────────────────────────────────────────────
SSH_KEY="${SSH_KEY:-C:/Users/User/.ssh/id_ed25519}"
HOST="${HOST:-root@177.153.194.228}"
LIVE="/var/www/ecogranel/frontend/dist"
STAGING="/tmp/ecogranel-fe-staging"
FE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../src/Sistema.Frontend" && pwd)"

SSH="ssh -i $SSH_KEY -o StrictHostKeyChecking=no $HOST"
SCP="scp -i $SSH_KEY -o StrictHostKeyChecking=no"

# ── 1. Build ──────────────────────────────────────────────────────────
echo "▶ Build do frontend…"
( cd "$FE_DIR" && npm run build )

# ── 2. Sobe para staging (pasta limpa a cada deploy) ─────────────────
echo "▶ Enviando build para staging no servidor…"
$SSH "rm -rf $STAGING && mkdir -p $STAGING"
$SCP -r "$FE_DIR/dist/." "$HOST:$STAGING/"

# ── 3. rsync --delete no servidor: staging → produção ────────────────
echo "▶ Sincronizando com --delete (remove órfãos)…"
$SSH "rsync -a --delete $STAGING/ $LIVE/ && rm -rf $STAGING"

# ── 4. Verificação ───────────────────────────────────────────────────
LOCAL_IDX=$(grep -o 'index-[A-Za-z0-9_-]*\.js' "$FE_DIR/dist/index.html" | head -1)
SRV_IDX=$($SSH "grep -o 'index-[A-Za-z0-9_-]*\.js' $LIVE/index.html | head -1")
echo "▶ index local:    $LOCAL_IDX"
echo "▶ index servidor: $SRV_IDX"
if [ "$LOCAL_IDX" = "$SRV_IDX" ]; then
  echo "✅ Deploy OK — assets sincronizados (sem órfãos). Peça Ctrl+Shift+R ao usuário."
else
  echo "⚠ index divergente — confira o deploy." >&2
  exit 1
fi
