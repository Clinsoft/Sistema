<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Devoluções de Venda</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-history" to="/pdv/vendas">Ver Vendas</v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="devolucoes"
      titulo="Como usar as Devoluções"
      :passos="[
        'As devoluções são criadas a partir do <b>Histórico de Vendas</b>: abra uma venda finalizada e clique em <b>Registrar Devolução</b>.',
        'Aqui você <b>consulta</b> as devoluções do período: filtre por datas e clique em <b>Buscar</b>.',
        'Clique no ícone 👁 para ver o <b>detalhe</b> (itens devolvidos e se o estoque foi reposto).',
        'Para <b>estornar</b> uma devolução feita por engano, use o ícone 🗑 — o crédito é removido e a reposição de estoque é revertida automaticamente.',
      ]"
    />

    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" sm="3">
          <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f }" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.inicio" label="De" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.fim" label="Até" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" @click="carregar" :loading="carregando">Buscar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <!-- Totais -->
    <v-row v-if="devolucoes.length" class="mb-4">
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-warning">{{ devolucoes.length }}</div>
          <div class="text-caption text-medium-emphasis">Devoluções</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-error">R$ {{ fmt(totalDevolvido) }}</div>
          <div class="text-caption text-medium-emphasis">Total Devolvido</div>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="devolucoes" :loading="carregando"
        density="compact" hover items-per-page="20">
        <template #item.dataHora="{ item }">
          {{ new Date(item.dataHora).toLocaleString('pt-BR') }}
        </template>
        <template #item.clienteNome="{ item }">
          {{ item.clienteNome ?? 'Consumidor' }}
        </template>
        <template #item.totalDevolvido="{ item }">
          <span class="font-weight-medium text-warning">R$ {{ fmt(item.totalDevolvido) }}</span>
        </template>
        <template #item.acoes="{ item }">
          <v-btn icon="mdi-eye-outline" size="x-small" variant="text" color="primary"
            title="Ver detalhe" @click="abrirDetalhe(item)" />
          <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error"
            title="Estornar devolução" @click="confirmarExclusao(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Drawer detalhe -->
    <v-navigation-drawer v-model="drawerDetalhe" location="right" width="420" temporary>
      <v-toolbar flat>
        <v-toolbar-title class="text-body-1 font-weight-bold">
          Devolução — Venda {{ devSel?.numeroVenda }}
        </v-toolbar-title>
        <v-btn icon="mdi-close" @click="drawerDetalhe = false" />
      </v-toolbar>
      <v-divider />
      <div v-if="devSel" class="pa-4">
        <div class="text-caption text-medium-emphasis mb-1">
          {{ new Date(devSel.dataHora).toLocaleString('pt-BR') }}
        </div>
        <div class="mb-2">
          <span class="text-body-2 text-medium-emphasis">Cliente: </span>
          <span class="text-body-2">{{ devSel.clienteNome ?? 'Consumidor' }}</span>
        </div>
        <div class="mb-3">
          <span class="text-body-2 text-medium-emphasis">Motivo: </span>
          <span class="text-body-2">{{ devSel.motivo }}</span>
        </div>
        <v-chip size="small" :color="devSel.reporEstoque ? 'success' : 'grey'" variant="tonal" class="mb-3">
          {{ devSel.reporEstoque ? 'Estoque reposto' : 'Sem reposição de estoque' }}
        </v-chip>

        <div class="text-overline mb-1">Itens devolvidos</div>
        <v-table density="compact" class="mb-3">
          <thead>
            <tr><th>Produto</th><th class="text-right">Qtd</th><th class="text-right">Total</th></tr>
          </thead>
          <tbody>
            <tr v-for="it in (devSel.itens ?? [])" :key="it.id">
              <td class="text-body-2">{{ it.descricao }}</td>
              <td class="text-right text-body-2">{{ it.quantidade }}</td>
              <td class="text-right text-body-2">R$ {{ fmt(it.quantidade * it.valorUnitario) }}</td>
            </tr>
            <tr v-if="!(devSel.itens?.length)">
              <td colspan="3" class="text-center text-caption text-medium-emphasis py-2">Carregando itens…</td>
            </tr>
          </tbody>
        </v-table>

        <div class="d-flex justify-space-between">
          <span class="text-body-1 font-weight-bold">Total devolvido</span>
          <span class="text-h6 font-weight-bold text-warning">R$ {{ fmt(devSel.totalDevolvido) }}</span>
        </div>
      </div>
    </v-navigation-drawer>

    <!-- Dialog confirmar exclusão -->
    <v-dialog v-model="dialogExcluir" max-width="420">
      <v-card rounded="xl" class="pa-2">
        <v-card-title class="text-body-1 font-weight-bold d-flex align-center gap-2">
          <v-icon color="error">mdi-alert-circle-outline</v-icon> Estornar devolução?
        </v-card-title>
        <v-card-text class="text-body-2">
          A devolução da venda <strong>{{ devExcluir?.numeroVenda }}</strong>
          (R$ {{ fmt(devExcluir?.totalDevolvido ?? 0) }}) será removida.
          <template v-if="devExcluir?.reporEstoque">
            A reposição de estoque feita por ela será <strong>revertida</strong> automaticamente.
          </template>
          Esta ação não pode ser desfeita.
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogExcluir = false">Cancelar</v-btn>
          <v-btn color="error" :loading="excluindo" @click="excluir">Estornar</v-btn>
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

const auth = useAuthStore()
const notif = useNotifStore()
const carregando = ref(false)
const devolucoes = ref<any[]>([])
const drawerDetalhe = ref(false)
const devSel = ref<any>(null)
const dialogExcluir = ref(false)
const devExcluir = ref<any>(null)
const excluindo = ref(false)

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
})

const headers = [
  { title: 'Venda Nº', key: 'numeroVenda', width: 110 },
  { title: 'Data', key: 'dataHora', width: 160 },
  { title: 'Cliente', key: 'clienteNome' },
  { title: 'Motivo', key: 'motivo' },
  { title: 'Total Devolvido', key: 'totalDevolvido', width: 150 },
  { title: '', key: 'acoes', sortable: false, width: 80 },
]

const totalDevolvido = computed(() =>
  devolucoes.value.reduce((s, d) => s + (d.totalDevolvido ?? 0), 0)
)

function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/devolucoes', {
      params: { empresaId: auth.empresaId, ...filtros.value },
    })
    devolucoes.value = r.data ?? []
  } finally { carregando.value = false }
}

async function abrirDetalhe(dev: any) {
  devSel.value = dev
  drawerDetalhe.value = true
  try {
    const r = await api.get(`/devolucoes/${dev.id}`)
    devSel.value = { ...devSel.value, ...r.data }
  } catch { /* usa dados da listagem */ }
}

function confirmarExclusao(dev: any) {
  devExcluir.value = dev
  dialogExcluir.value = true
}

async function excluir() {
  if (!devExcluir.value) return
  excluindo.value = true
  try {
    await api.delete(`/devolucoes/${devExcluir.value.id}`)
    notif.ok('Devolução estornada com sucesso!')
    dialogExcluir.value = false
    drawerDetalhe.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao estornar devolução.')
  } finally { excluindo.value = false }
}

onMounted(carregar)
</script>
