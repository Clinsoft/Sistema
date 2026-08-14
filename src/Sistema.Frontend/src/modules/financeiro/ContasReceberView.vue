<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Contas a Receber</div>
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">Nova</v-btn>
    </div>

    <GuiaPassos
      id="contas-receber"
      titulo="Como usar Contas a Receber"
      :passos="[
        'Use o filtro de <b>Mês</b> ou as datas para listar os títulos do período e clique em <b>Buscar</b>.',
        'Clique em <b>Nova</b> para lançar um recebimento. Escolha <b>Único</b>, <b>Parcelar</b> (divide o total) ou <b>Repetir</b> (mesmo valor por período).',
        'Informe a <b>subcategoria</b> (Vendas, Serviços…), o <b>cliente/pagador</b> e o vencimento.',
        'Na tabela: <b>💲 Baixar</b> registra o recebimento (credita a conta), <b>↻ Renegociar</b> reprograma valor e vencimento.',
        'Títulos de vendas a prazo (crediário) e do faturamento entram aqui automaticamente.',
      ]"
    />

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense>
        <v-col cols="12" sm="3">
          <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f }" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.inicio" label="Data início" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.fim" label="Data fim" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-select v-model="filtros.categoria" label="Categoria"
            :items="['Todas', ...subcategorias]" variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-select v-model="filtros.status" label="Status"
            :items="['Todos', 'EmAberto', 'Pago', 'Vencido']"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-autocomplete v-model="filtros.cliente" label="Cliente"
            :items="clientesLista" variant="outlined" density="compact" hide-details clearable
            no-data-text="Sem contas no período" />
        </v-col>
      </v-row>
      <div class="d-flex align-center justify-end mt-2 gap-3 flex-wrap">
        <v-btn color="warning" variant="tonal" rounded="lg" prepend-icon="mdi-calendar-today"
          :loading="carregando" @click="filtrarHoje">Hoje</v-btn>
        <v-switch v-model="filtros.tudo" color="primary" density="compact" hide-details
          label="Ver todas (ignora as datas)" @update:model-value="carregar" />
        <v-btn color="primary" variant="tonal" rounded="lg" prepend-icon="mdi-magnify"
          :loading="carregando" @click="carregar">Buscar</v-btn>
      </div>
    </v-card>

    <!-- Totalizadores -->
    <v-row class="mb-3">
      <v-col v-for="t in totais" :key="t.label" cols="6" sm="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="text-caption text-medium-emphasis">{{ t.label }}</div>
            <div class="text-h6 font-weight-bold" :class="t.classe">R$ {{ fmt(t.valor) }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="lancamentosFiltrados" :loading="carregando" density="compact" hover>
        <template #item.categoria="{ item }">
          <v-chip :color="corSubcategoria(item.categoria)" size="x-small" variant="tonal" label>
            {{ item.categoria ?? 'Recebimentos' }}
          </v-chip>
        </template>
        <template #item.status="{ item }">
          <v-chip :color="corStatus(item.status)" size="small" variant="tonal">
            {{ rotuloStatus(item.status) }}
          </v-chip>
        </template>
        <template #item.valorOriginal="{ item }">R$ {{ fmt(item.valorOriginal) }}</template>
        <template #item.saldo="{ item }">R$ {{ fmt(item.saldo) }}</template>
        <template #item.dataVencimento="{ item }">{{ fmtData(item.dataVencimento) }}</template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-cash-check" size="x-small" color="success" variant="text"
            title="Baixar" @click="abrirBaixa(item)" />
          <v-btn icon="mdi-content-copy" size="x-small" color="indigo" variant="text"
            title="Duplicar" @click="duplicarConta(item)" />
          <v-btn icon="mdi-refresh" size="x-small" color="warning" variant="text"
            title="Renegociar" @click="abrirRenegociacao(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog: Novo Recebimento -->
    <v-dialog v-model="dialogNovo" max-width="520" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon start color="success">mdi-arrow-down-circle-outline</v-icon>
          Novo Recebimento
        </v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12">
              <v-text-field v-model="form.descricao" label="Descrição *"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-select v-model="form.categoria" label="Subcategoria"
                :items="subcategorias" variant="outlined" density="compact"
                clearable clear-icon="mdi-close-circle" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-autocomplete v-model="form.clienteId" v-model:search="form._buscaCliente"
                label="Cliente / Pagador" :items="clientes" item-title="nome" item-value="id"
                variant="outlined" density="compact" clearable hide-details
                no-data-text="Nenhum cliente — use + para cadastrar">
                <template #append-inner>
                  <v-btn icon="mdi-plus" size="x-small" variant="text" density="compact" tabindex="-1"
                    title="Cadastrar cliente" @click.stop="abrirNovoCliente(form._buscaCliente?.trim())" />
                </template>
                <template #no-data>
                  <v-list-item @click="abrirNovoCliente(form._buscaCliente?.trim())">
                    <v-list-item-title>
                      <v-icon start size="16">mdi-plus</v-icon>Cadastrar
                      <b v-if="form._buscaCliente?.trim()">"{{ form._buscaCliente.trim() }}"</b>
                      <span v-else>novo cliente</span>
                    </v-list-item-title>
                  </v-list-item>
                </template>
              </v-autocomplete>
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model.number="form.valorOriginal" label="Valor (R$) *"
                type="number" step="0.01" prefix="R$" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="form.dataVencimento" label="Primeiro vencimento *"
                type="date" variant="outlined" density="compact" />
            </v-col>

            <!-- Modo: Único / Parcelar / Repetir -->
            <v-col cols="12" class="mt-1">
              <v-btn-toggle v-model="form.modo" mandatory density="compact" rounded="lg" color="success" class="w-100">
                <v-btn value="unico" class="flex-grow-1"><v-icon start size="16">mdi-numeric-1-circle-outline</v-icon>Único</v-btn>
                <v-btn value="parcelar" class="flex-grow-1"><v-icon start size="16">mdi-call-split</v-icon>Parcelar</v-btn>
                <v-btn value="repetir" class="flex-grow-1"><v-icon start size="16">mdi-repeat</v-icon>Repetir</v-btn>
              </v-btn-toggle>
            </v-col>
            <template v-if="form.modo !== 'unico'">
              <v-col cols="6" sm="4">
                <v-text-field v-model.number="form.quantas" type="number" min="2"
                  :label="form.modo === 'parcelar' ? 'Nº de parcelas' : 'Nº de vezes'"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="6" sm="4">
                <v-select v-model="form.periodo" :items="periodos" item-title="label" item-value="value"
                  label="Período" variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="4" class="d-flex align-center">
                <div class="text-caption text-medium-emphasis">
                  <template v-if="form.modo === 'parcelar'">{{ form.quantas }}× de <b>R$ {{ fmtParcela }}</b></template>
                  <template v-else>{{ form.quantas }}× de <b>R$ {{ fmt(form.valorOriginal) }}</b> = R$ {{ fmtTotalRepetir }}</template>
                </div>
              </v-col>
            </template>

            <v-col cols="12">
              <v-text-field v-model="form.observacao" label="Observação"
                variant="outlined" density="compact" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0 flex-wrap">
          <v-spacer />
          <v-btn variant="text" @click="dialogNovo = false" :disabled="salvando">Cancelar</v-btn>
          <v-btn variant="tonal" rounded="lg" :loading="salvando" @click="salvarNovo(true)">Salvar e nova</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando" @click="salvarNovo(false)">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: cadastrar cliente sem sair do recebimento -->
    <v-dialog v-model="dialogCliente" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2">
          <v-icon color="primary">mdi-account-plus-outline</v-icon>
          Novo cliente / pagador
        </v-card-title>
        <v-card-text>
          <v-text-field v-model="formCliente.nome" label="Nome / Razão Social *"
            variant="outlined" density="compact" autofocus class="mb-2" />
          <v-text-field v-model="formCliente.cpfCnpj" label="CPF / CNPJ"
            variant="outlined" density="compact" class="mb-2" />
          <v-text-field v-model="formCliente.telefone" label="Telefone"
            variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogCliente = false" :disabled="salvandoCliente">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvandoCliente"
            :disabled="!formCliente.nome.trim()" @click="salvarClienteRapido">Cadastrar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Baixar Título -->
    <v-dialog v-model="dialogBaixa" max-width="400">
      <v-card rounded="xl" class="pa-4">
        <v-card-title>Baixar Título</v-card-title>
        <v-card-text>
          <v-text-field v-model.number="baixa.valor" label="Valor recebido (R$)"
            type="number" variant="outlined" density="compact" class="mb-2" />
          <v-text-field v-model="baixa.data" label="Data do recebimento"
            type="date" variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions class="justify-end">
          <v-btn variant="text" @click="dialogBaixa = false">Cancelar</v-btn>
          <v-btn color="success" :loading="salvando" @click="confirmarBaixa">Confirmar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Renegociar -->
    <v-dialog v-model="dialogReneg" max-width="460" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon start color="warning">mdi-refresh</v-icon>
          Renegociar Título
        </v-card-title>
        <v-card-text v-if="itemReneg">
          <div class="text-body-2 mb-3">
            <strong>{{ itemReneg.descricao }}</strong> —
            Saldo: <span class="text-error font-weight-bold">R$ {{ fmt(itemReneg.saldo) }}</span>
          </div>
          <v-row dense>
            <v-col cols="12" sm="6">
              <v-text-field v-model.number="reneg.novoValor" label="Novo valor (R$)"
                type="number" prefix="R$" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="reneg.novoVencimento" label="Novo vencimento"
                type="date" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="reneg.motivo" label="Motivo da renegociação"
                variant="outlined" density="compact" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogReneg = false" :disabled="salvando">Cancelar</v-btn>
          <v-btn color="warning" rounded="lg" :loading="salvando" @click="confirmarRenegociacao">
            Renegociar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { rotuloStatus } from '@/utils/status'
import FiltroMes from '@/components/FiltroMes.vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const carregando = ref(false)
const salvando = ref(false)
const lancamentos = ref<any[]>([])
const dialogBaixa = ref(false)
const dialogNovo = ref(false)
const dialogReneg = ref(false)
const itemReneg = ref<any>(null)
const baixa = ref({ id: '', valor: 0, data: new Date().toISOString().slice(0, 10) })
const reneg = ref({ novoValor: 0, novoVencimento: '', motivo: '' })

// Subcategorias de Recebimentos
const subcategorias = ['Vendas', 'Serviços', 'Aluguel Recebido', 'Outras Receitas']

const periodos = [
  { label: 'Diário',     value: 'diario' },
  { label: 'Semanal',    value: 'semanal' },
  { label: 'Quinzenal',  value: 'quinzenal' },
  { label: 'Mensal',     value: 'mensal' },
  { label: 'Bimestral',  value: 'bimestral' },
  { label: 'Trimestral', value: 'trimestral' },
  { label: 'Semestral',  value: 'semestral' },
  { label: 'Anual',      value: 'anual' },
]

const formPadrao = () => ({
  descricao: '', categoria: '', clienteId: null as string | null, _buscaCliente: '',
  valorOriginal: 0, dataVencimento: '', observacao: '',
  modo: 'unico' as 'unico' | 'parcelar' | 'repetir',
  quantas: 2, periodo: 'mensal',
})
const form = ref(formPadrao())

// Clientes/pagadores (autocomplete + cadastro rápido, igual ao Contas a Pagar)
const clientes = ref<any[]>([])
async function carregarClientes() {
  try {
    const r = await api.get('/clientes', { params: { empresaId: auth.empresaId } })
    clientes.value = (r.data?.itens ?? r.data ?? []).map((c: any) => ({ id: c.id, nome: c.nome }))
  } catch { /* silencioso */ }
}

// Diálogo de cadastro rápido de cliente (sem sair da tela)
const dialogCliente = ref(false)
const salvandoCliente = ref(false)
const formCliente = ref({ nome: '', cpfCnpj: '', telefone: '' })
function abrirNovoCliente(nome = '') {
  formCliente.value = { nome, cpfCnpj: '', telefone: '' }
  dialogCliente.value = true
}
async function salvarClienteRapido() {
  const nome = formCliente.value.nome.trim()
  if (!nome) return
  const doc = formCliente.value.cpfCnpj.replace(/\D/g, '')
  if (doc && doc.length !== 11 && doc.length !== 14) {
    notif.aviso('CPF deve ter 11 dígitos ou CNPJ 14 caracteres.'); return
  }
  salvandoCliente.value = true
  try {
    const r = await api.post('/clientes/garantir', {
      empresaId: auth.empresaId, nome, cpfCnpj: doc || null,
      telefone: formCliente.value.telefone.trim() || null,
    }, { _quiet: true } as any)
    const novo = { id: r.data.id ?? r.data, nome }
    if (!clientes.value.find(c => c.id === novo.id))
      clientes.value = [...clientes.value, novo].sort((a, b) => a.nome.localeCompare(b.nome))
    form.value.clienteId = novo.id
    dialogCliente.value = false
    notif.ok('Cliente cadastrado e selecionado!')
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao cadastrar cliente.') }
  finally { salvandoCliente.value = false }
}

const fmtParcela = computed(() =>
  fmt(Math.round((form.value.valorOriginal || 0) / (form.value.quantas || 1) * 100) / 100)
)
const fmtTotalRepetir = computed(() =>
  fmt(Math.round((form.value.valorOriginal || 0) * (form.value.quantas || 1) * 100) / 100)
)

function proximaData(base: string, periodo: string, n: number): string {
  const d = new Date(base + 'T12:00:00')
  const map: Record<string, () => void> = {
    diario:     () => d.setDate(d.getDate() + n),
    semanal:    () => d.setDate(d.getDate() + n * 7),
    quinzenal:  () => d.setDate(d.getDate() + n * 15),
    mensal:     () => d.setMonth(d.getMonth() + n),
    bimestral:  () => d.setMonth(d.getMonth() + n * 2),
    trimestral: () => d.setMonth(d.getMonth() + n * 3),
    semestral:  () => d.setMonth(d.getMonth() + n * 6),
    anual:      () => d.setFullYear(d.getFullYear() + n),
  }
  map[periodo]?.()
  return d.toISOString().slice(0, 10)
}

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date(new Date().getFullYear(), new Date().getMonth() + 1, 0).toISOString().slice(0, 10),
  categoria: 'Todas',
  status: 'Todos',
  cliente: null as string | null,
  tudo: false,
})

const headers = [
  { title: 'Descrição',  key: 'descricao',     sortable: true },
  { title: 'Categoria',  key: 'categoria',     width: 150 },
  { title: 'Cliente',    key: 'clienteNome',   sortable: true },
  { title: 'Vencimento', key: 'dataVencimento', sortable: true },
  { title: 'Valor',      key: 'valorOriginal',  sortable: true },
  { title: 'Saldo',      key: 'saldo',          sortable: true },
  { title: 'Status',     key: 'status' },
  { title: 'Ações',      key: 'actions', sortable: false },
]

const hoje = () => new Date(new Date().toISOString().slice(0, 10) + 'T12:00:00')

const clientesLista = computed(() =>
  [...new Set(lancamentos.value.map((l: any) => l.clienteNome).filter(Boolean))].sort((a: any, b: any) => a.localeCompare(b))
)

const lancamentosFiltrados = computed(() => {
  let lista = lancamentos.value
  if (filtros.value.categoria !== 'Todas')
    lista = lista.filter(l => (l.categoria ?? 'Vendas') === filtros.value.categoria)
  if (filtros.value.status !== 'Todos')
    lista = lista.filter(l => l.status === filtros.value.status)
  if (filtros.value.cliente)
    lista = lista.filter(l => l.clienteNome === filtros.value.cliente)
  return lista
})

const totais = computed(() => [
  { label: 'Em aberto',   valor: lancamentos.value.filter(l => l.status === 'EmAberto').reduce((s, l) => s + l.saldo, 0), classe: 'text-primary' },
  { label: 'Vencidos',    valor: lancamentos.value.filter(l => l.status === 'EmAberto' && new Date(String(l.dataVencimento).slice(0, 10) + 'T12:00:00') < hoje()).reduce((s, l) => s + l.saldo, 0), classe: 'text-error' },
  { label: 'Recebidos',   valor: lancamentos.value.filter(l => l.status === 'Pago').reduce((s, l) => s + l.valorOriginal, 0), classe: 'text-success' },
  { label: 'Total geral', valor: lancamentos.value.reduce((s, l) => s + l.valorOriginal, 0), classe: '' },
])

function corSubcategoria(cat?: string) {
  const mapa: Record<string, string> = {
    'Vendas': 'success', 'Serviços': 'teal',
    'Aluguel Recebido': 'blue', 'Outras Receitas': 'primary',
  }
  return mapa[cat ?? ''] ?? 'success'
}
function fmt(v: number) {
  return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function fmtData(d?: string) {
  // A data pode vir como "2026-08-05" ou ISO completo "2026-08-05T00:00:00" — normaliza para os 10 primeiros.
  return d ? new Date(String(d).slice(0, 10) + 'T12:00:00').toLocaleDateString('pt-BR') : '—'
}
function corStatus(s: string) {
  return ({ EmAberto: 'info', Pago: 'success', Cancelado: 'error', Renegociado: 'warning' } as any)[s] ?? 'default'
}

async function carregar() {
  carregando.value = true
  try {
    // "Ver todas" → não envia datas (backend retorna tudo)
    const params: any = { empresaId: auth.empresaId }
    if (!filtros.value.tudo) {
      params.inicio = filtros.value.inicio
      params.fim = filtros.value.fim
    }
    const res = await api.get('/contas-receber', { params })
    lancamentos.value = res.data
  } finally { carregando.value = false }
}

// Filtro rápido "Hoje": mostra todos os títulos que vencem hoje (sem esconder por status/categoria).
function filtrarHoje() {
  const h = new Date().toISOString().slice(0, 10)
  filtros.value.inicio = h
  filtros.value.fim = h
  filtros.value.tudo = false
  filtros.value.status = 'Todos'
  filtros.value.categoria = 'Todas'
  carregar()
}

function abrirNovo() {
  form.value = formPadrao()
  dialogNovo.value = true
}

// Duplica um título: reabre "Novo Recebimento" com os dados e o cliente em branco.
function duplicarConta(item: any) {
  form.value = {
    ...formPadrao(),
    descricao: item.descricao ?? '',
    categoria: item.categoria ?? '',
    valorOriginal: item.valorOriginal ?? 0,
    dataVencimento: (item.dataVencimento ?? '').slice(0, 10),
    observacao: item.observacao ?? '',
    clienteId: item.clienteId ?? null,
  }
  dialogNovo.value = true
  notif.aviso('Cópia carregada. Confira os dados e salve.')
}

async function salvarNovo(continuar = false) {
  const f = form.value
  if (!f.descricao || f.valorOriginal <= 0 || !f.dataVencimento) {
    notif.erro('Preencha descrição, valor e vencimento.')
    return
  }
  salvando.value = true
  try {
    const cli = clientes.value.find(c => c.id === f.clienteId)
    const base = {
      empresaId: auth.empresaId,
      descricao: f.descricao,
      categoria: f.categoria || null,
      pessoaId: f.clienteId || null,
      clienteNome: cli?.nome || null,
      observacao: f.observacao || null,
    }

    if (f.modo === 'unico') {
      await api.post('/contas-receber', {
        ...base, valor: f.valorOriginal,
        primeiroVencimento: f.dataVencimento, totalParcelas: 1,
      })
    } else if (f.modo === 'parcelar') {
      const n = Math.max(2, f.quantas || 2)
      const valorParcela = Math.round(f.valorOriginal / n * 100) / 100
      for (let i = 0; i < n; i++) {
        await api.post('/contas-receber', {
          ...base,
          descricao: `${f.descricao} ${i + 1}/${n}`,
          valor: i === n - 1
            ? Math.round((f.valorOriginal - valorParcela * (n - 1)) * 100) / 100
            : valorParcela,
          primeiroVencimento: i === 0 ? f.dataVencimento : proximaData(f.dataVencimento, f.periodo, i),
          totalParcelas: 1,
        })
      }
    } else {
      const n = Math.max(2, f.quantas || 2)
      for (let i = 0; i < n; i++) {
        await api.post('/contas-receber', {
          ...base,
          descricao: `${f.descricao} ${i + 1}/${n}`,
          valor: f.valorOriginal,
          primeiroVencimento: i === 0 ? f.dataVencimento : proximaData(f.dataVencimento, f.periodo, i),
          totalParcelas: 1,
        })
      }
    }

    if (continuar) {
      // Mantém descrição/categoria/valor/vencimento e limpa só o cliente
      f.clienteId = null
      f._buscaCliente = ''
      notif.ok('Recebimento cadastrado! Escolha o próximo cliente.')
    } else {
      notif.ok('Recebimento(s) cadastrado(s)!')
      dialogNovo.value = false
    }
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao salvar.') }
  finally { salvando.value = false }
}

function abrirBaixa(item: any) {
  baixa.value = { id: item.id, valor: item.saldo, data: new Date().toISOString().slice(0, 10) }
  dialogBaixa.value = true
}

async function confirmarBaixa() {
  salvando.value = true
  try {
    await api.post(`/contas-receber/${baixa.value.id}/baixar`, {
      valorPago: baixa.value.valor, dataPagamento: baixa.value.data,
    })
    notif.ok('Título baixado!')
    dialogBaixa.value = false
    await carregar()
  } finally { salvando.value = false }
}

function abrirRenegociacao(item: any) {
  itemReneg.value = item
  reneg.value = {
    novoValor: item.saldo,
    novoVencimento: '',
    motivo: '',
  }
  dialogReneg.value = true
}

async function confirmarRenegociacao() {
  if (!reneg.value.novoVencimento) { notif.erro('Informe o novo vencimento.'); return }
  salvando.value = true
  try {
    await api.post(`/contas-receber/${itemReneg.value.id}/renegociar`, {
      novoValor: reneg.value.novoValor,
      novoVencimento: reneg.value.novoVencimento,
      observacao: reneg.value.motivo,
    })
    notif.ok('Título renegociado!')
    dialogReneg.value = false
    await carregar()
  } catch { notif.erro('Erro ao renegociar.') }
  finally { salvando.value = false }
}

const route = useRoute()

onMounted(() => {
  // Vindo do Dashboard (calendário) com ?data=YYYY-MM-DD: filtra só aquele dia.
  const data = route.query.data
  if (typeof data === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(data)) {
    filtros.value.inicio = data
    filtros.value.fim = data
    filtros.value.tudo = false
    filtros.value.status = 'Todos'
    filtros.value.categoria = 'Todas'
  }
  carregar()
  carregarClientes()
})
</script>
