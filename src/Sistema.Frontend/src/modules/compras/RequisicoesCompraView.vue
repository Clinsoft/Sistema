<template>
  <div>
    <div class="d-flex align-center mb-4 gap-2">
      <div class="text-h6 font-weight-bold flex-grow-1">Requisições de Compra</div>
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNova">Nova Requisição</v-btn>
    </div>

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      <template v-if="ehGestor">
        As requisições feitas pelos atendentes aparecem aqui. Abra uma para ver os itens
        <b>agrupados por fornecedor</b> e <b>gerar os pedidos de compra</b>.
      </template>
      <template v-else>
        Peça o que está faltando: busque o produto e informe a quantidade. Não precisa escolher
        fornecedor nem preço — o gestor cuida disso.
      </template>
    </v-alert>

    <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
      <div class="d-flex align-center flex-wrap gap-2">
        <v-select v-model="filtroStatus" :items="['Todas','Aberta','Processada','Cancelada']"
          label="Status" variant="outlined" density="compact" hide-details style="max-width:200px" />
        <v-spacer />
        <v-btn color="primary" variant="tonal" rounded="lg" :loading="carregando" @click="carregar">Buscar</v-btn>
      </div>
    </v-card>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="listaFiltrada" :loading="carregando" density="compact" hover
        no-data-text="Nenhuma requisição.">
        <template #item.criadoEm="{ item }">{{ new Date(item.criadoEm).toLocaleString('pt-BR') }}</template>
        <template #item.status="{ item }">
          <v-chip size="small" :color="corStatus(item.status)" variant="tonal">{{ item.status }}</v-chip>
        </template>
        <template #item.acoes="{ item }">
          <v-btn v-if="ehGestor" size="small" color="primary" variant="tonal" rounded="lg"
            prepend-icon="mdi-truck-outline" @click="abrirDetalhe(item)">Ver / gerar pedidos</v-btn>
          <v-btn v-else icon="mdi-eye-outline" size="small" variant="text" color="primary"
            @click="abrirDetalhe(item)" title="Ver itens" />
          <v-btn v-if="item.status==='Aberta'" icon="mdi-cancel" size="small" variant="text" color="error"
            @click="cancelar(item)" title="Cancelar requisição" />
          <v-btn v-if="ehGestor" icon="mdi-delete-outline" size="small" variant="text" color="error"
            @click="excluir(item)" title="Excluir requisição" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog: nova requisição -->
    <v-dialog v-model="dialogNova" max-width="640" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 d-flex align-center">
          Nova Requisição de Compra <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="dialogNova = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <div class="d-flex ga-2 mb-3">
            <v-autocomplete v-model="prodSel" :items="prodOpcoes" :loading="buscandoProd"
              item-title="descricao" item-value="id" return-object no-filter clearable
              label="Buscar produto (nome ou código)" variant="outlined" density="compact" class="flex-grow-1"
              @update:search="buscarProduto" @update:model-value="onSelecionarProd" />
            <v-text-field v-model.number="qtdSel" label="Qtd" type="number" min="1"
              variant="outlined" density="compact" style="width:90px" @keyup.enter="addItem" />
            <v-btn icon="mdi-plus" color="primary" variant="tonal" :disabled="!prodSel" @click="addItem" />
          </div>
          <v-table density="compact">
            <thead><tr><th>Produto</th><th style="width:90px" class="text-center">Qtd</th><th style="width:48px"></th></tr></thead>
            <tbody>
              <tr v-for="(i, idx) in novaItens" :key="idx">
                <td>{{ i.descricao }}</td>
                <td class="text-center">
                  <v-text-field v-model.number="i.quantidade" type="number" min="1"
                    variant="outlined" density="compact" hide-details style="width:80px" />
                </td>
                <td><v-btn icon="mdi-close" size="x-small" variant="text" color="error" @click="novaItens.splice(idx,1)" /></td>
              </tr>
              <tr v-if="!novaItens.length"><td colspan="3" class="text-center pa-4 text-medium-emphasis">Nenhum item</td></tr>
            </tbody>
          </v-table>
          <v-textarea v-model="novaObs" label="Observação (opcional)" rows="2" variant="outlined"
            density="compact" class="mt-3" hide-details />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 justify-end">
          <v-btn variant="text" @click="dialogNova = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando" :disabled="!novaItens.length" @click="salvarNova">
            Enviar requisição
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: detalhe / gerar pedidos -->
    <v-dialog v-model="dialogDet" max-width="900">
      <v-card rounded="xl" v-if="det">
        <v-card-title class="pa-4 d-flex align-center flex-wrap ga-2">
          <v-icon icon="mdi-store-outline" size="20" />
          <span>{{ det.loja || 'Sem loja' }}</span>
          <span class="text-body-2 text-medium-emphasis">· {{ det.solicitante }} · {{ det.itens.length }} item(ns)</span>
          <v-chip size="small" :color="corStatus(det.status)" variant="tonal">{{ det.status }}</v-chip>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="dialogDet = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <div v-if="det.observacao" class="text-body-2 mb-3"><b>Obs.:</b> {{ det.observacao }}</div>

          <v-card v-for="g in porFornecedor" :key="g.fornecedor" rounded="lg" variant="outlined" class="mb-3">
            <v-card-title class="text-body-2 font-weight-bold d-flex align-center py-2 flex-wrap ga-2">
              <v-icon icon="mdi-truck-outline" size="18" class="mr-1" /> {{ g.fornecedor }}
              <v-spacer />
              <span class="text-caption text-medium-emphasis">{{ g.itens.length }} item(ns)</span>
              <v-btn v-if="ehGestor && det.status==='Aberta'" size="small" color="primary" variant="tonal"
                rounded="lg" prepend-icon="mdi-cart-plus" :loading="gerando===g.fornecedor"
                :disabled="!g.fornecedorId" @click="gerarPedido(g)">Gerar pedido</v-btn>
            </v-card-title>
            <v-alert v-if="!g.fornecedorId" type="warning" variant="tonal" density="compact" class="mx-3 mb-2">
              Sem fornecedor vinculado — vincule no cadastro do produto (ou na Sugestão de Compra) para gerar o pedido.
            </v-alert>
            <v-table density="compact">
              <thead><tr><th>Produto</th><th class="text-center" style="width:90px">Qtd</th>
                <th class="text-right" style="width:110px">Custo un.</th><th class="text-right" style="width:120px">Estimado</th></tr></thead>
              <tbody>
                <tr v-for="i in g.itens" :key="i.produtoId">
                  <td>{{ i.descricao }}</td>
                  <td class="text-center">{{ fmtQtd(i.quantidade) }}</td>
                  <td class="text-right">R$ {{ fmt(i.custoUnitario) }}</td>
                  <td class="text-right">R$ {{ fmt(i.quantidade * i.custoUnitario) }}</td>
                </tr>
              </tbody>
            </v-table>
          </v-card>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3 justify-end">
          <v-btn v-if="ehGestor && det.status==='Aberta'" color="success" variant="tonal" rounded="lg"
            prepend-icon="mdi-check" @click="marcarProcessada">Marcar como processada</v-btn>
          <v-btn variant="text" @click="dialogDet = false">Fechar</v-btn>
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
const ehGestor = computed(() => ['Administrador', 'Gerente'].includes(auth.usuario?.role ?? ''))
const lojaAtual = computed(() => auth.lojaAtualId ?? auth.usuario?.localEstoqueId ?? null)

interface ItemDet { produtoId: string; descricao: string; quantidade: number; custoUnitario: number; fornecedorId: string | null; fornecedor: string }

const carregando = ref(false)
const lista = ref<any[]>([])
const filtroStatus = ref('Aberta')
const headers = [
  { title: 'Data', key: 'criadoEm' }, { title: 'Loja', key: 'loja' },
  { title: 'Solicitante', key: 'solicitante' }, { title: 'Itens', key: 'qtdItens', align: 'center' as const },
  { title: 'Status', key: 'status' }, { title: '', key: 'acoes', sortable: false, width: 240 },
]
const listaFiltrada = computed(() => lista.value)

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtQtd = (v: number) => (v ?? 0).toLocaleString('pt-BR', { maximumFractionDigits: 3 })
const corStatus = (s: string) => ({ Aberta: 'warning', Processada: 'success', Cancelada: 'error' } as any)[s] ?? 'default'

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/requisicoes-compra', {
      params: { empresaId: auth.empresaId, status: filtroStatus.value === 'Todas' ? undefined : filtroStatus.value },
    })
    lista.value = r.data ?? []
  } catch { lista.value = [] } finally { carregando.value = false }
}

// ── Nova requisição ──
const dialogNova = ref(false)
const salvando = ref(false)
const prodOpcoes = ref<any[]>([])
const buscandoProd = ref(false)
const prodSel = ref<any>(null)
const qtdSel = ref(1)
const novaItens = ref<{ produtoId: string; descricao: string; quantidade: number }[]>([])
const novaObs = ref('')

function abrirNova() {
  novaItens.value = []; novaObs.value = ''; prodSel.value = null; qtdSel.value = 1; prodOpcoes.value = []
  dialogNova.value = true
}
async function buscarProduto(q: string) {
  if (!q || q.length < 2) return
  buscandoProd.value = true
  try {
    const r = await api.get('/produtos/buscar', { params: { q, empresaId: auth.empresaId } })
    prodOpcoes.value = r.data ?? []
  } finally { buscandoProd.value = false }
}
function onSelecionarProd() { /* mantém seleção; add via botão/enter */ }
function addItem() {
  const p = prodSel.value
  if (!p) return
  const q = Math.max(1, Number(qtdSel.value) || 1)
  const existe = novaItens.value.find(i => i.produtoId === p.id)
  if (existe) existe.quantidade += q
  else novaItens.value.push({ produtoId: p.id, descricao: p.descricao, quantidade: q })
  prodSel.value = null; qtdSel.value = 1; prodOpcoes.value = []
}
async function salvarNova() {
  if (!novaItens.value.length) return
  salvando.value = true
  try {
    await api.post('/requisicoes-compra', {
      empresaId: auth.empresaId,
      usuarioId: auth.usuario?.id,
      localEstoqueId: lojaAtual.value,
      observacao: novaObs.value || null,
      itens: novaItens.value.map(i => ({ produtoId: i.produtoId, quantidade: i.quantidade })),
    })
    notif.ok('Requisição enviada!')
    dialogNova.value = false
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao enviar requisição.') }
  finally { salvando.value = false }
}

// ── Detalhe / gerar pedidos ──
const dialogDet = ref(false)
const det = ref<any>(null)
const gerando = ref<string | null>(null)

const porFornecedor = computed(() => {
  const map = new Map<string, { fornecedor: string; fornecedorId: string | null; itens: ItemDet[] }>()
  for (const it of (det.value?.itens ?? []) as ItemDet[]) {
    const g = map.get(it.fornecedor) ?? { fornecedor: it.fornecedor, fornecedorId: it.fornecedorId, itens: [] }
    g.itens.push(it); map.set(it.fornecedor, g)
  }
  return [...map.values()]
})

async function abrirDetalhe(item: any) {
  try {
    const r = await api.get(`/requisicoes-compra/${item.id}`)
    det.value = { ...r.data, loja: item.loja, solicitante: item.solicitante }
    dialogDet.value = true
  } catch { notif.erro('Erro ao carregar a requisição.') }
}

async function gerarPedido(g: { fornecedor: string; fornecedorId: string | null; itens: ItemDet[] }) {
  if (!g.fornecedorId) { notif.aviso('Grupo sem fornecedor vinculado.'); return }
  gerando.value = g.fornecedor
  try {
    await api.post('/pedidos-compra', {
      empresaId: auth.empresaId,
      fornecedorId: g.fornecedorId,
      usuarioId: auth.usuario?.id,
      itens: g.itens.map(i => ({
        produtoId: i.produtoId, descricao: i.descricao,
        quantidade: i.quantidade, precoUnitario: i.custoUnitario,
      })),
    })
    notif.ok(`Pedido criado para ${g.fornecedor}. Veja em Compras › Pedido de Compra.`)
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao gerar o pedido.') }
  finally { gerando.value = null }
}

async function marcarProcessada() {
  try {
    await api.patch(`/requisicoes-compra/${det.value.id}/processar`)
    notif.ok('Requisição marcada como processada.')
    dialogDet.value = false
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao processar.') }
}

async function cancelar(item: any) {
  if (!confirm('Cancelar esta requisição?')) return
  try {
    await api.patch(`/requisicoes-compra/${item.id}/cancelar`)
    notif.ok('Requisição cancelada.')
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao cancelar.') }
}

async function excluir(item: any) {
  if (!confirm('Excluir esta requisição definitivamente?')) return
  try {
    await api.delete(`/requisicoes-compra/${item.id}`)
    notif.ok('Requisição excluída.')
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao excluir.') }
}

onMounted(carregar)
</script>
