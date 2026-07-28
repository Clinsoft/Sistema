<template>
  <div>
    <div class="d-flex align-center mb-4 gap-2">
      <div class="text-h6 font-weight-bold flex-grow-1">Pedidos de Compra</div>
      <v-btn color="secondary" variant="tonal" prepend-icon="mdi-file-compare"
        rounded="lg" @click="$router.push('/compras/cotacoes')">
        Comparar Cotações
      </v-btn>
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">Novo Pedido</v-btn>
    </div>

    <GuiaPassos
      id="compras"
      titulo="Como usar os Pedidos de Compra"
      :passos="[
        'Clique em <b>Novo Pedido</b>, escolha o <b>fornecedor</b> e adicione itens. Os produtos <b>abaixo do estoque mínimo</b> são sugeridos automaticamente — clique para incluí-los.',
        'Salve como <b>Rascunho</b>. Depois use <b>➤ Enviar</b> para marcar como enviado ao fornecedor.',
        'Quando a mercadoria chegar, use <b>📦 Receber</b>, escolha o <b>local de estoque</b> e confirme — o estoque é atualizado automaticamente.',
        'Use <b>🚫 Cancelar</b> em pedidos ainda não recebidos. Filtre por status e período para consultar o histórico.',
      ]"
    />
    <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
      <v-row dense>
        <v-col cols="12" sm="4">
          <v-select v-model="filtros.status" label="Status"
            :items="['Todos','Rascunho','Enviado','Recebido','Cancelado']"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f }" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.inicio" label="Início" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.fim" label="Fim" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
      </v-row>
      <div class="d-flex justify-end mt-2">
        <v-btn color="primary" variant="tonal" rounded="lg" :loading="carregando" @click="carregar">Buscar</v-btn>
      </div>
    </v-card>
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="pedidos" :loading="carregando" density="compact" hover>
        <template #item.status="{ item }">
          <v-chip :color="corStatus(item.status)" size="small" variant="tonal">{{ item.status }}</v-chip>
        </template>
        <template #item.totalPedido="{ item }">R$ {{ fmt(item.totalPedido) }}</template>
        <template #item.criadoEm="{ item }">{{ new Date(item.criadoEm).toLocaleDateString('pt-BR') }}</template>
        <template #item.actions="{ item }">
          <v-btn v-if="item.status==='Rascunho'" icon="mdi-send-outline" size="x-small" variant="text"
            color="primary" @click="enviar(item)" title="Enviar" />
          <v-btn v-if="item.status==='Enviado'" icon="mdi-package-check" size="x-small" variant="text"
            color="success" @click="abrirRecebimento(item)" title="Receber" />
          <v-btn v-if="item.status==='Rascunho' || item.status==='Enviado'" icon="mdi-cancel"
            size="x-small" variant="text" color="error" @click="cancelar(item)" title="Cancelar pedido" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog novo pedido -->
    <v-dialog v-model="dialog" max-width="800" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 d-flex align-center">
          Novo Pedido de Compra <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="dialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row dense class="mb-3">
            <v-col cols="12" sm="8">
              <v-autocomplete v-model="np.fornecedorId" :items="forns"
                item-title="razaoSocial" item-value="id" auto-select-first clearable
                label="Fornecedor *" variant="outlined" density="compact"
                no-data-text="Nenhum fornecedor cadastrado" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="np.previsaoEntrega" label="Previsão Entrega"
                type="date" variant="outlined" density="compact" />
            </v-col>
          </v-row>
          <!-- Seletor de produtos (estoque zero/abaixo, unidade, selecionar todos) -->
          <v-sheet border rounded="lg" class="mb-3 pa-2">
            <div class="d-flex align-center flex-wrap ga-1 mb-2">
              <v-icon icon="mdi-cart-plus" size="18" color="primary" class="mr-1" />
              <span class="text-caption font-weight-bold">Adicionar produtos ao pedido</span>
              <v-spacer />
              <span class="text-caption text-medium-emphasis">{{ pickerFiltrados.length }} produto(s)</span>
            </div>
            <v-row dense>
              <v-col cols="12" sm="4">
                <v-select v-model="pickerEstoque" :items="['Abaixo do mínimo','Zerados','Todos']"
                  label="Estoque" variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="12" sm="4">
                <v-select v-model="pickerUnidade" :items="unidades" item-title="nome" item-value="id"
                  label="Unidade de medida" variant="outlined" density="compact" hide-details clearable />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model="pickerBusca" label="Buscar produto" prepend-inner-icon="mdi-magnify"
                  variant="outlined" density="compact" hide-details clearable />
              </v-col>
            </v-row>
            <div class="picker-scroll mt-2">
              <v-table density="compact">
                <thead>
                  <tr>
                    <th style="width:40px">
                      <v-checkbox-btn :model-value="todosSelecionados" :indeterminate="algunsSelecionados"
                        density="compact" @update:model-value="toggleTodos" />
                    </th>
                    <th>Produto</th>
                    <th class="text-center" style="width:64px">Estoque</th>
                    <th class="text-center" style="width:64px">Mínimo</th>
                    <th class="text-center" style="width:56px">Un.</th>
                    <th class="text-center" style="width:88px">Comprar</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="p in pickerFiltrados" :key="p.produtoId"
                    :class="jaAdicionado(p.produtoId) ? 'bg-success-lighten-5' : ''">
                    <td>
                      <v-checkbox-btn v-model="selecionados[p.produtoId]"
                        :disabled="jaAdicionado(p.produtoId)" density="compact" />
                    </td>
                    <td class="text-body-2">{{ p.descricao }}</td>
                    <td class="text-center font-weight-bold"
                      :class="p.estoqueAtual <= 0 ? 'text-error' : (p.abaixoMinimo ? 'text-warning' : 'text-medium-emphasis')">
                      {{ p.estoqueAtual }}
                    </td>
                    <td class="text-center text-medium-emphasis">{{ p.estoqueMinimo }}</td>
                    <td class="text-center text-caption text-medium-emphasis">{{ p.unidadeNome }}</td>
                    <td class="text-center">
                      <v-text-field v-model.number="qtdSugestao[p.produtoId]" type="number"
                        variant="outlined" density="compact" hide-details style="width:80px" />
                    </td>
                  </tr>
                  <tr v-if="!pickerFiltrados.length">
                    <td colspan="6" class="text-center pa-3 text-medium-emphasis">Nenhum produto no filtro</td>
                  </tr>
                </tbody>
              </v-table>
            </div>
            <div class="pa-2 d-flex justify-end align-center ga-2">
              <span class="text-caption text-medium-emphasis">{{ qtdSelecionados }} selecionado(s)</span>
              <v-btn size="small" color="primary" variant="tonal" prepend-icon="mdi-playlist-plus"
                :disabled="!qtdSelecionados" @click="addSelecionados">Adicionar selecionados</v-btn>
            </div>
          </v-sheet>

          <div class="d-flex ga-2 mb-3">
            <v-autocomplete v-model="it.produtoId" :items="prods"
              item-title="descricao" item-value="id" auto-select-first clearable
              label="Adicionar produto avulso" variant="outlined" density="compact" class="flex-grow-1"
              @update:model-value="selecionarProd" />
            <v-text-field v-model.number="it.quantidade" label="Qtd" type="number"
              variant="outlined" density="compact" style="width:80px" />
            <v-text-field v-model.number="it.precoUnitario" label="R$ Un." type="number"
              variant="outlined" density="compact" style="width:100px" />
            <v-btn icon="mdi-plus" color="primary" variant="tonal" @click="addItem" />
          </div>
          <v-table density="compact">
            <thead><tr><th>Produto</th><th>Qtd</th><th>R$ Un.</th><th>Total</th><th></th></tr></thead>
            <tbody>
              <tr v-for="(item, i) in np.itens" :key="i">
                <td>{{ item.descricao }}</td><td>{{ item.quantidade }}</td>
                <td>R$ {{ fmt(item.precoUnitario) }}</td>
                <td>R$ {{ fmt(item.quantidade * item.precoUnitario) }}</td>
                <td><v-btn icon="mdi-close" size="x-small" variant="text" color="error"
                  @click="np.itens.splice(i, 1)" /></td>
              </tr>
              <tr v-if="!np.itens.length">
                <td colspan="5" class="text-center pa-4 text-medium-emphasis">Nenhum item</td>
              </tr>
            </tbody>
            <tfoot><tr>
              <td colspan="3" class="text-right font-weight-bold">Total:</td>
              <td class="font-weight-bold text-primary">R$ {{ fmt(totalNp) }}</td><td></td>
            </tr></tfoot>
          </v-table>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4 justify-end">
          <v-btn variant="text" @click="dialog = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando"
            :disabled="!np.fornecedorId || !np.itens.length" @click="salvar">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog recebimento -->
    <v-dialog v-model="dialogRec" max-width="600">
      <v-card rounded="xl">
        <v-card-title class="pa-4">Receber Pedido</v-card-title>
        <v-card-text class="pa-4">
          <v-row dense class="mb-3">
            <v-col cols="12">
              <v-select v-model="rec.localEstoqueId" :items="locaisEstoque"
                item-title="nome" item-value="id" label="Local de estoque (entrada) *"
                variant="outlined" density="compact" hide-details
                hint="Onde as mercadorias serão lançadas" persistent-hint />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="rec.dataRecebimento" label="Data Recebimento"
                type="date" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="rec.numeroNf" label="Nº NF Fornecedor"
                variant="outlined" density="compact" />
            </v-col>
          </v-row>
          <v-table density="compact">
            <thead><tr><th>Produto</th><th>Pedido</th><th>Recebido</th></tr></thead>
            <tbody>
              <tr v-for="(item, i) in rec.itens" :key="i">
                <td>{{ item.descricao }}</td>
                <td>{{ item.quantidadePedida }}</td>
                <td><v-text-field v-model.number="item.quantidadeRecebida" type="number"
                  variant="outlined" density="compact" hide-details style="width:80px" /></td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-card-actions class="pa-4 justify-end">
          <v-btn variant="text" @click="dialogRec = false">Cancelar</v-btn>
          <v-btn color="success" :loading="salvando" @click="confirmarRec">Confirmar</v-btn>
        </v-card-actions>
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

const auth = useAuthStore(); const notif = useNotifStore()
const carregando = ref(false); const salvando = ref(false)
const pedidos = ref<any[]>([]); const forns = ref<any[]>([]); const prods = ref<any[]>([])
const locaisEstoque = ref<any[]>([])
const dialog = ref(false); const dialogRec = ref(false)
const unidades = ref<any[]>([])
const qtdSugestao = ref<Record<string, number>>({})
// Seletor de produtos do pedido
const pickerProdutos = ref<any[]>([])
const pickerEstoque = ref('Abaixo do mínimo')
const pickerUnidade = ref<string | null>(null)
const pickerBusca = ref('')
const selecionados = ref<Record<string, boolean>>({})
const filtros = ref({ status:'Todos', inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0,10), fim: new Date().toISOString().slice(0,10) })
const np = ref<any>({ fornecedorId: null, previsaoEntrega: '', itens: [], observacoes: '' })
const it = ref({ produtoId:'', descricao:'', quantidade:1, precoUnitario:0 })
const rec = ref<any>({ pedidoId:'', dataRecebimento: new Date().toISOString().slice(0,10), numeroNf:'', itens:[] })
const totalNp = computed(() => np.value.itens.reduce((s: number, i: any) => s + i.quantidade * i.precoUnitario, 0))
const headers = [
  { title:'Nº', key:'numero' }, { title:'Fornecedor', key:'fornecedorNome', sortable:true },
  { title:'Data', key:'criadoEm' }, { title:'Total', key:'totalPedido', sortable:true },
  { title:'Status', key:'status' }, { title:'Ações', key:'actions', sortable:false },
]
const corStatus = (s: string) => ({ Rascunho:'default', Enviado:'info', Recebido:'success', Cancelado:'error' })[s] ?? 'default'
const fmt = (v: number) => (v??0).toLocaleString('pt-BR', { minimumFractionDigits:2 })
async function carregar() {
  carregando.value=true
  try { const r = await api.get('/pedidos-compra', { params:{ empresaId:auth.empresaId, ...filtros.value } }); pedidos.value=r.data }
  finally { carregando.value=false }
}
// Carrega, uma vez, os cadastros usados no dialog (fornecedores, produtos, unidades).
async function carregarCatalogo() {
  try {
    const [f, p, u] = await Promise.all([
      api.get('/fornecedores',    { params: { empresaId: auth.empresaId } }),
      api.get('/produtos',        { params: { empresaId: auth.empresaId, pagina: 1, tamanhoPagina: 5000 } }),
      api.get('/unidades-medida', { params: { empresaId: auth.empresaId } }),
    ])
    forns.value = Array.isArray(f.data) ? f.data : (f.data.itens ?? [])
    prods.value = p.data?.itens ?? p.data ?? []
    unidades.value = Array.isArray(u.data) ? u.data : (u.data.itens ?? [])
  } catch { /* silencioso */ }
}
function selecionarProd(id: string) { const p=prods.value.find((x: any)=>x.id===id); if (p) { it.value.descricao=p.descricao; it.value.precoUnitario=p.custoUnitario??0 } }
function addItem() { if (!it.value.produtoId) return; np.value.itens.push({...it.value}); it.value={produtoId:'', descricao:'', quantidade:1, precoUnitario:0} }
async function abrirNovo() {
  np.value = { fornecedorId: null, previsaoEntrega: '', itens: [], observacoes: '' }
  qtdSugestao.value = {}
  selecionados.value = {}
  pickerEstoque.value = 'Abaixo do mínimo'; pickerUnidade.value = null; pickerBusca.value = ''
  dialog.value = true
  if (!forns.value.length || !prods.value.length) await carregarCatalogo()
  try {
    // Posição de TODOS os produtos (sem filtro) — filtramos no cliente.
    const r = await api.get('/estoque/posicao', { params: { empresaId: auth.empresaId } })
    const lista: any[] = r.data?.produtos ?? r.data?.itens ?? r.data ?? []
    const custoMap = new Map(prods.value.map((x: any) => [x.id, x.custoUnitario ?? 0]))
    const uniMap = new Map(unidades.value.map((x: any) => [x.id, x.sigla ?? x.nome ?? '']))
    pickerProdutos.value = lista.map((p: any) => {
      const atual = p.estoqueAtual ?? p.quantidadeAtual ?? 0
      const min = p.estoqueMinimo ?? 0
      const pid = p.id ?? p.produtoId
      qtdSugestao.value[pid] = Math.max(1, min - atual)
      return {
        produtoId: pid, descricao: p.descricao,
        estoqueAtual: atual, estoqueMinimo: min,
        abaixoMinimo: p.abaixoMinimo ?? (atual <= min),
        unidadeId: p.unidadeMedidaId, unidadeNome: uniMap.get(p.unidadeMedidaId) ?? '',
        custoUnitario: custoMap.get(pid) ?? p.custoUnitario ?? 0,
      }
    })
  } catch { pickerProdutos.value = [] }
}

// ── Filtros e seleção do picker ──────────────────────────────────
const pickerFiltrados = computed(() => {
  let l = pickerProdutos.value
  if (pickerEstoque.value === 'Abaixo do mínimo') l = l.filter((p: any) => p.abaixoMinimo)
  else if (pickerEstoque.value === 'Zerados') l = l.filter((p: any) => p.estoqueAtual <= 0)
  if (pickerUnidade.value) l = l.filter((p: any) => p.unidadeId === pickerUnidade.value)
  const q = pickerBusca.value?.trim().toLowerCase()
  if (q) l = l.filter((p: any) => (p.descricao ?? '').toLowerCase().includes(q))
  return l
})
const selecionaveis = computed(() => pickerFiltrados.value.filter((p: any) => !jaAdicionado(p.produtoId)))
const qtdSelecionados = computed(() => selecionaveis.value.filter((p: any) => selecionados.value[p.produtoId]).length)
const todosSelecionados = computed(() => selecionaveis.value.length > 0 && qtdSelecionados.value === selecionaveis.value.length)
const algunsSelecionados = computed(() => qtdSelecionados.value > 0 && !todosSelecionados.value)
function toggleTodos(val: boolean | null) { selecionaveis.value.forEach((p: any) => { selecionados.value[p.produtoId] = !!val }) }
function addSelecionados() {
  selecionaveis.value.forEach((p: any) => { if (selecionados.value[p.produtoId]) addSugestao(p) })
  selecionados.value = {}
}
function jaAdicionado(produtoId: string) {
  return np.value.itens.some((i: any) => i.produtoId === produtoId)
}
function addSugestao(p: any) {
  if (jaAdicionado(p.produtoId)) return
  np.value.itens.push({
    produtoId: p.produtoId,
    descricao: p.descricao,
    quantidade: qtdSugestao.value[p.produtoId] ?? 1,
    precoUnitario: p.custoUnitario ?? 0,
  })
}
async function salvar() {
  salvando.value = true
  try {
    await api.post('/pedidos-compra', {
      empresaId: auth.empresaId,
      fornecedorId: np.value.fornecedorId,
      usuarioId: auth.usuario?.id,
      previsaoEntrega: np.value.previsaoEntrega || null,
      observacao: np.value.observacoes || null,
      itens: np.value.itens.map((i: any) => ({
        produtoId: i.produtoId, descricao: i.descricao,
        quantidade: i.quantidade, precoUnitario: i.precoUnitario,
      })),
    })
    notif.ok('Pedido criado!')
    dialog.value = false
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao criar pedido.') }
  finally { salvando.value = false }
}
async function enviar(item: any) {
  await api.post(`/pedidos-compra/${item.id}/enviar`, null, { params: { empresaId: auth.empresaId } })
  notif.ok('Pedido enviado!'); await carregar()
}
async function cancelar(item: any) {
  if (!confirm(`Cancelar o pedido ${item.numero}?`)) return
  try {
    await api.post(`/pedidos-compra/${item.id}/cancelar`, null, { params: { empresaId: auth.empresaId } })
    notif.ok('Pedido cancelado.'); await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao cancelar.') }
}
async function abrirRecebimento(item: any) {
  try {
    const r = await api.get(`/pedidos-compra/${item.id}`)
    const detalhe = r.data
    const localPadrao = locaisEstoque.value.find((l: any) => l.principal)?.id ?? locaisEstoque.value[0]?.id ?? null
    rec.value = { pedidoId: item.id, localEstoqueId: localPadrao, dataRecebimento: new Date().toISOString().slice(0, 10), numeroNf: '',
      itens: (detalhe.itens ?? []).map((i: any) => ({ itemPedidoId: i.id, descricao: i.descricao ?? i.produtoNome, quantidadePedida: i.quantidade, quantidadeRecebida: i.quantidade })) }
  } catch {
    rec.value = { pedidoId: item.id, localEstoqueId: locaisEstoque.value[0]?.id ?? null, dataRecebimento: new Date().toISOString().slice(0, 10), numeroNf: '', itens: [] }
  }
  dialogRec.value = true
}
async function confirmarRec() {
  if (!rec.value.localEstoqueId) { notif.erro('Selecione o local de estoque para dar entrada.'); return }
  salvando.value = true
  try {
    await api.post(`/pedidos-compra/${rec.value.pedidoId}/receber`, {
      localEstoqueId: rec.value.localEstoqueId,
      usuarioId: auth.usuario?.id,
    })
    notif.ok('Recebimento registrado! Estoque atualizado.')
    dialogRec.value = false
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao receber pedido.') }
  finally { salvando.value = false }
}
async function carregarLocais() {
  try {
    const r = await api.get('/locais-estoque', { params: { empresaId: auth.empresaId } })
    locaisEstoque.value = r.data ?? []
  } catch { locaisEstoque.value = [] }
}
onMounted(() => { carregar(); carregarLocais(); carregarCatalogo() })
</script>

<style scoped>
.picker-scroll {
  max-height: 300px;
  overflow-y: auto;
  border: 1px solid rgba(0,0,0,0.08);
  border-radius: 8px;
}
</style>
