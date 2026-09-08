<template>
  <div>
    <div class="d-flex align-center mb-3">
      <v-icon color="primary" class="mr-2">mdi-chart-line</v-icon>
      <div class="text-h6 font-weight-bold">DRE — Demonstrativo de Resultados</div>
    </div>

    <v-tabs v-model="aba" color="primary" density="comfortable" class="mb-4">
      <v-tab value="periodo"><v-icon start size="18">mdi-file-chart-outline</v-icon>Por período</v-tab>
      <v-tab value="mensal"><v-icon start size="18">mdi-chart-timeline-variant</v-icon>Evolução mensal</v-tab>
      <v-tab value="lojas"><v-icon start size="18">mdi-store-outline</v-icon>Por loja</v-tab>
    </v-tabs>

    <v-window v-model="aba">
      <v-window-item value="periodo"><DreView /></v-window-item>
      <v-window-item value="mensal"><DreMensalView /></v-window-item>
      <v-window-item value="lojas"><DreLojasView /></v-window-item>
    </v-window>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import DreView from './DreView.vue'
import DreMensalView from './DreMensalView.vue'
import DreLojasView from './DreLojasView.vue'

const route = useRoute()
const router = useRouter()
const abas = ['periodo', 'mensal', 'lojas']
const aba = ref<string>(abas.includes(String(route.query.aba)) ? String(route.query.aba) : 'periodo')

// Reflete a aba na URL (deep link: /financeiro/dre?aba=lojas)
watch(aba, (v) => router.replace({ query: { ...route.query, aba: v } }))
</script>
