# Deploy do backend (.NET 8) para o VPS.
# Publica a API em Release e envia os 4 Sistema.*.dll JUNTOS para /api, depois
# reinicia o serviço. Enviar os 4 juntos evita BadImageFormatException por
# mistura de versões (o erro real fica mascarado). Versão PowerShell.
#
# Uso:  .\scripts\deploy-backend.ps1
#   ou: powershell -ExecutionPolicy Bypass -File scripts\deploy-backend.ps1

# 'Continue' (não 'Stop'): dotnet escreve no stderr e, sob 'Stop', o PowerShell
# 5.1 trataria isso como erro terminante mesmo com exit 0. Falhas reais são
# pegas pelas checagens explícitas de $LASTEXITCODE + throw.
$ErrorActionPreference = 'Continue'

# ── Config ────────────────────────────────────────────────────────────
$SshKey  = if ($env:SSH_KEY) { $env:SSH_KEY } else { 'C:/Users/User/.ssh/id_ed25519' }
$Host_   = if ($env:HOST)    { $env:HOST }    else { 'root@177.153.194.228' }
$ApiDir  = '/var/www/ecogranel/api'
$Service = 'ecogranel'
$Csproj  = Join-Path $PSScriptRoot '..\src\Sistema.API\Sistema.API.csproj'
$PubDir  = Join-Path $env:TEMP 'ecogranel-pub'

$SshArgs = @('-i', $SshKey, '-o', 'StrictHostKeyChecking=no', $Host_)
$ScpArgs = @('-i', $SshKey, '-o', 'StrictHostKeyChecking=no')

# DLLs do projeto que precisam subir JUNTOS (não misturar versões).
$Dlls = @('Sistema.API.dll', 'Sistema.Application.dll', 'Sistema.Domain.dll', 'Sistema.Infrastructure.dll')

# ── 1. Publish ────────────────────────────────────────────────────────
Write-Host "> Publicando backend (Release)..." -ForegroundColor Cyan
& dotnet publish $Csproj -c Release -o $PubDir --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou (exit $LASTEXITCODE)" }

# Confere que os 4 DLLs existem antes de enviar.
$paths = foreach ($d in $Dlls) {
    $p = Join-Path $PubDir $d
    if (-not (Test-Path $p)) { throw "DLL nao encontrada apos publish: $d" }
    $p
}

# ── 2. Envia os 4 DLLs juntos ────────────────────────────────────────
Write-Host "> Enviando os 4 Sistema.*.dll para $ApiDir ..." -ForegroundColor Cyan
& scp @ScpArgs @paths "${Host_}:$ApiDir/"
if ($LASTEXITCODE -ne 0) { throw "Falha no scp dos DLLs (exit $LASTEXITCODE)" }

# ── 3. Reinicia o serviço e confere ──────────────────────────────────
Write-Host "> Reiniciando o servico $Service ..." -ForegroundColor Cyan
$status = & ssh @SshArgs "systemctl restart $Service && sleep 3 && systemctl is-active $Service"
if ($LASTEXITCODE -ne 0) { throw "Falha ao reiniciar/checar o servico (exit $LASTEXITCODE)" }
$status = ($status | Select-Object -Last 1).Trim()

Write-Host "> Status do servico: $status"
if ($status -eq 'active') {
    Write-Host "OK - Backend publicado e servico ativo." -ForegroundColor Green
} else {
    throw "Servico nao esta ativo (status: $status) - rode: ssh ... journalctl -u $Service -n 50"
}
