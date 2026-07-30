<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div>
        <div class="text-h5 font-weight-bold">Sessões de Caixa</div>
        <div class="text-caption text-medium-emphasis">Abertura, fechamento e movimentações internas</div>
      </div>
      <v-spacer />
      <div class="d-flex gap-2">
        <template v-if="sessaoAtiva">
          <v-btn color="secondary" variant="tonal" prepend-icon="mdi-cash-plus"
            @click="abrirOperacao('suprimento')">Suprimento</v-btn>
          <v-btn color="warning" variant="tonal" prepend-icon="mdi-cash-minus"
            @click="abrirOperacao('sangria')">Sangria</v-btn>
          <v-btn color="error" prepend-icon="mdi-lock-outline"
            @click="prepararFechamento">Fechar Caixa</v-btn>
        </template>
        <v-btn v-else color="success" prepend-icon="mdi-lock-open-outline"
          @click="dialogAbrir = true">Abrir Caixa</v-btn>
      </div>
    </div>

    <GuiaPassos
      id="sessoes-caixa"
      titulo="Como usar as Sessões de Caixa"
      :passos="[
        '<b>Abrir Caixa</b>: selecione o <b>local de estoque</b> e informe o <b>fundo de troco</b> (dinheiro que já está na gaveta).',
        'Durante o turno, registre <b>Suprimento</b> (entrada de dinheiro) ou <b>Sangria</b> (retirada) conforme necessário.',
        'O banner mostra em tempo real as vendas por forma de pagamento e o <b>saldo esperado</b> em dinheiro.',
        '<b>Fechar Caixa</b>: conte o dinheiro físico e informe o valor. O sistema calcula a <b>diferença</b> (sobra/falta) automaticamente.',
        'Use o filtro por período para consultar o <b>histórico</b> de fechamentos e conferir diferenças de caixas anteriores.',
      ]"
    />

    <!-- Banner sessão ativa -->
    <v-card v-if="sessaoAtiva" rounded="xl" elevation="1" class="mb-4" color="success" variant="tonal">
      <v-card-text class="pa-4">
        <v-row align="center">
          <v-col cols="auto">
            <v-avatar color="success" size="48">
              <v-icon icon="mdi-cash-register" color="white" />
            </v-avatar>
          </v-col>
          <v-col>
            <div class="text-body-1 font-weight-bold">
              Caixa #{{ sessaoAtiva.numero }} — {{ sessaoAtiva.operador ?? 'Operador' }}
            </div>
            <div class="text-caption text-medium-emphasis">
              Abertura: {{ fmtDt(sessaoAtiva.abertura) }} ·
              Local: {{ sessaoAtiva.localEstoque ?? '—' }} ·
              Fundo: R$ {{ fmt(sessaoAtiva.saldoAbertura) }}
            </div>
          </v-col>
          <v-col cols="12" sm="auto">
            <v-row dense justify="end">
              <v-col cols="auto" class="text-center">
                <div class="text-h6 font-weight-bold text-success">R$ {{ fmt(sessaoAtiva.totalVendas) }}</div>
                <div class="text-caption text-medium-emphasis">Total vendas</div>
              </v-col>
              <v-divider vertical class="mx-3 my-1" />
              <v-col cols="auto" class="text-center">
                <div class="text-body-1 font-weight-bold text-secondary">+R$ {{ fmt(sessaoAtiva.totalSuprimentos ?? 0) }}</div>
                <div class="text-caption text-medium-emphasis">Suprimentos</div>
              </v-col>
              <v-divider vertical class="mx-3 my-1" />
              <v-col cols="auto" class="text-center">
                <div class="text-body-1 font-weight-bold text-warning">-R$ {{ fmt(sessaoAtiva.totalSangrias ?? 0) }}</div>
                <div class="text-caption text-medium-emphasis">Sangrias</div>
              </v-col>
              <v-divider vertical class="mx-3 my-1" />
              <v-col cols="auto" class="text-center">
                <div class="text-body-1 font-weight-bold text-primary">R$ {{ fmt(saldoEsperado) }}</div>
                <div class="text-caption text-medium-emphasis">Esperado em caixa</div>
              </v-col>
            </v-row>
          </v-col>
        </v-row>

        <!-- Breakdown por forma de pagamento -->
        <v-divider class="my-3" />
        <v-row dense>
          <v-col v-for="fp in formasPagamentoBanner" :key="fp.label" cols="6" sm="3">
            <div class="d-flex align-center gap-2">
              <v-icon :icon="fp.icon" :color="fp.cor" size="18" />
              <div>
                <div class="text-caption text-medium-emphasis">{{ fp.label }}</div>
                <div class="text-body-2 font-weight-bold">R$ {{ fmt(fp.valor) }}</div>
              </div>
            </div>
          </v-col>
        </v-row>

        <!-- Movimentações internas da sessão -->
        <template v-if="sessaoAtiva.operacoes?.length">
          <v-divider class="my-3" />
          <div class="text-caption text-medium-emphasis font-weight-bold mb-2 text-uppercase" style="letter-spacing:.05em">
            Movimentações internas
          </div>
          <v-row dense>
            <v-col v-for="op in sessaoAtiva.operacoes" :key="op.criadoEm + op.valor" cols="12" sm="6" md="4">
              <div class="d-flex align-center gap-2">
                <v-icon
                  :icon="op.tipo === 'Suprimento' ? 'mdi-cash-plus' : 'mdi-cash-minus'"
                  :color="op.tipo === 'Suprimento' ? 'secondary' : 'warning'"
                  size="18" />
                <div class="flex-grow-1">
                  <span class="text-caption font-weight-medium">{{ op.tipo }}</span>
                  <span class="text-caption text-medium-emphasis ml-1">{{ op.descricao }}</span>
                </div>
                <span class="text-caption font-weight-bold"
                  :class="op.tipo === 'Suprimento' ? 'text-secondary' : 'text-warning'">
                  {{ op.tipo === 'Suprimento' ? '+' : '-' }}R$ {{ fmt(op.valor) }}
                </span>
                <span class="text-caption text-medium-emphasis">{{ fmtHora(op.criadoEm) }}</span>
              </div>
            </v-col>
          </v-row>
        </template>
      </v-card-text>
    </v-card>

    <v-alert v-else-if="!carregando" type="warning" variant="tonal" rounded="xl" class="mb-4"
      text="Nenhum caixa aberto. Clique em 'Abrir Caixa' para começar." />

    <!-- Filtros histórico -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" sm="3">
          <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f }" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.inicio" label="De" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.fim" label="Até" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" @click="listar" :loading="carregando">Buscar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <!-- Tabela histórico -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="sessoes" :loading="carregando"
        density="compact" hover items-per-page="20">
        <template #item.status="{ item }">
          <v-chip :color="item.status === 'Aberta' ? 'success' : 'default'" size="small" variant="tonal">
            {{ item.status }}
          </v-chip>
        </template>
        <template #item.abertura="{ item }">{{ fmtDt(item.abertura) }}</template>
        <template #item.fechamento="{ item }">{{ item.fechamento ? fmtDt(item.fechamento) : '—' }}</template>
        <template #item.saldoAbertura="{ item }">R$ {{ fmt(item.saldoAbertura) }}</template>
        <template #item.totalVendas="{ item }">R$ {{ fmt(item.totalVendas) }}</template>
        <template #item.movimentos="{ item }">
          <span class="text-secondary text-caption">+{{ fmt(item.totalSuprimentos ?? 0) }}</span>
          <span class="mx-1 text-medium-emphasis">/</span>
          <span class="text-warning text-caption">-{{ fmt(item.totalSangrias ?? 0) }}</span>
        </template>
        <template #item.saldoFechamento="{ item }">
          {{ item.saldoFechamento != null ? 'R$ ' + fmt(item.saldoFechamento) : '—' }}
        </template>
        <template #item.diferenca="{ item }">
          <span v-if="item.saldoFechamento != null"
            :class="calcDiferenca(item) >= 0 ? 'text-success font-weight-bold' : 'text-error font-weight-bold'">
            R$ {{ fmt(calcDiferenca(item)) }}
          </span>
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <template #item.acoes="{ item }">
          <v-btn v-if="item.status === 'Aberta'" icon="mdi-lock-outline" size="x-small" variant="text"
            color="error" title="Fechar caixa" @click="sessaoAtiva = item; prepararFechamento()" />
          <v-btn v-else icon="mdi-printer" size="x-small" variant="text" color="primary"
            title="Imprimir fechamento" @click="imprimirFechamento(item.id)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- ── Dialog suprimento / sangria ── -->
    <v-dialog v-model="dialogOperacao" max-width="400" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2">
          <v-icon :icon="tipoOperacao === 'suprimento' ? 'mdi-cash-plus' : 'mdi-cash-minus'"
            :color="tipoOperacao === 'suprimento' ? 'secondary' : 'warning'" />
          {{ tipoOperacao === 'suprimento' ? 'Suprimento de Caixa' : 'Sangria de Caixa' }}
        </v-card-title>
        <v-card-text>
          <v-alert :type="tipoOperacao === 'suprimento' ? 'info' : 'warning'"
            variant="tonal" density="compact" class="mb-3 text-body-2">
            {{ tipoOperacao === 'suprimento'
              ? 'Entrada de dinheiro no caixa (ex: troco adicional de turno, recebimento avulso).'
              : 'Retirada de dinheiro do caixa (ex: reforço de cofre, pagamento interno).' }}
          </v-alert>
          <v-text-field v-model.number="operacao.valor" label="Valor (R$) *"
            type="number" variant="outlined" density="compact" prefix="R$" autofocus
            class="mb-3" @keyup.enter="salvarOperacao" />
          <v-text-field v-model="operacao.descricao" label="Motivo / Observação"
            variant="outlined" density="compact" @keyup.enter="salvarOperacao" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogOperacao = false">Cancelar</v-btn>
          <v-btn :color="tipoOperacao === 'suprimento' ? 'secondary' : 'warning'"
            :loading="salvandoOperacao" @click="salvarOperacao">Confirmar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ── Dialog abrir caixa ── -->
    <v-dialog v-model="dialogAbrir" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-text class="pa-6 text-center">
          <v-avatar color="success" size="64" class="mb-4">
            <v-icon icon="mdi-lock-open-variant-outline" size="36" color="white" />
          </v-avatar>
          <div class="text-h6 font-weight-bold mb-1">Abrir Caixa</div>
          <div class="text-body-2 text-medium-emphasis mb-5">
            {{ new Date().toLocaleDateString('pt-BR', { weekday:'long', day:'numeric', month:'long', year:'numeric' }) }}
          </div>
          <v-select v-model="abertura.localEstoqueId" :items="locais" item-title="nome" item-value="id"
            label="Local de Estoque *" variant="outlined" density="comfortable" class="mb-3 text-left"
            :rules="[r => !!r || 'Obrigatório']" />
          <v-text-field v-model.number="abertura.saldoAbertura" label="Fundo de troco inicial (R$)"
            type="number" variant="outlined" density="comfortable" prefix="R$" class="mb-4"
            hint="Valor em dinheiro que já está no caixa antes de começar as vendas" persistent-hint />
          <v-btn block color="success" size="large" rounded="lg"
            :loading="abrindo" @click="abrirCaixa" :disabled="!abertura.localEstoqueId">
            <v-icon start>mdi-lock-open-variant-outline</v-icon>
            Abrir Caixa
          </v-btn>
          <v-btn block variant="text" class="mt-2" @click="dialogAbrir = false">Cancelar</v-btn>
        </v-card-text>
      </v-card>
    </v-dialog>

    <!-- ── Dialog fechar caixa ── -->
    <v-dialog v-model="dialogFechar" max-width="560" persistent>
      <v-card rounded="xl">
        <v-card-text class="pa-6">
          <div class="d-flex align-center gap-3 mb-5">
            <v-avatar color="error" size="48">
              <v-icon icon="mdi-lock-outline" color="white" />
            </v-avatar>
            <div>
              <div class="text-h6 font-weight-bold">Fechar Caixa #{{ sessaoAtiva?.numero }}</div>
              <div class="text-caption text-medium-emphasis">
                Aberto em {{ fmtDt(sessaoAtiva?.abertura) }}
              </div>
            </div>
          </div>

          <!-- Resumo por forma de pagamento -->
          <div class="fech-resumo mb-4">
            <div class="fech-resumo-titulo">Resumo de Vendas</div>
            <div v-for="fp in formasPagamentoFechamento" :key="fp.label" class="fech-linha">
              <div class="d-flex align-center gap-2">
                <v-icon :icon="fp.icon" :color="fp.cor" size="16" />
                <span class="text-body-2 text-medium-emphasis">{{ fp.label }}</span>
              </div>
              <span class="text-body-2 font-weight-bold">R$ {{ fmt(fp.valor) }}</span>
            </div>
            <v-divider class="my-2" />
            <div class="fech-linha">
              <span class="text-body-2 font-weight-bold">Total de Vendas</span>
              <span class="text-body-2 font-weight-bold">R$ {{ fmt(sessaoAtiva?.totalVendas ?? 0) }}</span>
            </div>
            <div class="fech-linha" v-if="(sessaoAtiva?.totalCrediario ?? 0) > 0">
              <span class="text-caption text-medium-emphasis">
                Crediário é conta a receber — não entra na conferência do caixa.
              </span>
            </div>
            <div class="fech-linha" v-if="(sessaoAtiva?.totalSuprimentos ?? 0) > 0">
              <span class="text-body-2 text-secondary">+ Suprimentos</span>
              <span class="text-body-2 text-secondary">R$ {{ fmt(sessaoAtiva?.totalSuprimentos ?? 0) }}</span>
            </div>
            <div class="fech-linha" v-if="(sessaoAtiva?.totalSangrias ?? 0) > 0">
              <span class="text-body-2 text-warning">- Sangrias</span>
              <span class="text-body-2 text-warning">R$ {{ fmt(sessaoAtiva?.totalSangrias ?? 0) }}</span>
            </div>
            <v-divider class="my-2" />
            <div class="fech-linha">
              <span class="text-body-2 text-medium-emphasis">Saldo inicial (fundo)</span>
              <span class="text-body-2">R$ {{ fmt(sessaoAtiva?.saldoAbertura ?? 0) }}</span>
            </div>
            <div class="fech-linha">
              <div>
                <span class="text-body-2 font-weight-bold">Esperado em caixa (dinheiro)</span>
                <div class="text-caption text-medium-emphasis">
                  Fundo + vendas em dinheiro + suprimentos - sangrias
                </div>
              </div>
              <span class="text-body-1 font-weight-bold text-primary">R$ {{ fmt(saldoEsperado) }}</span>
            </div>
          </div>

          <!-- Contagem física -->
          <v-text-field v-model.number="fechamento.saldoContado"
            label="Saldo contado fisicamente no caixa (R$) *"
            type="number" variant="outlined" density="comfortable" prefix="R$" class="mb-2"
            autofocus @keyup.enter="fecharCaixa" />

          <v-text-field v-model="fechamento.observacao" label="Observação"
            variant="outlined" density="compact" class="mb-3" />

          <!-- Diferença -->
          <v-alert v-if="fechamento.saldoContado != null"
            :type="Math.abs(diferencaFechamento) < 0.01 ? 'success' : diferencaFechamento >= 0 ? 'info' : 'error'"
            variant="tonal" density="compact" rounded="lg">
            <div class="d-flex justify-space-between align-center">
              <span>
                {{ diferencaFechamento > 0.01 ? 'Sobra de caixa' : diferencaFechamento < -0.01 ? 'Falta de caixa' : 'Caixa conferido ✓' }}
              </span>
              <span class="font-weight-bold text-h6">R$ {{ fmt(Math.abs(diferencaFechamento)) }}</span>
            </div>
          </v-alert>

          <div class="d-flex gap-2 mt-4">
            <v-btn flex-grow-1 variant="text" @click="dialogFechar = false">Cancelar</v-btn>
            <v-btn flex-grow-1 color="error" size="large" rounded="lg"
              :loading="fechando" @click="fecharCaixa"
              :disabled="fechamento.saldoContado == null">
              <v-icon start>mdi-lock-outline</v-icon>
              Confirmar Fechamento
            </v-btn>
          </div>
        </v-card-text>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import FiltroMes from '@/components/FiltroMes.vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const carregando = ref(false)
const abrindo = ref(false)
const fechando = ref(false)
const salvandoOperacao = ref(false)
const dialogAbrir = ref(false)
const dialogFechar = ref(false)
const dialogOperacao = ref(false)
const tipoOperacao = ref<'suprimento' | 'sangria'>('suprimento')
const operacao = ref({ valor: 0, descricao: '' })
const sessoes = ref<any[]>([])
const sessaoAtiva = ref<any>(null)
const locais = ref<any[]>([])

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
})
const abertura = ref({ localEstoqueId: null as string | null, saldoAbertura: 0 })
const fechamento = ref({ saldoContado: null as number | null, observacao: '' })

// ── Saldo esperado em dinheiro no caixa físico ────────────────────
// = fundo inicial + suprimentos - sangrias + vendas pagas em dinheiro
// Se não tivermos breakdown de forma de pagamento, usamos totalVendas (conservador)
const saldoEsperado = computed(() => {
  if (!sessaoAtiva.value) return 0
  const s = sessaoAtiva.value
  const dinheiro = s.totalDinheiro ?? s.totalVendas ?? 0
  return (s.saldoAbertura ?? 0) + (s.totalSuprimentos ?? 0) - (s.totalSangrias ?? 0) + dinheiro
})

const diferencaFechamento = computed(() => {
  if (fechamento.value.saldoContado == null) return 0
  return fechamento.value.saldoContado - saldoEsperado.value
})

// ── Breakdown de formas de pagamento ─────────────────────────────
const formasPagamentoBanner = computed(() => {
  const s = sessaoAtiva.value
  if (!s) return []
  return [
    { label: 'Dinheiro',        icon: 'mdi-cash',               cor: 'success',  valor: s.totalDinheiro ?? 0 },
    { label: 'Pix',             icon: 'mdi-qrcode',             cor: 'teal',     valor: s.totalPix ?? 0 },
    { label: 'Cartão Débito',   icon: 'mdi-credit-card-outline',cor: 'primary',  valor: s.totalCartaoDebito ?? 0 },
    { label: 'Cartão Crédito',  icon: 'mdi-credit-card',        cor: 'indigo',   valor: s.totalCartaoCredito ?? 0 },
    { label: 'Crediário (a receber)', icon: 'mdi-account-credit-card-outline', cor: 'purple', valor: s.totalCrediario ?? 0 },
  ].filter(fp => fp.valor > 0 || !sessaoAtiva.value?.totalDinheiro)
})

const formasPagamentoFechamento = computed(() => {
  const s = sessaoAtiva.value
  if (!s) return []
  return [
    { label: 'Dinheiro',        icon: 'mdi-cash',               cor: 'success',  valor: s.totalDinheiro ?? 0 },
    { label: 'Pix',             icon: 'mdi-qrcode',             cor: 'teal',     valor: s.totalPix ?? 0 },
    { label: 'Cartão Débito',   icon: 'mdi-credit-card-outline',cor: 'primary',  valor: s.totalCartaoDebito ?? 0 },
    { label: 'Cartão Crédito',  icon: 'mdi-credit-card',        cor: 'indigo',   valor: s.totalCartaoCredito ?? 0 },
    { label: 'Crediário (a receber)', icon: 'mdi-account-credit-card-outline', cor: 'purple', valor: s.totalCrediario ?? 0 },
  ]
})

function calcDiferenca(item: any): number {
  if (item.saldoFechamento == null) return 0
  const dinheiro = item.totalDinheiro ?? item.totalVendas ?? 0
  const esperado = (item.saldoAbertura ?? 0) + (item.totalSuprimentos ?? 0) - (item.totalSangrias ?? 0) + dinheiro
  return item.saldoFechamento - esperado
}

const headers = [
  { title: '#',           key: 'numero',         width: 60 },
  { title: 'Operador',    key: 'operador',        width: 130 },
  { title: 'Local',       key: 'localEstoque',    width: 120 },
  { title: 'Abertura',    key: 'abertura',        width: 155 },
  { title: 'Fechamento',  key: 'fechamento',      width: 155 },
  { title: 'Fundo',       key: 'saldoAbertura',   width: 100 },
  { title: 'Vendas',      key: 'totalVendas',     width: 110 },
  { title: 'Sup/San',     key: 'movimentos',      width: 120, sortable: false },
  { title: 'Saldo Final', key: 'saldoFechamento', width: 110 },
  { title: 'Diferença',   key: 'diferenca',       width: 110 },
  { title: 'Status',      key: 'status',          width: 90  },
  { title: '',            key: 'acoes',           sortable: false, width: 50 },
]

const fmt   = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtDt = (v?: string) => v ? new Date(v).toLocaleString('pt-BR', { day:'2-digit', month:'2-digit', year:'numeric', hour:'2-digit', minute:'2-digit' }) : '—'
const fmtHora = (v?: string) => v ? new Date(v).toLocaleTimeString('pt-BR', { hour:'2-digit', minute:'2-digit' }) : '—'

function abrirOperacao(tipo: 'suprimento' | 'sangria') {
  tipoOperacao.value = tipo
  operacao.value = { valor: 0, descricao: '' }
  dialogOperacao.value = true
}

function prepararFechamento() {
  fechamento.value = { saldoContado: null, observacao: '' }
  dialogFechar.value = true
}

async function carregarSessaoAtiva() {
  try {
    const r = await api.get('/pdv/sessoes/aberta', {
      params: { empresaId: auth.empresaId },
    })
    sessaoAtiva.value = r.data ?? null
  } catch { sessaoAtiva.value = null }
}

async function listar() {
  carregando.value = true
  try {
    const r = await api.get('/pdv/sessoes', {
      params: { empresaId: auth.empresaId, ...filtros.value },
    })
    sessoes.value = r.data ?? []
  } finally { carregando.value = false }
}

async function abrirCaixa() {
  if (!abertura.value.localEstoqueId) { notif.aviso('Selecione o local de estoque.'); return }
  abrindo.value = true
  try {
    await api.post('/pdv/sessoes/abrir', {
      empresaId: auth.empresaId,
      usuarioId: auth.usuario?.id,
      localEstoqueId: abertura.value.localEstoqueId,
      saldoAbertura: abertura.value.saldoAbertura,
    })
    notif.ok('Caixa aberto com sucesso!')
    dialogAbrir.value = false
    await Promise.all([carregarSessaoAtiva(), listar()])
  } catch (e: any) {
    notif.erro(e?.response?.data?.title ?? 'Erro ao abrir caixa.')
  } finally { abrindo.value = false }
}

async function fecharCaixa() {
  if (!sessaoAtiva.value || fechamento.value.saldoContado == null) return
  fechando.value = true
  const sessaoId = sessaoAtiva.value.id
  try {
    await api.post(`/pdv/sessoes/${sessaoId}/fechar`, {
      saldoFechamento: fechamento.value.saldoContado,
      diferencaCaixa: diferencaFechamento.value,
      observacao: fechamento.value.observacao || null,
    })
    notif.ok('Caixa fechado com sucesso!')
    dialogFechar.value = false
    fechamento.value = { saldoContado: null, observacao: '' }
    await Promise.all([carregarSessaoAtiva(), listar()])
    imprimirFechamento(sessaoId)   // imprime o cupom de fechamento ao fechar
  } catch (e: any) {
    notif.erro(e?.response?.data?.title ?? 'Erro ao fechar caixa.')
  } finally { fechando.value = false }
}

// Imprime o fechamento de caixa (cupom térmico) via impressora padrão.
async function imprimirFechamento(sessaoId: string) {
  try {
    const r = await api.get(`/fiscal/recibo/sessao/${sessaoId}`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data)
    let iframe = document.getElementById('sessao-print-frame') as HTMLIFrameElement | null
    if (!iframe) {
      iframe = document.createElement('iframe')
      iframe.id = 'sessao-print-frame'
      iframe.style.cssText = 'position:fixed;right:0;bottom:0;width:0;height:0;border:0;'
      document.body.appendChild(iframe)
    }
    iframe.src = url
    iframe.onload = () => {
      setTimeout(() => {
        try { iframe!.contentWindow?.focus(); iframe!.contentWindow?.print() }
        catch { window.open(url, '_blank') }
        setTimeout(() => URL.revokeObjectURL(url), 60000)
      }, 300)
    }
  } catch {
    notif.erro('Não foi possível gerar o cupom de fechamento.')
  }
}

async function salvarOperacao() {
  if (!sessaoAtiva.value || !operacao.value.valor) {
    notif.aviso('Informe o valor da operação.')
    return
  }
  salvandoOperacao.value = true
  try {
    await api.post(`/caixa/sessoes/${sessaoAtiva.value.id}/${tipoOperacao.value}`, {
      valor: operacao.value.valor,
      descricao: operacao.value.descricao || null,
    })
    notif.ok(`${tipoOperacao.value === 'suprimento' ? 'Suprimento' : 'Sangria'} registrado!`)
    dialogOperacao.value = false
    await carregarSessaoAtiva()
  } catch (e: any) {
    notif.erro(e?.response?.data?.title ?? 'Erro ao registrar operação.')
  } finally { salvandoOperacao.value = false }
}

onMounted(async () => {
  const [, loc] = await Promise.all([
    Promise.all([carregarSessaoAtiva(), listar()]),
    api.get('/locais-estoque', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] })),
  ])
  locais.value = loc.data ?? []
})
</script>

<style scoped>
.fech-resumo {
  background: #f8f9fb;
  border: 1px solid #e8edf3;
  border-radius: 12px;
  padding: 16px;
}
.fech-resumo-titulo {
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  color: rgb(var(--v-theme-primary));
  margin-bottom: 12px;
}
.fech-linha {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 4px 0;
}
</style>
