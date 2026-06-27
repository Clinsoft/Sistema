<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Posição de Estoque</h2></v-col>
      <v-col cols="auto">
        <v-btn prepend-icon="mdi-refresh" variant="tonal" @click="carregar">Atualizar</v-btn>
      </v-col>
    </v-row>

    <!-- Totalizadores -->
    <v-row class="mb-4">
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h5 font-weight-bold text-primary">{{ totais.qtdProdutos }}</div>
          <div class="text-caption text-medium-emphasis">Produtos</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h5 font-weight-bold text-warning">{{ totais.qtdAbaixoMinimo }}</div>
          <div class="text-caption text-medium-emphasis">Abaixo do Mínimo</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-success">R$ {{ fmt(totais.custoTotalEstoque) }}</div>
          <div class="text-caption text-medium-emphasis">Custo Total</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold">R$ {{ fmt(totais.valorVendaTotalEstoque) }}</div>
          <div class="text-caption text-medium-emphasis">Valor de Venda</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" md="4">
          <v-text-field v-model="busca" placeholder="Buscar produto…" prepend-inner-icon="mdi-magnify"
            variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" md="3">
          <v-select v-model="filtroCategoria" :items="categorias" item-title="nome" item-value="id"
            label="Categoria" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="auto">
          <v-switch v-model="apenasAbaixoMinimo" label="Apenas abaixo do mínimo"
            color="warning" density="compact" hide-details inset />
        </v-col>
      </v-row>
    </v-card>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="produtosFiltrados" :loading="carregando"
        density="compact" hover items-per-page="25">
        <template #item.estoqueAtual="{ item }">
          <v-chip :color="item.abaixoMinimo ? 'error' : 'success'" size="small" variant="tonal">
            {{ item.estoqueAtual }}
          </v-chip>
        </template>
        <template #item.custoTotal="{ item }">R$ {{ fmt(item.custoTotal) }}</template>
        <template #item.valorVendaTotal="{ item }">R$ {{ fmt(item.valorVendaTotal) }}</template>
        <template #item.custoUnitario="{ item }">R$ {{ fmt(item.custoUnitario) }}</template>
        <template #item.precoVenda="{ item }">R$ {{ fmt(item.precoVenda) }}</template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const carregando = ref(false)
const produtos = ref<any[]>([])
const categorias = ref<any[]>([])
const totais = ref<any>({ qtdProdutos: 0, qtdAbaixoMinimo: 0, custoTotalEstoque: 0, valorVendaTotalEstoque: 0 })
const busca = ref('')
const filtroCategoria = ref<string | null>(null)
const apenasAbaixoMinimo = ref(false)

const headers = [
  { title: 'Código', key: 'codigo', width: 110 },
  { title: 'Descrição', key: 'descricao', sortable: true },
  { title: 'Estoque Atual', key: 'estoqueAtual', width: 120 },
  { title: 'Mínimo', key: 'estoqueMinimo', width: 90 },
  { title: 'Custo Unit.', key: 'custoUnitario', width: 110 },
  { title: 'Preço Venda', key: 'precoVenda', width: 110 },
  { title: 'Custo Total', key: 'custoTotal', width: 120 },
  { title: 'Val. Venda', key: 'valorVendaTotal', width: 120 },
]

const produtosFiltrados = computed(() => {
  let lista = produtos.value
  if (busca.value) {
    const q = busca.value.toLowerCase()
    lista = lista.filter(p => p.descricao?.toLowerCase().includes(q) || p.codigo?.toLowerCase().includes(q))
  }
  if (filtroCategoria.value) lista = lista.filter(p => p.categoriaId === filtroCategoria.value)
  if (apenasAbaixoMinimo.value) lista = lista.filter(p => p.abaixoMinimo)
  return lista
})

function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }

async function carregar() {
  carregando.value = true
  try {
    const [pos, cat] = await Promise.all([
      api.get('/estoque/posicao', { params: { empresaId: auth.empresaId } }),
      api.get('/categorias', { params: { empresaId: auth.empresaId } }),
    ])
    produtos.value = pos.data.produtos ?? []
    totais.value = pos.data.totais ?? totais.value
    categorias.value = cat.data
  } finally { carregando.value = false }
}

onMounted(carregar)
</script>
