<template>
  <div>
    <div class="d-flex align-center mb-4 flex-wrap gap-2">
      <div class="text-h6 font-weight-bold flex-grow-1">Relatórios de Materiais de Consumo</div>
      <v-btn variant="tonal" rounded="lg" prepend-icon="mdi-package-variant-closed"
        to="/estoque/materiais">Ir para Materiais</v-btn>
    </div>

    <!-- Período (relatórios que dependem de datas) -->
    <v-card v-if="abaUsaPeriodo" rounded="xl" elevation="1" class="mb-3 pa-3">
      <v-row dense align="center">
        <v-col cols="12" sm="3">
          <FiltroMes @selecionar="(i, f) => { inicio = i; fim = f; carregar() }" />
        </v-col>
        <v-col cols="6" sm="3">
          <v-text-field v-model="inicio" label="Início" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="6" sm="3">
          <v-text-field v-model="fim" label="Fim" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" rounded="lg" prepend-icon="mdi-magnify"
            :loading="carregando" @click="carregar">Buscar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <v-tabs v-model="aba" bg-color="transparent" class="mb-3" @update:model-value="carregar">
      <v-tab value="estoque" prepend-icon="mdi-warehouse">Estoque</v-tab>
      <v-tab value="consumo" prepend-icon="mdi-tray-arrow-down">Consumo</v-tab>
      <v-tab value="giro" prepend-icon="mdi-autorenew">Giro</v-tab>
      <v-tab value="ultima-compra" prepend-icon="mdi-cart-outline">Última compra</v-tab>
      <v-tab value="custo-total" prepend-icon="mdi-cash-multiple">Custo total</v-tab>
      <v-tab value="abaixo-minimo" prepend-icon="mdi-alert-outline">
        Abaixo do mínimo
        <v-chip v-if="qtdAbaixoMinimo > 0" color="warning" size="x-small" class="ml-2">
          {{ qtdAbaixoMinimo }}
        </v-chip>
      </v-tab>
    </v-tabs>

    <!-- Cards de resumo -->
    <v-row v-if="cards.length" class="mb-3">
      <v-col v-for="c in cards" :key="c.label" cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="d-flex align-center gap-1 mb-1">
              <v-icon :icon="c.icon" :color="c.cor" size="15" />
              <span class="text-caption text-medium-emphasis">{{ c.label }}</span>
            </div>
            <div class="text-h6 font-weight-bold" :class="`text-${c.cor}`">{{ c.valor }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <div v-if="carregando" class="pa-8 text-center">
        <v-progress-circular indeterminate color="primary" />
      </div>

      <!-- Custo total é só um resumo (sem tabela) -->
      <v-card-text v-else-if="aba === 'custo-total'" class="pa-6 text-center text-medium-emphasis">
        Valores do período nos cards acima. <b>Valor em estoque</b> é a posição atual;
        <b>comprado</b> e <b>consumido</b> consideram as datas selecionadas.
      </v-card-text>

      <v-data-table v-else :headers="headers" :items="itens" density="compact" hover
        :items-per-page="50" items-per-page-text="Itens por página"
        :no-data-text="semDados">
        <template #item.estoqueAtual="{ item }">
          <v-chip :color="item.abaixoDoMinimo ? 'error' : 'success'" size="small" variant="tonal">
            {{ fmtQtd(item.estoqueAtual) }} {{ item.unidadeSigla ?? '' }}
          </v-chip>
        </template>
        <template #item.custoMedio="{ item }">R$ {{ fmt(item.custoMedio) }}</template>
        <template #item.ultimoCusto="{ item }">R$ {{ fmt(item.ultimoCusto) }}</template>
        <template #item.valorEmEstoque="{ item }">R$ {{ fmt(item.valorEmEstoque) }}</template>
        <template #item.valor="{ item }">R$ {{ fmt(item.valor) }}</template>
        <template #item.valorConsumido="{ item }">R$ {{ fmt(item.valorConsumido) }}</template>
        <template #item.custoReposicao="{ item }">R$ {{ fmt(item.custoReposicao) }}</template>
        <template #item.quantidade="{ item }">{{ fmtQtd(item.quantidade) }}</template>
        <template #item.consumo="{ item }">{{ fmtQtd(item.consumo) }}</template>
        <template #item.consumoInterno="{ item }">{{ fmtQtd(item.consumoInterno) }}</template>
        <template #item.producao="{ item }">{{ fmtQtd(item.producao) }}</template>
        <template #item.perda="{ item }">
          <span :class="item.perda > 0 ? 'text-error font-weight-medium' : ''">{{ fmtQtd(item.perda) }}</span>
        </template>
        <template #item.repor="{ item }">
          <span class="text-warning font-weight-medium">{{ fmtQtd(item.repor) }}</span>
        </template>
        <template #item.estoqueMinimo="{ item }">{{ fmtQtd(item.estoqueMinimo) }}</template>
        <template #item.consumoDiario="{ item }">{{ fmtQtd(item.consumoDiario) }}</template>
        <template #item.giro="{ item }">
          <v-chip size="small" variant="tonal" :color="corGiro(item.giro)">{{ fmtQtd(item.giro) }}</v-chip>
        </template>
        <template #item.diasCobertura="{ item }">
          <span v-if="item.diasCobertura === null" class="text-medium-emphasis">—</span>
          <v-chip v-else size="small" variant="tonal"
            :color="item.diasCobertura <= 15 ? 'error' : item.diasCobertura <= 30 ? 'warning' : 'success'">
            {{ fmtQtd(item.diasCobertura) }} dias
          </v-chip>
        </template>
        <template #item.dataUltimaCompra="{ item }">{{ fmtData(item.dataUltimaCompra) }}</template>
        <template #item.ultimaEntrada="{ item }">{{ fmtData(item.ultimaEntrada) }}</template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import FiltroMes from '@/components/FiltroMes.vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

const aba = ref('estoque')
const carregando = ref(false)
const itens = ref<any[]>([])
const resumo = ref<any>(null)
const qtdAbaixoMinimo = ref(0)

const hoje = new Date()
const inicio = ref(new Date(hoje.getFullYear(), hoje.getMonth(), 1).toISOString().slice(0, 10))
const fim = ref(hoje.toISOString().slice(0, 10))

const abaUsaPeriodo = computed(() => ['consumo', 'giro', 'custo-total'].includes(aba.value))

const fmt = (v?: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtQtd = (v?: number) => (v ?? 0).toLocaleString('pt-BR', { maximumFractionDigits: 3 })
const fmtData = (d?: string) => d ? new Date(d).toLocaleDateString('pt-BR') : '—'

// Giro alto = material que roda; baixo = estoque parado
const corGiro = (g: number) => g >= 1 ? 'success' : g >= 0.3 ? 'info' : 'grey'

const headersPorAba: Record<string, any[]> = {
  'estoque': [
    { title: 'Código', key: 'codigo', width: 90 },
    { title: 'Material', key: 'descricao' },
    { title: 'Estoque', key: 'estoqueAtual', width: 130 },
    { title: 'Mínimo', key: 'estoqueMinimo', width: 90 },
    { title: 'Custo médio', key: 'custoMedio', width: 110 },
    { title: 'Último custo', key: 'ultimoCusto', width: 110 },
    { title: 'Valor em estoque', key: 'valorEmEstoque', width: 130 },
    { title: 'Local', key: 'localizacao', width: 110 },
  ],
  'consumo': [
    { title: 'Código', key: 'codigo', width: 90 },
    { title: 'Material', key: 'descricao' },
    { title: 'Consumido', key: 'quantidade', width: 110 },
    { title: 'Uso interno', key: 'consumoInterno', width: 110 },
    { title: 'Produção', key: 'producao', width: 100 },
    { title: 'Perda', key: 'perda', width: 90 },
    { title: 'Valor', key: 'valor', width: 110 },
  ],
  'giro': [
    { title: 'Código', key: 'codigo', width: 90 },
    { title: 'Material', key: 'descricao' },
    { title: 'Estoque', key: 'estoqueAtual', width: 120 },
    { title: 'Consumo', key: 'consumo', width: 110 },
    { title: 'Consumo/dia', key: 'consumoDiario', width: 120 },
    { title: 'Giro', key: 'giro', width: 90 },
    { title: 'Cobertura', key: 'diasCobertura', width: 120 },
    { title: 'Valor consumido', key: 'valorConsumido', width: 130 },
  ],
  'ultima-compra': [
    { title: 'Código', key: 'codigo', width: 90 },
    { title: 'Material', key: 'descricao' },
    { title: 'Fornecedor', key: 'fornecedorNome' },
    { title: 'Última compra', key: 'dataUltimaCompra', width: 130 },
    { title: 'Última entrada', key: 'ultimaEntrada', width: 130 },
    { title: 'Último custo', key: 'ultimoCusto', width: 110 },
    { title: 'Custo médio', key: 'custoMedio', width: 110 },
  ],
  'abaixo-minimo': [
    { title: 'Código', key: 'codigo', width: 90 },
    { title: 'Material', key: 'descricao' },
    { title: 'Fornecedor', key: 'fornecedorNome' },
    { title: 'Estoque', key: 'estoqueAtual', width: 130 },
    { title: 'Mínimo', key: 'estoqueMinimo', width: 90 },
    { title: 'Repor', key: 'repor', width: 100 },
    { title: 'Custo reposição', key: 'custoReposicao', width: 130 },
  ],
  'custo-total': [],
}
const headers = computed(() => headersPorAba[aba.value] ?? [])

const semDados = computed(() => ({
  'estoque': 'Nenhum material cadastrado.',
  'consumo': 'Nenhum consumo registrado no período.',
  'giro': 'Nenhum material para calcular giro.',
  'ultima-compra': 'Nenhuma compra registrada.',
  'abaixo-minimo': 'Nenhum material abaixo do mínimo. 👍',
} as any)[aba.value] ?? 'Sem dados.')

const cards = computed(() => {
  const r = resumo.value
  if (!r) return []
  switch (aba.value) {
    case 'estoque':
      return [
        { label: 'Materiais', valor: String(r.totalItens ?? 0), cor: 'primary', icon: 'mdi-package-variant-closed' },
        { label: 'Valor em estoque', valor: 'R$ ' + fmt(r.valorTotal), cor: 'success', icon: 'mdi-cash-multiple' },
        { label: 'Abaixo do mínimo', valor: String(r.abaixoMinimo ?? 0), cor: 'warning', icon: 'mdi-alert-outline' },
      ]
    case 'consumo':
      return [
        { label: 'Materiais consumidos', valor: String(itens.value.length), cor: 'primary', icon: 'mdi-tray-arrow-down' },
        { label: 'Valor consumido', valor: 'R$ ' + fmt(r.valorTotal), cor: 'teal', icon: 'mdi-cash-multiple' },
      ]
    case 'custo-total':
      return [
        { label: 'Valor em estoque', valor: 'R$ ' + fmt(r.valorEmEstoque), cor: 'success', icon: 'mdi-warehouse' },
        { label: 'Comprado no período', valor: 'R$ ' + fmt(r.valorComprado), cor: 'primary', icon: 'mdi-cart-outline' },
        { label: 'Consumido no período', valor: 'R$ ' + fmt(r.valorConsumido), cor: 'teal', icon: 'mdi-tray-arrow-down' },
      ]
    case 'abaixo-minimo':
      return [
        { label: 'Para repor', valor: String(r.total ?? 0), cor: 'warning', icon: 'mdi-alert-outline' },
        { label: 'Custo da reposição', valor: 'R$ ' + fmt(r.custoReposicao), cor: 'error', icon: 'mdi-cash-multiple' },
      ]
    default:
      return []
  }
})

async function carregar() {
  carregando.value = true
  itens.value = []
  resumo.value = null
  try {
    const params: any = { empresaId: auth.empresaId }
    if (abaUsaPeriodo.value) { params.inicio = inicio.value; params.fim = fim.value }

    const r = await api.get(`/relatorios/materiais/${aba.value}`, { params })
    itens.value = r.data.itens ?? []
    resumo.value = r.data
  } catch { itens.value = [] } finally { carregando.value = false }
}

/** Badge da aba: quantos materiais estão abaixo do mínimo. */
async function carregarBadge() {
  try {
    const r = await api.get('/relatorios/materiais/abaixo-minimo', { params: { empresaId: auth.empresaId } })
    qtdAbaixoMinimo.value = r.data.total ?? 0
  } catch { qtdAbaixoMinimo.value = 0 }
}

onMounted(() => { carregar(); carregarBadge() })
</script>
