<template>
  <div>
    <div class="text-h6 font-weight-bold mb-4">Estoque</div>

    <!-- Cards com dados reais -->
    <v-row>
      <v-col v-for="c in cards" :key="c.label" cols="6" sm="3">
        <v-card rounded="xl" elevation="1" :to="c.to" hover>
          <v-card-text class="d-flex align-center pa-4">
            <v-avatar :color="c.cor" size="44" class="mr-3">
              <v-icon :icon="c.icon" color="white" />
            </v-avatar>
            <div>
              <div v-if="carregando" class="text-h6">
                <v-progress-circular size="20" width="2" indeterminate />
              </div>
              <div v-else class="text-h5 font-weight-bold">{{ c.valor }}</div>
              <div class="text-caption text-medium-emphasis">{{ c.label }}</div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Alertas de vencimento -->
    <v-card v-if="vencimentos.length" rounded="xl" elevation="1" class="mt-4">
      <v-card-title class="pa-4 pb-2 d-flex align-center">
        <v-icon icon="mdi-alert-circle-outline" color="warning" class="mr-2" />
        Lotes com Vencimento Próximo (30 dias)
        <v-spacer />
        <v-btn variant="text" size="small" to="/estoque/lotes">Ver todos</v-btn>
      </v-card-title>
      <v-list density="compact" class="pa-0">
        <v-list-item v-for="lote in vencimentos.slice(0,5)" :key="lote.id"
          :subtitle="`Lote ${lote.numeroLote} — ${lote.quantidade} unidades`"
          :base-color="lote.vencido ? 'error' : 'warning'">
          <template #title>
            <span class="text-body-2">{{ lote.numeroProduto ?? 'Produto' }}</span>
          </template>
          <template #append>
            <v-chip size="x-small" :color="lote.vencido ? 'error' : 'warning'" variant="tonal">
              {{ lote.vencido ? 'VENCIDO' : fmtData(lote.dataValidade) }}
            </v-chip>
          </template>
        </v-list-item>
      </v-list>
    </v-card>

    <!-- Atalhos -->
    <v-card rounded="xl" elevation="1" class="mt-4">
      <v-card-title class="pa-4 pb-0 text-body-1 font-weight-bold">Módulos</v-card-title>
      <v-row class="pa-4" dense>
        <v-col v-for="a in atalhos" :key="a.to" cols="12" sm="6" md="4">
          <v-card :to="a.to" rounded="lg" variant="tonal" :color="a.cor" hover class="pa-3 d-flex align-center">
            <v-icon :icon="a.icon" class="mr-3" />
            <span class="text-body-2 font-weight-medium">{{ a.label }}</span>
          </v-card>
        </v-col>
      </v-row>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const carregando = ref(false)
const posicao = ref<any>({ totais: { qtdProdutos: 0, qtdAbaixoMinimo: 0, custoTotalEstoque: 0 } })
const vencimentos = ref<any[]>([])

const cards = computed(() => [
  { label: 'Produtos ativos', valor: posicao.value.totais?.qtdProdutos ?? '--',
    icon: 'mdi-package-variant-closed', cor: 'primary', to: '/estoque/produtos' },
  { label: 'Abaixo do mínimo', valor: posicao.value.totais?.qtdAbaixoMinimo ?? '--',
    icon: 'mdi-alert-outline', cor: 'warning', to: '/estoque/posicao' },
  { label: 'Lotes vencidos/próximos', valor: vencimentos.value.length,
    icon: 'mdi-calendar-alert', cor: 'error', to: '/estoque/lotes' },
  { label: 'Custo total estoque', valor: fmtVal(posicao.value.totais?.custoTotalEstoque ?? 0),
    icon: 'mdi-currency-brl', cor: 'success', to: '/estoque/posicao' },
])

const atalhos = [
  { icon: 'mdi-package-variant-closed', label: 'Produtos',              to: '/estoque/produtos',       cor: 'primary' },
  { icon: 'mdi-swap-horizontal',        label: 'Movimentações',         to: '/estoque/movimentacoes',  cor: 'secondary' },
  { icon: 'mdi-list-status',           label: 'Posição de Estoque',    to: '/estoque/posicao',        cor: 'info' },
  { icon: 'mdi-package-variant',       label: 'Lotes e Validades',     to: '/estoque/lotes',          cor: 'warning' },
  { icon: 'mdi-transfer',              label: 'Transferências',        to: '/estoque/transferencias', cor: 'success' },
  { icon: 'mdi-tune-vertical',         label: 'Ajuste em Lote',       to: '/estoque/ajuste',         cor: 'error' },
]

function fmtVal(v: number) {
  if (v >= 1000) return 'R$ ' + (v / 1000).toFixed(1) + 'K'
  return 'R$ ' + v.toFixed(0)
}

function fmtData(d: string) {
  return d ? new Date(d + 'T12:00:00').toLocaleDateString('pt-BR') : ''
}

async function carregar() {
  carregando.value = true
  try {
    const [pos, venc] = await Promise.all([
      api.get('/estoque/posicao', { params: { empresaId: auth.empresaId } }),
      api.get('/lotes/vencimentos', { params: { empresaId: auth.empresaId, diasAlerta: 30 } }),
    ])
    posicao.value = pos.data
    vencimentos.value = venc.data
  } catch { /* silencioso no dashboard */ }
  finally { carregando.value = false }
}

onMounted(carregar)
</script>
