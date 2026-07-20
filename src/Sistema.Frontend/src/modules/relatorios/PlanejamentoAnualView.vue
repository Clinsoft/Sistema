<template>
  <div>
    <!-- Cabeçalho + controles -->
    <v-row class="mb-4" align="center">
      <v-col cols="12" md="6">
        <div class="text-h6 font-weight-bold">Planejamento Anual de Vendas</div>
        <div class="text-body-2 text-medium-emphasis">
          Realizado {{ ano }} · Projeção {{ ano + 1 }} com meta de crescimento
        </div>
      </v-col>
      <v-col cols="12" md="6">
        <div class="d-flex flex-wrap ga-3 justify-end align-center">
          <v-select
            v-model="ano"
            :items="anosDisponiveis"
            label="Ano base"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width:120px"
            @update:model-value="carregar"
          />
          <v-text-field
            v-model.number="meta"
            label="Meta de crescimento %"
            type="number"
            min="0"
            max="200"
            suffix="%"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width:180px"
            @update:model-value="carregar"
          />
          <v-btn prepend-icon="mdi-content-save-outline" color="success"
            :loading="salvando" @click="salvarPlano">Salvar Plano</v-btn>
          <v-btn prepend-icon="mdi-delete-outline" variant="tonal" color="error"
            @click="limparPlano">Limpar</v-btn>
          <v-btn prepend-icon="mdi-printer-outline" variant="tonal" color="primary"
            @click="imprimir">Exportar</v-btn>
        </div>
      </v-col>
    </v-row>

    <GuiaPassos
      id="planejamento-anual"
      titulo="Como usar o Planejamento Anual"
      :passos="[
        'Escolha o <b>Ano base</b> (realizado) e a <b>Meta de crescimento %</b> — o sistema sugere a meta de cada mês para o próximo ano.',
        'Você pode <b>editar a meta de cada mês</b> diretamente na coluna <b>Meta</b> da tabela (sobrescreve a sugestão automática).',
        'Clique em <b>Salvar Plano</b> para gravar as metas do ano-alvo. Elas passam a aparecer no <b>Dashboard</b> e no acompanhamento em tempo real.',
        'Use <b>Limpar</b> para excluir o plano salvo e voltar à sugestão automática. <b>Exportar</b> imprime o planejamento.',
      ]"
    />

    <!-- Cards de totais -->
    <v-row class="mb-4">
      <v-col v-for="c in cardsTotais" :key="c.label" cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-4">
            <div class="text-caption text-medium-emphasis mb-1">{{ c.label }}</div>
            <div class="text-h6 font-weight-bold" :class="c.cor">{{ c.valor }}</div>
            <div v-if="c.sub" class="text-caption mt-1" :class="c.subCor ?? 'text-medium-emphasis'">{{ c.sub }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-row>
      <!-- Gráfico de barras + linha -->
      <v-col cols="12" md="8">
        <v-card rounded="xl" elevation="1" class="mb-4">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-chart-bar" class="mr-2" color="primary" />
            Realizado {{ ano }} vs Meta {{ ano + 1 }}
            <v-spacer />
            <div class="d-flex ga-3 text-caption">
              <span><v-icon size="12" color="primary" icon="mdi-square-rounded" /> Realizado {{ ano }}</span>
              <span><v-icon size="12" color="teal" icon="mdi-square-rounded" /> Meta {{ ano + 1 }}</span>
              <span><v-icon size="12" color="grey" icon="mdi-square-rounded" /> {{ ano - 1 }}</span>
            </div>
          </v-card-title>
          <v-card-text>
            <div v-if="carregando" class="d-flex justify-center pa-8">
              <v-progress-circular indeterminate color="primary" />
            </div>
            <canvas v-else ref="barCanvas" height="220" />
          </v-card-text>
        </v-card>

        <!-- Gráfico acumulado -->
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-chart-line" class="mr-2" color="indigo" />
            Acumulado anual — Realizado vs Meta projetada
          </v-card-title>
          <v-card-text>
            <canvas v-if="!carregando" ref="lineCanvas" height="160" />
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Painel lateral: crescimento necessário + ranking -->
      <v-col cols="12" md="4">
        <!-- Velocímetro de crescimento -->
        <v-card rounded="xl" elevation="1" class="mb-4">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold">
            <v-icon icon="mdi-speedometer" class="mr-2" color="deep-purple" />
            Crescimento necessário
          </v-card-title>
          <v-card-text class="text-center pt-0">
            <canvas ref="gaugeCanvas" height="130" />
            <div class="text-h4 font-weight-bold mt-n2"
              :class="crescimentoNecessario >= 0 ? 'text-success' : 'text-error'">
              {{ crescimentoNecessario >= 0 ? '+' : '' }}{{ crescimentoNecessario }}%
            </div>
            <div class="text-caption text-medium-emphasis mt-1">
              acima do realizado em {{ ano }} para atingir a meta de {{ meta }}%
            </div>
          </v-card-text>
        </v-card>

        <!-- Top meses -->
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold">
            <v-icon icon="mdi-podium" class="mr-2" color="amber-darken-2" />
            Meses com maior potencial
          </v-card-title>
          <v-list density="compact" class="pa-2">
            <v-list-item
              v-for="(m, i) in topMeses"
              :key="m.mes"
              rounded="lg"
              class="mb-1"
            >
              <template #prepend>
                <v-avatar
                  :color="i === 0 ? 'amber-darken-2' : i === 1 ? 'grey-lighten-1' : i === 2 ? 'brown-lighten-1' : 'surface-variant'"
                  size="28" class="mr-2 text-caption font-weight-bold">
                  {{ i + 1 }}º
                </v-avatar>
              </template>
              <v-list-item-title class="text-body-2">{{ m.nomeMes }}</v-list-item-title>
              <v-list-item-subtitle class="text-caption">
                Realizado: {{ fmtK(m.realizado) }}
              </v-list-item-subtitle>
              <template #append>
                <div class="text-right">
                  <div class="text-body-2 font-weight-bold text-teal">{{ fmtK(m.meta) }}</div>
                  <div class="text-caption text-medium-emphasis">meta</div>
                </div>
              </template>
            </v-list-item>
          </v-list>
        </v-card>
      </v-col>
    </v-row>

    <!-- Tabela detalhada -->
    <v-card rounded="xl" elevation="1" class="mt-4" id="tabela-print">
      <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
        <v-icon icon="mdi-table" class="mr-2" color="primary" />
        Detalhamento Mês a Mês
        <v-spacer />
        <v-chip size="small" label color="primary">{{ ano }}</v-chip>
        <v-icon icon="mdi-arrow-right" class="mx-2" size="16" />
        <v-chip size="small" label color="teal">Meta {{ ano + 1 }}</v-chip>
      </v-card-title>

      <v-table density="compact" class="text-body-2">
        <thead>
          <tr class="bg-surface-variant">
            <th class="text-left py-2 px-4">Mês</th>
            <th class="text-right px-3">{{ ano - 1 }}</th>
            <th class="text-right px-3">Realizado {{ ano }}</th>
            <th class="text-right px-3">Qtd Vendas</th>
            <th class="text-right px-3">Ticket Médio</th>
            <th class="text-right px-3">Descontos</th>
            <th class="text-right px-3">Var. YoY</th>
            <th class="text-right px-3 font-weight-bold text-teal">Meta {{ ano + 1 }}</th>
            <th class="text-right px-3 text-teal">Crescimento</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="m in meses"
            :key="m.mes"
            :class="m.mes === mesAtual ? 'bg-primary-lighten-5' : ''"
          >
            <td class="px-4 py-2 font-weight-medium">
              {{ m.nomeMes }}
              <v-chip v-if="m.mes === mesAtual" size="x-small" color="primary" label class="ml-1">atual</v-chip>
            </td>
            <td class="text-right px-3 text-medium-emphasis">{{ fmtR(m.realizadoAnoAnt) }}</td>
            <td class="text-right px-3 font-weight-bold">{{ fmtR(m.realizado) }}</td>
            <td class="text-right px-3">{{ m.qtdVendas }}</td>
            <td class="text-right px-3">{{ fmtR(m.ticketMedio) }}</td>
            <td class="text-right px-3 text-error">
              {{ m.totalDesconto > 0 ? '- ' + fmtR(m.totalDesconto) : '—' }}
            </td>
            <td class="text-right px-3">
              <v-chip
                v-if="m.realizadoAnoAnt > 0 || m.realizado > 0"
                size="x-small"
                :color="m.variacaoYoy > 0 ? 'success' : m.variacaoYoy < 0 ? 'error' : 'default'"
                label
              >
                {{ m.variacaoYoy > 0 ? '+' : '' }}{{ m.variacaoYoy }}%
              </v-chip>
              <span v-else class="text-medium-emphasis">—</span>
            </td>
            <td class="text-right px-3 font-weight-bold text-teal" style="min-width:130px">
              <v-text-field v-model.number="m.meta" type="number" density="compact" variant="plain"
                hide-details prefix="R$" reverse class="dre-meta-input"
                @update:model-value="recomputarMetas" />
            </td>
            <td class="text-right px-3">
              <span class="text-caption font-weight-medium text-deep-purple">
                {{ m.realizado > 0 ? (m.meta >= m.realizado ? '+' : '') + Math.round((m.meta / m.realizado - 1) * 100) + '%' : '—' }}
              </span>
            </td>
          </tr>
        </tbody>
        <tfoot>
          <tr class="bg-surface-variant font-weight-bold">
            <td class="px-4 py-2">TOTAL</td>
            <td class="text-right px-3 text-medium-emphasis">{{ fmtR(totais.totalRealizadoAnoAnt) }}</td>
            <td class="text-right px-3">{{ fmtR(totais.totalRealizadoAno) }}</td>
            <td class="text-right px-3">{{ totais.qtdTotalAno }}</td>
            <td class="text-right px-3">{{ fmtR(totais.ticketMedioGeral) }}</td>
            <td class="text-right px-3 text-error">
              {{ totais.totalDesconto > 0 ? '- ' + fmtR(totais.totalDesconto) : '—' }}
            </td>
            <td class="text-right px-3">
              <v-chip size="x-small"
                :color="totais.variacaoTotalYoy > 0 ? 'success' : 'error'"
                label>
                {{ totais.variacaoTotalYoy > 0 ? '+' : '' }}{{ totais.variacaoTotalYoy }}%
              </v-chip>
            </td>
            <td class="text-right px-3 text-teal">{{ fmtR(totais.totalMeta) }}</td>
            <td class="text-right px-3 text-deep-purple">+{{ meta }}%</td>
          </tr>
        </tfoot>
      </v-table>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick, watch } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import GuiaPassos from '@/components/GuiaPassos.vue'
import api from '@/composables/useApi'

const auth = useAuthStore()
const notif = useNotifStore()
const salvando = ref(false)
const anoAlvo = computed(() => ano.value + 1)

const barCanvas  = ref<HTMLCanvasElement>()
const lineCanvas = ref<HTMLCanvasElement>()
const gaugeCanvas = ref<HTMLCanvasElement>()

const anoAtual = new Date().getFullYear()
const mesAtual = new Date().getMonth() + 1
// Base = ano anterior, para que a PROJEÇÃO (ano base + 1) seja o ano atual —
// assim o plano criado aqui é o mesmo que o Dashboard mostra (ano atual).
const ano = ref(anoAtual - 1)
const meta = ref(10)
const carregando = ref(false)

const anosDisponiveis = Array.from({ length: 5 }, (_, i) => anoAtual - i)

// ── Tipos ────────────────────────────────────────────────────────────────────
interface MesData {
  mes: number; nomeMes: string
  realizado: number; qtdVendas: number; ticketMedio: number; totalDesconto: number
  realizadoAnoAnt: number; qtdVendasAnoAnt: number; variacaoYoy: number
  meta: number; crescimentoMeta: number; atingiuMeta: boolean
}
interface Acumulado { mes: number; acumAno: number; acumMeta: number }
interface Totais {
  totalRealizadoAno: number; totalRealizadoAnoAnt: number; totalMeta: number
  qtdTotalAno: number; ticketMedioGeral: number; variacaoTotalYoy: number
  crescimentoNecessario: number; totalDesconto: number
}

const meses = ref<MesData[]>([])
const acumulados = ref<Acumulado[]>([])
const totais = ref<Totais>({
  totalRealizadoAno: 0, totalRealizadoAnoAnt: 0, totalMeta: 0,
  qtdTotalAno: 0, ticketMedioGeral: 0, variacaoTotalYoy: 0,
  crescimentoNecessario: 0, totalDesconto: 0
})

const crescimentoNecessario = computed(() => totais.value.crescimentoNecessario)

// Top 5 meses por valor de meta (maior oportunidade)
const topMeses = computed(() =>
  [...meses.value].sort((a, b) => b.meta - a.meta).slice(0, 5)
)

// ── Formatação ───────────────────────────────────────────────────────────────
const fmtR = (v: number) =>
  'R$ ' + (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

const fmtK = (v: number) => {
  if (v >= 1_000_000) return 'R$ ' + (v / 1_000_000).toFixed(1).replace('.', ',') + 'M'
  if (v >= 1_000)     return 'R$ ' + (v / 1_000).toFixed(1).replace('.', ',') + 'k'
  return fmtR(v)
}

// ── Cards de totais ──────────────────────────────────────────────────────────
const cardsTotais = computed(() => {
  const t = totais.value
  const varYoy = t.variacaoTotalYoy
  return [
    {
      label: `Total Realizado ${ano.value}`,
      valor: fmtK(t.totalRealizadoAno),
      cor: 'text-primary',
      sub: `${t.qtdTotalAno} vendas · TM ${fmtK(t.ticketMedioGeral)}`,
    },
    {
      label: `Variação vs ${ano.value - 1}`,
      valor: (varYoy >= 0 ? '+' : '') + varYoy + '%',
      cor: varYoy >= 0 ? 'text-success' : 'text-error',
      sub: fmtK(t.totalRealizadoAnoAnt),
      subCor: 'text-medium-emphasis',
    },
    {
      label: `Meta ${ano.value + 1} (+${meta.value}%)`,
      valor: fmtK(t.totalMeta),
      cor: 'text-teal',
      sub: `+ ${fmtK(t.totalMeta - t.totalRealizadoAno)} acima`,
      subCor: 'text-teal',
    },
    {
      label: 'Crescimento necessário',
      valor: (crescimentoNecessario.value >= 0 ? '+' : '') + crescimentoNecessario.value + '%',
      cor: crescimentoNecessario.value <= 15 ? 'text-success' : crescimentoNecessario.value <= 30 ? 'text-warning' : 'text-error',
      sub: 'para atingir a meta',
    },
  ]
})

// ── Carregar dados ───────────────────────────────────────────────────────────
let debounceTimer: ReturnType<typeof setTimeout>

async function carregar() {
  clearTimeout(debounceTimer)
  debounceTimer = setTimeout(_carregar, 400)
}

async function _carregar() {
  if (!auth.empresaId) return
  carregando.value = true
  try {
    const res = await api.get('/relatorios/vendas/anual', {
      params: { empresaId: auth.empresaId, ano: ano.value, crescimentoMeta: meta.value }
    })
    const d = res.data
    meses.value     = d.meses
    acumulados.value = d.acumulados
    totais.value    = {
      ...d.totais,
      totalDesconto: d.meses.reduce((s: number, m: MesData) => s + m.totalDesconto, 0)
    }

    // Sobrepõe com metas SALVAS do ano-alvo, se existirem
    try {
      const rp = await api.get('/relatorios/planejamento-anual', {
        params: { empresaId: auth.empresaId, ano: anoAlvo.value },
      })
      const salvo = rp.data
      if (salvo?.meses?.length) {
        const mapa: Record<number, number> = {}
        salvo.meses.forEach((m: any) => { mapa[m.mes] = m.meta })
        meses.value.forEach(m => { if (mapa[m.mes] != null && mapa[m.mes] > 0) m.meta = mapa[m.mes] })
        recomputarTotaisMetas()
      }
    } catch { /* sem plano salvo — usa a sugestão automática */ }

    await nextTick()
    desenharBarras()
    desenharLinha()
    desenharGauge()
  } catch {
    /* sem dados — estado vazio */
  } finally {
    carregando.value = false
  }
}

// Recalcula totais/acumulados de meta a partir das metas (editadas) e redesenha
function recomputarTotaisMetas() {
  const totalMeta = meses.value.reduce((s, m) => s + (m.meta ?? 0), 0)
  totais.value.totalMeta = totalMeta
  totais.value.crescimentoNecessario = totais.value.totalRealizadoAno > 0
    ? Math.round((totalMeta - totais.value.totalRealizadoAno) / totais.value.totalRealizadoAno * 100)
    : 0
  let acumMeta = 0
  acumulados.value = meses.value.map(m => {
    acumMeta += m.meta ?? 0
    const a = acumulados.value.find(x => x.mes === m.mes)
    return { mes: m.mes, acumAno: a?.acumAno ?? 0, acumMeta }
  })
}

let redrawTimer: ReturnType<typeof setTimeout>
function recomputarMetas() {
  recomputarTotaisMetas()
  clearTimeout(redrawTimer)
  redrawTimer = setTimeout(() => { desenharBarras(); desenharLinha(); desenharGauge() }, 250)
}

async function salvarPlano() {
  salvando.value = true
  try {
    await api.post('/relatorios/planejamento-anual', {
      empresaId: auth.empresaId,
      ano: anoAlvo.value,
      metas: meses.value.map(m => ({ mes: m.mes, valor: m.meta ?? 0 })),
    })
    notif.ok(`Plano de ${anoAlvo.value} salvo!`)
  } catch { notif.erro('Erro ao salvar o plano.') }
  finally { salvando.value = false }
}

async function limparPlano() {
  if (!confirm(`Excluir o plano salvo de ${anoAlvo.value}? As metas voltam à sugestão automática.`)) return
  try {
    await api.delete('/relatorios/planejamento-anual', {
      params: { empresaId: auth.empresaId, ano: anoAlvo.value },
    })
    notif.ok('Plano excluído.')
    await _carregar()
  } catch { notif.erro('Erro ao excluir o plano.') }
}

// ── Gráfico de barras agrupadas ───────────────────────────────────────────────
function desenharBarras() {
  const canvas = barCanvas.value
  if (!canvas || !meses.value.length) return
  const ctx = canvas.getContext('2d')!
  const W = canvas.offsetWidth || 600
  const H = 220
  canvas.width = W; canvas.height = H

  const labels = meses.value.map(m => m.nomeMes)
  const realizadoAnt = meses.value.map(m => m.realizadoAnoAnt)
  const realizadoAno = meses.value.map(m => m.realizado)
  const metaProj     = meses.value.map(m => m.meta)

  const maxV = Math.max(...realizadoAnt, ...realizadoAno, ...metaProj, 1)
  const padL = 52, padR = 8, padT = 16, padB = 28
  const areaW = W - padL - padR
  const areaH = H - padT - padB
  const grupoW = areaW / 12
  const barW = Math.floor(grupoW / 3) - 2

  ctx.clearRect(0, 0, W, H)

  // Linhas de grade
  for (let i = 0; i <= 4; i++) {
    const y = padT + areaH - (i / 4) * areaH
    ctx.strokeStyle = '#e0e0e0'; ctx.lineWidth = 1
    ctx.beginPath(); ctx.moveTo(padL, y); ctx.lineTo(W - padR, y); ctx.stroke()
    const val = (maxV * i / 4)
    ctx.fillStyle = '#9e9e9e'; ctx.font = '9px sans-serif'; ctx.textAlign = 'right'
    ctx.fillText(fmtK(val), padL - 4, y + 3)
  }

  // Barras
  meses.value.forEach((m, i) => {
    const gx = padL + i * grupoW + 2

    const drawBar = (val: number, x: number, cor: string) => {
      const bh = Math.round((val / maxV) * areaH)
      const by = padT + areaH - bh
      ctx.fillStyle = cor
      ctx.beginPath()
      ctx.roundRect(x, by, barW, bh, [3, 3, 0, 0])
      ctx.fill()
    }

    drawBar(m.realizadoAnoAnt, gx,              '#bdbdbd')
    drawBar(m.realizado,       gx + barW + 2,   m.mes === mesAtual && ano.value === anoAtual ? '#1565c0' : '#1976d2')
    drawBar(m.meta,            gx + barW*2 + 4, '#00897b')

    // Label mês
    ctx.fillStyle = m.mes === mesAtual ? '#1976d2' : '#757575'
    ctx.font = m.mes === mesAtual ? 'bold 9px sans-serif' : '9px sans-serif'
    ctx.textAlign = 'center'
    ctx.fillText(m.nomeMes, gx + grupoW / 2 - 2, H - 8)
  })
}

// ── Gráfico de linha acumulada ────────────────────────────────────────────────
function desenharLinha() {
  const canvas = lineCanvas.value
  if (!canvas || !acumulados.value.length) return
  const ctx = canvas.getContext('2d')!
  const W = canvas.offsetWidth || 600
  const H = 160
  canvas.width = W; canvas.height = H

  const maxV = Math.max(...acumulados.value.map(a => a.acumMeta), 1)
  const padL = 52, padR = 8, padT = 12, padB = 24
  const areaW = W - padL - padR
  const areaH = H - padT - padB

  ctx.clearRect(0, 0, W, H)

  // Grade
  for (let i = 0; i <= 3; i++) {
    const y = padT + areaH - (i / 3) * areaH
    ctx.strokeStyle = '#e0e0e0'; ctx.lineWidth = 1
    ctx.beginPath(); ctx.moveTo(padL, y); ctx.lineTo(W - padR, y); ctx.stroke()
    ctx.fillStyle = '#9e9e9e'; ctx.font = '9px sans-serif'; ctx.textAlign = 'right'
    ctx.fillText(fmtK(maxV * i / 3), padL - 4, y + 3)
  }

  const px = (i: number) => padL + (i / 11) * areaW
  const py = (v: number) => padT + areaH - (v / maxV) * areaH

  const drawLine = (data: number[], cor: string, dash: number[] = []) => {
    ctx.strokeStyle = cor; ctx.lineWidth = 2.5
    ctx.setLineDash(dash)
    ctx.beginPath()
    data.forEach((v, i) => i === 0 ? ctx.moveTo(px(i), py(v)) : ctx.lineTo(px(i), py(v)))
    ctx.stroke()
    ctx.setLineDash([])
    // Pontos
    data.forEach((v, i) => {
      ctx.fillStyle = cor
      ctx.beginPath(); ctx.arc(px(i), py(v), 3, 0, Math.PI * 2); ctx.fill()
    })
  }

  const realizados = acumulados.value.map(a => a.acumAno)
  const metas      = acumulados.value.map(a => a.acumMeta)

  drawLine(metas,      '#00897b', [6, 3])
  drawLine(realizados, '#1976d2')

  // Labels meses
  ctx.fillStyle = '#9e9e9e'; ctx.font = '9px sans-serif'; ctx.textAlign = 'center'
  meses.value.forEach((m, i) => ctx.fillText(m.nomeMes, px(i), H - 6))
}

// ── Gauge (semicírculo) ───────────────────────────────────────────────────────
function desenharGauge() {
  const canvas = gaugeCanvas.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')!
  const W = canvas.offsetWidth || 220; const H = 130
  canvas.width = W; canvas.height = H

  const cx = W / 2, cy = H - 20, r = Math.min(cx, cy + 20) - 16
  const startA = Math.PI
  const endA   = 0

  // Trilha
  ctx.strokeStyle = '#e0e0e0'; ctx.lineWidth = 18
  ctx.beginPath(); ctx.arc(cx, cy, r, startA, endA); ctx.stroke()

  // Arco colorido
  const pct   = Math.min(Math.max((crescimentoNecessario.value) / 50, 0), 1)
  const cor   = crescimentoNecessario.value <= 15 ? '#4caf50'
              : crescimentoNecessario.value <= 30 ? '#ff9800' : '#f44336'
  const angle = startA + pct * Math.PI

  ctx.strokeStyle = cor; ctx.lineWidth = 18; ctx.lineCap = 'round'
  ctx.beginPath(); ctx.arc(cx, cy, r, startA, angle); ctx.stroke()

  // Rótulos extremos
  ctx.fillStyle = '#9e9e9e'; ctx.font = '10px sans-serif'
  ctx.textAlign = 'left';  ctx.fillText('0%',  cx - r - 4, cy + 16)
  ctx.textAlign = 'right'; ctx.fillText('50%', cx + r + 4, cy + 16)
}

onMounted(() => carregar())
watch([ano, meta], () => carregar())

function imprimir() {
  window.print()
}
</script>

<style scoped>
.dre-meta-input :deep(input) { text-align: right; font-weight: 700; color: rgb(var(--v-theme-teal, 0 137 123)); font-size: 0.875rem; }
@media print {
  .v-btn, .v-select { display: none !important; }
  .dre-meta-input :deep(.v-field__prepend-inner) { display: none; }
}
</style>
