<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Transferências de Estoque</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-transfer" @click="dialog = true">
          Nova Transferência
        </v-btn>
      </v-col>
    </v-row>

    <!-- Histórico de transferências via movimentações -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" md="3">
          <FiltroMes @selecionar="(i, f) => { filtros.de = i; filtros.ate = f }" />
        </v-col>
        <v-col cols="12" md="3">
          <v-text-field v-model="filtros.de" label="De" type="date" variant="outlined"
            density="compact" hide-details />
        </v-col>
        <v-col cols="12" md="3">
          <v-text-field v-model="filtros.ate" label="Até" type="date" variant="outlined"
            density="compact" hide-details />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" @click="listarHistorico">Filtrar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="historico" :loading="carregando"
        density="compact" hover items-per-page="25">
        <template #item.quantidade="{ item }">{{ item.quantidade }}</template>
        <template #item.dataHora="{ item }">
          {{ new Date(item.dataHora).toLocaleString('pt-BR') }}
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog nova transferência -->
    <v-dialog v-model="dialog" max-width="560" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center">
          <v-icon icon="mdi-transfer" class="mr-2" />
          Nova Transferência
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="dialog = false" />
        </v-card-title>
        <v-card-text>
          <v-form ref="formulario">
            <!-- Busca produto -->
            <v-text-field v-model="form.buscaProduto" label="Produto *"
              variant="outlined" density="compact" @update:model-value="buscarProduto"
              :loading="buscandoProduto" class="mb-1" />
            <v-list v-if="produtosSugeridos.length" elevation="2" rounded="lg" class="mb-2">
              <v-list-item v-for="p in produtosSugeridos" :key="p.id"
                :title="p.descricao" :subtitle="`Cód: ${p.codigo} | Estoque: ${p.estoqueAtual}`"
                @click="selecionarProduto(p)" hover />
            </v-list>
            <v-alert v-if="form.produtoId" type="success" variant="tonal"
              density="compact" class="mb-3" :text="`✓ ${form.produtoNome} — Estoque: ${form.estoqueDisponivel}`" />

            <v-row dense>
              <v-col cols="12" md="6">
                <v-select v-model="form.localOrigemId" :items="locais" item-title="nome" item-value="id"
                  label="Local de Origem *" variant="outlined" density="compact"
                  :rules="[r => !!r || 'Obrigatório']" />
              </v-col>
              <v-col cols="12" md="6">
                <v-select v-model="form.localDestinoId" :items="locaisDestino" item-title="nome" item-value="id"
                  label="Local de Destino *" variant="outlined" density="compact"
                  :rules="[r => !!r || 'Obrigatório', r => r !== form.localOrigemId || 'Diferente da origem']" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model.number="form.quantidade" label="Quantidade *" type="number"
                  variant="outlined" density="compact"
                  :rules="[r => r > 0 || 'Inválido', r => r <= form.estoqueDisponivel || 'Acima do disponível']" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="form.observacao" label="Observação"
                  variant="outlined" density="compact" />
              </v-col>
            </v-row>
          </v-form>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvando" @click="transferir">
            Transferir
          </v-btn>
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

const auth = useAuthStore()
const notif = useNotifStore()

const carregando = ref(false)
const salvando = ref(false)
const buscandoProduto = ref(false)
const dialog = ref(false)

const historico = ref<any[]>([])
const locais = ref<any[]>([])
const produtosSugeridos = ref<any[]>([])

const filtros = ref({
  de: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  ate: new Date().toISOString().slice(0, 10),
})

const formPadrao = () => ({
  produtoId: null as string | null, produtoNome: '', buscaProduto: '',
  estoqueDisponivel: 0,
  localOrigemId: null as string | null, localDestinoId: null as string | null,
  quantidade: 1, observacao: '',
})
const form = ref(formPadrao())
const formulario = ref<any>(null)

const locaisDestino = computed(() =>
  locais.value.filter(l => l.id !== form.value.localOrigemId)
)

const headers = [
  { title: 'Produto', key: 'produto' },
  { title: 'Quantidade', key: 'quantidade', width: 110 },
  { title: 'Documento', key: 'documentoOrigem' },
  { title: 'Observação', key: 'observacao' },
  { title: 'Data', key: 'dataHora', width: 160 },
]

async function listarHistorico() {
  carregando.value = true
  try {
    const r = await api.get('/estoque/movimentacoes', {
      params: {
        empresaId: auth.empresaId,
        tipo: 'Transferencia',
        de: filtros.value.de,
        ate: filtros.value.ate,
      },
    })
    historico.value = r.data?.itens ?? r.data ?? []
  } finally { carregando.value = false }
}

let timer: any
function buscarProduto() {
  clearTimeout(timer)
  produtosSugeridos.value = []
  if (!form.value.buscaProduto || form.value.buscaProduto.length < 2) return
  timer = setTimeout(async () => {
    buscandoProduto.value = true
    try {
      const r = await api.get('/produtos/buscar', {
        params: { empresaId: auth.empresaId, q: form.value.buscaProduto },
      })
      produtosSugeridos.value = r.data
    } finally { buscandoProduto.value = false }
  }, 300)
}

function selecionarProduto(p: any) {
  form.value.produtoId = p.id
  form.value.produtoNome = p.descricao
  form.value.buscaProduto = p.descricao
  form.value.estoqueDisponivel = p.estoqueAtual ?? 0
  produtosSugeridos.value = []
}

async function transferir() {
  const ok = await formulario.value?.validate()
  if (!ok?.valid || !form.value.produtoId) {
    notif.aviso('Preencha todos os campos obrigatórios.')
    return
  }
  salvando.value = true
  try {
    await api.post('/transferencias', {
      empresaId: auth.empresaId,
      produtoId: form.value.produtoId,
      localOrigemId: form.value.localOrigemId,
      localDestinoId: form.value.localDestinoId,
      quantidade: form.value.quantidade,
      observacao: form.value.observacao || null,
    })
    notif.ok('Transferência realizada com sucesso!')
    dialog.value = false
    form.value = formPadrao()
    await listarHistorico()
  } catch (e: any) {
    notif.erro(e?.response?.data?.title ?? e?.response?.data ?? 'Erro ao transferir.')
  } finally { salvando.value = false }
}

onMounted(async () => {
  const [, loc] = await Promise.all([
    listarHistorico(),
    api.get('/locais-estoque', { params: { empresaId: auth.empresaId } }),
  ])
  locais.value = loc.data
})
</script>
