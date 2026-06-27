<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">DRE — Demonstrativo de Resultados</div>
      <v-btn-toggle v-model="modo" mandatory density="compact" rounded="lg" class="mr-2">
        <v-btn value="mensal">Mensal</v-btn>
        <v-btn value="anual">Anual</v-btn>
      </v-btn-toggle>
      <v-btn color="primary" variant="tonal" prepend-icon="mdi-magnify"
        :loading="carregando" @click="carregar">Gerar</v-btn>
    </div>

    <!-- Seletor de período -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col v-if="modo === 'mensal'" cols="6" sm="2">
          <v-select v-model.number="mes" label="Mês"
            :items="meses" item-title="label" item-value="value"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="6" sm="2">
          <v-text-field v-model.number="ano" label="Ano"
            type="number" variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col v-if="dre" cols="12" sm="auto" class="ml-auto">
          <span class="text-caption text-medium-emphasis">
            {{ modo === 'mensal' ? `${meses.find(m => m.value === mes)?.label} / ${ano}` : `Exercício ${ano}` }}
          </span>
        </v-col>
      </v-row>
    </v-card>

    <!-- DRE -->
    <v-card v-if="dre && !carregando" rounded="xl" elevation="1">
      <v-list class="pa-2">

        <!-- RECEBIMENTOS -->
        <v-list-subheader class="dre-grupo-header text-success">
          <v-icon size="14" class="mr-1">mdi-arrow-down-circle-outline</v-icon>
          RECEBIMENTOS
        </v-list-subheader>
        <v-list-item v-for="sub in dre.subcategorias?.recebimentos ?? []" :key="sub.nome"
          :title="sub.nome" class="dre-sublinha">
          <template #append>
            <span class="text-success text-body-2">R$ {{ fmt(sub.total) }}</span>
          </template>
        </v-list-item>
        <v-list-item class="dre-total-linha">
          <template #title><span class="font-weight-bold">(+) Total Recebimentos</span></template>
          <template #append>
            <span class="font-weight-bold text-success text-body-1">R$ {{ fmt(dre.recebimentos) }}</span>
          </template>
        </v-list-item>

        <v-divider class="my-2" />

        <!-- DESPESAS FIXAS -->
        <v-list-subheader class="dre-grupo-header text-deep-purple">
          <v-icon size="14" class="mr-1">mdi-home-city-outline</v-icon>
          DESPESAS FIXAS
        </v-list-subheader>
        <v-list-item v-for="sub in dre.subcategorias?.despesasFixas ?? []" :key="sub.nome"
          :title="sub.nome" class="dre-sublinha">
          <template #append>
            <span class="text-deep-purple text-body-2">- R$ {{ fmt(sub.total) }}</span>
          </template>
        </v-list-item>
        <v-list-item class="dre-total-linha">
          <template #title><span class="font-weight-bold">(-) Despesas Fixas</span></template>
          <template #append>
            <span class="font-weight-bold text-deep-purple text-body-1">- R$ {{ fmt(dre.despesasFixas) }}</span>
          </template>
        </v-list-item>

        <v-divider class="my-1" />

        <!-- DESPESAS VARIÁVEIS -->
        <v-list-subheader class="dre-grupo-header text-orange">
          <v-icon size="14" class="mr-1">mdi-chart-bell-curve-cumulative</v-icon>
          DESPESAS VARIÁVEIS
        </v-list-subheader>
        <v-list-item v-for="sub in dre.subcategorias?.despesasVariaveis ?? []" :key="sub.nome"
          :title="sub.nome" class="dre-sublinha">
          <template #append>
            <span class="text-orange text-body-2">- R$ {{ fmt(sub.total) }}</span>
          </template>
        </v-list-item>
        <v-list-item class="dre-total-linha">
          <template #title><span class="font-weight-bold">(-) Despesas Variáveis</span></template>
          <template #append>
            <span class="font-weight-bold text-orange text-body-1">- R$ {{ fmt(dre.despesasVariaveis) }}</span>
          </template>
        </v-list-item>

        <v-divider class="my-1" />

        <!-- PESSOAS -->
        <v-list-subheader class="dre-grupo-header text-blue">
          <v-icon size="14" class="mr-1">mdi-account-group-outline</v-icon>
          PESSOAS
        </v-list-subheader>
        <v-list-item v-for="sub in dre.subcategorias?.pessoas ?? []" :key="sub.nome"
          :title="sub.nome" class="dre-sublinha">
          <template #append>
            <span class="text-blue text-body-2">- R$ {{ fmt(sub.total) }}</span>
          </template>
        </v-list-item>
        <v-list-item class="dre-total-linha">
          <template #title><span class="font-weight-bold">(-) Pessoas</span></template>
          <template #append>
            <span class="font-weight-bold text-blue text-body-1">- R$ {{ fmt(dre.pessoas) }}</span>
          </template>
        </v-list-item>

        <v-divider class="my-1" />

        <!-- IMPOSTOS -->
        <v-list-subheader class="dre-grupo-header text-error">
          <v-icon size="14" class="mr-1">mdi-gavel</v-icon>
          IMPOSTOS
        </v-list-subheader>
        <v-list-item v-for="sub in dre.subcategorias?.impostos ?? []" :key="sub.nome"
          :title="sub.nome" class="dre-sublinha">
          <template #append>
            <span class="text-error text-body-2">- R$ {{ fmt(sub.total) }}</span>
          </template>
        </v-list-item>
        <v-list-item class="dre-total-linha">
          <template #title><span class="font-weight-bold">(-) Impostos</span></template>
          <template #append>
            <span class="font-weight-bold text-error text-body-1">- R$ {{ fmt(dre.impostos) }}</span>
          </template>
        </v-list-item>

        <v-divider class="my-2" style="border-width:2px" />

        <!-- RESULTADO LÍQUIDO -->
        <v-list-item class="dre-resultado-linha">
          <template #title>
            <span class="text-h6 font-weight-bold">(=) Resultado Líquido</span>
          </template>
          <template #append>
            <div class="d-flex align-center gap-3">
              <v-chip
                :color="dre.resultado >= 0 ? 'success' : 'error'"
                variant="tonal" size="small">
                {{ pct(dre.margemLiquida) }}%
              </v-chip>
              <span class="text-h6 font-weight-bold"
                :class="dre.resultado >= 0 ? 'text-success' : 'text-error'">
                R$ {{ fmt(dre.resultado) }}
              </span>
            </div>
          </template>
        </v-list-item>

      </v-list>
    </v-card>

    <!-- Cards de resumo quando carregado -->
    <v-row v-if="dre && !carregando" class="mt-4">
      <v-col v-for="c in cardsResumo" :key="c.label" cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="d-flex align-center gap-1 mb-1">
              <v-icon :icon="c.icon" :color="c.cor" size="15" />
              <span class="text-caption text-medium-emphasis">{{ c.label }}</span>
            </div>
            <div class="text-h6 font-weight-bold" :class="`text-${c.cor}`">R$ {{ fmt(c.valor) }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <div v-if="carregando" class="d-flex justify-center pa-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <v-card v-if="!dre && !carregando" rounded="xl" elevation="0" class="pa-8 text-center">
      <v-icon icon="mdi-chart-bar" size="48" class="text-medium-emphasis mb-2" />
      <div class="text-body-1 text-medium-emphasis">Selecione o período e clique em Gerar</div>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const carregando = ref(false)
const dre = ref<any>(null)
const modo = ref<string>('mensal')
const mes = ref(new Date().getMonth() + 1)
const ano = ref(new Date().getFullYear())

const meses = [
  { value: 1, label: 'Janeiro' }, { value: 2, label: 'Fevereiro' },
  { value: 3, label: 'Março' },   { value: 4, label: 'Abril' },
  { value: 5, label: 'Maio' },    { value: 6, label: 'Junho' },
  { value: 7, label: 'Julho' },   { value: 8, label: 'Agosto' },
  { value: 9, label: 'Setembro' },{ value: 10, label: 'Outubro' },
  { value: 11, label: 'Novembro' },{ value: 12, label: 'Dezembro' },
]

const cardsResumo = computed(() => {
  if (!dre.value) return []
  return [
    { label: 'Recebimentos',      valor: dre.value.recebimentos,      cor: 'success',     icon: 'mdi-arrow-down-circle-outline' },
    { label: 'Despesas Fixas',    valor: dre.value.despesasFixas,     cor: 'deep-purple', icon: 'mdi-home-city-outline' },
    { label: 'Pessoas',           valor: dre.value.pessoas,           cor: 'blue',        icon: 'mdi-account-group-outline' },
    { label: 'Impostos',          valor: dre.value.impostos,          cor: 'error',       icon: 'mdi-gavel' },
  ]
})

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const pct = (v: number) => (v ?? 0).toFixed(1)

async function carregar() {
  carregando.value = true
  dre.value = null
  try {
    const url = modo.value === 'mensal' ? '/financeiro/dre/mensal' : '/financeiro/dre/anual'
    const res = await api.get(url, {
      params: { empresaId: auth.empresaId, ano: ano.value, mes: mes.value },
    })
    dre.value = res.data
  } finally { carregando.value = false }
}
</script>

<style scoped>
.dre-grupo-header {
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  min-height: 28px;
  padding-left: 16px;
}
.dre-sublinha {
  padding-left: 24px;
  min-height: 36px;
  font-size: 0.875rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
}
.dre-total-linha {
  background: rgba(var(--v-theme-surface-variant), 0.3);
  border-radius: 8px;
  min-height: 40px;
  margin: 2px 4px;
}
.dre-resultado-linha {
  background: rgba(var(--v-theme-primary), 0.05);
  border-radius: 10px;
  min-height: 52px;
  margin: 4px;
}
</style>
