<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Clientes Sumidos</h2></v-col>
    </v-row>

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      Clientes que <b>compravam mas pararam</b> — última compra há mais de X dias. Ordenados por
      <b>quanto já gastaram</b> (priorize os mais valiosos) para uma <b>reativação</b> por WhatsApp.
    </v-alert>

    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="6" sm="3">
          <v-select v-model.number="diasSem" :items="[30,60,90,180]" label="Sem comprar há (dias)"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="6" sm="4">
          <v-select v-model="localEstoqueId" :items="locaisOpcoes" item-title="nome" item-value="id"
            label="Loja" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" sm="3">
          <v-btn color="primary" variant="tonal" rounded="lg" :loading="carregando" @click="carregar">Buscar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <v-row dense class="mb-2">
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Clientes sumidos</div>
          <div class="text-h5 font-weight-bold">{{ itens.length }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Já gastaram (total)</div>
          <div class="text-h5 font-weight-bold text-success">R$ {{ fmt(totalGasto) }}</div>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="itens" :loading="carregando"
        density="compact" hover items-per-page="50"
        :sort-by="[{ key: 'totalGasto', order: 'desc' }]"
        no-data-text="Nenhum cliente sumido com esses filtros.">
        <template #item.ultimaCompra="{ item }">{{ fmtData(item.ultimaCompra) }}</template>
        <template #item.diasSemComprar="{ item }">
          <span class="font-weight-bold" :class="item.diasSemComprar >= 120 ? 'text-error' : 'text-warning'">
            {{ item.diasSemComprar }} d
          </span>
        </template>
        <template #item.totalGasto="{ item }">R$ {{ fmt(item.totalGasto) }}</template>
        <template #item.ticketMedio="{ item }">R$ {{ fmt(item.ticketMedio) }}</template>
        <template #item.acoes="{ item }">
          <v-btn v-if="item.telefone" :href="linkWhats(item)" target="_blank"
            icon="mdi-whatsapp" size="x-small" color="green" variant="text"
            title="Reativar por WhatsApp" />
          <span v-else class="text-caption text-medium-emphasis">sem tel.</span>
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

interface Item {
  id: string; nome: string; telefone: string | null; loja: string
  ultimaCompra: string; diasSemComprar: number; totalGasto: number
  numeroCompras: number; ticketMedio: number
}

const carregando = ref(true)
const itens = ref<Item[]>([])
const diasSem = ref(60)
const localEstoqueId = ref<string | null>(null)
const locais = ref<any[]>([])

const locaisOpcoes = computed(() => [{ id: null, nome: 'Todas as lojas' }, ...locais.value])
const totalGasto = computed(() => itens.value.reduce((s, i) => s + i.totalGasto, 0))

const headers = [
  { title: 'Cliente', key: 'nome' },
  { title: 'Loja', key: 'loja', width: 130 },
  { title: 'Última compra', key: 'ultimaCompra', width: 130 },
  { title: 'Sem comprar', key: 'diasSemComprar', width: 120, align: 'end' as const },
  { title: 'Compras', key: 'numeroCompras', width: 90, align: 'end' as const },
  { title: 'Total gasto', key: 'totalGasto', align: 'end' as const },
  { title: 'Ticket médio', key: 'ticketMedio', align: 'end' as const },
  { title: 'Reativar', key: 'acoes', width: 90, align: 'center' as const, sortable: false },
]

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtData = (v: string) => v ? new Date(v).toLocaleDateString('pt-BR') : '—'

function linkWhats(item: Item) {
  let tel = (item.telefone ?? '').replace(/\D/g, '')
  if (tel.length <= 11) tel = '55' + tel   // adiciona DDI Brasil se vier sem
  const primeiro = (item.nome || '').split(' ')[0]
  const msg = `Olá ${primeiro}! Sentimos sua falta na EcoGranel 💚 Passe na loja e aproveite nossas novidades!`
  return `https://wa.me/${tel}?text=${encodeURIComponent(msg)}`
}

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<{ itens: Item[] }>('/dashboard/clientes-sumidos', {
      params: { empresaId: auth.empresaId, diasSem: diasSem.value, localEstoqueId: localEstoqueId.value || undefined },
    })
    itens.value = res.data.itens ?? []
  } catch { itens.value = [] } finally { carregando.value = false }
}

onMounted(async () => {
  await carregar()
  try {
    const r = await api.get('/locais-estoque', { params: { empresaId: auth.empresaId } })
    locais.value = r.data ?? []
  } catch { locais.value = [] }
})
</script>
