<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Crediário</div>
    </div>
    <v-row class="mb-3">
      <v-col v-for="c in resumo" :key="c.label" cols="6" sm="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="text-caption text-medium-emphasis">{{ c.label }}</div>
            <div class="text-h6 font-weight-bold" :class="c.classe">{{ c.valor }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
    <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
      <v-row dense>
        <v-col cols="12" sm="5">
          <v-text-field v-model="busca" placeholder="Buscar cliente..."
            prepend-inner-icon="mdi-magnify" variant="outlined" density="compact" hide-details
            @update:model-value="carregar" />
        </v-col>
        <v-col cols="12" sm="4">
          <v-select v-model="filtroStatus" label="Status"
            :items="['Todos','EmAberto','Liquidado','Inadimplente']"
            variant="outlined" density="compact" hide-details />
        </v-col>
      </v-row>
    </v-card>
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="crediarios" :loading="carregando" density="compact" hover>
        <template #item.saldoDevedor="{ item }">
          <span :class="item.saldoDevedor > 0 ? 'text-error font-weight-bold' : 'text-success'">
            R$ {{ fmt(item.saldoDevedor) }}
          </span>
        </template>
        <template #item.status="{ item }">
          <v-chip :color="corStatus(item.status)" size="small" variant="tonal">{{ item.status }}</v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-eye-outline" size="x-small" variant="text" color="info"
            @click="verParcelas(item)" title="Ver parcelas" />
          <v-btn icon="mdi-file-sign" size="x-small" variant="text" color="success"
            @click="imprimirContrato(item)" title="Imprimir contrato p/ assinatura" />
          <v-btn icon="mdi-printer" size="x-small" variant="text" color="primary"
            @click="imprimirCarne(item)" title="Imprimir carnê de parcelas" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog Parcelas -->
    <v-dialog v-model="dialogParcelas" max-width="700">
      <v-card rounded="xl">
        <v-card-title class="pa-4 d-flex align-center">
          Parcelas — {{ crediarioSel?.clienteNome }}
          <v-spacer />
          <v-chip :color="corStatus(crediarioSel?.status)" size="small" variant="tonal" class="mr-2">
            {{ crediarioSel?.status }}
          </v-chip>
          <v-btn icon="mdi-close" variant="text" @click="dialogParcelas = false" />
        </v-card-title>
        <v-data-table :headers="headersParcelas" :items="parcelas" density="compact" :loading="loadParcelas">
          <template #item.valor="{ item }">R$ {{ fmt(item.valor) }}</template>
          <template #item.dataVencimento="{ item }">{{ fmtData(item.dataVencimento) }}</template>
          <template #item.status="{ item }">
            <v-chip :color="item.status==='Pago'?'success':item.status==='Atrasado'?'error':'info'"
              size="small" variant="tonal">{{ item.status }}</v-chip>
          </template>
          <template #item.actions="{ item }">
            <v-btn v-if="item.status !== 'Pago'" icon="mdi-cash-check" size="x-small"
              variant="text" color="success" @click="baixarParcela(item)" />
          </template>
        </v-data-table>
      </v-card>
    </v-dialog>
  </div>
</template>
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore(); const notif = useNotifStore()
const carregando = ref(false); const loadParcelas = ref(false)
const crediarios = ref<any[]>([]); const parcelas = ref<any[]>([])
const busca = ref(''); const filtroStatus = ref('Todos')
const dialogParcelas = ref(false); const crediarioSel = ref<any>(null)

const resumo = computed(() => [
  { label:'Em aberto', valor:`R$ ${fmt(crediarios.value.filter((c: any)=>c.saldoDevedor>0).reduce((s: number,c: any)=>s+c.saldoDevedor,0))}`, classe:'text-error' },
  { label:'Clientes ativos', valor:crediarios.value.length, classe:'' },
  { label:'Inadimplentes', valor:crediarios.value.filter((c: any)=>c.status==='Inadimplente').length, classe:'text-warning' },
  { label:'Liquidados', valor:crediarios.value.filter((c: any)=>c.status==='Liquidado').length, classe:'text-success' },
])

const headers = [
  { title:'Cliente', key:'clienteNome', sortable:true },
  { title:'Limite', key:'limiteCredito' },
  { title:'Saldo Devedor', key:'saldoDevedor', sortable:true },
  { title:'Status', key:'status' },
  { title:'Ações', key:'actions', sortable:false },
]
const headersParcelas = [
  { title:'Nº', key:'numero' }, { title:'Vencimento', key:'dataVencimento' },
  { title:'Valor', key:'valor' }, { title:'Status', key:'status' }, { title:'', key:'actions' },
]
const fmt = (v: number) => (v??0).toLocaleString('pt-BR', { minimumFractionDigits:2 })
const fmtData = (d: string) => new Date(d).toLocaleDateString('pt-BR')
const corStatus = (s: string) => ({ EmAberto:'info', Liquidado:'success', Inadimplente:'error', Cancelado:'default' })[s] ?? 'default'

async function carregar() {
  carregando.value=true
  try { const r=await api.get('/crediario', { params:{ empresaId:auth.empresaId, q:busca.value } }); crediarios.value=r.data }
  finally { carregando.value=false }
}
async function verParcelas(item: any) {
  crediarioSel.value=item; dialogParcelas.value=true; loadParcelas.value=true
  try { const r=await api.get(`/crediario/${item.id}/parcelas`); parcelas.value=r.data }
  finally { loadParcelas.value=false }
}
async function baixarParcela(parcela: any) {
  await api.post(`/crediario/parcelas/${parcela.id}/pagar`, { dataPagamento: new Date().toISOString().slice(0,10) })
  notif.ok('Parcela baixada!'); await verParcelas(crediarioSel.value)
}
async function imprimirContrato(item: any) {
  try {
    const r = await api.get(`/crediario/${item.id}/contrato`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data)
    window.open(url, '_blank')
  } catch { notif.erro('Erro ao gerar contrato.') }
}
async function imprimirCarne(item: any) {
  const r=await api.get(`/crediario/${item.id}/carne`, { responseType:'blob' })
  const url=URL.createObjectURL(r.data); window.open(url, '_blank')
}
onMounted(carregar)
</script>
