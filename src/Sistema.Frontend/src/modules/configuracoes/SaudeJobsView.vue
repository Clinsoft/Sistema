<template>
  <div>
    <v-row align="center" class="mb-2">
      <v-col><h2 class="text-h5 font-weight-bold">Saúde dos Jobs Automáticos</h2></v-col>
      <v-col cols="auto">
        <v-btn variant="tonal" color="primary" :loading="carregando" prepend-icon="mdi-refresh" @click="carregar">
          Atualizar
        </v-btn>
      </v-col>
    </v-row>

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      Tarefas que rodam sozinhas (disparos de WhatsApp, backup, alertas, folha…). Aqui você
      vê a <b>última execução</b> e se <b>falhou</b> — para nenhuma quebrar em silêncio.
    </v-alert>

    <!-- Estatísticas -->
    <v-row dense class="mb-2">
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Execuções com sucesso</div>
          <div class="text-h5 font-weight-bold text-success">{{ stats.sucesso ?? 0 }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3" :color="(stats.falha ?? 0) > 0 ? 'error' : undefined"
          :variant="(stats.falha ?? 0) > 0 ? 'tonal' : 'elevated'">
          <div class="text-caption text-medium-emphasis">Falhas</div>
          <div class="text-h5 font-weight-bold text-error">{{ stats.falha ?? 0 }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Processando agora</div>
          <div class="text-h5 font-weight-bold">{{ stats.processando ?? 0 }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" class="pa-3">
          <div class="text-caption text-medium-emphasis">Jobs recorrentes</div>
          <div class="text-h5 font-weight-bold">{{ stats.recorrentes ?? 0 }}</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Jobs recorrentes -->
    <v-card rounded="xl" elevation="1" class="mb-4">
      <v-data-table :headers="headers" :items="recorrentes" :loading="carregando"
        density="compact" hover items-per-page="25"
        no-data-text="Nenhum job recorrente encontrado.">
        <template #item.nome="{ item }">{{ nomeAmigavel(item.id) }}</template>
        <template #item.ultimaExecucao="{ item }">{{ fmtData(item.ultimaExecucao) }}</template>
        <template #item.proximaExecucao="{ item }">{{ fmtData(item.proximaExecucao) }}</template>
        <template #item.ultimoEstado="{ item }">
          <v-chip size="small" label variant="tonal" :color="corEstado(item.ultimoEstado)">
            {{ rotuloEstado(item.ultimoEstado) }}
          </v-chip>
        </template>
      </v-data-table>
    </v-card>

    <!-- Falhas recentes -->
    <v-card v-if="falhas.length" rounded="xl" elevation="1">
      <v-card-title class="text-body-1 font-weight-bold text-error d-flex align-center">
        <v-icon icon="mdi-alert-circle-outline" class="mr-2" /> Falhas recentes
      </v-card-title>
      <v-list density="compact">
        <v-list-item v-for="(f, i) in falhas" :key="i">
          <v-list-item-title class="text-body-2 font-weight-medium">{{ f.job }}</v-list-item-title>
          <v-list-item-subtitle class="text-caption">
            {{ fmtData(f.falhouEm) }} — {{ f.erro }}
          </v-list-item-subtitle>
        </v-list-item>
      </v-list>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import api from '@/composables/useApi'

const carregando = ref(true)
const stats = ref<any>({})
const recorrentes = ref<any[]>([])
const falhas = ref<any[]>([])

const headers = [
  { title: 'Job', key: 'nome' },
  { title: 'Agendamento (cron)', key: 'cron', width: 150 },
  { title: 'Última execução', key: 'ultimaExecucao', width: 170 },
  { title: 'Próxima', key: 'proximaExecucao', width: 170 },
  { title: 'Último estado', key: 'ultimoEstado', width: 130, align: 'center' as const },
]

const NOMES: Record<string, string> = {
  'estoque-alerta-minimo': 'Alerta de estoque mínimo',
  'crediario-lembrete-parcelas': 'Lembrete de crediário',
  'financeiro-alerta-vencimentos': 'Alerta de vencimentos',
  'recebivel-cartao-baixa-automatica': 'Baixa automática de cartão',
  'taxa-cartao-despesa-variavel': 'Taxa de cartão (despesa)',
  'backup-banco-dados': 'Backup do banco de dados',
  'validade-monitoramento': 'Monitoramento de validade',
  'produto-imagens-diaria': 'Busca de fotos de produtos',
  'whatsapp-disparos-automaticos': 'Disparos de WhatsApp (aniversário/promoção)',
  'limpar-vendas-abertas': 'Limpeza de vendas abertas',
  'nfce-retransmitir-pendentes': 'Retransmitir NFC-e pendentes',
  'folha-previsao-mensal': 'Previsão de folha',
  'despesas-fixas-mensais': 'Despesas fixas mensais',
}
const nomeAmigavel = (id: string) => NOMES[id] ?? id

const fmtData = (v: string | null) =>
  v ? new Date(v).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' }) : '—'

function corEstado(e: string | null) {
  if (e === 'Succeeded') return 'success'
  if (e === 'Failed') return 'error'
  if (e === 'Processing') return 'info'
  return 'grey'
}
function rotuloEstado(e: string | null) {
  return e === 'Succeeded' ? 'Sucesso'
    : e === 'Failed' ? 'Falhou'
    : e === 'Processing' ? 'Rodando'
    : e === 'Scheduled' ? 'Agendado'
    : e ?? 'Nunca rodou'
}

async function carregar() {
  carregando.value = true
  try {
    const res = await api.get('/jobs/saude')
    stats.value = res.data.estatisticas ?? {}
    recorrentes.value = res.data.recorrentes ?? []
    falhas.value = res.data.falhasRecentes ?? []
  } catch { stats.value = {}; recorrentes.value = []; falhas.value = [] }
  finally { carregando.value = false }
}

onMounted(carregar)
</script>
