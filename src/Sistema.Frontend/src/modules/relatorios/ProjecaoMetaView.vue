<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Projeção de Meta</h2></v-col>
      <v-col cols="auto">
        <v-btn variant="tonal" color="primary" :loading="carregando" prepend-icon="mdi-refresh" @click="carregar">
          Atualizar
        </v-btn>
      </v-col>
    </v-row>

    <div v-if="d">
      <!-- Sem meta definida -->
      <v-alert v-if="d.meta == null" type="warning" variant="tonal" class="mb-4">
        Nenhuma <b>meta de vendas</b> definida para este mês.
        <router-link to="/relatorios/planejamento-anual" class="ml-1">Definir no Planejamento Anual</router-link>.
        Abaixo, o realizado e a projeção pelo ritmo atual.
      </v-alert>

      <!-- Progresso da meta -->
      <v-card v-if="d.meta != null" rounded="xl" elevation="1" class="pa-4 mb-4">
        <div class="d-flex align-center flex-wrap mb-1">
          <span class="text-body-1 font-weight-bold">Realizado x Meta do mês</span>
          <v-spacer />
          <v-chip size="small" label :color="d.noRitmoBatiMeta ? 'success' : 'error'" variant="tonal">
            <v-icon start size="14">{{ d.noRitmoBatiMeta ? 'mdi-check-bold' : 'mdi-alert' }}</v-icon>
            {{ d.noRitmoBatiMeta ? 'No ritmo, bate a meta' : 'No ritmo, NÃO bate a meta' }}
          </v-chip>
        </div>
        <div class="text-caption text-medium-emphasis mb-1">
          R$ {{ fmt(d.realizado) }} de R$ {{ fmt(d.meta) }} ({{ d.pctAtingido?.toFixed(1) }}%)
        </div>
        <v-progress-linear :model-value="Math.min(100, d.pctAtingido || 0)" height="18" rounded
          :color="(d.pctAtingido || 0) >= 100 ? 'success' : (d.noRitmoBatiMeta ? 'primary' : 'warning')">
          <span class="text-caption font-weight-bold">{{ (d.pctAtingido || 0).toFixed(0) }}%</span>
        </v-progress-linear>
      </v-card>

      <!-- Indicadores -->
      <v-row dense>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-3">
            <div class="text-caption text-medium-emphasis">Realizado (mês)</div>
            <div class="text-h6 font-weight-bold">R$ {{ fmt(d.realizado) }}</div>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-3">
            <div class="text-caption text-medium-emphasis">Projeção (fim do mês)</div>
            <div class="text-h6 font-weight-bold">R$ {{ fmt(d.projecao) }}</div>
            <div v-if="d.pctProjetado != null" class="text-caption"
              :class="d.noRitmoBatiMeta ? 'text-success' : 'text-error'">
              {{ d.pctProjetado.toFixed(0) }}% da meta
            </div>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-3">
            <div class="text-caption text-medium-emphasis">Média por dia</div>
            <div class="text-h6 font-weight-bold">R$ {{ fmt(d.mediaDiaria) }}</div>
            <div class="text-caption text-medium-emphasis">{{ d.diasDecorridos }}/{{ d.diasNoMes }} dias</div>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-3" :color="corFalta" variant="tonal">
            <div class="text-caption text-medium-emphasis">Falta para a meta</div>
            <div class="text-h6 font-weight-bold">
              {{ d.faltaParaMeta == null ? '—' : 'R$ ' + fmt(d.faltaParaMeta) }}
            </div>
            <div v-if="d.metaDiariaRestante != null" class="text-caption">
              R$ {{ fmt(d.metaDiariaRestante) }}/dia nos {{ d.diasRestantes }} dias restantes
            </div>
          </v-card>
        </v-col>
      </v-row>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

interface Proj {
  ano: number; mes: number; meta: number | null; realizado: number
  diasNoMes: number; diasDecorridos: number; diasRestantes: number
  mediaDiaria: number; projecao: number
  pctAtingido: number | null; pctProjetado: number | null
  faltaParaMeta: number | null; metaDiariaRestante: number | null; noRitmoBatiMeta: boolean | null
}

const carregando = ref(true)
const d = ref<Proj | null>(null)

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const corFalta = computed(() => {
  if (!d.value || d.value.faltaParaMeta == null) return undefined
  return d.value.faltaParaMeta <= 0 ? 'success' : (d.value.noRitmoBatiMeta ? undefined : 'warning')
})

async function carregar() {
  if (!auth.empresaId) { carregando.value = false; return }
  carregando.value = true
  try {
    const res = await api.get<Proj>('/dashboard/projecao-meta', { params: { empresaId: auth.empresaId } })
    d.value = res.data
  } catch { d.value = null } finally { carregando.value = false }
}

onMounted(carregar)
</script>
