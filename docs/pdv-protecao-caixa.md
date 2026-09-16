# Proteção de Caixa (PDV)

Regras que evitam caixa "solto" — sessão de PDV deixada aberta e carregada para o
dia seguinte, misturando vendas de vários dias num único fechamento.

## Regras

1. **Múltiplos caixas por loja são permitidos.** Vários operadores podem ter caixa
   aberto ao mesmo tempo na mesma unidade. Continua valendo **1 caixa por pessoa**
   (o mesmo operador não tem duas sessões abertas).

2. **Fechamento forçado do caixa deixado aberto**, em três momentos:

   | Gatilho | Onde | Quando age |
   |---|---|---|
   | **Entrada no PDV (retomada)** | `PDVSessaoController.SessaoAberta` (`GET /pdv/sessoes/aberta`) | Se a sessão aberta é de um **dia anterior**, fecha automaticamente e retorna "sem sessão" → o PDV pede para abrir um caixa novo (não retoma o de ontem). |
   | **Ao reabrir** | `AbrirSessaoHandler` (`POST /pdv/sessoes/abrir`) | Se o operador já tem uma sessão aberta, fecha a antiga antes de abrir a nova (em vez de bloquear). |
   | **Job noturno** | `FechamentoCaixaJob` (`caixa-fechamento-automatico`, 03:00 BRT) | Fecha todas as sessões abertas de dias anteriores, mesmo que o operador não volte a abrir. |

## Saldo do fechamento automático

Todos os três caminhos fecham com o **saldo esperado calculado pelo sistema**
(diferença zero), pois ninguém contou a gaveta:

```
saldoFechamento = SaldoAbertura + (Dinheiro recebido − Troco) + Suprimentos − Sangrias
```

Só o **dinheiro** entra (cartão/Pix/crediário não ficam na gaveta), e do dinheiro
recebido desconta-se o **troco** devolvido. A observação registra a origem, ex.:
`"Fechamento automático — caixa deixado aberto desde 15/09/2026 09:00."`

## Monitoramento

O painel **"Caixas abertos agora"** (tela de Sessões / `GET /pdv/sessoes/abertas`)
lista todos os caixas abertos e destaca em alerta os que estão
`aberto há mais de 14h` **ou** `abertos em dia anterior` (`alertaAberto`).

## Testes

`tests/Sistema.UnitTests/Application/Vendas/AbrirSessaoHandlerTests.cs` cobre o
fechamento forçado ao reabrir:
- abre sem sessão anterior → cria nova;
- com sessão deixada aberta → fecha a antiga (saldo esperado) e abre a nova;
- fecha com o saldo esperado correto (fundo + dinheiro líquido).

Validado em produção em 16/09/2026: caixa esquecido (aberto 15/09) fechado
automaticamente ao reentrar no PDV, com `SaldoFechamento` = fundo e a observação
de fechamento automático.
