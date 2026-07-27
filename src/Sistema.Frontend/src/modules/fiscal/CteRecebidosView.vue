<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col cols="12" sm="">
        <h2 class="text-h5 font-weight-bold">CT-e recebidos (frete)</h2>
        <div class="text-caption text-medium-emphasis">
          Conhecimentos de transporte emitidos pelas transportadoras contra o CNPJ da empresa.
        </div>
      </v-col>
      <v-col cols="12" sm="auto">
        <v-btn color="primary" variant="tonal" prepend-icon="mdi-cloud-download"
          :loading="consultando" @click="consultarSefaz">Consultar SEFAZ</v-btn>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1" class="mb-4">
      <v-card-text>
        <v-row dense align="center">
          <v-col cols="12" sm="4">
            <v-text-field v-model="filtros.emitente" label="Transportadora / CNPJ" density="compact"
              prepend-inner-icon="mdi-magnify" clearable hide-details @update:model-value="carregar" />
          </v-col>
          <v-col cols="6" sm="3">
            <v-text-field v-model="filtros.dataInicio" label="De" type="date" density="compact"
              hide-details @update:model-value="carregar" />
          </v-col>
          <v-col cols="6" sm="3">
            <v-text-field v-model="filtros.dataFim" label="Até" type="date" density="compact"
              hide-details @update:model-value="carregar" />
          </v-col>
          <v-col cols="12" sm="2" class="text-sm-right">
            <div class="text-caption text-medium-emphasis">Total do frete</div>
            <div class="text-h6 text-error font-weight-bold">R$ {{ fmt(totalFrete) }}</div>
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="ctes" :loading="carregando" density="compact"
        hover items-per-page="25">
        <template #item.dataEmissao="{ item }">{{ fmtData(item.dataEmissao) }}</template>
        <template #item.valorTotal="{ item }">R$ {{ fmt(item.valorTotal) }}</template>
        <template #item.situacao="{ item }">
          <v-chip size="x-small" :color="item.situacao === 'Cancelada' ? 'error' : 'success'">
            {{ item.situacao === 'Cancelada' ? 'Cancelado' : 'Autorizado' }}
          </v-chip>
        </template>
        <template #item.financeiro="{ item }">
          <v-chip v-if="item.lancado" size="x-small" color="success" variant="tonal"
            prepend-icon="mdi-check">Lançado</v-chip>
          <span v-else class="text-caption text-medium-emphasis">—</span>
        </template>
        <template #item.actions="{ item }">
          <v-btn v-if="item.temXml" icon="mdi-file-xml-box" size="x-small" variant="text"
            color="primary" title="Baixar XML" @click="baixarXml(item)" />
          <v-btn size="x-small" variant="tonal" color="error" class="ml-1"
            :disabled="item.situacao === 'Cancelada' || item.valorTotal <= 0"
            prepend-icon="mdi-cash-plus" @click="abrirLancar(item)">Financeiro</v-btn>
        </template>
        <template #no-data>
          <div class="text-center text-medium-emphasis py-6">
            Nenhum CT-e recebido. Clique em <b>Consultar SEFAZ</b> para buscar.
          </div>
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog: lançar frete no financeiro -->
    <v-dialog v-model="dialogLancar" max-width="460">
      <v-card rounded="xl">
        <v-card-title>Lançar frete no financeiro</v-card-title>
        <v-card-text>
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            Gera uma <b>conta a pagar</b> de <b>R$ {{ fmt(cteSel?.valorTotal || 0) }}</b> para
            <b>{{ cteSel?.emitenteNome }}</b> (CT-e {{ cteSel?.numero }}).
          </v-alert>
          <v-text-field v-model="lancForm.vencimento" label="Vencimento" type="date"
            density="compact" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="dialogLancar = false">Cancelar</v-btn>
          <v-btn color="error" :loading="lancando" @click="lancarFinanceiro">Lançar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const ctes = ref<any[]>([])
const carregando = ref(false)
const consultando = ref(false)
const filtros = ref({ emitente: '', dataInicio: '', dataFim: '' })

const headers = [
  { title: 'Transportadora', key: 'emitenteNome' },
  { title: 'CT-e', key: 'numero', width: 90 },
  { title: 'Série', key: 'serie', width: 70 },
  { title: 'Emissão', key: 'dataEmissao', width: 110 },
  { title: 'Valor frete', key: 'valorTotal', width: 120, align: 'end' as const },
  { title: 'Situação', key: 'situacao', width: 110 },
  { title: 'Financeiro', key: 'financeiro', width: 110 },
  { title: '', key: 'actions', sortable: false, width: 160 },
]

const totalFrete = computed(() =>
  ctes.value.filter(c => c.situacao !== 'Cancelada').reduce((s, c) => s + (c.valorTotal || 0), 0))

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtData = (v: string) => v ? String(v).slice(0, 10).split('-').reverse().join('/') : '—'

async function carregar() {
  carregando.value = true
  try {
    const { data } = await api.get('/fiscal/nfes-recebidas', {
      params: {
        empresaId: auth.empresaId, modelo: '57',
        emitente: filtros.value.emitente || undefined,
        dataInicio: filtros.value.dataInicio || undefined,
        dataFim: filtros.value.dataFim || undefined,
      },
    })
    ctes.value = Array.isArray(data) ? data : []
  } catch { ctes.value = [] }
  finally { carregando.value = false }
}

async function consultarSefaz() {
  consultando.value = true
  try {
    await api.post('/fiscal/nfes-recebidas/consultar', null, { params: { empresaId: auth.empresaId } })
    notif.ok('Consulta à SEFAZ concluída.')
    await carregar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao consultar a SEFAZ.')
  } finally { consultando.value = false }
}

function baixarXml(item: any) {
  window.open(`/api/fiscal/nfes-recebidas/${item.id}/xml?empresaId=${auth.empresaId}`, '_blank')
}


// Lançar no financeiro
const dialogLancar = ref(false)
const cteSel = ref<any>(null)
const lancando = ref(false)
const lancForm = ref({ vencimento: '' })

function abrirLancar(item: any) {
  cteSel.value = item
  const d = new Date(); d.setDate(d.getDate() + 7)
  lancForm.value = { vencimento: d.toISOString().slice(0, 10) }
  dialogLancar.value = true
}

async function lancarFinanceiro() {
  if (!cteSel.value) return
  lancando.value = true
  try {
    const { data } = await api.post(`/fiscal/nfes-recebidas/${cteSel.value.id}/lancar-financeiro`, {
      empresaId: auth.empresaId,
      dataVencimento: lancForm.value.vencimento || null,
    })
    notif.ok(data?.fornecedorVinculado
      ? 'Frete lançado como conta a pagar (transportadora vinculada ao fornecedor).'
      : 'Frete lançado como conta a pagar.')
    cteSel.value.lancado = true
    dialogLancar.value = false
  } catch (e: any) {
    if (e?.response?.status === 409)
      notif.aviso('Este CT-e já foi lançado no financeiro.')
    else
      notif.erro(e?.response?.data?.mensagem ?? 'Erro ao lançar no financeiro.')
  } finally { lancando.value = false }
}

onMounted(carregar)
</script>
