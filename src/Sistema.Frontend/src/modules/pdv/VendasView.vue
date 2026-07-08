<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Histórico de Vendas</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-cash-register" to="/pdv">Ir ao PDV</v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="historico-vendas"
      titulo="Como usar o Histórico de Vendas"
      :passos="[
        'Escolha o <b>período</b> (mês ou datas) e, se quiser, filtre por <b>Status</b> (Finalizada, Cancelada, Em Aberto). Clique em <b>Buscar</b>.',
        'Os cards mostram o resumo: total de vendas, faturamento, ticket médio e canceladas do período.',
        'Clique em uma linha (ou no ícone 👁) para abrir o <b>detalhe</b> da venda: itens, pagamentos e totais.',
        'Em vendas <b>Finalizadas</b>, use <b>↩ Registrar Devolução</b> para devolver itens e repor o estoque automaticamente.',
      ]"
    />

    <!-- Filtros -->
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
        <v-col cols="12" sm="3">
          <v-select v-model="filtros.status" :items="statusOptions" label="Status"
            variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" @click="carregar" :loading="carregando">Buscar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <!-- Totalizadores -->
    <v-row v-if="vendas.length" class="mb-4">
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-primary">{{ vendas.length }}</div>
          <div class="text-caption text-medium-emphasis">Vendas no Período</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-success">R$ {{ fmt(totalVendas) }}</div>
          <div class="text-caption text-medium-emphasis">Total Faturado</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold">R$ {{ fmt(ticketMedio) }}</div>
          <div class="text-caption text-medium-emphasis">Ticket Médio</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-error">{{ canceladas }}</div>
          <div class="text-caption text-medium-emphasis">Canceladas</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="vendas" :loading="carregando"
        density="compact" hover items-per-page="20"
        @click:row="(_: any, row: any) => abrirDetalhe(row.item)">
        <template #item.total="{ item }">
          <span class="font-weight-medium">R$ {{ fmt(item.total) }}</span>
        </template>
        <template #item.dataHora="{ item }">
          {{ new Date(item.dataHora ?? item.criadoEm).toLocaleString('pt-BR') }}
        </template>
        <template #item.status="{ item }">
          <v-chip size="small" :color="corStatus(item.status)" variant="tonal">
            {{ item.status }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-eye-outline" size="x-small" variant="text" color="primary"
            @click.stop="abrirDetalhe(item)" />
          <v-btn v-if="item.status === 'Finalizada'" icon="mdi-keyboard-return"
            size="x-small" variant="text" color="warning" title="Registrar Devolução"
            @click.stop="abrirDevolucao(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Drawer detalhe -->
    <v-navigation-drawer v-model="drawerDetalhe" location="right" width="420" temporary>
      <v-toolbar flat>
        <v-toolbar-title class="text-body-1 font-weight-bold">
          Venda {{ vendaSel?.numero }}
        </v-toolbar-title>
        <v-btn icon="mdi-close" @click="drawerDetalhe = false" />
      </v-toolbar>
      <v-divider />

      <div v-if="vendaSel" class="pa-4">
        <div class="mb-3">
          <v-chip :color="corStatus(vendaSel.status)" size="small" class="mr-2">{{ vendaSel.status }}</v-chip>
          <span class="text-caption text-medium-emphasis">
            {{ new Date(vendaSel.dataHora ?? vendaSel.criadoEm).toLocaleString('pt-BR') }}
          </span>
        </div>

        <div class="text-overline mb-1">Itens</div>
        <v-table density="compact" class="mb-3">
          <thead>
            <tr><th>Produto</th><th class="text-right">Qtd</th><th class="text-right">Total</th></tr>
          </thead>
          <tbody>
            <tr v-for="item in (vendaSel.itens ?? [])" :key="item.id">
              <td class="text-body-2">{{ item.descricao }}</td>
              <td class="text-right text-body-2">{{ item.quantidade }}</td>
              <td class="text-right text-body-2">R$ {{ fmt(item.total) }}</td>
            </tr>
            <tr v-if="!(vendaSel.itens?.length)">
              <td colspan="3" class="text-center text-caption text-medium-emphasis py-2">
                Carregando itens…
              </td>
            </tr>
          </tbody>
        </v-table>

        <div class="text-overline mb-1">Pagamentos</div>
        <div v-for="pag in (vendaSel.pagamentos ?? [])" :key="pag.id"
          class="d-flex justify-space-between mb-1">
          <span class="text-body-2">{{ pag.forma }}</span>
          <span class="text-body-2 font-weight-medium">R$ {{ fmt(pag.valor) }}</span>
        </div>

        <v-divider class="my-3" />
        <div class="d-flex justify-space-between mb-1">
          <span class="text-body-2 text-medium-emphasis">Subtotal</span>
          <span class="text-body-2">R$ {{ fmt(vendaSel.subTotal) }}</span>
        </div>
        <div v-if="(vendaSel.totalDesconto ?? 0) > 0" class="d-flex justify-space-between mb-1">
          <span class="text-body-2 text-medium-emphasis">Desconto</span>
          <span class="text-body-2 text-error">- R$ {{ fmt(vendaSel.totalDesconto) }}</span>
        </div>
        <div class="d-flex justify-space-between">
          <span class="text-body-1 font-weight-bold">Total</span>
          <span class="text-h6 font-weight-bold text-success">R$ {{ fmt(vendaSel.total) }}</span>
        </div>
        <div v-if="(vendaSel.troco ?? 0) > 0" class="d-flex justify-space-between mt-1">
          <span class="text-body-2 text-medium-emphasis">Troco</span>
          <span class="text-body-2">R$ {{ fmt(vendaSel.troco) }}</span>
        </div>

        <div v-if="vendaSel.observacao" class="mt-3">
          <div class="text-overline mb-1">Observação</div>
          <div class="text-body-2 text-medium-emphasis">{{ vendaSel.observacao }}</div>
        </div>

        <div v-if="vendaSel.status === 'Finalizada'" class="mt-4">
          <v-btn block color="warning" variant="tonal" prepend-icon="mdi-keyboard-return"
            @click="abrirDevolucao(vendaSel)">
            Registrar Devolução
          </v-btn>
        </div>
      </div>
    </v-navigation-drawer>

    <!-- Dialog devolução -->
    <v-dialog v-model="dialogDevolucao" max-width="600" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          Devolução — Venda {{ vendaDevolucao?.numero }}
        </v-card-title>
        <v-card-text>
          <v-text-field v-model="motivoDevolucao" label="Motivo da Devolução *"
            variant="outlined" density="compact" class="mb-3"
            :rules="[r => !!r || 'Obrigatório']" />

          <div class="text-body-2 font-weight-medium mb-2">Selecione os itens a devolver:</div>
          <div v-for="item in itensParaDevolucao" :key="item.produtoId" class="mb-2">
            <div class="d-flex align-center gap-2">
              <v-checkbox v-model="item.selecionado" hide-details density="compact" class="flex-shrink-0" />
              <div class="flex-grow-1">
                <div class="text-body-2">{{ item.descricao }}</div>
                <div class="text-caption text-medium-emphasis">
                  Qtd vendida: {{ item.quantidade }} | R$ {{ fmt(item.precoUnitario) }} un.
                </div>
              </div>
              <v-text-field v-if="item.selecionado" v-model.number="item.qtdDevolver"
                type="number" variant="outlined" density="compact" hide-details
                style="width:90px" :max="item.quantidade" :min="0.01" label="Qtd" />
            </div>
          </div>

          <v-divider class="my-3" />
          <div class="d-flex justify-space-between">
            <span class="text-body-2 text-medium-emphasis">Total a devolver:</span>
            <span class="text-body-1 font-weight-bold text-warning">R$ {{ fmt(totalDevolucao) }}</span>
          </div>

          <v-checkbox v-model="reporEstoque" label="Repor estoque automaticamente"
            density="compact" class="mt-2" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogDevolucao = false">Cancelar</v-btn>
          <v-btn color="warning" :loading="salvandoDevolucao"
            :disabled="!motivoDevolucao || totalDevolucao === 0"
            @click="confirmarDevolucao">
            Confirmar Devolução
          </v-btn>
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
const vendas = ref<any[]>([])
const drawerDetalhe = ref(false)
const vendaSel = ref<any>(null)

const dialogDevolucao = ref(false)
const vendaDevolucao = ref<any>(null)
const motivoDevolucao = ref('')
const reporEstoque = ref(true)
const salvandoDevolucao = ref(false)
const itensParaDevolucao = ref<any[]>([])

const totalDevolucao = computed(() =>
  itensParaDevolucao.value
    .filter(i => i.selecionado)
    .reduce((s, i) => s + i.qtdDevolver * i.precoUnitario, 0)
)

const statusOptions = ['EmAberto', 'Finalizada', 'Cancelada']

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
  status: null as string | null,
})

const headers = [
  { title: 'Nº', key: 'numero', width: 90 },
  { title: 'Data/Hora', key: 'dataHora', sortable: true, width: 160 },
  { title: 'Cliente', key: 'clienteNome' },
  { title: 'Total', key: 'total', sortable: true, width: 120 },
  { title: 'Status', key: 'status', width: 110 },
  { title: '', key: 'actions', sortable: false, width: 50 },
]

const totalVendas = computed(() =>
  vendas.value.filter(v => v.status === 'Finalizada').reduce((s, v) => s + (v.total ?? 0), 0)
)
const ticketMedio = computed(() => {
  const fins = vendas.value.filter(v => v.status === 'Finalizada')
  return fins.length ? totalVendas.value / fins.length : 0
})
const canceladas = computed(() => vendas.value.filter(v => v.status === 'Cancelada').length)

function corStatus(s: string) {
  return s === 'Finalizada' ? 'success' : s === 'Cancelada' ? 'error' : 'warning'
}
function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/vendas', {
      params: {
        empresaId: auth.empresaId,
        inicio: filtros.value.inicio,
        fim: filtros.value.fim,
        status: filtros.value.status || undefined,
      },
    })
    vendas.value = r.data?.itens ?? r.data ?? []
  } finally { carregando.value = false }
}

async function abrirDetalhe(venda: any) {
  vendaSel.value = venda
  drawerDetalhe.value = true
  if (!venda.itens) {
    try {
      const r = await api.get(`/vendas/${venda.id}`, { params: { empresaId: auth.empresaId } })
      vendaSel.value = { ...vendaSel.value, ...r.data }
    } catch { /* usa dados da listagem */ }
  }
}

async function abrirDevolucao(venda: any) {
  vendaDevolucao.value = venda
  motivoDevolucao.value = ''
  reporEstoque.value = true
  itensParaDevolucao.value = []
  dialogDevolucao.value = true
  try {
    const r = await api.get(`/devolucoes/venda/${venda.id}/itens`, { params: { empresaId: auth.empresaId } })
    itensParaDevolucao.value = (r.data ?? []).map((i: any) => ({
      ...i, selecionado: true, qtdDevolver: i.quantidade,
    }))
  } catch { /* usa lista vazia */ }
}

async function confirmarDevolucao() {
  const itensSel = itensParaDevolucao.value
    .filter(i => i.selecionado && i.qtdDevolver > 0)
    .map(i => ({
      produtoId: i.produtoId,
      descricao: i.descricao,
      quantidade: i.qtdDevolver,
      valorUnitario: i.precoUnitario,
    }))

  if (!itensSel.length || !motivoDevolucao.value) return
  salvandoDevolucao.value = true
  try {
    await api.post('/devolucoes', {
      empresaId: auth.empresaId,
      vendaId: vendaDevolucao.value.id,
      motivo: motivoDevolucao.value,
      itens: itensSel,
      reporEstoque: reporEstoque.value,
    })
    notif.ok('Devolução registrada com sucesso!')
    dialogDevolucao.value = false
    drawerDetalhe.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.title ?? e?.response?.data ?? 'Erro ao registrar devolução.')
  } finally { salvandoDevolucao.value = false }
}

onMounted(carregar)
</script>
