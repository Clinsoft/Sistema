<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Comparativo entre Lojas</h2></v-col>
    </v-row>

    <!-- Período -->
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
          <v-btn color="primary" variant="tonal" rounded="lg" :loading="carregando" @click="carregar">Comparar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <div v-if="!carregando && !lojas.length" class="text-center text-medium-emphasis pa-6">
      Sem vendas finalizadas no período.
    </div>

    <v-row dense>
      <v-col v-for="l in lojas" :key="l.localEstoqueId" cols="12" :md="lojas.length > 1 ? 6 : 12">
        <v-card rounded="xl" elevation="1" class="pa-4" height="100%">
          <div class="d-flex align-center mb-3">
            <v-icon icon="mdi-store-outline" color="primary" class="mr-2" />
            <span class="text-h6 font-weight-bold">{{ l.nome }}</span>
            <v-spacer />
            <v-chip v-if="l.crescimentoPct != null" size="small" label
              :color="l.crescimentoPct >= 0 ? 'success' : 'error'" variant="tonal">
              <v-icon start size="14">{{ l.crescimentoPct >= 0 ? 'mdi-trending-up' : 'mdi-trending-down' }}</v-icon>
              {{ l.crescimentoPct >= 0 ? '+' : '' }}{{ l.crescimentoPct.toFixed(1) }}% vs período anterior
            </v-chip>
          </div>

          <div class="text-caption text-medium-emphasis">Faturamento</div>
          <div class="text-h4 font-weight-bold mb-3">R$ {{ fmt(l.faturamento) }}</div>

          <v-row dense class="mb-2">
            <v-col cols="4">
              <div class="text-caption text-medium-emphasis">Nº de vendas</div>
              <div class="text-body-1 font-weight-bold">{{ l.numeroVendas }}</div>
            </v-col>
            <v-col cols="4">
              <div class="text-caption text-medium-emphasis">Ticket médio</div>
              <div class="text-body-1 font-weight-bold">R$ {{ fmt(l.ticketMedio) }}</div>
            </v-col>
            <v-col cols="4">
              <div class="text-caption text-medium-emphasis">Margem</div>
              <div class="text-body-1 font-weight-bold text-success">{{ l.margemPct.toFixed(1) }}%</div>
              <div class="text-caption text-success">R$ {{ fmt(l.margemValor) }}</div>
            </v-col>
          </v-row>

          <v-divider class="my-3" />
          <div class="text-caption font-weight-bold mb-1">Top 5 produtos (faturamento)</div>
          <v-list density="compact" class="pa-0">
            <v-list-item v-for="(p, i) in l.topProdutos" :key="i" class="px-0" min-height="30">
              <template #prepend>
                <span class="text-caption text-medium-emphasis mr-2" style="width:16px">{{ i + 1 }}.</span>
              </template>
              <v-list-item-title class="text-caption">{{ p.descricao }}</v-list-item-title>
              <template #append>
                <span class="text-caption font-weight-bold">R$ {{ fmt(p.total) }}</span>
              </template>
            </v-list-item>
            <v-list-item v-if="!l.topProdutos.length" class="px-0 text-caption text-medium-emphasis">
              Sem itens no período.
            </v-list-item>
          </v-list>
        </v-card>
      </v-col>
    </v-row>

    <!-- Totais consolidados (quando há mais de uma loja) -->
    <v-card v-if="lojas.length > 1" rounded="xl" elevation="1" class="mt-3 pa-4">
      <div class="text-body-2 font-weight-bold mb-1">Consolidado ({{ lojas.length }} lojas)</div>
      <div class="d-flex flex-wrap ga-6">
        <div><span class="text-caption text-medium-emphasis">Faturamento total: </span>
          <b>R$ {{ fmt(totalFat) }}</b></div>
        <div><span class="text-caption text-medium-emphasis">Vendas: </span><b>{{ totalVendas }}</b></div>
        <div><span class="text-caption text-medium-emphasis">Ticket médio geral: </span>
          <b>R$ {{ fmt(totalVendas ? totalFat / totalVendas : 0) }}</b></div>
      </div>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import FiltroMes from '@/components/FiltroMes.vue'
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

interface Loja {
  localEstoqueId: string; nome: string; faturamento: number; numeroVendas: number
  ticketMedio: number; margemValor: number; margemPct: number
  faturamentoAnterior: number; crescimentoPct: number | null
  topProdutos: { descricao: string; total: number; qtd: number }[]
}

const carregando = ref(true)
const lojas = ref<Loja[]>([])

const anoAtual = new Date().getFullYear()
const mesAtual = new Date().getMonth()
const filtros = ref({
  inicio: new Date(anoAtual, mesAtual, 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
})

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const totalFat = computed(() => lojas.value.reduce((s, l) => s + l.faturamento, 0))
const totalVendas = computed(() => lojas.value.reduce((s, l) => s + l.numeroVendas, 0))

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<{ lojas: Loja[] }>('/dashboard/comparativo-lojas', {
      params: { empresaId: auth.empresaId, inicio: filtros.value.inicio, fim: filtros.value.fim },
    })
    lojas.value = res.data.lojas ?? []
  } catch { lojas.value = [] } finally { carregando.value = false }
}

onMounted(carregar)
</script>
