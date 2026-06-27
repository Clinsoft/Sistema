<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Contas a Receber</div>
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">Nova</v-btn>
    </div>

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
      </v-row>
      <div class="d-flex justify-end mt-2">
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
            {{ item.status }}
          </v-chip>
        </template>
        <template #item.valorOriginal="{ item }">R$ {{ fmt(item.valorOriginal) }}</template>
        <template #item.saldo="{ item }">R$ {{ fmt(item.saldo) }}</template>
        <template #item.dataVencimento="{ item }">{{ fmtData(item.dataVencimento) }}</template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-cash-check" size="x-small" color="success" variant="text"
            title="Baixar" @click="abrirBaixa(item)" />
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
              <v-text-field v-model="form.clienteNome" label="Cliente / Pagador"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model.number="form.valorOriginal" label="Valor (R$) *"
                type="number" step="0.01" prefix="R$" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="form.dataVencimento" label="Vencimento *"
                type="date" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="form.observacao" label="Observação"
                variant="outlined" density="compact" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogNovo = false" :disabled="salvando">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando" @click="salvarNovo">Salvar</v-btn>
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
import FiltroMes from '@/components/FiltroMes.vue'
import { ref, computed, onMounted } from 'vue'
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

const formPadrao = () => ({
  descricao: '', categoria: '', clienteNome: '',
  valorOriginal: 0, dataVencimento: '', observacao: '',
})
const form = ref(formPadrao())

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
  categoria: 'Todas',
  status: 'Todos',
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

const lancamentosFiltrados = computed(() => {
  let lista = lancamentos.value
  if (filtros.value.categoria !== 'Todas')
    lista = lista.filter(l => (l.categoria ?? 'Vendas') === filtros.value.categoria)
  if (filtros.value.status !== 'Todos')
    lista = lista.filter(l => l.status === filtros.value.status)
  return lista
})

const totais = computed(() => [
  { label: 'Em aberto',   valor: lancamentos.value.filter(l => l.status === 'EmAberto').reduce((s, l) => s + l.saldo, 0), classe: 'text-primary' },
  { label: 'Vencidos',    valor: lancamentos.value.filter(l => l.status === 'EmAberto' && new Date(l.dataVencimento + 'T12:00:00') < hoje()).reduce((s, l) => s + l.saldo, 0), classe: 'text-error' },
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
  return d ? new Date(d + 'T12:00:00').toLocaleDateString('pt-BR') : '—'
}
function corStatus(s: string) {
  return ({ EmAberto: 'info', Pago: 'success', Cancelado: 'error', Renegociado: 'warning' } as any)[s] ?? 'default'
}

async function carregar() {
  carregando.value = true
  try {
    const res = await api.get('/contas-receber', {
      params: {
        empresaId: auth.empresaId,
        inicio: filtros.value.inicio,
        fim: filtros.value.fim,
        status: filtros.value.status !== 'Todos' ? filtros.value.status : undefined,
      },
    })
    lancamentos.value = res.data
  } finally { carregando.value = false }
}

function abrirNovo() {
  form.value = formPadrao()
  dialogNovo.value = true
}

async function salvarNovo() {
  if (!form.value.descricao || form.value.valorOriginal <= 0 || !form.value.dataVencimento) {
    notif.erro('Preencha descrição, valor e vencimento.')
    return
  }
  salvando.value = true
  try {
    await api.post('/contas-receber', { empresaId: auth.empresaId, ...form.value })
    notif.ok('Recebimento cadastrado!')
    dialogNovo.value = false
    await carregar()
  } catch { notif.erro('Erro ao salvar.') }
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
    await api.post(`/contas-receber/${itemReneg.value.id}/renegociar`, reneg.value)
    notif.ok('Título renegociado!')
    dialogReneg.value = false
    await carregar()
  } catch { notif.erro('Erro ao renegociar.') }
  finally { salvando.value = false }
}

onMounted(carregar)
</script>
