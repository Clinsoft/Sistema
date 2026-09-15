<template>
  <div>
    <div class="d-flex align-center mb-4 flex-wrap ga-2">
      <div class="text-h6 font-weight-bold flex-grow-1">
        <v-icon class="mr-1" color="amber-darken-2">mdi-trophy-outline</v-icon>Premiação por Desempenho
      </div>
      <v-select v-model.number="mes" label="Mês" :items="meses" item-title="label" item-value="value"
        variant="outlined" density="compact" hide-details style="max-width:150px" @update:model-value="carregar" />
      <v-text-field v-model.number="ano" label="Ano" type="number"
        variant="outlined" density="compact" hide-details style="max-width:104px" @change="carregar" />
    </div>

    <v-tabs v-model="aba" color="amber-darken-2" class="mb-4">
      <v-tab value="apuracao">Apuração &amp; Prêmio</v-tab>
      <v-tab value="avaliacao">Avaliação semanal</v-tab>
      <v-tab value="elegibilidade">Elegibilidade &amp; Cortes</v-tab>
      <v-tab value="metas">Metas &amp; Regras</v-tab>
      <v-tab value="arquivo">Arquivo (PDF)</v-tab>
    </v-tabs>

    <!-- ══ APURAÇÃO ══ -->
    <div v-if="aba === 'apuracao'">
      <div v-if="apuracao">
        <div v-for="lj in apuracao.lojas" :key="lj.lojaId" class="mb-5">
          <div class="d-flex align-center ga-3 mb-2 flex-wrap">
            <div class="text-subtitle-1 font-weight-bold">{{ lj.loja }}</div>
            <v-chip size="small" :color="lj.percentLoja >= 100 ? 'success' : lj.percentLoja >= 90 ? 'amber-darken-2' : 'error'" variant="tonal">
              Loja {{ fmt(lj.faturamentoLoja) }} / {{ fmt(lj.metaLoja) }} · {{ pct(lj.percentLoja) }}%
            </v-chip>
            <v-tooltip v-if="lj.projecaoLoja" location="top"
              :text="'No ritmo atual, projeção do mês: R$ ' + fmt(lj.projecaoLoja.projecao) + (lj.projecaoLoja.falta > 0 ? ' · falta R$ ' + fmt(lj.projecaoLoja.falta) + ' (R$ ' + fmt(lj.projecaoLoja.porDia) + '/dia nos ' + lj.projecaoLoja.diasRestantes + ' dias restantes)' : '')">
              <template #activator="{ props }">
                <v-chip v-bind="props" size="small" variant="flat" :color="lj.projecaoLoja.vaiBater ? 'success' : 'error'">
                  <v-icon start size="14">{{ lj.projecaoLoja.vaiBater ? 'mdi-check-bold' : 'mdi-alert' }}</v-icon>
                  No ritmo: {{ lj.projecaoLoja.vaiBater ? 'bate a meta' : 'NÃO bate' }} ({{ lj.projecaoLoja.percentProjecao }}%)
                </v-chip>
              </template>
            </v-tooltip>
            <v-spacer />
            <div class="text-body-2">Total de prêmios: <b>R$ {{ fmt(lj.totalPremios) }}</b></div>
          </div>
          <v-card rounded="lg" elevation="1">
            <v-table density="comfortable">
              <thead>
                <tr>
                  <th>Colaborador</th>
                  <th class="text-right">Venda ind.</th>
                  <th class="text-right">% meta</th>
                  <th class="text-center">No ritmo</th>
                  <th class="text-right">Performance</th>
                  <th class="text-center">Status</th>
                  <th class="text-right">Prêmio</th>
                  <th class="text-right">PDF</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="c in lj.colaboradores" :key="c.colaboradorId">
                  <td class="font-weight-medium">{{ c.colaborador }}</td>
                  <td class="text-right">{{ fmt(c.vendaIndividual) }} <span class="text-caption text-medium-emphasis">/ {{ fmt(c.metaIndividual) }}</span></td>
                  <td class="text-right" :class="c.percentIndividual >= 100 ? 'text-success' : c.percentIndividual >= 90 ? 'text-amber-darken-2' : 'text-error'">{{ pct(c.percentIndividual) }}%</td>
                  <td class="text-center">
                    <v-tooltip v-if="c.projecao" location="top"
                      :text="'Projeção do mês: R$ ' + fmt(c.projecao.projecao) + (c.projecao.falta > 0 ? ' · falta R$ ' + fmt(c.projecao.falta) + ' (R$ ' + fmt(c.projecao.porDia) + '/dia)' : '')">
                      <template #activator="{ props }">
                        <v-chip v-bind="props" size="x-small" variant="tonal" :color="c.projecao.vaiBater ? 'success' : 'error'">
                          {{ c.projecao.vaiBater ? 'bate' : 'não bate' }} · {{ c.projecao.percentProjecao }}%
                        </v-chip>
                      </template>
                    </v-tooltip>
                    <span v-else class="text-caption text-medium-emphasis">—</span>
                  </td>
                  <td class="text-right">
                    {{ pct(c.performancePercent) }}% <span class="text-caption text-medium-emphasis">({{ c.semanasAvaliadas }}s)</span>
                    <v-tooltip v-if="c.descontoValidade > 0" text="Produto vencido na loja: pontos de Validade descontados de toda a equipe"><template #activator="{ props }">
                      <v-chip v-bind="props" size="x-small" color="error" variant="tonal" class="ml-1">−{{ c.descontoValidade }} validade</v-chip>
                    </template></v-tooltip>
                    <v-tooltip v-if="c.descontoAtendimentoWhatsapp > 0" text="Conversas do WhatsApp não respondidas em 6h de funcionamento: 10 pts por conversa, descontados de todos os atendentes da loja"><template #activator="{ props }">
                      <v-chip v-bind="props" size="x-small" color="error" variant="tonal" class="ml-1">−{{ c.descontoAtendimentoWhatsapp }} atend.</v-chip>
                    </template></v-tooltip>
                  </td>
                  <td class="text-center">
                    <v-chip v-if="c.premio > 0" size="x-small" color="success" variant="tonal">Elegível</v-chip>
                    <v-tooltip v-else :text="c.motivo || 'Sem prêmio'"><template #activator="{ props }">
                      <v-chip v-bind="props" size="x-small" color="grey" variant="tonal">Zerado</v-chip>
                    </template></v-tooltip>
                  </td>
                  <td class="text-right font-weight-bold" :class="c.premio > 0 ? 'text-success' : 'text-medium-emphasis'">R$ {{ fmt(c.premio) }}</td>
                  <td class="text-right">
                    <v-btn size="x-small" color="red-darken-1" variant="tonal" icon="mdi-file-pdf-box"
                      :loading="baixandoPdf === c.colaboradorId" title="Demonstrativo em PDF"
                      @click="baixarDemonstrativo(c.colaboradorId, c.colaborador)" />
                  </td>
                </tr>
              </tbody>
            </v-table>
          </v-card>
        </div>
      </div>
      <v-alert v-else type="info" variant="tonal">Sem dados de apuração para o período.</v-alert>
    </div>

    <!-- ══ AVALIAÇÃO SEMANAL ══ -->
    <div v-else-if="aba === 'avaliacao'">
      <v-card rounded="lg" class="pa-4 mb-3">
        <v-row dense>
          <v-col cols="12" sm="6">
            <v-select v-model="avColab" label="Colaborador" :items="colaboradoresAtivos" item-title="nome" item-value="id"
              variant="outlined" density="compact" hide-details @update:model-value="carregarAvaliacao" />
          </v-col>
          <v-col cols="12" sm="6">
            <v-text-field v-model="avSemana" label="Semana (segunda-feira)" type="date"
              variant="outlined" density="compact" hide-details @change="carregarAvaliacao" />
          </v-col>
        </v-row>
      </v-card>

      <div v-if="avColab" class="av-grid">
        <div v-for="g in gruposAval" :key="g.titulo" class="mb-3">
          <div class="text-subtitle-2 font-weight-bold mb-1">{{ g.titulo }} <span class="text-caption text-medium-emphasis">({{ g.max }} pts)</span></div>
          <v-card rounded="lg" variant="outlined">
            <div v-for="it in g.itens" :key="it.key" class="d-flex align-center pa-2 av-item">
              <div class="flex-grow-1">{{ it.label }} <span class="text-caption text-medium-emphasis">({{ it.pts }} pts)</span></div>
              <v-btn-toggle v-model="aval[it.key]" mandatory density="compact" rounded="lg">
                <v-btn :value="0" size="small" color="error">Não</v-btn>
                <v-btn :value="50" size="small" color="amber-darken-2">Parcial</v-btn>
                <v-btn :value="100" size="small" color="success">Sim</v-btn>
              </v-btn-toggle>
            </div>
          </v-card>
        </div>
        <div class="d-flex align-center ga-3 mt-2">
          <div class="text-h6">Total: <b :class="totalAval >= 70 ? 'text-success' : 'text-amber-darken-2'">{{ totalAval }}</b> / 100 pts</div>
          <v-spacer />
          <v-btn color="amber-darken-2" :loading="salvando" prepend-icon="mdi-content-save" @click="salvarAvaliacao">Salvar avaliação</v-btn>
        </div>

        <!-- ══ PENALIDADES LANÇADAS NO MÊS ══ -->
        <v-card rounded="lg" variant="outlined" class="mt-5">
          <div class="pa-3 d-flex align-center">
            <v-icon color="error" class="mr-2">mdi-alert-decagram</v-icon>
            <div class="text-subtitle-1 font-weight-bold">Penalidades de {{ meses.find(m => m.value === mes)?.label }} / {{ ano }}</div>
            <v-spacer />
            <v-chip color="error" variant="flat" size="small">−{{ (penal?.descontoValidade ?? 0) + (penal?.descontoWhatsapp ?? 0) }} pts na performance</v-chip>
          </div>
          <v-divider />

          <!-- Produtos vencidos -->
          <div class="pa-3 d-flex align-center av-item">
            <v-icon color="amber-darken-3" class="mr-2">mdi-food-off</v-icon>
            <div class="flex-grow-1">
              <div class="font-weight-medium">Produtos vencidos na loja</div>
              <div class="text-caption text-medium-emphasis">Desconto de validade aplicado à equipe da loja quando há lote vencido em estoque</div>
            </div>
            <v-chip v-if="(penal?.descontoValidade ?? 0) > 0" color="error" variant="tonal" size="small">−{{ penal.descontoValidade }} pts</v-chip>
            <v-chip v-else color="success" variant="tonal" size="small">Sem desconto</v-chip>
          </div>
          <v-divider />

          <!-- Falta de atendimento no WhatsApp -->
          <div class="pa-3">
            <div class="d-flex align-center">
              <v-icon color="green-darken-2" class="mr-2">mdi-whatsapp</v-icon>
              <div class="flex-grow-1">
                <div class="font-weight-medium">Falta de atendimento no WhatsApp</div>
                <div class="text-caption text-medium-emphasis">Conversas não respondidas em 6h de funcionamento — 10 pts por conversa</div>
              </div>
              <v-chip v-if="(penal?.descontoWhatsapp ?? 0) > 0" color="error" variant="tonal" size="small">−{{ penal.descontoWhatsapp }} pts</v-chip>
              <v-chip v-else color="success" variant="tonal" size="small">Sem desconto</v-chip>
            </div>
            <v-table v-if="penal?.whatsapp?.length" density="compact" class="mt-2">
              <thead><tr><th>Data/hora</th><th>Telefone</th><th class="text-right">Pontos</th></tr></thead>
              <tbody>
                <tr v-for="(w, i) in penal.whatsapp" :key="i">
                  <td>{{ dataHora(w.data) }}</td>
                  <td>{{ w.telefone }}</td>
                  <td class="text-right text-error">−{{ w.pontos }}</td>
                </tr>
              </tbody>
            </v-table>
          </div>
        </v-card>
      </div>
      <v-alert v-else type="info" variant="tonal">Selecione um colaborador e a semana.</v-alert>
    </div>

    <!-- ══ ELEGIBILIDADE & CORTES ══ -->
    <div v-else-if="aba === 'elegibilidade'">
      <v-card rounded="lg" class="pa-4 mb-3">
        <v-select v-model="elColab" label="Colaborador" :items="colaboradoresAtivos" item-title="nome" item-value="id"
          variant="outlined" density="compact" hide-details style="max-width:380px" @update:model-value="carregarElegibilidade" />
      </v-card>
      <div v-if="elColab">
        <v-card rounded="lg" class="pa-4 mb-3">
          <div class="text-subtitle-2 font-weight-bold mb-3">Elegibilidade (mensal)</div>
          <v-text-field v-model.number="eleg.presencaPercent" label="Presença no mês (%)" type="number"
            variant="outlined" density="compact" style="max-width:220px" hint="Mínimo 95%" persistent-hint />
          <div class="d-flex flex-wrap ga-4 mt-3">
            <v-switch v-model="eleg.execucaoMinima" color="success" label="Execução mínima cumprida" hide-details inset />
            <v-switch v-model="eleg.advertencia" color="error" label="Teve advertência" hide-details inset />
          </div>
        </v-card>
        <v-card rounded="lg" class="pa-4 mb-3">
          <div class="text-subtitle-2 font-weight-bold mb-1">Cortes (zeram o prêmio — Cláusula 6ª)</div>
          <div class="d-flex flex-wrap ga-x-6 ga-y-1">
            <v-switch v-model="eleg.faltaInjustificada" color="error" label="Falta injustificada" hide-details inset />
            <v-switch v-model="eleg.produtoVencidoExposto" color="error" label="Produto vencido exposto" hide-details inset />
            <v-switch v-model="eleg.higieneGrave" color="error" label="Falha grave de higiene" hide-details inset />
            <v-switch v-model="eleg.rotinaNaoExecutada" color="error" label="Rotina mínima não executada" hide-details inset />
            <v-switch v-model="eleg.reclamacaoRelevante" color="error" label="Reclamação relevante de cliente" hide-details inset />
          </div>
        </v-card>
        <div class="d-flex justify-end">
          <v-btn color="amber-darken-2" :loading="salvando" prepend-icon="mdi-content-save" @click="salvarElegibilidade">Salvar</v-btn>
        </div>
      </div>
      <v-alert v-else type="info" variant="tonal">Selecione um colaborador.</v-alert>
    </div>

    <!-- ══ ARQUIVO ══ -->
    <div v-else-if="aba === 'arquivo'">
      <v-alert type="info" variant="tonal" density="comfortable" class="mb-3">
        Os demonstrativos são <b>arquivados automaticamente todo dia 2</b> (referentes ao mês anterior), com os valores congelados daquele mês. Você também pode arquivar/reprocessar o mês selecionado manualmente.
      </v-alert>
      <div class="d-flex ga-2 mb-3">
        <v-btn color="amber-darken-2" variant="tonal" prepend-icon="mdi-archive-arrow-down" :loading="arquivando" @click="arquivarMes">
          Arquivar {{ meses.find(m => m.value === mes)?.label }}/{{ ano }} agora
        </v-btn>
      </div>
      <v-card rounded="lg" elevation="1">
        <v-table density="comfortable">
          <thead><tr><th>Colaborador(a)</th><th>Competência</th><th class="text-right">Prêmio</th><th>Gerado em</th><th class="text-right">PDF</th></tr></thead>
          <tbody>
            <tr v-for="x in arquivo" :key="x.id">
              <td class="font-weight-medium">{{ x.colaboradorNome }}</td>
              <td>{{ x.competencia }}</td>
              <td class="text-right">R$ {{ fmt(x.premio) }}</td>
              <td class="text-medium-emphasis">{{ new Date(x.geradoEm).toLocaleString('pt-BR') }}</td>
              <td class="text-right">
                <v-btn size="small" color="red-darken-1" variant="tonal" prepend-icon="mdi-file-pdf-box"
                  @click="baixarArquivo(x)">PDF</v-btn>
              </td>
            </tr>
            <tr v-if="!arquivo.length"><td colspan="5" class="text-center text-medium-emphasis py-6">Nenhum demonstrativo arquivado para {{ meses.find(m => m.value === mes)?.label }}/{{ ano }}.</td></tr>
          </tbody>
        </v-table>
      </v-card>
    </div>

    <!-- ══ METAS & REGRAS ══ -->
    <div v-else-if="aba === 'metas'">
      <v-alert type="info" variant="tonal" density="comfortable" class="mb-3">
        As metas são <b>dinâmicas</b>: calculadas do <b>faturamento base</b> de cada loja ({{ metasInfo.baseInicio }} → {{ metasInfo.baseFim }}) × <b>{{ cfg.fatorMetaLoja }}%</b>, e a individual = meta da loja ÷ nº de vendedores. Mudam sozinhas todo mês. Você pode fixar um valor manual para um mês específico.
      </v-alert>
      <v-card rounded="lg" class="pa-4 mb-4">
        <div class="text-subtitle-2 font-weight-bold mb-3">Metas de {{ meses.find(m => m.value === mes)?.label }} / {{ ano }}</div>
        <div v-for="l in lojas" :key="l.id" class="mb-4">
          <div class="d-flex align-center ga-2 mb-1 flex-wrap">
            <div style="min-width:170px" class="font-weight-medium">{{ l.nome }}</div>
            <v-chip size="x-small" :color="metasEdit[l.id]?.manual ? 'amber-darken-2' : 'success'" variant="tonal">
              {{ metasEdit[l.id]?.manual ? 'Manual (fixada)' : 'Automática' }}
            </v-chip>
            <span class="text-caption text-medium-emphasis">base: R$ {{ fmt(metasEdit[l.id]?.baseFaturamento || 0) }} · {{ metasEdit[l.id]?.vendedores || 0 }} vendedor(es)</span>
            <v-chip size="x-small" color="amber-darken-2" variant="flat" class="ml-auto">Prêmio integral: R$ {{ fmt(metasEdit[l.id]?.valorBase || 0) }}/pessoa</v-chip>
          </div>
          <div class="d-flex align-center ga-3 flex-wrap">
            <v-text-field v-model.number="metasEdit[l.id].metaLoja" label="Meta da loja (R$)" type="number" prefix="R$"
              variant="outlined" density="compact" hide-details style="max-width:200px" />
            <v-text-field v-model.number="metasEdit[l.id].metaIndividual" label="Meta individual (R$)" type="number" prefix="R$"
              variant="outlined" density="compact" hide-details style="max-width:200px" />
            <v-btn size="small" color="amber-darken-2" variant="tonal" :loading="salvando" @click="salvarMeta(l.id)">Fixar p/ este mês</v-btn>
            <v-btn v-if="metasEdit[l.id]?.manual" size="small" variant="text" :loading="salvando" @click="voltarAutomatica(l.id)">Voltar ao automático</v-btn>
          </div>
        </div>
      </v-card>
      <v-card rounded="lg" class="pa-4">
        <div class="text-subtitle-2 font-weight-bold mb-3">Regras gerais</div>
        <div class="mb-2">
          <v-switch v-model="cfg.valorBaseDinamico" color="amber-darken-2" hide-details inset density="compact"
            label="Valor base proporcional ao faturamento (não fere a margem da loja)" />
        </div>
        <v-row dense>
          <v-col v-if="cfg.valorBaseDinamico" cols="6" sm="4"><v-text-field v-model.number="cfg.percentFaturamentoPremio" label="% da meta individual" type="number" suffix="%" variant="outlined" density="compact" hide-details hint="1,78% de R$22,5k ≈ R$400" persistent-hint /></v-col>
          <v-col v-else cols="6" sm="4"><v-text-field v-model.number="cfg.valorBase" label="Valor base fixo (R$)" type="number" prefix="R$" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="6" sm="4"><v-text-field v-model.number="cfg.redutorPercent" label="Redutor 90–99% (%)" type="number" suffix="%" variant="outlined" density="compact" hide-details hint="80 = base cai p/ 80%" persistent-hint /></v-col>
          <v-col cols="6" sm="4"><v-text-field v-model.number="cfg.fatorMetaLoja" label="Fator da meta (%)" type="number" suffix="%" variant="outlined" density="compact" hide-details hint="90 = meta é 90% do faturamento base" persistent-hint /></v-col>
          <v-col cols="6" sm="4"><v-text-field v-model.number="cfg.mesesBaseMeta" label="Meses da base" type="number" variant="outlined" density="compact" hide-details hint="1 = mês anterior; 3 = média trimestral" persistent-hint /></v-col>
          <v-col cols="6" sm="4"><v-text-field v-model.number="cfg.minPresenca" label="Presença mín. (%)" type="number" suffix="%" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="6" sm="4"><v-text-field v-model.number="cfg.thresholdLoja" label="Ativação loja (%)" type="number" suffix="%" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="6" sm="4"><v-text-field v-model.number="cfg.thresholdIndividual" label="Ativação individual (%)" type="number" suffix="%" variant="outlined" density="compact" hide-details /></v-col>
        </v-row>
        <div class="d-flex justify-end mt-3"><v-btn color="amber-darken-2" :loading="salvando" @click="salvarConfig">Salvar regras</v-btn></div>
      </v-card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const aba = ref('apuracao')
const mes = ref(new Date().getMonth() + 1)
const ano = ref(new Date().getFullYear())
const salvando = ref(false)
const meses = [
  { value: 1, label: 'Janeiro' }, { value: 2, label: 'Fevereiro' }, { value: 3, label: 'Março' },
  { value: 4, label: 'Abril' }, { value: 5, label: 'Maio' }, { value: 6, label: 'Junho' },
  { value: 7, label: 'Julho' }, { value: 8, label: 'Agosto' }, { value: 9, label: 'Setembro' },
  { value: 10, label: 'Outubro' }, { value: 11, label: 'Novembro' }, { value: 12, label: 'Dezembro' },
]
const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const pct = (v: number) => (v ?? 0).toFixed(1)

const apuracao = ref<any>(null)
const colaboradores = ref<any[]>([])
// Avaliação/Elegibilidade só para colaboradores ATIVOS.
const colaboradoresAtivos = computed(() => colaboradores.value.filter(c => c.ativo))
const lojas = computed(() => (auth.lojas ?? []) as any[])

const baixandoPdf = ref<string | null>(null)
async function carregar() {
  try {
    const r = await api.get('/premiacao/apuracao', { params: { empresaId: auth.empresaId, ano: ano.value, mes: mes.value } })
    apuracao.value = r.data
  } catch { apuracao.value = null }
  if (aba.value === 'metas') await carregarConfig()
}
async function baixarDemonstrativo(colaboradorId: string, nome: string) {
  baixandoPdf.value = colaboradorId
  try {
    const r = await api.get('/premiacao/demonstrativo-pdf', {
      params: { empresaId: auth.empresaId, ano: ano.value, mes: mes.value, colaboradorId }, responseType: 'blob',
    })
    const url = URL.createObjectURL(r.data as Blob)
    const link = document.createElement('a')
    link.href = url; link.download = `premio-${nome}-${ano.value}-${String(mes.value).padStart(2, '0')}.pdf`; link.click()
    setTimeout(() => URL.revokeObjectURL(url), 60000)
  } catch { notif.erro('Erro ao gerar o demonstrativo.') }
  finally { baixandoPdf.value = null }
}

// ── Avaliação semanal ──
const avColab = ref<string | null>(null)
const avSemana = ref(segundaFeira(new Date()))
const gruposAval = [
  { titulo: '1. Processo de Venda', max: 40, itens: [
    { key: 'abordagem', label: 'Abordagem', pts: 8 }, { key: 'diagnostico', label: 'Diagnóstico', pts: 8 },
    { key: 'conexaoProduto', label: 'Conexão com Produto', pts: 8 }, { key: 'sugestaoComplementar', label: 'Sugestão Complementar', pts: 8 },
    { key: 'fechamento', label: 'Fechamento', pts: 8 } ] },
  { titulo: '2. Execução Operacional', max: 30, itens: [
    { key: 'abastecimento', label: 'Abastecimento', pts: 10 }, { key: 'organizacao', label: 'Organização', pts: 10 }, { key: 'rotina', label: 'Rotina', pts: 10 } ] },
  { titulo: '3. Qualidade da Operação', max: 30, itens: [
    { key: 'validade', label: 'Validade', pts: 10 }, { key: 'perdas', label: 'Perdas', pts: 10 }, { key: 'armazenamento', label: 'Armazenamento e Integridade', pts: 10 } ] },
]
const itensKeys = gruposAval.flatMap(g => g.itens.map(i => ({ key: i.key, pts: i.pts })))
const aval = ref<Record<string, number>>({})
function resetAval() { const o: Record<string, number> = {}; itensKeys.forEach(i => o[i.key] = 100); aval.value = o }
resetAval()
const totalAval = computed(() => Math.round(itensKeys.reduce((s, i) => s + i.pts * (aval.value[i.key] ?? 0) / 100, 0)))

async function carregarAvaliacao() {
  if (!avColab.value || !avSemana.value) return
  resetAval()
  try {
    const r = await api.get('/premiacao/avaliacoes', { params: { empresaId: auth.empresaId, ano: new Date(avSemana.value).getFullYear(), mes: new Date(avSemana.value).getMonth() + 1, colaboradorId: avColab.value } })
    const a = (r.data as any[]).find(x => x.inicioSemana === avSemana.value)
    if (a) itensKeys.forEach(i => aval.value[i.key] = a[i.key] ?? 0)
  } catch { /* nova avaliação */ }
  await carregarPenalidades()
}

// ── Penalidades lançadas no mês (produtos vencidos + WhatsApp) ──
const penal = ref<any>(null)
const dataHora = (s: string) => s ? new Date(s).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' }) : ''
async function carregarPenalidades() {
  penal.value = null
  if (!avColab.value) return
  try {
    const r = await api.get('/premiacao/penalidades', { params: { empresaId: auth.empresaId, ano: ano.value, mes: mes.value, colaboradorId: avColab.value } })
    penal.value = r.data
  } catch { penal.value = null }
}
async function salvarAvaliacao() {
  const c = colaboradores.value.find(x => x.id === avColab.value)
  if (!c) return
  salvando.value = true
  try {
    await api.post('/premiacao/avaliacoes', {
      empresaId: auth.empresaId, localEstoqueId: c.localEstoqueId, colaboradorId: avColab.value,
      inicioSemana: avSemana.value, ...aval.value,
    })
    notif.ok('Avaliação salva.')
  } catch { notif.erro('Erro ao salvar avaliação.') } finally { salvando.value = false }
}

// ── Elegibilidade ──
const elColab = ref<string | null>(null)
const eleg = ref<any>(elegPadrao())
function elegPadrao() { return { presencaPercent: 100, execucaoMinima: true, advertencia: false, faltaInjustificada: false, produtoVencidoExposto: false, higieneGrave: false, rotinaNaoExecutada: false, reclamacaoRelevante: false } }
async function carregarElegibilidade() {
  eleg.value = elegPadrao()
  // a apuração é lida junto da apuração geral; aqui só reseta e o gestor preenche/salva
}
async function salvarElegibilidade() {
  const c = colaboradores.value.find(x => x.id === elColab.value)
  if (!c) return
  salvando.value = true
  try {
    await api.post('/premiacao/apuracao-mensal', {
      empresaId: auth.empresaId, localEstoqueId: c.localEstoqueId, colaboradorId: elColab.value,
      ano: ano.value, mes: mes.value, ...eleg.value,
    })
    notif.ok('Elegibilidade salva.'); await carregar()
  } catch { notif.erro('Erro ao salvar.') } finally { salvando.value = false }
}

// ── Metas & config ──
const cfg = ref<any>({ valorBase: 400, redutorPercent: 80, minPresenca: 95, thresholdLoja: 90, thresholdIndividual: 90, fatorMetaLoja: 90, mesesBaseMeta: 3, valorBaseDinamico: true, percentFaturamentoPremio: 1.78 })
const metasInfo = ref<any>({ baseInicio: '', baseFim: '' })
const metasEdit = ref<Record<string, any>>({})
async function carregarConfig() {
  try {
    const r = await api.get('/premiacao/config', { params: { empresaId: auth.empresaId, ano: ano.value, mes: mes.value } })
    cfg.value = { valorBase: r.data.valorBase, redutorPercent: r.data.redutorPercent, minPresenca: r.data.minPresenca, thresholdLoja: r.data.thresholdLoja, thresholdIndividual: r.data.thresholdIndividual, fatorMetaLoja: r.data.fatorMetaLoja, mesesBaseMeta: r.data.mesesBaseMeta, valorBaseDinamico: r.data.valorBaseDinamico, percentFaturamentoPremio: r.data.percentFaturamentoPremio }
    metasInfo.value = { baseInicio: r.data.baseInicio, baseFim: r.data.baseFim }
    const m: Record<string, any> = {}
    for (const l of lojas.value) {
      const found = (r.data.metas as any[]).find(x => x.localEstoqueId === l.id)
      m[l.id] = { metaLoja: found?.metaLoja ?? 0, metaIndividual: found?.metaIndividual ?? 0, baseFaturamento: found?.baseFaturamento ?? 0, vendedores: found?.vendedores ?? 0, manual: found?.manual ?? false }
    }
    metasEdit.value = m
  } catch { /* sem config ainda */ }
}
async function salvarMeta(lojaId: string) {
  salvando.value = true
  try {
    await api.put('/premiacao/metas', { empresaId: auth.empresaId, localEstoqueId: lojaId, ano: ano.value, mes: mes.value, metaLoja: metasEdit.value[lojaId].metaLoja, metaIndividual: metasEdit.value[lojaId].metaIndividual })
    notif.ok('Meta fixada para o mês.'); await carregarConfig(); await carregar()
  } catch { notif.erro('Erro ao salvar meta.') } finally { salvando.value = false }
}
async function voltarAutomatica(lojaId: string) {
  salvando.value = true
  try {
    await api.delete('/premiacao/metas', { params: { empresaId: auth.empresaId, localEstoqueId: lojaId, ano: ano.value, mes: mes.value } })
    notif.ok('Meta voltou ao automático.'); await carregarConfig(); await carregar()
  } catch { notif.erro('Erro ao remover meta.') } finally { salvando.value = false }
}
async function salvarConfig() {
  salvando.value = true
  try {
    await api.put('/premiacao/config', { empresaId: auth.empresaId, ...cfg.value, ativo: true })
    notif.ok('Regras salvas.'); await carregarConfig(); await carregar()
  } catch { notif.erro('Erro ao salvar regras.') } finally { salvando.value = false }
}

function segundaFeira(d: Date): string {
  const x = new Date(d); const dia = (x.getDay() + 6) % 7; x.setDate(x.getDate() - dia)
  return x.toISOString().slice(0, 10)
}

// ── Arquivo ──
const arquivo = ref<any[]>([])
const arquivando = ref(false)
async function carregarArquivo() {
  const r = await api.get('/premiacao/arquivo', { params: { empresaId: auth.empresaId, ano: ano.value, mes: mes.value } }).catch(() => ({ data: [] }))
  arquivo.value = r.data
}
async function arquivarMes() {
  arquivando.value = true
  try {
    const r = await api.post('/premiacao/arquivar', null, { params: { empresaId: auth.empresaId, ano: ano.value, mes: mes.value } })
    notif.ok(`${r.data.arquivados} demonstrativo(s) arquivado(s).`); await carregarArquivo()
  } catch { notif.erro('Erro ao arquivar.') } finally { arquivando.value = false }
}
async function baixarArquivo(x: any) {
  const r = await api.get(`/premiacao/arquivo/${x.id}/pdf`, { responseType: 'blob' })
  const url = URL.createObjectURL(r.data as Blob)
  const link = document.createElement('a'); link.href = url; link.download = `premio-${x.colaboradorNome}-${x.competencia.replace('/', '-')}.pdf`; link.click()
  setTimeout(() => URL.revokeObjectURL(url), 60000)
}

watch(aba, v => { if (v === 'metas') carregarConfig(); if (v === 'arquivo') carregarArquivo() })
watch([mes, ano], () => { if (aba.value === 'avaliacao' && avColab.value) carregarPenalidades() })

onMounted(async () => {
  if (!auth.lojas?.length) await auth.carregarLojas()
  const r = await api.get('/usuarios', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] }))
  colaboradores.value = (r.data as any[]).filter(u => u.localEstoqueId)
  await carregar()
})
</script>

<style scoped>
.av-item:not(:last-child){border-bottom:1px solid rgba(128,128,128,.15)}
.ga-x-6{column-gap:24px}
</style>
