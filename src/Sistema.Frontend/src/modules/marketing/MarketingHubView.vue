<template>
  <div>
    <div class="d-flex align-center mb-3">
      <v-icon color="primary" class="mr-2">mdi-bullhorn-outline</v-icon>
      <div class="text-h6 font-weight-bold">Marketing</div>
    </div>

    <v-tabs v-model="aba" color="primary" density="comfortable" class="mb-4" show-arrows>
      <v-tab v-for="t in abasDisponiveis" :key="t.value" :value="t.value">
        <v-icon start size="18">{{ t.icon }}</v-icon>{{ t.label }}
      </v-tab>
    </v-tabs>

    <!-- Renderiza a aba ativa direto (sem v-window, que restringia o layout).
         keep-alive mantém a aba viva depois de visitada. -->
    <keep-alive>
      <MarketingView v-if="aba === 'artes'" />
      <ClubePromocoesView v-else-if="aba === 'clube'" />
      <PromocoesView v-else-if="aba === 'promocoes'" />
    </keep-alive>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import MarketingView from './MarketingView.vue'
import ClubePromocoesView from './ClubePromocoesView.vue'
import PromocoesView from './PromocoesView.vue'

const auth = useAuthStore()
const ehAtendente = computed(() => auth.usuario?.role === 'Atendente')

const todasAbas = [
  { value: 'artes', label: 'Artes para Redes', icon: 'mdi-image-multiple-outline', ver: () => true },
  { value: 'clube', label: 'Clube de Promoções', icon: 'mdi-star-circle-outline', ver: () => true },
  { value: 'promocoes', label: 'Promoções', icon: 'mdi-tag-multiple-outline', ver: () => !ehAtendente.value },
]
const abasDisponiveis = computed(() => todasAbas.filter(t => t.ver()))

const route = useRoute()
const router = useRouter()
function inicial() {
  const q = String(route.query.aba)
  if (abasDisponiveis.value.some(t => t.value === q)) return q
  return abasDisponiveis.value[0]?.value ?? 'artes'
}
const aba = ref<string>(inicial())
watch(aba, (v) => router.replace({ query: { ...route.query, aba: v } }))
</script>
