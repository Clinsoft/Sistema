# Aplica as migrations de banco PENDENTES em producao, de forma SEGURA.
#
# Gera um script SQL IDEMPOTENTE com o EF (cada migration vem embrulhada em
# "IF NOT EXISTS (... __EFMigrationsHistory ...)"), envia ao VPS e roda via
# sqlcmd. Rodar de novo nao repete nada — aplica so o que falta. A conexao e
# lida do appsettings NO servidor (a senha nao passa pela sua maquina).
#
# Rode ANTES do deploy-backend quando houver migration nova.
# Uso:  .\scripts\deploy-migrations.ps1   (ou -WhatIf para so gerar e nao aplicar)

param([switch]$WhatIf)

$ErrorActionPreference = 'Continue'

# ── Config ────────────────────────────────────────────────────────────
$SshKey  = if ($env:SSH_KEY) { $env:SSH_KEY } else { 'C:/Users/User/.ssh/id_ed25519' }
$Host_   = if ($env:HOST)    { $env:HOST }    else { 'root@177.153.194.228' }
$Infra   = Join-Path $PSScriptRoot '..\src\Sistema.Infrastructure'
$Api     = Join-Path $PSScriptRoot '..\src\Sistema.API'
$SqlLocal = Join-Path $env:TEMP 'ecogranel-migrations.sql'

$SshArgs = @('-i', $SshKey, '-o', 'StrictHostKeyChecking=no', $Host_)
$ScpArgs = @('-i', $SshKey, '-o', 'StrictHostKeyChecking=no')

# ── 1. Gera o script idempotente ─────────────────────────────────────
Write-Host "> Gerando script de migrations (idempotente)..." -ForegroundColor Cyan
& dotnet ef migrations script --idempotent --project $Infra --startup-project $Api -o $SqlLocal
if ($LASTEXITCODE -ne 0) { throw "Falha ao gerar o script de migrations (dotnet ef)." }
Write-Host "  Script: $SqlLocal ($((Get-Item $SqlLocal).Length) bytes)"

if ($WhatIf) {
    Write-Host "WhatIf: script gerado, nada aplicado." -ForegroundColor Yellow
    return
}

# ── 2. Envia ao servidor ─────────────────────────────────────────────
Write-Host "> Enviando ao servidor..." -ForegroundColor Cyan
& scp @ScpArgs $SqlLocal "${Host_}:/tmp/ecogranel-migrations.sql"
if ($LASTEXITCODE -ne 0) { throw "Falha no scp do script de migrations." }

# ── 3. Aplica via runner no servidor (evita problemas de CRLF do Windows) ──
# A lógica de conexão/sqlcmd vive em /root/run_sql_file.sh (LF, no servidor);
# aqui só chamamos numa linha, passando o arquivo enviado.
Write-Host "> Aplicando migrations pendentes..." -ForegroundColor Cyan
& ssh @SshArgs "bash /root/run_sql_file.sh /tmp/ecogranel-migrations.sql"
if ($LASTEXITCODE -ne 0) { throw "Falha ao aplicar as migrations no servidor." }

Write-Host "OK - Migrations aplicadas. Agora rode deploy-backend.ps1." -ForegroundColor Green
