# Deploy do backend SEM DOWNTIME (blue-green).
# Publica a API, envia os binarios para um staging no servidor e chama
# /root/deploy_zero_downtime.sh, que sobe a versao nova na instancia INATIVA
# (blue=5131 / green=5132), faz health-check e so entao troca o upstream do
# nginx com reload gracioso (sem derrubar conexoes), parando a instancia antiga.
# Uso:  .\scripts\deploy-backend-zero.ps1
$ErrorActionPreference = 'Continue'

$SshKey  = if ($env:SSH_KEY) { $env:SSH_KEY } else { 'C:/Users/User/.ssh/id_ed25519' }
$Host_   = if ($env:HOST)    { $env:HOST }    else { 'root@177.153.194.228' }
$Stage   = '/tmp/ecogranel-stage'
$Csproj  = Join-Path $PSScriptRoot '..\src\Sistema.API\Sistema.API.csproj'
$PubDir  = Join-Path $env:TEMP 'ecogranel-pub'
$SshArgs = @('-i', $SshKey, '-o', 'StrictHostKeyChecking=no', $Host_)
$ScpArgs = @('-i', $SshKey, '-o', 'StrictHostKeyChecking=no')
$Dlls = @('Sistema.API.dll', 'Sistema.Application.dll', 'Sistema.Domain.dll', 'Sistema.Infrastructure.dll')

Write-Host "> Publicando backend (Release)..." -ForegroundColor Cyan
& dotnet publish $Csproj -c Release -o $PubDir --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou (exit $LASTEXITCODE)" }
foreach ($d in $Dlls) { if (-not (Test-Path (Join-Path $PubDir $d))) { throw "DLL nao encontrada apos publish: $d" } }

Write-Host "> Enviando binarios para staging ($Stage)..." -ForegroundColor Cyan
& ssh @SshArgs "rm -rf $Stage && mkdir -p $Stage"
if ($LASTEXITCODE -ne 0) { throw "Falha ao preparar staging (exit $LASTEXITCODE)" }
$envios = @()
$envios += (Get-ChildItem -Path $PubDir -Filter '*.dll' | Select-Object -ExpandProperty FullName)
foreach ($meta in @('Sistema.API.deps.json', 'Sistema.API.runtimeconfig.json')) {
    $mp = Join-Path $PubDir $meta
    if (Test-Path $mp) { $envios += $mp }
}
& scp @ScpArgs @envios "${Host_}:$Stage/"
if ($LASTEXITCODE -ne 0) { throw "Falha no scp dos binarios (exit $LASTEXITCODE)" }
Write-Host "  ($($envios.Count) arquivos enviados)"

Write-Host "> Flip blue-green (sem downtime)..." -ForegroundColor Cyan
$out = & ssh @SshArgs "bash /root/deploy_zero_downtime.sh $Stage"
$out | ForEach-Object { Write-Host "  $_" }
if ($LASTEXITCODE -ne 0) { throw "Deploy sem downtime FALHOU - a versao anterior continua ativa (rollback automatico). Veja a saida acima." }
Write-Host "OK - Deploy sem downtime concluido." -ForegroundColor Green
