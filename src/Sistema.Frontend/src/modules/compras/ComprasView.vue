<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Pedidos de Compra</div>
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">Novo Pedido</v-btn>
    </div>
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
              <v-autocomplete v-model="np.fornecedorId" v-model:search="buscaForn"
                :items="forns" item-title="razaoSocial" item-value="id"
                label="Fornecedor *" variant="outlined" density="compact"
                @update:search="buscarForns" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="np.previsaoEntrega" label="Previsão Entrega"
                type="date" variant="outlined" density="compact" />
            </v-col>
          </v-row>
          <!-- Sugestões: produtos abaixo do estoque mínimo -->
          <v-expand-transition>
            <div v-if="produtosBaixoEstoque.length">
              <div class="d-flex align-center mb-1">
                <v-icon icon="mdi-alert-outline" color="warning" size="16" class="mr-1" />
                <span class="text-caption font-weight-bold text-warning">
                  {{ produtosBaixoEstoque.length }} produto(s) abaixo do mínimo — clique para adicionar ao pedido
                </span>
                <v-spacer />
                <v-btn size="x-small" variant="text" @click="mostrarSugestoes = !mostrarSugestoes">
                  {{ mostrarSugestoes ? 'Ocultar' : 'Mostrar' }}
                </v-btn>
              </div>
              <v-expand-transition>
                <v-sheet v-if="mostrarSugestoes" border rounded="lg" class="mb-3">
                  <v-table density="compact">
                    <thead>
                      <tr>
                        <th>Produto</th>
                        <th class="text-center" style="width:80px">Estoque</th>
                        <th class="text-center" style="width:80px">Mínimo</th>
                        <th class="text-center" style="width:80px">Comprar</th>
                        <th style="width:40px"></th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="p in produtosBaixoEstoque" :key="p.produtoId"
                        :class="jaAdicionado(p.produtoId) ? 'bg-success-lighten-5' : ''">
                        <td class="text-body-2">{{ p.descricao }}</td>
                        <td class="text-center text-error font-weight-bold">{{ p.quantidadeAtual }}</td>
                        <td class="text-center text-medium-emphasis">{{ p.estoqueMinimo }}</td>
                        <td class="text-center">
                          <v-text-field
                            v-model.number="qtdSugestao[p.produtoId]"
                            type="number" variant="outlined" density="compact"
                            hide-details style="width:70px" />
                        </td>
                        <td>
                          <v-btn v-if="!jaAdicionado(p.produtoId)"
                            icon="mdi-plus" size="x-small" color="primary" variant="tonal"
                            @click="addSugestao(p)" />
                          <v-icon v-else icon="mdi-check-circle" color="success" size="18" />
                        </td>
                      </tr>
                    </tbody>
                  </v-table>
                  <div class="pa-2 d-flex justify-end">
                    <v-btn size="small" color="warning" variant="tonal"
                      prepend-icon="mdi-playlist-plus" @click="addTodasSugestoes">
                      Adicionar todos
                    </v-btn>
                  </div>
                </v-sheet>
              </v-expand-transition>
            </div>
          </v-expand-transition>

          <div class="d-flex ga-2 mb-3">
            <v-autocomplete v-model="it.produtoId" v-model:search="buscaProd"
              :items="prods" item-title="descricao" item-value="id"
              label="Produto" variant="outlined" density="compact" class="flex-grow-1"
              @update:model-value="selecionarProd"
              @update:search="buscarProds" />
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
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore(); const notif = useNotifStore()
const carregando = ref(false); const salvando = ref(false)
const pedidos = ref<any[]>([]); const forns = ref<any[]>([]); const prods = ref<any[]>([])
const dialog = ref(false); const dialogRec = ref(false)
const buscaForn = ref(''); const buscaProd = ref('')
const produtosBaixoEstoque = ref<any[]>([])
const mostrarSugestoes = ref(true)
const qtdSugestao = ref<Record<string, number>>({})
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
async function buscarForns(q: string) { if (!q||q.length<2) return; const r=await api.get('/fornecedores', { params:{ empresaId:auth.empresaId, q } }); forns.value=r.data }
async function buscarProds(q: string) { if (!q||q.length<2) return; const r=await api.get('/produtos', { params:{ empresaId:auth.empresaId, q } }); prods.value=r.data }
function selecionarProd(id: string) { const p=prods.value.find((x: any)=>x.id===id); if (p) { it.value.descricao=p.descricao; it.value.precoUnitario=p.custoUnitario??0 } }
function addItem() { if (!it.value.produtoId) return; np.value.itens.push({...it.value}); it.value={produtoId:'', descricao:'', quantidade:1, precoUnitario:0} }
async function abrirNovo() {
  np.value = { fornecedorId: null, previsaoEntrega: '', itens: [], observacoes: '' }
  mostrarSugestoes.value = true
  qtdSugestao.value = {}
  dialog.value = true
  try {
    const r = await api.get('/estoque/posicao', { params: { empresaId: auth.empresaId, apenasAbaixoMinimo: true } })
    const lista: any[] = r.data?.produtos ?? r.data ?? []
    produtosBaixoEstoque.value = lista
    lista.forEach((p: any) => {
      const falta = (p.estoqueMinimo ?? 0) - (p.quantidadeAtual ?? 0)
      qtdSugestao.value[p.produtoId] = Math.max(1, falta)
    })
  } catch { produtosBaixoEstoque.value = [] }
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
function addTodasSugestoes() {
  produtosBaixoEstoque.value.forEach((p: any) => addSugestao(p))
}
async function salvar() { salvando.value=true; try { await api.post('/pedidos-compra', {...np.value, empresaId:auth.empresaId}); notif.ok('Pedido criado!'); dialog.value=false; await carregar() } finally { salvando.value=false } }
async function enviar(item: any) { await api.post(`/pedidos-compra/${item.id}/enviar`); notif.ok('Pedido enviado!'); await carregar() }
async function abrirRecebimento(item: any) {
  try {
    const r = await api.get(`/pedidos-compra/${item.id}`)
    const detalhe = r.data
    rec.value = { pedidoId: item.id, dataRecebimento: new Date().toISOString().slice(0, 10), numeroNf: '',
      itens: (detalhe.itens ?? []).map((i: any) => ({ itemPedidoId: i.id, descricao: i.descricao ?? i.produtoNome, quantidadePedida: i.quantidade, quantidadeRecebida: i.quantidade })) }
  } catch {
    rec.value = { pedidoId: item.id, dataRecebimento: new Date().toISOString().slice(0, 10), numeroNf: '', itens: [] }
  }
  dialogRec.value = true
}
async function confirmarRec() { salvando.value=true; try { await api.post(`/pedidos-compra/${rec.value.pedidoId}/receber`, rec.value); notif.ok('Recebimento registrado!'); dialogRec.value=false; await carregar() } finally { salvando.value=false } }
onMounted(carregar)
</script>
