<template>
  <div>
    <div class="d-flex align-center mb-3">
      <v-icon color="primary" class="mr-2">mdi-clipboard-list-outline</v-icon>
      <div class="text-h6 font-weight-bold">Relatórios de Estoque</div>
    </div>

    <v-tabs v-model="aba" color="primary" density="comfortable" class="mb-4" show-arrows>
      <v-tab v-for="t in abasDisponiveis" :key="t.value" :value="t.value">
        <v-icon start size="18">{{ t.icon }}</v-icon>{{ t.label }}
      </v-tab>
    </v-tabs>

    <!-- Renderiza a aba ativa direto (sem v-window, que restringia o layout).
         keep-alive mantém a aba viva depois de visitada. -->
    <keep-alive>
      <PosicaoEstoqueView v-if="aba === 'posicao'" />
      <CurvaAbcProdutosView v-else-if="aba === 'curva-abc'" />
      <EstoqueNegativoView v-else-if="aba === 'negativos'" />
      <ProdutosParadosView v-else-if="aba === 'produtos-parados'" />
      <PerdasValidadeView v-else-if="aba === 'perdas-validade'" />
      <MovimentacoesView v-else-if="aba === 'movimentacoes'" />
    </keep-alive>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import PosicaoEstoqueView from './PosicaoEstoqueView.vue'
import CurvaAbcProdutosView from './CurvaAbcProdutosView.vue'
import EstoqueNegativoView from './EstoqueNegativoView.vue'
import ProdutosParadosView from './ProdutosParadosView.vue'
import PerdasValidadeView from './PerdasValidadeView.vue'
import MovimentacoesView from './MovimentacoesView.vue'

const auth = useAuthStore()
const ehAtendente = computed(() => auth.usuario?.role === 'Atendente')
const ehGestor = computed(() => auth.usuario?.role === 'Administrador' || auth.usuario?.role === 'Gerente')

// Visibilidade por papel espelhando as regras que os itens tinham no menu
const todasAbas = [
  { value: 'posicao', label: 'Posição', icon: 'mdi-list-status', ver: () => !ehAtendente.value },
  { value: 'curva-abc', label: 'Curva ABC', icon: 'mdi-chart-bar-stacked', ver: () => !ehAtendente.value },
  { value: 'negativos', label: 'Estoque Negativo', icon: 'mdi-alert-octagon-outline', ver: () => !ehAtendente.value },
  { value: 'produtos-parados', label: 'Produtos Parados', icon: 'mdi-timer-sand-empty', ver: () => ehGestor.value },
  { value: 'perdas-validade', label: 'Perdas por Validade', icon: 'mdi-cash-remove', ver: () => true },
  { value: 'movimentacoes', label: 'Movimentações', icon: 'mdi-swap-horizontal', ver: () => !ehAtendente.value },
]
const abasDisponiveis = computed(() => todasAbas.filter(t => t.ver()))

const route = useRoute()
const router = useRouter()
function inicial() {
  const q = String(route.query.aba)
  if (abasDisponiveis.value.some(t => t.value === q)) return q
  return abasDisponiveis.value[0]?.value ?? 'perdas-validade'
}
const aba = ref<string>(inicial())
watch(aba, (v) => router.replace({ query: { ...route.query, aba: v } }))
</script>
