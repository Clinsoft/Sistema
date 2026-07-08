<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Ajuste de Estoque</h2></v-col>
    </v-row>

    <GuiaPassos
      id="ajuste-estoque"
      titulo="Como usar o Ajuste de Estoque"
      :passos="[
        '<b>Ajuste Unitário</b>: use para corrigir <b>um</b> produto. Busque o produto, selecione o <b>local de estoque</b> e informe a <b>quantidade física real</b> contada.',
        'O sistema calcula a <b>diferença</b> sozinho e mostra se é ENTRADA (+) ou SAÍDA (−). Clique em <b>Aplicar Ajuste</b> para gravar — o estoque é atualizado na hora.',
        '<b>Inventário (Lote)</b>: escolha o <b>local</b>, clique em <b>Carregar Produtos</b>, digite as quantidades contadas de cada item e clique em <b>Aplicar Inventário</b>. Só os itens com diferença são ajustados.',
        'Cada ajuste gera uma <b>movimentação</b> registrada (com seu usuário) que aparece em <b>Estoque → Movimentações</b>. Ajustes não podem ser editados nem excluídos — para corrigir, faça um novo ajuste.',
      ]"
    />

    <v-tabs v-model="aba" class="mb-4" bg-color="transparent">
      <v-tab value="unitario">Ajuste Unitário</v-tab>
      <v-tab value="lote">Inventário (Lote)</v-tab>
    </v-tabs>

    <!-- ── Ajuste unitário ─────────────────────────────────────────────────── -->
    <div v-if="aba === 'unitario'">
      <v-card rounded="xl" elevation="1" class="pa-4">
        <div class="text-body-2 text-medium-emphasis mb-4">
          Informe a quantidade <strong>física real</strong> contada. O sistema calcula a diferença automaticamente.
        </div>
        <v-form ref="formUnit">
          <v-row dense>
            <v-col cols="12" md="6">
              <v-text-field v-model="unit.buscaProduto" label="Produto *"
                variant="outlined" density="compact" @update:model-value="buscarProduto"
                :loading="buscandoProduto" />
              <v-list v-if="produtosSugeridos.length" elevation="2" rounded="lg" class="mb-2">
                <v-list-item v-for="p in produtosSugeridos" :key="p.id"
                  :title="p.descricao" :subtitle="`Estoque atual: ${p.estoqueAtual}`"
                  @click="selecionarProduto(p)" hover />
              </v-list>
            </v-col>
            <v-col cols="12" md="6">
              <v-select v-model="unit.localEstoqueId" :items="locais" item-title="nome" item-value="id"
                label="Local de Estoque *" variant="outlined" density="compact"
                :rules="[r => !!r || 'Obrigatório']" />
            </v-col>
          </v-row>

          <v-alert v-if="unit.produtoId" class="mb-3" type="info" variant="tonal" density="compact">
            <strong>{{ unit.produtoNome }}</strong> — Estoque atual no sistema:
            <strong>{{ unit.estoqueAtual }}</strong>
          </v-alert>

          <v-row dense>
            <v-col cols="12" md="4">
              <v-text-field v-model.number="unit.quantidadeContada" label="Qtd. Contada *"
                type="number" variant="outlined" density="compact"
                :rules="[r => r >= 0 || 'Inválido']" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field :model-value="diferenca" label="Diferença" readonly
                variant="outlined" density="compact"
                :color="diferenca > 0 ? 'success' : diferenca < 0 ? 'error' : 'default'"
                :prefix="diferenca > 0 ? '+' : ''" />
            </v-col>
            <v-col cols="12" md="4">
              <v-chip v-if="unit.produtoId"
                :color="diferenca > 0 ? 'success' : diferenca < 0 ? 'error' : 'default'"
                class="mt-2">
                {{ diferenca > 0 ? 'ENTRADA' : diferenca < 0 ? 'SAÍDA' : 'SEM DIFERENÇA' }}
              </v-chip>
            </v-col>
          </v-row>
          <v-text-field v-model="unit.observacao" label="Observação" variant="outlined"
            density="compact" class="mt-2" />

          <div class="d-flex justify-end mt-3">
            <v-btn color="primary" :loading="salvandoUnit"
              :disabled="!unit.produtoId || diferenca === 0"
              @click="ajustarUnitario">
              Aplicar Ajuste
            </v-btn>
          </div>
        </v-form>
      </v-card>
    </div>

    <!-- ── Inventário em lote ──────────────────────────────────────────────── -->
    <div v-if="aba === 'lote'">
      <v-card rounded="xl" elevation="1" class="mb-4 pa-4">
        <v-row dense align="center">
          <v-col cols="12" md="4">
            <v-select v-model="lote.localEstoqueId" :items="locais" item-title="nome" item-value="id"
              label="Local de Estoque *" variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="auto">
            <v-btn color="secondary" variant="tonal" prepend-icon="mdi-download"
              @click="carregarProdutos" :loading="carregandoProdutos">
              Carregar Produtos
            </v-btn>
          </v-col>
          <v-spacer />
          <v-col cols="auto">
            <v-btn color="primary" :loading="salvandoLote"
              :disabled="!itensInventario.length || !lote.localEstoqueId"
              @click="aplicarInventario">
              Aplicar Inventário
            </v-btn>
          </v-col>
        </v-row>
      </v-card>

      <v-card v-if="itensInventario.length" rounded="xl" elevation="1">
        <v-card-subtitle class="pa-3">
          {{ itensInventario.length }} produto(s) — Digite as quantidades físicas contadas
        </v-card-subtitle>
        <v-data-table :headers="headersLote" :items="itensInventario"
          density="compact" items-per-page="50">
          <template #item.estoqueAtual="{ item }">
            <span :class="{ 'text-warning': item.estoqueAtual <= item.estoqueMinimo }">
              {{ item.estoqueAtual }}
            </span>
          </template>
          <template #item.quantidadeContada="{ item }">
            <v-text-field v-model.number="item.quantidadeContada" type="number"
              variant="outlined" density="compact" hide-details style="width:100px"
              :color="item.quantidadeContada !== item.estoqueAtual ? 'warning' : ''" />
          </template>
          <template #item.diferenca="{ item }">
            <v-chip size="small"
              :color="item.quantidadeContada - item.estoqueAtual > 0 ? 'success' : item.quantidadeContada - item.estoqueAtual < 0 ? 'error' : 'default'"
              variant="tonal">
              {{ item.quantidadeContada - item.estoqueAtual > 0 ? '+' : '' }}{{ item.quantidadeContada - item.estoqueAtual }}
            </v-chip>
          </template>
        </v-data-table>
      </v-card>
    </div>
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

const aba = ref('unitario')
const buscandoProduto = ref(false)
const salvandoUnit = ref(false)
const salvandoLote = ref(false)
const carregandoProdutos = ref(false)

const locais = ref<any[]>([])
const produtosSugeridos = ref<any[]>([])
const itensInventario = ref<any[]>([])

const formUnit = ref<any>(null)

const unit = ref({
  produtoId: null as string | null, produtoNome: '', buscaProduto: '',
  estoqueAtual: 0, localEstoqueId: null as string | null,
  quantidadeContada: 0, observacao: '',
})

const lote = ref({ localEstoqueId: null as string | null })

const diferenca = computed(() =>
  unit.value.produtoId ? unit.value.quantidadeContada - unit.value.estoqueAtual : 0
)

const headersLote = [
  { title: 'Código', key: 'codigo', width: 100 },
  { title: 'Descrição', key: 'descricao' },
  { title: 'Estoque Sistema', key: 'estoqueAtual', width: 130 },
  { title: 'Qtd. Contada', key: 'quantidadeContada', width: 130 },
  { title: 'Diferença', key: 'diferenca', width: 100 },
]

let timer: any
function buscarProduto() {
  clearTimeout(timer)
  produtosSugeridos.value = []
  if (!unit.value.buscaProduto || unit.value.buscaProduto.length < 2) return
  timer = setTimeout(async () => {
    buscandoProduto.value = true
    try {
      const r = await api.get('/produtos/buscar', {
        params: { empresaId: auth.empresaId, q: unit.value.buscaProduto },
      })
      produtosSugeridos.value = r.data
    } finally { buscandoProduto.value = false }
  }, 300)
}

function selecionarProduto(p: any) {
  unit.value.produtoId = p.id
  unit.value.produtoNome = p.descricao
  unit.value.buscaProduto = p.descricao
  unit.value.estoqueAtual = p.estoqueAtual ?? 0
  unit.value.quantidadeContada = p.estoqueAtual ?? 0
  produtosSugeridos.value = []
}

async function ajustarUnitario() {
  if (!unit.value.produtoId || !unit.value.localEstoqueId) return
  salvandoUnit.value = true
  try {
    const r = await api.post('/ajuste-estoque/unitario', {
      empresaId: auth.empresaId,
      produtoId: unit.value.produtoId,
      localEstoqueId: unit.value.localEstoqueId,
      quantidadeContada: unit.value.quantidadeContada,
      usuarioId: auth.usuario?.id,
      observacao: unit.value.observacao || null,
    })
    const d = r.data
    // 204 NoContent quando não há diferença
    if (!d) { notif.aviso('Sem diferença — nada a ajustar.'); return }
    notif.ok(`Ajuste aplicado! Diferença: ${d.diferenca > 0 ? '+' : ''}${d.diferenca}`)
    unit.value = {
      produtoId: null, produtoNome: '', buscaProduto: '',
      estoqueAtual: 0, localEstoqueId: unit.value.localEstoqueId,
      quantidadeContada: 0, observacao: '',
    }
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? e?.response?.data?.detalhe ?? e?.response?.data?.title ?? 'Erro ao ajustar.')
  } finally { salvandoUnit.value = false }
}

async function carregarProdutos() {
  if (!lote.value.localEstoqueId) { notif.aviso('Selecione um local de estoque.'); return }
  carregandoProdutos.value = true
  try {
    const r = await api.get('/produtos', {
      params: { empresaId: auth.empresaId, ativo: true, tamanhoPagina: 500 },
    })
    const lista = r.data?.itens ?? r.data ?? []
    itensInventario.value = lista.map((p: any) => ({
      ...p,
      quantidadeContada: p.estoqueAtual ?? 0,
    }))
    notif.ok(`${itensInventario.value.length} produto(s) carregados.`)
  } finally { carregandoProdutos.value = false }
}

async function aplicarInventario() {
  const alterados = itensInventario.value.filter(i => i.quantidadeContada !== i.estoqueAtual)
  if (!alterados.length) { notif.aviso('Nenhuma diferença encontrada.'); return }
  if (!confirm(`Aplicar ajuste em ${alterados.length} produto(s)?`)) return
  salvandoLote.value = true
  try {
    const r = await api.post('/ajuste-estoque/lote', {
      empresaId: auth.empresaId,
      localEstoqueId: lote.value.localEstoqueId,
      usuarioId: auth.usuario?.id,
      itens: alterados.map(i => ({ produtoId: i.id, quantidadeContada: i.quantidadeContada })),
    })
    notif.ok(`Inventário aplicado! ${r.data.processados} produto(s) ajustados.`)
    await carregarProdutos()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? e?.response?.data?.detalhe ?? e?.response?.data?.title ?? 'Erro ao aplicar inventário.')
  } finally { salvandoLote.value = false }
}

onMounted(async () => {
  const r = await api.get('/locais-estoque', { params: { empresaId: auth.empresaId } })
  locais.value = r.data
})
</script>
