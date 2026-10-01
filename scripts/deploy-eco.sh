#!/usr/bin/env bash
# =============================================================================
#  Deploy único da EcoGranel (produção) — a partir do worktree eco-prod.
#
#  Uso (Git Bash):
#     bash scripts/deploy-eco.sh                # backend + frontend
#     bash scripts/deploy-eco.sh --backend-only
#     bash scripts/deploy-eco.sh --frontend-only
#     bash scripts/deploy-eco.sh --no-build
#
#  A EcoGranel roda a BASE PRÉ-SaaS (commit 0b4fae2), no worktree D:\eco-prod
#  (branch ecogranel-prod). O HEAD/master tem o gate SaaS (AssinaturaGateFilter)
#  que quebra a EcoGranel ("Invalid object name 'Assinaturas'"). Por isso este
#  script builda do eco-prod e ABORTA se o gate SaaS aparecer no binário.
#  (O par deste é scripts/deploy-ns.sh, que é o inverso: EXIGE o gate SaaS.)
# =============================================================================
set -euo pipefail
export PATH="/c/Program Files/dotnet:/c/Program Files/nodejs:$PATH"

# ---- Config do servidor EcoGranel ----
ECO_HOST="root@177.153.194.228"
ECO_KEY="$HOME/.ssh/id_ed25519"
ECO_REPO="/d/eco-prod"                              # worktree da base pré-SaaS
API_DIR="/var/www/ecogranel/api"
DIST_DIR="/var/www/ecogranel/frontend/dist"
SERVICE="ecogranel.service"
APP_URL="https://sistema.ecogranel.com.br"
BRANCH_OK="ecogranel-prod"

DLLS=(Sistema.Domain.dll Sistema.Application.dll Sistema.Infrastructure.dll Sistema.API.dll)

STAMP="$(date +%Y%m%d-%H%M%S)"
BAK="/root/eco-bak-$STAMP"
SSH="ssh -i $ECO_KEY -o StrictHostKeyChecking=no"
SCP="scp -i $ECO_KEY -o StrictHostKeyChecking=no"
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

# ---- Guard: o worktree eco-prod existe e está no branch certo ----
if [ ! -d "$ECO_REPO" ]; then
  echo "ERRO: worktree $ECO_REPO não existe. Crie com: git worktree add -f ../eco-prod ecogranel-prod"; exit 1
fi
cd "$ECO_REPO"
BR="$(git rev-parse --abbrev-ref HEAD)"
if [ "$BR" != "$BRANCH_OK" ]; then
  echo "ERRO: EcoGranel deploya do '$BRANCH_OK', mas o eco-prod está em '$BR'. Abortado."; exit 1
fi
DIRTY=""; git diff --quiet || DIRTY=" (working tree com alterações não commitadas)"
echo ">> Deploy EcoGranel @ $STAMP | branch=$BR commit=$(git rev-parse --short HEAD)$DIRTY"

# ---- Build backend ----
if [ "$DO_BACKEND" = 1 ] && [ "$DO_BUILD" = 1 ]; then
  echo ">> [backend] dotnet publish (eco-prod)..."
  rm -rf /tmp/eco-build
  "$DOTNET" publish src/Sistema.API -c Release -o /tmp/eco-build >/tmp/eco-build.log 2>&1 \
    || { echo "FALHA no build backend:"; tail -25 /tmp/eco-build.log; exit 1; }
  # TRAVA INVERSA: a EcoGranel NÃO pode ter o gate SaaS.
  if grep -aql AssinaturaGateFilter /tmp/eco-build/Sistema.API.dll; then
    echo "ERRO: build COM AssinaturaGateFilter (gate SaaS) — isso quebra a EcoGranel. Abortado."; exit 1
  fi
  echo "   build ok (sem gate SaaS)."
fi

# ---- Build frontend ----
if [ "$DO_FRONTEND" = 1 ] && [ "$DO_BUILD" = 1 ]; then
  echo ">> [frontend] vite build (eco-prod)..."
  ( cd src/Sistema.Frontend && npx vite build >/tmp/eco-fe.log 2>&1 ) \
    || { echo "FALHA no build frontend:"; tail -25 /tmp/eco-fe.log; exit 1; }
  # sanity: dist da EcoGranel traz a marca própria e NÃO pode ter branding SaaS
  if grep -rniq "naturalsistemas\|/api/branding" src/Sistema.Frontend/dist 2>/dev/null; then
    echo "ERRO: dist contém branding/endpoint SaaS — não é a base EcoGranel. Abortado."; exit 1
  fi
  echo "   build ok ($(grep -o 'index-[A-Za-z0-9_-]*\.js' src/Sistema.Frontend/dist/index.html | head -1), logo-ecogranel embutido)."
fi

# ---- Deploy backend (4 DLLs juntos + restart) ----
if [ "$DO_BACKEND" = 1 ]; then
  echo ">> [backend] enviando e aplicando (reinicia o serviço — evite horário de pico)..."
  $SCP "${DLLS[@]/#//tmp/eco-build/}" "$ECO_HOST:/tmp/"
  $SSH "$ECO_HOST" "set -e
    mkdir -p $BAK/api
    cd $API_DIR && cp ${DLLS[*]} $BAK/api/
    cp ${DLLS[*]/#//tmp/} $API_DIR/
    systemctl restart $SERVICE
    sleep 2
    printf 'serviço: '; systemctl is-active $SERVICE"
fi

# ---- Deploy frontend (dist; marca já vem no build) ----
if [ "$DO_FRONTEND" = 1 ]; then
  echo ">> [frontend] enviando e aplicando (estático)..."
  tar -czf /tmp/eco-dist.tar.gz -C src/Sistema.Frontend/dist .
  $SCP /tmp/eco-dist.tar.gz "$ECO_HOST:/tmp/"
  $SSH "$ECO_HOST" "set -e
    mkdir -p $BAK
    cp -r $DIST_DIR $BAK/dist
    rm -rf $DIST_DIR/*
    tar -xzf /tmp/eco-dist.tar.gz -C $DIST_DIR
    printf 'hash dist: '; grep -o 'index-[A-Za-z0-9_-]*\.js' $DIST_DIR/index.html | head -1"
fi

# ---- Health-check (EcoGranel é base pré-SaaS: /api/branding deve dar 404) ----
echo ">> sanity:"
$SSH "$ECO_HOST" "
  printf '   /api/branding (base, esperado 404): '; curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5131/api/branding
  printf '   /api/produtos (esperado 401):       '; curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:5131/api/produtos
  printf '   app https (esperado 200):           '; curl -s -o /dev/null -w '%{http_code}\n' $APP_URL/"
echo ">> Deploy concluído. Backup do que havia antes: $BAK (no servidor)."
