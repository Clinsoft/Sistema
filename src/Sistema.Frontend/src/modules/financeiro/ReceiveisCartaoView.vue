<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div>
        <div class="text-h6 font-weight-bold">Recebíveis de Cartão</div>
        <div class="text-caption text-medium-emphasis">Conciliação financeira — taxas, repasses e antecipações</div>
      </div>
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-credit-card-settings-outline"
        to="/financeiro/operadoras-cartao" size="small">
        Operadoras
      </v-btn>
    </div>

    <GuiaPassos
      id="recebiveis-cartao"
      titulo="Como usar os Recebíveis de Cartão"
      :passos="[
        'Os recebíveis são gerados automaticamente pelas <b>vendas no cartão</b> do PDV — cada venda vira um recebível com taxa da operadora e data prevista de crédito.',
        'Filtre por período, <b>operadora</b>, forma de pagamento ou <b>status</b> (A Receber, Recebido, Antecipado) e clique em <b>Buscar</b>.',
        'Selecione linhas (caixas à esquerda) para <b>Marcar como Recebido</b> (quando a operadora repassar), <b>Antecipar</b> (com desconto da taxa) ou <b>Cancelar</b>.',
        'Clique no 👁 para ver o <b>detalhamento</b> de taxas e o valor líquido. Configure taxas e prazos em <b>Operadoras</b>.',
      ]"
    />

    <!-- Cards de resumo -->
    <v-row class="mb-4">
      <v-col v-for="c in cards" :key="c.label" cols="6" md="3" sm="6">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-4">
            <div class="d-flex align-center justify-space-between mb-2">
              <span class="text-caption text-medium-emphasis">{{ c.label }}</span>
              <v-avatar :color="c.cor" size="32" rounded="lg">
                <v-icon :icon="c.icon" color="white" size="16" />
              </v-avatar>
            </div>
            <div class="text-h6 font-weight-bold" :class="c.textCor">
              R$ {{ fmt(c.valor) }}
            </div>
            <div v-if="c.sub" class="text-caption text-medium-emphasis mt-1">{{ c.sub }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Cards por operadora (conciliação por maquininha) -->
    <div v-if="porOperadora.length" class="mb-2 text-caption text-medium-emphasis font-weight-medium">
      POR OPERADORA
    </div>
    <v-row v-if="porOperadora.length" class="mb-4">
      <v-col v-for="op in porOperadora" :key="op.operadora" cols="12" md="4" sm="6">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-4">
            <div class="d-flex align-center mb-2" style="gap:8px">
              <v-avatar :color="op.cor" size="28" rounded="lg">
                <v-icon :icon="op.ehPix ? 'mdi-qrcode' : 'mdi-credit-card-outline'" color="white" size="16" />
              </v-avatar>
              <span class="text-subtitle-2 font-weight-bold text-truncate">{{ op.operadora }}</span>
              <v-spacer />
              <v-chip size="x-small" variant="tonal">{{ op.qtd }}</v-chip>
            </div>
            <div class="det-linha py-0">
              <span class="text-caption text-medium-emphasis">Bruto</span>
              <strong>R$ {{ fmt(op.bruto) }}</strong>
            </div>
            <div class="det-linha py-0 text-error">
              <span class="text-caption">Taxa</span>
              <strong>-R$ {{ fmt(op.taxa) }}</strong>
            </div>
            <v-divider class="my-1" />
            <div class="det-linha py-0">
              <span class="text-caption font-weight-medium">Líquido</span>
              <strong class="text-success">R$ {{ fmt(op.liquido) }}</strong>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" sm="2">
          <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f; carregar() }" />
        </v-col>
        <v-col cols="12" sm="2">
          <v-text-field v-model="filtros.inicio" label="De" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="2">
          <v-text-field v-model="filtros.fim" label="Até" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="2">
          <v-select v-model="filtros.operadoraId" :items="operadoras"
            item-title="nome" item-value="id"
            label="Operadora" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" sm="2">
          <v-select v-model="filtros.formaPagamento" :items="formasPagamento"
            label="Forma de pagamento" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" sm="2">
          <v-select v-model="filtros.status" :items="statusOpcoes"
            label="Status" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" @click="carregar" :loading="carregando">
            Buscar
          </v-btn>
        </v-col>
      </v-row>
    </v-card>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table
        :headers="headers"
        :items="recebiveis"
        :loading="carregando"
        density="compact"
        hover
        items-per-page="25"
        show-select
        v-model="selecionados"
      >
        <template #top v-if="selecionados.length">
          <div class="d-flex align-center gap-3 pa-3 bg-grey-lighten-4">
            <span class="text-body-2 font-weight-bold">
              {{ selecionados.length }} itens selecionados —
              Bruto: R$ {{ fmt(totalSelecionado.bruto) }} /
              Líquido: R$ {{ fmt(totalSelecionado.liquido) }}
            </span>
            <v-spacer />
            <v-btn color="warning" size="small" variant="tonal"
              prepend-icon="mdi-calendar-fast-forward-outline"
              @click="dialogAntecipar = true"
              :disabled="selecionadosAReceberIds.length === 0">
              Antecipar selecionados
            </v-btn>
            <v-btn color="success" size="small" variant="tonal"
              prepend-icon="mdi-check-circle-outline"
              @click="confirmarRecebimento">
              Marcar como Recebido
            </v-btn>
            <v-btn color="error" size="small" variant="tonal"
              prepend-icon="mdi-cancel"
              @click="cancelarSelecionados">
              Cancelar
            </v-btn>
          </div>
        </template>

        <template #item.venda="{ item }">
          <span class="font-weight-bold text-primary">Venda #{{ item.numeroVenda }}</span>
        </template>

        <template #item.dataVenda="{ item }">{{ fmtData(item.dataVenda) }}</template>

        <template #item.operadora="{ item }">
          <div class="d-flex align-center gap-1">
            <v-icon icon="mdi-credit-card-outline" size="14" class="text-medium-emphasis" />
            <span class="text-body-2">{{ item.operadora }}</span>
            <v-chip v-if="item.bandeira" size="x-small" variant="tonal" color="primary">
              {{ item.bandeira }}
            </v-chip>
          </div>
        </template>

        <template #item.formaPagamento="{ item }">
          <v-chip :color="corFormaPagamento(item.formaPagamento)" size="small" variant="tonal">
            <v-icon start size="12" :icon="iconFormaPagamento(item.formaPagamento)" />
            {{ item.formaPagamento }}
            <span v-if="item.parcelas > 1" class="ml-1">({{ item.parcelas }}x)</span>
          </v-chip>
        </template>

        <template #item.valorBruto="{ item }">
          <span class="font-weight-medium">R$ {{ fmt(item.valorBruto) }}</span>
        </template>

        <template #item.taxa="{ item }">
          <div>
            <span class="text-error font-weight-medium">{{ item.taxaTotal?.toFixed(2) }}%</span>
            <div class="text-caption text-medium-emphasis">R$ {{ fmt(item.valorTaxa) }}</div>
          </div>
        </template>

        <template #item.valorLiquido="{ item }">
          <span class="text-success font-weight-bold">R$ {{ fmt(item.valorLiquido) }}</span>
        </template>

        <template #item.dataPrevistaCredito="{ item }">
          <div>
            <span>{{ fmtData(item.dataPrevistaCredito) }}</span>
            <div v-if="item.dataEfetiva" class="text-caption text-success">
              Recebido: {{ fmtData(item.dataEfetiva) }}
            </div>
          </div>
        </template>

        <template #item.status="{ item }">
          <v-chip :color="corStatus(item.status)" size="small" variant="tonal">
            <v-icon start size="12" :icon="iconStatus(item.status)" />
            {{ item.status }}
          </v-chip>
        </template>

        <template #item.antecipacao="{ item }">
          <div v-if="item.status === 'Antecipado'" class="text-warning text-caption">
            <v-icon icon="mdi-calendar-fast-forward-outline" size="13" />
            Taxa: {{ item.taxaAntecipacao?.toFixed(2) }}% a.m.<br />
            Desc: R$ {{ fmt(item.valorDescontoAntecipacao) }}
          </div>
          <span v-else class="text-medium-emphasis text-caption">—</span>
        </template>

        <template #item.acoes="{ item }">
          <v-btn icon size="x-small" variant="text" @click="verDetalhe(item)" title="Detalhes">
            <v-icon icon="mdi-eye-outline" size="16" />
          </v-btn>
        </template>

        <template #bottom="{ itemsLength }">
          <div class="d-flex align-center pa-3 gap-4 bg-grey-lighten-5 border-t">
            <span class="text-caption text-medium-emphasis">{{ itemsLength }} registros</span>
            <span class="text-caption">
              Bruto: <strong>R$ {{ fmt(totais.bruto) }}</strong>
            </span>
            <span class="text-caption text-error">
              Taxas: <strong>-R$ {{ fmt(totais.taxas) }}</strong>
            </span>
            <span class="text-caption text-success">
              Líquido: <strong>R$ {{ fmt(totais.liquido) }}</strong>
            </span>
            <span class="text-caption text-warning" v-if="totais.descontoAntecipacao > 0">
              Desc. antecipação: <strong>-R$ {{ fmt(totais.descontoAntecipacao) }}</strong>
            </span>
          </div>
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog Detalhe -->
    <v-dialog v-model="dialogDetalhe" max-width="520">
      <v-card v-if="itemDetalhe" rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center justify-space-between">
          <span>Venda #{{ itemDetalhe.numeroVenda }}</span>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialogDetalhe = false" />
        </v-card-title>
        <v-card-text class="pa-4">
          <v-row dense>
            <v-col cols="6"><span class="text-caption text-medium-emphasis">Operadora</span><div>{{ itemDetalhe.operadora }}</div></v-col>
            <v-col cols="6"><span class="text-caption text-medium-emphasis">Bandeira</span><div>{{ itemDetalhe.bandeira || '—' }}</div></v-col>
            <v-col cols="6"><span class="text-caption text-medium-emphasis">Forma</span><div>{{ itemDetalhe.formaPagamento }}{{ itemDetalhe.parcelas > 1 ? ` (${itemDetalhe.parcelas}x)` : '' }}</div></v-col>
            <v-col cols="6"><span class="text-caption text-medium-emphasis">Data da venda</span><div>{{ fmtData(itemDetalhe.dataVenda) }}</div></v-col>
          </v-row>
          <v-divider class="my-3" />
          <div class="det-linha">
            <span>Valor bruto</span><strong>R$ {{ fmt(itemDetalhe.valorBruto) }}</strong>
          </div>
          <div class="det-linha text-error">
            <span>Taxa operadora ({{ itemDetalhe.taxaOperadora?.toFixed(2) }}%)</span>
            <strong>-R$ {{ fmt(itemDetalhe.valorTaxaOperadora) }}</strong>
          </div>
          <div v-if="itemDetalhe.status === 'Antecipado'" class="det-linha text-warning">
            <span>Taxa antecipação ({{ itemDetalhe.taxaAntecipacao?.toFixed(2) }}% a.m.)</span>
            <strong>-R$ {{ fmt(itemDetalhe.valorDescontoAntecipacao) }}</strong>
          </div>
          <v-divider class="my-2" />
          <div class="det-linha">
            <span class="font-weight-bold">Valor líquido a receber</span>
            <span class="text-h6 font-weight-bold text-success">R$ {{ fmt(itemDetalhe.valorLiquido) }}</span>
          </div>
          <v-divider class="my-3" />
          <div class="det-linha">
            <span>Data prevista de crédito</span>
            <v-chip :color="corStatus(itemDetalhe.status)" size="small" variant="tonal">
              {{ fmtData(itemDetalhe.dataPrevistaCredito) }}
            </v-chip>
          </div>
          <div v-if="itemDetalhe.dataEfetiva" class="det-linha text-success">
            <span>Recebido em</span><strong>{{ fmtData(itemDetalhe.dataEfetiva) }}</strong>
          </div>
        </v-card-text>
      </v-card>
    </v-dialog>

    <!-- Dialog Antecipar -->
    <v-dialog v-model="dialogAntecipar" max-width="480" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2">
          <v-icon icon="mdi-calendar-fast-forward-outline" color="warning" />
          Antecipar Recebíveis
        </v-card-title>
        <v-card-text>
          <v-alert type="warning" variant="tonal" density="compact" class="mb-4 text-body-2">
            A antecipação quita os recebíveis selecionados com desconto da taxa da operadora.
          </v-alert>
          <v-row dense>
            <v-col cols="12" md="6">
              <v-text-field v-model.number="antecipacao.taxaAm" label="Taxa de antecipação (% a.m.)"
                type="number" step="0.01" variant="outlined" density="compact" suffix="% a.m."
                @update:model-value="calcularAntecipacao" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="antecipacao.dataCredito" label="Data de crédito prevista"
                type="date" variant="outlined" density="compact" />
            </v-col>
          </v-row>
          <div class="ant-resumo mt-3">
            <div class="det-linha">
              <span>Itens selecionados</span><strong>{{ selecionadosAReceberIds.length }}</strong>
            </div>
            <div class="det-linha">
              <span>Valor bruto</span><strong>R$ {{ fmt(totalSelecionado.bruto) }}</strong>
            </div>
            <div class="det-linha text-warning">
              <span>Desconto antecipação</span><strong>-R$ {{ fmt(antecipacao.desconto) }}</strong>
            </div>
            <v-divider class="my-2" />
            <div class="det-linha">
              <span class="font-weight-bold">Valor líquido antecipado</span>
              <span class="font-weight-bold text-success text-h6">R$ {{ fmt(antecipacao.liquido) }}</span>
            </div>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogAntecipar = false">Cancelar</v-btn>
          <v-btn color="warning" :loading="salvando" @click="confirmarAntecipacao">
            Confirmar Antecipação
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import FiltroMes from '@/components/FiltroMes.vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const carregando = ref(false)
const salvando = ref(false)
const recebiveis = ref<any[]>([])
const operadoras = ref<any[]>([])
const selecionados = ref<any[]>([])
const dialogDetalhe = ref(false)
const dialogAntecipar = ref(false)
const itemDetalhe = ref<any>(null)

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date(new Date().getFullYear(), new Date().getMonth() + 1, 0).toISOString().slice(0, 10),
  operadoraId: null as string | null,
  formaPagamento: null as string | null,
  status: null as string | null,
})

const antecipacao = ref({ taxaAm: 0, dataCredito: new Date().toISOString().slice(0, 10), desconto: 0, liquido: 0 })

const formasPagamento = ['Débito', 'Crédito à Vista', 'Crédito Parcelado', 'Pix']
const statusOpcoes = ['A Receber', 'Recebido', 'Antecipado', 'Cancelado']

const headers = [
  { title: 'Venda',        key: 'venda',               sortable: false, width: 110 },
  { title: 'Operadora',    key: 'operadora',            width: 160 },
  { title: 'Pagamento',    key: 'formaPagamento',       width: 160 },
  { title: 'Data Venda',   key: 'dataVenda',            width: 110 },
  { title: 'Valor Bruto',  key: 'valorBruto',           width: 120 },
  { title: 'Taxa',         key: 'taxa',                 width: 110 },
  { title: 'Valor Líquido',key: 'valorLiquido',         width: 120 },
  { title: 'Crédito em',   key: 'dataPrevistaCredito',  width: 130 },
  { title: 'Status',       key: 'status',               width: 120 },
  { title: 'Antecipação',  key: 'antecipacao',          width: 140, sortable: false },
  { title: '',             key: 'acoes',                sortable: false, width: 50 },
]

const totais = computed(() => ({
  bruto: recebiveis.value.reduce((s, r) => s + (r.valorBruto ?? 0), 0),
  taxas: recebiveis.value.reduce((s, r) => s + (r.valorTaxa ?? 0), 0),
  liquido: recebiveis.value.reduce((s, r) => s + (r.valorLiquido ?? 0), 0),
  descontoAntecipacao: recebiveis.value.reduce((s, r) => s + (r.valorDescontoAntecipacao ?? 0), 0),
}))

const aReceber = computed(() => recebiveis.value.filter(r => r.status === 'A Receber'))
const recebido = computed(() => recebiveis.value.filter(r => r.status === 'Recebido'))

const cards = computed(() => [
  { label: 'Total Bruto',      valor: totais.value.bruto,   cor: 'primary', icon: 'mdi-cash-multiple',       textCor: '' },
  { label: 'Total de Taxas',   valor: totais.value.taxas,   cor: 'error',   icon: 'mdi-percent',              textCor: 'text-error', sub: 'Descontado pelas operadoras' },
  { label: 'Total Líquido',    valor: totais.value.liquido, cor: 'success', icon: 'mdi-cash-check',           textCor: 'text-success' },
  { label: 'A Receber',        valor: aReceber.value.reduce((s,r) => s+(r.valorLiquido??0),0), cor: 'warning', icon: 'mdi-clock-outline', textCor: 'text-warning', sub: `${aReceber.value.length} transações` },
])

const CORES_OP = ['#3f51b5', '#00897b', '#43a047', '#fb8c00', '#8e24aa', '#e53935']
const porOperadora = computed(() => {
  const map = new Map<string, any>()
  for (const r of recebiveis.value) {
    const key = r.operadora || '—'
    const cur = map.get(key) ?? { operadora: key, qtd: 0, bruto: 0, taxa: 0, liquido: 0 }
    cur.qtd++
    cur.bruto += r.valorBruto ?? 0
    cur.taxa += r.valorTaxa ?? 0
    cur.liquido += r.valorLiquido ?? 0
    map.set(key, cur)
  }
  const arr = Array.from(map.values()).sort((a, b) => b.bruto - a.bruto)
  arr.forEach((op, i) => {
    const meta = operadoras.value.find((o: any) => o.nome === op.operadora)
    op.cor = meta?.cor || CORES_OP[i % CORES_OP.length]
    op.ehPix = /pix/i.test(op.operadora)
  })
  return arr
})

const selecionadosAReceberIds = computed(() =>
  selecionados.value.filter(s => s.status === 'A Receber').map(s => s.id)
)

const totalSelecionado = computed(() => ({
  bruto: selecionados.value.reduce((s, r) => s + (r.valorBruto ?? 0), 0),
  liquido: selecionados.value.reduce((s, r) => s + (r.valorLiquido ?? 0), 0),
}))

function calcularAntecipacao() {
  const bruto = totalSelecionado.value.bruto
  const taxa = antecipacao.value.taxaAm / 100
  antecipacao.value.desconto = bruto * taxa
  antecipacao.value.liquido = bruto - antecipacao.value.desconto
}

const fmt     = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtData = (v?: string) => v ? new Date(v + 'T12:00:00').toLocaleDateString('pt-BR') : '—'

function corStatus(s: string) {
  return s === 'Recebido' ? 'success' : s === 'Antecipado' ? 'warning' : 'primary'
}
function iconStatus(s: string) {
  return s === 'Recebido' ? 'mdi-check-circle' : s === 'Antecipado' ? 'mdi-calendar-fast-forward-outline' : 'mdi-clock-outline'
}
function corFormaPagamento(fp: string) {
  if (fp?.includes('Débito')) return 'teal'
  if (fp?.includes('Crédito')) return 'indigo'
  if (fp === 'Pix') return 'green'
  return 'primary'
}
function iconFormaPagamento(fp: string) {
  if (fp?.includes('Débito')) return 'mdi-credit-card-minus-outline'
  if (fp?.includes('Crédito')) return 'mdi-credit-card-outline'
  if (fp === 'Pix') return 'mdi-qrcode'
  return 'mdi-cash'
}

function verDetalhe(item: any) {
  itemDetalhe.value = item
  dialogDetalhe.value = true
}

async function carregar() {
  carregando.value = true
  selecionados.value = []
  try {
    const r = await api.get('/financeiro/recebiveis-cartao', {
      params: { empresaId: auth.empresaId, ...filtros.value },
    })
    recebiveis.value = r.data ?? []
  } catch { recebiveis.value = [] }
  finally { carregando.value = false }
}

async function confirmarRecebimento() {
  salvando.value = true
  try {
    await api.post('/financeiro/recebiveis-cartao/receber', {
      ids: selecionados.value.filter(s => s.status === 'A Receber').map(s => s.id),
      dataEfetiva: new Date().toISOString().slice(0, 10),
    })
    notif.ok('Recebimento confirmado!')
    selecionados.value = []
    await carregar()
  } catch { notif.erro('Erro ao confirmar recebimento.') }
  finally { salvando.value = false }
}

async function confirmarAntecipacao() {
  salvando.value = true
  try {
    await api.post('/financeiro/recebiveis-cartao/antecipar', {
      ids: selecionadosAReceberIds.value,
      taxaAntecipacao: antecipacao.value.taxaAm,
    })
    notif.ok('Antecipação registrada!')
    dialogAntecipar.value = false
    selecionados.value = []
    await carregar()
  } catch { notif.erro('Erro ao registrar antecipação.') }
  finally { salvando.value = false }
}

async function cancelarSelecionados() {
  const ids = selecionados.value.filter(s => s.status !== 'Cancelado').map(s => s.id)
  if (!ids.length) return
  if (!confirm(`Cancelar ${ids.length} recebível(is) de cartão?`)) return
  salvando.value = true
  try {
    await api.post('/financeiro/recebiveis-cartao/cancelar', { ids })
    notif.ok('Recebível(is) cancelado(s).')
    selecionados.value = []
    await carregar()
  } catch { notif.erro('Erro ao cancelar.') }
  finally { salvando.value = false }
}

async function carregarOperadoras() {
  try {
    const r = await api.get('/financeiro/operadoras-cartao', { params: { empresaId: auth.empresaId } })
    operadoras.value = r.data ?? []
  } catch { operadoras.value = [] }
}

onMounted(() => Promise.all([carregar(), carregarOperadoras()]))
</script>

<style scoped>
.det-linha { display: flex; justify-content: space-between; align-items: center; padding: 4px 0; font-size: 0.875rem; }
.ant-resumo { background: #f8f9fb; border: 1px solid #e8edf3; border-radius: 10px; padding: 14px; }
</style>
