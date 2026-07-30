<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Posição de Estoque</h2></v-col>
      <v-col cols="auto">
        <v-btn prepend-icon="mdi-refresh" variant="tonal" @click="carregar">Atualizar</v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="posicao-estoque"
      titulo="Como usar a Posição de Estoque"
      :passos="[
        'Os cards no topo somam o total de produtos, quantos estão <b>abaixo do mínimo</b>, o custo total e o valor de venda do estoque.',
        'Filtre por <b>produto</b>, <b>categoria</b> ou ative <b>Apenas abaixo do mínimo</b>. Itens em falta aparecem com o estoque em <b>vermelho</b>.',
        'Use o ícone <b>⚙ Ajustar estoque</b> na linha para fazer uma <b>contagem/inventário</b>: informe a quantidade real e o sistema registra a diferença como movimentação.',
        'Para editar preços, custos e mínimos do produto, vá em <b>Estoque → Produtos</b>. Esta tela é a fotografia atual do estoque.',
      ]"
    />

    <!-- Totalizadores -->
    <v-row class="mb-4">
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h5 font-weight-bold text-primary">{{ totais.qtdProdutos }}</div>
          <div class="text-caption text-medium-emphasis">Produtos</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h5 font-weight-bold text-warning">{{ totais.qtdAbaixoMinimo }}</div>
          <div class="text-caption text-medium-emphasis">Abaixo do Mínimo</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-success">R$ {{ fmt(totais.custoTotalEstoque) }}</div>
          <div class="text-caption text-medium-emphasis">Custo Total</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold">R$ {{ fmt(totais.valorVendaTotalEstoque) }}</div>
          <div class="text-caption text-medium-emphasis">Valor de Venda</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" md="4">
          <v-text-field v-model="busca" placeholder="Buscar produto…" prepend-inner-icon="mdi-magnify"
            variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" md="3">
          <v-select v-model="filtroCategoria" :items="categorias" item-title="nome" item-value="id"
            label="Categoria" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" md="2">
          <v-select v-model="filtroLoja" :items="locaisEstoque" item-title="nome" item-value="id"
            label="Loja" variant="outlined" density="compact" hide-details clearable
            prepend-inner-icon="mdi-store-outline"
            placeholder="Todas" @update:model-value="carregar" />
        </v-col>
        <v-col cols="auto">
          <v-switch v-model="apenasAbaixoMinimo" label="Apenas abaixo do mínimo"
            color="warning" density="compact" hide-details inset />
        </v-col>
      </v-row>
    </v-card>

    <v-alert v-if="filtroLoja" type="info" variant="tonal" density="compact" rounded="lg" class="mb-3">
      Mostrando o estoque da loja <b>{{ locaisEstoque.find(l => l.id === filtroLoja)?.nome }}</b> —
      a coluna "Estoque Atual" é o saldo <b>nesta loja</b> (reconstruído do histórico). Limpe o filtro para ver o total de todas as lojas.
    </v-alert>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="produtosFiltrados" :loading="carregando"
        density="compact" hover items-per-page="25">
        <template #item.estoqueAtual="{ item }">
          <v-chip :color="item.abaixoMinimo ? 'error' : 'success'" size="small" variant="tonal">
            {{ item.estoqueAtual }}
          </v-chip>
        </template>
        <template #item.custoTotal="{ item }">R$ {{ fmt(item.custoTotal) }}</template>
        <template #item.valorVendaTotal="{ item }">R$ {{ fmt(item.valorVendaTotal) }}</template>
        <template #item.custoUnitario="{ item }">R$ {{ fmt(item.custoUnitario) }}</template>
        <template #item.precoVenda="{ item }">R$ {{ fmt(item.precoVenda) }}</template>
        <template #item.acoes="{ item }">
          <v-btn icon="mdi-tune-vertical" size="x-small" variant="text" color="primary"
            title="Ajustar estoque (contagem)" @click="abrirAjuste(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog: ajuste rápido de estoque (inventário) -->
    <v-dialog v-model="dlgAjuste" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2">
          <v-icon icon="mdi-tune-vertical" color="primary" /> Ajustar Estoque
        </v-card-title>
        <v-card-text class="pa-4 pt-2" v-if="ajuste.produto">
          <div class="text-body-2 mb-3">
            <strong>{{ ajuste.produto.descricao }}</strong>
            <div class="text-caption text-medium-emphasis">
              Estoque atual: <b>{{ ajuste.produto.estoqueAtual }}</b>
            </div>
          </div>
          <v-select v-model="ajuste.localEstoqueId" :items="locaisEstoque" item-title="nome" item-value="id"
            label="Local de estoque *" variant="outlined" density="compact" class="mb-2" />
          <v-text-field v-model.number="ajuste.quantidadeContada" label="Quantidade contada (real) *"
            type="number" variant="outlined" density="compact" class="mb-2"
            hint="O sistema calcula e registra a diferença" persistent-hint />
          <v-text-field v-model="ajuste.observacao" label="Motivo / Observação"
            variant="outlined" density="compact" />
          <v-alert v-if="ajuste.quantidadeContada != null" type="info" variant="tonal" density="compact" class="mt-3">
            Diferença: <b>{{ (ajuste.quantidadeContada - (ajuste.produto.estoqueAtual ?? 0)).toFixed(2) }}</b>
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgAjuste = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvando" :disabled="!ajuste.localEstoqueId" @click="salvarAjuste">
            Confirmar
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
const salvando = ref(false)
const produtos = ref<any[]>([])
const categorias = ref<any[]>([])
const locaisEstoque = ref<any[]>([])
const totais = ref<any>({ qtdProdutos: 0, qtdAbaixoMinimo: 0, custoTotalEstoque: 0, valorVendaTotalEstoque: 0 })
const busca = ref('')
const filtroCategoria = ref<string | null>(null)
const filtroLoja = ref<string | null>(null)
const apenasAbaixoMinimo = ref(false)

const dlgAjuste = ref(false)
const ajuste = ref<any>({ produto: null, localEstoqueId: null, quantidadeContada: 0, observacao: '' })

const headers = [
  { title: 'Código', key: 'codigo', width: 110 },
  { title: 'Descrição', key: 'descricao', sortable: true },
  { title: 'Estoque Atual', key: 'estoqueAtual', width: 120 },
  { title: 'Mínimo', key: 'estoqueMinimo', width: 90 },
  { title: 'Custo Unit.', key: 'custoUnitario', width: 110 },
  { title: 'Preço Venda', key: 'precoVenda', width: 110 },
  { title: 'Custo Total', key: 'custoTotal', width: 120 },
  { title: 'Val. Venda', key: 'valorVendaTotal', width: 120 },
  { title: '', key: 'acoes', sortable: false, width: 50 },
]

const produtosFiltrados = computed(() => {
  let lista = produtos.value
  if (busca.value) {
    const q = busca.value.toLowerCase()
    lista = lista.filter(p => p.descricao?.toLowerCase().includes(q) || p.codigo?.toLowerCase().includes(q))
  }
  if (filtroCategoria.value) lista = lista.filter(p => p.categoriaId === filtroCategoria.value)
  if (apenasAbaixoMinimo.value) lista = lista.filter(p => p.abaixoMinimo)
  return lista
})

function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }

async function carregar() {
  carregando.value = true
  try {
    // Com loja selecionada → posição daquela loja (saldo reconstruído do histórico).
    // Sem loja → posição global (soma de todas as lojas), como antes.
    const posReq = filtroLoja.value
      ? api.get('/estoque/posicao-por-loja', { params: { empresaId: auth.empresaId, localEstoqueId: filtroLoja.value } })
      : api.get('/estoque/posicao', { params: { empresaId: auth.empresaId } })
    const [pos, cat, loc] = await Promise.all([
      posReq,
      api.get('/categorias', { params: { empresaId: auth.empresaId } }),
      api.get('/locais-estoque', { params: { empresaId: auth.empresaId } }),
    ])
    // Na visão por loja, o campo "estoqueAtual" da tabela passa a ser o saldo NA loja.
    produtos.value = (pos.data.produtos ?? []).map((p: any) =>
      filtroLoja.value ? { ...p, estoqueAtual: p.saldoLoja } : p)
    totais.value = pos.data.totais ?? totais.value
    categorias.value = cat.data
    locaisEstoque.value = loc.data ?? []
  } finally { carregando.value = false }
}

function abrirAjuste(produto: any) {
  const local = locaisEstoque.value.find((l: any) => l.principal)?.id ?? locaisEstoque.value[0]?.id ?? null
  ajuste.value = { produto, localEstoqueId: local, quantidadeContada: produto.estoqueAtual ?? 0, observacao: '' }
  dlgAjuste.value = true
}

async function salvarAjuste() {
  if (!ajuste.value.localEstoqueId) { notif.erro('Selecione o local de estoque.'); return }
  salvando.value = true
  try {
    await api.post('/ajuste-estoque/unitario', {
      empresaId: auth.empresaId,
      produtoId: ajuste.value.produto.id,
      localEstoqueId: ajuste.value.localEstoqueId,
      quantidadeContada: ajuste.value.quantidadeContada,
      usuarioId: auth.usuario?.id,
      observacao: ajuste.value.observacao || null,
    })
    notif.ok('Estoque ajustado!')
    dlgAjuste.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao ajustar estoque.')
  } finally { salvando.value = false }
}

onMounted(carregar)
</script>
