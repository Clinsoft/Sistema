<template>
  <div>
    <div class="text-h6 font-weight-bold mb-1">Perdas por Validade</div>
    <div class="text-caption text-medium-emphasis mb-4">
      Baixas de produtos vencidos (descarte, devolução, uso interno) e o prejuízo por descarte.
    </div>

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="6" sm="3">
          <v-text-field v-model="inicio" type="date" label="Início" variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="6" sm="3">
          <v-text-field v-model="fim" type="date" label="Fim" variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col v-if="!ehAtendente" cols="6" sm="3">
          <v-select v-model="filtroLoja" :items="lojasFiltro" item-title="nome" item-value="id"
            label="Loja" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="6" sm="3">
          <v-select v-model="filtroDestino" :items="destinosFiltro" item-title="label" item-value="value"
            label="Destino" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" sm="auto">
          <v-btn color="primary" variant="tonal" prepend-icon="mdi-magnify" :loading="carregando" @click="carregar">Gerar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <div v-if="carregando" class="d-flex justify-center pa-8"><v-progress-circular indeterminate color="primary" /></div>

    <template v-else-if="dados">
      <!-- Cards -->
      <v-row class="mb-1">
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1"><v-card-text class="pa-3">
            <div class="d-flex align-center gap-1 mb-1"><v-icon icon="mdi-cash-remove" color="error" size="15" /><span class="text-caption text-medium-emphasis">Perda financeira (descarte)</span></div>
            <div class="text-h6 font-weight-bold text-error">R$ {{ fmt(dados.resumo.perdaFinanceira) }}</div>
          </v-card-text></v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1"><v-card-text class="pa-3">
            <div class="d-flex align-center gap-1 mb-1"><v-icon icon="mdi-package-variant-closed-remove" color="brown" size="15" /><span class="text-caption text-medium-emphasis">Custo total baixado</span></div>
            <div class="text-h6 font-weight-bold text-brown">R$ {{ fmt(dados.resumo.total) }}</div>
          </v-card-text></v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1"><v-card-text class="pa-3">
            <div class="d-flex align-center gap-1 mb-1"><v-icon icon="mdi-scale" color="teal" size="15" /><span class="text-caption text-medium-emphasis">Qtd. baixada</span></div>
            <div class="text-h6 font-weight-bold text-teal">{{ fmt(dados.resumo.quantidade) }}</div>
          </v-card-text></v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1"><v-card-text class="pa-3">
            <div class="d-flex align-center gap-1 mb-1"><v-icon icon="mdi-format-list-numbered" color="grey-darken-1" size="15" /><span class="text-caption text-medium-emphasis">Registros</span></div>
            <div class="text-h6 font-weight-bold">{{ dados.resumo.registros }}</div>
          </v-card-text></v-card>
        </v-col>
      </v-row>

      <!-- Por destino -->
      <v-card v-if="dados.resumo.porDestino.length" rounded="xl" elevation="1" class="mb-4">
        <v-card-title class="text-body-1 font-weight-bold pa-4 pb-2">Por destino</v-card-title>
        <v-table density="comfortable">
          <thead><tr><th>Destino</th><th class="text-right">Qtd.</th><th class="text-right">Custo</th></tr></thead>
          <tbody>
            <tr v-for="d in dados.resumo.porDestino" :key="d.destino">
              <td>{{ d.destino }}</td>
              <td class="text-right">{{ fmt(d.quantidade) }}</td>
              <td class="text-right">R$ {{ fmt(d.valor) }}</td>
            </tr>
          </tbody>
        </v-table>
      </v-card>

      <!-- Detalhe -->
      <v-card rounded="xl" elevation="1">
        <v-card-title class="text-body-1 font-weight-bold pa-4 pb-2">Detalhamento</v-card-title>
        <v-data-table :headers="headers" :items="dados.itens" density="compact" items-per-page="25">
          <template #item.data="{ item }">{{ dataBr(item.data) }}</template>
          <template #item.destinoNome="{ item }">
            <v-chip size="x-small" :color="corDestino(item.destino)" variant="tonal">{{ item.destinoNome }}</v-chip>
          </template>
          <template #item.custoTotal="{ item }">R$ {{ fmt(item.custoTotal) }}</template>
          <template #no-data><div class="text-center text-medium-emphasis pa-6">Nenhuma baixa por validade no período.</div></template>
        </v-data-table>
      </v-card>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const ehAtendente = computed(() => auth.usuario?.role === 'Atendente')
const carregando = ref(false)
const dados = ref<any>(null)
const locais = ref<any[]>([])
const filtroLoja = ref<string | null>(null)
const filtroDestino = ref<string | null>(null)

const hoje = new Date()
const inicio = ref(new Date(hoje.getFullYear(), hoje.getMonth(), 1).toISOString().slice(0, 10))
const fim = ref(hoje.toISOString().slice(0, 10))

const lojasFiltro = computed(() => [{ id: null, nome: 'Todas as lojas' }, ...locais.value])
const destinosFiltro = [
  { label: 'Todos', value: null },
  { label: 'Descarte / Perda', value: 'Descarte' },
  { label: 'Devolução ao fornecedor', value: 'Devolucao' },
  { label: 'Uso interno / reprocesso', value: 'UsoInterno' },
]
const headers = [
  { title: 'Data', key: 'data', width: 100 },
  { title: 'Produto', key: 'produto' },
  { title: 'Loja', key: 'loja' },
  { title: 'Destino', key: 'destinoNome' },
  { title: 'Qtd.', key: 'quantidade', width: 70 },
  { title: 'Custo total', key: 'custoTotal', width: 120, align: 'end' },
  { title: 'Por', key: 'usuario' },
]

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const dataBr = (iso: string) => new Date(iso).toLocaleDateString('pt-BR')
const corDestino = (d: string) => d === 'Descarte' ? 'error' : d === 'Devolucao' ? 'blue' : 'grey'

async function carregar() {
  carregando.value = true
  try {
    const res = await api.get('/lotes/perdas', {
      params: {
        empresaId: auth.empresaId, inicio: inicio.value, fim: fim.value,
        localEstoqueId: filtroLoja.value || undefined, destino: filtroDestino.value || undefined,
      },
    })
    dados.value = res.data
  } finally { carregando.value = false }
}

onMounted(async () => {
  try { locais.value = (await api.get('/locais-estoque', { params: { empresaId: auth.empresaId } })).data } catch { /* ignore */ }
  await carregar()
})
</script>
