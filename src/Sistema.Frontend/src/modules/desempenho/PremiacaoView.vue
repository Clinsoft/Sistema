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
                  <th class="text-right">Performance</th>
                  <th class="text-center">Status</th>
                  <th class="text-right">Prêmio</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="c in lj.colaboradores" :key="c.colaboradorId">
                  <td class="font-weight-medium">{{ c.colaborador }}</td>
                  <td class="text-right">{{ fmt(c.vendaIndividual) }} <span class="text-caption text-medium-emphasis">/ {{ fmt(c.metaIndividual) }}</span></td>
                  <td class="text-right" :class="c.percentIndividual >= 100 ? 'text-success' : c.percentIndividual >= 90 ? 'text-amber-darken-2' : 'text-error'">{{ pct(c.percentIndividual) }}%</td>
                  <td class="text-right">{{ pct(c.performancePercent) }}% <span class="text-caption text-medium-emphasis">({{ c.semanasAvaliadas }}s)</span></td>
                  <td class="text-center">
                    <v-chip v-if="c.premio > 0" size="x-small" color="success" variant="tonal">Elegível</v-chip>
                    <v-tooltip v-else :text="c.motivo || 'Sem prêmio'"><template #activator="{ props }">
                      <v-chip v-bind="props" size="x-small" color="grey" variant="tonal">Zerado</v-chip>
                    </template></v-tooltip>
                  </td>
                  <td class="text-right font-weight-bold" :class="c.premio > 0 ? 'text-success' : 'text-medium-emphasis'">R$ {{ fmt(c.premio) }}</td>
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
            <v-select v-model="avColab" label="Colaborador" :items="colaboradores" item-title="nome" item-value="id"
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
      </div>
      <v-alert v-else type="info" variant="tonal">Selecione um colaborador e a semana.</v-alert>
    </div>

    <!-- ══ ELEGIBILIDADE & CORTES ══ -->
    <div v-else-if="aba === 'elegibilidade'">
      <v-card rounded="lg" class="pa-4 mb-3">
        <v-select v-model="elColab" label="Colaborador" :items="colaboradores" item-title="nome" item-value="id"
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

    <!-- ══ METAS & REGRAS ══ -->
    <div v-else-if="aba === 'metas'">
      <v-card rounded="lg" class="pa-4 mb-4">
        <div class="text-subtitle-2 font-weight-bold mb-3">Metas por loja</div>
        <div v-for="l in lojas" :key="l.id" class="d-flex align-center ga-3 mb-2 flex-wrap">
          <div style="min-width:170px" class="font-weight-medium">{{ l.nome }}</div>
          <v-text-field v-model.number="metasEdit[l.id].metaLoja" label="Meta da loja (R$)" type="number" prefix="R$"
            variant="outlined" density="compact" hide-details style="max-width:200px" />
          <v-text-field v-model.number="metasEdit[l.id].metaIndividual" label="Meta individual (R$)" type="number" prefix="R$"
            variant="outlined" density="compact" hide-details style="max-width:200px" />
          <v-btn size="small" color="amber-darken-2" variant="tonal" :loading="salvando" @click="salvarMeta(l.id)">Salvar</v-btn>
        </div>
      </v-card>
      <v-card rounded="lg" class="pa-4">
        <div class="text-subtitle-2 font-weight-bold mb-3">Regras gerais</div>
        <v-row dense>
          <v-col cols="6" sm="4"><v-text-field v-model.number="cfg.valorBase" label="Valor base (R$)" type="number" prefix="R$" variant="outlined" density="compact" hide-details /></v-col>
          <v-col cols="6" sm="4"><v-text-field v-model.number="cfg.redutorPercent" label="Redutor 90–99% (%)" type="number" suffix="%" variant="outlined" density="compact" hide-details hint="80 = base cai p/ R$320" persistent-hint /></v-col>
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
const lojas = computed(() => (auth.lojas ?? []) as any[])

async function carregar() {
  try {
    const r = await api.get('/premiacao/apuracao', { params: { empresaId: auth.empresaId, ano: ano.value, mes: mes.value } })
    apuracao.value = r.data
  } catch { apuracao.value = null }
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
const cfg = ref<any>({ valorBase: 400, redutorPercent: 80, minPresenca: 95, thresholdLoja: 90, thresholdIndividual: 90 })
const metasEdit = ref<Record<string, any>>({})
async function carregarConfig() {
  try {
    const r = await api.get('/premiacao/config', { params: { empresaId: auth.empresaId } })
    cfg.value = { valorBase: r.data.valorBase, redutorPercent: r.data.redutorPercent, minPresenca: r.data.minPresenca, thresholdLoja: r.data.thresholdLoja, thresholdIndividual: r.data.thresholdIndividual }
    const m: Record<string, any> = {}
    for (const l of lojas.value) {
      const found = (r.data.metas as any[]).find(x => x.localEstoqueId === l.id)
      m[l.id] = { metaLoja: found?.metaLoja ?? 0, metaIndividual: found?.metaIndividual ?? 0 }
    }
    metasEdit.value = m
  } catch { /* sem config ainda */ }
}
async function salvarMeta(lojaId: string) {
  salvando.value = true
  try {
    await api.put('/premiacao/metas', { empresaId: auth.empresaId, localEstoqueId: lojaId, metaLoja: metasEdit.value[lojaId].metaLoja, metaIndividual: metasEdit.value[lojaId].metaIndividual })
    notif.ok('Meta salva.'); await carregar()
  } catch { notif.erro('Erro ao salvar meta.') } finally { salvando.value = false }
}
async function salvarConfig() {
  salvando.value = true
  try {
    await api.put('/premiacao/config', { empresaId: auth.empresaId, ...cfg.value, ativo: true })
    notif.ok('Regras salvas.'); await carregar()
  } catch { notif.erro('Erro ao salvar regras.') } finally { salvando.value = false }
}

function segundaFeira(d: Date): string {
  const x = new Date(d); const dia = (x.getDay() + 6) % 7; x.setDate(x.getDate() - dia)
  return x.toISOString().slice(0, 10)
}

watch(aba, v => { if (v === 'metas') carregarConfig() })

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
