<template>
  <div>
    <div class="d-flex align-center mb-4 flex-wrap gap-2">
      <div class="text-h6 font-weight-bold flex-grow-1">Materiais de Consumo</div>
      <v-btn color="teal" variant="tonal" rounded="lg" prepend-icon="mdi-tray-arrow-down"
        @click="abrirConsumo">Baixar consumo</v-btn>
      <v-btn color="secondary" variant="tonal" rounded="lg" prepend-icon="mdi-clipboard-list-outline"
        @click="abrirInventario">Inventário</v-btn>
      <v-btn color="primary" rounded="lg" prepend-icon="mdi-plus" @click="abrirNovo">Novo material</v-btn>
    </div>

    <GuiaPassos
      id="materiais-consumo"
      titulo="Como usar Materiais de Consumo"
      :passos="[
        'Aqui ficam os itens de <b>uso interno</b> (embalagens, sacolas, etiquetas, limpeza). Eles <b>não</b> aparecem no PDV, no catálogo nem na formação de preço.',
        'Entradas: pela <b>NF-e</b> (marque a nota como <b>Material de Consumo</b> na escrituração) ou por <b>compra manual</b> no botão ⊕ da linha.',
        'Saídas: use <b>Baixar consumo</b> para dar baixa do que foi usado no dia (ex.: 80 embalagens, 45 sacolas). Tipos: consumo interno, produção ou perda.',
        'O <b>custo médio</b> é recalculado a cada entrada; o <b>último custo</b> guarda o preço da compra mais recente.',
        '<b>Inventário</b> tem saldo próprio, separado do inventário de mercadorias.',
      ]"
    />

    <!-- Resumo -->
    <v-row class="mb-3">
      <v-col v-for="c in cards" :key="c.label" cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="d-flex align-center gap-1 mb-1">
              <v-icon :icon="c.icon" :color="c.cor" size="15" />
              <span class="text-caption text-medium-emphasis">{{ c.label }}</span>
            </div>
            <div class="text-h6 font-weight-bold" :class="`text-${c.cor}`">{{ c.valor }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
      <v-row dense align="center">
        <v-col cols="12" md="5">
          <v-text-field v-model="busca" placeholder="Buscar por descrição, código ou EAN…"
            prepend-inner-icon="mdi-magnify" variant="outlined" density="compact" hide-details
            clearable @update:model-value="listar" />
        </v-col>
        <v-col cols="12" md="3">
          <v-select v-model="filtroAtivo"
            :items="[{title:'Ativos',value:true},{title:'Inativos',value:false},{title:'Todos',value:null}]"
            label="Status" variant="outlined" density="compact" hide-details @update:model-value="listar" />
        </v-col>
        <v-col cols="auto">
          <v-switch v-model="soAbaixoMinimo" color="warning" density="compact" hide-details
            label="Só abaixo do mínimo" @update:model-value="listar" />
        </v-col>
      </v-row>
    </v-card>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="materiais" :loading="carregando" density="compact"
        hover :items-per-page="50" items-per-page-text="Itens por página"
        :items-per-page-options="[{title:'25',value:25},{title:'50',value:50},{title:'100',value:100},{title:'Todos',value:-1}]"
        no-data-text="Nenhum material cadastrado. Use 'Novo material' ou importe uma NF-e marcada como Material de Consumo.">
        <template #item.estoqueAtual="{ item }">
          <v-chip :color="item.abaixoDoMinimo ? 'error' : 'success'" size="small" variant="tonal">
            {{ fmtQtd(item.estoqueAtual) }} {{ item.unidadeSigla }}
          </v-chip>
        </template>
        <template #item.custoMedio="{ item }">R$ {{ fmt(item.custoMedio) }}</template>
        <template #item.ultimoCusto="{ item }">R$ {{ fmt(item.ultimoCusto) }}</template>
        <template #item.valorEmEstoque="{ item }">R$ {{ fmt(item.valorEmEstoque) }}</template>
        <template #item.ativo="{ item }">
          <v-chip :color="item.ativo ? 'success' : 'default'" size="small" variant="tonal">
            {{ item.ativo ? 'Ativo' : 'Inativo' }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-plus-circle-outline" size="x-small" variant="text" color="success"
            title="Entrada (compra manual)" @click="abrirEntrada(item)" />
          <v-btn icon="mdi-tune-variant" size="x-small" variant="text" color="teal"
            title="Ajustar estoque" @click="abrirAjuste(item)" />
          <v-btn icon="mdi-history" size="x-small" variant="text" color="grey-darken-1"
            title="Movimentações" @click="abrirMovimentacoes(item)" />
          <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" color="primary"
            title="Editar" @click="abrirEdicao(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog: cadastro -->
    <v-dialog v-model="dlg" max-width="620" persistent scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="primary">mdi-package-variant-closed</v-icon>
          {{ editando ? 'Editar material' : 'Novo material' }}
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <v-row dense>
            <v-col cols="12" sm="4">
              <v-text-field v-model="form.codigo" label="Código" variant="outlined" density="compact"
                placeholder="auto" persistent-placeholder :disabled="editando"
                hint="Em branco = gerado automático" />
            </v-col>
            <v-col cols="12" sm="8">
              <v-text-field v-model="form.descricao" label="Descrição *" variant="outlined"
                density="compact" autofocus />
            </v-col>
            <v-col cols="12" sm="6">
              <v-select v-model="form.unidadeMedidaId" :items="unidades" item-title="sigla" item-value="id"
                label="Unidade *" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-autocomplete v-model="form.fornecedorPrincipalId" :items="fornecedores"
                item-title="razaoSocial" item-value="id" label="Fornecedor"
                variant="outlined" density="compact" clearable />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model.number="form.estoqueMinimo" label="Estoque mínimo" type="number"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="form.localizacao" label="Localização"
                variant="outlined" density="compact" placeholder="Ex.: Prateleira A3" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="form.codigoBarras" label="Código de barras"
                variant="outlined" density="compact" prepend-inner-icon="mdi-barcode" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="form.observacao" label="Observação" variant="outlined" density="compact" />
            </v-col>
            <v-col v-if="editando" cols="12">
              <v-switch v-model="form.ativo" color="success" density="compact" hide-details label="Ativo" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-btn v-if="editando" variant="text" color="error" :loading="excluindo"
            @click="excluir">Excluir</v-btn>
          <v-spacer />
          <v-btn variant="text" @click="dlg = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando"
            :disabled="!form.descricao || !form.unidadeMedidaId" @click="salvar">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: entrada (compra manual) -->
    <v-dialog v-model="dlgEntrada" max-width="460">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="success">mdi-plus-circle-outline</v-icon>Entrada de material
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <div class="text-body-2 font-weight-medium mb-1">{{ alvo?.descricao }}</div>
          <div class="text-caption text-medium-emphasis mb-3">
            Estoque atual: <b>{{ fmtQtd(alvo?.estoqueAtual) }}</b> ·
            Custo médio: <b>R$ {{ fmt(alvo?.custoMedio) }}</b>
          </div>
          <v-row dense>
            <v-col cols="6">
              <v-text-field v-model.number="formEntrada.quantidade" label="Quantidade *" type="number"
                variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="formEntrada.custoUnitario" label="Custo unitário *" type="number"
                prefix="R$" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" class="mt-2">
              <v-text-field v-model="formEntrada.observacao" label="Observação / documento"
                variant="outlined" density="compact" hide-details />
            </v-col>
          </v-row>
          <v-alert v-if="formEntrada.quantidade > 0" type="info" variant="tonal" density="compact" class="mt-3">
            Total: <b>R$ {{ fmt(formEntrada.quantidade * formEntrada.custoUnitario) }}</b> ·
            Novo saldo: <b>{{ fmtQtd((alvo?.estoqueAtual ?? 0) + formEntrada.quantidade) }}</b>
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgEntrada = false">Cancelar</v-btn>
          <v-btn color="success" rounded="lg" :loading="salvando"
            :disabled="!(formEntrada.quantidade > 0)" @click="salvarEntrada">Lançar entrada</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: ajuste -->
    <v-dialog v-model="dlgAjuste" max-width="440">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="teal">mdi-tune-variant</v-icon>Ajustar estoque
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <div class="text-body-2 font-weight-medium mb-1">{{ alvo?.descricao }}</div>
          <div class="text-caption text-medium-emphasis mb-3">
            Estoque no sistema: <b>{{ fmtQtd(alvo?.estoqueAtual) }}</b>
          </div>
          <v-row dense>
            <v-col cols="6">
              <v-text-field v-model.number="formAjuste.quantidadeContada" label="Qtd. física contada *"
                type="number" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="6">
              <v-text-field :model-value="diferencaAjuste" label="Diferença" readonly
                variant="outlined" density="compact" hide-details
                :prefix="diferencaAjuste > 0 ? '+' : ''" />
            </v-col>
          </v-row>
          <v-text-field v-model="formAjuste.observacao" label="Observação" variant="outlined"
            density="compact" hide-details class="mt-2" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgAjuste = false">Cancelar</v-btn>
          <v-btn color="teal" rounded="lg" :loading="salvando" :disabled="diferencaAjuste === 0"
            @click="salvarAjuste">Aplicar ajuste</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: baixa de consumo em lote -->
    <v-dialog v-model="dlgConsumo" max-width="640" scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="teal">mdi-tray-arrow-down</v-icon>Baixar consumo
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            Informe o que foi utilizado. Só os itens com quantidade maior que zero são baixados.
          </v-alert>
          <v-select v-model="tipoConsumo" :items="tiposSaida" item-title="label" item-value="value"
            label="Tipo da saída" variant="outlined" density="compact" class="mb-2" hide-details />
          <v-text-field v-model="obsConsumo" label="Observação" variant="outlined"
            density="compact" hide-details class="mb-3" />
          <v-table density="compact">
            <thead>
              <tr><th>Material</th><th style="width:110px">Saldo</th><th style="width:130px">Consumido</th></tr>
            </thead>
            <tbody>
              <tr v-for="m in materiaisAtivos" :key="m.id">
                <td class="text-body-2">{{ m.descricao }}</td>
                <td class="text-caption">{{ fmtQtd(m.estoqueAtual) }} {{ m.unidadeSigla }}</td>
                <td>
                  <v-text-field v-model.number="consumo[m.id]" type="number" variant="outlined"
                    density="compact" hide-details style="width:110px" placeholder="0" />
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgConsumo = false">Cancelar</v-btn>
          <v-btn color="teal" rounded="lg" :loading="salvando" @click="salvarConsumo">
            Baixar ({{ itensConsumo.length }})
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: inventário -->
    <v-dialog v-model="dlgInventario" max-width="640" scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="secondary">mdi-clipboard-list-outline</v-icon>Inventário de materiais
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            Saldo próprio, separado do inventário de mercadorias. Digite a quantidade
            física contada — só os itens com diferença são ajustados.
          </v-alert>
          <v-table density="compact">
            <thead>
              <tr><th>Material</th><th style="width:110px">Sistema</th><th style="width:130px">Contado</th><th style="width:90px">Dif.</th></tr>
            </thead>
            <tbody>
              <tr v-for="m in materiaisAtivos" :key="m.id">
                <td class="text-body-2">{{ m.descricao }}</td>
                <td class="text-caption">{{ fmtQtd(m.estoqueAtual) }}</td>
                <td>
                  <v-text-field v-model.number="contagem[m.id]" type="number" variant="outlined"
                    density="compact" hide-details style="width:110px" />
                </td>
                <td>
                  <v-chip v-if="difInventario(m) !== 0" size="x-small" variant="tonal"
                    :color="difInventario(m) > 0 ? 'success' : 'error'">
                    {{ difInventario(m) > 0 ? '+' : '' }}{{ fmtQtd(difInventario(m)) }}
                  </v-chip>
                  <span v-else class="text-caption text-medium-emphasis">—</span>
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgInventario = false">Cancelar</v-btn>
          <v-btn color="secondary" rounded="lg" :loading="salvando" @click="salvarInventario">
            Aplicar inventário ({{ itensInventario.length }})
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: movimentações -->
    <v-dialog v-model="dlgMov" max-width="720" scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon>mdi-history</v-icon>Movimentações — {{ alvo?.descricao }}
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <div v-if="!movs.length" class="text-center text-medium-emphasis py-6">
            Nenhuma movimentação registrada.
          </div>
          <v-table v-else density="compact">
            <thead>
              <tr><th>Data</th><th>Tipo</th><th>Qtd.</th><th>Custo</th><th>Total</th><th>Documento</th></tr>
            </thead>
            <tbody>
              <tr v-for="m in movs" :key="m.id">
                <td class="text-caption">{{ fmtData(m.criadoEm) }}</td>
                <td>
                  <v-chip size="x-small" variant="tonal" :color="corTipo(m.tipo)">{{ labelTipo(m.tipo) }}</v-chip>
                </td>
                <td class="text-caption">{{ fmtQtd(m.quantidade) }}</td>
                <td class="text-caption">R$ {{ fmt(m.custoUnitario) }}</td>
                <td class="text-caption">R$ {{ fmt(m.valorTotal) }}</td>
                <td class="text-caption text-medium-emphasis">{{ m.documentoOrigem ?? '—' }}</td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgMov = false">Fechar</v-btn>
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
const excluindo = ref(false)
const materiais = ref<any[]>([])
const unidades = ref<any[]>([])
const fornecedores = ref<any[]>([])

const busca = ref('')
const filtroAtivo = ref<boolean | null>(true)
const soAbaixoMinimo = ref(false)

const dlg = ref(false)
const dlgEntrada = ref(false)
const dlgAjuste = ref(false)
const dlgConsumo = ref(false)
const dlgInventario = ref(false)
const dlgMov = ref(false)
const editando = ref(false)
const alvo = ref<any>(null)
const movs = ref<any[]>([])

const formPadrao = () => ({
  codigo: '', descricao: '', unidadeMedidaId: null as string | null,
  fornecedorPrincipalId: null as string | null, estoqueMinimo: 0,
  localizacao: '', observacao: '', codigoBarras: '', ativo: true,
})
const form = ref(formPadrao())
const formEntrada = ref({ quantidade: 0, custoUnitario: 0, observacao: '' })
const formAjuste = ref({ quantidadeContada: 0, observacao: '' })

const consumo = ref<Record<string, number>>({})
const contagem = ref<Record<string, number>>({})
const tipoConsumo = ref('ConsumoInterno')
const obsConsumo = ref('')

const tiposSaida = [
  { label: 'Consumo interno', value: 'ConsumoInterno' },
  { label: 'Produção', value: 'Producao' },
  { label: 'Perda / quebra', value: 'Perda' },
]

const headers = [
  { title: 'Código', key: 'codigo', width: 90 },
  { title: 'Descrição', key: 'descricao', sortable: true },
  { title: 'Fornecedor', key: 'fornecedorNome' },
  { title: 'Estoque', key: 'estoqueAtual', width: 130 },
  { title: 'Custo médio', key: 'custoMedio', width: 110 },
  { title: 'Último custo', key: 'ultimoCusto', width: 110 },
  { title: 'Valor em estoque', key: 'valorEmEstoque', width: 130 },
  { title: 'Local', key: 'localizacao', width: 110 },
  { title: 'Status', key: 'ativo', width: 90 },
  { title: '', key: 'actions', sortable: false, width: 150 },
]

const fmt = (v?: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtQtd = (v?: number) => (v ?? 0).toLocaleString('pt-BR', { maximumFractionDigits: 3 })
const fmtData = (d?: string) => d ? new Date(d).toLocaleDateString('pt-BR') : '—'

const materiaisAtivos = computed(() => materiais.value.filter(m => m.ativo))

const cards = computed(() => {
  const valor = materiais.value.reduce((s, m) => s + (m.valorEmEstoque ?? 0), 0)
  return [
    { label: 'Materiais ativos', valor: String(materiaisAtivos.value.length), cor: 'primary', icon: 'mdi-package-variant-closed' },
    { label: 'Valor em estoque', valor: 'R$ ' + fmt(valor), cor: 'success', icon: 'mdi-cash-multiple' },
    { label: 'Abaixo do mínimo', valor: String(materiais.value.filter(m => m.abaixoDoMinimo).length), cor: 'warning', icon: 'mdi-alert-outline' },
    { label: 'Sem estoque', valor: String(materiais.value.filter(m => (m.estoqueAtual ?? 0) <= 0).length), cor: 'error', icon: 'mdi-close-circle-outline' },
  ]
})

const diferencaAjuste = computed(() =>
  Number(formAjuste.value.quantidadeContada || 0) - Number(alvo.value?.estoqueAtual || 0))

const itensConsumo = computed(() =>
  Object.entries(consumo.value)
    .filter(([, q]) => Number(q) > 0)
    .map(([id, q]) => ({ materialConsumoId: id, quantidade: Number(q) })))

function difInventario(m: any) {
  const c = contagem.value[m.id]
  if (c === undefined || c === null || c === '' as any) return 0
  return Number(c) - Number(m.estoqueAtual ?? 0)
}
const itensInventario = computed(() =>
  materiaisAtivos.value
    .filter(m => difInventario(m) !== 0)
    .map(m => ({ materialConsumoId: m.id, quantidadeContada: Number(contagem.value[m.id]) })))

function corTipo(t: string) {
  return ({ Entrada: 'success', ConsumoInterno: 'teal', Producao: 'indigo',
            Perda: 'error', AjustePositivo: 'success', AjusteNegativo: 'warning' } as any)[t] ?? 'grey'
}
function labelTipo(t: string) {
  return ({ Entrada: 'Entrada', ConsumoInterno: 'Consumo', Producao: 'Produção',
            Perda: 'Perda', AjustePositivo: 'Ajuste +', AjusteNegativo: 'Ajuste −' } as any)[t] ?? t
}

async function listar() {
  carregando.value = true
  try {
    const r = await api.get('/materiais-consumo', {
      params: {
        empresaId: auth.empresaId,
        termo: busca.value || undefined,
        ativo: filtroAtivo.value,
        abaixoMinimo: soAbaixoMinimo.value || undefined,
      },
    })
    materiais.value = r.data ?? []
  } finally { carregando.value = false }
}

async function carregarCatalogo() {
  try {
    const [u, f] = await Promise.all([
      api.get('/unidades-medida', { params: { empresaId: auth.empresaId } }),
      api.get('/fornecedores', { params: { empresaId: auth.empresaId } }),
    ])
    unidades.value = u.data
    fornecedores.value = f.data
  } catch { /* silencioso */ }
}

function abrirNovo() {
  editando.value = false
  alvo.value = null
  form.value = formPadrao()
  // Unidade mais comum para materiais: UN
  form.value.unidadeMedidaId = unidades.value.find((u: any) => u.sigla === 'UN')?.id ?? unidades.value[0]?.id ?? null
  dlg.value = true
}

function abrirEdicao(item: any) {
  editando.value = true
  alvo.value = item
  form.value = {
    codigo: item.codigo, descricao: item.descricao,
    unidadeMedidaId: item.unidadeMedidaId,
    fornecedorPrincipalId: item.fornecedorPrincipalId ?? null,
    estoqueMinimo: item.estoqueMinimo ?? 0,
    localizacao: item.localizacao ?? '', observacao: item.observacao ?? '',
    codigoBarras: item.codigoBarras ?? '', ativo: item.ativo,
  }
  dlg.value = true
}

async function salvar() {
  salvando.value = true
  try {
    const body = { empresaId: auth.empresaId, ...form.value }
    if (editando.value) await api.put(`/materiais-consumo/${alvo.value.id}`, body)
    else await api.post('/materiais-consumo', body)
    notif.ok(editando.value ? 'Material atualizado!' : 'Material cadastrado!')
    dlg.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao salvar material.')
  } finally { salvando.value = false }
}

async function excluir() {
  if (!confirm(`Excluir "${form.value.descricao}"? Materiais com movimentação não podem ser excluídos.`)) return
  excluindo.value = true
  try {
    await api.delete(`/materiais-consumo/${alvo.value.id}`)
    notif.ok('Material excluído.')
    dlg.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao excluir material.')
  } finally { excluindo.value = false }
}

function abrirEntrada(item: any) {
  alvo.value = item
  formEntrada.value = { quantidade: 0, custoUnitario: item.ultimoCusto || 0, observacao: '' }
  dlgEntrada.value = true
}

async function salvarEntrada() {
  salvando.value = true
  try {
    await api.post(`/materiais-consumo/${alvo.value.id}/entrada`, {
      quantidade: formEntrada.value.quantidade,
      custoUnitario: formEntrada.value.custoUnitario,
      usuarioId: auth.usuario?.id ?? null,
      observacao: formEntrada.value.observacao || null,
    })
    notif.ok('Entrada lançada!')
    dlgEntrada.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao lançar entrada.')
  } finally { salvando.value = false }
}

function abrirAjuste(item: any) {
  alvo.value = item
  formAjuste.value = { quantidadeContada: item.estoqueAtual ?? 0, observacao: '' }
  dlgAjuste.value = true
}

async function salvarAjuste() {
  salvando.value = true
  try {
    await api.post(`/materiais-consumo/${alvo.value.id}/ajuste`, {
      quantidadeContada: formAjuste.value.quantidadeContada,
      usuarioId: auth.usuario?.id ?? null,
      observacao: formAjuste.value.observacao || null,
    })
    notif.ok(`Estoque ajustado (${diferencaAjuste.value > 0 ? '+' : ''}${diferencaAjuste.value}).`)
    dlgAjuste.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao ajustar.')
  } finally { salvando.value = false }
}

function abrirConsumo() {
  consumo.value = {}
  tipoConsumo.value = 'ConsumoInterno'
  obsConsumo.value = ''
  dlgConsumo.value = true
}

async function salvarConsumo() {
  if (!itensConsumo.value.length) { notif.aviso('Informe ao menos uma quantidade.'); return }
  salvando.value = true
  try {
    const r = await api.post('/materiais-consumo/consumo-lote', {
      empresaId: auth.empresaId,
      tipo: tipoConsumo.value,
      itens: itensConsumo.value,
      usuarioId: auth.usuario?.id ?? null,
      observacao: obsConsumo.value || null,
    })
    notif.ok(`${r.data.processados} material(is) baixado(s).`)
    dlgConsumo.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao baixar consumo.')
  } finally { salvando.value = false }
}

function abrirInventario() {
  contagem.value = {}
  dlgInventario.value = true
}

async function salvarInventario() {
  if (!itensInventario.value.length) { notif.aviso('Nenhuma diferença encontrada.'); return }
  if (!confirm(`Aplicar inventário em ${itensInventario.value.length} material(is)?`)) return
  salvando.value = true
  try {
    const r = await api.post('/materiais-consumo/inventario', {
      empresaId: auth.empresaId,
      itens: itensInventario.value,
      usuarioId: auth.usuario?.id ?? null,
    })
    notif.ok(`Inventário aplicado! ${r.data.ajustados} material(is) ajustado(s).`)
    dlgInventario.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao aplicar inventário.')
  } finally { salvando.value = false }
}

async function abrirMovimentacoes(item: any) {
  alvo.value = item
  movs.value = []
  dlgMov.value = true
  try {
    const r = await api.get(`/materiais-consumo/${item.id}/movimentacoes`)
    movs.value = r.data ?? []
  } catch { movs.value = [] }
}

onMounted(() => {
  listar().catch(() => {})
  carregarCatalogo()
})
</script>
