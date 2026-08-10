<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Financiamentos — Comprometimento Mensal</div>
      <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNovo">Novo financiamento</v-btn>
    </div>

    <div v-if="carregando" class="d-flex justify-center pa-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <template v-else-if="dados && dados.resumo.parcelasRestantes > 0">
      <!-- Cards de resumo -->
      <v-row class="mb-1">
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1">
            <v-card-text class="pa-3">
              <div class="d-flex align-center gap-1 mb-1">
                <v-icon icon="mdi-calendar-clock" color="primary" size="15" />
                <span class="text-caption text-medium-emphasis">Comprometimento/mês</span>
              </div>
              <div class="text-h6 font-weight-bold text-primary">R$ {{ fmt(dados.resumo.comprometimentoMensal) }}</div>
              <div class="text-caption text-medium-emphasis">{{ dados.resumo.contratosAtivos }} contrato(s) ativo(s)</div>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1">
            <v-card-text class="pa-3">
              <div class="d-flex align-center gap-1 mb-1">
                <v-icon icon="mdi-cash-multiple" color="error" size="15" />
                <span class="text-caption text-medium-emphasis">Total a pagar</span>
              </div>
              <div class="text-h6 font-weight-bold text-error">R$ {{ fmt(dados.resumo.totalRestante) }}</div>
              <div class="text-caption text-medium-emphasis">{{ dados.resumo.parcelasRestantes }} parcelas</div>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1">
            <v-card-text class="pa-3">
              <div class="d-flex align-center gap-1 mb-1">
                <v-icon icon="mdi-bank-outline" color="brown" size="15" />
                <span class="text-caption text-medium-emphasis">Juros a pagar</span>
              </div>
              <div class="text-h6 font-weight-bold text-brown">R$ {{ fmt(dados.resumo.jurosRestante) }}</div>
              <div class="text-caption text-medium-emphasis">custo financeiro</div>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1">
            <v-card-text class="pa-3">
              <div class="d-flex align-center gap-1 mb-1">
                <v-icon icon="mdi-flag-checkered" color="success" size="15" />
                <span class="text-caption text-medium-emphasis">Quita em</span>
              </div>
              <div class="text-h6 font-weight-bold text-success">{{ mesAno(dados.resumo.ultimoVencimento) }}</div>
              <div class="text-caption text-medium-emphasis">principal R$ {{ fmt(dados.resumo.principalRestante) }}</div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <!-- Por contrato -->
      <v-card rounded="xl" elevation="1" class="mb-4">
        <v-card-title class="text-body-1 font-weight-bold pa-4 pb-2">Por contrato</v-card-title>
        <v-table density="comfortable">
          <thead>
            <tr>
              <th>Contrato</th>
              <th class="text-right">Parcela/mês</th>
              <th class="text-right">Parcelas restantes</th>
              <th class="text-right">Total restante</th>
              <th class="text-right">Juros</th>
              <th class="text-right">Quita em</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="c in dados.contratos" :key="c.nome">
              <td class="font-weight-medium">{{ c.nome }}</td>
              <td class="text-right">R$ {{ fmt(c.parcelaMensal) }}</td>
              <td class="text-right">{{ c.parcelasRestantes }}</td>
              <td class="text-right">R$ {{ fmt(c.restante) }}</td>
              <td class="text-right text-brown">R$ {{ fmt(c.juros) }}</td>
              <td class="text-right">{{ mesAno(c.ultimoVencimento) }}</td>
            </tr>
          </tbody>
        </v-table>
      </v-card>

      <!-- Linha do tempo mês a mês -->
      <v-card rounded="xl" elevation="1">
        <v-card-title class="text-body-1 font-weight-bold pa-4 pb-2 d-flex align-center">
          Linha do tempo
          <v-spacer />
          <div class="d-flex align-center gap-3 text-caption">
            <span><v-icon icon="mdi-square" color="brown" size="12" /> Juros</span>
            <span><v-icon icon="mdi-square" color="teal" size="12" /> Amortização</span>
          </div>
        </v-card-title>
        <v-table density="compact" fixed-header height="440">
          <thead>
            <tr>
              <th>Mês</th>
              <th style="min-width:180px">Composição da parcela</th>
              <th class="text-right">Juros</th>
              <th class="text-right">Amortização</th>
              <th class="text-right">Parcela</th>
              <th class="text-right">Saldo devedor</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="m in dados.timeline" :key="m.mes">
              <td class="font-weight-medium">{{ m.label }}</td>
              <td>
                <div class="d-flex barra">
                  <div class="seg" :style="{ width: pctJuros(m) + '%', background: 'rgb(var(--v-theme-brown, 121,85,72))' }" />
                  <div class="seg" :style="{ width: (100 - pctJuros(m)) + '%', background: 'rgb(var(--v-theme-teal, 0,150,136))' }" />
                </div>
              </td>
              <td class="text-right text-brown">R$ {{ fmt(m.juros) }}</td>
              <td class="text-right text-teal">R$ {{ fmt(m.amortizacao) }}</td>
              <td class="text-right font-weight-medium">R$ {{ fmt(m.parcela) }}</td>
              <td class="text-right text-medium-emphasis">R$ {{ fmt(m.saldoApos) }}</td>
            </tr>
          </tbody>
        </v-table>
      </v-card>
    </template>

    <v-card v-else rounded="xl" elevation="0" class="pa-8 text-center">
      <v-icon icon="mdi-bank-off-outline" size="48" class="text-medium-emphasis mb-2" />
      <div class="text-body-1 text-medium-emphasis">Nenhum financiamento em aberto. Clique em "Novo financiamento".</div>
    </v-card>

    <!-- Contratos cadastrados -->
    <v-card v-if="cadastros.length" rounded="xl" elevation="1" class="mt-4">
      <v-card-title class="text-body-1 font-weight-bold pa-4 pb-2">Contratos cadastrados</v-card-title>
      <v-table density="comfortable">
        <thead>
          <tr>
            <th>Contrato</th><th class="text-right">Crédito</th><th class="text-right">Parcela</th>
            <th class="text-right">Parcelas</th><th class="text-right">Taxa efetiva</th>
            <th class="text-center">Contrato</th><th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="f in cadastros" :key="f.id">
            <td class="font-weight-medium">{{ f.descricao }}</td>
            <td class="text-right">R$ {{ fmt(f.valorCredito) }}</td>
            <td class="text-right">R$ {{ fmt(f.valorParcela) }}</td>
            <td class="text-right">{{ f.numeroParcelas }}x</td>
            <td class="text-right">{{ (f.taxaEfetivaMensal * 100).toFixed(4) }}% a.m.</td>
            <td class="text-center">
              <v-btn v-if="f.contratoPdfUrl" :href="f.contratoPdfUrl" target="_blank"
                icon="mdi-file-pdf-box" size="small" variant="text" color="red" />
              <v-btn :icon="f.contratoPdfUrl ? 'mdi-file-replace-outline' : 'mdi-paperclip'"
                size="small" variant="text" :color="f.contratoPdfUrl ? 'grey' : 'primary'"
                :loading="anexandoId === f.id" @click="escolherContrato(f)" />
            </td>
            <td class="text-right">
              <v-btn icon="mdi-delete-outline" size="small" variant="text" color="error" @click="excluir(f)" />
            </td>
          </tr>
        </tbody>
      </v-table>
    </v-card>

    <!-- Diálogo Novo financiamento -->
    <v-dialog v-model="dialogo" max-width="760" persistent scrollable>
      <v-card rounded="xl">
        <v-card-title class="d-flex align-center pa-4">
          <span class="font-weight-bold">Novo financiamento</span>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="dialogo = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <!-- Passo 1: PDF -->
          <v-file-input v-model="pdf" accept="application/pdf" label="PDF do contrato/extrato (SICREDI)"
            prepend-icon="mdi-file-pdf-box" variant="outlined" density="compact" show-size
            :loading="analisando" @update:model-value="analisar" />
          <v-alert v-if="erroLeitura" type="warning" variant="tonal" density="compact" class="mb-3">
            Não consegui ler os dados automaticamente. Preencha os campos abaixo à mão.
          </v-alert>

          <!-- Passo 2: dados (editáveis) -->
          <v-row dense>
            <v-col cols="12" sm="6"><v-text-field v-model="form.banco" label="Banco" variant="outlined" density="compact" /></v-col>
            <v-col cols="12" sm="6"><v-text-field v-model="form.titulo" label="Nº do título" variant="outlined" density="compact" /></v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model.number="form.credito" type="number" label="Crédito real (entrou na conta)"
                variant="outlined" density="compact" hint="No extrato vem como 'Valor Liberado' (total). Confira o crédito real." persistent-hint @update:model-value="recalcular" />
            </v-col>
            <v-col cols="6" sm="6"><v-text-field v-model.number="form.valorParcela" type="number" label="Valor da parcela" variant="outlined" density="compact" @update:model-value="recalcular" /></v-col>
            <v-col cols="6" sm="4"><v-text-field v-model.number="form.numeroParcelas" type="number" label="Nº parcelas" variant="outlined" density="compact" @update:model-value="recalcular" /></v-col>
            <v-col cols="6" sm="4"><v-text-field v-model.number="form.parcelasPagas" type="number" label="Já pagas" variant="outlined" density="compact" @update:model-value="recalcular" /></v-col>
            <v-col cols="6" sm="4"><v-text-field v-model="form.primeiroVencimento" type="date" label="1º vencimento" variant="outlined" density="compact" @update:model-value="recalcular" /></v-col>
            <v-col cols="12"><v-checkbox v-model="form.lancarEntrada" label="Lançar a entrada do crédito no caixa (Empréstimo Captado)" density="compact" hide-details /></v-col>
          </v-row>

          <!-- Prévia -->
          <v-card v-if="previa" variant="tonal" color="brown" rounded="lg" class="mt-2 pa-3">
            <div class="d-flex flex-wrap gap-4 text-body-2">
              <div><b>Taxa efetiva:</b> {{ (previa.taxaEfetivaMensal * 100).toFixed(4) }}% a.m.</div>
              <div><b>Parcelas a lançar:</b> {{ previa.parcelasRestantes }}</div>
              <div><b>Total:</b> R$ {{ fmt(previa.totalRestante) }}</div>
              <div><b>Juros:</b> R$ {{ fmt(previa.jurosRestante) }}</div>
            </div>
          </v-card>
          <v-table v-if="previa" density="compact" height="200" fixed-header class="mt-2">
            <thead><tr><th>#</th><th>Vencimento</th><th class="text-right">Juros</th><th class="text-right">Amortização</th><th class="text-right">Parcela</th></tr></thead>
            <tbody>
              <tr v-for="l in previa.linhas" :key="l.numero">
                <td>{{ l.numero }}</td>
                <td>{{ dataBr(l.vencimento) }}</td>
                <td class="text-right text-brown">R$ {{ fmt(l.juros) }}</td>
                <td class="text-right text-teal">R$ {{ fmt(l.amortizacao) }}</td>
                <td class="text-right">R$ {{ fmt(l.parcela) }}</td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="dialogo = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvando" :disabled="!podeConfirmar" @click="confirmar">
            Lançar {{ previa ? previa.parcelasRestantes : '' }} parcelas
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const carregando = ref(true)
const dados = ref<any>(null)
const cadastros = ref<any[]>([])

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const mesAno = (iso?: string) => {
  if (!iso) return '—'
  const d = new Date(iso)
  const m = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']
  return `${m[d.getMonth()]}/${String(d.getFullYear()).slice(2)}`
}
const dataBr = (iso: string) => new Date(iso).toLocaleDateString('pt-BR')
const pctJuros = (m: any) => m.parcela > 0 ? Math.round((m.juros / m.parcela) * 100) : 0

// ── Novo financiamento ─────────────────────────────────────────────
const dialogo = ref(false)
const pdf = ref<File | File[] | null>(null)
const analisando = ref(false)
const salvando = ref(false)
const erroLeitura = ref(false)
const previa = ref<any>(null)
const form = ref<any>({
  banco: '', titulo: '', credito: 0, valorParcela: 0, numeroParcelas: 0,
  parcelasPagas: 0, primeiroVencimento: '', lancarEntrada: true,
})

const podeConfirmar = computed(() =>
  form.value.credito > 0 && form.value.valorParcela > 0 && form.value.numeroParcelas > 0 && !!form.value.primeiroVencimento)

function abrirNovo() {
  pdf.value = null; previa.value = null; erroLeitura.value = false
  form.value = { banco: '', titulo: '', credito: 0, valorParcela: 0, numeroParcelas: 0, parcelasPagas: 0, primeiroVencimento: '', lancarEntrada: true }
  dialogo.value = true
}

const arquivo = () => Array.isArray(pdf.value) ? pdf.value[0] : pdf.value

async function analisar() {
  const f = arquivo()
  if (!f) return
  analisando.value = true; erroLeitura.value = false
  try {
    const fd = new FormData(); fd.append('arquivo', f)
    const { data } = await api.post('/financeiro/financiamentos/analisar-pdf', fd)
    if (!data.reconhecido) { erroLeitura.value = true; return }
    form.value = {
      banco: data.banco ?? 'SICREDI', titulo: data.titulo ?? '',
      credito: data.creditoEstimado ?? 0, valorParcela: data.valorParcela ?? 0,
      numeroParcelas: data.numeroParcelas ?? 0, parcelasPagas: data.parcelasPagas ?? 0,
      primeiroVencimento: (data.primeiroVencimento ?? '').slice(0, 10), lancarEntrada: true,
    }
    previa.value = data.previa
  } catch { erroLeitura.value = true } finally { analisando.value = false }
}

let deb: any
function recalcular() {
  clearTimeout(deb)
  deb = setTimeout(async () => {
    if (!podeConfirmar.value) { previa.value = null; return }
    const { data } = await api.post('/financeiro/financiamentos/previa', {
      credito: form.value.credito, valorParcela: form.value.valorParcela,
      numeroParcelas: form.value.numeroParcelas, parcelasPagas: form.value.parcelasPagas,
      primeiroVencimento: form.value.primeiroVencimento,
    })
    previa.value = data
  }, 350)
}

async function confirmar() {
  salvando.value = true
  try {
    const fd = new FormData()
    fd.append('empresaId', auth.empresaId as string)
    fd.append('banco', form.value.banco || 'Banco')
    fd.append('titulo', form.value.titulo || '')
    fd.append('credito', String(form.value.credito))
    fd.append('valorParcela', String(form.value.valorParcela))
    fd.append('numeroParcelas', String(form.value.numeroParcelas))
    fd.append('parcelasPagas', String(form.value.parcelasPagas || 0))
    fd.append('primeiroVencimento', form.value.primeiroVencimento)
    fd.append('lancarEntrada', String(!!form.value.lancarEntrada))
    const f = arquivo(); if (f) fd.append('contrato', f)
    await api.post('/financeiro/financiamentos', fd)
    dialogo.value = false
    await carregar()
  } finally { salvando.value = false }
}

const anexandoId = ref<string | null>(null)
function escolherContrato(f: any) {
  const input = document.createElement('input')
  input.type = 'file'; input.accept = 'application/pdf'
  input.onchange = async () => {
    const file = input.files?.[0]
    if (!file) return
    anexandoId.value = f.id
    try {
      const fd = new FormData(); fd.append('arquivo', file)
      await api.post(`/financeiro/financiamentos/${f.id}/contrato`, fd)
      await carregar()
    } finally { anexandoId.value = null }
  }
  input.click()
}

async function excluir(f: any) {
  if (!confirm(`Excluir o financiamento "${f.descricao}" e suas parcelas em aberto?`)) return
  await api.delete(`/financeiro/financiamentos/${f.id}`)
  await carregar()
}

async function carregar() {
  carregando.value = true
  try {
    const [comp, cad] = await Promise.all([
      api.get('/financeiro/financiamentos/comprometimento', { params: { empresaId: auth.empresaId } }),
      api.get('/financeiro/financiamentos', { params: { empresaId: auth.empresaId } }),
    ])
    dados.value = comp.data
    cadastros.value = cad.data
  } finally { carregando.value = false }
}

onMounted(carregar)
</script>

<style scoped>
.barra { height: 14px; border-radius: 4px; overflow: hidden; width: 100%; }
.seg { height: 100%; }
</style>
