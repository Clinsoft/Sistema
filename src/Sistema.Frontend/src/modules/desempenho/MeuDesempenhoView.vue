<template>
  <div>
    <!-- Portão: precisa aceitar o termo para acessar o painel -->
    <TermoAceiteAssinatura v-if="aceiteVerificado && !aceito" @assinado="onAssinado" />

    <div v-else-if="aceiteVerificado">
    <div class="d-flex align-center mb-4 flex-wrap ga-2">
      <div class="text-h6 font-weight-bold flex-grow-1">
        <v-icon class="mr-1" color="amber-darken-2">mdi-medal-outline</v-icon>Meu Desempenho
      </div>
      <v-select v-model.number="mes" label="Mês" :items="meses" item-title="label" item-value="value"
        variant="outlined" density="compact" hide-details style="max-width:150px" @update:model-value="carregar" />
      <v-text-field v-model.number="ano" label="Ano" type="number"
        variant="outlined" density="compact" hide-details style="max-width:104px" @change="carregar" />
    </div>

    <div v-if="d && !d.semDados">
      <!-- Prêmio projetado -->
      <v-card rounded="xl" elevation="2" class="pa-5 mb-4"
        :color="d.premio > 0 ? 'amber-lighten-4' : undefined">
        <div class="d-flex align-center justify-space-between flex-wrap ga-2">
          <div>
            <div class="text-caption font-weight-bold text-uppercase" style="letter-spacing:.08em">Prêmio projetado do mês</div>
            <div class="text-h3 font-weight-bold" :class="d.premio > 0 ? 'text-amber-darken-4' : 'text-medium-emphasis'">R$ {{ fmt(d.premio) }}</div>
          </div>
          <div class="text-right">
            <v-chip v-if="d.premio > 0" color="success" variant="flat">No caminho do prêmio 🎉</v-chip>
            <div v-else>
              <v-chip color="grey" variant="tonal">Ainda sem prêmio</v-chip>
              <div class="text-caption text-medium-emphasis mt-1" style="max-width:260px">{{ d.motivo }}</div>
            </div>
          </div>
        </div>
      </v-card>

      <!-- Portões -->
      <v-row dense class="mb-2">
        <v-col cols="12" sm="4">
          <v-card rounded="lg" class="pa-4 h-100">
            <div class="text-caption font-weight-bold text-uppercase text-medium-emphasis mb-1">Minha meta de venda</div>
            <div class="text-h6">R$ {{ fmt(d.vendaIndividual) }}</div>
            <div class="text-caption text-medium-emphasis">de R$ {{ fmt(d.metaIndividual) }}</div>
            <v-progress-linear :model-value="Math.min(100, d.percentIndividual)" height="8" rounded class="mt-2"
              :color="d.percentIndividual >= 100 ? 'success' : d.percentIndividual >= 90 ? 'amber-darken-2' : 'error'" />
            <div class="text-caption mt-1" :class="corPct(d.percentIndividual)">{{ pct(d.percentIndividual) }}% da meta</div>
          </v-card>
        </v-col>
        <v-col cols="12" sm="4">
          <v-card rounded="lg" class="pa-4 h-100">
            <div class="text-caption font-weight-bold text-uppercase text-medium-emphasis mb-1">Minha loja</div>
            <div class="text-h6">R$ {{ fmt(d.faturamentoLoja) }}</div>
            <div class="text-caption text-medium-emphasis">de R$ {{ fmt(d.metaLoja) }}</div>
            <v-progress-linear :model-value="Math.min(100, d.percentLoja)" height="8" rounded class="mt-2"
              :color="d.percentLoja >= 100 ? 'success' : d.percentLoja >= 90 ? 'amber-darken-2' : 'error'" />
            <div class="text-caption mt-1" :class="corPct(d.percentLoja)">{{ pct(d.percentLoja) }}% da meta</div>
          </v-card>
        </v-col>
        <v-col cols="12" sm="4">
          <v-card rounded="lg" class="pa-4 h-100">
            <div class="text-caption font-weight-bold text-uppercase text-medium-emphasis mb-1">Performance comercial</div>
            <div class="text-h6">{{ pct(d.performancePercent) }}<span class="text-body-2"> pts</span></div>
            <div class="text-caption text-medium-emphasis">{{ d.semanasAvaliadas }} semana(s) avaliada(s)</div>
            <v-progress-linear :model-value="d.performancePercent" height="8" rounded class="mt-2" color="amber-darken-2" />
            <div v-if="d.descontoValidade > 0" class="text-caption mt-1 text-error">−{{ d.descontoValidade }} pts: produto vencido na loja</div>
            <div v-else class="text-caption mt-1 text-medium-emphasis">multiplica o valor base</div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Como o prêmio é formado -->
      <v-card rounded="lg" class="pa-4 mb-3">
        <div class="text-subtitle-2 font-weight-bold mb-2">Como seu prêmio é calculado</div>
        <div class="d-flex align-center flex-wrap ga-2 text-body-2">
          <v-chip variant="tonal" :color="d.baseLoja > 0 ? 'success' : 'grey'">Base loja: R$ {{ fmt(d.baseLoja) }}</v-chip>
          <span>×</span>
          <v-chip variant="tonal" :color="d.fatorIndividual > 0 ? 'success' : 'grey'">Fator individual: {{ (d.fatorIndividual * 100).toFixed(0) }}%</v-chip>
          <span>×</span>
          <v-chip variant="tonal" color="amber-darken-2">Performance: {{ pct(d.performancePercent) }}%</v-chip>
          <span>=</span>
          <v-chip variant="flat" :color="d.premio > 0 ? 'success' : 'grey'"><b>R$ {{ fmt(d.premio) }}</b></v-chip>
        </div>
        <div v-if="!d.elegivel || d.temCorte" class="mt-2">
          <v-alert type="warning" variant="tonal" density="compact">{{ d.motivo }}</v-alert>
        </div>
      </v-card>

      <!-- Semanas -->
      <v-card v-if="d.avaliacoes && d.avaliacoes.length" rounded="lg" class="pa-4">
        <div class="text-subtitle-2 font-weight-bold mb-2">Avaliações da semana</div>
        <div class="d-flex flex-wrap ga-2">
          <v-chip v-for="a in d.avaliacoes" :key="a.inicioSemana" variant="tonal"
            :color="a.pontos >= 70 ? 'success' : 'amber-darken-2'">
            {{ fmtData(a.inicioSemana) }}: {{ a.pontos }} pts
          </v-chip>
        </div>
      </v-card>
    </div>

    <v-alert v-else type="info" variant="tonal">Ainda não há dados de desempenho para você neste período.</v-alert>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import TermoAceiteAssinatura from './TermoAceiteAssinatura.vue'

const auth = useAuthStore()
const mes = ref(new Date().getMonth() + 1)
const ano = ref(new Date().getFullYear())
const d = ref<any>(null)
const aceiteVerificado = ref(false)
const aceito = ref(false)
const meses = [
  { value: 1, label: 'Janeiro' }, { value: 2, label: 'Fevereiro' }, { value: 3, label: 'Março' },
  { value: 4, label: 'Abril' }, { value: 5, label: 'Maio' }, { value: 6, label: 'Junho' },
  { value: 7, label: 'Julho' }, { value: 8, label: 'Agosto' }, { value: 9, label: 'Setembro' },
  { value: 10, label: 'Outubro' }, { value: 11, label: 'Novembro' }, { value: 12, label: 'Dezembro' },
]
const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const pct = (v: number) => (v ?? 0).toFixed(1)
const corPct = (v: number) => v >= 100 ? 'text-success' : v >= 90 ? 'text-amber-darken-2' : 'text-error'
const fmtData = (s: string) => new Date(s + 'T12:00:00').toLocaleDateString('pt-BR')

async function carregar() {
  const r = await api.get('/premiacao/meu-desempenho', { params: { empresaId: auth.empresaId, ano: ano.value, mes: mes.value } }).catch(() => ({ data: { semDados: true } }))
  d.value = r.data
}
async function verificarAceite() {
  try {
    const r = await api.get('/premiacao/meu-aceite', { params: { empresaId: auth.empresaId } })
    aceito.value = !!r.data.aceito
  } catch { aceito.value = false }
  aceiteVerificado.value = true
  if (aceito.value) await carregar()
}
async function onAssinado() { aceito.value = true; await carregar() }
onMounted(verificarAceite)
</script>
