<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Sugestão de Compra</h2></v-col>
    </v-row>

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      Produtos <b>abaixo do mínimo</b> ou com <b>cobertura baixa</b> (poucos dias de estoque frente à
      venda média), com <b>quantidade sugerida</b> para durar o alvo de dias, agrupados por fornecedor.
    </v-alert>

    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="6" sm="3">
          <v-select v-model.number="dias" :items="[15,30,60,90]" label="Analisar últimos (dias)"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="6" sm="3">
          <v-text-field v-model.number="diasAlvo" label="Cobertura desejada (dias)" type="number" min="1"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="6" sm="3">
          <v-select v-model="lojaFiltro" :items="lojasOpcoes" item-title="nome" item-value="id"
            label="Falta na loja" variant="outlined" density="compact" hide-details clearable
            hint="Só itens zerados/negativos nessa loja" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-btn color="primary" variant="tonal" rounded="lg" :loading="carregando" @click="carregar">Calcular</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <v-row dense class="mb-2">
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Produtos a comprar</div>
          <div class="text-h5 font-weight-bold">{{ itens.length }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Custo estimado</div>
          <div class="text-h5 font-weight-bold">R$ {{ fmt(custoTotal) }}</div>
        </v-card>
      </v-col>
    </v-row>

    <div v-if="!carregando && !itens.length" class="text-center text-medium-emphasis pa-6">
      Nada a comprar com esses parâmetros. 🎉
    </div>

    <v-card v-for="grupo in porFornecedor" :key="grupo.fornecedor" rounded="xl" elevation="1" class="mb-3">
      <v-card-title class="text-body-1 font-weight-bold d-flex align-center py-2 flex-wrap ga-2">
        <v-checkbox-btn :model-value="grupoTodos(grupo)" :indeterminate="grupoAlguns(grupo)"
          density="compact" hide-details class="flex-grow-0" @update:model-value="v => toggleGrupo(grupo, !!v)" />
        <v-icon icon="mdi-truck-outline" class="mr-1" size="20" /> {{ grupo.fornecedor }}
        <v-spacer />
        <span class="text-body-2 mr-2">{{ grupoSelecionados(grupo).length }}/{{ grupo.itens.length }} sel. · R$ {{ fmt(grupoCustoSel(grupo)) }}</span>
        <v-btn size="small" color="primary" variant="tonal" rounded="lg" prepend-icon="mdi-cart-plus"
          :loading="gerando === grupo.fornecedor" :disabled="!grupoSelecionados(grupo).length"
          @click="gerarPedido(grupo)">Gerar pedido</v-btn>
      </v-card-title>
      <v-sheet v-if="!grupo.fornecedorId" color="warning" rounded="lg" variant="tonal"
        class="mx-3 mb-2 pa-2 d-flex align-center flex-wrap ga-2">
        <v-icon icon="mdi-link-variant-plus" size="18" />
        <span class="text-caption">Sem fornecedor. Marque os itens, escolha o fornecedor e vincule:</span>
        <v-spacer />
        <v-autocomplete v-model="vincForn" :items="forns" item-title="razaoSocial" item-value="id"
          label="Fornecedor" variant="outlined" density="compact" hide-details auto-select-first clearable
          style="min-width:260px" />
        <v-btn size="small" color="primary" variant="flat" rounded="lg" prepend-icon="mdi-check"
          :loading="vinculando" :disabled="!vincForn || !grupoSelecionados(grupo).length"
          @click="vincularFornecedor(grupo)">Vincular aos selecionados</v-btn>
      </v-sheet>
      <v-data-table :headers="headers" :items="grupo.itens" density="compact" hide-default-footer
        :items-per-page="-1">
        <template #item.sel="{ item }">
          <v-checkbox-btn v-model="sel[item.id]" density="compact" hide-details />
        </template>
        <template v-for="l in lojas" :key="l.localEstoqueId" #[`item.loja_${l.localEstoqueId}`]="{ item }">
          <span :class="lojaSaldo(item, l.localEstoqueId) < 0 ? 'text-error font-weight-bold' : 'font-weight-medium'">
            {{ fmtQtd(lojaSaldo(item, l.localEstoqueId)) }}
          </span>
        </template>
        <template #item.semLoja="{ item }">
          <span class="text-medium-emphasis font-italic">{{ fmtQtd(semLoja(item)) }}</span>
        </template>
        <template #item.estoqueAtual="{ item }">
          <span class="text-medium-emphasis">{{ fmtQtd(item.estoqueAtual) }}</span>
        </template>
        <template #item.vendaDia="{ item }">{{ fmtQtd(item.vendaDia) }}</template>
        <template #item.coberturaDias="{ item }">
          <span :class="item.abaixoMinimo ? 'text-error font-weight-bold' : ''">
            {{ item.coberturaDias == null ? '—' : item.coberturaDias + ' d' }}
          </span>
        </template>
        <template #item.quantidadeSugerida="{ item }">
          <v-text-field v-model.number="item.quantidadeSugerida" type="number" min="0"
            variant="outlined" density="compact" hide-details style="width:96px"
            @update:model-value="() => recalcCusto(item)" />
        </template>
        <template #item.custoSugerido="{ item }">R$ {{ fmt(item.quantidadeSugerida * item.custoUnitario) }}</template>
        <template #item.abaixoMinimo="{ item }">
          <v-chip v-if="item.abaixoMinimo" size="x-small" color="error" variant="tonal" label>abaixo do mín.</v-chip>
          <span v-else class="text-caption text-medium-emphasis">giro</span>
        </template>
      </v-data-table>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

interface Item {
  id: string; codigo: string; descricao: string; estoqueAtual: number; estoqueMinimo: number
  vendaDia: number; coberturaDias: number | null; abaixoMinimo: boolean
  quantidadeSugerida: number; custoUnitario: number; custoSugerido: number
  fornecedorId: string | null; fornecedor: string
  porLoja: { localEstoqueId: string; nome: string; saldo: number }[]
}

const carregando = ref(true)
const itens = ref<Item[]>([])
const custoTotal = ref(0)
const dias = ref(30)
const diasAlvo = ref(30)
const lojaFiltro = ref<string | null>(null)

const lojas = computed(() => itens.value[0]?.porLoja ?? [])
const lojasOpcoes = computed(() => lojas.value.map(l => ({ id: l.localEstoqueId, nome: l.nome })))

// Resíduo sem loja: saldo do cadastro não atribuído a nenhuma loja
// (ex.: saldo inicial importado sem movimentação). Faz as colunas fecharem com o Total.
const somaLojas = (item: Item) => item.porLoja.reduce((s, l) => s + (l.saldo ?? 0), 0)
const semLoja = (item: Item) => item.estoqueAtual - somaLojas(item)
const temSemLoja = computed(() => itens.value.some(i => Math.abs(semLoja(i)) > 0.001))

const headers = computed(() => [
  { title: '', key: 'sel', sortable: false, width: 48 },
  { title: 'Cód', key: 'codigo', width: 80 },
  { title: 'Produto', key: 'descricao' },
  ...lojas.value.map(l => ({ title: l.nome, key: `loja_${l.localEstoqueId}`, align: 'end' as const, sortable: false })),
  ...(temSemLoja.value ? [{ title: 'Sem loja', key: 'semLoja', align: 'end' as const, sortable: false, width: 80 }] : []),
  { title: 'Total', key: 'estoqueAtual', align: 'end' as const, width: 80 },
  { title: 'Venda/dia', key: 'vendaDia', align: 'end' as const },
  { title: 'Cobertura', key: 'coberturaDias', align: 'end' as const },
  { title: 'Situação', key: 'abaixoMinimo', width: 110, align: 'center' as const },
  { title: 'Comprar', key: 'quantidadeSugerida', align: 'end' as const },
  { title: 'Custo est.', key: 'custoSugerido', align: 'end' as const },
])

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtQtd = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 3 })
const lojaSaldo = (item: Item, localId: string) =>
  item.porLoja.find(l => l.localEstoqueId === localId)?.saldo ?? 0

const itensFiltrados = computed(() =>
  lojaFiltro.value
    ? itens.value.filter(i => lojaSaldo(i, lojaFiltro.value!) <= 0)   // falta nessa loja
    : itens.value)

interface Grupo { fornecedor: string; fornecedorId: string | null; itens: Item[]; custo: number }
const porFornecedor = computed<Grupo[]>(() => {
  const map = new Map<string, Grupo>()
  for (const it of itensFiltrados.value) {
    const g = map.get(it.fornecedor) ?? { fornecedor: it.fornecedor, fornecedorId: it.fornecedorId, itens: [], custo: 0 }
    g.itens.push(it); g.custo += it.custoSugerido
    map.set(it.fornecedor, g)
  }
  return [...map.values()].sort((a, b) => b.custo - a.custo)
})

// ── Seleção de itens e geração de pedido por fornecedor ──────────────
const sel = ref<Record<string, boolean>>({})
const gerando = ref<string | null>(null)

// ── Vincular fornecedor a produtos sem fornecedor ────────────────────
const forns = ref<any[]>([])
const vincForn = ref<string | null>(null)
const vinculando = ref(false)

async function carregarFornecedores() {
  try {
    const r = await api.get('/fornecedores', { params: { empresaId: auth.empresaId, ativo: true } })
    forns.value = Array.isArray(r.data) ? r.data : (r.data.itens ?? [])
  } catch { forns.value = [] }
}

async function vincularFornecedor(g: Grupo) {
  const itensSel = grupoSelecionados(g)
  if (!vincForn.value || !itensSel.length) return
  vinculando.value = true
  try {
    await Promise.all(itensSel.map(i =>
      api.patch(`/produtos/${i.id}/fornecedor`, { fornecedorId: vincForn.value })))
    const nome = forns.value.find(f => f.id === vincForn.value)?.razaoSocial ?? 'fornecedor'
    notif.ok(`${itensSel.length} produto(s) vinculado(s) a ${nome}.`)
    itensSel.forEach(i => { sel.value[i.id] = false })
    vincForn.value = null
    await carregar()   // recarrega para reagrupar sob o novo fornecedor
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao vincular o fornecedor.')
  } finally { vinculando.value = false }
}

const recalcCusto = (item: Item) => { item.custoSugerido = (item.quantidadeSugerida || 0) * item.custoUnitario }

const grupoSelecionados = (g: Grupo) => g.itens.filter(i => sel.value[i.id])
const grupoCustoSel = (g: Grupo) => grupoSelecionados(g).reduce((s, i) => s + i.quantidadeSugerida * i.custoUnitario, 0)
const grupoTodos = (g: Grupo) => g.itens.length > 0 && g.itens.every(i => sel.value[i.id])
const grupoAlguns = (g: Grupo) => g.itens.some(i => sel.value[i.id]) && !grupoTodos(g)
const toggleGrupo = (g: Grupo, val: boolean) => g.itens.forEach(i => { sel.value[i.id] = val })

async function gerarPedido(g: Grupo) {
  const itensSel = grupoSelecionados(g)
  if (!itensSel.length) return
  if (!g.fornecedorId) { notif.aviso('Este grupo não tem fornecedor vinculado. Vincule um fornecedor no cadastro do produto.'); return }
  if (itensSel.some(i => !i.quantidadeSugerida || i.quantidadeSugerida <= 0)) {
    notif.aviso('Há itens selecionados com quantidade zerada. Ajuste antes de gerar.'); return
  }
  gerando.value = g.fornecedor
  try {
    await api.post('/pedidos-compra', {
      empresaId: auth.empresaId,
      fornecedorId: g.fornecedorId,
      usuarioId: auth.usuario?.id,
      itens: itensSel.map(i => ({
        produtoId: i.id, descricao: i.descricao,
        quantidade: i.quantidadeSugerida, precoUnitario: i.custoUnitario,
      })),
    })
    notif.ok(`Pedido criado para ${g.fornecedor} com ${itensSel.length} item(ns). Veja em Compras › Pedido de Compra.`)
    itensSel.forEach(i => { sel.value[i.id] = false })
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao gerar o pedido de compra.')
  } finally { gerando.value = null }
}

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<{ itens: Item[]; custoTotal: number }>('/relatorios/estoque/sugestao-compra', {
      params: { empresaId: auth.empresaId, dias: dias.value, diasAlvo: diasAlvo.value },
    })
    itens.value = res.data.itens ?? []
    custoTotal.value = res.data.custoTotal ?? 0
  } catch { itens.value = []; custoTotal.value = 0 } finally { carregando.value = false }
}

onMounted(() => { carregar(); carregarFornecedores() })
</script>
