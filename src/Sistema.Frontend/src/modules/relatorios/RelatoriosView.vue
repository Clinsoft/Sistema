<template>
  <div>
    <div class="text-h6 font-weight-bold mb-4">Relatórios</div>

    <GuiaPassos
      id="relatorios"
      titulo="Como usar os Relatórios"
      :passos="[
        'Clique no relatório desejado. Os marcados como <b>PDF</b> abrem/baixam um documento pronto para impressão.',
        'Ao escolher um relatório com período, informe <b>Data início</b> e <b>Data fim</b> (ou use o seletor de mês) e confirme.',
        'Relatórios de <b>dados</b> baixam um arquivo <b>.json</b> com os registros; relatórios de <b>tela</b> (DRE, Fluxo, Contas, Validades) abrem a página correspondente para consulta e filtro.',
        'As <b>exportações de balança</b> geram o arquivo no formato do fabricante (Filizola, Toledo, Urano) com os produtos marcados como Balança.',
      ]"
    />

    <v-row>
      <v-col v-for="g in grupos" :key="g.titulo" cols="12" md="6">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold">
            <v-icon :icon="g.icon" class="mr-2" color="primary" />{{ g.titulo }}
          </v-card-title>
          <v-list density="compact" nav class="pa-2">
            <v-list-item v-for="r in g.relatorios" :key="r.label"
              :prepend-icon="iconeRel(r)"
              :title="r.label" :subtitle="tipoRel(r)"
              color="primary" rounded="lg" @click="gerarRelatorio(r)">
              <template #append>
                <v-icon v-if="r.pdf" icon="mdi-download" size="small" color="red" />
                <v-icon v-else-if="r.rota" icon="mdi-arrow-right" size="small" color="primary" />
                <v-icon v-else-if="r.blob" icon="mdi-file-download-outline" size="small" color="grey" />
                <v-icon v-else icon="mdi-code-json" size="small" color="grey" />
              </template>
            </v-list-item>
          </v-list>
        </v-card>
      </v-col>
    </v-row>

    <!-- Dialog de parâmetros -->
    <v-dialog v-model="dialogParams" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center">
          <v-icon :icon="relSel?.pdf ? 'mdi-file-pdf-box' : 'mdi-file-chart-outline'"
            :color="relSel?.pdf ? 'red' : 'primary'" class="mr-2" />
          {{ relSel?.label }}
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <template v-if="relSel?.semPeriodo">
            <v-alert type="info" variant="tonal" density="compact"
              text="Este relatório usa dados atuais (sem filtro de período)." />
          </template>
          <template v-else>
            <v-row dense>
              <v-col cols="12">
                <FiltroMes @selecionar="(i, f) => { params.inicio = i; params.fim = f }" />
              </v-col>
              <v-col cols="6">
                <v-text-field v-model="params.inicio" label="Data início" type="date"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="6">
                <v-text-field v-model="params.fim" label="Data fim" type="date"
                  variant="outlined" density="compact" />
              </v-col>
            </v-row>
          </template>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogParams = false">Cancelar</v-btn>
          <v-btn :color="relSel?.pdf ? 'red' : 'primary'" :loading="gerando" @click="executarRelatorio">
            {{ relSel?.pdf ? 'Baixar PDF' : 'Gerar' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import FiltroMes from '@/components/FiltroMes.vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const router = useRouter()
const dialogParams = ref(false)
const gerando = ref(false)
const relSel = ref<any>(null)

const params = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
})

const grupos = [
  {
    titulo: 'Vendas', icon: 'mdi-cash-register', relatorios: [
      { label: 'Planejamento Anual de Vendas', rota: '/relatorios/planejamento-anual', destaque: true },
      { label: 'Vendas realizadas (PDF)', endpoint: '/relatorios/pdf/vendas', pdf: true },
      { label: 'Curva ABC de Produtos (PDF)', endpoint: '/relatorios/pdf/curva-abc-produtos', pdf: true },
      { label: 'Curva ABC de Clientes (dados)', endpoint: '/relatorios/clientes/ranking', json: true },
      { label: 'Vendas por hora (dados)', endpoint: '/relatorios/vendas/por-hora', json: true },
      { label: 'Ticket médio diário (dados)', endpoint: '/relatorios/vendas/ticket-medio', json: true },
      { label: 'Devoluções do período', rota: '/pdv/devolucoes' },
    ],
  },
  {
    titulo: 'Estoque', icon: 'mdi-package-variant-closed', relatorios: [
      { label: 'Posição de Estoque (PDF)', endpoint: '/relatorios/pdf/posicao-estoque', pdf: true, semPeriodo: true },
      { label: 'Estoque abaixo do mínimo (PDF)', endpoint: '/relatorios/pdf/posicao-estoque', pdf: true, semPeriodo: true, extra: { apenasAbaixoMinimo: true } },
      { label: 'Movimentação de estoque (dados)', endpoint: '/movimentacoes', json: true },
      { label: 'Validades (próximos 30 dias)', rota: '/estoque/validade' },
    ],
  },
  {
    titulo: 'Financeiro', icon: 'mdi-currency-usd', relatorios: [
      { label: 'Contas a Receber (PDF)', endpoint: '/relatorios/pdf/contas-receber', pdf: true, semPeriodo: true },
      { label: 'Contas a Pagar', rota: '/financeiro/contas-pagar' },
      { label: 'DRE do período', rota: '/financeiro/dre' },
      { label: 'Fluxo de Caixa', rota: '/financeiro/fluxo-caixa' },
    ],
  },
  {
    titulo: 'Exportações', icon: 'mdi-export', relatorios: [
      { label: 'Balança Filizola', endpoint: '/balanca/exportar', extra: { modelo: 'filizola' }, semPeriodo: true, blob: true },
      { label: 'Balança Toledo MGV6', endpoint: '/balanca/exportar', extra: { modelo: 'toledo_mgv6' }, semPeriodo: true, blob: true },
      { label: 'Balança Urano', endpoint: '/balanca/exportar', extra: { modelo: 'urano' }, semPeriodo: true, blob: true },
      { label: 'Etiquetas (abre editor)', rota: '/estoque/etiquetas', semPeriodo: true },
    ],
  },
]

function iconeRel(r: any) {
  if (r.pdf) return 'mdi-file-pdf-box'
  if (r.rota) return 'mdi-open-in-app'
  if (r.blob) return 'mdi-scale-balance'
  return 'mdi-file-chart-outline'
}
function tipoRel(r: any) {
  if (r.pdf) return 'PDF'
  if (r.rota) return 'Abrir tela'
  if (r.blob) return 'Arquivo p/ balança'
  return 'Dados (.json)'
}

function baixar(blob: Blob, nome: string, novaAba: boolean) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  if (novaAba) a.target = '_blank'
  a.download = nome
  a.click()
  URL.revokeObjectURL(url)
}

function gerarRelatorio(r: any) {
  if (r.rota) { router.push(r.rota); return }
  relSel.value = r
  dialogParams.value = true
}

async function executarRelatorio() {
  if (!relSel.value) return
  gerando.value = true
  try {
    const extraParams = relSel.value.extra ?? {}
    const p: any = { empresaId: auth.empresaId, ...extraParams }
    if (!relSel.value.semPeriodo) { p.inicio = params.value.inicio; p.fim = params.value.fim }

    const useBlob = relSel.value.pdf || relSel.value.blob
    const r = await api.get(relSel.value.endpoint, {
      params: p,
      responseType: useBlob ? 'blob' : 'json',
    })

    const nomeArq = relSel.value.label.replace(/[^a-z0-9]/gi, '_')

    if (relSel.value.pdf) {
      baixar(new Blob([r.data], { type: 'application/pdf' }), nomeArq + '.pdf', true)
    } else if (relSel.value.blob) {
      baixar(new Blob([r.data]), nomeArq + '.txt', false)
    } else {
      // Relatório de dados: baixa como JSON e informa a quantidade
      const dados = Array.isArray(r.data) ? r.data : (r.data?.itens ?? r.data?.linhas ?? r.data)
      const qtd = Array.isArray(dados) ? dados.length : 1
      baixar(new Blob([JSON.stringify(dados, null, 2)], { type: 'application/json' }), nomeArq + '.json', false)
      notif.ok(`Relatório gerado: ${qtd} registro(s) — arquivo baixado.`)
    }
    dialogParams.value = false
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? e?.response?.data?.title ?? 'Erro ao gerar relatório.')
  } finally { gerando.value = false }
}
</script>
