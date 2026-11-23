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
        <v-select v-model="loja" :items="lojasOpcoes" item-title="nome" item-value="id"
          label="Loja" variant="outlined" density="comfortable" hide-details
          prepend-inner-icon="mdi-store" style="max-width:220px" />
        <v-file-input v-model="arquivo" label="Relatório de vendas (CSV ou Excel)" accept=".csv,.xlsx,.xls"
          variant="outlined" density="comfortable" hide-details prepend-icon="mdi-file-delimited-outline"
          style="max-width:420px" />
        <v-btn color="indigo" :loading="carregando" :disabled="!arquivo" prepend-icon="mdi-compare-horizontal"
          class="text-none" @click="importar">Conciliar</v-btn>
      </div>
      <div class="text-caption text-medium-emphasis mt-2">
        Use o <b>Relatório de Vendas</b> da InfinitePay — aceita o <b>CSV</b> antigo e o <b>Excel (.xlsx)</b> novo ("Rede/Relatório de Vendas"). Não use o extrato da conta. O período é detectado pelo arquivo.
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

      <v-card v-if="res.formaDivergente?.length" rounded="xl" elevation="1" class="mb-4">
        <v-card-title class="text-subtitle-1 d-flex align-center">
          <v-icon color="deep-orange" class="mr-2">mdi-swap-horizontal</v-icon>
          Forma registrada errada ({{ res.formaDivergente.length }})
        </v-card-title>
        <v-card-subtitle>Casou pela hora+valor, mas a forma no sistema difere da máquina (ex.: cliente pagou Pix e foi lançado como cartão).</v-card-subtitle>
        <v-table density="compact" height="240" fixed-header>
          <thead><tr><th>Data</th><th class="text-right">Valor</th><th>No sistema</th><th></th><th>Na máquina</th><th>Cliente</th></tr></thead>
          <tbody>
            <tr v-for="(t,i) in res.formaDivergente" :key="i">
              <td>{{ fmtD(t.data) }}</td><td class="text-right">{{ fmt(t.valor) }}</td>
              <td><v-chip size="x-small" color="grey" label>{{ t.formaSistema }}</v-chip></td>
              <td><v-icon size="16">mdi-arrow-right</v-icon></td>
              <td><v-chip size="x-small" color="deep-orange" label>{{ t.formaOperadora }}</v-chip></td>
              <td class="text-truncate" style="max-width:140px">{{ t.cliente }}</td>
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
import * as XLSX from 'xlsx'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const arquivo = ref<File | File[] | null>(null)
const carregando = ref(false)
const res = ref<any>(null)
// Cada relatório da operadora é de UMA loja (CNPJ/conta própria) — concilia por loja.
const loja = ref<string | null>(auth.lojaAtualId)
const lojasOpcoes = [{ id: null as any, nome: 'Todas as lojas' }, ...auth.lojas]

// Adapta QUALQUER export da InfinitePay (xlsx "Rede/Relatório de Vendas" ou CSV antigo)
// para o CSV normalizado que o backend entende. Reconhece cabeçalhos novos e antigos e
// resolve a data (série do Excel) + hora. Se não reconhecer, devolve o arquivo original.
async function normalizarArquivo(file: File): Promise<File> {
  let rows: any[][]
  try {
    const wb = XLSX.read(await file.arrayBuffer(), { type: 'array' })
    const ws = wb.Sheets[wb.SheetNames[0]]
    rows = XLSX.utils.sheet_to_json(ws, { header: 1, raw: true, blankrows: false, defval: '' }) as any[][]
  } catch { return file }
  if (!rows?.length) return file

  const norm = (s: any) => String(s ?? '').trim().toLowerCase()
  const nomesData = ['data da venda', 'data e hora', 'data']
  let h = -1
  for (let i = 0; i < Math.min(rows.length, 15); i++)
    if (rows[i].some(c => nomesData.includes(norm(c)))) { h = i; break }
  if (h < 0) return file
  const header = rows[h].map(norm)
  const col = (...nomes: string[]) => { for (const n of nomes) { const i = header.indexOf(n); if (i >= 0) return i } return -1 }
  const iData = col('data da venda', 'data e hora', 'data')
  const iHora = col('hora da venda')
  const iStatus = col('status da venda', 'status')
  const iValor = col('valor da venda original', 'valor da venda atualizado', 'valor (r$)', 'valor bruto (r$)', 'valor')
  const iMeio = col('modalidade', 'meio - meio', 'método', 'metodo', 'meio de pagamento')
  const iTaxa = col('valor total das taxas descontadas (mdr+recebimento automático)', 'taxa aplicada - valor(r$)', 'valor mdr', 'taxa')
  const iNome = col('origem - nome', 'comprador', 'nome')
  if (iData < 0 || iMeio < 0 || iValor < 0) return file

  const p2 = (x: number) => String(x).padStart(2, '0')
  const serialData = (n: number) => { const d = new Date(Math.round((n - 25569) * 86400000)); return `${p2(d.getUTCDate())}/${p2(d.getUTCMonth() + 1)}/${d.getUTCFullYear()}` }
  const serialHora = (n: number) => { const s = Math.round((n - Math.floor(n)) * 86400); return `${p2(Math.floor(s / 3600))}:${p2(Math.floor((s % 3600) / 60))}:${p2(s % 60)}` }
  const dataHora = (dc: any, hc: any): string => {
    if (typeof dc === 'number') return serialData(dc) + (typeof hc === 'number' ? ' ' + serialHora(hc) : ' 00:00:00')
    if (dc instanceof Date) return `${p2(dc.getDate())}/${p2(dc.getMonth() + 1)}/${dc.getFullYear()} ${p2(dc.getHours())}:${p2(dc.getMinutes())}:${p2(dc.getSeconds())}`
    return String(dc ?? '').trim()
  }
  const valorStr = (cell: any): string => {
    let n: number
    if (typeof cell === 'number') n = cell
    else n = parseFloat(String(cell ?? '').replace(/[^\d.,-]/g, '').replace(/\./g, '').replace(',', '.'))
    return (isFinite(n) ? n : 0).toFixed(2).replace('.', ',')
  }
  const limpa = (s: any) => String(s ?? '').replace(/[;\r\n]/g, ' ').trim()

  const out: string[] = ['Data e hora;Meio - Meio;Status;Valor (R$);Taxa Aplicada - Valor(R$);Origem - Nome']
  for (let k = h + 1; k < rows.length; k++) {
    const r = rows[k]; if (!r || r[iData] === '' || r[iData] == null) continue
    const st = iStatus >= 0 ? norm(r[iStatus]) : 'aprovada'
    if (!(st.startsWith('aprovad') || st === 'pago' || st === 'complete')) continue
    const dh = dataHora(r[iData], iHora >= 0 ? r[iHora] : null); if (!dh) continue
    out.push([dh, limpa(r[iMeio]), 'Aprovada', valorStr(r[iValor]),
      iTaxa >= 0 ? valorStr(r[iTaxa]) : '0,00', iNome >= 0 ? limpa(r[iNome]) : ''].join(';'))
  }
  if (out.length <= 1) return file
  return new File([new Blob([out.join('\n')], { type: 'text/csv' })], 'conciliacao.csv', { type: 'text/csv' })
}

async function importar() {
  const original = Array.isArray(arquivo.value) ? arquivo.value[0] : arquivo.value
  if (!original) return
  carregando.value = true
  try {
    const file = await normalizarArquivo(original)
    const fd = new FormData()
    fd.append('empresaId', auth.empresaId)
    fd.append('arquivo', file)
    if (loja.value) fd.append('localEstoqueId', loja.value)
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
