<template>
  <div>
    <div class="text-h6 font-weight-bold mb-1">
      <v-icon class="mr-1" color="primary">mdi-chart-line-variant</v-icon>Histórico de Faturamento
    </div>
    <p class="text-body-2 text-medium-emphasis mb-4">
      Evolução mensal por loja. Meses anteriores ao uso do sistema são <b>importados</b>; a partir do registro de vendas, vêm do <b>sistema</b>.
    </p>

    <div v-if="dados">
      <v-card v-for="lj in dados.lojas" :key="lj.lojaId" rounded="xl" elevation="1" class="pa-4 mb-4">
        <div class="d-flex align-center flex-wrap ga-3 mb-3">
          <div class="text-subtitle-1 font-weight-bold">{{ lj.loja }}</div>
          <v-chip size="small" variant="tonal" color="primary">Média: R$ {{ fmt(media(lj)) }}</v-chip>
          <v-chip size="small" variant="tonal" :color="cresc(lj) >= 0 ? 'success' : 'error'">
            {{ cresc(lj) >= 0 ? '↑' : '↓' }} {{ Math.abs(cresc(lj)).toFixed(0) }}% (12m)
          </v-chip>
          <v-spacer />
          <div class="text-body-2">Último: <b>R$ {{ fmt(ultimo(lj)) }}</b></div>
        </div>

        <!-- Gráfico de linha -->
        <div class="grafico" @mousemove="() => {}">
          <svg :viewBox="`0 0 ${W} ${H}`" preserveAspectRatio="none" class="svg-line">
            <polyline :points="area(lj)" :fill="`url(#g${lj.lojaId})`" stroke="none" opacity="0.15" />
            <polyline :points="linha(lj)" fill="none" stroke="rgb(var(--v-theme-primary))" stroke-width="2" vector-effect="non-scaling-stroke" />
            <circle v-for="(p,i) in pontos(lj)" :key="i" :cx="p.x" :cy="p.y" r="2.5"
              :fill="lj.meses[i].origem === 'Importado' ? '#90a4ae' : 'rgb(var(--v-theme-primary))'" vector-effect="non-scaling-stroke" />
            <defs>
              <linearGradient :id="`g${lj.lojaId}`" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="rgb(var(--v-theme-primary))" />
                <stop offset="100%" stop-color="rgb(var(--v-theme-primary))" stop-opacity="0" />
              </linearGradient>
            </defs>
          </svg>
          <div class="eixo-x">
            <span v-for="(m,i) in lj.meses" :key="i" v-show="i % Math.ceil(lj.meses.length/8) === 0">{{ rot(m.competencia) }}</span>
          </div>
        </div>

        <!-- Tabela -->
        <v-expansion-panels variant="accordion" class="mt-2">
          <v-expansion-panel title="Ver tabela mês a mês">
            <template #text>
              <v-table density="compact">
                <thead><tr><th>Competência</th><th class="text-right">Faturamento</th><th class="text-right">Variação</th><th class="text-center">Origem</th></tr></thead>
                <tbody>
                  <tr v-for="(m,i) in lj.meses" :key="m.competencia">
                    <td>{{ rot(m.competencia) }}</td>
                    <td class="text-right">R$ {{ fmt(m.faturamento) }}</td>
                    <td class="text-right" :class="varMes(lj,i) >= 0 ? 'text-success' : 'text-error'">
                      <span v-if="i > 0">{{ varMes(lj,i) >= 0 ? '+' : '' }}{{ varMes(lj,i).toFixed(1) }}%</span>
                      <span v-else class="text-medium-emphasis">—</span>
                    </td>
                    <td class="text-center">
                      <v-chip size="x-small" :color="m.origem === 'Importado' ? 'blue-grey' : 'success'" variant="tonal">{{ m.origem }}</v-chip>
                    </td>
                  </tr>
                </tbody>
              </v-table>
            </template>
          </v-expansion-panel>
        </v-expansion-panels>
      </v-card>
    </div>
    <v-alert v-else type="info" variant="tonal">Sem histórico de faturamento.</v-alert>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const dados = ref<any>(null)
const W = 600, H = 120
const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const rot = (c: string) => { const [a, m] = c.split('-'); return `${m}/${a.slice(2)}` }

const vals = (lj: any) => lj.meses.map((m: any) => m.faturamento)
const media = (lj: any) => { const v = vals(lj); return v.reduce((s: number, x: number) => s + x, 0) / v.length }
const ultimo = (lj: any) => vals(lj)[vals(lj).length - 1]
const cresc = (lj: any) => { const v = vals(lj); if (v.length < 2) return 0; const base = v.length > 12 ? v[v.length - 13] : v[0]; return base > 0 ? (v[v.length - 1] / base - 1) * 100 : 0 }
const varMes = (lj: any, i: number) => { const v = vals(lj); return i > 0 && v[i - 1] > 0 ? (v[i] / v[i - 1] - 1) * 100 : 0 }

function pontos(lj: any) {
  const v = vals(lj); const max = Math.max(...v), min = Math.min(...v)
  const rng = max - min || 1; const n = v.length
  return v.map((x: number, i: number) => ({
    x: n > 1 ? (i / (n - 1)) * W : W / 2,
    y: H - 8 - ((x - min) / rng) * (H - 16),
  }))
}
const linha = (lj: any) => pontos(lj).map((p: any) => `${p.x},${p.y}`).join(' ')
const area = (lj: any) => { const p = pontos(lj); return `${p[0].x},${H} ` + p.map((q: any) => `${q.x},${q.y}`).join(' ') + ` ${p[p.length - 1].x},${H}` }

onMounted(async () => {
  const r = await api.get('/relatorios/historico-faturamento', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: null }))
  dados.value = r.data
})
</script>

<style scoped>
.grafico { position: relative; }
.svg-line { width: 100%; height: 130px; display: block; }
.eixo-x { display: flex; justify-content: space-between; font-size: 10px; color: #94a3b8; margin-top: 2px; }
</style>
