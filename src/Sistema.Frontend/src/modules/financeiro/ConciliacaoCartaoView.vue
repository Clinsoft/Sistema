<template>
  <v-container class="py-6" style="max-width:1100px">
    <div class="d-flex align-center mb-4">
      <v-icon size="26" color="indigo" class="mr-2">mdi-credit-card-check-outline</v-icon>
      <div>
        <div class="text-h6 font-weight-bold">Conciliação de Cartão</div>
        <div class="text-caption text-medium-emphasis">Importe o relatório de vendas da operadora (InfinitePay) e confira com as vendas do sistema.</div>
      </div>
    </div>

    <v-card rounded="xl" elevation="1" class="pa-4 mb-4">
      <div class="d-flex flex-wrap align-center gap-3">
        <v-file-input v-model="arquivo" label="Relatório de vendas (CSV)" accept=".csv"
          variant="outlined" density="comfortable" hide-details prepend-icon="mdi-file-delimited-outline"
          style="max-width:420px" />
        <v-btn color="indigo" :loading="carregando" :disabled="!arquivo" prepend-icon="mdi-compare-horizontal"
          class="text-none" @click="importar">Conciliar</v-btn>
      </div>
      <div class="text-caption text-medium-emphasis mt-2">
        Use o CSV de <b>Relatório de Vendas</b> da InfinitePay (não o extrato da conta). O período é detectado pelo arquivo.
      </div>
    </v-card>

    <template v-if="res">
      <v-alert :type="res.casados === res.totalOperadora && res.totalSistema === res.totalOperadora ? 'success' : 'warning'"
        variant="tonal" class="mb-4" density="comfortable">
        Período <b>{{ fmtD(res.periodo.inicio) }}</b> a <b>{{ fmtD(res.periodo.fim) }}</b> —
        operadora <b>{{ res.totalOperadora }}</b> vendas, sistema <b>{{ res.totalSistema }}</b>,
        casaram <b>{{ res.casados }}</b>. Taxa retida pela operadora: <b>R$ {{ fmt(res.taxaTotalOperadora) }}</b>.
      </v-alert>

      <v-card rounded="xl" elevation="1" class="mb-4">
        <v-table density="comfortable">
          <thead><tr>
            <th>Forma</th><th class="text-center">Operadora (qtd)</th><th class="text-right">Operadora (R$)</th>
            <th class="text-center">Sistema (qtd)</th><th class="text-right">Sistema (R$)</th><th class="text-right">Diferença</th>
          </tr></thead>
          <tbody>
            <tr v-for="r in res.resumo" :key="r.forma">
              <td class="font-weight-medium">{{ r.forma }}</td>
              <td class="text-center">{{ r.operadoraQtd }}</td>
              <td class="text-right">{{ fmt(r.operadoraBruto) }}</td>
              <td class="text-center">{{ r.sistemaQtd }}</td>
              <td class="text-right">{{ fmt(r.sistemaBruto) }}</td>
              <td class="text-right" :class="difCor(r.sistemaBruto - r.operadoraBruto)">
                {{ dif(r.sistemaBruto - r.operadoraBruto) }}
              </td>
            </tr>
          </tbody>
        </v-table>
      </v-card>

      <v-row dense>
        <v-col cols="12" md="6">
          <v-card rounded="xl" elevation="1">
            <v-card-title class="text-subtitle-1 d-flex align-center">
              <v-icon color="error" class="mr-2">mdi-alert-circle-outline</v-icon>
              Só no sistema ({{ res.soNoSistema.length }}) — {{ fmt(totalLista(res.soNoSistema)) }}
            </v-card-title>
            <v-card-subtitle>Registrado como venda, mas sem correspondente na operadora (venda negada/retida, forma trocada, ou teste).</v-card-subtitle>
            <v-table density="compact" height="280" fixed-header>
              <thead><tr><th>Data</th><th>Forma</th><th class="text-right">Valor</th></tr></thead>
              <tbody>
                <tr v-for="(t,i) in res.soNoSistema" :key="i"><td>{{ fmtD(t.data) }}</td><td>{{ t.forma }}</td><td class="text-right">{{ fmt(t.valor) }}</td></tr>
                <tr v-if="!res.soNoSistema.length"><td colspan="3" class="text-center text-medium-emphasis py-4">Tudo casou 🎉</td></tr>
              </tbody>
            </v-table>
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card rounded="xl" elevation="1">
            <v-card-title class="text-subtitle-1 d-flex align-center">
              <v-icon color="orange" class="mr-2">mdi-help-circle-outline</v-icon>
              Só na operadora ({{ res.soNaOperadora.length }}) — {{ fmt(totalLista(res.soNaOperadora)) }}
            </v-card-title>
            <v-card-subtitle>Passou na maquininha, mas não achei venda equivalente no sistema (venda não registrada).</v-card-subtitle>
            <v-table density="compact" height="280" fixed-header>
              <thead><tr><th>Data</th><th>Forma</th><th class="text-right">Valor</th><th>Cliente</th></tr></thead>
              <tbody>
                <tr v-for="(t,i) in res.soNaOperadora" :key="i"><td>{{ fmtD(t.data) }}</td><td>{{ t.forma }}</td><td class="text-right">{{ fmt(t.valor) }}</td><td class="text-truncate" style="max-width:140px">{{ t.nome }}</td></tr>
                <tr v-if="!res.soNaOperadora.length"><td colspan="4" class="text-center text-medium-emphasis py-4">Tudo casou 🎉</td></tr>
              </tbody>
            </v-table>
          </v-card>
        </v-col>
      </v-row>
    </template>
  </v-container>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const arquivo = ref<File | File[] | null>(null)
const carregando = ref(false)
const res = ref<any>(null)

async function importar() {
  const file = Array.isArray(arquivo.value) ? arquivo.value[0] : arquivo.value
  if (!file) return
  carregando.value = true
  try {
    const fd = new FormData()
    fd.append('empresaId', auth.empresaId)
    fd.append('arquivo', file)
    const r = await api.post('/financeiro/conciliacao-cartao/importar', fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    res.value = r.data
    notif.ok('Conciliação concluída.')
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Falha ao conciliar o arquivo.')
  } finally {
    carregando.value = false
  }
}

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtD = (d: string) => d ? new Date(d).toLocaleDateString('pt-BR') : '—'
const dif = (v: number) => (v > 0 ? '+' : '') + fmt(v)
const difCor = (v: number) => Math.abs(v) < 0.005 ? 'text-success' : 'text-error font-weight-bold'
const totalLista = (l: any[]) => l.reduce((s, t) => s + (t.valor ?? 0), 0)
</script>
