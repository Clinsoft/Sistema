<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Crediário</div>
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">
        Novo Crediário
      </v-btn>
    </div>

    <GuiaPassos
      id="crediario"
      titulo="Como usar o Crediário"
      :passos="[
        'Clique em <b>Novo Crediário</b>, busque o <b>cliente</b>, informe o <b>valor total</b>, a <b>entrada</b> (opcional) e o <b>nº de parcelas</b>. A prévia mostra a parcela.',
        'Defina a <b>1ª parcela</b>, um <b>dia fixo de vencimento</b> e a <b>taxa de juros</b> (0 = sem juros). As parcelas são geradas automaticamente.',
        'Na lista, use 👁 para ver as <b>parcelas</b> e receber cada uma (💲). Baixe o <b>Contrato</b> e o <b>Carnê</b> em PDF pelos ícones de documento.',
        'Os cards mostram inadimplentes, saldo devedor e vencimentos de hoje. Parcelas em atraso aparecem destacadas em vermelho.',
      ]"
    />

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" sm="6">
          <v-text-field v-model="busca" label="Buscar cliente" variant="outlined"
            density="compact" hide-details clearable prepend-inner-icon="mdi-magnify"
            @update:model-value="carregar" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-select v-model="filtroStatus" label="Status"
            :items="[{title:'Todos',value:''},{title:'Ativo',value:'Ativo'},{title:'Quitado',value:'Quitado'},{title:'Inadimplente',value:'Inadimplente'}]"
            variant="outlined" density="compact" hide-details
            @update:model-value="carregar" />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" rounded="lg" icon="mdi-magnify"
            :loading="carregando" @click="carregar" />
        </v-col>
      </v-row>
    </v-card>

    <!-- Cards resumo -->
    <v-row class="mb-4">
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3 d-flex align-center">
            <v-avatar color="primary" size="40" class="mr-3">
              <v-icon icon="mdi-account-credit-card-outline" color="white" size="20" />
            </v-avatar>
            <div>
              <div class="text-h6 font-weight-bold">{{ crediarios.length }}</div>
              <div class="text-caption text-medium-emphasis">Contratos ativos</div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3 d-flex align-center">
            <v-avatar color="error" size="40" class="mr-3">
              <v-icon icon="mdi-alert-circle-outline" color="white" size="20" />
            </v-avatar>
            <div>
              <div class="text-h6 font-weight-bold">{{ totalInadimplentes }}</div>
              <div class="text-caption text-medium-emphasis">Inadimplentes</div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3 d-flex align-center">
            <v-avatar color="warning" size="40" class="mr-3">
              <v-icon icon="mdi-currency-usd" color="white" size="20" />
            </v-avatar>
            <div>
              <div class="text-h6 font-weight-bold">R$ {{ fmt(totalSaldoDevedor) }}</div>
              <div class="text-caption text-medium-emphasis">Saldo devedor total</div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3 d-flex align-center">
            <v-avatar color="success" size="40" class="mr-3">
              <v-icon icon="mdi-calendar-check-outline" color="white" size="20" />
            </v-avatar>
            <div>
              <div class="text-h6 font-weight-bold">{{ totalParcelasHoje }}</div>
              <div class="text-caption text-medium-emphasis">Vencem hoje</div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Tabela de crediários -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="crediariosExibidos" :loading="carregando"
        density="comfortable" hover no-data-text="Nenhum crediário encontrado">
        <template #item.clienteNome="{ item }">
          <div class="d-flex align-center gap-2">
            <v-avatar color="primary" size="32" variant="tonal">
              <span class="text-caption font-weight-bold">{{ item.clienteNome?.charAt(0) }}</span>
            </v-avatar>
            <div>
              <div class="text-body-2 font-weight-medium">{{ item.clienteNome }}</div>
              <div class="text-caption text-medium-emphasis">Contrato Nº {{ item.numero }}</div>
            </div>
          </div>
        </template>
        <template #item.saldoDevedor="{ item }">
          <span class="font-weight-medium" :class="item.saldoDevedor > 0 ? 'text-error' : 'text-success'">
            R$ {{ fmt(item.saldoDevedor) }}
          </span>
        </template>
        <template #item.valorTotal="{ item }">R$ {{ fmt(item.valorTotal) }}</template>
        <template #item.dataContrato="{ item }">{{ fmtData(item.dataContrato) }}</template>
        <template #item.inadimplente="{ item }">
          <v-chip v-if="item.inadimplente" size="small" color="error" variant="tonal">
            <v-icon start size="12">mdi-alert</v-icon>Inadimplente
          </v-chip>
          <v-chip v-else-if="item.saldoDevedor <= 0" size="small" color="success" variant="tonal">
            <v-icon start size="12">mdi-check</v-icon>Quitado
          </v-chip>
          <v-chip v-else size="small" color="primary" variant="tonal">
            <v-icon start size="12">mdi-clock-outline</v-icon>Em dia
          </v-chip>
        </template>
        <template #item.acoes="{ item }">
          <v-btn v-if="item.saldoDevedor > 0" icon="mdi-cash-check" size="x-small" variant="text"
            color="success" @click="receberPagamento(item)" title="Receber pagamento (dar baixa)" />
          <v-btn icon="mdi-eye-outline" size="x-small" variant="text" color="primary"
            @click="abrirParcelas(item)" title="Ver parcelas" />
          <v-btn icon="mdi-file-document-outline" size="x-small" variant="text" color="secondary"
            @click="baixarContrato(item)" title="Contrato PDF" />
          <v-btn icon="mdi-card-text-outline" size="x-small" variant="text" color="info"
            @click="baixarCarne(item)" title="Carnê PDF" />
        </template>
      </v-data-table>
    </v-card>

    <!-- ══ Dialog: Parcelas do crediário ══ -->
    <v-dialog v-model="dlgParcelas" max-width="700" persistent>
      <v-card rounded="xl">
        <v-toolbar color="primary" density="compact">
          <v-btn icon="mdi-close" @click="dlgParcelas = false" />
          <v-toolbar-title>
            Parcelas — {{ crediarioSelecionado?.clienteNome }}
          </v-toolbar-title>
          <v-spacer />
          <v-chip color="white" variant="flat" text-color="primary" class="mr-2" size="small">
            Saldo: R$ {{ fmt(crediarioSelecionado?.saldoDevedor ?? 0) }}
          </v-chip>
        </v-toolbar>

        <v-card-text class="pa-3">
          <!-- Resumo do contrato -->
          <v-row dense class="mb-3">
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis">Valor Total</div>
              <div class="text-body-2 font-weight-bold">R$ {{ fmt(crediarioSelecionado?.valorTotal ?? 0) }}</div>
            </v-col>
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis">Parcelas</div>
              <div class="text-body-2 font-weight-bold">{{ crediarioSelecionado?.numeroParcelas }}x</div>
            </v-col>
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis">Data contrato</div>
              <div class="text-body-2 font-weight-bold">{{ fmtData(crediarioSelecionado?.dataContrato) }}</div>
            </v-col>
            <v-col cols="6" sm="3">
              <div class="text-caption text-medium-emphasis">Status</div>
              <v-chip size="x-small" :color="crediarioSelecionado?.inadimplente ? 'error' : 'success'" variant="tonal">
                {{ crediarioSelecionado?.inadimplente ? 'Inadimplente' : 'Em dia' }}
              </v-chip>
            </v-col>
          </v-row>

          <v-data-table :headers="headersParcelas" :items="parcelas" :loading="carregandoParcelas"
            density="compact" hide-default-footer items-per-page="-1"
            no-data-text="Nenhuma parcela encontrada">
            <template #item.numero="{ item }">
              <span class="font-weight-bold">{{ item.numero }}/{{ crediarioSelecionado?.numeroParcelas }}</span>
            </template>
            <template #item.valor="{ item }">R$ {{ fmt(item.valor) }}</template>
            <template #item.valorAtualizado="{ item }">
              <span :class="item.valorAtualizado > item.valor ? 'text-error' : ''">
                R$ {{ fmt(item.valorAtualizado ?? item.valor) }}
              </span>
            </template>
            <template #item.dataVencimento="{ item }">
              <span :class="eVencida(item) ? 'text-error font-weight-medium' : ''">
                {{ fmtData(item.dataVencimento) }}
                <v-icon v-if="eVencida(item)" size="12" color="error">mdi-alert</v-icon>
              </span>
            </template>
            <template #item.dataPagamento="{ item }">
              {{ item.dataPagamento ? fmtData(item.dataPagamento) : '—' }}
            </template>
            <template #item.status="{ item }">
              <v-chip size="x-small" :color="corStatusParcela(item.status)" variant="tonal">
                {{ rotuloStatusParcela(item.status) }}
              </v-chip>
            </template>
            <template #item.acoes="{ item }">
              <v-btn v-if="item.status !== 'Paga' && item.status !== 'Cancelada'"
                icon="mdi-cash-check" size="x-small" variant="text" color="success"
                @click="abrirPagamento(item)" title="Receber parcela" />
            </template>
          </v-data-table>
        </v-card-text>
      </v-card>
    </v-dialog>

    <!-- ══ Dialog: Registrar pagamento de parcela ══ -->
    <v-dialog v-model="dlgPagamento" max-width="400" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon start color="success">mdi-cash-check</v-icon>
          Receber Parcela {{ parcelaSelecionada?.numero }}
        </v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12">
              <div class="text-body-2 mb-3">
                Valor original:
                <strong class="text-body-1">R$ {{ fmt(parcelaSelecionada?.valor ?? 0) }}</strong>
                <span v-if="(parcelaSelecionada?.valorAtualizado ?? 0) > (parcelaSelecionada?.valor ?? 0)"
                  class="text-error text-caption ml-2">
                  (atualizado: R$ {{ fmt(parcelaSelecionada?.valorAtualizado ?? 0) }})
                </span>
              </div>
            </v-col>
            <v-col cols="12">
              <v-text-field v-model.number="pagamento.valorPago" label="Valor recebido (R$) *"
                type="number" step="0.01" prefix="R$" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="pagamento.dataPagamento" label="Data do recebimento *"
                type="date" variant="outlined" density="compact" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgPagamento = false" :disabled="salvando">Cancelar</v-btn>
          <v-btn color="success" rounded="lg" :loading="salvando"
            :disabled="!(pagamento.valorPago > 0) || !pagamento.dataPagamento"
            @click="confirmarPagamento">
            Confirmar Recebimento
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ══ Dialog: Novo Crediário ══ -->
    <v-dialog v-model="dlgNovo" max-width="560" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon start color="primary">mdi-account-credit-card-outline</v-icon>
          Novo Crediário
        </v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12">
              <v-autocomplete v-model="novoForm.clienteId" label="Cliente *"
                :items="clientesBusca" item-title="nome" item-value="id"
                variant="outlined" density="compact"
                :loading="buscandoCliente"
                @update:search="buscarClientes"
                no-data-text="Digite para buscar clientes"
                return-object
                @update:model-value="selecionarCliente" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model.number="novoForm.valorTotal" label="Valor Total (R$) *"
                type="number" step="0.01" prefix="R$" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model.number="novoForm.entrada" label="Entrada (R$)"
                type="number" step="0.01" prefix="R$" variant="outlined" density="compact"
                hint="Opcional — desconta do valor financiado" persistent-hint />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model.number="novoForm.numeroParcelas" label="Nº de Parcelas *"
                type="number" min="1" max="60" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model.number="novoForm.taxaJurosMensal" label="Taxa Mensal (%)"
                type="number" step="0.01" suffix="%" variant="outlined" density="compact"
                hint="0 = sem juros" persistent-hint />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="novoForm.dataPrimeiraParcela" label="1ª Parcela em *"
                type="date" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-select v-model="novoForm.diaPagamento" label="Dia fixo de vencimento"
                :items="diasMes" variant="outlined" density="compact"
                hint="Aplica em todas as parcelas" persistent-hint />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="novoForm.observacao" label="Observação"
                rows="2" variant="outlined" density="compact" />
            </v-col>
          </v-row>

          <!-- Prévia das parcelas -->
          <v-card v-if="novoForm.valorTotal > 0 && novoForm.numeroParcelas > 0"
            rounded="lg" color="grey-lighten-4" class="mt-3 pa-3">
            <div class="text-caption text-medium-emphasis mb-1">Prévia</div>
            <div class="text-body-2">
              Valor financiado: <strong>R$ {{ fmt(valorFinanciado) }}</strong>
            </div>
            <div class="text-body-2">
              Parcela aprox.: <strong>R$ {{ fmt(valorParcela) }}</strong>
            </div>
          </v-card>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgNovo = false" :disabled="salvando">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando" @click="salvarNovo">
            Criar Crediário
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const carregando = ref(false)
const carregandoParcelas = ref(false)
const salvando = ref(false)
const busca = ref('')
const filtroStatus = ref('')
const crediarios = ref<any[]>([])

const dlgParcelas = ref(false)
const dlgPagamento = ref(false)
const dlgNovo = ref(false)
const crediarioSelecionado = ref<any>(null)
const parcelas = ref<any[]>([])
const parcelaSelecionada = ref<any>(null)

const pagamento = ref({ valorPago: 0, dataPagamento: new Date().toISOString().slice(0, 10) })

const headers = [
  { title: 'Cliente', key: 'clienteNome', sortable: true },
  { title: 'Data Contrato', key: 'dataContrato', width: 130 },
  { title: 'Total', key: 'valorTotal', width: 120 },
  { title: 'Saldo Devedor', key: 'saldoDevedor', width: 140 },
  { title: 'Parcelas', key: 'numeroParcelas', width: 90 },
  { title: 'Situação', key: 'inadimplente', width: 130 },
  { title: '', key: 'acoes', sortable: false, width: 160 },
]

const headersParcelas = [
  { title: 'Parc.', key: 'numero', width: 70 },
  { title: 'Vencimento', key: 'dataVencimento', width: 120 },
  { title: 'Valor', key: 'valor', width: 110 },
  { title: 'Valor Atual.', key: 'valorAtualizado', width: 120 },
  { title: 'Pgto', key: 'dataPagamento', width: 110 },
  { title: 'Status', key: 'status', width: 100 },
  { title: '', key: 'acoes', sortable: false, width: 50 },
]

const diasMes = Array.from({ length: 28 }, (_, i) => i + 1)

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
// Aceita "yyyy-MM-dd" ou ISO com hora ("yyyy-MM-ddTHH:mm:ss"): usa só a parte da data.
const fmtData = (d?: string) => {
  if (!d) return '—'
  const dt = new Date(String(d).slice(0, 10) + 'T12:00:00')
  return isNaN(dt.getTime()) ? '—' : dt.toLocaleDateString('pt-BR')
}
const hoje = new Date().toISOString().slice(0, 10)

function eVencida(parcela: any) {
  return parcela.status !== 'Paga' && parcela.dataVencimento < hoje
}

function corStatusParcela(s: string) {
  return ({ Paga: 'success', Atrasada: 'error', EmAberto: 'info', Renegociada: 'warning', Cancelada: 'default' } as any)[s] ?? 'default'
}
function rotuloStatusParcela(s: string) {
  return ({ Paga: 'Paga', Atrasada: 'Atrasada', EmAberto: 'Em aberto', Renegociada: 'Renegociada', Cancelada: 'Cancelada' } as any)[s] ?? s
}

const crediariosExibidos = computed(() => {
  let lista = crediarios.value
  if (filtroStatus.value === 'Inadimplente') lista = lista.filter(c => c.inadimplente)
  else if (filtroStatus.value === 'Quitado') lista = lista.filter(c => c.saldoDevedor <= 0)
  else if (filtroStatus.value === 'Ativo') lista = lista.filter(c => c.saldoDevedor > 0 && !c.inadimplente)
  return lista
})

const totalInadimplentes = computed(() => crediarios.value.filter(c => c.inadimplente).length)
const totalSaldoDevedor = computed(() => crediarios.value.reduce((s, c) => s + (c.saldoDevedor ?? 0), 0))
const totalParcelasHoje = computed(() =>
  parcelas.value.filter(p => p.dataVencimento === hoje && p.status !== 'Pago').length
)

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/crediario', {
      params: { empresaId: auth.empresaId, q: busca.value || undefined }
    })
    crediarios.value = r.data
  } catch { /* silencioso */ } finally { carregando.value = false }
}

async function abrirParcelas(crediario: any) {
  crediarioSelecionado.value = crediario
  dlgParcelas.value = true
  carregandoParcelas.value = true
  try {
    const r = await api.get(`/crediario/${crediario.id}/parcelas`)
    parcelas.value = r.data
  } catch { /* silencioso */ } finally { carregandoParcelas.value = false }
}

// Atalho: dar baixa direto na linha do crediário — abre as parcelas e já leva
// para o recebimento da próxima parcela em aberto.
async function receberPagamento(crediario: any) {
  await abrirParcelas(crediario)
  const proxima = parcelas.value.find((p: any) => p.status !== 'Paga' && p.status !== 'Cancelada')
  if (proxima) abrirPagamento(proxima)
  else notif.aviso('Não há parcelas em aberto para receber.')
}

function abrirPagamento(parcela: any) {
  parcelaSelecionada.value = parcela
  pagamento.value = {
    valorPago: parcela.valorAtualizado ?? parcela.valor,
    dataPagamento: new Date().toISOString().slice(0, 10),
  }
  dlgPagamento.value = true
}

async function confirmarPagamento() {
  salvando.value = true
  try {
    await api.post(`/crediario/parcelas/${parcelaSelecionada.value.id}/pagar`, {
      valorPago: pagamento.value.valorPago,
    })
    notif.ok('Pagamento registrado!')
    dlgPagamento.value = false
    // Recarregar parcelas e lista
    await Promise.all([
      abrirParcelas(crediarioSelecionado.value),
      carregar(),
    ])
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao registrar pagamento.')
  } finally { salvando.value = false }
}

async function baixarContrato(crediario: any) {
  try {
    const r = await api.get(`/crediario/${crediario.id}/contrato`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data)
    const a = document.createElement('a')
    a.href = url; a.download = `Contrato_${crediario.numero}.pdf`; a.click()
  } catch { notif.erro('Contrato não disponível.') }
}

async function baixarCarne(crediario: any) {
  try {
    const r = await api.get(`/crediario/${crediario.id}/carne`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data)
    const a = document.createElement('a')
    a.href = url; a.download = `Carne_${crediario.numero}.pdf`; a.click()
  } catch { notif.erro('Carnê não disponível.') }
}

// ── Novo crediário ──
const buscandoCliente = ref(false)
const clientesBusca = ref<any[]>([])

const novoFormPadrao = () => ({
  clienteId: null as any,
  clienteNome: '',
  valorTotal: 0,
  entrada: 0,
  numeroParcelas: 3,
  taxaJurosMensal: 0,
  dataPrimeiraParcela: (() => {
    const d = new Date(); d.setMonth(d.getMonth() + 1); d.setDate(1)
    return d.toISOString().slice(0, 10)
  })(),
  diaPagamento: null as number | null,
  observacao: '',
})

const novoForm = ref(novoFormPadrao())

const valorFinanciado = computed(() =>
  Math.max(0, (novoForm.value.valorTotal ?? 0) - (novoForm.value.entrada ?? 0))
)

const valorParcela = computed(() => {
  const vf = valorFinanciado.value
  const n = novoForm.value.numeroParcelas ?? 1
  const taxa = (novoForm.value.taxaJurosMensal ?? 0) / 100
  if (n <= 0) return 0
  if (taxa === 0) return vf / n
  // Price (SAC simplificado via Price)
  return vf * (taxa * Math.pow(1 + taxa, n)) / (Math.pow(1 + taxa, n) - 1)
})

async function buscarClientes(busca: string) {
  if (!busca || busca.length < 2) return
  buscandoCliente.value = true
  try {
    const r = await api.get('/clientes', { params: { empresaId: auth.empresaId, termo: busca } })
    clientesBusca.value = r.data?.itens ?? r.data ?? []
  } catch { clientesBusca.value = [] }
  finally { buscandoCliente.value = false }
}

function selecionarCliente(cliente: any) {
  if (cliente?.id) novoForm.value.clienteId = cliente.id
}

function abrirNovo() {
  novoForm.value = novoFormPadrao()
  clientesBusca.value = []
  dlgNovo.value = true
}

async function salvarNovo() {
  if (!novoForm.value.clienteId || novoForm.value.valorTotal <= 0 || !novoForm.value.dataPrimeiraParcela) {
    notif.erro('Preencha cliente, valor total e data da 1ª parcela.')
    return
  }
  salvando.value = true
  try {
    const clienteId = typeof novoForm.value.clienteId === 'object'
      ? novoForm.value.clienteId.id
      : novoForm.value.clienteId
    await api.post('/crediario', {
      empresaId: auth.empresaId,
      clienteId,
      usuarioId: auth.usuario?.id,
      valorTotal: novoForm.value.valorTotal,
      valorEntrada: novoForm.value.entrada || 0,
      numeroParcelas: novoForm.value.numeroParcelas,
      taxaJurosMensal: novoForm.value.taxaJurosMensal || 0,
      dataPrimeiraParcela: novoForm.value.dataPrimeiraParcela,
      diaVencimento: novoForm.value.diaPagamento,
      observacao: novoForm.value.observacao || null,
    })
    notif.ok('Crediário criado!')
    dlgNovo.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao criar crediário.')
  } finally { salvando.value = false }
}

onMounted(carregar)
</script>
