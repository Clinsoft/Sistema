<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Fluxo de Caixa</div>
      <v-btn-toggle v-model="tipo" mandatory density="compact" rounded="lg">
        <v-btn value="realizado">Realizado</v-btn>
        <v-btn value="projetado">Projetado</v-btn>
      </v-btn-toggle>
    </div>

    <GuiaPassos
      id="fluxo-caixa"
      titulo="Como usar o Fluxo de Caixa"
      :passos="[
        'Escolha <b>Realizado</b> (entradas e saídas já efetivadas) ou <b>Projetado</b> (contas em aberto por vencimento).',
        'Selecione o período (mês ou datas) e clique na <b>lupa</b> para atualizar. Filtre por <b>categoria</b> se quiser.',
        'O <b>Realizado</b> soma vendas do PDV, recebimentos baixados e pagamentos efetuados. O <b>Projetado</b> mostra o que vai entrar/sair pelas contas em aberto.',
        'É um <b>relatório</b>: para alterar um valor, edite a venda no PDV ou o lançamento em <b>Contas a Pagar/Receber</b>. O <b>Saldo Acumulado</b> parte do saldo das contas bancárias.',
      ]"
    />

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
      <v-row dense align="center">
        <v-col cols="12" sm="3">
          <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f; carregar() }" />
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
            :items="['Todas', 'Recebimentos', 'Custo (CMV)', 'Despesas Administrativas', 'Despesas Operacionais', 'Despesas Variáveis', 'Pessoas', 'Impostos']"
            variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" sm="3">
          <v-autocomplete v-model="filtros.beneficiario" label="Fornecedor / Cliente"
            :items="beneficiariosLista" variant="outlined" density="compact" hide-details clearable
            no-data-text="Sem movimentos no período" />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" icon="mdi-magnify"
            :loading="carregando" @click="carregar" />
        </v-col>
      </v-row>
    </v-card>

    <!-- Cards de saldo -->
    <v-row v-if="dados" class="mb-3">
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="text-caption text-medium-emphasis">Total Entradas</div>
            <div class="text-h6 font-weight-bold text-success">R$ {{ fmt(totaisFluxo.entradas) }}</div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="text-caption text-medium-emphasis">Total Saídas</div>
            <div class="text-h6 font-weight-bold text-error">R$ {{ fmt(totaisFluxo.saidas) }}</div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="text-caption text-medium-emphasis">Resultado do período</div>
            <div class="text-h6 font-weight-bold"
              :class="totaisFluxo.saldo >= 0 ? 'text-success' : 'text-error'">
              R$ {{ fmt(totaisFluxo.saldo) }}
            </div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="text-caption text-medium-emphasis">Saldo Acumulado</div>
            <div class="text-h6 font-weight-bold"
              :class="totaisFluxo.acumulado >= 0 ? 'text-primary' : 'text-error'">
              R$ {{ fmt(totaisFluxo.acumulado) }}
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Tabela -->
    <v-card v-if="dados" rounded="xl" elevation="1">
      <v-data-table
        :headers="headers"
        :items="linhasFiltradas"
        density="compact"
        items-per-page="-1"
        hide-default-footer
      >
        <template #item.data="{ item }">
          {{ item.data ? new Date(item.data + 'T12:00:00').toLocaleDateString('pt-BR') : '—' }}
        </template>

        <template #item.categoria="{ item }">
          <v-chip v-if="item.categoria"
            :color="corCategoria(item.categoria)"
            size="x-small" variant="tonal" label>
            <v-icon start size="11" :icon="iconCategoria(item.categoria)" />
            {{ item.categoria }}
          </v-chip>
          <span v-else class="text-medium-emphasis text-caption">—</span>
        </template>

        <template #item.entradas="{ item }">
          <span v-if="item.entradas" class="text-success font-weight-medium">
            R$ {{ fmt(item.entradas) }}
          </span>
          <span v-else class="text-medium-emphasis text-caption">—</span>
        </template>

        <template #item.saidas="{ item }">
          <span v-if="item.saidas" class="text-error font-weight-medium">
            R$ {{ fmt(item.saidas) }}
          </span>
          <span v-else class="text-medium-emphasis text-caption">—</span>
        </template>

        <template #item.saldo="{ item }">
          <span :class="(item.saldo ?? 0) >= 0 ? 'text-success' : 'text-error'" class="font-weight-bold">
            R$ {{ fmt(item.saldo) }}
          </span>
        </template>

        <template #item.acumulado="{ item }">
          <span :class="(item.acumulado ?? 0) >= 0 ? 'text-primary' : 'text-error'" class="font-weight-bold">
            R$ {{ fmt(item.acumulado) }}
          </span>
        </template>
      </v-data-table>
    </v-card>

    <div v-if="carregando" class="d-flex justify-center pa-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <v-card v-if="!dados && !carregando" rounded="xl" elevation="0" class="pa-8 text-center">
      <v-icon icon="mdi-cash-flow" size="48" class="text-medium-emphasis mb-2" />
      <div class="text-body-1 text-medium-emphasis">Selecione o período e clique em buscar</div>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import FiltroMes from '@/components/FiltroMes.vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import { ref, computed, watch, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const carregando = ref(false)
const tipo = ref('realizado')
const dados = ref<any>(null)

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date(new Date().getFullYear(), new Date().getMonth() + 1, 0).toISOString().slice(0, 10),
  categoria: null as string | null,
  beneficiario: null as string | null,
})

const headers = [
  { title: 'Data / Período', key: 'data',       width: 130 },
  { title: 'Descrição',      key: 'descricao'  },
  { title: 'Fornecedor / Cliente', key: 'beneficiario', width: 180 },
  { title: 'Categoria',      key: 'categoria',  width: 180 },
  { title: 'Entradas',       key: 'entradas',   width: 130 },
  { title: 'Saídas',         key: 'saidas',     width: 130 },
  { title: 'Saldo',          key: 'saldo',      width: 130 },
  { title: 'Acumulado',      key: 'acumulado',  width: 130 },
]

const linhas = computed(() => dados.value?.linhas ?? [])

const beneficiariosLista = computed(() =>
  [...new Set(linhas.value.map((l: any) => l.beneficiario).filter(Boolean))].sort((a: any, b: any) => a.localeCompare(b))
)

const linhasFiltradas = computed(() => {
  let l = linhas.value
  if (filtros.value.categoria && filtros.value.categoria !== 'Todas')
    l = l.filter((x: any) => x.categoria === filtros.value.categoria)
  if (filtros.value.beneficiario)
    l = l.filter((x: any) => x.beneficiario === filtros.value.beneficiario)
  return l
})

const totaisFluxo = computed(() => ({
  entradas:  linhas.value.reduce((s: number, l: any) => s + (l.entradas ?? 0), 0),
  saidas:    linhas.value.reduce((s: number, l: any) => s + (l.saidas ?? 0), 0),
  saldo:     linhas.value.reduce((s: number, l: any) => s + ((l.entradas ?? 0) - (l.saidas ?? 0)), 0),
  acumulado: linhas.value.length ? (linhas.value[linhas.value.length - 1].acumulado ?? 0) : 0,
}))

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })

function corCategoria(cat: string) {
  const mapa: Record<string, string> = {
    'Recebimentos': 'success',
    'Despesas Administrativas': 'deep-purple', 'Despesas Operacionais': 'teal',
    'Despesas Variáveis': 'orange', 'Pessoas': 'blue', 'Impostos': 'error',
  }
  return mapa[cat] ?? 'grey'
}
function iconCategoria(cat: string) {
  const mapa: Record<string, string> = {
    'Recebimentos': 'mdi-arrow-down-circle-outline',
    'Despesas Administrativas': 'mdi-home-city-outline',
    'Despesas Operacionais': 'mdi-cog-outline',
    'Despesas Variáveis': 'mdi-chart-bell-curve-cumulative',
    'Pessoas': 'mdi-account-group-outline',
    'Impostos': 'mdi-gavel',
  }
  return mapa[cat] ?? 'mdi-tag-outline'
}

async function carregar() {
  carregando.value = true
  dados.value = null
  try {
    const path = tipo.value === 'realizado'
      ? '/financeiro/fluxo-caixa/realizado'
      : '/financeiro/fluxo-caixa/projetado'
    const r = await api.get(path, {
      params: { empresaId: auth.empresaId, inicio: filtros.value.inicio, fim: filtros.value.fim },
    })
    dados.value = r.data
  } finally { carregando.value = false }
}

watch(tipo, () => { dados.value = null; carregar() })
onMounted(carregar)
</script>
