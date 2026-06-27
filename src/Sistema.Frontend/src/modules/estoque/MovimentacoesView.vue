<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Movimentações de Estoque</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirAjuste">Ajuste Manual</v-btn>
      </v-col>
    </v-row>

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
const ajuste = ref({ produtoId: null, quantidade: 0, observacao: '' })
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
  ajuste.value = { produtoId: null, quantidade: 0, observacao: '' }
  dialogAjuste.value = true
}

async function buscarProdutos(q: string) {
  if (!q || q.length < 2) return
  const { data } = await api.get('/produtos/buscar', { params: { empresaId: auth.empresaId, q } })
  produtos.value = Array.isArray(data) ? data : []
}

async function salvarAjuste() {
  const { valid } = await formAjusteRef.value?.validate()
  if (!valid) return
  salvando.value = true
  try {
    await api.post('/estoque/ajuste', {
      empresaId: auth.empresaId,
      produtoId: ajuste.value.produtoId,
      quantidade: ajuste.value.quantidade,
      observacao: ajuste.value.observacao,
    })
    notif.ok('Ajuste registrado com sucesso!')
    dialogAjuste.value = false
    await listar()
  } catch {
    notif.erro('Erro ao registrar ajuste.')
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

onMounted(listar)
</script>


