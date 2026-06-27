<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Lotes e Validades</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNovo">Novo Lote</v-btn>
      </v-col>
    </v-row>

    <!-- Alertas vencimento -->
    <v-alert v-if="vencidos > 0" type="error" variant="tonal" rounded="xl" class="mb-3"
      :text="`${vencidos} lote(s) VENCIDO(S) — revise o estoque!`" />
    <v-alert v-if="proximos > 0 && vencidos === 0" type="warning" variant="tonal" rounded="xl" class="mb-3"
      :text="`${proximos} lote(s) vence(m) nos próximos 30 dias.`" />

    <!-- Abas: Todos / Vencimentos -->
    <v-tabs v-model="aba" class="mb-3" bg-color="transparent">
      <v-tab value="todos">Todos os Lotes</v-tab>
      <v-tab value="vencimentos">
        Vencimentos
        <v-badge v-if="vencidos + proximos > 0" :content="vencidos + proximos" color="error" inline class="ml-1" />
      </v-tab>
    </v-tabs>

    <!-- Filtro de produto -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" md="6">
          <v-text-field v-model="buscaProduto" label="Buscar produto (nome ou código)"
            prepend-inner-icon="mdi-magnify" variant="outlined" density="compact" hide-details
            clearable @update:model-value="listar" />
        </v-col>
        <v-col cols="12" md="3">
          <v-select v-model="filtroLocal" :items="locais" item-title="nome" item-value="id"
            label="Local de Estoque" variant="outlined" density="compact" hide-details clearable
            @update:model-value="listar" />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" @click="listar">Buscar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <!-- Tabela Todos -->
    <v-card v-if="aba === 'todos'" rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="lotes" :loading="carregando"
        density="compact" hover items-per-page="25">
        <template #item.dataValidade="{ item }">
          <v-chip v-if="item.dataValidade"
            :color="item.vencido ? 'error' : item.venceEm30 ? 'warning' : 'success'"
            size="small" variant="tonal">
            {{ fmtData(item.dataValidade) }}
          </v-chip>
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <template #item.quantidade="{ item }">{{ item.quantidade }}</template>
        <template #item.custoUnitario="{ item }">R$ {{ fmt(item.custoUnitario) }}</template>
      </v-data-table>
    </v-card>

    <!-- Tabela Vencimentos -->
    <v-card v-if="aba === 'vencimentos'" rounded="xl" elevation="1">
      <v-data-table :headers="headersVenc" :items="alertasVenc" :loading="carregandoVenc"
        density="compact" hover items-per-page="25">
        <template #item.dataValidade="{ item }">
          <v-chip :color="item.vencido ? 'error' : 'warning'" size="small" variant="tonal">
            {{ fmtData(item.dataValidade) }}
          </v-chip>
        </template>
        <template #item.status="{ item }">
          <v-chip :color="item.vencido ? 'error' : 'warning'" size="small">
            {{ item.vencido ? 'VENCIDO' : 'Próximo' }}
          </v-chip>
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog novo lote -->
    <v-dialog v-model="dialog" max-width="600" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">Novo Lote</v-card-title>
        <v-card-text>
          <v-form ref="formulario">
            <v-row dense>
              <v-col cols="12">
                <v-text-field v-model="form.buscaProduto" label="Produto (código ou nome) *"
                  variant="outlined" density="compact" @update:model-value="buscarProduto"
                  :loading="buscandoProduto" />
                <v-list v-if="produtosSugeridos.length" elevation="2" rounded="lg" class="mt-1 mb-2">
                  <v-list-item v-for="p in produtosSugeridos" :key="p.id"
                    :title="p.descricao" :subtitle="p.codigo"
                    @click="selecionarProduto(p)" hover />
                </v-list>
                <div v-if="form.produtoId" class="text-success text-body-2 mb-2">
                  ✓ {{ form.produtoNome }}
                </div>
              </v-col>
              <v-col cols="12" md="6">
                <v-select v-model="form.localEstoqueId" :items="locais" item-title="nome" item-value="id"
                  label="Local de Estoque *" variant="outlined" density="compact"
                  :rules="[r => !!r || 'Obrigatório']" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="form.numeroLote" label="Número do Lote *"
                  variant="outlined" density="compact" :rules="[r => !!r || 'Obrigatório']" />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field v-model.number="form.quantidade" label="Quantidade *" type="number"
                  variant="outlined" density="compact" :rules="[r => r > 0 || 'Inválido']" />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field v-model.number="form.custoUnitario" label="Custo Unitário"
                  type="number" variant="outlined" density="compact" prefix="R$" />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field v-model="form.dataValidade" label="Validade" type="date"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="form.dataFabricacao" label="Fabricação" type="date"
                  variant="outlined" density="compact" />
              </v-col>
            </v-row>
          </v-form>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvando" @click="salvar">Salvar Lote</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const aba = ref('todos')
const carregando = ref(false)
const carregandoVenc = ref(false)
const salvando = ref(false)
const buscandoProduto = ref(false)
const dialog = ref(false)

const lotes = ref<any[]>([])
const alertasVenc = ref<any[]>([])
const locais = ref<any[]>([])
const produtosSugeridos = ref<any[]>([])

const buscaProduto = ref('')
const filtroLocal = ref<string | null>(null)
const formulario = ref<any>(null)

const vencidos = computed(() => alertasVenc.value.filter(l => l.vencido).length)
const proximos = computed(() => alertasVenc.value.filter(l => !l.vencido).length)

const formPadrao = () => ({
  produtoId: null as string | null, produtoNome: '', buscaProduto: '',
  localEstoqueId: null as string | null,
  numeroLote: '', quantidade: 1, custoUnitario: 0,
  dataValidade: '', dataFabricacao: '',
})
const form = ref(formPadrao())

const headers = [
  { title: 'Produto', key: 'descricao', sortable: true },
  { title: 'Nº Lote', key: 'numeroLote' },
  { title: 'Qtd.', key: 'quantidade', width: 80 },
  { title: 'Custo', key: 'custoUnitario', width: 110 },
  { title: 'Validade', key: 'dataValidade', width: 130 },
]

const headersVenc = [
  { title: 'Produto', key: 'descricao' },
  { title: 'Nº Lote', key: 'numeroLote' },
  { title: 'Qtd.', key: 'quantidade', width: 80 },
  { title: 'Validade', key: 'dataValidade', width: 130 },
  { title: 'Status', key: 'status', width: 110 },
]

function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }
function fmtData(d: string) { return d ? new Date(d + 'T12:00:00').toLocaleDateString('pt-BR') : '' }

async function listar() {
  carregando.value = true
  try {
    const params: any = { empresaId: auth.empresaId }
    if (filtroLocal.value) params.localEstoqueId = filtroLocal.value
    if (buscaProduto.value) params.q = buscaProduto.value
    const r = await api.get('/lotes', { params })
    lotes.value = r.data
  } finally { carregando.value = false }
}

async function listarVencimentos() {
  carregandoVenc.value = true
  try {
    const r = await api.get('/lotes/vencimentos', { params: { empresaId: auth.empresaId, diasAlerta: 30 } })
    alertasVenc.value = r.data
  } finally { carregandoVenc.value = false }
}

let timerBusca: any = null
async function buscarProduto() {
  clearTimeout(timerBusca)
  if (!form.value.buscaProduto || form.value.buscaProduto.length < 2) {
    produtosSugeridos.value = []
    return
  }
  timerBusca = setTimeout(async () => {
    buscandoProduto.value = true
    try {
      const r = await api.get('/produtos/buscar', {
        params: { empresaId: auth.empresaId, q: form.value.buscaProduto }
      })
      produtosSugeridos.value = r.data
    } finally { buscandoProduto.value = false }
  }, 300)
}

function selecionarProduto(p: any) {
  form.value.produtoId = p.id
  form.value.produtoNome = p.descricao
  form.value.buscaProduto = p.descricao
  produtosSugeridos.value = []
}

function abrirNovo() {
  form.value = formPadrao()
  produtosSugeridos.value = []
  dialog.value = true
}

async function salvar() {
  const ok = await formulario.value?.validate()
  if (!ok?.valid || !form.value.produtoId) {
    notif.aviso('Selecione um produto válido.')
    return
  }
  salvando.value = true
  try {
    await api.post('/lotes', {
      empresaId: auth.empresaId,
      produtoId: form.value.produtoId,
      localEstoqueId: form.value.localEstoqueId,
      numeroLote: form.value.numeroLote,
      quantidade: form.value.quantidade,
      custoUnitario: form.value.custoUnitario,
      dataValidade: form.value.dataValidade || null,
      dataFabricacao: form.value.dataFabricacao || null,
    })
    notif.ok('Lote registrado!')
    dialog.value = false
    await Promise.all([listar(), listarVencimentos()])
  } catch (e: any) {
    notif.erro(e?.response?.data?.title ?? 'Erro ao salvar.')
  } finally { salvando.value = false }
}

watch(aba, (v) => { if (v === 'vencimentos') listarVencimentos() })

onMounted(async () => {
  const [, loc] = await Promise.all([
    listar(),
    api.get('/locais-estoque', { params: { empresaId: auth.empresaId } }),
  ])
  locais.value = loc.data
  await listarVencimentos()
})
</script>
