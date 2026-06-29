<template>
  <div>
    <div class="d-flex align-center mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" class="mr-2" @click="$router.push('/compras')" />
      <div class="text-h6 font-weight-bold flex-grow-1">Comparador de Cotações</div>
      <template v-if="resultado">
        <v-btn variant="tonal" prepend-icon="mdi-printer" class="mr-2"
          @click="imprimirRelatorio">
          Imprimir
        </v-btn>
        <v-btn color="primary" prepend-icon="mdi-cart-plus"
          @click="criarPedidoMelhores">
          Criar Pedido com Melhores Preços
        </v-btn>
      </template>
    </div>

    <!-- Upload dos PDFs -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-4">
      <div class="text-body-2 font-weight-bold mb-3">
        <v-icon icon="mdi-file-pdf-box" color="error" class="mr-1" />
        Envie até 3 orçamentos em PDF (de fornecedores diferentes)
      </div>
      <v-row>
        <v-col v-for="n in 3" :key="n" cols="12" md="4">
          <div class="cotacao-drop-zone"
            :class="{ 'cotacao-drop-zone--ativo': pdfs[n-1] }"
            @dragover.prevent @drop.prevent="onDrop($event, n-1)"
            @click="triggerInput(n-1)">
            <input type="file" accept=".pdf" :ref="el => inputs[n-1] = el as HTMLInputElement"
              style="display:none" @change="onFileChange($event, n-1)" />
            <template v-if="pdfs[n-1]">
              <v-icon icon="mdi-file-check" color="success" size="32" class="mb-2" />
              <div class="text-body-2 font-weight-bold text-center" style="word-break:break-all">
                {{ pdfs[n-1]!.name }}
              </div>
              <div class="text-caption text-medium-emphasis">{{ fmtTamanho(pdfs[n-1]!.size) }}</div>
              <v-text-field v-model="nomes[n-1]" :label="`Nome do Fornecedor ${n}`"
                variant="outlined" density="compact" class="mt-2" hide-details
                @click.stop />
              <v-btn icon="mdi-close" size="x-small" variant="text" color="error"
                class="mt-1" @click.stop="removerPdf(n-1)" />
            </template>
            <template v-else>
              <v-icon icon="mdi-upload" size="32" color="primary" class="mb-2" />
              <div class="text-body-2 font-weight-bold">Fornecedor {{ n }}</div>
              <div class="text-caption text-medium-emphasis">Clique ou arraste o PDF</div>
            </template>
          </div>
        </v-col>
      </v-row>
      <div class="d-flex justify-end mt-4">
        <v-btn color="success" size="large" :loading="processando"
          :disabled="!temPdf" prepend-icon="mdi-magnify"
          @click="comparar">
          Comparar Cotações
        </v-btn>
      </div>
    </v-card>

    <!-- Resultado -->
    <template v-if="resultado">
      <!-- Resumo -->
      <v-row class="mb-4">
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-4 text-center">
            <div class="text-h5 font-weight-bold text-primary">{{ resultado.totalProdutos }}</div>
            <div class="text-caption">produtos identificados</div>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-4 text-center">
            <div class="text-h5 font-weight-bold text-success">R$ {{ fmt(economiaTotalPossivel) }}</div>
            <div class="text-caption">economia possível</div>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-4 text-center">
            <div class="text-h5 font-weight-bold text-warning">{{ resultado.totalNaoIdentificados }}</div>
            <div class="text-caption">itens não identificados</div>
          </v-card>
        </v-col>
        <v-col cols="6" md="3">
          <v-card rounded="xl" elevation="1" class="pa-4 text-center">
            <div class="text-h5 font-weight-bold">{{ resultado.fornecedores.length }}</div>
            <div class="text-caption">fornecedores comparados</div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Filtros -->
      <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
        <v-row dense align="center">
          <v-col cols="12" md="4">
            <v-text-field v-model="filtroTexto" label="Buscar produto..."
              variant="outlined" density="compact" hide-details
              prepend-inner-icon="mdi-magnify" clearable />
          </v-col>
          <v-col cols="auto">
            <v-btn-toggle v-model="filtroModo" mandatory density="compact" divided>
              <v-btn value="todos" size="small">Todos</v-btn>
              <v-btn value="economia" size="small" color="success">Com economia</v-btn>
              <v-btn value="sem-match" size="small" color="warning">Não identificados</v-btn>
            </v-btn-toggle>
          </v-col>
          <v-spacer />
          <v-col cols="auto">
            <v-btn size="small" variant="tonal" prepend-icon="mdi-download"
              @click="exportarCsv">Exportar CSV</v-btn>
          </v-col>
        </v-row>
      </v-card>

      <!-- Tabela de comparação -->
      <v-card rounded="xl" elevation="1" class="mb-4">
        <v-table density="comfortable" hover>
          <thead>
            <tr>
              <th style="min-width:220px">Produto</th>
              <th class="text-right" style="width:110px">Custo Atual</th>
              <th v-for="forn in resultado.fornecedores" :key="forn"
                class="text-right" style="width:130px">
                <v-chip size="small" color="primary" variant="tonal">{{ forn }}</v-chip>
              </th>
              <th class="text-right" style="width:110px">Menor Preço</th>
              <th class="text-right" style="width:110px">Economia</th>
              <th style="width:50px"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in produtosFiltrados" :key="p.produtoId"
              :class="{ 'cot-row--economia': p.economia > 0 }">
              <td>
                <div class="text-body-2 font-weight-bold">{{ p.descricao }}</div>
              </td>
              <td class="text-right text-medium-emphasis">
                {{ p.custoAtual > 0 ? 'R$ ' + fmt(p.custoAtual) : '—' }}
              </td>
              <td v-for="(cot, idx) in p.cotacoes" :key="idx" class="text-right">
                <span v-if="cot.preco != null"
                  :class="cot.melhor ? 'cot-melhor' : 'text-medium-emphasis'">
                  <v-icon v-if="cot.melhor" icon="mdi-trophy" size="13" color="success" />
                  R$ {{ fmt(cot.preco) }}
                  <span v-if="cot.unidade" class="text-caption">/{{ cot.unidade }}</span>
                </span>
                <span v-else class="text-medium-emphasis">—</span>
              </td>
              <!-- Preenche colunas vazias se há menos de 3 fornecedores -->
              <td v-for="n in 3 - resultado.fornecedores.length" :key="'vazio-' + n"
                class="text-right text-medium-emphasis">—</td>
              <td class="text-right font-weight-bold text-success">
                R$ {{ fmt(p.menorPreco) }}
              </td>
              <td class="text-right">
                <span v-if="p.economia > 0" class="text-success text-body-2">
                  -R$ {{ fmt(p.economia) }}
                </span>
                <span v-else class="text-medium-emphasis text-caption">sem dados</span>
              </td>
              <td>
                <v-tooltip text="Selecionar melhor preço" location="left">
                  <template #activator="{ props }">
                    <v-checkbox v-bind="props" v-model="selecionados" :value="p.produtoId"
                      density="compact" hide-details color="primary" />
                  </template>
                </v-tooltip>
              </td>
            </tr>
            <tr v-if="!produtosFiltrados.length">
              <td :colspan="5 + resultado.fornecedores.length" class="text-center pa-6 text-medium-emphasis">
                Nenhum produto encontrado
              </td>
            </tr>
          </tbody>
        </v-table>
      </v-card>

      <!-- Itens não identificados -->
      <v-expansion-panels v-if="filtroModo === 'sem-match' || resultado.naoIdentificados.length">
        <v-expansion-panel rounded="xl">
          <v-expansion-panel-title>
            <v-icon icon="mdi-help-circle-outline" color="warning" class="mr-2" />
            {{ resultado.naoIdentificados.length }} itens dos PDFs não encontrados no cadastro de produtos
          </v-expansion-panel-title>
          <v-expansion-panel-text>
            <v-table density="compact">
              <thead>
                <tr>
                  <th>Fornecedor</th>
                  <th>Descrição no PDF</th>
                  <th class="text-right">Preço</th>
                  <th>Unidade</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(item, i) in resultado.naoIdentificados" :key="i">
                  <td><v-chip size="small" variant="tonal">{{ item.fornecedor }}</v-chip></td>
                  <td>{{ item.descricao }}</td>
                  <td class="text-right">R$ {{ fmt(item.preco) }}</td>
                  <td>{{ item.unidade ?? '—' }}</td>
                </tr>
              </tbody>
            </v-table>
            <v-alert type="info" variant="tonal" density="compact" class="mt-3">
              Esses itens não foram vinculados a nenhum produto do cadastro. Confira se a descrição
              no PDF é similar ao nome cadastrado no sistema.
            </v-alert>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>
    </template>

    <!-- Dialog: criar pedido -->
    <v-dialog v-model="dialogPedido" max-width="700" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4">Criar Pedido com Melhores Preços</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-alert type="info" variant="tonal" density="compact" class="mb-4">
            Serão criados pedidos separados por fornecedor, com os itens de menor preço de cada um.
          </v-alert>
          <v-row dense>
            <v-col cols="12" md="6">
              <v-autocomplete v-model="pedidoFornMap[forn]" v-for="forn in fornecedoresNoPedido" :key="forn"
                :label="`Fornecedor cadastrado para '${forn}'`"
                :items="forns" item-title="razaoSocial" item-value="id"
                variant="outlined" density="compact" class="mb-2"
                @update:search="buscarForns" />
            </v-col>
          </v-row>
          <v-table density="compact" class="mt-2">
            <thead><tr><th>Produto</th><th>Fornecedor</th><th class="text-right">Preço</th></tr></thead>
            <tbody>
              <tr v-for="item in itensPedido" :key="item.produtoId">
                <td>{{ item.descricao }}</td>
                <td><v-chip size="x-small" variant="tonal">{{ item.fornecedor }}</v-chip></td>
                <td class="text-right text-success font-weight-bold">R$ {{ fmt(item.preco) }}</td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-card-actions class="pa-4 justify-end">
          <v-btn variant="text" @click="dialogPedido = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvando" @click="confirmarPedido">Criar Pedidos</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const pdfs = ref<(File | null)[]>([null, null, null])
const nomes = ref(['Fornecedor 1', 'Fornecedor 2', 'Fornecedor 3'])
const inputs = ref<HTMLInputElement[]>([])
const processando = ref(false)
const resultado = ref<any>(null)
const filtroTexto = ref('')
const filtroModo = ref('todos')
const selecionados = ref<string[]>([])
const dialogPedido = ref(false)
const salvando = ref(false)
const forns = ref<any[]>([])
const pedidoFornMap = ref<Record<string, string>>({})

const temPdf = computed(() => pdfs.value.some(p => p !== null))

const produtosFiltrados = computed(() => {
  if (!resultado.value) return []
  let lista: any[] = resultado.value.produtos
  if (filtroTexto.value) {
    const q = filtroTexto.value.toLowerCase()
    lista = lista.filter((p: any) => p.descricao.toLowerCase().includes(q))
  }
  if (filtroModo.value === 'economia') lista = lista.filter((p: any) => p.economia > 0)
  return lista
})

const economiaTotalPossivel = computed(() => {
  if (!resultado.value) return 0
  return resultado.value.produtos.reduce((s: number, p: any) => {
    if (p.custoAtual > 0 && p.menorPreco < p.custoAtual) s += p.custoAtual - p.menorPreco
    return s
  }, 0)
})

const itensPedido = computed(() => {
  if (!resultado.value) return []
  const sel = selecionados.value.length > 0
    ? resultado.value.produtos.filter((p: any) => selecionados.value.includes(p.produtoId))
    : resultado.value.produtos

  return sel.map((p: any) => {
    const melhor = p.cotacoes.find((c: any) => c.melhor)
    return { produtoId: p.produtoId, descricao: p.descricao, fornecedor: melhor?.fornecedor ?? '?', preco: p.menorPreco }
  })
})

const fornecedoresNoPedido = computed(() => {
  const set = new Set(itensPedido.value.map((i: any) => i.fornecedor))
  return [...set]
})

function triggerInput(idx: number) {
  inputs.value[idx]?.click()
}

function onFileChange(ev: Event, idx: number) {
  const file = (ev.target as HTMLInputElement).files?.[0]
  if (file) {
    pdfs.value[idx] = file
    if (nomes.value[idx] === `Fornecedor ${idx + 1}`)
      nomes.value[idx] = file.name.replace('.pdf', '').substring(0, 30)
  }
}

function onDrop(ev: DragEvent, idx: number) {
  const file = ev.dataTransfer?.files[0]
  if (file?.type === 'application/pdf') {
    pdfs.value[idx] = file
    if (nomes.value[idx] === `Fornecedor ${idx + 1}`)
      nomes.value[idx] = file.name.replace('.pdf', '').substring(0, 30)
  }
}

function removerPdf(idx: number) {
  pdfs.value[idx] = null
  nomes.value[idx] = `Fornecedor ${idx + 1}`
  resultado.value = null
}

function fmtTamanho(bytes: number) {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB'
}

function fmt(v: number) {
  return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
}

async function comparar() {
  processando.value = true
  resultado.value = null
  selecionados.value = []
  try {
    const fd = new FormData()
    fd.append('empresaId', auth.empresaId!)
    pdfs.value.forEach((f, i) => {
      if (f) {
        fd.append(`pdf${i + 1}`, f)
        fd.append(`nome${i + 1}`, nomes.value[i])
      }
    })
    const r = await api.post('/cotacoes/comparar', fd, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    resultado.value = r.data
    if (!resultado.value.produtos.length)
      notif.aviso('Nenhum produto do cadastro foi encontrado nos PDFs. Verifique se as descrições coincidem.')
    else
      notif.ok(`${resultado.value.totalProdutos} produtos comparados com sucesso!`)
  } catch (e: any) {
    notif.erro('Erro ao processar os PDFs. Verifique se os arquivos são válidos.')
  } finally {
    processando.value = false
  }
}

function imprimirRelatorio() {
  if (!resultado.value) return
  const data = new Date().toLocaleDateString('pt-BR')
  const fornecedores: string[] = resultado.value.fornecedores

  const headerCols = fornecedores.map((f: string) => `<th>${f}</th>`).join('')
  const vazios = 3 - fornecedores.length

  const linhas = resultado.value.produtos.map((p: any) => {
    const cotCols = p.cotacoes.map((c: any) => {
      if (c.preco == null) return '<td class="nd">—</td>'
      const cls = c.melhor ? 'melhor' : ''
      const trofeu = c.melhor ? '🏆 ' : ''
      return `<td class="${cls}">${trofeu}R$ ${fmt(c.preco)}${c.unidade ? `<span class="un">/${c.unidade}</span>` : ''}</td>`
    }).join('')
    const vaziosCols = Array(vazios).fill('<td class="nd">—</td>').join('')
    const econStr = p.economia > 0
      ? `<span class="eco">-R$ ${fmt(p.economia)}</span>`
      : '<span class="nd">—</span>'
    const custoStr = p.custoAtual > 0 ? `R$ ${fmt(p.custoAtual)}` : '—'
    return `
      <tr>
        <td class="nome">${p.descricao}</td>
        <td class="num">${custoStr}</td>
        ${cotCols}${vaziosCols}
        <td class="num menor">R$ ${fmt(p.menorPreco)}</td>
        <td class="num">${econStr}</td>
      </tr>`
  }).join('')

  const html = `<!DOCTYPE html>
<html lang="pt-BR">
<head>
  <meta charset="UTF-8">
  <title>Comparativo de Cotações — ${data}</title>
  <style>
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: Arial, sans-serif; font-size: 11px; color: #1a1a1a; padding: 20px; }
    h1 { font-size: 16px; font-weight: bold; margin-bottom: 2px; }
    .sub { font-size: 11px; color: #666; margin-bottom: 16px; }

    .resumo { display: flex; gap: 20px; margin-bottom: 16px; }
    .resumo-card { border: 1px solid #e2e8f0; border-radius: 6px; padding: 8px 14px; text-align: center; }
    .resumo-card .val { font-size: 18px; font-weight: bold; color: #1d4ed8; }
    .resumo-card .lab { font-size: 9px; color: #64748b; }
    .resumo-card.eco .val { color: #16a34a; }

    table { width: 100%; border-collapse: collapse; }
    th { background: #1e3a5f; color: white; padding: 7px 8px; text-align: left; font-size: 10px; }
    th.num { text-align: right; }
    td { padding: 5px 8px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    td.num { text-align: right; }
    td.nd { text-align: center; color: #94a3b8; }
    td.nome { font-weight: 600; max-width: 220px; }
    td.melhor { color: #16a34a; font-weight: bold; text-align: right; }
    td.menor { color: #1d4ed8; font-weight: bold; }
    span.eco { color: #16a34a; font-weight: bold; }
    span.nd { color: #94a3b8; }
    span.un { font-size: 9px; color: #64748b; }
    tr:nth-child(even) { background: #f8fafc; }
    tr:hover { background: #eff6ff; }

    .rodape { margin-top: 14px; font-size: 9px; color: #94a3b8; text-align: right; }

    @media print {
      @page { margin: 12mm; size: landscape; }
      body { padding: 0; }
      tr:hover { background: inherit; }
    }
  </style>
</head>
<body>
  <h1>Comparativo de Cotações</h1>
  <div class="sub">Gerado em ${data} — ${fornecedores.join(' · ')}</div>

  <div class="resumo">
    <div class="resumo-card">
      <div class="val">${resultado.value.totalProdutos}</div>
      <div class="lab">produtos comparados</div>
    </div>
    <div class="resumo-card eco">
      <div class="val">R$ ${fmt(economiaTotalPossivel.value)}</div>
      <div class="lab">economia possível</div>
    </div>
    <div class="resumo-card">
      <div class="val">${resultado.value.totalNaoIdentificados}</div>
      <div class="lab">itens não identificados</div>
    </div>
  </div>

  <table>
    <thead>
      <tr>
        <th>Produto</th>
        <th class="num">Custo Atual</th>
        ${headerCols}
        ${Array(vazios).fill('<th>—</th>').join('')}
        <th class="num">Menor Preço</th>
        <th class="num">Economia</th>
      </tr>
    </thead>
    <tbody>${linhas}</tbody>
  </table>

  <div class="rodape">EcoGranel — Relatório gerado automaticamente</div>
  <script>window.onload = () => { window.print() }<\/script>
</body>
</html>`

  const w = window.open('', '_blank')!
  w.document.write(html)
  w.document.close()
}

function exportarCsv() {
  if (!resultado.value) return
  const colunas = ['Produto', 'Custo Atual', ...resultado.value.fornecedores, 'Menor Preço', 'Economia']
  const linhas = resultado.value.produtos.map((p: any) =>
    [p.descricao, p.custoAtual, ...p.cotacoes.map((c: any) => c.preco ?? ''), p.menorPreco, p.economia ?? ''].join(';')
  )
  const csv = [colunas.join(';'), ...linhas].join('\n')
  const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `cotacoes_${new Date().toISOString().slice(0, 10)}.csv`
  a.click()
  URL.revokeObjectURL(url)
}

async function buscarForns(q: string) {
  if (!q || q.length < 2) return
  const r = await api.get('/fornecedores', { params: { empresaId: auth.empresaId, q } })
  forns.value = r.data
}

async function criarPedidoMelhores() {
  await buscarForns('a')
  dialogPedido.value = true
}

async function confirmarPedido() {
  salvando.value = true
  try {
    // Agrupa por fornecedor e cria um pedido por fornecedor
    const porFornecedor = Map.groupBy(itensPedido.value, (i: any) => i.fornecedor)
    for (const [forn, itens] of porFornecedor.entries()) {
      const fornecedorId = pedidoFornMap.value[forn] ?? null
      await api.post('/pedidos-compra', {
        empresaId: auth.empresaId,
        fornecedorId,
        observacoes: `Gerado automaticamente pela comparação de cotações — ${forn}`,
        itens: (itens as any[]).map(i => ({
          produtoId: i.produtoId,
          descricao: i.descricao,
          quantidade: 1,
          precoUnitario: i.preco,
        }))
      })
    }
    notif.ok('Pedidos criados com sucesso!')
    dialogPedido.value = false
  } catch {
    notif.erro('Erro ao criar os pedidos.')
  } finally {
    salvando.value = false
  }
}
</script>

<style scoped>
.cotacao-drop-zone {
  border: 2px dashed #cbd5e1;
  border-radius: 12px;
  min-height: 140px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 16px;
  cursor: pointer;
  transition: all 0.2s;
  background: #f8fafc;
}
.cotacao-drop-zone:hover {
  border-color: #3b82f6;
  background: #eff6ff;
}
.cotacao-drop-zone--ativo {
  border-color: #10b981;
  background: #f0fdf4;
}
.cot-row--economia {
  background: rgba(16, 185, 129, 0.03);
}
.cot-melhor {
  color: #16a34a;
  font-weight: 700;
}
</style>
