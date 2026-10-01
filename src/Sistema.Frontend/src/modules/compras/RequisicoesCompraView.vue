<template>
  <div>
    <div class="d-flex align-center mb-4 gap-2">
      <div class="text-h6 font-weight-bold flex-grow-1">Requisições de Compra</div>
      <v-btn v-if="ehGestor" color="secondary" variant="tonal" prepend-icon="mdi-merge" rounded="lg"
        class="mr-2" @click="abrirUnir">Unir requisições abertas</v-btn>
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
        <b>Confira abaixo o que você já pediu antes de pedir de novo:</b> requisições
        <b>"Processada"</b> já viraram pedido de compra (estão a caminho).
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
          <v-alert type="info" variant="tonal" density="compact" class="mb-3" icon="mdi-format-list-checks">
            Adicione <b>todos os itens</b> na lista abaixo e clique em <b>Enviar</b> <u>uma única vez</u> — é <b>uma</b> requisição com vários itens (não envie um item de cada vez).
          </v-alert>
          <div class="d-flex ga-2 mb-3">
            <v-autocomplete v-model="prodSel" :items="prodOpcoes" :loading="buscandoProd"
              item-title="descricao" item-value="id" return-object no-filter clearable
              label="Buscar produto (nome ou código)" variant="outlined" density="compact" class="flex-grow-1"
              @update:search="buscarProduto" @update:model-value="onSelecionarProd" />
            <v-text-field v-model.number="qtdSel" label="Qtd" type="number" min="1"
              variant="outlined" density="compact" style="width:90px" @keyup.enter="addItem" />
            <v-btn icon="mdi-plus" color="primary" variant="tonal" :disabled="!prodSel" @click="addItem" />
          </div>
          <div class="mb-3">
            <v-btn size="small" variant="text" color="teal" prepend-icon="mdi-plus-box-outline"
              @click="abrirNovoProdRapido">Não achou? Cadastrar produto novo</v-btn>
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
            Enviar requisição ({{ novaItens.length }} {{ novaItens.length === 1 ? 'item' : 'itens' }})
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: cadastro rápido de produto (pedir algo que não existe no cadastro) -->
    <v-dialog v-model="dlgProdRapido" max-width="460" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon icon="mdi-plus-box-outline" color="teal" /> Cadastrar produto novo
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="dlgProdRapido = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-form ref="formProdRapido">
          <div class="text-caption text-medium-emphasis mb-3">
            Cria um produto simples (categoria "Diversos", marca "Sem marca") já para incluir na requisição.
            O gestor completa o cadastro depois.
          </div>
          <v-text-field v-model="prodRapido.descricao" label="Descrição do produto *"
            :rules="[r => !!r || 'Obrigatório']"
            variant="outlined" density="compact" class="mb-2" autofocus />
          <v-select v-model="prodRapido.unidadeMedidaId" :items="unidades" :item-title="uniTitle" item-value="id"
            label="Unidade *" :rules="[r => !!r || 'Obrigatório']"
            variant="outlined" density="compact" />
          </v-form>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3 justify-end">
          <v-btn variant="text" @click="dlgProdRapido = false">Cancelar</v-btn>
          <v-btn color="teal" rounded="lg" :loading="salvandoProdRapido"
            :disabled="!prodRapido.descricao || !prodRapido.unidadeMedidaId"
            @click="salvarProdRapido">Cadastrar e adicionar</v-btn>
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

          <!-- Adicionar item a esta requisição (enquanto Aberta) -->
          <div v-if="det.status === 'Aberta'" class="mb-3">
            <div class="d-flex ga-2">
              <v-autocomplete v-model="addSel" :items="prodOpcoes" :loading="buscandoProd"
                item-title="descricao" item-value="id" return-object no-filter clearable
                label="Adicionar produto a esta requisição" variant="outlined" density="compact" class="flex-grow-1"
                @update:search="buscarProduto" />
              <v-text-field v-model.number="addQtd" label="Qtd" type="number" min="1"
                variant="outlined" density="compact" style="width:90px" @keyup.enter="adicionarItemExistente" />
              <v-btn icon="mdi-plus" color="primary" variant="tonal" :disabled="!addSel" :loading="adicionandoItem"
                @click="adicionarItemExistente" />
            </div>
            <v-btn size="small" variant="text" color="teal" prepend-icon="mdi-plus-box-outline"
              @click="abrirNovoProdRapidoExistente">Não achou? Cadastrar produto novo</v-btn>
          </div>

          <!-- Acompanhamento: o que vai chegar (NF já cruzada) x aguardando fornecedor -->
          <v-alert v-if="conf" :type="conf.completo ? 'success' : 'info'" variant="tonal"
            density="comfortable" class="mb-3">
            <b v-if="conf.completo">Todos os {{ conf.totalItens }} itens já estão cobertos (vão chegar ou já em pedido) — evite pedir de novo.</b>
            <b v-else>{{ conf.jaEmPedido }} já em pedido · {{ conf.vaoChegar }} vão chegar · <span v-if="conf.precisaPedir" class="text-error">{{ conf.precisaPedir }} precisa pedir (estoque baixo)</span> · {{ conf.itensPendentes }} sem pedido (de {{ conf.totalItens }}).</b>
            <div class="text-caption text-medium-emphasis mt-1">
              <b>Já em pedido</b> conta só pedidos <b>em aberto</b> (a caminho) — os <b>Recebidos</b> aparecem como histórico, não cobrem necessidade. Se o <b>estoque</b> está no/abaixo do mínimo, o item mostra <b>Precisa pedir</b> mesmo com OC antiga.
            </div>
            <v-table density="compact" class="mt-2 bg-transparent">
              <thead><tr><th>Produto</th><th class="text-center" style="width:60px">Qtd</th>
                <th class="text-center" style="width:90px">Estoque</th>
                <th style="width:200px">Pedidos</th>
                <th class="text-center" style="width:150px">Situação</th></tr></thead>
              <tbody>
                <tr v-for="l in conf.itens" :key="l.produtoId"
                  :class="l.situacao === 'PrecisaPedir' ? 'bg-red-lighten-5' : (l.jaPedido ? 'bg-amber-lighten-5' : '')">
                  <td>{{ l.descricao }}</td>
                  <td class="text-center">{{ fmtQtd(l.requisitado) }}</td>
                  <td class="text-center">
                    <span :class="l.estoqueBaixo ? 'text-error font-weight-bold' : ''"
                      :title="'Mínimo: ' + fmtQtd(l.estoqueMinimo)">{{ fmtQtd(l.estoqueLoja) }}</span>
                  </td>
                  <td>
                    <template v-if="l.pedidos && l.pedidos.length">
                      <v-chip v-for="p in l.pedidos" :key="p.numero" size="x-small" class="mr-1 mb-1"
                        :color="p.status === 'Recebido' ? 'default' : (p.status === 'Enviado' ? 'info' : 'warning')"
                        variant="tonal" :title="p.status">
                        {{ p.numero }} · {{ p.status }}
                      </v-chip>
                    </template>
                    <span v-else class="text-caption text-medium-emphasis">—</span>
                  </td>
                  <td class="text-center">
                    <v-chip v-if="l.situacao === 'PrecisaPedir'" size="small" color="error" variant="flat">
                      <v-icon start size="14">mdi-alert-circle-outline</v-icon>Precisa pedir
                    </v-chip>
                    <v-chip v-else-if="l.situacao === 'VaiChegar'" size="small" color="success" variant="tonal">
                      <v-icon start size="14">mdi-truck-check-outline</v-icon>Vai chegar
                    </v-chip>
                    <v-chip v-else-if="l.situacao === 'JaPedido'" size="small" color="info" variant="tonal">
                      <v-icon start size="14">mdi-cart-check</v-icon>A caminho
                    </v-chip>
                    <v-chip v-else size="small" color="warning" variant="tonal">
                      <v-icon start size="14">mdi-clock-outline</v-icon>Sem pedido
                    </v-chip>
                  </td>
                </tr>
              </tbody>
            </v-table>
          </v-alert>

          <!-- Agrupamento por fornecedor + gerar pedido: só para o gestor -->
          <template v-if="ehGestor">
          <v-card v-for="g in porFornecedor" :key="g.fornecedor" rounded="lg" variant="outlined" class="mb-3">
            <v-card-title class="text-body-2 font-weight-bold d-flex align-center py-2 flex-wrap ga-2">
              <v-icon icon="mdi-truck-outline" size="18" class="mr-1" /> {{ g.fornecedor }}
              <v-spacer />
              <span class="text-caption text-medium-emphasis">{{ g.itens.length }} item(ns)</span>
              <v-btn v-if="ehGestor && det.status !== 'Cancelada'" size="small" color="primary" variant="tonal"
                rounded="lg" prepend-icon="mdi-cart-plus" :loading="gerando===g.fornecedor"
                :disabled="!g.fornecedorId" @click="gerarPedido(g)">Gerar pedido</v-btn>
            </v-card-title>
            <v-alert v-if="!g.fornecedorId" type="warning" variant="tonal" density="compact" class="mx-3 mb-2">
              Sem fornecedor vinculado — vincule no cadastro do produto (ou na Sugestão de Compra) para gerar o pedido.
            </v-alert>
            <v-table density="compact">
              <thead><tr><th>Produto</th><th class="text-center" style="width:90px">Qtd</th>
                <th v-if="ehGestor" class="text-right" style="width:110px">Custo un.</th>
                <th v-if="ehGestor" class="text-right" style="width:120px">Estimado</th></tr></thead>
              <tbody>
                <tr v-for="i in g.itens" :key="i.itemId ?? i.produtoId">
                  <td>
                    {{ i.descricao }}
                    <v-chip v-if="i.movido" size="x-small" color="info" variant="tonal" class="ml-1">movido</v-chip>
                    <v-btn v-if="ehGestor && det.status !== 'Cancelada'" icon="mdi-account-switch"
                      size="x-small" variant="text" color="primary" class="ml-1"
                      @click="abrirMover(i)" title="Mover para outro fornecedor" />
                    <v-btn v-if="ehGestor && det.status === 'Aberta' && i.itemId" icon="mdi-delete-outline"
                      size="x-small" variant="text" color="error"
                      :loading="removendoItem === i.itemId"
                      @click="removerItem(i)" title="Remover item (não vou mais comprar)" />
                  </td>
                  <td class="text-center">{{ fmtQtd(i.quantidade) }}</td>
                  <td v-if="ehGestor" class="text-right">R$ {{ fmt(i.custoUnitario) }}</td>
                  <td v-if="ehGestor" class="text-right">R$ {{ fmt(i.quantidade * i.custoUnitario) }}</td>
                </tr>
              </tbody>
            </v-table>
          </v-card>
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3 justify-end">
          <v-btn v-if="ehGestor && det.status==='Aberta'" color="success" variant="tonal" rounded="lg"
            prepend-icon="mdi-check" @click="marcarProcessada">Marcar como processada</v-btn>
          <v-btn variant="text" @click="dialogDet = false">Fechar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: mover item para outro fornecedor -->
    <v-dialog v-model="dialogMover" max-width="480">
      <v-card rounded="xl">
        <v-card-title class="pa-4 d-flex align-center">
          <v-icon icon="mdi-account-switch" class="mr-2" />Mover item para fornecedor
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="dialogMover = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <div class="text-body-2 mb-3"><b>{{ itemMover?.descricao }}</b></div>
          <v-autocomplete v-model="fornMoverId" :items="fornecedores"
            item-title="razaoSocial" item-value="id" clearable
            label="Fornecedor (vazio = principal do produto)" variant="outlined" density="compact"
            no-data-text="Nenhum fornecedor" />
          <div class="text-caption text-medium-emphasis mt-1">
            Isto muda para qual pedido este item vai, sem afetar os outros itens.
          </div>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3 justify-end">
          <v-btn variant="text" @click="dialogMover = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="movendo" @click="confirmarMover">Mover</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: unir requisições abertas por loja -->
    <v-dialog v-model="dialogUnir" max-width="720" persistent scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 d-flex align-center">
          <v-icon icon="mdi-merge" class="mr-2" />Unir requisições abertas por loja
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="dialogUnir = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-alert type="info" variant="tonal" density="comfortable" class="mb-3">
            Reúne, em <b>uma única requisição aberta por loja</b>, tudo o que está solto:
            os itens das <b>requisições abertas</b> e dos <b>pedidos de compra em rascunho</b> (não processados).
            As origens são <b>canceladas</b> para não duplicar. Pedidos já enviados/recebidos não são tocados.
          </v-alert>

          <div v-if="carregandoUnir" class="text-center py-6"><v-progress-circular indeterminate color="primary" /></div>

          <template v-else-if="dadosUnir">
            <div class="d-flex ga-2 mb-3">
              <v-chip color="warning" variant="tonal" size="small">Requisições abertas: {{ dadosUnir.requisicoesAbertas }}</v-chip>
              <v-chip color="orange" variant="tonal" size="small">Pedidos em rascunho: {{ dadosUnir.pedidosRascunho }}</v-chip>
            </div>

            <v-alert v-if="!dadosUnir.temPendencias" type="success" variant="tonal" density="comfortable">
              Nada a unir — não há requisições abertas nem pedidos em rascunho.
            </v-alert>

            <v-expansion-panels v-else multiple>
              <v-expansion-panel v-for="lj in dadosUnir.porLoja" :key="lj.localEstoqueId">
                <v-expansion-panel-title>
                  <b>{{ lj.loja }}</b>&nbsp;— {{ lj.itens.length }} produto(s)
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-table density="compact">
                    <thead><tr><th class="text-left">Produto</th><th class="text-right">Qtde</th></tr></thead>
                    <tbody>
                      <tr v-for="it in lj.itens" :key="it.produtoId">
                        <td>{{ it.descricao }}</td>
                        <td class="text-right">{{ fmtQtd(it.quantidade) }}</td>
                      </tr>
                    </tbody>
                  </v-table>
                </v-expansion-panel-text>
              </v-expansion-panel>
            </v-expansion-panels>
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3 justify-end">
          <v-btn variant="text" @click="dialogUnir = false">Cancelar</v-btn>
          <v-btn color="secondary" rounded="lg" prepend-icon="mdi-merge"
            :loading="unindo" :disabled="!dadosUnir?.temPendencias"
            @click="confirmarUnir">
            Unir em 1 requisição por loja
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const ehGestor = computed(() => ['Administrador', 'Gerente'].includes(auth.usuario?.role ?? ''))
const lojaAtual = computed(() => auth.lojaAtualId ?? auth.usuario?.localEstoqueId ?? null)

interface ItemDet { itemId?: string; produtoId: string; descricao: string; quantidade: number; custoUnitario: number; fornecedorId: string | null; fornecedor: string; movido?: boolean }

const carregando = ref(false)
const lista = ref<any[]>([])
const filtroStatus = ref('Todas')
const headers = [
  { title: 'Data', key: 'criadoEm' }, { title: 'Loja', key: 'loja' },
  { title: 'Solicitante', key: 'solicitante' }, { title: 'Itens', key: 'qtdItens', align: 'center' as const },
  { title: 'Status', key: 'status' }, { title: '', key: 'acoes', sortable: false, width: 240 },
]
const listaFiltrada = computed(() => {
  // Filtro de status no cliente (instantâneo). No "Todas":
  //  • Gestor esconde as finalizadas (Processada + Cancelada) — foco no que falta processar.
  //  • Atendente esconde só Cancelada e MANTÉM as Processadas visíveis, para enxergar o que já
  //    pediu (que virou pedido / está a caminho) e não fazer requisição duplicada.
  const s = filtroStatus.value
  if (s === 'Todas') {
    return lista.value.filter((r: any) => ehGestor.value
      ? (r.status !== 'Processada' && r.status !== 'Cancelada')
      : (r.status !== 'Cancelada'))
  }
  return lista.value.filter((r: any) => r.status === s)
})

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtQtd = (v: number) => (v ?? 0).toLocaleString('pt-BR', { maximumFractionDigits: 3 })
const corStatus = (s: string) => ({ Aberta: 'warning', Processada: 'success', Cancelada: 'error' } as any)[s] ?? 'default'

async function carregar() {
  carregando.value = true
  try {
    // Carrega TODAS as requisições; o filtro de status é aplicado no cliente (listaFiltrada).
    const r = await api.get('/requisicoes-compra', { params: { empresaId: auth.empresaId } })
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

// ── Cadastro rápido de produto (pedir algo que não existe no cadastro) ──
const unidades = ref<any[]>([])
const uniTitle = (u: any) => u?.descricao ? `${u.sigla} — ${u.descricao}` : (u?.sigla ?? '')
const dlgProdRapido = ref(false)
const formProdRapido = ref()  // <v-form> do cadastro rápido de produto
const salvandoProdRapido = ref(false)
const prodRapidoModo = ref<'nova' | 'existente'>('nova')
const prodRapido = ref<{ descricao: string; unidadeMedidaId: string | null }>({ descricao: '', unidadeMedidaId: null })
async function carregarUnidades() {
  if (unidades.value.length) return
  try {
    const r = await api.get('/unidades-medida', { params: { empresaId: auth.empresaId } })
    unidades.value = Array.isArray(r.data) ? r.data : (r.data.itens ?? [])
  } catch { unidades.value = [] }
}
async function abrirNovoProdRapido() {
  prodRapidoModo.value = 'nova'
  await carregarUnidades()
  prodRapido.value = { descricao: '', unidadeMedidaId: unidades.value[0]?.id ?? null }
  dlgProdRapido.value = true
  nextTick(() => formProdRapido.value?.resetValidation())
}
async function abrirNovoProdRapidoExistente() {
  prodRapidoModo.value = 'existente'
  await carregarUnidades()
  prodRapido.value = { descricao: '', unidadeMedidaId: unidades.value[0]?.id ?? null }
  dlgProdRapido.value = true
  nextTick(() => formProdRapido.value?.resetValidation())
}
async function salvarProdRapido() {
  const _v = await formProdRapido.value?.validate()
  if (_v && _v.valid === false) { notif.erro('Preencha os campos destacados em vermelho.'); return }
  const pr = prodRapido.value
  if (!pr.descricao || !pr.unidadeMedidaId) return
  salvandoProdRapido.value = true
  try {
    const { data } = await api.post('/produtos/rapido', {
      empresaId: auth.empresaId,
      descricao: pr.descricao.trim(),
      unidadeMedidaId: pr.unidadeMedidaId,
    })
    if (prodRapidoModo.value === 'existente' && det.value?.id) {
      await api.post(`/requisicoes-compra/${det.value.id}/itens`, {
        itens: [{ produtoId: data.id, quantidade: Math.max(1, Number(addQtd.value) || 1) }],
      })
      await recarregarDetalhe()
    } else {
      novaItens.value.push({ produtoId: data.id, descricao: data.descricao, quantidade: Math.max(1, Number(qtdSel.value) || 1) })
    }
    dlgProdRapido.value = false
    notif.ok(`Produto "${data.descricao}" criado e adicionado.`)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Não foi possível cadastrar o produto.')
  } finally { salvandoProdRapido.value = false }
}

// Adicionar item a uma requisição JÁ existente (aberta).
const addSel = ref<any>(null)
const addQtd = ref(1)
const adicionandoItem = ref(false)
async function adicionarItemExistente() {
  if (!addSel.value || !det.value?.id) return
  adicionandoItem.value = true
  try {
    await api.post(`/requisicoes-compra/${det.value.id}/itens`, {
      itens: [{ produtoId: addSel.value.id, quantidade: Math.max(1, Number(addQtd.value) || 1) }],
    })
    notif.ok('Item adicionado à requisição.')
    addSel.value = null; addQtd.value = 1; prodOpcoes.value = []
    await recarregarDetalhe()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao adicionar item.') }
  finally { adicionandoItem.value = false }
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
const conf = ref<any>(null)
const mostrarConf = ref(false)

async function carregarConferencia() {
  if (!det.value?.id) return
  try {
    const r = await api.get(`/requisicoes-compra/${det.value.id}/conferencia`)
    conf.value = r.data
  } catch { conf.value = null }
}

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
    conf.value = null; mostrarConf.value = false
    dialogDet.value = true
    carregarConferencia()
  } catch { notif.erro('Erro ao carregar a requisição.') }
}
async function recarregarDetalhe() {
  if (!det.value?.id) return
  const loja = det.value.loja, solic = det.value.solicitante
  const r = await api.get(`/requisicoes-compra/${det.value.id}`)
  det.value = { ...r.data, loja, solicitante: solic }
  carregarConferencia()
}

// ── Mover item para outro fornecedor ──
const fornecedores = ref<any[]>([])
const dialogMover = ref(false)
const movendo = ref(false)
const itemMover = ref<ItemDet | null>(null)
const fornMoverId = ref<string | null>(null)
async function carregarFornecedores() {
  try {
    const r = await api.get('/fornecedores', { params: { empresaId: auth.empresaId, ativo: true } })
    fornecedores.value = r.data ?? []
  } catch { fornecedores.value = [] }
}
function abrirMover(i: ItemDet) {
  itemMover.value = i
  fornMoverId.value = i.movido ? (i.fornecedorId ?? null) : null
  dialogMover.value = true
}
// ── Remover item da requisição (produto que não vai mais comprar) ──
const removendoItem = ref<string | null>(null)
async function removerItem(i: ItemDet) {
  if (!i.itemId || !det.value?.id) return
  if (!confirm(`Remover "${i.descricao}" desta requisição?`)) return
  removendoItem.value = i.itemId
  try {
    await api.delete(`/requisicoes-compra/${det.value.id}/itens/${i.itemId}`)
    notif.ok('Item removido.')
    await recarregarDetalhe()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao remover o item.') }
  finally { removendoItem.value = null }
}

async function confirmarMover() {
  if (!itemMover.value?.itemId) return
  movendo.value = true
  try {
    await api.patch(`/requisicoes-compra/itens/${itemMover.value.itemId}/fornecedor`,
      { fornecedorId: fornMoverId.value || null })
    notif.ok('Item movido de fornecedor.')
    dialogMover.value = false
    await recarregarDetalhe()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao mover o item.') }
  finally { movendo.value = false }
}

async function gerarPedido(g: { fornecedor: string; fornecedorId: string | null; itens: ItemDet[] }) {
  if (!g.fornecedorId) { notif.aviso('Grupo sem fornecedor vinculado.'); return }
  gerando.value = g.fornecedor
  try {
    await api.post('/pedidos-compra', {
      empresaId: auth.empresaId,
      fornecedorId: g.fornecedorId,
      usuarioId: auth.usuario?.id,
      localEstoqueId: det.value?.localEstoqueId ?? null,
      requisicaoCompraId: det.value?.id ?? null,
      itens: g.itens.map(i => ({
        produtoId: i.produtoId, descricao: i.descricao,
        quantidade: i.quantidade, precoUnitario: i.custoUnitario,
      })),
    })
    notif.ok(`Pedido criado para ${g.fornecedor}. Requisição marcada como processada.`)
    if (det.value) det.value.status = 'Processada'
    await carregar()
    await carregarConferencia()
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

// ── Unir requisições abertas por loja (junta abertas + pedidos em rascunho) ──
// Endpoint backend continua /pendencias (leitura) e /consolidar (ação).
const dialogUnir = ref(false)
const carregandoUnir = ref(false)
const unindo = ref(false)
const dadosUnir = ref<any>(null)

async function abrirUnir() {
  dialogUnir.value = true
  carregandoUnir.value = true
  dadosUnir.value = null
  try {
    const r = await api.get('/requisicoes-compra/pendencias', { params: { empresaId: auth.empresaId } })
    dadosUnir.value = r.data
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao carregar pendências.') }
  finally { carregandoUnir.value = false }
}

async function confirmarUnir() {
  if (!dadosUnir.value?.temPendencias) return
  if (!confirm('Unir todas as requisições abertas em uma por loja? As requisições abertas e os pedidos em rascunho atuais serão cancelados.')) return
  unindo.value = true
  try {
    const r = await api.post('/requisicoes-compra/consolidar', {
      empresaId: auth.empresaId,
      usuarioId: auth.usuario?.id,
    })
    notif.ok(r.data?.mensagem ?? 'Requisições unidas!')
    dialogUnir.value = false
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao unir.') }
  finally { unindo.value = false }
}

onMounted(() => { carregar(); if (ehGestor.value) carregarFornecedores() })
</script>
