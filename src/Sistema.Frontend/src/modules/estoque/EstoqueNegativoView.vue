<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Estoque Negativo</h2></v-col>
      <v-col cols="auto">
        <v-btn variant="tonal" color="primary" :loading="carregando" prepend-icon="mdi-refresh" @click="carregar">
          Atualizar
        </v-btn>
      </v-col>
    </v-row>

    <v-alert type="warning" variant="tonal" density="comfortable" class="mb-4">
      Estoque negativo quase sempre significa <b>venda sem a entrada escriturada</b> (o produto foi
      vendido, mas a nota de compra não foi lançada no estoque) — ou <b>produto duplicado</b>.
      <div class="text-caption mt-1">
        Para corrigir: <b>escriture a NF-e de compra</b> (Fiscal → NF-e Recebidas) ou faça um
        <b>ajuste por contagem física</b> (Estoque → Ajuste de Estoque). A coluna “Teve entrada?”
        ajuda a identificar os que nunca foram escriturados.
      </div>
    </v-alert>

    <v-row dense class="mb-2">
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Produtos negativos</div>
          <div class="text-h5 font-weight-bold text-error">{{ itens.length }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Nunca escriturados</div>
          <div class="text-h5 font-weight-bold text-error">{{ semEntrada }}</div>
        </v-card>
      </v-col>
    </v-row>

    <div class="d-flex align-center flex-wrap gap-2 mb-2">
      <v-chip-group v-model="filtro" mandatory>
        <v-chip value="todos" filter variant="outlined">Todos</v-chip>
        <v-chip value="semEntrada" filter color="error" variant="outlined">Nunca escriturados</v-chip>
        <v-chip value="granel" filter color="teal" variant="outlined">Granel (kg)</v-chip>
        <v-chip value="un" filter variant="outlined">Unidade</v-chip>
      </v-chip-group>
      <v-spacer />
      <v-text-field v-model="busca" label="Buscar produto" prepend-inner-icon="mdi-magnify"
        variant="outlined" density="compact" hide-details clearable style="max-width:280px" />
    </div>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="itensFiltrados" :loading="carregando"
        density="compact" hover items-per-page="50"
        :sort-by="[{ key: 'estoqueAtual', order: 'asc' }]"
        no-data-text="Nenhum produto com estoque negativo. 🎉">
        <template #item.estoqueAtual="{ item }">
          <span class="text-error font-weight-bold">{{ fmtQtd(item.estoqueAtual) }} {{ item.unidadeSigla }}</span>
        </template>
        <template #item.tipo="{ item }">
          <v-chip size="x-small" label variant="tonal" :color="item.pesavel ? 'teal' : 'grey'">
            {{ item.pesavel ? 'granel' : 'un' }}
          </v-chip>
        </template>
        <template #item.temEntrada="{ item }">
          <v-chip size="x-small" label variant="tonal" :color="item.temEntrada ? 'success' : 'error'">
            {{ item.temEntrada ? 'sim' : 'nunca' }}
          </v-chip>
        </template>
        <template #item.codigoBarras="{ item }">{{ item.codigoBarras || '—' }}</template>
        <template #item.acoes="{ item }">
          <v-btn icon="mdi-tune-vertical" size="x-small" color="primary" variant="text"
            title="Ajustar estoque pela contagem física" @click="abrirAjuste(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog: ajuste por contagem física -->
    <v-dialog v-model="dlgAjuste" max-width="440">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold">Ajustar estoque</v-card-title>
        <v-card-text>
          <div class="text-body-2 font-weight-medium mb-1">{{ ajuste.produtoNome }}</div>
          <div class="text-caption text-medium-emphasis mb-3">
            Estoque atual: <b class="text-error">{{ fmtQtd(ajuste.estoqueAtual) }} {{ ajuste.unidade }}</b>
          </div>
          <v-select v-model="ajuste.localEstoqueId" :items="locais" item-title="nome" item-value="id"
            label="Loja / local *" variant="outlined" density="compact" class="mb-3"
            :rules="[r => !!r || 'Obrigatório']" />
          <v-text-field v-model.number="ajuste.quantidadeContada" label="Quantidade contada (física) *"
            type="number" min="0" step="0.001" variant="outlined" density="compact"
            hint="Quanto tem de verdade na prateleira agora" persistent-hint />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgAjuste = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvando" :disabled="!ajuste.localEstoqueId"
            @click="confirmarAjuste">Aplicar ajuste</v-btn>
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

interface NegItem {
  id: string; codigo: string; descricao: string; estoqueAtual: number
  precoVenda: number; codigoBarras: string | null; unidadeSigla: string
  pesavel: boolean; temEntrada: boolean
}

const carregando = ref(true)
const itens = ref<NegItem[]>([])
const filtro = ref<'todos' | 'semEntrada' | 'granel' | 'un'>('todos')
const busca = ref('')

const headers = [
  { title: 'Código', key: 'codigo', width: 90 },
  { title: 'Produto', key: 'descricao' },
  { title: 'Estoque', key: 'estoqueAtual', align: 'end' as const },
  { title: 'Tipo', key: 'tipo', width: 90, align: 'center' as const },
  { title: 'Teve entrada?', key: 'temEntrada', width: 120, align: 'center' as const },
  { title: 'EAN', key: 'codigoBarras', width: 150 },
  { title: 'Ajustar', key: 'acoes', width: 96, align: 'center' as const, sortable: false },
]

const fmtQtd = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 3 })

const semEntrada = computed(() => itens.value.filter(i => !i.temEntrada).length)

const itensFiltrados = computed(() => {
  let r = itens.value
  if (filtro.value === 'semEntrada') r = r.filter(i => !i.temEntrada)
  else if (filtro.value === 'granel') r = r.filter(i => i.pesavel)
  else if (filtro.value === 'un') r = r.filter(i => !i.pesavel)
  const q = busca.value?.trim().toLowerCase()
  if (q) r = r.filter(i => (i.descricao ?? '').toLowerCase().includes(q) || (i.codigo ?? '').includes(q))
  return r
})

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<{ itens: NegItem[] }>('/estoque/negativos', {
      params: { empresaId: auth.empresaId },
    })
    itens.value = res.data.itens ?? []
  } catch { itens.value = [] } finally { carregando.value = false }
}

// ── Ajuste inline por contagem física ────────────────────────────────
const locais = ref<any[]>([])
const dlgAjuste = ref(false)
const salvando = ref(false)
const ajuste = ref({
  produtoId: '' as string, produtoNome: '', unidade: '',
  estoqueAtual: 0, localEstoqueId: (auth.lojaAtualId || null) as string | null,
  quantidadeContada: 0,
})

function abrirAjuste(item: NegItem) {
  ajuste.value = {
    produtoId: item.id, produtoNome: `${item.codigo} — ${item.descricao}`,
    unidade: item.unidadeSigla, estoqueAtual: item.estoqueAtual,
    localEstoqueId: auth.lojaAtualId || locais.value[0]?.id || null,
    quantidadeContada: 0,
  }
  dlgAjuste.value = true
}

async function confirmarAjuste() {
  if (!ajuste.value.localEstoqueId) return
  salvando.value = true
  try {
    const r = await api.post('/ajuste-estoque/unitario', {
      empresaId: auth.empresaId,
      produtoId: ajuste.value.produtoId,
      localEstoqueId: ajuste.value.localEstoqueId,
      quantidadeContada: ajuste.value.quantidadeContada,
      usuarioId: auth.usuario?.id,
      observacao: 'Ajuste pela tela de Estoque Negativo (contagem física)',
    })
    const d = r.data
    if (!d) notif.aviso('Sem diferença — nada a ajustar.')
    else notif.ok(`Ajuste aplicado! Diferença: ${d.diferenca > 0 ? '+' : ''}${d.diferenca}`)
    dlgAjuste.value = false
    await carregar()   // o item some da lista se deixou de ser negativo
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? e?.response?.data?.detalhe ?? 'Erro ao ajustar.')
  } finally { salvando.value = false }
}

onMounted(async () => {
  await carregar()
  try {
    const r = await api.get('/locais-estoque', { params: { empresaId: auth.empresaId } })
    locais.value = r.data ?? []
  } catch { locais.value = [] }
})
</script>
