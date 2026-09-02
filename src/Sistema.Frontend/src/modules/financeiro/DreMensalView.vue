<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">DRE Comparativo (mês a mês)</h2></v-col>
      <v-col cols="auto">
        <v-select v-model.number="meses" :items="[6,12,24]" label="Meses" density="compact"
          variant="outlined" hide-details style="width:120px" @update:model-value="carregar" />
      </v-col>
    </v-row>

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      Evolução mensal: <b>receita</b>, <b>CMV</b> (custo da mercadoria vendida), <b>lucro bruto</b>,
      <b>despesas</b> pagas e <b>resultado</b>. Enxergue a tendência, não só o mês isolado.
    </v-alert>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="itens" :loading="carregando"
        density="compact" hover :items-per-page="-1" hide-default-footer
        no-data-text="Sem dados no período.">
        <template #item.receita="{ item }">R$ {{ fmt(item.receita) }}</template>
        <template #item.cmv="{ item }">R$ {{ fmt(item.cmv) }}</template>
        <template #item.lucroBruto="{ item }">R$ {{ fmt(item.lucroBruto) }}</template>
        <template #item.despesas="{ item }">R$ {{ fmt(item.despesas) }}</template>
        <template #item.resultado="{ item }">
          <div class="d-flex align-center justify-end ga-2">
            <div class="barra" :style="barraStyle(item.resultado)"></div>
            <span class="font-weight-bold" :class="item.resultado >= 0 ? 'text-success' : 'text-error'">
              R$ {{ fmt(item.resultado) }}
            </span>
          </div>
        </template>
        <template #item.margemPct="{ item }">
          <span :class="item.margemPct >= 0 ? 'text-success' : 'text-error'">{{ item.margemPct.toFixed(1) }}%</span>
        </template>
        <template #body.append>
          <tr class="font-weight-bold" style="background:rgba(0,0,0,0.03)">
            <td>Total</td>
            <td class="text-end">R$ {{ fmt(tot.receita) }}</td>
            <td class="text-end">R$ {{ fmt(tot.cmv) }}</td>
            <td class="text-end">R$ {{ fmt(tot.lucroBruto) }}</td>
            <td class="text-end">R$ {{ fmt(tot.despesas) }}</td>
            <td class="text-end" :class="tot.resultado >= 0 ? 'text-success' : 'text-error'">R$ {{ fmt(tot.resultado) }}</td>
            <td class="text-center">{{ tot.receita > 0 ? (tot.resultado / tot.receita * 100).toFixed(1) : '0.0' }}%</td>
          </tr>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

interface Mes {
  mes: string; label: string; receita: number; cmv: number
  lucroBruto: number; despesas: number; resultado: number; margemPct: number
}

const carregando = ref(true)
const itens = ref<Mes[]>([])
const meses = ref(12)

const headers = [
  { title: 'Mês', key: 'label', sortable: false },
  { title: 'Receita', key: 'receita', align: 'end' as const, sortable: false },
  { title: 'CMV', key: 'cmv', align: 'end' as const, sortable: false },
  { title: 'Lucro bruto', key: 'lucroBruto', align: 'end' as const, sortable: false },
  { title: 'Despesas', key: 'despesas', align: 'end' as const, sortable: false },
  { title: 'Resultado', key: 'resultado', align: 'end' as const, sortable: false },
  { title: 'Margem', key: 'margemPct', width: 90, align: 'center' as const, sortable: false },
]

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

const maxAbs = computed(() => Math.max(1, ...itens.value.map(i => Math.abs(i.resultado))))
function barraStyle(v: number) {
  const w = Math.round(Math.abs(v) / maxAbs.value * 60)
  return { width: w + 'px', height: '8px', borderRadius: '2px', background: v >= 0 ? '#4caf50' : '#ef5350', opacity: '0.6' }
}

const tot = computed(() => itens.value.reduce((a, i) => ({
  receita: a.receita + i.receita, cmv: a.cmv + i.cmv, lucroBruto: a.lucroBruto + i.lucroBruto,
  despesas: a.despesas + i.despesas, resultado: a.resultado + i.resultado,
}), { receita: 0, cmv: 0, lucroBruto: 0, despesas: 0, resultado: 0 }))

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<{ itens: Mes[] }>('/financeiro/dre/mensal', {
      params: { empresaId: auth.empresaId, meses: meses.value },
    })
    itens.value = res.data.itens ?? []
  } catch { itens.value = [] } finally { carregando.value = false }
}

onMounted(carregar)
</script>

<style scoped>
.barra { display: inline-block; }
</style>
