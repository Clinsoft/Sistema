<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Contas a Pagar</div>
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">Nova</v-btn>
    </div>

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
      <v-row dense>
        <v-col cols="12" sm="3">
          <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f }" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.inicio" label="Início" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.fim" label="Fim" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-select v-model="filtros.categoria" label="Categoria"
            :items="['Todas', ...categorias]" variant="outlined" density="compact" hide-details />
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

    <!-- Totais por categoria -->
    <v-row class="mb-3">
      <v-col v-for="t in totaisCategorias" :key="t.label" cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="d-flex align-center gap-1 mb-1">
              <v-icon :icon="t.icon" :color="t.cor" size="15" />
              <span class="text-caption text-medium-emphasis">{{ t.label }}</span>
            </div>
            <div class="text-h6 font-weight-bold" :class="`text-${t.cor}`">R$ {{ fmt(t.valor) }}</div>
            <div class="text-caption text-medium-emphasis">em aberto</div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card rounded="xl" elevation="1" color="grey-lighten-4">
          <v-card-text class="pa-3">
            <div class="text-caption text-medium-emphasis mb-1">Total em Aberto</div>
            <div class="text-h6 font-weight-bold text-error">R$ {{ fmt(totalAberto) }}</div>
            <div class="text-caption text-medium-emphasis">
              Vencidos: R$ {{ fmt(totalVencidos) }}
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table
        :headers="headers"
        :items="lancamentosFiltrados"
        :loading="carregando"
        density="compact"
        hover
      >
        <template #item.categoria="{ item }">
          <v-chip :color="corCategoria(item.categoria)" size="x-small" variant="tonal" label>
            <v-icon start size="11" :icon="iconCategoria(item.categoria)" />
            {{ item.categoria ?? '—' }}
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
            title="Pagar" @click="abrirPagamento(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog: Nova Conta a Pagar -->
    <v-dialog v-model="dialogNovo" max-width="520" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon start color="error">mdi-arrow-up-circle-outline</v-icon>
          Nova Conta a Pagar
        </v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12">
              <v-text-field v-model="form.descricao" label="Descrição *"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-select v-model="form.categoria" label="Categoria *" :items="categorias"
                variant="outlined" density="compact">
                <template #item="{ item, props }">
                  <v-list-item v-bind="props">
                    <template #prepend>
                      <v-icon :icon="iconCategoria(item.value)" :color="corCategoria(item.value)" size="18" class="mr-2" />
                    </template>
                  </v-list-item>
                </template>
                <template #selection="{ item }">
                  <v-chip :color="corCategoria(item.value)" size="x-small" variant="tonal" label class="mr-1">
                    <v-icon start size="11" :icon="iconCategoria(item.value)" />{{ item.value }}
                  </v-chip>
                </template>
              </v-select>
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="form.fornecedorNome" label="Fornecedor / Beneficiário"
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
          <v-btn color="primary" rounded="lg" :loading="salvando" @click="salvarNova">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Registrar Pagamento -->
    <v-dialog v-model="dialogPagamento" max-width="400">
      <v-card rounded="xl" class="pa-4">
        <v-card-title>Registrar Pagamento</v-card-title>
        <v-card-text>
          <v-text-field v-model.number="pagamento.valor" label="Valor pago (R$)"
            type="number" variant="outlined" density="compact" class="mb-2" />
          <v-text-field v-model="pagamento.data" label="Data pagamento"
            type="date" variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions class="justify-end">
          <v-btn variant="text" @click="dialogPagamento = false">Cancelar</v-btn>
          <v-btn color="success" :loading="salvando" @click="confirmarPagamento">Confirmar</v-btn>
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
const dialogPagamento = ref(false)
const dialogNovo = ref(false)
const pagamento = ref({ id: '', valor: 0, data: new Date().toISOString().slice(0, 10) })

const categorias = ['Despesas Fixas', 'Despesas Variáveis', 'Pessoas', 'Impostos']

const formPadrao = () => ({
  descricao: '', categoria: '', fornecedorNome: '',
  valorOriginal: 0, dataVencimento: '', observacao: '',
})
const form = ref(formPadrao())

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
  categoria: 'Todas',
  status: 'Todos',
})

const hoje = () => new Date(new Date().toISOString().slice(0, 10) + 'T12:00:00')

const lancamentosFiltrados = computed(() => {
  let lista = lancamentos.value
  if (filtros.value.categoria !== 'Todas')
    lista = lista.filter(l => l.categoria === filtros.value.categoria)
  if (filtros.value.status !== 'Todos')
    lista = lista.filter(l => l.status === filtros.value.status)
  return lista
})

function somarAberto(cat: string) {
  return lancamentos.value
    .filter(l => l.categoria === cat && l.status === 'EmAberto')
    .reduce((s: number, l: any) => s + l.saldo, 0)
}

const totalAberto = computed(() =>
  lancamentos.value.filter(l => l.status === 'EmAberto').reduce((s: number, l: any) => s + l.saldo, 0)
)
const totalVencidos = computed(() =>
  lancamentos.value
    .filter(l => l.status === 'EmAberto' && new Date(l.dataVencimento + 'T12:00:00') < hoje())
    .reduce((s: number, l: any) => s + l.saldo, 0)
)

const totaisCategorias = computed(() => [
  { label: 'Despesas Fixas',     valor: somarAberto('Despesas Fixas'),     cor: 'deep-purple', icon: 'mdi-home-city-outline' },
  { label: 'Despesas Variáveis', valor: somarAberto('Despesas Variáveis'), cor: 'orange',      icon: 'mdi-chart-bell-curve-cumulative' },
  { label: 'Pessoas',            valor: somarAberto('Pessoas'),            cor: 'blue',        icon: 'mdi-account-group-outline' },
  { label: 'Impostos',           valor: somarAberto('Impostos'),           cor: 'error',       icon: 'mdi-gavel' },
])

const headers = [
  { title: 'Descrição',  key: 'descricao',     sortable: true },
  { title: 'Categoria',  key: 'categoria',     width: 170 },
  { title: 'Fornecedor', key: 'fornecedorNome' },
  { title: 'Vencimento', key: 'dataVencimento', sortable: true },
  { title: 'Valor',      key: 'valorOriginal' },
  { title: 'Saldo',      key: 'saldo' },
  { title: 'Status',     key: 'status' },
  { title: '',           key: 'actions', sortable: false },
]

function corCategoria(cat?: string) {
  const mapa: Record<string, string> = {
    'Despesas Fixas': 'deep-purple', 'Despesas Variáveis': 'orange',
    'Pessoas': 'blue', 'Impostos': 'error',
  }
  return mapa[cat ?? ''] ?? 'grey'
}
function iconCategoria(cat?: string) {
  const mapa: Record<string, string> = {
    'Despesas Fixas': 'mdi-home-city-outline', 'Despesas Variáveis': 'mdi-chart-bell-curve-cumulative',
    'Pessoas': 'mdi-account-group-outline', 'Impostos': 'mdi-gavel',
  }
  return mapa[cat ?? ''] ?? 'mdi-tag-outline'
}
const corStatus = (s: string) =>
  ({ EmAberto: 'info', Pago: 'success', Cancelado: 'error', Renegociado: 'warning' } as any)[s] ?? 'default'
const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtData = (d?: string) => d ? new Date(d + 'T12:00:00').toLocaleDateString('pt-BR') : '—'

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/contas-pagar', {
      params: { empresaId: auth.empresaId, inicio: filtros.value.inicio, fim: filtros.value.fim },
    })
    lancamentos.value = r.data
  } finally { carregando.value = false }
}

function abrirNovo() {
  form.value = formPadrao()
  dialogNovo.value = true
}

async function salvarNova() {
  if (!form.value.descricao || !form.value.categoria || form.value.valorOriginal <= 0 || !form.value.dataVencimento) {
    notif.erro('Preencha todos os campos obrigatórios.')
    return
  }
  salvando.value = true
  try {
    await api.post('/contas-pagar', { empresaId: auth.empresaId, ...form.value })
    notif.ok('Conta a pagar cadastrada!')
    dialogNovo.value = false
    await carregar()
  } catch { notif.erro('Erro ao salvar.') }
  finally { salvando.value = false }
}

function abrirPagamento(item: any) {
  pagamento.value = { id: item.id, valor: item.saldo, data: new Date().toISOString().slice(0, 10) }
  dialogPagamento.value = true
}

async function confirmarPagamento() {
  salvando.value = true
  try {
    await api.post(`/contas-pagar/${pagamento.value.id}/pagar`, {
      valorPago: pagamento.value.valor, dataPagamento: pagamento.value.data,
    })
    notif.ok('Pagamento registrado!')
    dialogPagamento.value = false
    await carregar()
  } finally { salvando.value = false }
}

onMounted(carregar)
</script>
