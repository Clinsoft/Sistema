<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Movimentações de Estoque</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirAjuste">Ajuste Manual</v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="movimentacoes"
      titulo="Como usar as Movimentações de Estoque"
      :passos="[
        'Esta tela é o <b>histórico</b> de tudo que entrou e saiu: vendas, compras, ajustes, transferências e devoluções. Filtre por período, tipo ou produto.',
        'Entradas aparecem em <b>verde (+)</b> e saídas em <b>vermelho (−)</b>. Os cards somam entradas, saídas e o saldo do período.',
        'Use <b>Ajuste Manual</b> para corrigir o estoque: escolha o produto e o <b>local</b>, informe a quantidade (<b>+</b> entrada / <b>−</b> saída) e o motivo.',
        'O ajuste <b>atualiza o saldo do produto</b> na hora e fica registrado aqui. Movimentações são um registro histórico — não podem ser editadas nem excluídas (use um novo ajuste para corrigir).',
      ]"
    />

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-4">
      <v-card-text>
        <v-row>
          <v-col cols="12" md="3">
            <FiltroMes @selecionar="(i, f) => { filtros.de = i; filtros.ate = f }" />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="filtros.de" label="De" type="date" density="compact" hide-details />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="filtros.ate" label="Até" type="date" density="compact" hide-details />
          </v-col>
          <v-col cols="12" md="3">
            <v-select v-model="filtros.tipo" :items="tiposMovimento" label="Tipo" density="compact"
              hide-details clearable />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field v-model="filtros.q" label="Produto" prepend-inner-icon="mdi-magnify"
              density="compact" hide-details clearable />
          </v-col>
        </v-row>
        <v-btn class="mt-2" color="primary" variant="tonal" @click="listar">Filtrar</v-btn>
      </v-card-text>
    </v-card>

    <!-- Totalizadores -->
    <v-row class="mb-4">
      <v-col v-for="t in totais" :key="t.label" cols="6" md="3">
        <v-card :color="t.cor" variant="tonal">
          <v-card-text class="text-center">
            <div class="text-caption">{{ t.label }}</div>
            <div class="text-h6 font-weight-bold">{{ t.valor }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-card>
      <v-data-table :headers="headers" :items="movimentacoes" :loading="carregando"
        items-per-page="25">
        <template #item.tipo="{ item }">
          <v-chip :color="corTipo(item.tipo)" size="small" label>{{ item.tipo }}</v-chip>
        </template>
        <template #item.quantidade="{ item }">
          <span :class="item.quantidade > 0 ? 'text-success' : 'text-error'">
            {{ item.quantidade > 0 ? '+' : '' }}{{ item.quantidade }}
          </span>
        </template>
        <template #item.dataHora="{ item }">
          {{ new Date(item.dataHora).toLocaleString('pt-BR') }}
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog Ajuste -->
    <v-dialog v-model="dialogAjuste" max-width="520" persistent>
      <v-card rounded="xl">
        <v-card-title>Ajuste Manual de Estoque</v-card-title>
        <v-card-text>
          <v-form ref="formAjusteRef">
            <v-autocomplete v-model="ajuste.produtoId" :items="produtos" item-title="descricao"
              item-value="id" label="Produto *" :rules="[r => !!r || 'Obrigatório']"
              @update:search="buscarProdutos" />
            <v-select v-model="ajuste.localEstoqueId" :items="locaisEstoque" item-title="nome"
              item-value="id" label="Local de estoque *" :rules="[r => !!r || 'Obrigatório']" />
            <v-text-field v-model.number="ajuste.quantidade" label="Quantidade (+/-) *" type="number"
              hint="Use valor positivo para entrada e negativo para saída"
              :rules="[r => r !== 0 || 'Não pode ser zero']" />
            <v-textarea v-model="ajuste.observacao" label="Motivo / Observação" rows="2" />
          </v-form>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="dialogAjuste = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvando" @click="salvarAjuste">Confirmar</v-btn>
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
import { useNotifStore } from '@/stores/notif'
import { useAuthStore } from '@/stores/auth'


const notif = useNotifStore()
const auth = useAuthStore()

const movimentacoes = ref<any[]>([])
const carregando = ref(false)
const dialogAjuste = ref(false)
const salvando = ref(false)
const produtos = ref<any[]>([])
const locaisEstoque = ref<any[]>([])
const ajuste = ref<any>({ produtoId: null, localEstoqueId: null, quantidade: 0, observacao: '' })
const formAjusteRef = ref()

const hoje = new Date().toISOString().split('T')[0]
const mesPassado = new Date(Date.now() - 30 * 864e5).toISOString().split('T')[0]
const filtros = ref({ de: mesPassado, ate: hoje, tipo: null, q: '' })

const tiposMovimento = ['Entrada', 'Saida', 'Ajuste', 'Transferencia', 'Devolucao', 'Inventario']

const headers = [
  { title: 'Data/Hora', key: 'dataHora' },
  { title: 'Produto', key: 'produtoNome' },
  { title: 'Tipo', key: 'tipo' },
  { title: 'Quantidade', key: 'quantidade', align: 'end' as const },
  { title: 'Custo Unit.', key: 'custoUnitario', align: 'end' as const },
  { title: 'Documento', key: 'documentoOrigem' },
  { title: 'Usuário', key: 'usuarioNome' },
]

const totais = computed(() => {
  const entradas = movimentacoes.value.filter(m => m.quantidade > 0).reduce((s, m) => s + m.quantidade, 0)
  const saidas = movimentacoes.value.filter(m => m.quantidade < 0).reduce((s, m) => s + Math.abs(m.quantidade), 0)
  return [
    { label: 'Total Entradas', valor: entradas.toFixed(2), cor: 'success' },
    { label: 'Total Saídas', valor: saidas.toFixed(2), cor: 'error' },
    { label: 'Saldo Período', valor: (entradas - saidas).toFixed(2), cor: 'info' },
    { label: 'Registros', valor: movimentacoes.value.length, cor: undefined },
  ]
})

async function listar() {
  carregando.value = true
  try {
    const { data } = await api.get('/estoque/movimentacoes', {
      params: {
        empresaId: auth.empresaId,
        de: filtros.value.de,
        ate: filtros.value.ate,
        tipo: filtros.value.tipo || undefined,
        q: filtros.value.q || undefined,
      }
    })
    movimentacoes.value = Array.isArray(data) ? data : (data.itens ?? [])
  } finally {
    carregando.value = false
  }
}

function abrirAjuste() {
  const local = locaisEstoque.value.find((l: any) => l.principal)?.id ?? locaisEstoque.value[0]?.id ?? null
  ajuste.value = { produtoId: null, localEstoqueId: local, quantidade: 0, observacao: '' }
  dialogAjuste.value = true
}

async function buscarProdutos(q: string) {
  if (!q || q.length < 2) return
  const { data } = await api.get('/produtos/buscar', { params: { empresaId: auth.empresaId, q } })
  produtos.value = Array.isArray(data) ? data : (data.itens ?? [])
}

async function carregarLocais() {
  try {
    const { data } = await api.get('/locais-estoque', { params: { empresaId: auth.empresaId } })
    locaisEstoque.value = data ?? []
  } catch { locaisEstoque.value = [] }
}

async function salvarAjuste() {
  const { valid } = await formAjusteRef.value?.validate()
  if (!valid) return
  if (ajuste.value.quantidade === 0) { notif.erro('Informe uma quantidade diferente de zero.'); return }
  salvando.value = true
  try {
    const qtd = ajuste.value.quantidade
    await api.post('/movimentacoes', {
      empresaId: auth.empresaId,
      produtoId: ajuste.value.produtoId,
      localEstoqueId: ajuste.value.localEstoqueId,
      tipo: qtd > 0 ? 'AjustePositivo' : 'AjusteNegativo',
      quantidade: Math.abs(qtd),
      custoUnitario: 0,
      usuarioId: auth.usuario?.id,
      observacao: ajuste.value.observacao || null,
    })
    notif.ok('Ajuste registrado! Estoque atualizado.')
    dialogAjuste.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao registrar ajuste.')
  } finally {
    salvando.value = false
  }
}

function corTipo(tipo: string) {
  const mapa: Record<string, string> = {
    Entrada: 'success', Saida: 'error', Ajuste: 'warning',
    Transferencia: 'info', Devolucao: 'orange', Inventario: 'purple'
  }
  return mapa[tipo] ?? 'default'
}

onMounted(() => { listar(); carregarLocais() })
</script>


