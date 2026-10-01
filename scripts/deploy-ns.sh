#!/usr/bin/env bash
# =============================================================================
#  Deploy único do Natural Sistemas (SaaS) — a partir do master.
#
#  Uso (no Git Bash, a partir da raiz do repo D:\Sistema):
#     bash scripts/deploy-ns.sh                # backend + frontend
#     bash scripts/deploy-ns.sh --backend-only
#     bash scripts/deploy-ns.sh --frontend-only
#     bash scripts/deploy-ns.sh --no-build     # usa o último build em /tmp
#
#  O QUE FAZ: compila o master, confere que é a base SaaS (gate presente),
#  faz backup do que está no servidor, envia os 4 DLLs + o dist, REPÕE a marca
#  do NS (logo/favicon não vêm no build), reinicia o serviço e valida a saúde.
#
#  IMPORTANTE: o NS deploya do MASTER (tem o gate SaaS). A EcoGranel é outra
#  instância, deploya de eco-prod (base 0b4fae2) — NÃO use este script p/ ela.
# =============================================================================
set -euo pipefail

# Garante dotnet/node/npm/npx no PATH mesmo quando chamado via Git Bash do PowerShell.
export PATH="/c/Program Files/dotnet:/c/Program Files/nodejs:$PATH"

# ---- Config do servidor NS ----
NS_HOST="root@191.252.212.225"
NS_KEY="$HOME/.ssh/natural-sistemas"               # = C:/Users/User/.ssh/natural-sistemas
API_DIR="/var/www/naturalsistemas/api"
DIST_DIR="/var/www/naturalsistemas/frontend/dist"
SERVICE="naturalsistemas-api"
APP_URL="https://app.naturalsistemas.com.br"

DLLS=(Sistema.Domain.dll Sistema.Application.dll Sistema.Infrastructure.dll Sistema.API.dll)
BRANDING=(favicon-natural-sistemas.png favicon.png logo-natural-sistemas.png)

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STAMP="$(date +%Y%m%d-%H%M%S)"
BAK="/root/ns-bak-$STAMP"
SSH="ssh -i $NS_KEY -o StrictHostKeyChecking=no"
SCP="scp -i $NS_KEY -o StrictHostKeyChecking=no"

# dotnet pode não estar no PATH do Git Bash
DOTNET="dotnet"; command -v dotnet >/dev/null 2>&1 || DOTNET="/c/Program Files/dotnet/dotnet.exe"

# ---- Flags ----
DO_BACKEND=1; DO_FRONTEND=1; DO_BUILD=1
for a in "${@:-}"; do
  case "$a" in
    "") ;;
    --backend-only)  DO_FRONTEND=0 ;;
    --frontend-only) DO_BACKEND=0 ;;
    --no-build)      DO_BUILD=0 ;;
    -h|--help) grep -E '^#( |!)' "$0" | sed 's/^#//'; exit 0 ;;
    *) echo "arg desconhecido: $a (use --help)"; exit 2 ;;
  esac
done

cd "$REPO"

# ---- Guard: só do master ----
BR="$(git rev-parse --abbrev-ref HEAD)"
if [ "$BR" != "master" ]; then
  echo "ERRO: o NS deploya do master, mas o branch atual é '$BR'. Abortado."; exit 1
fi
DIRTY=""; git diff --quiet || DIRTY=" (working tree com alterações não commitadas)"
echo ">> Deploy NS @ $STAMP | branch=$BR commit=$(git rev-parse --short HEAD)$DIRTY"

# ---- Build backend ----
if [ "$DO_BACKEND" = 1 ] && [ "$DO_BUILD" = 1 ]; then
  echo ">> [backend] dotnet publish..."
  rm -rf /tmp/ns-build
  "$DOTNET" publish src/Sistema.API -c Release -o /tmp/ns-build >/tmp/ns-build.log 2>&1 \
    || { echo "FALHA no build backend:"; tail -25 /tmp/ns-build.log; exit 1; }
  # sanity: o build do NS PRECISA ter o gate SaaS (senão é a base EcoGranel por engano)
  if ! grep -aql AssinaturaGateFilter /tmp/ns-build/Sistema.API.dll; then
    echo "ERRO: build sem AssinaturaGateFilter — não é a base SaaS. Abortado."; exit 1
  fi
  echo "   build ok (gate SaaS presente)."
fi

# ---- Build frontend ----
if [ "$DO_FRONTEND" = 1 ] && [ "$DO_BUILD" = 1 ]; then
  echo ">> [frontend] vite build..."
  ( cd src/Sistema.Frontend && npx vite build >/tmp/ns-fe.log 2>&1 ) \
    || { echo "FALHA no build frontend:"; tail -25 /tmp/ns-fe.log; exit 1; }
  echo "   build ok ($(grep -o 'index-[A-Za-z0-9_-]*\.js' src/Sistema.Frontend/dist/index.html | head -1))."
fi

# ---- Deploy backend (4 DLLs juntos + restart) ----
if [ "$DO_BACKEND" = 1 ]; then
  echo ">> [backend] enviando e aplicando (reinicia o serviço)..."
  $SCP "${DLLS[@]/#//tmp/ns-build/}" "$NS_HOST:/tmp/"
  $SSH "$NS_HOST" "set -e
    mkdir -p $BAK/api
    cd $API_DIR && cp ${DLLS[*]} $BAK/api/
    cp ${DLLS[*]/#//tmp/} $API_DIR/
    systemctl restart $SERVICE
    sleep 2
    printf 'serviço: '; systemctl is-active $SERVICE"
fi

# ---- Deploy frontend (dist + repõe marca do NS) ----
if [ "$DO_FRONTEND" = 1 ]; then
  echo ">> [frontend] enviando e aplicando (estático)..."
  tar -czf /tmp/ns-dist.tar.gz -C src/Sistema.Frontend/dist .
  $SCP /tmp/ns-dist.tar.gz "$NS_HOST:/tmp/"
  $SSH "$NS_HOST" "set -e
    mkdir -p $BAK
    cp -r $DIST_DIR $BAK/dist
    rm -rf $DIST_DIR/*
    tar -xzf /tmp/ns-dist.tar.gz -C $DIST_DIR
    for f in ${BRANDING[*]}; do cp $BAK/dist/\$f $DIST_DIR/ 2>/dev/null || echo \"   aviso: marca ausente no backup: \$f\"; done
    printf 'hash dist: '; grep -o 'index-[A-Za-z0-9_-]*\.js' $DIST_DIR/index.html | head -1"
fi

# ---- Health-check ----
echo ">> sanity:"
$SSH "$NS_HOST" "
  printf '   /api/branding (SaaS, 200): '; curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5131/api/branding
  printf '   /api/produtos (401):       '; curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5131/api/produtos
  printf '   app https (200):           '; curl -s -o /dev/null -w '%{http_code}\n' $APP_URL/"
echo ">> Deploy concluído. Backup do que havia antes: $BAK (no servidor)."
