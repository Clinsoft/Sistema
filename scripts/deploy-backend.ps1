# Deploy do backend (.NET 8) para o VPS.
# Publica a API em Release e envia TODAS as DLLs + metadados (.deps.json /
# .runtimeconfig.json) para /api, depois reinicia o serviço. Enviar tudo junto
# evita dois problemas: BadImageFormatException por mistura de versões dos
# Sistema.*.dll, e dependência (ex.: Newtonsoft, ImageSharp) que muda de versão
# e ficaria com o DLL antigo no runtime. NÃO remove nada (sem --delete), então
# o appsettings.Production.json no servidor fica intacto. Versão PowerShell.
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

# Sanidade: os 4 Sistema.*.dll precisam existir no publish (BadImageFormatException
# se subir so parte deles). Eles vao junto com o resto abaixo.
foreach ($d in $Dlls) {
    if (-not (Test-Path (Join-Path $PubDir $d))) { throw "DLL nao encontrada apos publish: $d" }
}

# ── 2. Envia TODAS as DLLs + metadados do publish ────────────────────
# Copiar so os 4 Sistema.*.dll quebra quando uma DEPENDENCIA muda de versao
# (ex.: Newtonsoft.Json 11->13): o runtime ficaria com o DLL antigo. Enviamos
# todos os *.dll e os metadados (.deps.json / .runtimeconfig.json), que
# descrevem as versoes resolvidas. NAO usamos --delete e nao tocamos em
# appsettings.Production.json (nao e .dll nem metadado do publish).
Write-Host "> Enviando todas as DLLs + metadados para $ApiDir ..." -ForegroundColor Cyan
$envios = @()
$envios += (Get-ChildItem -Path $PubDir -Filter '*.dll' | Select-Object -ExpandProperty FullName)
foreach ($meta in @('Sistema.API.deps.json', 'Sistema.API.runtimeconfig.json')) {
    $mp = Join-Path $PubDir $meta
    if (Test-Path $mp) { $envios += $mp }
}
& scp @ScpArgs @envios "${Host_}:$ApiDir/"
if ($LASTEXITCODE -ne 0) { throw "Falha no scp dos binarios (exit $LASTEXITCODE)" }
Write-Host "  ($($envios.Count) arquivos enviados)"

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
