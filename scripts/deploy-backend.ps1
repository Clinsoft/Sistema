# Deploy do backend — agora SEM DOWNTIME (blue-green).
#
# O antigo restart direto do serviço (que derrubava a API por alguns segundos)
# foi substituído pelo deploy blue-green: sobe a versão nova na instância inativa
# (blue=5131 / green=5132), faz health-check e o nginx troca com reload gracioso.
# Este arquivo agora apenas redireciona para deploy-backend-zero.ps1 para evitar
# reinícios com downtime durante o horário de loja.
& (Join-Path $PSScriptRoot 'deploy-backend-zero.ps1')
