<template>
  <div>
    <div class="text-h6 font-weight-bold mb-1">Auditoria — Atividade dos Usuários</div>
    <div class="text-caption text-medium-emphasis mb-4">
      Quem criou, alterou ou excluiu cada registro do sistema.
    </div>

    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="6" sm="2"><v-text-field v-model="f.inicio" type="date" label="Início" variant="outlined" density="compact" hide-details /></v-col>
        <v-col cols="6" sm="2"><v-text-field v-model="f.fim" type="date" label="Fim" variant="outlined" density="compact" hide-details /></v-col>
        <v-col cols="6" sm="2">
          <v-select v-model="f.usuarioId" :items="usuariosFiltro" item-title="nome" item-value="id"
            label="Usuário" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="6" sm="2">
          <v-select v-model="f.entidade" :items="entidadesFiltro" label="Tela / registro"
            variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="6" sm="2">
          <v-select v-model="f.acao" :items="['Inserir','Atualizar','Excluir']" label="Ação"
            variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="6" sm="2"><v-text-field v-model="f.termo" label="Buscar" variant="outlined" density="compact" hide-details clearable @keyup.enter="carregar" /></v-col>
      </v-row>
      <div class="d-flex mt-2">
        <v-spacer />
        <v-btn color="primary" variant="tonal" prepend-icon="mdi-magnify" :loading="carregando" @click="carregar">Filtrar</v-btn>
      </div>
    </v-card>

    <v-card rounded="xl" elevation="1">
      <v-data-table-server :headers="headers" :items="itens" :items-length="total"
        :loading="carregando" v-model:page="pagina" v-model:items-per-page="tamanho"
        :items-per-page-options="[25,50,100]" density="compact" @update:options="carregar">
        <template #item.dataHora="{ item }">{{ dataHora(item.dataHora) }}</template>
        <template #item.acao="{ item }">
          <v-chip size="x-small" :color="corAcao(item.acao)" variant="tonal">{{ item.acao }}</v-chip>
        </template>
        <template #item.entidade="{ item }">{{ nomeEntidade(item.entidade) }}</template>
        <template #item.resumo="{ item }">
          <span>{{ item.resumo || '—' }}</span>
          <span v-if="item.alteracoes" class="text-caption text-medium-emphasis d-block">campos: {{ item.alteracoes }}</span>
        </template>
        <template #no-data><div class="text-center text-medium-emphasis pa-6">Nenhum registro no período/filtro.</div></template>
      </v-data-table-server>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const carregando = ref(false)
const itens = ref<any[]>([])
const total = ref(0)
const pagina = ref(1)
const tamanho = ref(50)
const usuarios = ref<any[]>([])
const entidades = ref<string[]>([])

const hoje = new Date()
const f = ref<any>({
  inicio: new Date(hoje.getFullYear(), hoje.getMonth(), hoje.getDate() - 7).toISOString().slice(0, 10),
  fim: hoje.toISOString().slice(0, 10),
  usuarioId: null, entidade: null, acao: null, termo: '',
})

const usuariosFiltro = computed(() => usuarios.value)
const entidadesFiltro = computed(() => entidades.value.map(e => ({ title: nomeEntidade(e), value: e })))

const headers = [
  { title: 'Data/hora', key: 'dataHora', width: 150 },
  { title: 'Usuário', key: 'usuario', width: 180 },
  { title: 'Ação', key: 'acao', width: 100 },
  { title: 'Registro', key: 'entidade', width: 160 },
  { title: 'Descrição', key: 'resumo' },
]

// Nomes amigáveis das entidades técnicas mais comuns
const MAPA: Record<string, string> = {
  Lote: 'Lote / Validade', Produto: 'Produto', Cliente: 'Cliente', Fornecedor: 'Fornecedor',
  Venda: 'Venda', LancamentoFinanceiro: 'Conta (pagar/receber)', Financiamento: 'Financiamento',
  Usuario: 'Usuário/Colaborador', Categoria: 'Categoria', LocalEstoque: 'Loja/Local',
  NotaFiscal: 'Nota Fiscal', Crediario: 'Crediário', PedidoWhatsApp: 'Pedido WhatsApp',
  Promocao: 'Promoção', CustoFixo: 'Custo Fixo', ContaBancaria: 'Conta Bancária',
}
const nomeEntidade = (e: string) => MAPA[e] ?? e

const dataHora = (iso: string) => new Date(iso).toLocaleString('pt-BR')
const corAcao = (a: string) => a === 'Inserir' ? 'success' : a === 'Excluir' ? 'error' : 'warning'

async function carregar() {
  carregando.value = true
  try {
    const res = await api.get('/auditoria', {
      params: {
        empresaId: auth.empresaId, inicio: f.value.inicio, fim: f.value.fim,
        usuarioId: f.value.usuarioId || undefined, entidade: f.value.entidade || undefined,
        acao: f.value.acao || undefined, termo: f.value.termo || undefined,
        pagina: pagina.value, tamanho: tamanho.value,
      },
    })
    itens.value = res.data.itens
    total.value = res.data.total
  } finally { carregando.value = false }
}

onMounted(async () => {
  try {
    const r = await api.get('/auditoria/filtros', { params: { empresaId: auth.empresaId } })
    usuarios.value = r.data.usuarios; entidades.value = r.data.entidades
  } catch { /* ignore */ }
  await carregar()
})
</script>
