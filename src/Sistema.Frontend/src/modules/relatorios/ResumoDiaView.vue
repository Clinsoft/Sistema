<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Resumo do Dia</h2></v-col>
      <v-col cols="auto" class="d-flex ga-2 align-center">
        <v-text-field v-model="data" type="date" density="compact" variant="outlined" hide-details
          style="width:170px" @update:model-value="carregar" />
        <v-btn color="green" variant="flat" prepend-icon="mdi-whatsapp" :disabled="!d" @click="enviarWhats">
          Enviar no WhatsApp
        </v-btn>
      </v-col>
    </v-row>

    <div v-if="d">
      <v-row dense class="mb-1">
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-3">
            <div class="text-caption text-medium-emphasis">Vendas do dia</div>
            <div class="text-h5 font-weight-bold text-success">R$ {{ fmt(d.totalVendas) }}</div>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-3">
            <div class="text-caption text-medium-emphasis">Nº de vendas</div>
            <div class="text-h5 font-weight-bold">{{ d.numeroVendas }}</div>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-3">
            <div class="text-caption text-medium-emphasis">Ticket médio</div>
            <div class="text-h5 font-weight-bold">R$ {{ fmt(d.ticketMedio) }}</div>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-3">
            <div class="text-caption text-medium-emphasis">A pagar / receber hoje</div>
            <div class="text-body-1 font-weight-bold text-error">− R$ {{ fmt(d.aPagarHoje) }}</div>
            <div class="text-body-2 font-weight-bold text-success">+ R$ {{ fmt(d.aReceberHoje) }}</div>
          </v-card>
        </v-col>
      </v-row>

      <v-card rounded="xl" elevation="1" class="mt-2">
        <v-card-title class="text-body-1 font-weight-bold py-2">Por loja</v-card-title>
        <v-data-table :headers="headers" :items="d.porLoja" density="compact" hide-default-footer
          :items-per-page="-1" no-data-text="Sem vendas no dia.">
          <template #item.total="{ item }">R$ {{ fmt(item.total) }}</template>
          <template #item.ticketMedio="{ item }">R$ {{ fmt(item.ticketMedio) }}</template>
          <template #item.margemPct="{ item }">{{ item.margemPct.toFixed(1) }}%</template>
        </v-data-table>
      </v-card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

interface LojaDia { nome: string; total: number; numeroVendas: number; ticketMedio: number; margemPct: number }
interface Resumo {
  data: string; totalVendas: number; numeroVendas: number; ticketMedio: number
  porLoja: LojaDia[]; aPagarHoje: number; aReceberHoje: number
}

const carregando = ref(true)
const d = ref<Resumo | null>(null)
const data = ref(new Date().toISOString().slice(0, 10))

const headers = [
  { title: 'Loja', key: 'nome', sortable: false },
  { title: 'Vendas', key: 'total', align: 'end' as const, sortable: false },
  { title: 'Nº', key: 'numeroVendas', align: 'end' as const, sortable: false },
  { title: 'Ticket', key: 'ticketMedio', align: 'end' as const, sortable: false },
  { title: 'Margem', key: 'margemPct', align: 'center' as const, width: 90, sortable: false },
]

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

function enviarWhats() {
  if (!d.value) return
  const dt = new Date(data.value + 'T12:00:00').toLocaleDateString('pt-BR')
  const linhas = [
    `📊 *Resumo EcoGranel* — ${dt}`,
    ``,
    `💰 Vendas: R$ ${fmt(d.value.totalVendas)} (${d.value.numeroVendas} vendas)`,
    `🎟️ Ticket médio: R$ ${fmt(d.value.ticketMedio)}`,
  ]
  if (d.value.porLoja.length) {
    linhas.push(``, `🏬 Por loja:`)
    for (const l of d.value.porLoja)
      linhas.push(`• ${l.nome}: R$ ${fmt(l.total)} (${l.numeroVendas} vd, ${l.margemPct.toFixed(0)}% marg.)`)
  }
  linhas.push(``, `📅 Hoje: a pagar R$ ${fmt(d.value.aPagarHoje)} · a receber R$ ${fmt(d.value.aReceberHoje)}`)
  const texto = encodeURIComponent(linhas.join('\n'))
  window.open(`https://wa.me/?text=${texto}`, '_blank')
}

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<Resumo>('/dashboard/resumo-dia', {
      params: { empresaId: auth.empresaId, data: data.value },
    })
    d.value = res.data
  } catch { d.value = null } finally { carregando.value = false }
}

onMounted(carregar)
</script>
