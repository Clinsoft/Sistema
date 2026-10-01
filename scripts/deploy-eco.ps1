# Atalho PowerShell para o deploy da EcoGranel.
# Apenas localiza o Git Bash e executa o scripts/deploy-eco.sh (que faz todo o trabalho).
#
# Uso (de qualquer pasta):
#   & "D:\Sistema\scripts\deploy-eco.ps1"
#   & "D:\Sistema\scripts\deploy-eco.ps1" --frontend-only
#   & "D:\Sistema\scripts\deploy-eco.ps1" --backend-only
#   & "D:\Sistema\scripts\deploy-eco.ps1" --no-build
#   & "D:\Sistema\scripts\deploy-eco.ps1" --dry-run   # simula, sem tocar no servidor

$ErrorActionPreference = 'Stop'

$bash = @(
  "C:\Program Files\Git\bin\bash.exe",
  "C:\Program Files\Git\usr\bin\bash.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $bash) {
  Write-Error "Git Bash nao encontrado. Instale o Git for Windows ou ajuste o caminho."
  exit 1
}

# Caminho do .sh ao lado deste .ps1, convertido para formato do Git Bash (ex.: /d/Sistema/...).
$sh = Join-Path $PSScriptRoot 'deploy-eco.sh'
$drive = ($sh.Substring(0,1)).ToLower()
$rest  = ($sh.Substring(2)) -replace '\\','/'
$shBash = "/$drive$rest"

& $bash $shBash @args
exit $LASTEXITCODE
