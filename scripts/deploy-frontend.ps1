# Deploy do frontend (Vue/Vite) para o VPS SEM acúmulo de assets órfãos.
# Versão PowerShell (o terminal padrão do projeto). Usa ssh/scp do Windows e
# roda `rsync -a --delete` NO servidor (staging -> produção), removendo os
# chunks antigos a cada deploy — mesmo efeito do rsync --delete clássico.
#
# Uso:  powershell -ExecutionPolicy Bypass -File scripts\deploy-frontend.ps1
#   ou, no PowerShell:  .\scripts\deploy-frontend.ps1

# 'Continue' (não 'Stop'): npm/vite escrevem no stderr e, sob 'Stop', o
# PowerShell 5.1 trataria isso como erro terminante mesmo com exit 0. As
# falhas reais são pegas pelas checagens explícitas de $LASTEXITCODE + throw.
$ErrorActionPreference = 'Continue'

# ── Config ────────────────────────────────────────────────────────────
$SshKey  = if ($env:SSH_KEY) { $env:SSH_KEY } else { 'C:/Users/User/.ssh/id_ed25519' }
$Host_   = if ($env:HOST)    { $env:HOST }    else { 'root@177.153.194.228' }
$Live    = '/var/www/ecogranel/frontend/dist'
$Staging = '/tmp/ecogranel-fe-staging'
$FeDir   = Join-Path $PSScriptRoot '..\src\Sistema.Frontend'
$DistDir = Join-Path $FeDir 'dist'

$SshArgs = @('-i', $SshKey, '-o', 'StrictHostKeyChecking=no', $Host_)
$ScpArgs = @('-i', $SshKey, '-o', 'StrictHostKeyChecking=no')

# ── 1. Build ──────────────────────────────────────────────────────────
Write-Host "> Build do frontend..." -ForegroundColor Cyan
Push-Location $FeDir
try {
    & npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build falhou (exit $LASTEXITCODE)" }
} finally { Pop-Location }

# ── 2. Sobe para staging (pasta limpa a cada deploy) ─────────────────
Write-Host "> Enviando build para staging no servidor..." -ForegroundColor Cyan
& ssh @SshArgs "rm -rf $Staging && mkdir -p $Staging"
if ($LASTEXITCODE -ne 0) { throw "Falha ao preparar staging (exit $LASTEXITCODE)" }
& scp @ScpArgs -r "$DistDir/." "${Host_}:$Staging/"
if ($LASTEXITCODE -ne 0) { throw "Falha no scp (exit $LASTEXITCODE)" }

# ── 3. rsync --delete no servidor: staging -> produção ───────────────
Write-Host "> Sincronizando com --delete (remove orfaos)..." -ForegroundColor Cyan
& ssh @SshArgs "rsync -a --delete $Staging/ $Live/ && rm -rf $Staging"
if ($LASTEXITCODE -ne 0) { throw "Falha no rsync (exit $LASTEXITCODE)" }

# ── 4. Verificação ───────────────────────────────────────────────────
$localIdx = (Select-String -Path (Join-Path $DistDir 'index.html') -Pattern 'index-[A-Za-z0-9_-]*\.js' |
    Select-Object -First 1).Matches[0].Value
$srvIdx = & ssh @SshArgs "grep -o 'index-[A-Za-z0-9_-]*\.js' $Live/index.html | head -1"
Write-Host "> index local:    $localIdx"
Write-Host "> index servidor: $srvIdx"
if ($localIdx -eq $srvIdx) {
    Write-Host "OK - Deploy concluido (sem orfaos). Peca Ctrl+Shift+R ao usuario." -ForegroundColor Green
} else {
    throw "index divergente - confira o deploy."
}
