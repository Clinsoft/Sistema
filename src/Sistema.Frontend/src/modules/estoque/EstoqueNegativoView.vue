<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Estoque Negativo</h2></v-col>
      <v-col cols="auto">
        <v-btn variant="tonal" color="primary" :loading="carregando" prepend-icon="mdi-refresh" @click="carregar">
          Atualizar
        </v-btn>
      </v-col>
    </v-row>

    <v-alert type="warning" variant="tonal" density="comfortable" class="mb-4">
      Estoque negativo quase sempre significa <b>venda sem a entrada escriturada</b> (o produto foi
      vendido, mas a nota de compra não foi lançada no estoque) — ou <b>produto duplicado</b>.
      <div class="text-caption mt-1">
        Para corrigir: <b>escriture a NF-e de compra</b> (Fiscal → NF-e Recebidas) ou faça um
        <b>ajuste por contagem física</b> (Estoque → Ajuste de Estoque). A coluna “Teve entrada?”
        ajuda a identificar os que nunca foram escriturados.
      </div>
    </v-alert>

    <v-row dense class="mb-2">
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Produtos negativos</div>
          <div class="text-h5 font-weight-bold text-error">{{ itens.length }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Nunca escriturados</div>
          <div class="text-h5 font-weight-bold text-error">{{ semEntrada }}</div>
        </v-card>
      </v-col>
    </v-row>

    <div class="d-flex align-center flex-wrap gap-2 mb-2">
      <v-chip-group v-model="filtro" mandatory>
        <v-chip value="todos" filter variant="outlined">Todos</v-chip>
        <v-chip value="semEntrada" filter color="error" variant="outlined">Nunca escriturados</v-chip>
        <v-chip value="granel" filter color="teal" variant="outlined">Granel (kg)</v-chip>
        <v-chip value="un" filter variant="outlined">Unidade</v-chip>
      </v-chip-group>
      <v-spacer />
      <v-text-field v-model="busca" label="Buscar produto" prepend-inner-icon="mdi-magnify"
        variant="outlined" density="compact" hide-details clearable style="max-width:280px" />
    </div>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="itensFiltrados" :loading="carregando"
        density="compact" hover items-per-page="50"
        :sort-by="[{ key: 'estoqueAtual', order: 'asc' }]"
        no-data-text="Nenhum produto com estoque negativo. 🎉">
        <template #item.estoqueAtual="{ item }">
          <span class="text-error font-weight-bold">{{ fmtQtd(item.estoqueAtual) }} {{ item.unidadeSigla }}</span>
        </template>
        <template #item.tipo="{ item }">
          <v-chip size="x-small" label variant="tonal" :color="item.pesavel ? 'teal' : 'grey'">
            {{ item.pesavel ? 'granel' : 'un' }}
          </v-chip>
        </template>
        <template #item.temEntrada="{ item }">
          <v-chip size="x-small" label variant="tonal" :color="item.temEntrada ? 'success' : 'error'">
            {{ item.temEntrada ? 'sim' : 'nunca' }}
          </v-chip>
        </template>
        <template #item.codigoBarras="{ item }">{{ item.codigoBarras || '—' }}</template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

interface NegItem {
  id: string; codigo: string; descricao: string; estoqueAtual: number
  precoVenda: number; codigoBarras: string | null; unidadeSigla: string
  pesavel: boolean; temEntrada: boolean
}

const carregando = ref(true)
const itens = ref<NegItem[]>([])
const filtro = ref<'todos' | 'semEntrada' | 'granel' | 'un'>('todos')
const busca = ref('')

const headers = [
  { title: 'Código', key: 'codigo', width: 90 },
  { title: 'Produto', key: 'descricao' },
  { title: 'Estoque', key: 'estoqueAtual', align: 'end' as const },
  { title: 'Tipo', key: 'tipo', width: 90, align: 'center' as const },
  { title: 'Teve entrada?', key: 'temEntrada', width: 120, align: 'center' as const },
  { title: 'EAN', key: 'codigoBarras', width: 150 },
]

const fmtQtd = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 3 })

const semEntrada = computed(() => itens.value.filter(i => !i.temEntrada).length)

const itensFiltrados = computed(() => {
  let r = itens.value
  if (filtro.value === 'semEntrada') r = r.filter(i => !i.temEntrada)
  else if (filtro.value === 'granel') r = r.filter(i => i.pesavel)
  else if (filtro.value === 'un') r = r.filter(i => !i.pesavel)
  const q = busca.value?.trim().toLowerCase()
  if (q) r = r.filter(i => (i.descricao ?? '').toLowerCase().includes(q) || (i.codigo ?? '').includes(q))
  return r
})

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<{ itens: NegItem[] }>('/estoque/negativos', {
      params: { empresaId: auth.empresaId },
    })
    itens.value = res.data.itens ?? []
  } catch { itens.value = [] } finally { carregando.value = false }
}

onMounted(carregar)
</script>
