<template>
  <div>
    <v-row class="mb-2">
      <v-col>
        <div class="text-h6 font-weight-bold">Bom dia, {{ auth.usuario?.nome?.split(' ')?.[0] }}!</div>
        <div class="text-body-2 text-medium-emphasis">{{ dataHoje }}</div>
      </v-col>
    </v-row>

    <!-- Cards de resumo -->
    <v-row>
      <v-col v-for="card in cards" :key="card.titulo" cols="12" sm="6" md="3">
        <v-card rounded="xl" elevation="1" :to="card.to">
          <v-card-text class="d-flex align-center pa-4">
            <v-avatar :color="card.cor" size="52" class="mr-4">
              <v-icon :icon="card.icon" color="white" size="28" />
            </v-avatar>
            <div>
              <div class="text-h5 font-weight-bold">{{ card.valor }}</div>
              <div class="text-caption text-medium-emphasis">{{ card.titulo }}</div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Gráfico de vendas + atalhos -->
    <v-row class="mt-2">
      <v-col cols="12" md="8">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-0 text-body-1 font-weight-bold">
            Vendas — últimos 7 dias
          </v-card-title>
          <v-card-text>
            <div v-if="carregando" class="d-flex justify-center pa-8">
              <v-progress-circular indeterminate color="primary" />
            </div>
            <canvas v-else ref="graficoCanvas" height="200" />
          </v-card-text>
        </v-card>
      </v-col>

      <v-col cols="12" md="4">
        <v-card rounded="xl" elevation="1" height="100%" style="display:flex;flex-direction:column">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-calendar-month-outline" class="mr-2" color="blue-darken-2" />
            Planejamento — {{ anoAtual }}
            <v-spacer />
            <v-btn size="x-small" variant="tonal" color="blue-darken-2" to="/relatorios/planejamento-anual">
              Ver
            </v-btn>
          </v-card-title>

          <v-card-text v-if="!planejamento" class="text-center text-medium-emphasis flex-grow-1 d-flex flex-column justify-center pa-6">
            <v-icon icon="mdi-chart-timeline-variant-shimmer" size="36" class="mb-2" color="blue-lighten-3" />
            <div class="text-body-2">Nenhum planejamento para {{ anoAtual }}.</div>
            <v-btn class="mt-3" size="small" variant="tonal" color="blue-darken-2" to="/relatorios/planejamento-anual">
              Criar Planejamento
            </v-btn>
          </v-card-text>

          <div v-else class="pa-3 pt-0 flex-grow-1">
            <div v-for="item in planejamento.meses" :key="item.mes" class="mb-2">
              <div class="d-flex align-center mb-1">
                <span class="text-caption text-medium-emphasis flex-grow-1">{{ item.nomeMes }}</span>
                <span class="text-caption font-weight-bold"
                  :class="item.realizado >= item.meta ? 'text-success' : item.mes <= mesAtualNum ? 'text-warning' : ''">
                  {{ item.realizado > 0 ? fmt(item.realizado) : '—' }}
                </span>
              </div>
              <v-progress-linear
                :model-value="item.meta > 0 ? Math.min((item.realizado / item.meta) * 100, 100) : 0"
                :color="item.realizado >= item.meta ? 'success' : item.mes <= mesAtualNum ? 'warning' : 'blue-lighten-3'"
                height="5" rounded
              />
            </div>
            <v-divider class="my-2" />
            <div class="d-flex justify-space-between text-caption">
              <span class="text-medium-emphasis">Realizado YTD</span>
              <span class="font-weight-bold text-blue-darken-2">{{ fmt(planejamento.totalRealizado) }}</span>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Ponto de Equilíbrio + Contas a Pagar do dia -->
    <v-row class="mt-2">
      <v-col cols="12" md="7">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-chart-donut" class="mr-2" color="deep-purple" />
            Ponto de Equilíbrio — {{ mesAtual }}
            <v-spacer />
            <v-chip v-if="pe" :color="pe.peAtingido ? 'success' : 'warning'" size="small" label>
              {{ pe.peAtingido ? 'Atingido ✓' : pe.percentualAtingido + '% do PE' }}
            </v-chip>
          </v-card-title>

          <v-card-text v-if="carregandoPe" class="d-flex justify-center pa-8">
            <v-progress-circular indeterminate color="deep-purple" />
          </v-card-text>

          <v-card-text v-else-if="!pe || pe.pontoEquilibrio === 0" class="text-center text-medium-emphasis pa-6">
            <v-icon icon="mdi-information-outline" class="mb-2" size="32" />
            <div class="text-body-2">Cadastre os <strong>custos fixos</strong> do mês para calcular o ponto de equilíbrio.</div>
            <v-btn class="mt-3" size="small" variant="tonal" color="deep-purple" to="/financeiro/custos-fixos">
              Cadastrar custos
            </v-btn>
          </v-card-text>

          <v-card-text v-else class="pb-4">
            <div class="d-flex justify-space-between text-caption text-medium-emphasis mb-1">
              <span>Faturamento acumulado</span>
              <span class="font-weight-bold">{{ fmt(pe.faturamentoMes) }} / {{ fmt(pe.pontoEquilibrio) }}</span>
            </div>
            <v-progress-linear
              :model-value="Math.min(pe.percentualAtingido, 100)"
              :color="pe.peAtingido ? 'success' : pe.percentualAtingido >= 75 ? 'warning' : 'deep-purple'"
              height="18" rounded class="mb-4"
            >
              <template #default>
                <span class="text-caption font-weight-bold" style="color:white">{{ pe.percentualAtingido }}%</span>
              </template>
            </v-progress-linear>
            <v-row dense>
              <v-col cols="12" sm="7">
                <canvas ref="peCanvas" height="100" />
              </v-col>
              <v-col cols="12" sm="5">
                <div class="d-flex flex-column gap-2 text-body-2">
                  <div class="d-flex justify-space-between">
                    <span class="text-medium-emphasis">Custos fixos</span>
                    <span class="font-weight-bold text-error">{{ fmt(pe.totalCustosFixos) }}</span>
                  </div>
                  <div class="d-flex justify-space-between">
                    <span class="text-medium-emphasis">Margem contrib.</span>
                    <span class="font-weight-bold text-deep-purple">{{ pe.percentualMargemContribuicao }}%</span>
                  </div>
                  <div class="d-flex justify-space-between">
                    <span class="text-medium-emphasis">PE calculado</span>
                    <span class="font-weight-bold">{{ fmt(pe.pontoEquilibrio) }}</span>
                  </div>
                  <div v-if="pe.peAtingido" class="d-flex justify-space-between">
                    <span class="text-medium-emphasis">Lucro acima PE</span>
                    <span class="font-weight-bold text-success">+{{ fmt(pe.lucroAcimaPE) }}</span>
                  </div>
                  <div v-else class="d-flex justify-space-between">
                    <span class="text-medium-emphasis">Falta atingir</span>
                    <span class="font-weight-bold text-warning">{{ fmt(pe.pontoEquilibrio - pe.faturamentoMes) }}</span>
                  </div>
                </div>
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Contas a Pagar do dia -->
      <v-col cols="12" md="5">
        <v-card rounded="xl" elevation="1" height="100%">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-calendar-today" class="mr-2" color="error" />
            Contas a Pagar — Hoje
            <v-spacer />
            <v-chip v-if="contasPagarHoje.length" color="error" size="small" label>
              {{ fmt(totalPagarHoje) }}
            </v-chip>
          </v-card-title>

          <v-card-text v-if="carregandoCP" class="d-flex justify-center pa-6">
            <v-progress-circular indeterminate color="error" />
          </v-card-text>

          <div v-else-if="contasPagarHoje.length === 0" class="text-center text-medium-emphasis pa-6">
            <v-icon icon="mdi-check-circle-outline" color="success" size="36" class="mb-2" />
            <div class="text-body-2">Nenhuma conta vence hoje.</div>
          </div>

          <v-list v-else density="compact" class="pa-2" style="max-height: 260px; overflow-y: auto">
            <v-list-item v-for="c in contasPagarHoje" :key="c.id" rounded="lg" class="mb-1">
              <template #prepend>
                <v-icon
                  :icon="c.status === 'Pago' ? 'mdi-check-circle' : c.vencido ? 'mdi-alert-circle' : 'mdi-clock-outline'"
                  :color="c.status === 'Pago' ? 'success' : c.vencido ? 'error' : 'warning'"
                  size="20" class="mr-2"
                />
              </template>
              <v-list-item-title class="text-body-2">{{ c.descricao }}</v-list-item-title>
              <v-list-item-subtitle class="text-caption">
                {{ c.totalParcelas > 1 ? `${c.parcela}/${c.totalParcelas} · ` : '' }}{{ fmtData(c.dataVencimento) }}
              </v-list-item-subtitle>
              <template #append>
                <span class="text-body-2 font-weight-bold" :class="c.status === 'Pago' ? 'text-success' : 'text-error'">
                  {{ fmt(c.saldo) }}
                </span>
              </template>
            </v-list-item>
          </v-list>

          <v-card-actions class="pa-2 pt-0">
            <v-btn variant="text" size="small" color="error" to="/financeiro/contas-pagar" append-icon="mdi-arrow-right">
              Ver todas
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>

    <!-- Vendas por Colaborador + DRE do Mês -->
    <v-row class="mt-2">
      <!-- Vendas por Colaborador -->
      <v-col cols="12" md="5">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-account-group-outline" class="mr-2" color="teal" />
            Vendas por Colaborador — {{ mesAtual }}
            <v-spacer />
            <v-btn icon size="x-small" variant="text" @click="periodoColaborador = periodoColaborador === 'mes' ? 'hoje' : 'mes'">
              <v-icon>{{ periodoColaborador === 'mes' ? 'mdi-calendar-month' : 'mdi-calendar-today' }}</v-icon>
              <v-tooltip activator="parent">{{ periodoColaborador === 'mes' ? 'Ver hoje' : 'Ver mês' }}</v-tooltip>
            </v-btn>
          </v-card-title>

          <v-card-text v-if="carregandoColab" class="d-flex justify-center pa-8">
            <v-progress-circular indeterminate color="teal" />
          </v-card-text>

          <div v-else-if="vendasColaborador.length === 0" class="text-center text-medium-emphasis pa-6">
            <v-icon icon="mdi-account-off-outline" size="36" class="mb-2" />
            <div class="text-body-2">Nenhuma venda registrada no período.</div>
          </div>

          <div v-else class="pa-3 pt-0">
            <div
              v-for="(v, i) in vendasColaborador"
              :key="v.usuarioId"
              class="mb-3"
            >
              <div class="d-flex align-center mb-1">
                <v-avatar :color="coresColab[i % coresColab.length]" size="28" class="mr-2">
                  <span class="text-caption font-weight-bold text-white">
                    {{ iniciais(v.nome) }}
                  </span>
                </v-avatar>
                <span class="text-body-2 font-weight-medium flex-grow-1">{{ v.nome }}</span>
                <span class="text-body-2 font-weight-bold ml-2">{{ fmt(v.totalVendido) }}</span>
              </div>
              <div class="d-flex align-center gap-2">
                <v-progress-linear
                  :model-value="(v.totalVendido / vendasColaborador[0].totalVendido) * 100"
                  :color="coresColab[i % coresColab.length]"
                  height="6"
                  rounded
                  class="flex-grow-1"
                />
                <span class="text-caption text-medium-emphasis" style="min-width:44px;text-align:right">
                  {{ v.qtdVendas }} vda{{ v.qtdVendas !== 1 ? 's' : '' }}
                </span>
              </div>
            </div>

            <!-- Ticket médio total -->
            <v-divider class="my-2" />
            <div class="d-flex justify-space-between text-caption text-medium-emphasis">
              <span>Ticket médio geral</span>
              <span class="font-weight-bold text-teal">{{ fmt(ticketMedioGeral) }}</span>
            </div>
          </div>
        </v-card>
      </v-col>

      <!-- DRE do Mês -->
      <v-col cols="12" md="7">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-finance" class="mr-2" color="indigo" />
            DRE — {{ mesAtual }}
            <v-spacer />
            <v-chip
              v-if="dre"
              :color="dre.resultadoOperacional >= 0 ? 'success' : 'error'"
              size="small" label
            >
              {{ dre.resultadoOperacional >= 0 ? '+' : '' }}{{ fmt(dre.resultadoOperacional) }}
            </v-chip>
          </v-card-title>

          <v-card-text v-if="carregandoDre" class="d-flex justify-center pa-8">
            <v-progress-circular indeterminate color="indigo" />
          </v-card-text>

          <v-card-text v-else-if="!dre" class="text-center text-medium-emphasis pa-6">
            <v-icon icon="mdi-chart-line-variant" size="36" class="mb-2" />
            <div class="text-body-2">Sem dados para o período.</div>
          </v-card-text>

          <div v-else class="pa-3 pt-0">
            <!-- Linhas do DRE -->
            <div
              v-for="linha in linhasDre"
              :key="linha.label"
              class="d-flex justify-space-between align-center py-1"
              :class="{ 'border-t-thin mt-1 pt-2': linha.separador }"
            >
              <span
                class="text-body-2"
                :class="linha.destaque ? 'font-weight-bold' : 'text-medium-emphasis'"
                :style="linha.indent ? 'padding-left:16px' : ''"
              >
                {{ linha.label }}
              </span>
              <span
                class="text-body-2 font-weight-bold"
                :class="linha.cor ?? (linha.valor >= 0 ? '' : 'text-error')"
              >
                {{ linha.prefixo ?? '' }}{{ fmt(linha.valor) }}
              </span>
            </div>

            <!-- Gráfico receita vs despesa -->
            <v-divider class="my-3" />
            <canvas ref="dreCanvas" height="80" />

            <div class="d-flex justify-space-between mt-2">
              <div class="text-center">
                <div class="text-caption text-medium-emphasis">Margem bruta</div>
                <div class="text-body-2 font-weight-bold text-indigo">{{ dre.margemBruta }}%</div>
              </div>
              <div class="text-center">
                <div class="text-caption text-medium-emphasis">Margem operac.</div>
                <div class="text-body-2 font-weight-bold"
                  :class="dre.margemOperacional >= 0 ? 'text-success' : 'text-error'">
                  {{ dre.margemOperacional }}%
                </div>
              </div>
              <div class="text-center">
                <div class="text-caption text-medium-emphasis">CMV / Receita</div>
                <div class="text-body-2 font-weight-bold text-warning">
                  {{ dre.receitaLiquida > 0 ? Math.round(dre.cmv / dre.receitaLiquida * 100) : 0 }}%
                </div>
              </div>
            </div>
          </div>

          <v-card-actions class="pa-2 pt-0">
            <v-btn variant="text" size="small" color="indigo" to="/financeiro/dre" append-icon="mdi-arrow-right">
              Ver DRE completo
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>

    <!-- Alertas -->
    <v-row v-if="alertas.length" class="mt-2">
      <v-col cols="12">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-0 text-body-1 font-weight-bold">Alertas</v-card-title>
          <v-list density="compact" class="pa-2">
            <v-list-item
              v-for="(a, i) in alertas"
              :key="i"
              :prepend-icon="a.icon"
              :title="a.texto"
              :subtitle="a.detalhe"
              :base-color="a.cor"
            />
          </v-list>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, nextTick } from 'vue'
import { useAuthStore } from '@/stores/auth'
import api from '@/composables/useApi'

const auth = useAuthStore()
const graficoCanvas = ref<HTMLCanvasElement>()
const peCanvas = ref<HTMLCanvasElement>()
const dreCanvas = ref<HTMLCanvasElement>()
const carregando = ref(true)

const dataHoje = new Date().toLocaleDateString('pt-BR', {
  weekday: 'long', year: 'numeric', month: 'long', day: 'numeric'
})
const mesAtual = new Date().toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' })
const mesAtualNum = new Date().getMonth() + 1
const anoAtual = new Date().getFullYear()

// ── Planejamento Anual ───────────────────────────────────────────────────────
const MESES_PT = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']
interface PlanejamentoData {
  totalMeta: number; totalRealizado: number
  meses: { mes: number; nomeMes: string; meta: number; realizado: number }[]
}
const planejamento = ref<PlanejamentoData | null>(null)

async function carregarPlanejamento() {
  if (!auth.empresaId) return
  try {
    const res = await api.get<PlanejamentoData>('/relatorios/planejamento-anual', {
      params: { empresaId: auth.empresaId, ano: anoAtual }
    })
    planejamento.value = res.data
  } catch { planejamento.value = null }
}

const fmt = (v: number) => 'R$ ' + (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtData = (v: string) => v ? new Date(v).toLocaleDateString('pt-BR') : '-'
const iniciais = (nome: string) => nome.split(' ').slice(0, 2).map(p => p[0]).join('').toUpperCase()

const coresColab = ['teal', 'indigo', 'deep-purple', 'blue', 'cyan', 'green', 'orange']

const cards = ref([
  { titulo: 'Vendas hoje', valor: 'R$ --', icon: 'mdi-cash-multiple', cor: 'success', to: '/pdv/vendas' },
  { titulo: 'Pedidos abertos', valor: '--', icon: 'mdi-truck-delivery-outline', cor: 'info', to: '/compras' },
  { titulo: 'A receber (venc.)', valor: 'R$ --', icon: 'mdi-calendar-clock', cor: 'warning', to: '/financeiro/contas-receber' },
  { titulo: 'Produtos s/ estoque', valor: '--', icon: 'mdi-package-variant-remove', cor: 'error', to: '/estoque' },
])


const alertas = ref<{ icon: string; texto: string; detalhe: string; cor: string }[]>([])

// ── Ponto de Equilíbrio ───────────────────────────────────────────────────────
interface PeData {
  totalCustosFixos: number
  receitaTotal: number
  margemContribuicao: number
  percentualMargemContribuicao: number
  pontoEquilibrio: number
  faturamentoMes: number
  percentualAtingido: number
  peAtingido: boolean
  lucroAcimaPE: number
  detalhesCustosFixos: { categoria: string; total: number }[]
}

const pe = ref<PeData | null>(null)
const carregandoPe = ref(true)

async function carregarPe() {
  if (!auth.empresaId) { carregandoPe.value = false; return }
  try {
    const hoje = new Date()
    const res = await api.get<PeData>('/financeiro/ponto-equilibrio', {
      params: { empresaId: auth.empresaId, ano: hoje.getFullYear(), mes: hoje.getMonth() + 1 }
    })
    pe.value = res.data
    await nextTick()
    renderizarGraficoPe()
  } catch { /* estado vazio */ } finally {
    carregandoPe.value = false
  }
}

function renderizarGraficoPe() {
  const canvas = peCanvas.value
  if (!canvas || !pe.value) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  const d = pe.value
  const cats = d.detalhesCustosFixos.length ? d.detalhesCustosFixos : [{ categoria: 'Custos Fixos', total: d.totalCustosFixos }]
  const labels = ['Faturamento', 'PE', ...cats.map(c => c.categoria)]
  const valores = [d.faturamentoMes, d.pontoEquilibrio, ...cats.map(c => c.total)]
  const cores = [d.peAtingido ? '#4caf50' : '#ab47bc', '#ef5350', ...cats.map(() => '#78909c')]
  const W = canvas.offsetWidth || 260; const H = 100
  canvas.width = W; canvas.height = H
  const barW = Math.floor((W - 20) / labels.length) - 4
  const maxV = Math.max(...valores, 1); const maxBarH = H - 28
  ctx.clearRect(0, 0, W, H)
  labels.forEach((label, i) => {
    const x = 10 + i * (barW + 4)
    const barH = Math.round((valores[i] / maxV) * maxBarH)
    ctx.fillStyle = cores[i]
    ctx.beginPath(); ctx.roundRect(x, H - 20 - barH, barW, barH, [3, 3, 0, 0]); ctx.fill()
    ctx.fillStyle = '#9e9e9e'; ctx.font = '8px sans-serif'; ctx.textAlign = 'center'
    ctx.fillText(label.length > 8 ? label.slice(0, 7) + '…' : label, x + barW / 2, H - 6)
  })
}

// ── Contas a Pagar do Dia ────────────────────────────────────────────────────
interface ContaPagar {
  id: string; descricao: string; saldo: number; dataVencimento: string
  status: string; parcela: number; totalParcelas: number; vencido: boolean
}
const contasPagarHoje = ref<ContaPagar[]>([])
const carregandoCP = ref(true)
const totalPagarHoje = computed(() =>
  contasPagarHoje.value.filter(c => c.status !== 'Pago').reduce((s, c) => s + c.saldo, 0)
)

async function carregarContasPagarHoje() {
  if (!auth.empresaId) { carregandoCP.value = false; return }
  try {
    const hoje = new Date().toISOString().slice(0, 10)
    const res = await api.get<ContaPagar[]>('/contas-pagar', {
      params: { empresaId: auth.empresaId, inicio: hoje, fim: hoje }
    })
    contasPagarHoje.value = res.data
  } catch { contasPagarHoje.value = [] } finally { carregandoCP.value = false }
}

// ── Vendas por Colaborador ───────────────────────────────────────────────────
interface VendaColab {
  usuarioId: string; nome: string; qtdVendas: number
  totalVendido: number; ticketMedio: number
}
const vendasColaborador = ref<VendaColab[]>([])
const carregandoColab = ref(true)
const periodoColaborador = ref<'mes' | 'hoje'>('mes')

const ticketMedioGeral = computed(() => {
  const total = vendasColaborador.value.reduce((s, v) => s + v.totalVendido, 0)
  const qtd = vendasColaborador.value.reduce((s, v) => s + v.qtdVendas, 0)
  return qtd > 0 ? total / qtd : 0
})

async function carregarVendasColaborador() {
  if (!auth.empresaId) { carregandoColab.value = false; return }
  carregandoColab.value = true
  try {
    const hoje = new Date()
    const inicio = periodoColaborador.value === 'hoje'
      ? hoje.toISOString().slice(0, 10)
      : new Date(hoje.getFullYear(), hoje.getMonth(), 1).toISOString().slice(0, 10)
    const fim = hoje.toISOString().slice(0, 10)

    const [rankingRes, usuariosRes] = await Promise.all([
      api.get<{ usuarioId: string; qtdVendas: number; totalVendido: number; ticketMedio: number }[]>(
        '/relatorios/vendas/por-vendedor',
        { params: { empresaId: auth.empresaId, inicio, fim } }
      ),
      api.get<{ id: string; nome: string }[]>('/usuarios', { params: { empresaId: auth.empresaId } })
    ])

    const nomeMap = Object.fromEntries(usuariosRes.data.map(u => [u.id, u.nome]))
    vendasColaborador.value = rankingRes.data.map(r => ({
      ...r,
      nome: nomeMap[r.usuarioId] ?? 'Usuário #' + r.usuarioId.slice(0, 6)
    }))
  } catch { vendasColaborador.value = [] } finally { carregandoColab.value = false }
}

watch(periodoColaborador, carregarVendasColaborador)

// ── DRE do Mês ───────────────────────────────────────────────────────────────
interface DreData {
  receitaBruta: number; descontos: number; receitaLiquida: number
  cmv: number; lucroBruto: number; margemBruta: number
  despesasOperacionais: number; despesasPorCategoria: { categoria: string; total: number }[]
  resultadoOperacional: number; margemOperacional: number
}
const dre = ref<DreData | null>(null)
const carregandoDre = ref(true)

const linhasDre = computed(() => {
  if (!dre.value) return []
  const d = dre.value
  return [
    { label: 'Receita Bruta', valor: d.receitaBruta, cor: 'text-success', destaque: false },
    { label: '(-) Descontos', valor: -d.descontos, cor: 'text-error', destaque: false, indent: true },
    { label: 'Receita Líquida', valor: d.receitaLiquida, destaque: true, separador: true },
    { label: '(-) CMV', valor: -d.cmv, cor: 'text-warning', destaque: false, indent: true },
    { label: 'Lucro Bruto', valor: d.lucroBruto, destaque: true, separador: true },
    { label: '(-) Despesas Operac.', valor: -d.despesasOperacionais, cor: 'text-error', destaque: false, indent: true },
    { label: 'Resultado Operacional', valor: d.resultadoOperacional, cor: d.resultadoOperacional >= 0 ? 'text-success' : 'text-error', destaque: true, separador: true },
  ] as { label: string; valor: number; cor?: string; destaque?: boolean; separador?: boolean; indent?: boolean; prefixo?: string }[]
})

async function carregarDre() {
  if (!auth.empresaId) { carregandoDre.value = false; return }
  try {
    const hoje = new Date()
    const res = await api.get<DreData>('/financeiro/dre', {
      params: { empresaId: auth.empresaId, ano: hoje.getFullYear(), mes: hoje.getMonth() + 1 }
    })
    dre.value = res.data
    await nextTick()
    renderizarGraficoDre()
  } catch { /* vazio */ } finally { carregandoDre.value = false }
}

function renderizarGraficoDre() {
  const canvas = dreCanvas.value
  if (!canvas || !dre.value) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  const d = dre.value
  const W = canvas.offsetWidth || 400; const H = 80
  canvas.width = W; canvas.height = H

  // Barras horizontais empilhadas: CMV + Lucro bruto = Receita líquida
  const receita = d.receitaLiquida || 1
  const cmvPct = Math.min(d.cmv / receita, 1)
  const lbPct = Math.max(0, Math.min(d.lucroBruto / receita, 1))
  const despPct = Math.min(d.despesasOperacionais / receita, 1)
  const resPos = d.resultadoOperacional >= 0

  const barH = 22; const labelW = 96; const barW = W - labelW - 8

  const drawBar = (y: number, label: string, segments: { pct: number; cor: string }[]) => {
    ctx.fillStyle = '#9e9e9e'; ctx.font = '10px sans-serif'; ctx.textAlign = 'right'
    ctx.fillText(label, labelW - 4, y + barH / 2 + 4)
    let x = labelW
    for (const seg of segments) {
      const w = Math.round(seg.pct * barW)
      if (w <= 0) continue
      ctx.fillStyle = seg.cor
      ctx.beginPath(); ctx.roundRect(x, y, w, barH, 4); ctx.fill()
      x += w
    }
  }

  drawBar(2, 'Receita líq.', [
    { pct: cmvPct, cor: '#ffa726' },
    { pct: lbPct, cor: '#66bb6a' },
  ])
  drawBar(30, 'Lucro bruto', [
    { pct: despPct, cor: '#ef5350' },
    { pct: Math.max(0, (d.lucroBruto - d.despesasOperacionais) / receita), cor: resPos ? '#26a69a' : '#ef9a9a' },
  ])

  // Legenda
  const legenda = [
    { cor: '#ffa726', label: 'CMV' }, { cor: '#66bb6a', label: 'Lucro bruto' },
    { cor: '#ef5350', label: 'Despesas' }, { cor: '#26a69a', label: 'Resultado' }
  ]
  ctx.font = '9px sans-serif'; ctx.textAlign = 'left'
  legenda.forEach((l, i) => {
    const lx = labelW + i * Math.floor(barW / 4)
    ctx.fillStyle = l.cor
    ctx.fillRect(lx, 58, 8, 8)
    ctx.fillStyle = '#9e9e9e'
    ctx.fillText(l.label, lx + 10, 67)
  })
}

onMounted(async () => {
  carregando.value = false
  await Promise.all([carregarPe(), carregarContasPagarHoje(), carregarVendasColaborador(), carregarDre(), carregarPlanejamento()])
})
</script>
