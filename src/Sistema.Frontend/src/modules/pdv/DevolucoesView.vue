<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Devoluções de Venda</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-history" to="/pdv/vendas">Ver Vendas</v-btn>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" sm="3">
          <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f }" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.inicio" label="De" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.fim" label="Até" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" @click="carregar" :loading="carregando">Buscar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <!-- Totais -->
    <v-row v-if="devolucoes.length" class="mb-4">
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-warning">{{ devolucoes.length }}</div>
          <div class="text-caption text-medium-emphasis">Devoluções</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-error">R$ {{ fmt(totalDevolvido) }}</div>
          <div class="text-caption text-medium-emphasis">Total Devolvido</div>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="devolucoes" :loading="carregando"
        density="compact" hover items-per-page="20">
        <template #item.dataHora="{ item }">
          {{ new Date(item.dataHora).toLocaleString('pt-BR') }}
        </template>
        <template #item.totalDevolvido="{ item }">
          <span class="font-weight-medium text-warning">R$ {{ fmt(item.totalDevolvido) }}</span>
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
const carregando = ref(false)
const devolucoes = ref<any[]>([])

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
})

const headers = [
  { title: 'Venda Nº', key: 'numeroVenda', width: 110 },
  { title: 'Data', key: 'dataHora', width: 160 },
  { title: 'Cliente', key: 'clienteNome' },
  { title: 'Motivo', key: 'motivo' },
  { title: 'Total Devolvido', key: 'totalDevolvido', width: 150 },
]

const totalDevolvido = computed(() =>
  devolucoes.value.reduce((s, d) => s + (d.totalDevolvido ?? 0), 0)
)

function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/devolucoes', {
      params: { empresaId: auth.empresaId, ...filtros.value },
    })
    devolucoes.value = r.data ?? []
  } finally { carregando.value = false }
}

onMounted(carregar)
</script>
