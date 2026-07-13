<template>
  <div>
    <v-row align="center" class="mb-3">
      <v-col>
        <h2 class="text-h5 font-weight-bold">Alterar Preços</h2>
        <div class="text-body-2 text-medium-emphasis">
          Edite custo, preços e markups de todos os produtos em uma única tabela
        </div>
      </v-col>
      <v-col cols="auto">
        <v-btn color="success" variant="flat" size="large" :loading="salvando"
          :disabled="alterados.length === 0" @click="salvar">
          <v-icon start>mdi-content-save</v-icon>
          Salvar ({{ alterados.length }})
        </v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="alterar-precos"
      titulo="Como usar o Alterar Preços"
      :passos="[
        'Cada linha é um produto editável. Digite o <b>Custo</b>, <b>Markup</b> ou <b>Preço</b> direto na célula — o sistema recalcula os outros campos e a <b>Margem</b> automaticamente (Preço = Custo × Markup; Markup = Preço ÷ Custo).',
        'Use <b>Buscar produto</b> e <b>Categoria</b> para filtrar. Linhas alteradas ficam marcadas com um ponto laranja (●).',
        '<b>Ações em lote</b>: marque os checkboxes dos produtos, escolha uma ação (markup único, reajuste %, manter markup/preço) e clique em <b>Aplicar</b> para alterar vários de uma vez.',
        'Ao terminar, clique em <b>Salvar</b> (mostra quantos foram alterados). Use <b>Descartar</b> para desfazer as alterações não salvas. As mudanças são gravadas no cadastro do produto.',
      ]"
    />

    <!-- Filtros e ações em lote -->
    <v-card rounded="xl" elevation="1" class="pa-3 mb-3">
      <v-row dense align="center">
        <v-col cols="12" sm="4">
          <v-text-field v-model="busca" label="Buscar produto"
            variant="outlined" density="compact" clearable hide-details
            prepend-inner-icon="mdi-magnify" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-select v-model="catFiltro" label="Categoria"
            :items="categorias" item-title="nome" item-value="id"
            variant="outlined" density="compact" clearable hide-details />
        </v-col>
        <v-col cols="auto">
          <v-btn icon="mdi-refresh" variant="tonal" @click="carregar" :loading="carregando" />
        </v-col>
        <v-col cols="auto">
          <v-btn variant="text" color="error" :disabled="alterados.length === 0" @click="descartar">
            <v-icon start>mdi-undo</v-icon> Descartar
          </v-btn>
        </v-col>
      </v-row>

      <v-divider class="my-3" />

      <!-- Ações em lote -->
      <v-row dense align="center">
        <v-col cols="auto" class="text-body-2 font-weight-medium text-no-wrap">Lote nos selecionados:</v-col>
        <v-col cols="12" sm="3">
          <v-select v-model="opcaoLote" label="Ação"
            :items="opcoesLote" item-title="label" item-value="value"
            variant="outlined" density="compact" clearable hide-details />
        </v-col>
        <v-col cols="6" sm="2" v-if="opcaoLote === 'markup_unico' || opcaoLote === 'manter_markup'">
          <v-text-field v-model.number="markupLote" label="Markup"
            variant="outlined" density="compact" type="number" min="0.01" step="0.01" hide-details />
        </v-col>
        <v-col cols="6" sm="2" v-if="opcaoLote === 'reajuste_pct'">
          <v-text-field v-model.number="pctLote" label="% (ex: 10 ou -5)"
            variant="outlined" density="compact" type="number" step="0.1" hide-details />
        </v-col>
        <v-col cols="auto" v-if="opcaoLote">
          <v-btn color="primary" size="small"
            :disabled="selecionados.length === 0" @click="aplicarLote">
            Aplicar ({{ selecionados.length }})
          </v-btn>
        </v-col>
        <v-col cols="auto" class="text-caption text-medium-emphasis" v-if="selecionados.length === 0 && opcaoLote">
          Marque os checkboxes da tabela para selecionar
        </v-col>
      </v-row>
    </v-card>

    <!-- Legenda -->
    <div class="text-caption text-medium-emphasis mb-2">
      <strong>Custo</strong> = custo de aquisição ·
      <strong>P.Forn</strong> = preço de tabela do fornecedor ·
      <strong>Mkp Min / P.Mín</strong> = piso de venda ·
      <strong>Mkp</strong> = Preço ÷ Custo ·
      <strong>Mkp At.</strong> = markup do atacado
    </div>

    <!-- Tabela inline -->
    <v-card rounded="xl" elevation="1">
      <div style="overflow-x:auto">
        <table class="preco-table">
          <thead>
            <tr>
              <th style="width:32px">
                <v-checkbox density="compact" hide-details
                  :model-value="selecionados.length === filtrados.length && filtrados.length > 0"
                  :indeterminate="selecionados.length > 0 && selecionados.length < filtrados.length"
                  @update:model-value="toggleTodos" />
              </th>
              <th class="text-left" style="min-width:180px">Produto</th>
              <th style="width:110px">Custo</th>
              <th style="width:110px">P.Forn</th>
              <th style="width:90px">Mkp Min</th>
              <th style="width:110px">P.Mín</th>
              <th style="width:88px">Mkp</th>
              <th style="width:120px">Preço Varejo</th>
              <th style="width:120px">Preço Atacado</th>
              <th style="width:88px">Mkp At.</th>
              <th style="width:80px">Margem</th>
              <th style="width:20px"></th>
            </tr>
          </thead>
          <tbody v-if="carregando">
            <tr><td colspan="12" class="text-center pa-4 text-medium-emphasis">Carregando...</td></tr>
          </tbody>
          <tbody v-else-if="filtrados.length === 0">
            <tr><td colspan="12" class="text-center pa-4 text-medium-emphasis">Nenhum produto encontrado</td></tr>
          </tbody>
          <tbody v-else>
            <tr v-for="p in filtrados" :key="p.id" :class="{ 'row-alt': p._alt }">
              <td>
                <v-checkbox density="compact" hide-details
                  :model-value="selecionados.includes(p.id)"
                  @update:model-value="toggleSel(p.id)" />
              </td>
              <td>
                <div class="text-body-2 font-weight-medium">{{ p.descricao }}</div>
                <div v-if="p.referencia" class="text-caption text-medium-emphasis">{{ p.referencia }}</div>
              </td>

              <!-- Custo -->
              <td>
                <v-text-field v-model.number="p._custo" variant="outlined" density="compact"
                  type="number" min="0" step="0.01" hide-details prefix="R$"
                  @change="onCusto(p)" />
              </td>

              <!-- P.Forn -->
              <td>
                <v-text-field v-model.number="p._precoForn" variant="outlined" density="compact"
                  type="number" min="0" step="0.01" hide-details prefix="R$"
                  @change="p._alt = true" />
              </td>

              <!-- Mkp Min -->
              <td>
                <v-text-field v-model.number="p._mkpMin" variant="outlined" density="compact"
                  type="number" min="0" step="0.01" hide-details
                  @change="onMkpMin(p)" />
              </td>

              <!-- P.Mín -->
              <td>
                <v-text-field v-model.number="p._precoMin" variant="outlined" density="compact"
                  type="number" min="0" step="0.01" hide-details prefix="R$"
                  @change="p._alt = true" />
              </td>

              <!-- Mkp varejo -->
              <td>
                <v-text-field v-model.number="p._mkp" variant="outlined" density="compact"
                  type="number" min="0.01" step="0.01" hide-details
                  @change="onMkp(p)" />
              </td>

              <!-- Preço Varejo -->
              <td>
                <v-text-field v-model.number="p._preco" variant="outlined" density="compact"
                  type="number" min="0" step="0.01" hide-details prefix="R$"
                  @change="onPreco(p)" />
              </td>

              <!-- Preço Atacado -->
              <td>
                <v-text-field v-model.number="p._atacado" variant="outlined" density="compact"
                  type="number" min="0" step="0.01" hide-details prefix="R$"
                  placeholder="—"
                  @change="onAtacado(p)" />
              </td>

              <!-- Mkp Atacado -->
              <td>
                <v-text-field v-model.number="p._mkpAt" variant="outlined" density="compact"
                  type="number" min="0.01" step="0.01" hide-details
                  placeholder="—"
                  @change="onMkpAt(p)" />
              </td>

              <!-- Margem -->
              <td class="text-center">
                <v-chip
                  :color="p._margem >= 30 ? 'success' : p._margem >= 15 ? 'warning' : 'error'"
                  size="x-small" variant="tonal">
                  {{ p._margem?.toFixed(1) }}%
                </v-chip>
              </td>

              <!-- Alterado -->
              <td class="text-center">
                <v-icon v-if="p._alt" color="warning" size="12">mdi-circle</v-icon>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </v-card>

    <!-- Aviso: etiqueta desatualizada após alteração de preço (produtos por peso) -->
    <v-dialog v-model="dlgReimprimirEtiqueta" max-width="520">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2">
          <v-icon color="warning">mdi-tag-off-outline</v-icon>
          Etiqueta desatualizada
        </v-card-title>
        <v-card-text>
          <v-alert type="warning" variant="tonal" density="compact" class="mb-3">
            O preço {{ etiquetasReimprimir.length > 1 ? 'destes produtos foi alterado' : 'deste produto foi alterado' }}.
            É necessário imprimir uma nova etiqueta para atualizar as informações.
          </v-alert>
          <v-list density="compact">
            <v-list-item v-for="p in etiquetasReimprimir" :key="p.id"
              :title="p.descricao" :subtitle="`Novo preço: R$ ${fmt(p.precoVenda)}`" />
          </v-list>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgReimprimirEtiqueta = false">Depois</v-btn>
          <v-btn color="primary" rounded="lg" prepend-icon="mdi-printer"
            :loading="imprimindoEtiqueta" @click="reimprimirEtiquetas">
            Imprimir nova etiqueta
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import { imprimirEtiquetasKg } from '@/utils/etiquetaKg'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

// Reimpressão de etiquetas de produtos por peso após alteração de preço
const dlgReimprimirEtiqueta = ref(false)
const etiquetasReimprimir = ref<any[]>([])
const imprimindoEtiqueta = ref(false)

async function reimprimirEtiquetas() {
  imprimindoEtiqueta.value = true
  try {
    await imprimirEtiquetasKg(etiquetasReimprimir.value.map((p: any) => ({
      nome: p.descricao,
      codigoPlu: p.codigoPlu,
      precoVenda: p.precoVenda,
      descricao: p.descricaoComplementar,
    })))
    const ids = etiquetasReimprimir.value.map((p: any) => p.id)
    await api.post('/produtos/etiquetas-impressas', { ids }).catch(() => null)
    notif.ok('Etiquetas enviadas para impressão.')
    dlgReimprimirEtiqueta.value = false
  } finally { imprimindoEtiqueta.value = false }
}

const carregando = ref(false)
const salvando = ref(false)
const busca = ref('')
const catFiltro = ref<string | null>(null)
const categorias = ref<any[]>([])
const produtos = ref<any[]>([])
const selecionados = ref<string[]>([])
const opcaoLote = ref<string | null>(null)
const markupLote = ref(2.5)
const pctLote = ref(10)

const opcoesLote = [
  { label: 'Manter markup — recalcular preço pelo custo', value: 'manter_markup' },
  { label: 'Manter preços atuais', value: 'manter_preco' },
  { label: 'Definir markup único para todos', value: 'markup_unico' },
  { label: 'Reajuste % no preço de venda', value: 'reajuste_pct' },
]

const filtrados = computed(() => {
  let lista = produtos.value
  if (busca.value) {
    const q = busca.value.toLowerCase()
    lista = lista.filter(p => p.descricao.toLowerCase().includes(q) ||
      (p.referencia ?? '').toLowerCase().includes(q))
  }
  if (catFiltro.value) lista = lista.filter(p => p.categoriaId === catFiltro.value)
  return lista
})

const alterados = computed(() => produtos.value.filter(p => p._alt))

// ── Handlers de recálculo ──────────────────────────────────────────────

function onCusto(p: any) {
  p._alt = true
  if (p._custo > 0) {
    p._preco   = r2(p._custo * p._mkp)
    p._precoMin = r2(p._custo * (p._mkpMin || 1))
    if (p._atacado) p._mkpAt = r4(p._atacado / p._custo)
  }
  p._margem = margem(p._preco, p._custo)
}

function onMkpMin(p: any) {
  p._alt = true
  if (p._custo > 0) p._precoMin = r2(p._custo * p._mkpMin)
}

function onMkp(p: any) {
  p._alt = true
  if (p._custo > 0) p._preco = r2(p._custo * p._mkp)
  p._margem = margem(p._preco, p._custo)
}

function onPreco(p: any) {
  p._alt = true
  if (p._custo > 0) p._mkp = r4(p._preco / p._custo)
  p._margem = margem(p._preco, p._custo)
}

function onAtacado(p: any) {
  p._alt = true
  if (p._custo > 0 && p._atacado > 0) p._mkpAt = r4(p._atacado / p._custo)
}

function onMkpAt(p: any) {
  p._alt = true
  if (p._custo > 0 && p._mkpAt > 0) p._atacado = r2(p._custo * p._mkpAt)
}

// ── Seleção ───────────────────────────────────────────────────────────

function toggleSel(id: string) {
  const idx = selecionados.value.indexOf(id)
  if (idx >= 0) selecionados.value.splice(idx, 1)
  else selecionados.value.push(id)
}

function toggleTodos(v: boolean | null) {
  selecionados.value = v ? filtrados.value.map(p => p.id) : []
}

// ── Lote ──────────────────────────────────────────────────────────────

function aplicarLote() {
  const sel = produtos.value.filter(p => selecionados.value.includes(p.id))
  for (const p of sel) {
    if (opcaoLote.value === 'manter_markup') {
      const mk = markupLote.value || p._mkp
      p._mkp = mk
      if (p._custo > 0) p._preco = r2(p._custo * mk)
      p._margem = margem(p._preco, p._custo)
      p._alt = true
    } else if (opcaoLote.value === 'markup_unico') {
      p._mkp = markupLote.value
      if (p._custo > 0) p._preco = r2(p._custo * markupLote.value)
      p._margem = margem(p._preco, p._custo)
      p._alt = true
    } else if (opcaoLote.value === 'reajuste_pct') {
      const fator = 1 + pctLote.value / 100
      p._preco = r2(p._preco * fator)
      if (p._custo > 0) p._mkp = r4(p._preco / p._custo)
      p._margem = margem(p._preco, p._custo)
      p._alt = true
    }
    // manter_preco: nada
  }
}

// ── Descartar ─────────────────────────────────────────────────────────

function descartar() {
  produtos.value.forEach(p => {
    p._custo    = p.custoUnitario
    p._precoForn = p.precoFornecedor ?? 0
    p._mkpMin   = p.markupMinimo ?? 0
    p._precoMin  = p.precoMinimo ?? 0
    p._mkp      = p.markup
    p._preco    = p.precoVenda
    p._atacado  = p.precoAtacado ?? null
    p._mkpAt    = p.markupAtacado ?? null
    p._margem   = margem(p.precoVenda, p.custoUnitario)
    p._alt      = false
  })
  selecionados.value = []
}

// ── Carregar ──────────────────────────────────────────────────────────

async function carregar() {
  carregando.value = true
  try {
    const [resProd, resCat] = await Promise.all([
      api.get('/produtos', { params: { empresaId: auth.empresaId, pagina: 1, tamanhoPagina: 1000 } }),
      api.get('/categorias', { params: { empresaId: auth.empresaId } }),
    ])
    const itens: any[] = resProd.data?.itens ?? resProd.data ?? []
    produtos.value = itens.map(p => ({
      ...p,
      _custo:    p.custoUnitario,
      _precoForn: p.precoFornecedor ?? 0,
      _mkpMin:   p.markupMinimo ?? 0,
      _precoMin:  p.precoMinimo ?? 0,
      _mkp:      p.markup || 1,
      _preco:    p.precoVenda,
      _atacado:  p.precoAtacado ?? null,
      _mkpAt:    p.markupAtacado ?? null,
      _margem:   margem(p.precoVenda, p.custoUnitario),
      _alt:      false,
    }))
    categorias.value = resCat.data ?? []
  } catch { /* silencioso */ } finally {
    carregando.value = false
  }
}

// ── Salvar ────────────────────────────────────────────────────────────

async function salvar() {
  const lista = alterados.value
  if (!lista.length) return
  salvando.value = true
  try {
    const itens = lista.map(p => ({
      produtoId:          p.id,
      novoPrecoVenda:     p._preco,
      novoPrecoAtacado:   p._atacado || null,
      novoCusto:          p._custo,
      novoPrecoFornecedor: p._precoForn,
      novoMarkupMinimo:   p._mkpMin,
      novoPrecoMinimo:    p._precoMin,
      novoMarkupAtacado:  p._mkpAt || null,
    }))
    // Produtos por peso cujo preço de venda mudou → etiqueta desatualizada
    const pesoAlterados = lista.filter(p =>
      (p.produtoBalanca || p.vendidoFracionado) && p._preco !== p.precoVenda)

    await api.patch('/produtos/alterar-precos', { itens }, { params: { empresaId: auth.empresaId } })
    notif.ok(`${lista.length} produto(s) salvos.`)
    lista.forEach(p => {
      p.custoUnitario  = p._custo;  p.precoFornecedor = p._precoForn
      p.markupMinimo   = p._mkpMin; p.precoMinimo     = p._precoMin
      p.markup         = p._mkp;    p.precoVenda      = p._preco
      p.precoAtacado   = p._atacado; p.markupAtacado  = p._mkpAt
      p._alt = false
    })

    if (pesoAlterados.length) {
      etiquetasReimprimir.value = pesoAlterados
      dlgReimprimirEtiqueta.value = true
    }
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao salvar.')
  } finally { salvando.value = false }
}

// ── Utilitários ───────────────────────────────────────────────────────

const r2 = (v: number) => Math.round(v * 100) / 100
const r4 = (v: number) => Math.round(v * 10000) / 10000
const margem = (preco: number, custo: number) =>
  preco > 0 ? Math.round((preco - custo) / preco * 1000) / 10 : 0

onMounted(carregar)
</script>

<style scoped>
.preco-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}
.preco-table thead th {
  padding: 8px 6px;
  text-align: center;
  font-weight: 600;
  font-size: 12px;
  border-bottom: 2px solid rgba(0,0,0,0.12);
  white-space: nowrap;
  background: rgba(var(--v-theme-surface-variant), 0.4);
  position: sticky;
  top: 0;
  z-index: 1;
}
.preco-table thead th.text-left { text-align: left; }
.preco-table tbody tr td {
  padding: 4px 4px;
  border-bottom: 1px solid rgba(0,0,0,0.06);
  vertical-align: middle;
}
.preco-table tbody tr:hover td { background: rgba(var(--v-theme-primary), 0.04); }
.preco-table tbody tr.row-alt td { background: rgba(255,152,0,0.06); }
</style>
