<template>
  <div>
    <div class="text-h6 font-weight-bold mb-4">Financeiro</div>
    <v-row>
      <v-col v-for="c in cards" :key="c.label" cols="12" sm="6" md="3">
        <v-card rounded="xl" elevation="1" :to="c.to" hover>
          <v-card-text class="d-flex align-center pa-4">
            <v-avatar :color="c.cor" size="52" class="mr-4">
              <v-icon :icon="c.icon" color="white" size="28" />
            </v-avatar>
            <div>
              <div class="text-h5 font-weight-bold">{{ c.valor }}</div>
              <div class="text-caption text-medium-emphasis">{{ c.label }}</div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
    <v-row class="mt-3">
      <v-col cols="12" md="5">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-0 text-body-1 font-weight-bold">Módulos</v-card-title>
          <v-list density="compact" nav class="pa-2">
            <v-list-item prepend-icon="mdi-arrow-down-circle-outline" title="Contas a Receber"
              to="/financeiro/contas-receber" color="primary" rounded="lg" />
            <v-list-item prepend-icon="mdi-arrow-up-circle-outline" title="Contas a Pagar"
              to="/financeiro/contas-pagar" color="primary" rounded="lg" />
            <v-list-item prepend-icon="mdi-chart-line" title="Fluxo de Caixa"
              to="/financeiro/fluxo-caixa" color="primary" rounded="lg" />
            <v-list-item prepend-icon="mdi-chart-bar" title="DRE — Demonstrativo de Resultados"
              to="/financeiro/dre" color="primary" rounded="lg" />
            <v-list-item prepend-icon="mdi-tools" title="Custos Fixos (Ponto de Equilíbrio)"
              to="/financeiro/custos-fixos" color="primary" rounded="lg" />
            <v-divider class="my-1" />
            <v-list-item prepend-icon="mdi-credit-card-clock-outline" title="Recebíveis de Cartão"
              to="/financeiro/recebiveis-cartao" color="indigo" rounded="lg" />
            <v-list-item prepend-icon="mdi-credit-card-settings-outline" title="Operadoras de Cartão"
              to="/financeiro/operadoras-cartao" color="grey-darken-1" rounded="lg" />
          </v-list>
        </v-card>
      </v-col>

      <v-col cols="12" md="7">
        <v-card rounded="xl" elevation="1" hover :to="'/relatorios/planejamento-anual'"
          style="border:2px solid rgb(var(--v-theme-primary));opacity:1">
          <v-card-text class="d-flex align-center pa-5 gap-4">
            <v-avatar color="blue-darken-2" size="56">
              <v-icon icon="mdi-calendar-month-outline" size="30" color="white" />
            </v-avatar>
            <div class="flex-grow-1">
              <div class="text-h6 font-weight-bold">Planejamento Anual</div>
              <div class="text-body-2 text-medium-emphasis">
                Defina metas mensais de faturamento, despesas e lucro para {{ new Date().getFullYear() }}.
                Acompanhe o progresso em tempo real no dashboard.
              </div>
            </div>
            <v-icon icon="mdi-chevron-right" color="blue-darken-2" size="28" />
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

const fmt = (v: number) => `R$ ${(v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })}`

const cards = ref([
  { label: 'A Receber (em aberto)', valor: 'R$ --', icon: 'mdi-arrow-down-circle-outline', cor: 'success', to: '/financeiro/contas-receber' },
  { label: 'A Pagar (em aberto)',   valor: 'R$ --', icon: 'mdi-arrow-up-circle-outline',   cor: 'error',   to: '/financeiro/contas-pagar' },
  { label: 'Títulos vencidos',      valor: '--',    icon: 'mdi-calendar-alert',             cor: 'warning', to: '/financeiro/contas-receber' },
  { label: 'Resultado do mês',      valor: 'R$ --', icon: 'mdi-bank-outline',               cor: 'info',    to: '/financeiro/dre' },
])

onMounted(async () => {
  const ano = new Date().getFullYear()
  const mes = new Date().getMonth() + 1
  const hoje = new Date().toISOString().slice(0, 10)
  // Janela ampla para capturar TODOS os títulos em aberto (passados e futuros)
  const inicioAmplo = '2000-01-01'
  const fimAmplo = new Date(ano + 5, 11, 31).toISOString().slice(0, 10)

  await Promise.allSettled([
    // Card 0 — A Receber (total em aberto)
    api.get('/contas-receber', { params: { empresaId: auth.empresaId, inicio: inicioAmplo, fim: fimAmplo, status: 'EmAberto' } })
      .then(r => {
        const total = (r.data as any[]).filter((l: any) => l.status === 'EmAberto').reduce((s, l) => s + (l.saldo ?? 0), 0)
        cards.value[0].valor = fmt(total)
      }),

    // Card 1 — A Pagar (total em aberto)
    api.get('/contas-pagar', { params: { empresaId: auth.empresaId, inicio: inicioAmplo, fim: fimAmplo } })
      .then(r => {
        const total = (r.data as any[]).filter((l: any) => l.status === 'EmAberto').reduce((s, l) => s + (l.saldo ?? 0), 0)
        cards.value[1].valor = fmt(total)
      }),

    // Card 2 — Vencidos (receber + pagar)
    Promise.all([
      api.get('/contas-receber', { params: { empresaId: auth.empresaId, inicio: inicioAmplo, fim: hoje, status: 'EmAberto' } }),
      api.get('/contas-pagar',   { params: { empresaId: auth.empresaId, inicio: inicioAmplo, fim: hoje } }),
    ]).then(([rec, pag]) => {
      const vencRec = (rec.data as any[]).filter((l: any) => l.status === 'EmAberto' && l.dataVencimento < hoje).length
      const vencPag = (pag.data as any[]).filter((l: any) => l.status === 'EmAberto' && l.dataVencimento < hoje).length
      cards.value[2].valor = `${vencRec + vencPag} título(s)`
    }),

    // Card 3 — Resultado operacional do mês (DRE)
    api.get('/financeiro/dre', { params: { empresaId: auth.empresaId, ano, mes } })
      .then(r => {
        if (r.data) cards.value[3].valor = fmt(r.data.resultadoOperacional ?? 0)
      }),
  ])
})
</script>
