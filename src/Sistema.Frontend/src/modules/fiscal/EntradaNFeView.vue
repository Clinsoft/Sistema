<template>
  <div>
    <!-- Cabeçalho -->
    <div class="d-flex align-center mb-4 gap-2">
      <v-btn icon="mdi-arrow-left" variant="text" @click="$router.back()" />
      <div class="flex-grow-1">
        <div class="text-h6 font-weight-bold">Escrituração de Entrada</div>
        <div class="text-caption text-medium-emphasis">
          {{ entrada?.emitenteNome }} ·
          Chave: {{ entrada?.chaveAcesso?.substring(0, 22) }}…
        </div>
      </div>
      <v-chip :color="corStatus(entrada?.status)" variant="tonal" class="font-weight-medium">
        {{ entrada?.status }}
      </v-chip>
      <v-menu>
        <template #activator="{ props }">
          <v-btn v-bind="props" icon="mdi-dots-vertical" variant="outlined" />
        </template>
        <v-list density="compact">
          <v-list-item prepend-icon="mdi-content-copy" title="Clonar entrada" @click="clonar" />
          <v-list-item prepend-icon="mdi-printer-outline" title="Imprimir etiquetas"
            @click="dlgEtiquetas = true" :disabled="entrada?.status !== 'Processada'" />
          <v-divider />
          <v-list-item prepend-icon="mdi-swap-horizontal" title="Clonar para Saída (NF-e)"
            subtitle="Gera NF-e de saída com estes itens"
            @click="clonarParaSaida" :disabled="entrada?.status !== 'Processada'" />
          <v-list-item prepend-icon="mdi-arrow-u-left-top" title="Devolver Mercadoria"
            subtitle="Gera NF-e de devolução ao fornecedor"
            class="text-warning"
            @click="dlgDevolucao = true" :disabled="entrada?.status !== 'Processada'" />
          <v-divider />
          <v-list-item prepend-icon="mdi-undo" title="Estornar" class="text-warning"
            @click="dlgEstorno = true" :disabled="entrada?.status !== 'Processada'" />
          <v-list-item prepend-icon="mdi-delete-outline" title="Excluir" class="text-error"
            @click="excluir" :disabled="entrada?.status === 'Processada'" />
        </v-list>
      </v-menu>
    </div>

    <v-tabs v-model="aba" class="mb-4" bg-color="transparent">
      <v-tab value="dados">Dados da Nota</v-tab>
      <v-tab value="itens">
        Itens
        <v-badge v-if="itensSemProduto > 0" :content="itensSemProduto" color="warning" inline />
      </v-tab>
      <v-tab value="financeiro">Financeiro / Faturas</v-tab>
    </v-tabs>

    <!-- ─── ABA: DADOS DA NOTA ─── -->
    <div v-if="aba === 'dados'">
      <v-row>
        <v-col cols="12" md="6">
          <v-card rounded="xl" elevation="1" class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-3">Emitente</div>
            <v-text-field :model-value="entrada?.emitenteNome" label="Razão Social"
              variant="outlined" density="compact" readonly />
            <v-text-field :model-value="fmtCnpj(entrada?.emitenteCnpj)" label="CNPJ"
              variant="outlined" density="compact" readonly />
            <v-autocomplete v-model="fornecedorId" label="Vincular ao fornecedor cadastrado"
              :items="fornecedores" item-title="razaoSocial" item-value="id"
              variant="outlined" density="compact" clearable
              @update:model-value="salvarFornecedor" />
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card rounded="xl" elevation="1" class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-3">Dados Gerais</div>
            <v-row dense>
              <v-col cols="6">
                <v-text-field :model-value="fmtData(entrada?.dataEmissao)" label="Data Emissão"
                  variant="outlined" density="compact" readonly />
              </v-col>
              <v-col cols="6">
                <v-text-field :model-value="fmtData(entrada?.dataEntrada)" label="Data Entrada"
                  variant="outlined" density="compact" readonly />
              </v-col>
              <v-col cols="12">
                <v-select v-model="localEstoqueId" label="Local de Estoque *"
                  :items="locaisEstoque" item-title="nome" item-value="id"
                  variant="outlined" density="compact"
                  :disabled="entrada?.status === 'Processada'" />
              </v-col>
              <v-col cols="12">
                <v-autocomplete v-model="pedidoCompraId" label="Vincular Ordem de Compra"
                  :items="pedidosCompra" item-title="label" item-value="id"
                  variant="outlined" density="compact" clearable
                  @update:model-value="salvarPedidoCompra" />
              </v-col>
            </v-row>
          </v-card>
        </v-col>
        <v-col cols="12">
          <v-card rounded="xl" elevation="1" class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-3">Valores da Nota</div>
            <v-row dense>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(entrada?.valorProdutos)" label="Produtos"
                  variant="outlined" density="compact" readonly prefix="R$" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(entrada?.valorFrete)" label="Frete (XML)"
                  variant="outlined" density="compact" readonly prefix="R$" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field v-model.number="freteManual" label="Frete Manual"
                  type="number" variant="outlined" density="compact" prefix="R$"
                  :disabled="entrada?.status === 'Processada'"
                  @blur="salvarFreteManual" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(entrada?.valorIpi)" label="IPI"
                  variant="outlined" density="compact" readonly prefix="R$" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(entrada?.valorIcmsSt)" label="ICMS ST"
                  variant="outlined" density="compact" readonly prefix="R$" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(entrada?.valorTotal)" label="Total"
                  variant="outlined" density="compact" readonly prefix="R$"
                  class="font-weight-bold" />
              </v-col>
            </v-row>
          </v-card>
        </v-col>
      </v-row>
    </div>

    <!-- ─── ABA: ITENS ─── -->
    <div v-if="aba === 'itens'">
      <v-card rounded="xl" elevation="1">
        <v-data-table :headers="headersItens" :items="entrada?.itens ?? []"
          density="comfortable" :items-per-page="50">
          <template #item.descricaoXml="{ item }">
            <div class="text-body-2 font-weight-medium">{{ item.descricaoXml }}</div>
            <div class="text-caption text-medium-emphasis">
              NCM: {{ item.ncmXml }} · Cód. Fornecedor: {{ item.codigoFornecedor ?? '-' }}
            </div>
          </template>

          <template #item.cfop="{ item }">
            <v-chip size="small" variant="tonal"
              :color="item.cfopUtilizado !== item.cfopXml ? 'warning' : 'default'">
              {{ item.cfopUtilizado }}
            </v-chip>
            <div v-if="item.cfopUtilizado !== item.cfopXml"
              class="text-caption text-medium-emphasis">XML: {{ item.cfopXml }}</div>
          </template>

          <template #item.quantidade="{ item }">
            <div class="text-body-2">{{ item.quantidadeXml }} {{ item.unidadeXml }}</div>
            <div v-if="item.fatorConversao !== 1" class="text-caption text-success">
              → {{ item.quantidadeEstoque }} {{ item.unidadeEstoque }}
            </div>
          </template>

          <template #item.valores="{ item }">
            <div class="text-body-2">R$ {{ fmt(item.valorUnitarioXml) }}</div>
            <div class="text-caption text-medium-emphasis">Total: R$ {{ fmt(item.valorTotalXml) }}</div>
          </template>

          <template #item.produto="{ item }">
            <div v-if="item.produtoId" class="d-flex align-center gap-1">
              <v-icon color="success" size="16">mdi-check-circle</v-icon>
              <span class="text-body-2">{{ item.produtoDescricao }}</span>
            </div>
            <v-chip v-else color="warning" size="small" variant="tonal">
              <v-icon start size="14">mdi-alert</v-icon>
              Sem vínculo
            </v-chip>
          </template>

          <template #item.lote="{ item }">
            <div v-if="item.numeroLote" class="text-caption">
              <div>{{ item.numeroLote }}</div>
              <div class="text-medium-emphasis">Val: {{ fmtData(item.validade) }}</div>
            </div>
            <span v-else class="text-caption text-medium-emphasis">—</span>
          </template>

          <template #item.acoes="{ item }">
            <v-btn icon="mdi-pencil-outline" size="small" variant="text"
              @click="abrirEditarItem(item)"
              :disabled="entrada?.status === 'Processada'" />
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- ─── ABA: FINANCEIRO ─── -->
    <div v-if="aba === 'financeiro'">
      <v-card rounded="xl" elevation="1" class="pa-4 mb-4">
        <div class="d-flex justify-space-between align-center mb-3">
          <div class="text-subtitle-2 font-weight-bold">Faturas / Contas a Pagar</div>
          <v-btn v-if="entrada?.status !== 'Processada'"
            size="small" color="primary" variant="tonal"
            prepend-icon="mdi-plus" @click="adicionarFatura">
            Adicionar Fatura
          </v-btn>
        </div>

        <v-alert v-if="entrada?.status === 'Processada'" type="success" variant="tonal"
          density="compact" class="mb-3">
          Lançamentos registrados em Contas a Pagar.
        </v-alert>

        <div v-for="(f, i) in faturas" :key="i" class="d-flex align-center gap-2 mb-2">
          <v-text-field v-model="f.label" :label="`Fatura ${i + 1}`"
            variant="outlined" density="compact" hide-details readonly style="max-width:80px" />
          <v-text-field v-model.number="f.valor" label="Valor" type="number"
            variant="outlined" density="compact" hide-details prefix="R$" style="max-width:140px"
            :disabled="entrada?.status === 'Processada'" />
          <v-text-field v-model="f.vencimento" label="Vencimento" type="date"
            variant="outlined" density="compact" hide-details style="max-width:160px"
            :disabled="entrada?.status === 'Processada'" />
          <v-btn v-if="entrada?.status !== 'Processada'"
            icon="mdi-delete-outline" size="small" variant="text" color="error"
            @click="faturas.splice(i, 1)" />
        </div>

        <div v-if="faturas.length === 0" class="text-caption text-medium-emphasis text-center py-4">
          Nenhuma fatura adicionada. Use "+ Adicionar Fatura" para parcelar.
        </div>

        <v-divider class="my-3" />
        <div class="d-flex justify-space-between">
          <span class="text-body-2">Total das faturas:</span>
          <span class="font-weight-bold"
            :class="Math.abs(totalFaturas - (entrada?.valorTotal ?? 0)) > 0.01 ? 'text-error' : 'text-success'">
            R$ {{ fmt(totalFaturas) }}
            <span v-if="Math.abs(totalFaturas - (entrada?.valorTotal ?? 0)) > 0.01"
              class="text-caption ml-1">(diverge do total da nota: R$ {{ fmt(entrada?.valorTotal) }})</span>
          </span>
        </div>
      </v-card>
    </div>

    <!-- Botão Processar -->
    <div v-if="entrada?.status === 'EmEdicao'" class="d-flex justify-end mt-4">
      <v-btn color="success" size="large" rounded="lg" :loading="processando"
        prepend-icon="mdi-check-all" @click="processar">
        Processar Entrada
      </v-btn>
    </div>

    <!-- ─── Dialog: Editar Item ─── -->
    <v-dialog v-model="dlgItem" max-width="600" persistent>
      <v-card rounded="xl" v-if="itemEditando">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="primary">mdi-pencil</v-icon>
          Editar Item {{ itemEditando.numeroItem }}
        </v-card-title>
        <v-card-text>
          <div class="text-caption text-medium-emphasis mb-3">{{ itemEditando.descricaoXml }}</div>
          <v-row dense>
            <v-col cols="6">
              <v-text-field v-model="itemForm.cfopUtilizado" label="CFOP"
                variant="outlined" density="compact" hint="Ex: 1102, 2102" persistent-hint />
            </v-col>
            <v-col cols="6">
              <v-autocomplete v-model="itemForm.produtoId" label="Produto cadastrado"
                :items="produtos" item-title="descricao" item-value="id"
                variant="outlined" density="compact" clearable
                @update:model-value="onProdutoSelecionado" />
            </v-col>

            <!-- Conversão de unidade -->
            <v-col cols="12">
              <div class="text-caption font-weight-medium mb-1">
                Conversão de Unidade
                <span class="text-medium-emphasis">({{ itemEditando.quantidadeXml }} {{ itemEditando.unidadeXml }} no XML)</span>
              </div>
            </v-col>
            <v-col cols="4">
              <v-text-field v-model.number="itemForm.fatorConversao" label="Fator"
                type="number" variant="outlined" density="compact"
                hint="1 = sem conversão" persistent-hint />
            </v-col>
            <v-col cols="4">
              <v-text-field v-model="itemForm.unidadeEstoque" label="Unidade Estoque"
                variant="outlined" density="compact" hint="Ex: KG, UN, L" persistent-hint />
            </v-col>
            <v-col cols="4">
              <v-text-field
                :model-value="fmt(itemEditando.quantidadeXml * (itemForm.fatorConversao || 1))"
                label="Qtd. Estoque" variant="outlined" density="compact" readonly
                :suffix="itemForm.unidadeEstoque" />
            </v-col>

            <!-- Lote e Validade -->
            <v-col cols="6">
              <v-text-field v-model="itemForm.numeroLote" label="Número do Lote"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="itemForm.validade" label="Validade" type="date"
                variant="outlined" density="compact" />
            </v-col>

            <!-- Formação de Preço -->
            <v-col cols="12">
              <div class="text-caption font-weight-medium mb-1">Formação de Preço</div>
            </v-col>
            <v-col cols="4">
              <v-text-field :model-value="fmt(itemEditando.custoUnitarioFinal || itemEditando.valorUnitarioXml)"
                label="Custo c/ rateio" variant="outlined" density="compact" readonly prefix="R$" />
            </v-col>
            <v-col cols="4">
              <v-text-field v-model.number="itemForm.markupSugerido" label="Markup"
                type="number" variant="outlined" density="compact"
                hint="Ex: 2.5 = 150% margem" persistent-hint />
            </v-col>
            <v-col cols="4">
              <v-text-field
                :model-value="fmt((itemEditando.custoUnitarioFinal || itemEditando.valorUnitarioXml) * (itemForm.markupSugerido || 1))"
                label="Preço Sugerido" variant="outlined" density="compact" readonly prefix="R$" />
            </v-col>

            <!-- Tags -->
            <v-col cols="12">
              <v-combobox v-model="itemFormTags" label="Tags do produto"
                multiple chips closable-chips
                variant="outlined" density="compact"
                hint="Enter para adicionar tag" persistent-hint />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgItem = false" :disabled="salvandoItem">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvandoItem" @click="salvarItem">
            Salvar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Estorno -->
    <v-dialog v-model="dlgEstorno" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="warning">mdi-undo</v-icon>
          Estornar Entrada
        </v-card-title>
        <v-card-text>
          <v-alert type="warning" variant="tonal" density="compact" class="mb-3">
            Isso irá estornar as movimentações de estoque e cancelar os lançamentos financeiros em aberto.
          </v-alert>
          <v-textarea v-model="motivoEstorno" label="Motivo do estorno *"
            rows="3" variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgEstorno = false">Cancelar</v-btn>
          <v-btn color="warning" rounded="lg" :loading="estornando" @click="estornar">
            Confirmar Estorno
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Imprimir Etiquetas -->
    <v-dialog v-model="dlgEtiquetas" max-width="480">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="primary">mdi-printer</v-icon>
          Imprimir Etiquetas
        </v-card-title>
        <v-card-text>
          <div class="text-body-2 mb-3">
            Serão geradas etiquetas para todos os {{ entrada?.itens?.length }} itens desta entrada.
          </div>
          <v-select v-model="templateEtiqueta" label="Template de etiqueta"
            :items="templatesEtiqueta" item-title="nome" item-value="id"
            variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgEtiquetas = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" @click="imprimirEtiquetas">
            <v-icon start>mdi-printer</v-icon>
            Gerar PDF
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Devolução -->
    <v-dialog v-model="dlgDevolucao" max-width="520" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="warning">mdi-arrow-u-left-top</v-icon>
          Devolver Mercadoria
        </v-card-title>
        <v-card-text>
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            Será gerada uma NF-e de devolução modelo 55. Após a devolução, acesse
            <strong>Documentos Fiscais → Emitidas</strong> para assinar e transmitir.
          </v-alert>
          <div class="text-body-2 mb-2 font-weight-medium">Selecione os itens a devolver:</div>
          <v-checkbox v-for="item in itensProdutoVinculado" :key="item.id"
            v-model="itensDevolucao" :value="item.id"
            :label="`${item.produtoDescricao} — ${item.quantidadeEstoque} ${item.unidadeEstoque ?? item.unidadeXml}`"
            density="compact" hide-details />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgDevolucao = false" :disabled="devolvendo">Cancelar</v-btn>
          <v-btn color="warning" rounded="lg" :loading="devolvendo"
            :disabled="!itensDevolucao.length" @click="confirmarDevolucao">
            Gerar NF-e de Devolução
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import { formatarCnpj } from '@/utils/documento'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const notif = useNotifStore()

const entradaId = route.params.id as string
const entrada = ref<any>(null)
const aba = ref('dados')
const processando = ref(false)
const estornando = ref(false)
const salvandoItem = ref(false)
const devolvendo = ref(false)
const dlgDevolucao = ref(false)
const itensDevolucao = ref<string[]>([])
const itensProdutoVinculado = computed(() =>
  (entrada.value?.itens ?? []).filter((i: any) => i.produtoId))

// Listas auxiliares
const fornecedores = ref<any[]>([])
const locaisEstoque = ref<any[]>([])
const pedidosCompra = ref<any[]>([])
const produtos = ref<any[]>([])
const templatesEtiqueta = ref<any[]>([])

// Formulário frete e vínculos
const freteManual = ref(0)
const fornecedorId = ref<string | null>(null)
const localEstoqueId = ref<string | null>(null)
const pedidoCompraId = ref<string | null>(null)

// Financeiro
const faturas = ref<{ label: string; valor: number; vencimento: string }[]>([])
const totalFaturas = computed(() => faturas.value.reduce((s, f) => s + (f.valor || 0), 0))

// Dialogs
const dlgItem = ref(false)
const dlgEstorno = ref(false)
const dlgEtiquetas = ref(false)
const motivoEstorno = ref('')
const templateEtiqueta = ref<string | null>(null)
const itemEditando = ref<any>(null)
const itemFormTags = ref<string[]>([])
const itemForm = ref({
  cfopUtilizado: '',
  produtoId: null as string | null,
  produtoDescricao: '',
  fatorConversao: 1,
  unidadeEstoque: '',
  numeroLote: '',
  validade: '',
  markupSugerido: 2.5,
})

const itensSemProduto = computed(
  () => entrada.value?.itens?.filter((i: any) => !i.produtoId).length ?? 0)

const headersItens = [
  { title: '#', key: 'numeroItem', width: 40 },
  { title: 'Produto / Descrição XML', key: 'descricaoXml' },
  { title: 'CFOP', key: 'cfop' },
  { title: 'Quantidade', key: 'quantidade' },
  { title: 'Valor', key: 'valores' },
  { title: 'Produto Cadastrado', key: 'produto' },
  { title: 'Lote / Validade', key: 'lote' },
  { title: '', key: 'acoes', sortable: false, align: 'end' as const },
]

function corStatus(s: string) {
  return ({ EmEdicao: 'warning', Processada: 'success', Estornada: 'error' } as any)[s] ?? 'default'
}

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtData = (v: string) => v ? new Date(v).toLocaleDateString('pt-BR') : '-'
const fmtCnpj = formatarCnpj

async function carregar() {
  const r = await api.get(`/fiscal/entradas/${entradaId}`)
  entrada.value = r.data
  freteManual.value = r.data.valorFreteManual ?? 0
  fornecedorId.value = r.data.fornecedorId ?? null
  localEstoqueId.value = r.data.localEstoqueId ?? null
  pedidoCompraId.value = r.data.pedidoCompraId ?? null

  // Pré-popular faturas: primeiro tenta duplicatas salvas pelo import XML
  if (faturas.value.length === 0 && r.data.status === 'EmEdicao') {
    const chaveStorage = `entrada_duplicatas_${entradaId}`
    const dupJson = sessionStorage.getItem(chaveStorage)
    if (dupJson) {
      const dups = JSON.parse(dupJson)
      faturas.value = dups.map((d: any, i: number) => ({
        label: d.numero || String(i + 1),
        valor: d.valor,
        vencimento: d.vencimento?.slice(0, 10) ?? new Date().toISOString().slice(0, 10),
      }))
      sessionStorage.removeItem(chaveStorage)
    } else {
      const venc = new Date()
      venc.setDate(venc.getDate() + 30)
      faturas.value = [{
        label: '1',
        valor: r.data.valorTotal,
        vencimento: venc.toISOString().slice(0, 10),
      }]
    }
  }
}

async function carregarAuxiliares() {
  const [forn, locais, pedidos, prods] = await Promise.all([
    api.get('/fornecedores', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] })),
    api.get('/locais-estoque', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] })),
    api.get('/compras/pedidos', { params: { empresaId: auth.empresaId, status: 'Enviado' } }).catch(() => ({ data: [] })),
    api.get('/estoque/produtos', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] })),
  ])
  fornecedores.value = forn.data
  locaisEstoque.value = locais.data
  pedidosCompra.value = pedidos.data.map((p: any) => ({
    ...p,
    label: `OC #${p.numero} – ${fmtData(p.dataPedido)}`,
  }))
  produtos.value = prods.data
}

async function salvarFreteManual() {
  await api.patch(`/fiscal/entradas/${entradaId}/frete-manual`, { valor: freteManual.value })
  notif.ok('Frete atualizado.')
  await carregar()
}

async function salvarFornecedor(id: string | null) {
  if (!id) return
  await api.patch(`/fiscal/entradas/${entradaId}/fornecedor`, { fornecedorId: id })
}

async function salvarPedidoCompra(id: string | null) {
  if (!id) return
  await api.patch(`/fiscal/entradas/${entradaId}/pedido-compra`, { pedidoCompraId: id })
    .then(() => notif.ok('Ordem de compra vinculada.'))
}

function abrirEditarItem(item: any) {
  itemEditando.value = item
  itemForm.value = {
    cfopUtilizado: item.cfopUtilizado,
    produtoId: item.produtoId ?? null,
    produtoDescricao: item.produtoDescricao ?? '',
    fatorConversao: item.fatorConversao ?? 1,
    unidadeEstoque: item.unidadeEstoque ?? item.unidadeXml,
    numeroLote: item.numeroLote ?? '',
    validade: item.validade ? item.validade.slice(0, 10) : '',
    markupSugerido: item.markupSugerido ?? 2.5,
  }
  itemFormTags.value = item.tags ? item.tags.split(',').map((t: string) => t.trim()) : []
  dlgItem.value = true
}

function onProdutoSelecionado(id: string | null) {
  const prod = produtos.value.find((p: any) => p.id === id)
  if (prod) {
    itemForm.value.produtoDescricao = prod.descricao
    if (!itemForm.value.unidadeEstoque) itemForm.value.unidadeEstoque = prod.unidadeMedida ?? ''
  }
}

async function salvarItem() {
  salvandoItem.value = true
  try {
    await api.patch(`/fiscal/entradas/${entradaId}/itens/${itemEditando.value.id}`, {
      ...itemForm.value,
      validade: itemForm.value.validade || null,
      tags: itemFormTags.value.join(', '),
    })
    notif.ok('Item atualizado.')
    dlgItem.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao salvar item.')
  } finally { salvandoItem.value = false }
}

function adicionarFatura() {
  const num = faturas.value.length + 1
  const venc = new Date()
  venc.setMonth(venc.getMonth() + num)
  faturas.value.push({
    label: String(num),
    valor: 0,
    vencimento: venc.toISOString().slice(0, 10),
  })
}

async function processar() {
  if (itensSemProduto.value > 0) {
    notif.erro(`${itensSemProduto.value} item(ns) sem produto vinculado. Acesse a aba Itens.`)
    aba.value = 'itens'
    return
  }
  if (faturas.value.length === 0) {
    notif.erro('Adicione ao menos uma fatura na aba Financeiro.')
    aba.value = 'financeiro'
    return
  }
  processando.value = true
  try {
    await api.post(`/fiscal/entradas/${entradaId}/processar`, {
      faturas: faturas.value.map(f => ({ valor: f.valor, vencimento: f.vencimento })),
    })
    notif.ok('Entrada processada! Estoque e financeiro atualizados.')
    await carregar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao processar entrada.')
  } finally { processando.value = false }
}

async function estornar() {
  if (!motivoEstorno.value) { notif.erro('Informe o motivo do estorno.'); return }
  estornando.value = true
  try {
    const r = await api.post(`/fiscal/entradas/${entradaId}/estornar`, { motivo: motivoEstorno.value })
    notif.ok(`Entrada estornada. ${r.data.lancamentosCancelados} lançamento(s) cancelado(s).`)
    dlgEstorno.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao estornar.')
  } finally { estornando.value = false }
}

async function clonar() {
  const r = await api.post(`/fiscal/entradas/${entradaId}/clonar`)
  notif.ok('Entrada clonada!')
  router.push(`/fiscal/entradas/${r.data.id}`)
}

async function clonarParaSaida() {
  try {
    const r = await api.post(`/fiscal/entradas/${entradaId}/clonar-para-saida`)
    notif.ok(r.data.mensagem)
    router.push('/fiscal')
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao clonar para saída.')
  }
}

async function confirmarDevolucao() {
  devolvendo.value = true
  try {
    const r = await api.post(`/fiscal/entradas/${entradaId}/devolver`, {
      itens: itensDevolucao.value.length ? itensDevolucao.value : null,
    })
    notif.ok(r.data.mensagem)
    dlgDevolucao.value = false
    router.push('/fiscal')
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao gerar devolução.')
  } finally { devolvendo.value = false }
}

async function excluir() {
  await api.delete(`/fiscal/entradas/${entradaId}`)
  notif.ok('Entrada excluída.')
  router.push('/fiscal')
}

async function imprimirEtiquetas() {
  const r = await api.get(`/fiscal/entradas/${entradaId}/etiquetas`, {
    params: { templateId: templateEtiqueta.value },
  })
  // Redireciona para o editor de etiquetas com os dados
  router.push({
    path: '/estoque/etiquetas',
    query: { preCarregado: JSON.stringify(r.data) },
  })
  dlgEtiquetas.value = false
}

onMounted(async () => {
  await Promise.all([carregar(), carregarAuxiliares()])
})
</script>
