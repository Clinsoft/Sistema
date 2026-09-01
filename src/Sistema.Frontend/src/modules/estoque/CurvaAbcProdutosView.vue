<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Curva ABC de Produtos</h2></v-col>
    </v-row>

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      A Curva ABC classifica os produtos pelo <b>quanto cada um fatura</b> (não pela quantidade):
      <b class="text-success">A</b> = os poucos itens que somam até <b>80%</b> do faturamento (os mais importantes) ·
      <b class="text-warning">B</b> = os próximos até <b>95%</b> ·
      <b class="text-error">C</b> = a cauda (95–100%), muitos itens que faturam pouco.
      <div class="text-caption mt-1">A <b>margem</b> é estimada (venda − custo <i>atual</i> do produto × qtd vendida); o <b>estoque atual</b> é o saldo de agora.</div>
    </v-alert>

    <!-- Filtros -->
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
        <v-col cols="12" sm="3" class="d-flex gap-2">
          <v-btn color="primary" variant="tonal" rounded="lg" :loading="carregando" @click="carregar">Buscar</v-btn>
          <v-btn variant="text" size="small" @click="anoTodo">Ano todo</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <!-- Resumo -->
    <v-row dense class="mb-2">
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3" @click="classe = 'A'" style="cursor:pointer"
          :variant="classe === 'A' ? 'tonal' : 'elevated'" :color="classe === 'A' ? 'success' : undefined">
          <div class="text-caption text-medium-emphasis">Classe A (até 80%)</div>
          <div class="text-h5 font-weight-bold text-success">{{ resumo.A.qtd }}</div>
          <div class="text-caption">{{ fmt(resumo.A.total) }} · {{ resumo.A.pct }}% do fat.</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3" @click="classe = 'B'" style="cursor:pointer"
          :variant="classe === 'B' ? 'tonal' : 'elevated'" :color="classe === 'B' ? 'warning' : undefined">
          <div class="text-caption text-medium-emphasis">Classe B (80–95%)</div>
          <div class="text-h5 font-weight-bold text-warning">{{ resumo.B.qtd }}</div>
          <div class="text-caption">{{ fmt(resumo.B.total) }} · {{ resumo.B.pct }}% do fat.</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3" @click="classe = 'C'" style="cursor:pointer"
          :variant="classe === 'C' ? 'tonal' : 'elevated'" :color="classe === 'C' ? 'error' : undefined">
          <div class="text-caption text-medium-emphasis">Classe C (95–100%)</div>
          <div class="text-h5 font-weight-bold text-error">{{ resumo.C.qtd }}</div>
          <div class="text-caption">{{ fmt(resumo.C.total) }} · {{ resumo.C.pct }}% do fat.</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3" @click="classe = 'Todos'" style="cursor:pointer"
          :variant="classe === 'Todos' ? 'tonal' : 'elevated'" :color="classe === 'Todos' ? 'primary' : undefined">
          <div class="text-caption text-medium-emphasis">Total (faturamento)</div>
          <div class="text-h5 font-weight-bold">{{ itens.length }}</div>
          <div class="text-caption">{{ fmt(totalGeral) }} no período</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Filtro por classe (chips) + busca -->
    <div class="d-flex align-center flex-wrap gap-2 mb-2">
      <v-chip-group v-model="classe" mandatory selected-class="text-white">
        <v-chip value="Todos" filter variant="outlined">Todos</v-chip>
        <v-chip value="A" filter color="success" variant="outlined">A</v-chip>
        <v-chip value="B" filter color="warning" variant="outlined">B</v-chip>
        <v-chip value="C" filter color="error" variant="outlined">C</v-chip>
      </v-chip-group>
      <v-spacer />
      <v-text-field v-model="busca" label="Buscar produto" prepend-inner-icon="mdi-magnify"
        variant="outlined" density="compact" hide-details clearable style="max-width:280px" />
    </div>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="itensFiltrados" :loading="carregando"
        density="compact" hover items-per-page="25"
        :sort-by="[{ key: 'totalVendido', order: 'desc' }]"
        no-data-text="Sem vendas finalizadas no período.">
        <template #item.posicao="{ index }">{{ index + 1 }}</template>
        <template #item.totalVendido="{ item }">R$ {{ fmt(item.totalVendido) }}</template>
        <template #item.qtdVendida="{ item }">{{ fmtQtd(item.qtdVendida) }}</template>
        <template #item.margemValor="{ item }">
          <span :class="item.margemValor >= 0 ? 'text-success' : 'text-error'">
            R$ {{ fmt(item.margemValor) }}
            <span class="text-caption">({{ (item.margemPct ?? 0).toFixed(1) }}%)</span>
          </span>
        </template>
        <template #item.estoqueAtual="{ item }">{{ fmtQtd(item.estoqueAtual) }}</template>
        <template #item.participacao="{ item }">{{ (item.participacao ?? 0).toFixed(2) }}%</template>
        <template #item.participacaoAcumulada="{ item }">{{ (item.participacaoAcumulada ?? 0).toFixed(2) }}%</template>
        <template #item.curva="{ item }">
          <v-chip size="small" label variant="tonal"
            :color="item.curva === 'A' ? 'success' : item.curva === 'B' ? 'warning' : 'error'">
            {{ item.curva }}
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

interface AbcItem {
  produtoId: string; descricao: string; totalVendido: number; qtdVendida: number
  participacao: number; participacaoAcumulada: number; curva: string
  estoqueAtual: number; custoUnitario: number; margemValor: number; margemPct: number
}

const carregando = ref(true)
const itens = ref<AbcItem[]>([])
const totalGeral = ref(0)
const classe = ref<'Todos' | 'A' | 'B' | 'C'>('Todos')
const busca = ref('')

const anoAtual = new Date().getFullYear()
const filtros = ref({
  inicio: `${anoAtual}-01-01`,
  fim: new Date().toISOString().slice(0, 10),
})

const headers = [
  { title: '#', key: 'posicao', width: 56, sortable: false },
  { title: 'Produto', key: 'descricao' },
  { title: 'Qtd vendida', key: 'qtdVendida', align: 'end' as const },
  { title: 'Total vendido', key: 'totalVendido', align: 'end' as const },
  { title: 'Margem', key: 'margemValor', align: 'end' as const },
  { title: 'Estoque atual', key: 'estoqueAtual', align: 'end' as const },
  { title: 'Participação', key: 'participacao', align: 'end' as const },
  { title: '% acumulada', key: 'participacaoAcumulada', align: 'end' as const },
  { title: 'Classe', key: 'curva', width: 90, align: 'center' as const },
]

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtQtd = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 3 })

const resumo = computed(() => {
  const g = { A: { qtd: 0, total: 0 }, B: { qtd: 0, total: 0 }, C: { qtd: 0, total: 0 } }
  for (const i of itens.value) {
    const k = i.curva as 'A' | 'B' | 'C'
    if (g[k]) { g[k].qtd++; g[k].total += i.totalVendido }
  }
  const pct = (t: number) => totalGeral.value > 0 ? Math.round(t / totalGeral.value * 100) : 0
  return {
    A: { ...g.A, pct: pct(g.A.total) },
    B: { ...g.B, pct: pct(g.B.total) },
    C: { ...g.C, pct: pct(g.C.total) },
  }
})

const itensFiltrados = computed(() => {
  let r = itens.value
  if (classe.value !== 'Todos') r = r.filter(i => i.curva === classe.value)
  const q = busca.value?.trim().toLowerCase()
  if (q) r = r.filter(i => (i.descricao ?? '').toLowerCase().includes(q))
  return r
})

function anoTodo() {
  filtros.value.inicio = `${anoAtual}-01-01`
  filtros.value.fim = new Date().toISOString().slice(0, 10)
  carregar()
}

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<{ itens: AbcItem[]; totalGeral: number }>('/estoque/curva-abc', {
      params: {
        empresaId: auth.empresaId,
        inicio: filtros.value.inicio,
        fim: filtros.value.fim,
        localEstoqueId: auth.lojaAtualId || undefined,
      },
    })
    itens.value = res.data.itens ?? []
    totalGeral.value = res.data.totalGeral ?? 0
  } catch {
    itens.value = []; totalGeral.value = 0
  } finally { carregando.value = false }
}

onMounted(carregar)
</script>
