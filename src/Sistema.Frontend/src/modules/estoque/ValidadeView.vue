<template>
  <div>
    <div class="d-flex align-center mb-3">
      <div>
        <div class="text-h6 font-weight-bold">Controle de Validade</div>
        <div class="text-caption text-medium-emphasis">
          Monitore vencimentos, registre lotes e gerencie o estoque por validade.
        </div>
      </div>
      <v-spacer />
      <v-btn variant="text" color="primary" prepend-icon="mdi-cog-outline"
        to="/estoque/validade/config">Configurações</v-btn>
    </div>

    <v-tabs v-model="aba" class="mb-4" bg-color="transparent">
      <v-tab value="painel">
        <v-icon start>mdi-view-dashboard-outline</v-icon>Monitoramento
      </v-tab>
      <v-tab value="registrar">
        <v-icon start>mdi-barcode-scan</v-icon>Registrar Validade
      </v-tab>
      <v-tab value="lotes">
        <v-icon start>mdi-package-variant</v-icon>Lotes
        <v-badge v-if="vencidos + proximos > 0" :content="vencidos + proximos"
          color="error" inline class="ml-1" />
      </v-tab>
    </v-tabs>

    <!-- ═══════════════════ ABA: MONITORAMENTO ═══════════════════ -->
    <div v-if="aba === 'painel'">
      <!-- Cards resumo -->
      <v-row class="mb-3">
        <v-col v-for="c in resumoCards" :key="c.label" cols="6" sm="3">
          <v-card rounded="xl" elevation="1"
            :class="c.valor > 0 ? `border-${c.cor}` : ''"
            style="cursor:pointer"
            @click="filtro = filtro === c.label.split(' ')[1] ? '' : c.label.split(' ')[1]">
            <v-card-text class="text-center pa-3">
              <v-icon :icon="c.icon" :color="c.cor" size="28" />
              <div class="text-h5 font-weight-bold mt-1" :class="`text-${c.cor}`">{{ c.valor }}</div>
              <div class="text-caption">{{ c.label }}</div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <!-- Filtros + busca -->
      <v-row class="mb-2" align="center">
        <v-col>
          <v-chip-group v-model="filtro" selected-class="v-chip--selected">
            <v-chip v-for="f in filtros" :key="f.status" :value="f.status"
              :color="f.cor" variant="tonal" size="small">{{ f.label }}</v-chip>
          </v-chip-group>
        </v-col>
        <v-col cols="12" sm="4">
          <v-text-field v-model="busca" placeholder="Buscar produto..."
            prepend-inner-icon="mdi-magnify" variant="outlined" density="compact" hide-details clearable />
        </v-col>
      </v-row>

      <v-card rounded="xl" elevation="1">
        <v-list v-if="itensFiltrados.length" lines="two">
          <template v-for="(item, idx) in itensFiltrados" :key="item.loteId">
            <v-divider v-if="idx > 0" />
            <v-list-item :class="`validade-item validade-item--${item.status.toLowerCase()}`">
              <template #prepend>
                <div class="validade-status-bar" :class="`bg-${corStatus(item.status)}`" />
                <v-icon :icon="iconeStatus(item.status)" :color="corStatus(item.status)" class="mr-2" />
              </template>
              <template #default>
                <div class="text-body-2 font-weight-medium">{{ item.descricao }}</div>
                <div class="text-caption text-medium-emphasis">
                  {{ item.marca }} · Lote {{ item.numeroLote }} ·
                  Vence {{ item.dataValidade }} ·
                  {{ item.quantidade }} un.
                  <v-chip v-if="item.promoGerada" size="x-small" color="success" class="ml-1">
                    Promo enviada
                  </v-chip>
                </div>
              </template>
              <template #append>
                <div class="text-right">
                  <div class="text-body-2 font-weight-bold" :class="`text-${corStatus(item.status)}`">
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
        <v-card-text v-else class="text-center text-medium-emphasis py-8">
          <v-icon icon="mdi-check-circle-outline" size="48" color="success" />
          <div class="mt-2">Nenhum produto com alerta de validade.</div>
        </v-card-text>
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
    </div>

    <!-- ═══════════════════ ABA: REGISTRAR VALIDADE ═══════════════════ -->
    <div v-if="aba === 'registrar'">
      <v-row>
        <!-- Coluna esquerda: scanner + produto -->
        <v-col cols="12" md="4">

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
                autofocus
                hide-details
                append-inner-icon="mdi-magnify"
                @keyup.enter="buscarProduto"
                @click:append-inner="buscarProduto"
                :loading="buscando" />
              <v-btn v-if="produto" variant="text" size="small" class="mt-2" @click="limpar">
                <v-icon start>mdi-close</v-icon>Limpar
              </v-btn>
            </v-card-text>
          </v-card>

          <!-- Card do produto -->
          <v-card v-if="produto" rounded="xl" elevation="1" class="mb-4">
            <v-card-text>
              <div class="text-body-2 font-weight-medium">{{ produto.descricao }}</div>
              <div class="text-caption text-medium-emphasis">{{ marca }} · {{ produto.codigo }}</div>

              <!-- Lotes existentes -->
              <div v-if="lotes.length" class="mb-3 mt-2">
                <div class="text-caption font-weight-medium mb-1">Lotes com validade cadastrada:</div>
                <v-chip v-for="l in lotes" :key="l.id" size="small" class="mr-1 mb-1"
                  :color="statusLote(l.dataValidade)"
                  variant="tonal"
                  :title="l.numeroLote"
                  @click="selecionarLote(l)">
                  {{ l.numeroLote }} · {{ fmtData(l.dataValidade) }}
                </v-chip>
              </div>

              <div class="text-body-2 font-weight-medium mb-2">Registrar validade</div>
              <v-row dense>
                <v-col cols="12">
                  <v-text-field v-model="form.numeroLote" label="Número do lote (opcional)"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="8">
                  <v-text-field v-model="form.dataValidade" label="Data de validade *"
                    type="date" variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="4">
                  <v-text-field v-model.number="form.quantidade" label="Qtd"
                    type="number" variant="outlined" density="compact" hide-details />
                </v-col>
              </v-row>

              <!-- Preview da validade -->
              <v-alert v-if="form.dataValidade" class="mt-3" density="compact"
                :color="corAlertaPreview" :icon="iconeAlertaPreview" variant="tonal">
                <span v-if="diasRestantes < 0">Produto já VENCIDO!</span>
                <span v-else>
                  Vence em <strong>{{ diasRestantes }} dias</strong>
                  <span v-if="diasRestantes <= cfg.diasAlertaVermelho">
                    — desconto automático de {{ cfg.descontoAutoPercent }}% será aplicado.
                  </span>
                </span>
              </v-alert>
            </v-card-text>
            <v-card-actions class="pa-3 pt-0">
              <v-spacer />
              <v-btn color="primary" :loading="salvando"
                :disabled="!form.dataValidade" @click="registrar">
                <v-icon start>mdi-content-save-outline</v-icon>Registrar
              </v-btn>
            </v-card-actions>
          </v-card>

          <v-card v-else rounded="xl" elevation="0" variant="tonal" color="info">
            <v-card-text class="text-center py-6">
              <v-icon icon="mdi-barcode-scan" size="48" color="info" />
              <div class="mt-2 text-body-2">Escaneie ou digite um código de barras para começar.</div>
            </v-card-text>
          </v-card>

        </v-col>

        <!-- Coluna direita: painel resumido -->
        <v-col cols="12" md="8">
          <v-row class="mb-3">
            <v-col v-for="c in resumoCards" :key="c.label" cols="6" sm="3">
              <v-card rounded="xl" elevation="1">
                <v-card-text class="text-center pa-2">
                  <v-icon :icon="c.icon" :color="c.cor" size="22" />
                  <div class="text-h6 font-weight-bold" :class="`text-${c.cor}`">{{ c.valor }}</div>
                  <div class="text-caption">{{ c.label }}</div>
                </v-card-text>
              </v-card>
            </v-col>
          </v-row>

          <v-card rounded="xl" elevation="1">
            <v-card-text class="pa-2">
              <v-text-field v-model="busca" placeholder="Buscar produto..."
                prepend-inner-icon="mdi-magnify" variant="outlined" density="compact"
                hide-details clearable class="mb-2" />
            </v-card-text>
            <v-list v-if="itensFiltrados.length" lines="two">
              <template v-for="(item, idx) in itensFiltrados" :key="item.loteId">
                <v-divider v-if="idx > 0" />
                <v-list-item :class="`validade-item validade-item--${item.status.toLowerCase()}`">
                  <template #prepend>
                    <div class="validade-status-bar" :class="`bg-${corStatus(item.status)}`" />
                    <v-icon :icon="iconeStatus(item.status)" :color="corStatus(item.status)" class="mr-2" />
                  </template>
                  <template #default>
                    <div class="text-body-2 font-weight-medium">{{ item.descricao }}</div>
                    <div class="text-caption text-medium-emphasis">
                      {{ item.marca }} · Lote {{ item.numeroLote }} ·
                      Vence {{ item.dataValidade }} · {{ item.quantidade }} un.
                    </div>
                  </template>
                  <template #append>
                    <div class="text-right">
                      <div class="text-body-2 font-weight-bold" :class="`text-${corStatus(item.status)}`">
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
            <v-card-text v-else class="text-center text-medium-emphasis py-6">
              Nenhum item com alerta de validade.
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </div>

    <!-- ═══════════════════ ABA: LOTES ═══════════════════ -->
    <div v-if="aba === 'lotes'">
      <v-row align="center" class="mb-3">
        <v-col>
          <v-alert v-if="vencidos > 0" type="error" variant="tonal" density="compact"
            :text="`${vencidos} lote(s) VENCIDO(S) — revise o estoque!`" class="mb-2" />
          <v-alert v-else-if="proximos > 0" type="warning" variant="tonal" density="compact"
            :text="`${proximos} lote(s) vence(m) nos próximos 30 dias.`" class="mb-2" />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNovoLote">Novo Lote</v-btn>
        </v-col>
      </v-row>

      <v-tabs v-model="abaLotes" class="mb-3" bg-color="transparent" density="compact">
        <v-tab value="todos">Todos os Lotes</v-tab>
        <v-tab value="vencimentos">
          Vencimentos
          <v-badge v-if="vencidos + proximos > 0" :content="vencidos + proximos"
            color="error" inline class="ml-1" />
        </v-tab>
      </v-tabs>

      <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
        <v-row dense align="center">
          <v-col cols="12" md="6">
            <v-text-field v-model="buscaLote" label="Buscar produto (nome ou código)"
              prepend-inner-icon="mdi-magnify" variant="outlined" density="compact" hide-details
              clearable @update:model-value="listarLotes" />
          </v-col>
          <v-col cols="12" md="3">
            <v-select v-model="filtroLocal" :items="locais" item-title="nome" item-value="id"
              label="Local de estoque" variant="outlined" density="compact" hide-details clearable
              @update:model-value="listarLotes" />
          </v-col>
          <v-col cols="auto">
            <v-btn color="primary" variant="tonal" @click="listarLotes">Buscar</v-btn>
          </v-col>
        </v-row>
      </v-card>

      <v-card v-if="abaLotes === 'todos'" rounded="xl" elevation="1">
        <v-data-table :headers="headersLotes" :items="lotesLista" :loading="carregandoLotes"
          density="compact" hover items-per-page="25">
          <template #item.dataValidade="{ item }">
            <v-chip v-if="item.dataValidade"
              :color="item.vencido ? 'error' : item.venceEm30 ? 'warning' : 'success'"
              size="small" variant="tonal">
              {{ fmtData(item.dataValidade) }}
            </v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.custoUnitario="{ item }">R$ {{ fmt(item.custoUnitario) }}</template>
          <template #item.acoes="{ item }">
            <v-btn icon size="small" variant="text" color="primary"
              title="Transferir para filial" @click="abrirTransferencia(item)">
              <v-icon>mdi-transfer</v-icon>
            </v-btn>
          </template>
        </v-data-table>
      </v-card>

      <v-card v-if="abaLotes === 'vencimentos'" rounded="xl" elevation="1">
        <v-data-table :headers="headersVenc" :items="alertasVenc" :loading="carregandoVenc"
          density="compact" hover items-per-page="25">
          <template #item.dataValidade="{ item }">
            <v-chip :color="item.vencido ? 'error' : 'warning'" size="small" variant="tonal">
              {{ fmtData(item.dataValidade) }}
            </v-chip>
          </template>
          <template #item.status="{ item }">
            <v-chip :color="item.vencido ? 'error' : 'warning'" size="small">
              {{ item.vencido ? 'VENCIDO' : 'Próximo' }}
            </v-chip>
          </template>
          <template #item.acoes="{ item }">
            <v-btn icon size="small" variant="text" color="primary"
              title="Transferir para filial" @click="abrirTransferencia(item)">
              <v-icon>mdi-transfer</v-icon>
            </v-btn>
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- Dialog novo lote -->
    <v-dialog v-model="dialogLote" max-width="600" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">Novo Lote</v-card-title>
        <v-card-text>
          <v-form ref="formularioLote">
            <v-row dense>
              <v-col cols="12">
                <v-text-field v-model="formLote.buscaProduto" label="Produto (código ou nome) *"
                  variant="outlined" density="compact" @update:model-value="buscarProdutoLote"
                  :loading="buscandoProdutoLote" />
                <v-list v-if="produtosSugeridos.length" elevation="2" rounded="lg" class="mt-1 mb-2">
                  <v-list-item v-for="p in produtosSugeridos" :key="p.id"
                    :title="p.descricao" :subtitle="p.codigo"
                    @click="selecionarProdutoLote(p)" hover />
                </v-list>
                <div v-if="formLote.produtoId" class="text-success text-body-2 mb-2">
                  ✓ {{ formLote.produtoNome }}
                </div>
              </v-col>
              <v-col cols="12" md="6">
                <v-select v-model="formLote.localEstoqueId" :items="locais"
                  item-title="nome" item-value="id"
                  label="Local de Estoque *" variant="outlined" density="compact"
                  :rules="[r => !!r || 'Obrigatório']" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="formLote.numeroLote" label="Número do Lote *"
                  variant="outlined" density="compact" :rules="[r => !!r || 'Obrigatório']" />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field v-model.number="formLote.quantidade" label="Quantidade *" type="number"
                  variant="outlined" density="compact" :rules="[r => r > 0 || 'Inválido']" />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field v-model.number="formLote.custoUnitario" label="Custo Unitário"
                  type="number" variant="outlined" density="compact" prefix="R$" />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field v-model="formLote.dataValidade" label="Validade" type="date"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="formLote.dataFabricacao" label="Fabricação" type="date"
                  variant="outlined" density="compact" />
              </v-col>
            </v-row>
          </v-form>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogLote = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvandoLote" @click="salvarLote">Salvar Lote</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog transferência entre filiais -->
    <v-dialog v-model="dialogTransferencia" max-width="500" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon start color="primary">mdi-transfer</v-icon>
          Transferir para Filial
        </v-card-title>
        <v-card-text>
          <v-alert type="info" variant="tonal" density="compact" class="mb-4">
            <strong>{{ loteTransferindo?.descricao }}</strong><br>
            Lote {{ loteTransferindo?.numeroLote }} ·
            Disponível: <strong>{{ loteTransferindo?.quantidade }} un.</strong>
            <span v-if="loteTransferindo?.dataValidade">
              · Validade: {{ fmtData(loteTransferindo.dataValidade) }}
            </span>
          </v-alert>

          <v-select v-model="transferForm.empresaDestinoId"
            :items="filiaisDestino" item-title="nomeFantasia" item-value="id"
            label="Filial destino *" variant="outlined" density="compact"
            class="mb-3" prepend-inner-icon="mdi-store-outline"
            :loading="carregandoFiliais"
            @update:model-value="carregarLocaisDestino" />

          <v-select v-if="locaisDestino.length" v-model="transferForm.localDestinoId"
            :items="locaisDestino" item-title="nome" item-value="id"
            label="Local de estoque destino" variant="outlined" density="compact"
            class="mb-3" prepend-inner-icon="mdi-map-marker-outline" clearable />

          <v-row dense>
            <v-col cols="8">
              <v-text-field v-model.number="transferForm.quantidade"
                label="Quantidade a transferir *" type="number"
                :max="loteTransferindo?.quantidade"
                :min="0.001" variant="outlined" density="compact"
                prepend-inner-icon="mdi-package-variant" />
            </v-col>
            <v-col cols="4" class="d-flex align-center">
              <v-btn size="small" variant="tonal" color="warning"
                @click="transferForm.quantidade = loteTransferindo?.quantidade">
                Tudo
              </v-btn>
            </v-col>
          </v-row>

          <v-progress-linear v-if="loteTransferindo"
            :model-value="(transferForm.quantidade / loteTransferindo.quantidade) * 100"
            color="primary" height="6" rounded class="mb-3" />

          <v-textarea v-model="transferForm.observacao" label="Observação (opcional)"
            variant="outlined" density="compact" rows="2" auto-grow />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogTransferencia = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="transferindo"
            :disabled="!transferForm.empresaDestinoId || !transferForm.quantidade"
            @click="confirmarTransferencia">
            <v-icon start>mdi-send</v-icon>Transferir
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const aba = ref('painel')

// ─── Painel + filtros ────────────────────────────────────────────────────────
const carregando = ref(false)
const painel = ref<any[]>([])
const resumo = ref<any>(null)
const cfg = ref({ diasAlertaAmarelo: 60, diasAlertaVermelho: 30, diasAlertaUrgente: 15, descontoAutoPercent: 30 })
const filtro = ref('')
const busca = ref('')

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

const resumoCards = computed(() => [
  { label: 'Vencidos',  valor: resumo.value?.vencidos  ?? 0, cor: 'error',   icon: 'mdi-close-circle-outline' },
  { label: 'Urgentes',  valor: resumo.value?.urgentes  ?? 0, cor: 'error',   icon: 'mdi-alarm'                },
  { label: 'Vermelhos', valor: resumo.value?.vermelhos ?? 0, cor: 'warning', icon: 'mdi-alert-outline'        },
  { label: 'Amarelos',  valor: resumo.value?.amarelos  ?? 0, cor: 'amber',   icon: 'mdi-alert-circle-outline' },
])

async function carregarPainel() {
  carregando.value = true
  try {
    const r = await api.get('/validade/painel', { params: { empresaId: auth.empresaId } })
    painel.value = r.data.itens
    resumo.value = r.data.resumo
    if (r.data.resumo.configuracao) Object.assign(cfg.value, r.data.resumo.configuracao)
  } catch {
    notif.erro('Erro ao carregar painel de validade.')
  } finally { carregando.value = false }
}

// ─── Scanner / registrar ─────────────────────────────────────────────────────
const barcode = ref('')
const buscando = ref(false)
const produto = ref<any>(null)
const marca = ref('')
const lotes = ref<any[]>([])
const barcodeInput = ref<any>(null)
const salvando = ref(false)
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
      notif.erro(`Produto não encontrado: ${barcode.value}`)
    else
      notif.erro('Erro ao buscar produto.')
  } finally { buscando.value = false }
}

function selecionarLote(lote: any) {
  form.value.loteId = lote.id
  form.value.numeroLote = lote.numeroLote
  form.value.dataValidade = lote.dataValidade?.slice(0, 10) ?? ''
  form.value.quantidade = lote.quantidade
}

function limpar() {
  produto.value = null; marca.value = ''; lotes.value = []; barcode.value = ''
  form.value = { numeroLote: '', dataValidade: '', quantidade: 1, loteId: null }
  setTimeout(() => barcodeInput.value?.focus(), 100)
}

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
    notif.ok('Validade registrada!')
    limpar()
    carregarPainel()
  } catch {
    notif.erro('Erro ao registrar validade.')
  } finally { salvando.value = false }
}

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
  diasRestantes.value <= cfg.value.diasAlertaVermelho ? 'mdi-alert' : 'mdi-information-outline'
)

// ─── Aba Lotes ───────────────────────────────────────────────────────────────
const abaLotes = ref('todos')
const carregandoLotes = ref(false)
const carregandoVenc = ref(false)
const lotesLista = ref<any[]>([])
const alertasVenc = ref<any[]>([])
const locais = ref<any[]>([])
const buscaLote = ref('')
const filtroLocal = ref<string | null>(null)

const vencidos = computed(() => alertasVenc.value.filter(l => l.vencido).length)
const proximos = computed(() => alertasVenc.value.filter(l => !l.vencido).length)

const headersLotes = [
  { title: 'Produto',  key: 'descricao', sortable: true },
  { title: 'Nº Lote', key: 'numeroLote' },
  { title: 'Qtd.',    key: 'quantidade', width: 80 },
  { title: 'Custo',   key: 'custoUnitario', width: 110 },
  { title: 'Validade',key: 'dataValidade', width: 130 },
  { title: '',        key: 'acoes', width: 50, sortable: false },
]
const headersVenc = [
  { title: 'Produto',  key: 'descricao' },
  { title: 'Nº Lote', key: 'numeroLote' },
  { title: 'Qtd.',    key: 'quantidade', width: 80 },
  { title: 'Validade',key: 'dataValidade', width: 130 },
  { title: 'Status',  key: 'status', width: 110 },
  { title: '',        key: 'acoes', width: 50, sortable: false },
]

async function listarLotes() {
  carregandoLotes.value = true
  try {
    const params: any = { empresaId: auth.empresaId }
    if (filtroLocal.value) params.localEstoqueId = filtroLocal.value
    if (buscaLote.value) params.q = buscaLote.value
    const r = await api.get('/lotes', { params })
    lotesLista.value = r.data
  } finally { carregandoLotes.value = false }
}

async function listarVencimentos() {
  carregandoVenc.value = true
  try {
    const r = await api.get('/lotes/vencimentos', { params: { empresaId: auth.empresaId, diasAlerta: 30 } })
    alertasVenc.value = r.data
  } finally { carregandoVenc.value = false }
}

// Dialog novo lote
const dialogLote = ref(false)
const salvandoLote = ref(false)
const buscandoProdutoLote = ref(false)
const produtosSugeridos = ref<any[]>([])
const formularioLote = ref<any>(null)

const formLotePadrao = () => ({
  produtoId: null as string | null, produtoNome: '', buscaProduto: '',
  localEstoqueId: null as string | null,
  numeroLote: '', quantidade: 1, custoUnitario: 0,
  dataValidade: '', dataFabricacao: '',
})
const formLote = ref(formLotePadrao())

let timerBusca: any = null
async function buscarProdutoLote() {
  clearTimeout(timerBusca)
  if (!formLote.value.buscaProduto || formLote.value.buscaProduto.length < 2) {
    produtosSugeridos.value = []; return
  }
  timerBusca = setTimeout(async () => {
    buscandoProdutoLote.value = true
    try {
      const r = await api.get('/produtos/buscar', {
        params: { empresaId: auth.empresaId, q: formLote.value.buscaProduto }
      })
      produtosSugeridos.value = r.data
    } finally { buscandoProdutoLote.value = false }
  }, 300)
}

function selecionarProdutoLote(p: any) {
  formLote.value.produtoId = p.id
  formLote.value.produtoNome = p.descricao
  formLote.value.buscaProduto = p.descricao
  produtosSugeridos.value = []
}

function abrirNovoLote() {
  formLote.value = formLotePadrao()
  produtosSugeridos.value = []
  dialogLote.value = true
}

async function salvarLote() {
  const ok = await formularioLote.value?.validate()
  if (!ok?.valid || !formLote.value.produtoId) {
    notif.aviso('Selecione um produto válido.')
    return
  }
  salvandoLote.value = true
  try {
    await api.post('/lotes', {
      empresaId: auth.empresaId,
      produtoId: formLote.value.produtoId,
      localEstoqueId: formLote.value.localEstoqueId,
      numeroLote: formLote.value.numeroLote,
      quantidade: formLote.value.quantidade,
      custoUnitario: formLote.value.custoUnitario,
      dataValidade: formLote.value.dataValidade || null,
      dataFabricacao: formLote.value.dataFabricacao || null,
    })
    notif.ok('Lote registrado!')
    dialogLote.value = false
    await Promise.all([listarLotes(), listarVencimentos()])
  } catch (e: any) {
    notif.erro(e?.response?.data?.title ?? 'Erro ao salvar.')
  } finally { salvandoLote.value = false }
}

// ─── Transferência entre filiais ─────────────────────────────────────────────
const dialogTransferencia = ref(false)
const transferindo = ref(false)
const carregandoFiliais = ref(false)
const filiaisDestino = ref<any[]>([])
const locaisDestino = ref<any[]>([])
const loteTransferindo = ref<any>(null)

const transferFormPadrao = () => ({
  empresaDestinoId: null as string | null,
  localDestinoId: null as string | null,
  quantidade: 0,
  observacao: '',
})
const transferForm = ref(transferFormPadrao())

async function abrirTransferencia(lote: any) {
  loteTransferindo.value = lote
  transferForm.value = { ...transferFormPadrao(), quantidade: lote.quantidade }
  locaisDestino.value = []
  dialogTransferencia.value = true
  carregandoFiliais.value = true
  try {
    const r = await api.get('/transferencias/filiais-destino', {
      params: { empresaOrigemId: auth.empresaId }
    })
    filiaisDestino.value = r.data
  } catch { notif.erro('Erro ao carregar filiais.') }
  finally { carregandoFiliais.value = false }
}

async function carregarLocaisDestino(empresaId: string) {
  locaisDestino.value = []
  try {
    const r = await api.get('/locais-estoque', { params: { empresaId } })
    locaisDestino.value = r.data
  } catch {}
}

async function confirmarTransferencia() {
  if (!transferForm.value.empresaDestinoId || !transferForm.value.quantidade) return
  transferindo.value = true
  try {
    const r = await api.post('/transferencias/filial', {
      empresaOrigemId: auth.empresaId,
      empresaDestinoId: transferForm.value.empresaDestinoId,
      loteId: loteTransferindo.value.id,
      quantidade: transferForm.value.quantidade,
      localDestinoId: transferForm.value.localDestinoId ?? undefined,
      observacao: transferForm.value.observacao || undefined,
    })
    notif.ok(r.data.mensagem)
    dialogTransferencia.value = false
    await listarLotes()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro na transferência.')
  } finally { transferindo.value = false }
}

watch(abaLotes, v => { if (v === 'vencimentos') listarVencimentos() })
watch(aba, v => {
  if (v === 'lotes') { listarLotes(); listarVencimentos() }
  if (v === 'registrar') setTimeout(() => barcodeInput.value?.focus(), 200)
})

// ─── Helpers ─────────────────────────────────────────────────────────────────
function corStatus(s: string) {
  return { Vencido:'error', Urgente:'error', Vermelho:'warning', Amarelo:'amber', Ok:'success' }[s] ?? 'grey'
}
function iconeStatus(s: string) {
  return { Vencido:'mdi-close-circle', Urgente:'mdi-alarm', Vermelho:'mdi-alert', Amarelo:'mdi-alert-circle-outline' }[s] ?? 'mdi-check'
}
function statusLote(data: string | null) {
  if (!data) return 'grey'
  const d = Math.floor((new Date(data).getTime() - Date.now()) / 86400000)
  return d < 0 ? 'error' : d <= 15 ? 'error' : d <= 30 ? 'warning' : d <= 60 ? 'amber' : 'success'
}
const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtData = (d: string) => d ? new Date(d + 'T12:00:00').toLocaleDateString('pt-BR') : '—'

onMounted(async () => {
  const [, loc] = await Promise.all([
    carregarPainel(),
    api.get('/locais-estoque', { params: { empresaId: auth.empresaId } }),
  ])
  locais.value = loc.data
  await listarVencimentos()
})
</script>

<style>
.validade-item { position: relative; padding-left: 12px !important; }
.validade-status-bar {
  position: absolute; left: 0; top: 0; bottom: 0; width: 4px; border-radius: 4px 0 0 4px;
}
</style>
