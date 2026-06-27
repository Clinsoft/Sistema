<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Editor de Etiquetas</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-printer" @click="imprimir" :disabled="!produtosSel.length">
          Imprimir ({{ produtosSel.length }})
        </v-btn>
      </v-col>
    </v-row>

    <v-row>
      <!-- Painel esquerdo: configuração -->
      <v-col cols="12" md="4">
        <v-card rounded="xl" elevation="1" class="mb-4 pa-4">
          <div class="text-body-2 font-weight-bold mb-3">Template</div>
          <v-btn-toggle v-model="template" mandatory divided class="mb-4 flex-wrap" density="compact">
            <v-btn v-for="t in templates" :key="t.id" :value="t.id" size="small">{{ t.nome }}</v-btn>
          </v-btn-toggle>

          <div class="text-body-2 font-weight-bold mb-2">Campos visíveis</div>
          <v-checkbox v-model="campos.nome" label="Nome do produto" density="compact" hide-details />
          <v-checkbox v-model="campos.preco" label="Preço de venda" density="compact" hide-details />
          <v-checkbox v-model="campos.precoPor" label="Preço Promocional" density="compact" hide-details />
          <v-checkbox v-model="campos.codBarras" label="Código de barras (EAN)" density="compact" hide-details />
          <v-checkbox v-model="campos.validade" label="Validade" density="compact" hide-details />
          <v-checkbox v-model="campos.lote" label="Lote" density="compact" hide-details />
          <v-checkbox v-model="campos.ncm" label="NCM" density="compact" hide-details />

          <v-divider class="my-3" />
          <v-text-field v-model.number="qtdEtiquetas" label="Qtd por produto" type="number"
            variant="outlined" density="compact" :min="1" :max="100" />
          <v-text-field v-model="validade" label="Validade (p/ todos)" type="date"
            variant="outlined" density="compact" class="mt-2" />
          <v-text-field v-model="lote" label="Lote (p/ todos)" variant="outlined" density="compact" />
          <v-text-field v-model.number="precoPromo" label="Preço Promocional (R$)"
            type="number" variant="outlined" density="compact" prefix="R$" />
        </v-card>

        <!-- Busca de produtos -->
        <v-card rounded="xl" elevation="1" class="pa-4">
          <div class="text-body-2 font-weight-bold mb-2">Produtos Selecionados</div>
          <v-autocomplete v-model="buscaTemp" :items="sugestoes" item-title="descricao" item-value="id"
            label="Buscar produto..." variant="outlined" density="compact"
            :loading="buscando" clearable no-filter
            @update:search="buscarProdutos" @update:model-value="adicionarProduto" />
          <div v-if="!produtosSel.length" class="text-caption text-medium-emphasis mt-2">
            Nenhum produto adicionado.
          </div>
          <v-chip v-for="p in produtosSel" :key="p.id" closable class="ma-1" size="small"
            @click:close="removerProduto(p.id)">
            {{ p.descricao }}
          </v-chip>
        </v-card>
      </v-col>

      <!-- Painel direito: preview -->
      <v-col cols="12" md="8">
        <v-card rounded="xl" elevation="1" class="pa-4">
          <div class="text-body-2 font-weight-bold mb-3">Pré-visualização</div>
          <div v-if="!produtosSel.length" class="text-center py-8 text-medium-emphasis">
            <v-icon icon="mdi-tag-outline" size="60" class="mb-2" />
            <div>Selecione produtos para visualizar as etiquetas</div>
          </div>

          <div id="area-impressao" class="etiquetas-grid" :class="`template-${template}`">
            <div v-for="p in etiquetasExpandidas" :key="p._key" class="etiqueta"
              :style="tplAtual.style">
              <div v-if="campos.nome" class="etq-nome" :style="tplAtual.nomeStyle">
                {{ p.descricao }}
              </div>
              <div v-if="campos.precoPor && precoPromo" class="etq-de">
                DE R$ {{ fmt(p.precoVenda) }}
              </div>
              <div v-if="campos.preco" class="etq-preco" :style="tplAtual.precoStyle">
                R$ {{ fmt(precoPromo || p.precoVenda) }}
              </div>
              <div v-if="campos.codBarras && p.codigoBarras" class="etq-ean">
                ||||||||||||| {{ p.codigoBarras }}
              </div>
              <div class="etq-rodape">
                <span v-if="campos.ncm && p.ncm">NCM: {{ p.ncm }}</span>
                <span v-if="campos.lote && lote"> Lote: {{ lote }}</span>
                <span v-if="campos.validade && validade"> Val: {{ fmtData(validade) }}</span>
              </div>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const buscaTemp = ref<string | null>(null)
const buscando = ref(false)
const sugestoes = ref<any[]>([])
const produtosSel = ref<any[]>([])
const template = ref('40x25')
const qtdEtiquetas = ref(1)
const validade = ref('')
const lote = ref('')
const precoPromo = ref<number | null>(null)

const campos = ref({
  nome: true, preco: true, precoPor: false,
  codBarras: true, validade: false, lote: false, ncm: false,
})

const templates = [
  { id: '40x25', nome: '40×25mm' },
  { id: '50x30', nome: '50×30mm' },
  { id: '100x50', nome: '100×50mm' },
  { id: 'grande', nome: 'Grande' },
]

const tplConfig: Record<string, any> = {
  '40x25': {
    style: 'width:150px;height:94px;font-size:8px;padding:4px',
    nomeStyle: 'font-size:8px;font-weight:bold;line-height:1.1',
    precoStyle: 'font-size:16px;font-weight:bold',
  },
  '50x30': {
    style: 'width:189px;height:113px;font-size:9px;padding:5px',
    nomeStyle: 'font-size:9px;font-weight:bold',
    precoStyle: 'font-size:20px;font-weight:bold',
  },
  '100x50': {
    style: 'width:378px;height:189px;font-size:11px;padding:8px',
    nomeStyle: 'font-size:12px;font-weight:bold',
    precoStyle: 'font-size:28px;font-weight:bold',
  },
  'grande': {
    style: 'width:283px;height:170px;font-size:10px;padding:8px',
    nomeStyle: 'font-size:11px;font-weight:bold',
    precoStyle: 'font-size:24px;font-weight:bold',
  },
}

const tplAtual = computed(() => tplConfig[template.value] ?? tplConfig['40x25'])

const etiquetasExpandidas = computed(() => {
  const lista: any[] = []
  produtosSel.value.forEach(p => {
    for (let i = 0; i < qtdEtiquetas.value; i++)
      lista.push({ ...p, _key: `${p.id}_${i}` })
  })
  return lista
})

function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }
function fmtData(d: string) { if (!d) return ''; const [y,m,dd] = d.split('-'); return `${dd}/${m}/${y}` }

let timer: any
async function buscarProdutos(q: string) {
  if (!q || q.length < 2) { sugestoes.value = []; return }
  clearTimeout(timer)
  timer = setTimeout(async () => {
    buscando.value = true
    try {
      const r = await api.get('/produtos/buscar', { params: { empresaId: auth.empresaId, q } })
      sugestoes.value = r.data ?? []
    } finally { buscando.value = false }
  }, 300)
}

function adicionarProduto(id: string | null) {
  if (!id) return
  const p = sugestoes.value.find(s => s.id === id)
  if (p && !produtosSel.value.find(x => x.id === id))
    produtosSel.value.push(p)
  buscaTemp.value = null
}

function removerProduto(id: string) {
  produtosSel.value = produtosSel.value.filter(p => p.id !== id)
}

function imprimir() {
  const area = document.getElementById('area-impressao')
  if (!area) return
  const w = window.open('', '_blank')!
  w.document.write(`
    <html><head><title>Etiquetas</title>
    <style>
      body { margin: 0; }
      .etiquetas-grid { display: flex; flex-wrap: wrap; gap: 2mm; padding: 5mm; }
      .etiqueta {
        border: 1px solid #999; display: flex; flex-direction: column;
        justify-content: space-between; box-sizing: border-box; overflow: hidden;
        page-break-inside: avoid;
      }
      .etq-nome { font-weight: bold; word-break: break-word; }
      .etq-de { text-decoration: line-through; font-size: 0.8em; color: #888; }
      .etq-preco { font-weight: bold; color: #1a1a1a; }
      .etq-ean { font-family: monospace; font-size: 0.75em; letter-spacing: 1px; }
      .etq-rodape { font-size: 0.7em; color: #555; display: flex; gap: 4px; flex-wrap: wrap; }
      @media print { @page { margin: 5mm; } }
    </style></head><body>
    ${area.outerHTML}
    <script>window.onload=()=>{ window.print(); window.close(); }<\/script>
    </body></html>
  `)
  w.document.close()
}
</script>

<style scoped>
.etiquetas-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  background: #f0f0f0;
  padding: 12px;
  border-radius: 8px;
  min-height: 200px;
}
.etiqueta {
  border: 1px solid #ccc;
  background: white;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  box-sizing: border-box;
  overflow: hidden;
  cursor: default;
  transition: box-shadow .2s;
}
.etiqueta:hover { box-shadow: 0 2px 8px rgba(0,0,0,.15); }
.etq-preco { color: #1565C0; }
.etq-de { text-decoration: line-through; font-size: .75em; color: #888; }
.etq-ean { font-family: monospace; font-size: .7em; letter-spacing: 2px; }
.etq-rodape { font-size: .68em; color: #666; display: flex; gap: 4px; flex-wrap: wrap; }
</style>
