<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div>
        <div class="text-h6 font-weight-bold">Controle de Validade</div>
        <div class="text-caption text-medium-emphasis">
          Leia o código de barras, informe a validade. O sistema monitora e cria promoções automáticas.
        </div>
      </div>
      <v-spacer />
      <v-btn variant="text" color="primary" prepend-icon="mdi-cog-outline"
        to="/estoque/validade/config">Configurações</v-btn>
    </div>

    <v-row>
      <!-- ─── Coluna esquerda: scanner + produto ─── -->
      <v-col cols="12" md="4">

        <!-- Scanner -->
        <v-card rounded="xl" elevation="1" class="mb-4">
          <v-card-text>
            <div class="text-body-2 font-weight-bold mb-2">
              <v-icon icon="mdi-barcode-scan" class="mr-1" />Ler Código de Barras
            </div>
            <v-text-field
              ref="barcodeInput"
              v-model="barcode"
              placeholder="Escaneie ou digite o código..."
              variant="outlined"
              density="compact"
              hide-details
              autofocus
              clearable
              prepend-inner-icon="mdi-qrcode-scan"
              @keyup.enter="buscarProduto"
              @click:clear="limpar"
              :loading="buscando"
            />
            <div class="text-caption text-medium-emphasis mt-1">
              Pressione Enter ou use um leitor USB/Bluetooth.
            </div>
          </v-card-text>
        </v-card>

        <!-- Card do produto identificado -->
        <v-card v-if="produto" rounded="xl" elevation="2" class="mb-4">
          <v-card-text>
            <div class="d-flex align-start gap-3 mb-3">
              <v-img v-if="produto.imagemUrl" :src="produto.imagemUrl"
                width="80" height="80" rounded="lg" cover class="flex-shrink-0" />
              <v-avatar v-else color="grey-lighten-2" size="80" rounded="lg">
                <v-icon icon="mdi-package-variant-closed" size="40" color="grey" />
              </v-avatar>
              <div class="flex-grow-1">
                <div class="text-body-1 font-weight-bold">{{ produto.descricao }}</div>
                <div class="text-caption text-medium-emphasis">{{ marca }}</div>
                <div class="text-caption">
                  <v-chip size="x-small" class="mr-1">{{ produto.codigo }}</v-chip>
                  <v-chip size="x-small" variant="tonal" color="primary">
                    R$ {{ fmt(produto.precoVenda) }}
                  </v-chip>
                </div>
              </div>
            </div>

            <!-- Lotes existentes -->
            <div v-if="lotes.length" class="mb-3">
              <div class="text-caption font-weight-medium mb-1">Lotes com validade cadastrada:</div>
              <v-chip v-for="l in lotes" :key="l.id" size="small" class="mr-1 mb-1"
                :color="statusLote(l.dataValidade)"
                variant="tonal"
                :title="l.numeroLote"
                @click="selecionarLote(l)">
                {{ l.numeroLote }} · {{ fmtData(l.dataValidade) }}
              </v-chip>
            </div>

            <!-- Campos para registrar -->
            <v-divider class="mb-3" />
            <div class="text-body-2 font-weight-medium mb-2">Registrar validade</div>
            <v-row dense>
              <v-col cols="12">
                <v-text-field v-model="form.numeroLote" label="Número do lote (opcional)"
                  variant="outlined" density="compact" hide-details class="mb-2"
                  placeholder="L20260101" />
              </v-col>
              <v-col cols="12">
                <v-text-field v-model="form.dataValidade" label="Data de validade *"
                  type="date" variant="outlined" density="compact" hide-details class="mb-2" />
              </v-col>
              <v-col cols="12">
                <v-text-field v-model.number="form.quantidade" label="Quantidade no lote"
                  type="number" variant="outlined" density="compact" hide-details />
              </v-col>
            </v-row>

            <!-- Preview da validade -->
            <v-alert v-if="form.dataValidade" class="mt-3" density="compact"
              :type="corAlertaPreview" variant="tonal" :icon="iconeAlertaPreview">
              <strong>{{ diasRestantes >= 0 ? diasRestantes + ' dias' : 'VENCIDO' }}</strong>
              para vencer
              <span v-if="diasRestantes <= cfg.diasAlertaVermelho && diasRestantes >= 0">
                — promoção de {{ cfg.descontoAutoPercent }}% será criada automaticamente
              </span>
              <span v-if="diasRestantes < 0"> — produto vencido!</span>
            </v-alert>
          </v-card-text>
          <v-card-actions class="pt-0 px-4 pb-4">
            <v-btn variant="text" @click="limpar">Cancelar</v-btn>
            <v-spacer />
            <v-btn color="primary" variant="flat" :loading="salvando"
              :disabled="!form.dataValidade" @click="registrar">
              <v-icon icon="mdi-check" class="mr-1" />Registrar
            </v-btn>
          </v-card-actions>
        </v-card>

        <!-- Estado vazio -->
        <v-card v-else rounded="xl" elevation="0" variant="outlined"
          class="text-center pa-6" style="border-style:dashed">
          <v-icon icon="mdi-barcode-scan" size="56" color="grey-lighten-1" class="mb-2" />
          <div class="text-body-2 text-medium-emphasis">
            Escaneie o código de barras do produto para iniciar.
          </div>
        </v-card>
      </v-col>

      <!-- ─── Coluna direita: painel ─── -->
      <v-col cols="12" md="8">

        <!-- Cards de resumo -->
        <v-row dense class="mb-3">
          <v-col v-for="s in resumoCards" :key="s.label" cols="6" sm="3">
            <v-card rounded="xl" elevation="1">
              <v-card-text class="pa-3 text-center">
                <v-icon :icon="s.icon" :color="s.cor" size="28" class="mb-1" />
                <div class="text-h5 font-weight-bold" :class="`text-${s.cor}`">{{ s.valor }}</div>
                <div class="text-caption">{{ s.label }}</div>
              </v-card-text>
            </v-card>
          </v-col>
        </v-row>

        <!-- Filtro de status -->
        <div class="d-flex gap-2 mb-3 flex-wrap">
          <v-chip v-for="f in filtros" :key="f.status"
            :color="f.cor" variant="tonal" size="small"
            :class="{ 'v-chip--selected': filtro === f.status }"
            class="cursor-pointer"
            @click="filtro = filtro === f.status ? '' : f.status">
            {{ f.label }}
            <v-badge :content="contar(f.status)" inline class="ml-1" />
          </v-chip>
          <v-chip size="small" variant="text" class="cursor-pointer"
            :class="{ 'text-primary': !filtro }" @click="filtro = ''">
            Todos ({{ painel.length }})
          </v-chip>
          <v-spacer />
          <v-text-field v-model="busca" placeholder="Buscar produto..."
            variant="outlined" density="compact" hide-details style="max-width:220px"
            prepend-inner-icon="mdi-magnify" clearable />
        </div>

        <!-- Lista de produtos próximos ao vencimento -->
        <v-card rounded="xl" elevation="1">
          <div v-if="carregando" class="text-center pa-6">
            <v-progress-circular indeterminate color="primary" />
          </div>
          <div v-else-if="!itensFiltrados.length" class="text-center pa-6 text-medium-emphasis">
            <v-icon icon="mdi-check-circle-outline" size="48" color="success" class="mb-2" />
            <div>Nenhum produto neste status.</div>
          </div>
          <v-list v-else lines="two" class="pa-0">
            <template v-for="(item, idx) in itensFiltrados" :key="item.loteId">
              <v-divider v-if="idx > 0" />
              <v-list-item :class="`validade-item validade-item--${item.status.toLowerCase()}`">
                <template #prepend>
                  <div class="validade-status-bar" :class="`bg-${corStatus(item.status)}`" />
                  <v-img v-if="item.imagemUrl" :src="item.imagemUrl"
                    width="44" height="44" rounded="lg" cover class="mx-2 flex-shrink-0" />
                  <v-avatar v-else color="grey-lighten-3" size="44" rounded="lg" class="mx-2">
                    <v-icon icon="mdi-package-variant-closed" color="grey" size="22" />
                  </v-avatar>
                </template>

                <v-list-item-title class="d-flex align-center gap-2">
                  <v-icon :icon="iconeStatus(item.status)"
                    :color="corStatus(item.status)" size="16" />
                  {{ item.descricao }}
                  <v-chip v-if="item.promoGerada" size="x-small" color="success" variant="tonal">
                    Promoção ativa
                  </v-chip>
                </v-list-item-title>
                <v-list-item-subtitle>
                  {{ item.marca }} · Lote {{ item.numeroLote }} ·
                  Vence {{ item.dataValidade }} ·
                  {{ item.quantidade }} un. · R$ {{ fmt(item.valorEstoque) }}
                </v-list-item-subtitle>

                <template #append>
                  <div class="text-right">
                    <div class="text-body-2 font-weight-bold"
                      :class="`text-${corStatus(item.status)}`">
                      {{ item.diasRestantes >= 0 ? item.diasRestantes + 'd' : 'VENCIDO' }}
                    </div>
                    <v-chip :color="corStatus(item.status)" size="x-small" variant="flat">
                      {{ item.status }}
                    </v-chip>
                  </div>
                </template>
              </v-list-item>
            </template>
          </v-list>
        </v-card>

        <!-- Valor em risco -->
        <v-card v-if="resumo" rounded="xl" elevation="0" variant="tonal" color="warning" class="mt-3">
          <v-card-text class="d-flex align-center pa-3">
            <v-icon icon="mdi-currency-brl" class="mr-2" />
            <span>Valor em risco (Urgente + Vermelho):</span>
            <strong class="ml-2">R$ {{ fmt(resumo.valorEmRisco) }}</strong>
            <v-spacer />
            <v-btn size="small" variant="text" @click="carregarPainel">
              <v-icon icon="mdi-refresh" />
            </v-btn>
          </v-card-text>
        </v-card>

      </v-col>
    </v-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

// ─── Scanner ─────────────────────────────────────────────────────────────────
const barcode = ref('')
const buscando = ref(false)
const produto = ref<any>(null)
const marca = ref('')
const lotes = ref<any[]>([])
const barcodeInput = ref<any>(null)

const form = ref({ numeroLote: '', dataValidade: '', quantidade: 1, loteId: null as string | null })

async function buscarProduto() {
  if (!barcode.value.trim()) return
  buscando.value = true
  try {
    const r = await api.get('/validade/produto', {
      params: { empresaId: auth.empresaId, barcode: barcode.value.trim() }
    })
    produto.value = r.data.produto
    marca.value = r.data.marca ?? ''
    lotes.value = r.data.lotes ?? []
    form.value = { numeroLote: '', dataValidade: '', quantidade: 1, loteId: null }
  } catch (e: any) {
    if (e.response?.status === 404)
      notif.erro(`Produto não encontrado para o código: ${barcode.value}`)
    else
      notif.erro('Erro ao buscar produto.')
  } finally {
    buscando.value = false
  }
}

function selecionarLote(lote: any) {
  form.value.loteId = lote.id
  form.value.numeroLote = lote.numeroLote
  form.value.dataValidade = lote.dataValidade?.slice(0, 10) ?? ''
  form.value.quantidade = lote.quantidade
}

function limpar() {
  produto.value = null
  marca.value = ''
  lotes.value = []
  barcode.value = ''
  form.value = { numeroLote: '', dataValidade: '', quantidade: 1, loteId: null }
  setTimeout(() => barcodeInput.value?.focus(), 100)
}

// ─── Salvar ───────────────────────────────────────────────────────────────────
const salvando = ref(false)

async function registrar() {
  if (!form.value.dataValidade) return
  salvando.value = true
  try {
    await api.post('/validade/registrar', {
      empresaId: auth.empresaId,
      produtoId: produto.value.id,
      dataValidade: form.value.dataValidade,
      loteId: form.value.loteId ?? undefined,
      numeroLote: form.value.numeroLote || undefined,
      quantidade: form.value.quantidade,
    })
    notif.ok('Validade registrada com sucesso!')
    limpar()
    carregarPainel()
  } catch {
    notif.erro('Erro ao registrar validade.')
  } finally {
    salvando.value = false
  }
}

// ─── Preview inline ───────────────────────────────────────────────────────────
const diasRestantes = computed(() => {
  if (!form.value.dataValidade) return 9999
  return Math.floor((new Date(form.value.dataValidade).getTime() - Date.now()) / 86400000)
})

const corAlertaPreview = computed(() => {
  if (diasRestantes.value < 0) return 'error'
  if (diasRestantes.value <= cfg.value.diasAlertaUrgente) return 'error'
  if (diasRestantes.value <= cfg.value.diasAlertaVermelho) return 'warning'
  if (diasRestantes.value <= cfg.value.diasAlertaAmarelo) return 'info'
  return 'success'
})

const iconeAlertaPreview = computed(() =>
  diasRestantes.value < 0 ? 'mdi-alert-circle' :
  diasRestantes.value <= cfg.value.diasAlertaUrgente ? 'mdi-alarm' :
  diasRestantes.value <= cfg.value.diasAlertaVermelho ? 'mdi-alert' :
  'mdi-information-outline'
)

// ─── Painel ───────────────────────────────────────────────────────────────────
const carregando = ref(false)
const painel = ref<any[]>([])
const resumo = ref<any>(null)
const cfg = ref({ diasAlertaAmarelo: 60, diasAlertaVermelho: 30, diasAlertaUrgente: 15, descontoAutoPercent: 30 })
const filtro = ref('')
const busca = ref('')

async function carregarPainel() {
  carregando.value = true
  try {
    const r = await api.get('/validade/painel', { params: { empresaId: auth.empresaId } })
    painel.value = r.data.itens
    resumo.value = r.data.resumo
    if (r.data.resumo.configuracao) Object.assign(cfg.value, r.data.resumo.configuracao)
  } catch {
    notif.erro('Erro ao carregar painel de validade.')
  } finally {
    carregando.value = false
  }
}

const filtros = [
  { status: 'Vencido',  label: '✖ Vencido',  cor: 'error'   },
  { status: 'Urgente',  label: '⚠ Urgente',  cor: 'error'   },
  { status: 'Vermelho', label: '🔴 Vermelho', cor: 'warning' },
  { status: 'Amarelo',  label: '🟡 Amarelo',  cor: 'amber'   },
]

const itensFiltrados = computed(() => {
  let r = painel.value.filter(i => i.status !== 'Ok')
  if (filtro.value) r = r.filter(i => i.status === filtro.value)
  if (busca.value) {
    const q = busca.value.toLowerCase()
    r = r.filter(i => i.descricao.toLowerCase().includes(q) || i.marca?.toLowerCase().includes(q))
  }
  return r
})

const contar = (status: string) => painel.value.filter(i => i.status === status).length

const resumoCards = computed(() => [
  { label: 'Vencidos',  valor: resumo.value?.vencidos  ?? 0, cor: 'error',   icon: 'mdi-close-circle-outline' },
  { label: 'Urgentes',  valor: resumo.value?.urgentes  ?? 0, cor: 'error',   icon: 'mdi-alarm'                },
  { label: 'Vermelhos', valor: resumo.value?.vermelhos ?? 0, cor: 'warning', icon: 'mdi-alert-outline'        },
  { label: 'Amarelos',  valor: resumo.value?.amarelos  ?? 0, cor: 'amber',   icon: 'mdi-alert-circle-outline' },
])

function corStatus(s: string) {
  return { Vencido:'error', Urgente:'error', Vermelho:'warning', Amarelo:'amber', Ok:'success' }[s] ?? 'grey'
}
function iconeStatus(s: string) {
  return { Vencido:'mdi-close-circle', Urgente:'mdi-alarm', Vermelho:'mdi-alert', Amarelo:'mdi-alert-circle-outline' }[s] ?? 'mdi-check'
}
function statusLote(data: string | null) {
  if (!data) return 'grey'
  const d = Math.floor((new Date(data).getTime() - Date.now()) / 86400000)
  if (d < 0) return 'error'
  if (d <= 15) return 'error'
  if (d <= 30) return 'warning'
  if (d <= 60) return 'amber'
  return 'success'
}

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtData = (d: string) => d ? new Date(d + 'T12:00:00').toLocaleDateString('pt-BR') : '—'

onMounted(carregarPainel)
</script>

<style>
.validade-item { position: relative; padding-left: 12px !important; }
.validade-status-bar {
  position: absolute; left: 0; top: 0; bottom: 0; width: 4px; border-radius: 4px 0 0 4px;
}
.cursor-pointer { cursor: pointer; }
</style>
