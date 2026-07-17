<template>
  <div>
    <div class="d-flex align-center mb-4 flex-wrap gap-2">
      <div class="text-h6 font-weight-bold flex-grow-1">Ativo Imobilizado</div>
      <v-btn color="primary" rounded="lg" prepend-icon="mdi-plus" @click="abrirNovo">Novo bem</v-btn>
    </div>

    <GuiaPassos
      id="ativos-imobilizados"
      titulo="Como usar o Ativo Imobilizado"
      :passos="[
        'Aqui ficam os <b>bens da empresa</b>: balança, PDV, impressora, computador, móveis, veículos. Não são vendidos nem consumidos.',
        'Entradas: pela <b>NF-e</b> (marque a nota como <b>Ativo Imobilizado</b> na escrituração) ou cadastrando aqui com <b>Novo bem</b>.',
        'Informe a <b>vida útil em meses</b> para o sistema calcular a <b>depreciação linear</b> — equipamento costuma ser 60 meses (5 anos), móveis 120, veículos 60.',
        'O <b>valor contábil</b> é o valor de aquisição menos a depreciação acumulada até hoje.',
        'Vendeu, quebrou ou descartou? Use <b>Baixar</b> — o bem sai do total mas o histórico fica.',
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
          <v-text-field v-model="busca" placeholder="Buscar por descrição, código ou nº de série…"
            prepend-inner-icon="mdi-magnify" variant="outlined" density="compact" hide-details
            clearable @update:model-value="listar" />
        </v-col>
        <v-col cols="12" md="3">
          <v-select v-model="filtroCategoria" :items="[{title:'Todas',value:null}, ...categorias]"
            label="Categoria" variant="outlined" density="compact" hide-details
            @update:model-value="listar" />
        </v-col>
        <v-col cols="12" md="3">
          <v-select v-model="filtroAtivo"
            :items="[{title:'Em uso',value:true},{title:'Baixados',value:false},{title:'Todos',value:null}]"
            label="Situação" variant="outlined" density="compact" hide-details
            @update:model-value="listar" />
        </v-col>
      </v-row>
    </v-card>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="bens" :loading="carregando" density="compact"
        hover :items-per-page="50" items-per-page-text="Itens por página"
        no-data-text="Nenhum bem cadastrado. Use 'Novo bem' ou importe uma NF-e marcada como Ativo Imobilizado.">
        <template #item.categoria="{ item }">
          <v-chip size="x-small" variant="tonal" :color="corCategoria(item.categoria)" label>
            <v-icon start size="11" :icon="iconCategoria(item.categoria)" />{{ labelCategoria(item.categoria) }}
          </v-chip>
        </template>
        <template #item.valorAquisicao="{ item }">R$ {{ fmt(item.valorAquisicao) }}</template>
        <template #item.depreciacaoAcumulada="{ item }">
          <span :class="item.depreciacaoAcumulada > 0 ? 'text-error' : 'text-medium-emphasis'">
            R$ {{ fmt(item.depreciacaoAcumulada) }}
          </span>
        </template>
        <template #item.valorContabil="{ item }">
          <span class="font-weight-bold">R$ {{ fmt(item.valorContabil) }}</span>
        </template>
        <template #item.dataAquisicao="{ item }">{{ fmtData(item.dataAquisicao) }}</template>
        <template #item.vidaUtilMeses="{ item }">
          <span v-if="!item.vidaUtilMeses" class="text-medium-emphasis">não deprecia</span>
          <span v-else class="text-caption">
            {{ item.mesesDepreciados }}/{{ item.vidaUtilMeses }} meses
          </span>
        </template>
        <template #item.ativo="{ item }">
          <v-chip :color="item.ativo ? 'success' : 'default'" size="small" variant="tonal">
            {{ item.ativo ? 'Em uso' : 'Baixado' }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" color="primary"
            title="Editar" @click="abrirEdicao(item)" />
          <v-btn v-if="item.ativo" icon="mdi-archive-arrow-down-outline" size="x-small"
            variant="text" color="error" title="Baixar bem" @click="abrirBaixa(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog: cadastro -->
    <v-dialog v-model="dlg" max-width="660" persistent scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="primary">mdi-desktop-classic</v-icon>
          {{ editando ? 'Editar bem' : 'Novo bem' }}
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
            <v-col cols="12" sm="4">
              <v-select v-model="form.categoria" :items="categorias" label="Categoria *"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="8">
              <v-autocomplete v-model="form.fornecedorPrincipalId" :items="fornecedores"
                item-title="razaoSocial" item-value="id" label="Fornecedor"
                variant="outlined" density="compact" clearable />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model.number="form.valorAquisicao" label="Valor de aquisição *"
                type="number" prefix="R$" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="form.dataAquisicao" label="Data de aquisição *" type="date"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model.number="form.quantidade" label="Quantidade" type="number"
                variant="outlined" density="compact" />
            </v-col>

            <v-col cols="12"><v-divider class="my-1" /></v-col>
            <v-col cols="12" sm="4">
              <v-select v-model.number="form.vidaUtilMeses" :items="vidasUteis"
                label="Vida útil (depreciação)" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model.number="form.valorResidual" label="Valor residual"
                type="number" prefix="R$" variant="outlined" density="compact"
                hint="Quanto vale ao fim da vida útil" persistent-hint />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field :model-value="'R$ ' + fmt(depreciacaoMensalForm)" label="Depreciação mensal"
                variant="outlined" density="compact" readonly class="font-weight-bold" />
            </v-col>

            <v-col cols="12"><v-divider class="my-1" /></v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="form.numeroSerie" label="Nº de série"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="form.localizacao" label="Localização"
                variant="outlined" density="compact" placeholder="Ex.: Loja / Caixa 1" />
            </v-col>
            <v-col cols="12" sm="4" v-if="editando">
              <v-switch v-model="form.ativo" color="success" density="compact" hide-details label="Em uso" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="form.observacao" label="Observação"
                variant="outlined" density="compact" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlg = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando"
            :disabled="!form.descricao || !(form.valorAquisicao > 0)" @click="salvar">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: baixa -->
    <v-dialog v-model="dlgBaixa" max-width="440">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="error">mdi-archive-arrow-down-outline</v-icon>Baixar bem
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <div class="text-body-2 font-weight-medium mb-1">{{ alvo?.descricao }}</div>
          <div class="text-caption text-medium-emphasis mb-3">
            Valor contábil hoje: <b>R$ {{ fmt(alvo?.valorContabil) }}</b>
          </div>
          <v-text-field v-model="formBaixa.data" label="Data da baixa" type="date"
            variant="outlined" density="compact" class="mb-2" hide-details />
          <v-select v-model="formBaixa.motivo" :items="motivosBaixa" label="Motivo *"
            variant="outlined" density="compact" hide-details />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgBaixa = false">Cancelar</v-btn>
          <v-btn color="error" rounded="lg" :loading="salvando" :disabled="!formBaixa.motivo"
            @click="salvarBaixa">Baixar</v-btn>
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
const bens = ref<any[]>([])
const resumo = ref<any>(null)
const fornecedores = ref<any[]>([])

const busca = ref('')
const filtroCategoria = ref<string | null>(null)
const filtroAtivo = ref<boolean | null>(true)

const dlg = ref(false)
const dlgBaixa = ref(false)
const editando = ref(false)
const alvo = ref<any>(null)

const categorias = [
  { title: 'Equipamento', value: 'Equipamento' },
  { title: 'Móvel / utensílio', value: 'Movel' },
  { title: 'Veículo', value: 'Veiculo' },
  { title: 'Imóvel', value: 'Imovel' },
  { title: 'Software', value: 'Software' },
  { title: 'Outro', value: 'Outro' },
]

// Prazos usuais de depreciação (Receita Federal)
const vidasUteis = [
  { title: 'Não deprecia', value: 0 },
  { title: '5 anos (60 meses) — equipamentos, veículos', value: 60 },
  { title: '10 anos (120 meses) — móveis, instalações', value: 120 },
  { title: '25 anos (300 meses) — imóveis', value: 300 },
]

const motivosBaixa = ['Venda', 'Descarte / sucata', 'Quebra', 'Perda / roubo', 'Devolução', 'Outro']

const formPadrao = () => ({
  codigo: '', descricao: '', categoria: 'Equipamento',
  fornecedorPrincipalId: null as string | null,
  valorAquisicao: 0, dataAquisicao: new Date().toISOString().slice(0, 10),
  quantidade: 1, vidaUtilMeses: 60, valorResidual: 0,
  numeroSerie: '', localizacao: '', observacao: '', ativo: true,
})
const form = ref(formPadrao())
const formBaixa = ref({ data: new Date().toISOString().slice(0, 10), motivo: '' })

const headers = [
  { title: 'Código', key: 'codigo', width: 90 },
  { title: 'Bem', key: 'descricao', sortable: true },
  { title: 'Categoria', key: 'categoria', width: 140 },
  { title: 'Aquisição', key: 'dataAquisicao', width: 110 },
  { title: 'Valor', key: 'valorAquisicao', width: 110 },
  { title: 'Depreciação', key: 'vidaUtilMeses', width: 130 },
  { title: 'Depr. acumulada', key: 'depreciacaoAcumulada', width: 130 },
  { title: 'Valor contábil', key: 'valorContabil', width: 120 },
  { title: 'Local', key: 'localizacao', width: 110 },
  { title: 'Situação', key: 'ativo', width: 100 },
  { title: '', key: 'actions', sortable: false, width: 90 },
]

const fmt = (v?: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtData = (d?: string) => d ? new Date(d).toLocaleDateString('pt-BR') : '—'

const corCategoria = (c: string) => ({ Equipamento: 'primary', Movel: 'teal', Veiculo: 'indigo',
  Imovel: 'brown', Software: 'purple', Outro: 'grey' } as any)[c] ?? 'grey'
const iconCategoria = (c: string) => ({ Equipamento: 'mdi-desktop-classic', Movel: 'mdi-table-furniture',
  Veiculo: 'mdi-truck-outline', Imovel: 'mdi-home-city-outline', Software: 'mdi-application-outline',
  Outro: 'mdi-tag-outline' } as any)[c] ?? 'mdi-tag-outline'
const labelCategoria = (c: string) => categorias.find(x => x.value === c)?.title ?? c

/** Depreciação linear: (valor − residual) ÷ vida útil. Espelha o cálculo do domínio. */
const depreciacaoMensalForm = computed(() => {
  const f = form.value
  if (!f.vidaUtilMeses) return 0
  return Math.round(((f.valorAquisicao - f.valorResidual) / f.vidaUtilMeses) * 100) / 100
})

const cards = computed(() => {
  const r = resumo.value
  if (!r) return []
  return [
    { label: 'Bens em uso', valor: String(r.total ?? 0), cor: 'primary', icon: 'mdi-desktop-classic' },
    { label: 'Valor de aquisição', valor: 'R$ ' + fmt(r.valorAquisicao), cor: 'info', icon: 'mdi-cart-outline' },
    { label: 'Depreciação acumulada', valor: 'R$ ' + fmt(r.depreciacaoAcumulada), cor: 'error', icon: 'mdi-trending-down' },
    { label: 'Valor contábil', valor: 'R$ ' + fmt(r.valorContabil), cor: 'success', icon: 'mdi-cash-multiple' },
  ]
})

async function listar() {
  carregando.value = true
  try {
    const r = await api.get('/ativos-imobilizados', {
      params: {
        empresaId: auth.empresaId,
        termo: busca.value || undefined,
        ativo: filtroAtivo.value,
        categoria: filtroCategoria.value || undefined,
      },
    })
    bens.value = r.data.itens ?? []
    resumo.value = r.data
  } finally { carregando.value = false }
}

async function carregarFornecedores() {
  try {
    const r = await api.get('/fornecedores', { params: { empresaId: auth.empresaId } })
    fornecedores.value = r.data
  } catch { /* silencioso */ }
}

function abrirNovo() {
  editando.value = false
  alvo.value = null
  form.value = formPadrao()
  dlg.value = true
}

function abrirEdicao(item: any) {
  editando.value = true
  alvo.value = item
  form.value = {
    codigo: item.codigo, descricao: item.descricao, categoria: item.categoria,
    fornecedorPrincipalId: item.fornecedorPrincipalId ?? null,
    valorAquisicao: item.valorAquisicao,
    dataAquisicao: (item.dataAquisicao ?? '').slice(0, 10),
    quantidade: item.quantidade ?? 1,
    vidaUtilMeses: item.vidaUtilMeses ?? 0,
    valorResidual: item.valorResidual ?? 0,
    numeroSerie: item.numeroSerie ?? '', localizacao: item.localizacao ?? '',
    observacao: item.observacao ?? '', ativo: item.ativo,
  }
  dlg.value = true
}

async function salvar() {
  salvando.value = true
  try {
    const body = { empresaId: auth.empresaId, ...form.value }
    if (editando.value) await api.put(`/ativos-imobilizados/${alvo.value.id}`, body)
    else await api.post('/ativos-imobilizados', body)
    notif.ok(editando.value ? 'Bem atualizado!' : 'Bem cadastrado!')
    dlg.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao salvar bem.')
  } finally { salvando.value = false }
}

function abrirBaixa(item: any) {
  alvo.value = item
  formBaixa.value = { data: new Date().toISOString().slice(0, 10), motivo: '' }
  dlgBaixa.value = true
}

async function salvarBaixa() {
  salvando.value = true
  try {
    const r = await api.post(`/ativos-imobilizados/${alvo.value.id}/baixar`, {
      motivo: formBaixa.value.motivo,
      data: formBaixa.value.data,
    })
    notif.ok(`Bem baixado. Valor contábil na baixa: R$ ${fmt(r.data.valorContabilNaBaixa)}.`)
    dlgBaixa.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao baixar bem.')
  } finally { salvando.value = false }
}

onMounted(() => { listar().catch(() => {}); carregarFornecedores() })
</script>
