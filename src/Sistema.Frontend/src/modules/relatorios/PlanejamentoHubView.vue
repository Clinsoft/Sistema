<template>
  <div>
    <div class="d-flex align-center mb-3">
      <v-icon color="primary" class="mr-2">mdi-calendar-check-outline</v-icon>
      <div class="text-h6 font-weight-bold">Planejamento &amp; Metas</div>
    </div>

    <v-tabs v-model="aba" color="primary" density="comfortable" class="mb-4">
      <v-tab value="planejamento"><v-icon start size="18">mdi-calendar-month-outline</v-icon>Planejamento Anual</v-tab>
      <v-tab value="projecao"><v-icon start size="18">mdi-speedometer</v-icon>Projeção de Meta</v-tab>
    </v-tabs>

    <!-- Renderiza a aba ativa direto (sem v-window, que restringia o layout).
         keep-alive mantém a aba viva depois de visitada. -->
    <keep-alive>
      <PlanejamentoAnualView v-if="aba === 'planejamento'" />
      <ProjecaoMetaView v-else-if="aba === 'projecao'" />
    </keep-alive>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PlanejamentoAnualView from './PlanejamentoAnualView.vue'
import ProjecaoMetaView from './ProjecaoMetaView.vue'

const route = useRoute()
const router = useRouter()
const abas = ['planejamento', 'projecao']
const aba = ref<string>(abas.includes(String(route.query.aba)) ? String(route.query.aba) : 'planejamento')

watch(aba, (v) => router.replace({ query: { ...route.query, aba: v } }))
</script>
