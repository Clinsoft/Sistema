<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Produtos Parados</h2></v-col>
    </v-row>

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      Produtos com <b>estoque</b> mas <b>sem venda</b> no período — <b>capital travado</b> na prateleira.
      Ordenados pelo valor em estoque. Considere <b>promoção/liquidação</b> (cruze com a validade).
    </v-alert>

    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="6" sm="3">
          <v-select v-model.number="dias" :items="[60,90,120,180]" label="Sem vender há (dias)"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-btn color="primary" variant="tonal" rounded="lg" :loading="carregando" @click="carregar">Buscar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <v-row dense class="mb-2">
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Produtos parados</div>
          <div class="text-h5 font-weight-bold">{{ itens.length }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3" color="warning" variant="tonal">
          <div class="text-caption text-medium-emphasis">Capital parado</div>
          <div class="text-h5 font-weight-bold">R$ {{ fmt(capitalParado) }}</div>
        </v-card>
      </v-col>
    </v-row>

    <div class="d-flex align-center flex-wrap gap-2 mb-2">
      <v-chip-group v-model="filtro" mandatory>
        <v-chip value="todos" filter variant="outlined">Todos</v-chip>
        <v-chip value="nuncaVendeu" filter color="error" variant="outlined">Nunca vendeu</v-chip>
      </v-chip-group>
      <v-spacer />
      <v-text-field v-model="busca" label="Buscar produto" prepend-inner-icon="mdi-magnify"
        variant="outlined" density="compact" hide-details clearable style="max-width:280px" />
    </div>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="itensFiltrados" :loading="carregando"
        density="compact" hover items-per-page="50"
        :sort-by="[{ key: 'valorEstoque', order: 'desc' }]"
        no-data-text="Nenhum produto parado nesse período. 🎉">
        <template #item.estoqueAtual="{ item }">
          <div v-for="l in item.porLoja" :key="l.localEstoqueId" class="text-caption text-end"
            :class="l.saldo < 0 ? 'text-error' : ''">
            {{ l.nome }}: <b>{{ fmtQtd(l.saldo) }}</b>
          </div>
          <div class="text-caption text-medium-emphasis text-end">Total: {{ fmtQtd(item.estoqueAtual) }}</div>
        </template>
        <template #item.valorEstoque="{ item }">R$ {{ fmt(item.valorEstoque) }}</template>
        <template #item.ultimaVenda="{ item }">
          <span v-if="item.nuncaVendeu" class="text-error">nunca vendeu</span>
          <span v-else>{{ fmtData(item.ultimaVenda) }}</span>
        </template>
        <template #item.diasSemVender="{ item }">
          <span v-if="item.diasSemVender == null" class="text-medium-emphasis">—</span>
          <span v-else class="font-weight-bold" :class="item.diasSemVender >= 180 ? 'text-error' : 'text-warning'">
            {{ item.diasSemVender }} d
          </span>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

interface Item {
  id: string; codigo: string; descricao: string; estoqueAtual: number; custoUnitario: number
  valorEstoque: number; ultimaVenda: string | null; diasSemVender: number | null; nuncaVendeu: boolean
  porLoja: { localEstoqueId: string; nome: string; saldo: number }[]
}

const carregando = ref(true)
const itens = ref<Item[]>([])
const capitalParado = ref(0)
const dias = ref(90)
const filtro = ref<'todos' | 'nuncaVendeu'>('todos')
const busca = ref('')

const headers = [
  { title: 'Cód', key: 'codigo', width: 80 },
  { title: 'Produto', key: 'descricao' },
  { title: 'Estoque por loja', key: 'estoqueAtual', align: 'end' as const, width: 180 },
  { title: 'Valor em estoque', key: 'valorEstoque', align: 'end' as const },
  { title: 'Última venda', key: 'ultimaVenda', width: 130 },
  { title: 'Parado há', key: 'diasSemVender', width: 110, align: 'end' as const },
]

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtQtd = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 3 })
const fmtData = (v: string | null) => v ? new Date(v).toLocaleDateString('pt-BR') : '—'

const itensFiltrados = computed(() => {
  let r = itens.value
  if (filtro.value === 'nuncaVendeu') r = r.filter(i => i.nuncaVendeu)
  const q = busca.value?.trim().toLowerCase()
  if (q) r = r.filter(i => (i.descricao ?? '').toLowerCase().includes(q) || (i.codigo ?? '').includes(q))
  return r
})

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<{ itens: Item[]; capitalParado: number }>('/relatorios/estoque/produtos-parados', {
      params: { empresaId: auth.empresaId, dias: dias.value },
    })
    itens.value = res.data.itens ?? []
    capitalParado.value = res.data.capitalParado ?? 0
  } catch { itens.value = []; capitalParado.value = 0 } finally { carregando.value = false }
}

onMounted(carregar)
</script>
