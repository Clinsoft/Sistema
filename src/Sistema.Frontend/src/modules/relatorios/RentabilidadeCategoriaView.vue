<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Rentabilidade por Categoria</h2></v-col>
    </v-row>

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      Onde está o <b>lucro</b> — não só o faturamento. Margem por categoria no período
      (venda − custo). A margem usa o <b>custo atual</b> dos produtos (estimativa).
    </v-alert>

    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" sm="3">
          <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f; carregar() }" />
        </v-col>
        <v-col cols="6" sm="3">
          <v-text-field v-model="filtros.inicio" label="Início" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="6" sm="3">
          <v-text-field v-model="filtros.fim" label="Fim" type="date"
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
          <div class="text-caption text-medium-emphasis">Faturamento</div>
          <div class="text-h5 font-weight-bold">R$ {{ fmt(totalFaturamento) }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Margem total</div>
          <div class="text-h5 font-weight-bold text-success">R$ {{ fmt(margemTotal) }}</div>
          <div class="text-caption text-success">{{ margemPctGeral.toFixed(1) }}% do faturamento</div>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="itens" :loading="carregando"
        density="compact" hover items-per-page="50"
        :sort-by="[{ key: 'margemValor', order: 'desc' }]"
        no-data-text="Sem vendas no período.">
        <template #item.faturamento="{ item }">R$ {{ fmt(item.faturamento) }}</template>
        <template #item.participacaoPct="{ item }">{{ item.participacaoPct.toFixed(1) }}%</template>
        <template #item.custo="{ item }">R$ {{ fmt(item.custo) }}</template>
        <template #item.margemValor="{ item }">
          <span class="font-weight-bold" :class="item.margemValor >= 0 ? 'text-success' : 'text-error'">
            R$ {{ fmt(item.margemValor) }}
          </span>
        </template>
        <template #item.margemPct="{ item }">
          <v-chip size="small" label variant="tonal"
            :color="item.margemPct >= 40 ? 'success' : item.margemPct >= 20 ? 'warning' : 'error'">
            {{ item.margemPct.toFixed(1) }}%
          </v-chip>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import FiltroMes from '@/components/FiltroMes.vue'
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

interface Item {
  categoria: string; faturamento: number; custo: number
  margemValor: number; margemPct: number; participacaoPct: number; quantidade: number
}

const carregando = ref(true)
const itens = ref<Item[]>([])
const totalFaturamento = ref(0)

const anoAtual = new Date().getFullYear()
const mesAtual = new Date().getMonth()
const filtros = ref({
  inicio: new Date(anoAtual, mesAtual, 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
})

const headers = [
  { title: 'Categoria', key: 'categoria' },
  { title: 'Faturamento', key: 'faturamento', align: 'end' as const },
  { title: 'Part.', key: 'participacaoPct', align: 'end' as const },
  { title: 'Custo', key: 'custo', align: 'end' as const },
  { title: 'Margem (R$)', key: 'margemValor', align: 'end' as const },
  { title: 'Margem %', key: 'margemPct', width: 110, align: 'center' as const },
]

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const margemTotal = computed(() => itens.value.reduce((s, i) => s + i.margemValor, 0))
const margemPctGeral = computed(() => totalFaturamento.value > 0 ? margemTotal.value / totalFaturamento.value * 100 : 0)

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<{ itens: Item[]; totalFaturamento: number }>('/relatorios/estoque/rentabilidade-categoria', {
      params: { empresaId: auth.empresaId, inicio: filtros.value.inicio, fim: filtros.value.fim },
    })
    itens.value = res.data.itens ?? []
    totalFaturamento.value = res.data.totalFaturamento ?? 0
  } catch { itens.value = []; totalFaturamento.value = 0 } finally { carregando.value = false }
}

onMounted(carregar)
</script>
