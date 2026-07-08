<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Editor de Etiquetas</h2></v-col>
      <v-col cols="auto" class="d-flex gap-2">
        <template v-if="isGondola">
          <v-btn color="success" prepend-icon="mdi-download" @click="baixarZpl"
            :disabled="!produtosSel.length">
            Gerar ZPL ({{ produtosSel.length }})
          </v-btn>
          <v-btn color="secondary" variant="outlined" prepend-icon="mdi-usb"
            @click="enviarZebraDialog = true" :disabled="!produtosSel.length">
            Enviar para Zebra
          </v-btn>
        </template>
        <v-btn v-else color="primary" prepend-icon="mdi-printer" @click="imprimir"
          :disabled="!produtosSel.length">
          Imprimir ({{ produtosSel.length }})
        </v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="etiquetas"
      titulo="Como usar o Editor de Etiquetas"
      :passos="[
        '<b>1. Escolha o Template</b> (à esquerda): <b>EcoGranel</b> (etiqueta de preço 10×10cm com QR Code), <b>Gôndola Zebra</b> (impressora térmica ZPL), <b>Pote 9×9cm</b> ou os tamanhos padrão (40×25, 50×30, 100×50mm).',
        '<b>2. Ajuste os campos visíveis</b> (marque/desmarque nome, preço, código de barras, validade, PLU, etc.) e as opções do template — cor da borda, marca d\'água, URL do QR Code, texto descritivo.',
        '<b>3. Busque e adicione os produtos</b> no campo <b>Buscar produto</b>. Eles viram chips — clique no <b>×</b> do chip para remover. Defina a <b>Qtd por produto</b> e a <b>Validade</b>.',
        '<b>4. Confira a pré-visualização</b> à direita e <b>imprima</b> (templates comuns/EcoGranel/Pote) ou <b>Gere o ZPL</b> / envie para a <b>Zebra</b> (template Gôndola). Nada é salvo no banco — a etiqueta é gerada na hora a partir dos dados do produto.',
      ]"
    />

    <v-row>
      <!-- Painel esquerdo: configuração -->
      <v-col cols="12" md="4">
        <v-card rounded="xl" elevation="1" class="mb-4 pa-4">
          <div class="text-body-2 font-weight-bold mb-3">Template</div>
          <v-btn-toggle v-model="template" mandatory divided class="mb-4 flex-wrap" density="compact">
            <v-btn v-for="t in templates" :key="t.id" :value="t.id" size="small">{{ t.nome }}</v-btn>
          </v-btn-toggle>

          <!-- ── Gôndola Zebra ── -->
          <template v-if="isGondola">
            <div class="text-body-2 font-weight-bold mb-2">Tamanho</div>
            <v-btn-toggle v-model="gondolaTamanho" mandatory density="compact" class="mb-3 flex-wrap">
              <v-btn v-for="s in gondolaTamanhos" :key="s.id" :value="s.id" size="small">{{ s.nome }}</v-btn>
            </v-btn-toggle>

            <div class="text-body-2 font-weight-bold mb-2">Impressora Zebra</div>
            <v-text-field v-model.number="zebraDpi" label="DPI da impressora"
              type="number" variant="outlined" density="compact" class="mb-1"
              hint="203 = padrão, 300 = alta resolução" persistent-hint />

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Campos visíveis</div>
            <v-checkbox v-model="camposGondola.nome" label="Nome do produto" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.preco" label="Preço de venda" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.precoKg" label="Preço por kg / unidade" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.codBarras" label="Código de barras (EAN-13)" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.codigoPlu" label="Código PLU" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.validade" label="Validade" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.unidade" label="Unidade de medida" density="compact" hide-details />
          </template>

          <!-- ── EcoGranel (etiqueta de preço com QR Code) ── -->
          <template v-else-if="template === 'ecogranel'">
            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Cor da Borda</div>
            <v-row dense>
              <v-col cols="7">
                <v-text-field v-model="borda.cor" label="Cor" variant="outlined"
                  density="compact" type="color" hide-details />
              </v-col>
              <v-col cols="5">
                <v-text-field v-model.number="borda.espessura" label="Espessura (px)"
                  type="number" variant="outlined" density="compact" :min="2" :max="20" hide-details />
              </v-col>
            </v-row>

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Marca d'água</div>
            <div class="text-caption text-medium-emphasis mb-2">
              Imagem exibida como fundo (logo, semente, produto, etc.)
            </div>
            <v-btn variant="outlined" size="small" prepend-icon="mdi-image-plus"
              @click="inputMarcaDagua?.click()" class="mb-2">
              {{ marcaDaguaUrl ? 'Trocar imagem' : 'Carregar imagem' }}
            </v-btn>
            <input ref="inputMarcaDagua" type="file" accept="image/*" class="d-none"
              @change="carregarMarcaDagua" />
            <div v-if="marcaDaguaUrl" class="d-flex align-center gap-2 mt-1">
              <img :src="marcaDaguaUrl" style="height:40px;border-radius:4px;opacity:.6" />
              <v-btn icon="mdi-close" size="x-small" variant="text" @click="marcaDaguaUrl=''" />
            </div>

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Campos visíveis</div>
            <v-checkbox v-model="camposEco.nome" label="Nome do produto" density="compact" hide-details />
            <v-checkbox v-model="camposEco.codigoPlu" label="Código PLU (após nome)" density="compact" hide-details />
            <v-checkbox v-model="camposEco.preco100g" label="Preço por 100g" density="compact" hide-details />
            <v-checkbox v-model="camposEco.validade" label="Validade" density="compact" hide-details />
            <v-checkbox v-model="camposEco.descricao" label="Texto descritivo" density="compact" hide-details />
            <v-checkbox v-model="camposEco.qrcode" label="QR Code" density="compact" hide-details />
            <v-checkbox v-model="camposEco.frase" label='Frase "Natural como deve ser!"' density="compact" hide-details />

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Texto descritivo (padrão)</div>
            <v-textarea v-model="textoDescritivoEco" variant="outlined" density="compact"
              rows="3" auto-grow hint="Usado quando o produto não tem descrição complementar" persistent-hint />

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">URL base do QR Code</div>
            <v-text-field v-model="qrBaseUrl" variant="outlined" density="compact"
              placeholder="https://ecogranel.com.br/produtos/produto.php?p="
              hint="O slug do produto será adicionado automaticamente" persistent-hint />
          </template>

          <!-- ── Pote 9×9cm ── -->
          <template v-else-if="template === 'pote9x9'">
            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Borda</div>
            <v-row dense>
              <v-col cols="7">
                <v-text-field v-model="borda.cor" label="Cor da borda" variant="outlined"
                  density="compact" type="color" hide-details />
              </v-col>
              <v-col cols="5">
                <v-text-field v-model.number="borda.espessura" label="Espessura (px)"
                  type="number" variant="outlined" density="compact" :min="1" :max="20" hide-details />
              </v-col>
            </v-row>
            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Campos visíveis</div>
            <v-checkbox v-model="camposPote.nome" label="Nome do produto" density="compact" hide-details />
            <v-checkbox v-model="camposPote.descricao" label="Descrição complementar" density="compact" hide-details />
            <v-checkbox v-model="camposPote.codigoPlu" label="Código PLU (balança)" density="compact" hide-details />
            <v-checkbox v-model="camposPote.preco100g" label="Preço por 100g" density="compact" hide-details />
            <v-checkbox v-model="camposPote.validade" label="Validade" density="compact" hide-details />
            <v-checkbox v-model="camposPote.frase" label='Frase "Natural como deve ser!"' density="compact" hide-details />
          </template>

          <!-- ── Templates padrão ── -->
          <template v-else>
            <div class="text-body-2 font-weight-bold mb-2">Campos visíveis</div>
            <v-checkbox v-model="campos.nome" label="Nome do produto" density="compact" hide-details />
            <v-checkbox v-model="campos.preco" label="Preço de venda" density="compact" hide-details />
            <v-checkbox v-model="campos.precoPor" label="Preço Promocional" density="compact" hide-details />
            <v-checkbox v-model="campos.codBarras" label="Código de barras (EAN)" density="compact" hide-details />
            <v-checkbox v-model="campos.validade" label="Validade" density="compact" hide-details />
            <v-checkbox v-model="campos.lote" label="Lote" density="compact" hide-details />
            <v-checkbox v-model="campos.ncm" label="NCM" density="compact" hide-details />
          </template>

          <v-divider class="my-3" />
          <v-text-field v-model.number="qtdEtiquetas" label="Qtd por produto" type="number"
            variant="outlined" density="compact" :min="1" :max="100" />
          <v-text-field v-model="validade" label="Validade" type="date"
            variant="outlined" density="compact" class="mt-2" />
          <v-text-field v-if="!isGondola && template !== 'pote9x9' && template !== 'ecogranel'"
            v-model="lote" label="Lote (p/ todos)" variant="outlined" density="compact" />
          <v-text-field v-if="!isGondola && template !== 'pote9x9' && template !== 'ecogranel'"
            v-model.number="precoPromo" label="Preço Promocional (R$)"
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
          <div class="d-flex align-center mb-3 gap-3">
            <div class="text-body-2 font-weight-bold">Pré-visualização</div>
            <v-chip v-if="isGondola" color="success" size="small" prepend-icon="mdi-printer">Zebra ZPL</v-chip>
            <v-chip v-if="template === 'ecogranel'" color="green-darken-2" size="small" prepend-icon="mdi-qrcode">QR Code</v-chip>
          </div>

          <div v-if="!produtosSel.length" class="text-center py-8 text-medium-emphasis">
            <v-icon icon="mdi-tag-outline" size="60" class="mb-2" />
            <div>Selecione produtos para visualizar as etiquetas</div>
          </div>

          <!-- Preview gôndola -->
          <div v-if="isGondola" class="gondola-grid">
            <div v-for="p in etiquetasExpandidas" :key="p._key"
              class="gondola-etiqueta" :style="gondolaDimStyle">
              <div v-if="camposGondola.nome" class="gon-nome"
                :style="{ fontSize: gondolaCfg.nomeFontPx + 'px' }">{{ p.descricao }}</div>
              <div v-if="camposGondola.preco" class="gon-preco"
                :style="{ fontSize: gondolaCfg.precoFontPx + 'px' }">
                <span class="gon-preco-rs">R$</span>
                <span class="gon-preco-valor">{{ fmtPreco(p.precoVenda) }}</span>
              </div>
              <div v-if="camposGondola.precoKg" class="gon-por-unidade">{{ fmtPrecoKg(p) }}</div>
              <div v-if="camposGondola.validade && validade" class="gon-validade">Val: {{ fmtData(validade) }}</div>
              <div v-if="camposGondola.codBarras && p.codigoBarras" class="gon-barcode-area">
                <div class="gon-barcode-bars">
                  <span v-for="i in 50" :key="i"
                    :style="{ width: (i % 3 === 0 ? 2 : 1) + 'px', background: i % 7 === 0 ? '#fff' : '#111' }"
                    class="gon-bar" />
                </div>
                <div class="gon-barcode-num">{{ p.codigoBarras }}</div>
              </div>
              <div v-if="camposGondola.codigoPlu && p.codigoPlu" class="gon-plu">
                PLU: {{ String(p.codigoPlu).padStart(6, '0') }}
              </div>
            </div>
          </div>

          <!-- ── Preview EcoGranel ── -->
          <div v-else-if="template === 'ecogranel'" id="area-impressao" class="etiquetas-grid">
            <div v-for="p in etiquetasExpandidas" :key="p._key"
              class="etiqueta-eco"
              :style="{ border: `${borda.espessura}px solid ${borda.cor}`, borderRadius: `${borda.espessura * 2 + 4}px` }">

              <!-- Marca d'água -->
              <img v-if="marcaDaguaUrl" :src="marcaDaguaUrl" class="eco-marca-dagua" />

              <!-- Cabeçalho: nome do produto -->
              <div v-if="camposEco.nome" class="eco-nome" :style="{ color: '#111' }">
                {{ p.descricao?.toUpperCase() }}
                <span v-if="camposEco.codigoPlu && p.codigoPlu" class="eco-nome-plu">
                  -{{ String(p.codigoPlu).padStart(4, '0') }}
                </span>
              </div>

              <!-- Preço grande -->
              <div v-if="camposEco.preco100g" class="eco-preco-bloco">
                <span class="eco-preco-valor">{{ fmtPrecoDisplay(preco100g(p.precoVenda)) }}</span>
                <div class="eco-preco-label">cada 100g</div>
              </div>

              <!-- Validade -->
              <div v-if="camposEco.validade && validade" class="eco-validade">
                <strong>Validade: {{ fmtData(validade) }}</strong>
              </div>

              <!-- Texto descritivo -->
              <div v-if="camposEco.descricao" class="eco-descricao">
                {{ p.descricaoComplementar || textoDescritivoEco }}
              </div>

              <!-- Rodapé: QR Code + frase -->
              <div class="eco-rodape">
                <div v-if="camposEco.qrcode" class="eco-qr-wrap">
                  <canvas :ref="el => registrarQr(el, p)" class="eco-qr-canvas" />
                </div>
                <div v-if="camposEco.frase" class="eco-frase">
                  <strong>Natural como deve ser!</strong>
                </div>
              </div>
            </div>
          </div>

          <!-- Preview pote 9×9cm -->
          <div v-else-if="template === 'pote9x9'" id="area-impressao" class="etiquetas-grid">
            <div v-for="p in etiquetasExpandidas" :key="p._key"
              class="etiqueta-pote"
              :style="{ border: `${borda.espessura}px solid ${borda.cor}`, borderRadius: `${borda.espessura * 2}px` }">
              <div v-if="camposPote.nome" class="pote-nome" :style="{ background: borda.cor }">{{ p.descricao }}</div>
              <div class="pote-corpo">
                <div v-if="camposPote.descricao && p.descricaoComplementar" class="pote-descricao">
                  {{ p.descricaoComplementar }}
                </div>
                <div v-if="camposPote.codigoPlu && p.codigoPlu" class="pote-plu">
                  <span class="pote-plu-label">Cód. Balança:</span>
                  <span class="pote-plu-valor">{{ String(p.codigoPlu).padStart(6, '0') }}</span>
                </div>
                <div v-if="camposPote.preco100g" class="pote-preco-bloco">
                  <div class="pote-preco-valor" :style="{ color: borda.cor }">R$ {{ fmt(preco100g(p.precoVenda)) }}</div>
                  <div class="pote-preco-label">cada 100g</div>
                </div>
                <div v-if="camposPote.validade && validade" class="pote-validade">
                  <v-icon icon="mdi-calendar-clock" size="12" class="mr-1" />
                  Validade: {{ fmtData(validade) }}
                </div>
              </div>
              <div v-if="camposPote.frase" class="pote-frase"
                :style="{ color: borda.cor, borderTopColor: borda.cor + '33' }">
                Natural como deve ser!
              </div>
            </div>
          </div>

          <!-- Preview templates padrão -->
          <div v-else id="area-impressao" class="etiquetas-grid">
            <div v-for="p in etiquetasExpandidas" :key="p._key" class="etiqueta" :style="tplAtual.style">
              <div v-if="campos.nome" class="etq-nome" :style="tplAtual.nomeStyle">{{ p.descricao }}</div>
              <div v-if="campos.precoPor && precoPromo" class="etq-de">DE R$ {{ fmt(p.precoVenda) }}</div>
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

          <v-alert v-if="isGondola && produtosSel.length" type="info" variant="tonal"
            density="compact" class="mt-4" icon="mdi-information">
            O botão <strong>Gerar ZPL</strong> baixa o arquivo <code>.zpl</code> pronto para enviar à Zebra
            via USB, rede ou Zebra Setup Utilities.
          </v-alert>
        </v-card>
      </v-col>
    </v-row>

    <!-- Dialog: enviar para Zebra via rede -->
    <v-dialog v-model="enviarZebraDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title class="pa-4 text-body-1 font-weight-bold">Enviar para Zebra via Rede</v-card-title>
        <v-card-text class="pa-4">
          <v-text-field v-model="zebraIp" label="IP da impressora" placeholder="192.168.1.100"
            variant="outlined" density="compact" prepend-inner-icon="mdi-network" />
          <v-text-field v-model.number="zebraPorta" label="Porta" type="number"
            variant="outlined" density="compact" class="mt-2" />
          <v-alert type="warning" variant="tonal" density="compact" class="mt-2">
            Por enquanto, baixe o ZPL e use o <strong>Zebra Setup Utilities</strong> para enviar.
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn @click="enviarZebraDialog = false">Fechar</v-btn>
          <v-btn color="primary" @click="baixarZpl">Baixar ZPL</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, watch } from 'vue'
import QRCode from 'qrcode'
import GuiaPassos from '@/components/GuiaPassos.vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const buscaTemp = ref<string | null>(null)
const buscando = ref(false)
const sugestoes = ref<any[]>([])
const produtosSel = ref<any[]>([])
const template = ref('ecogranel')
const qtdEtiquetas = ref(1)
const validade = ref('')
const lote = ref('')
const precoPromo = ref<number | null>(null)
const enviarZebraDialog = ref(false)
const zebraIp = ref('192.168.1.100')
const zebraPorta = ref(9100)
const zebraDpi = ref(203)
const gondolaTamanho = ref('70x40')
const marcaDaguaUrl = ref('')
const inputMarcaDagua = ref<HTMLInputElement | null>(null)
const qrBaseUrl = ref('https://ecogranel.com.br/produtos/produto.php?p=')
const textoDescritivoEco = ref('')

const borda = ref({ cor: '#2e7d32', espessura: 5 })

const camposEco = ref({
  nome: true, codigoPlu: true, preco100g: true, validade: true,
  descricao: true, qrcode: true, frase: true,
})

const camposPote = ref({
  nome: true, descricao: true, codigoPlu: true,
  preco100g: true, validade: true, frase: true,
})

const camposGondola = ref({
  nome: true, preco: true, precoKg: true,
  codBarras: true, codigoPlu: false, validade: false, unidade: true,
})

const campos = ref({
  nome: true, preco: true, precoPor: false,
  codBarras: true, validade: false, lote: false, ncm: false,
})

const templates = [
  { id: 'ecogranel', nome: 'EcoGranel' },
  { id: 'gondola-70x40', nome: 'Gôndola Zebra' },
  { id: 'pote9x9', nome: 'Pote 9×9cm' },
  { id: '40x25', nome: '40×25mm' },
  { id: '50x30', nome: '50×30mm' },
  { id: '100x50', nome: '100×50mm' },
  { id: 'grande', nome: 'Grande' },
]

const gondolaTamanhos = [
  { id: '40x20', nome: '40×20mm', w: 40, h: 20 },
  { id: '50x25', nome: '50×25mm', w: 50, h: 25 },
  { id: '70x40', nome: '70×40mm', w: 70, h: 40 },
  { id: '100x60', nome: '100×60mm', w: 100, h: 60 },
  { id: '100x30', nome: '100×30mm', w: 100, h: 30 },
]

const isGondola = computed(() => template.value.startsWith('gondola'))

const gondolaTamanhoAtual = computed(() =>
  gondolaTamanhos.find(s => s.id === gondolaTamanho.value) ?? gondolaTamanhos[2]
)

const gondolaDimStyle = computed(() => {
  const { w, h } = gondolaTamanhoAtual.value
  return { width: `${w * 3.78}px`, height: `${h * 3.78}px` }
})

const gondolaCfg = computed(() => {
  const { w } = gondolaTamanhoAtual.value
  const scale = w / 70
  return {
    nomeFontPx: Math.round(10 * scale),
    precoFontPx: Math.round(22 * scale),
  }
})

const tplConfig: Record<string, any> = {
  '40x25':  { style: 'width:150px;height:94px;font-size:8px;padding:4px',  nomeStyle: 'font-size:8px;font-weight:bold;line-height:1.1', precoStyle: 'font-size:16px;font-weight:bold' },
  '50x30':  { style: 'width:189px;height:113px;font-size:9px;padding:5px', nomeStyle: 'font-size:9px;font-weight:bold', precoStyle: 'font-size:20px;font-weight:bold' },
  '100x50': { style: 'width:378px;height:189px;font-size:11px;padding:8px',nomeStyle: 'font-size:12px;font-weight:bold', precoStyle: 'font-size:28px;font-weight:bold' },
  'grande': { style: 'width:283px;height:170px;font-size:10px;padding:8px',nomeStyle: 'font-size:11px;font-weight:bold', precoStyle: 'font-size:24px;font-weight:bold' },
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

// ── Helpers ──────────────────────────────────────────────────────────────────
function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }
function fmtData(d: string) { if (!d) return ''; const [y, m, dd] = d.split('-'); return `${dd}/${m}/${y}` }
function preco100g(precoKg: number) { return precoKg / 10 }

// Formato especial: "7,69" (sem R$ e com vírgula)
function fmtPrecoDisplay(v: number) {
  return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
}

function fmtPreco(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }

function fmtPrecoKg(p: any) {
  const unidade = p.unidadeSigla || 'KG'
  return `R$ ${fmt(p.precoVenda)} / ${unidade}`
}

// Converte nome do produto em slug para URL do QR
function nomeParaSlug(nome: string): string {
  return (nome ?? '')
    .toLowerCase()
    .normalize('NFD').replace(/[̀-ͯ]/g, '') // remove acentos
    .replace(/[^a-z0-9\s-]/g, '')
    .trim()
    .replace(/\s+/g, '-')
}

// ── QR Code ──────────────────────────────────────────────────────────────────
// Guarda referência dos canvas por _key e gera QR quando monta
const qrCanvasMap = new Map<string, HTMLCanvasElement>()

function registrarQr(el: any, p: any) {
  if (!el) return
  const key = p._key
  if (qrCanvasMap.get(key) === el) return
  qrCanvasMap.set(key, el)
  renderizarQr(el, p)
}

async function renderizarQr(canvas: HTMLCanvasElement, p: any) {
  const slug = nomeParaSlug(p.descricao ?? '')
  const url = qrBaseUrl.value + slug
  try {
    await QRCode.toCanvas(canvas, url, {
      width: 100,
      margin: 1,
      color: { dark: '#000000', light: '#ffffff00' },
    })
  } catch { /* silencia erros de canvas */ }
}

// Re-renderiza QR quando a URL base muda
watch(qrBaseUrl, async () => {
  await nextTick()
  for (const [key, canvas] of qrCanvasMap.entries()) {
    const p = etiquetasExpandidas.value.find(e => e._key === key)
    if (p) renderizarQr(canvas, p)
  }
})

watch(etiquetasExpandidas, async () => {
  if (template.value !== 'ecogranel') return
  await nextTick()
  for (const [key, canvas] of qrCanvasMap.entries()) {
    const p = etiquetasExpandidas.value.find(e => e._key === key)
    if (p) renderizarQr(canvas, p)
  }
}, { deep: true })

// ── Marca d'água ─────────────────────────────────────────────────────────────
function carregarMarcaDagua(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = ev => { marcaDaguaUrl.value = ev.target?.result as string }
  reader.readAsDataURL(file)
}

// ── Gerador ZPL ──────────────────────────────────────────────────────────────
function gerarZpl(): string {
  const dpi = zebraDpi.value
  const dotsPerMm = dpi / 25.4
  const { w, h } = gondolaTamanhoAtual.value
  const labelW = Math.round(w * dotsPerMm)
  const labelH = Math.round(h * dotsPerMm)
  const mx = Math.round(2 * dotsPerMm)
  const my = Math.round(2 * dotsPerMm)

  const zplBlocks: string[] = []

  for (const p of etiquetasExpandidas.value) {
    const nome    = zplSanitize(p.descricao ?? '')
    const precoStr = 'R$ ' + fmt(p.precoVenda)
    const porKg   = fmtPrecoKg(p)
    const ean     = p.codigoBarras ?? ''
    const plu     = p.codigoPlu ? 'PLU: ' + String(p.codigoPlu).padStart(6, '0') : ''
    const val     = validade.value ? 'Val: ' + fmtData(validade.value) : ''

    let zpl = `^XA\n^PW${labelW}\n^LL${labelH}\n^LH0,0\n`
    let y = my

    if (camposGondola.value.nome && nome) {
      const fh = Math.round(4 * dotsPerMm); const fw = Math.round(fh * 0.6)
      zpl += `^FO${mx},${y}^A0N,${fh},${fw}^FD${nome}^FS\n`; y += fh + Math.round(1 * dotsPerMm)
    }
    if (camposGondola.value.preco) {
      const fh = Math.round(h * 0.38 * dotsPerMm); const fw = Math.round(fh * 0.65)
      zpl += `^FO${mx},${y}^A0N,${fh},${fw}^FD${precoStr}^FS\n`; y += fh + Math.round(0.5 * dotsPerMm)
    }
    if (camposGondola.value.precoKg) {
      const fh = Math.round(3 * dotsPerMm); const fw = Math.round(fh * 0.6)
      zpl += `^FO${mx},${y}^A0N,${fh},${fw}^FD${porKg}^FS\n`; y += fh + Math.round(0.5 * dotsPerMm)
    }
    if (camposGondola.value.validade && val) {
      const fh = Math.round(2.5 * dotsPerMm); const fw = Math.round(fh * 0.6)
      zpl += `^FO${mx},${y}^A0N,${fh},${fw}^FD${val}^FS\n`; y += fh + Math.round(0.5 * dotsPerMm)
    }
    if (camposGondola.value.codigoPlu && plu) {
      const fh = Math.round(2.5 * dotsPerMm); const fw = Math.round(fh * 0.6)
      zpl += `^FO${mx},${y}^A0N,${fh},${fw}^FD${plu}^FS\n`
    }
    if (camposGondola.value.codBarras && ean) {
      const barcodeH2 = Math.max(Math.round((labelH - y - Math.round(3 * dotsPerMm))), Math.round(6 * dotsPerMm))
      if (w >= 60) {
        zpl += `^FO${labelW - Math.round(35 * dotsPerMm)},${my}^BY1,2,${barcodeH2}^BE^FD${ean}^FS\n`
      } else {
        const bh = Math.max(Math.round(h * 0.3 * dotsPerMm), Math.round(5 * dotsPerMm))
        zpl += `^FO${mx},${y}^BY1,2,${bh}^BE^FD${ean}^FS\n`
      }
    }
    zpl += `^PQ${qtdEtiquetas.value}\n^XZ\n`
    zplBlocks.push(zpl)
  }
  return zplBlocks.join('\n')
}

function zplSanitize(s: string): string {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^\x20-\x7E]/g, '').substring(0, 50)
}

function baixarZpl() {
  const zpl = gerarZpl()
  const blob = new Blob([zpl], { type: 'text/plain' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `gondola_${gondolaTamanho.value}_${new Date().toISOString().slice(0, 10)}.zpl`
  a.click()
  URL.revokeObjectURL(url)
  enviarZebraDialog.value = false
}

// ── Busca de produtos ────────────────────────────────────────────────────────
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

// ── Impressão ────────────────────────────────────────────────────────────────
function imprimir() {
  const area = document.getElementById('area-impressao')
  if (!area) return
  const bordaCor = borda.value.cor
  const w = window.open('', '_blank')!
  w.document.write(`
    <html><head><title>Etiquetas EcoGranel</title>
    <style>
      * { box-sizing: border-box; margin: 0; padding: 0; }
      body { margin: 0; font-family: Arial, sans-serif; background: white; }
      .etiquetas-grid { display: flex; flex-wrap: wrap; gap: 6mm; padding: 5mm; }

      /* ── EcoGranel ── */
      .etiqueta-eco {
        width: 10cm; height: 10cm;
        display: flex; flex-direction: column;
        background: linear-gradient(160deg, #ffffff 0%, #e8f5e9 60%, #c8e6c9 100%);
        position: relative; overflow: hidden; page-break-inside: avoid;
      }
      .eco-marca-dagua {
        position: absolute; right: 4mm; top: 30%; width: 42%; opacity: 0.18;
        pointer-events: none; user-select: none;
      }
      .eco-nome {
        font-size: 13pt; font-weight: 900; color: #111;
        text-align: center; padding: 5mm 4mm 2mm; line-height: 1.15;
        letter-spacing: 0.5px;
      }
      .eco-nome-plu { font-weight: 700; font-size: 0.85em; }
      .eco-preco-bloco { text-align: center; padding: 2mm 4mm 1mm; }
      .eco-preco-valor { font-size: 60pt; font-weight: 900; color: #111; line-height: 1; letter-spacing: -2px; }
      .eco-preco-label { font-size: 9pt; color: #444; margin-top: 1mm; }
      .eco-validade { text-align: center; font-size: 10pt; color: #222; padding: 1mm 4mm; }
      .eco-descricao { font-size: 9pt; color: #333; text-align: center;
        padding: 2mm 5mm; line-height: 1.4; font-weight: 500; }
      .eco-rodape {
        flex: 1; display: flex; align-items: flex-end;
        justify-content: space-between; padding: 2mm 4mm 3mm;
      }
      .eco-qr-wrap canvas { width: 22mm !important; height: 22mm !important; display: block; }
      .eco-frase { font-size: 10pt; font-weight: 900; color: #1b5e20;
        text-align: right; max-width: 55%; line-height: 1.3; }

      /* ── Pote ── */
      .etiqueta-pote {
        width: 9cm; height: 9cm; display: flex; flex-direction: column;
        page-break-inside: avoid; overflow: hidden; background: white;
      }
      .pote-nome { color: white; text-align: center; font-weight: bold;
        font-size: 14pt; padding: 6px 8px; word-break: break-word; line-height: 1.2; }
      .pote-corpo { flex: 1; display: flex; flex-direction: column;
        justify-content: center; align-items: center; gap: 6px; padding: 8px; }
      .pote-descricao { font-size: 9pt; color: #444; text-align: center; line-height: 1.3; }
      .pote-plu { display: flex; align-items: center; gap: 4px; font-size: 8pt; color: #666; }
      .pote-plu-valor { font-family: monospace; font-weight: bold; font-size: 10pt; color: #333; }
      .pote-preco-bloco { text-align: center; }
      .pote-preco-valor { font-size: 22pt; font-weight: bold; color: ${bordaCor}; line-height: 1; }
      .pote-preco-label { font-size: 8pt; color: #666; margin-top: 1px; }
      .pote-validade { font-size: 8pt; color: #555; }
      .pote-frase { text-align: center; font-style: italic; font-weight: bold;
        font-size: 10pt; padding: 6px 8px; color: ${bordaCor};
        letter-spacing: 0.5px; border-top: 1px solid; }

      /* ── Padrão ── */
      .etiqueta { border: 1px solid #999; display: flex; flex-direction: column;
        justify-content: space-between; overflow: hidden; page-break-inside: avoid;
        background: white; }
      .etq-nome { font-weight: bold; word-break: break-word; }
      .etq-de { text-decoration: line-through; font-size: 0.8em; color: #888; }
      .etq-preco { font-weight: bold; color: #1565C0; }
      .etq-ean { font-family: monospace; font-size: 0.75em; }
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

<style>
.etiquetas-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  background: #f0f0f0;
  padding: 16px;
  border-radius: 8px;
  min-height: 200px;
}

/* ── EcoGranel ── */
.etiqueta-eco {
  width: 380px;
  height: 380px;
  display: flex;
  flex-direction: column;
  background: linear-gradient(160deg, #ffffff 0%, #e8f5e9 60%, #c8e6c9 100%);
  position: relative;
  overflow: hidden;
  box-shadow: 0 3px 12px rgba(0,0,0,.15);
  transition: box-shadow .2s;
}
.etiqueta-eco:hover { box-shadow: 0 6px 20px rgba(0,0,0,.22); }

.eco-marca-dagua {
  position: absolute;
  right: 8px;
  top: 30%;
  width: 42%;
  opacity: 0.18;
  pointer-events: none;
  user-select: none;
}

.eco-nome {
  font-size: 16px;
  font-weight: 900;
  color: #111;
  text-align: center;
  padding: 14px 12px 6px;
  line-height: 1.2;
  letter-spacing: 0.3px;
  z-index: 1;
}
.eco-nome-plu {
  font-size: 0.85em;
  font-weight: 700;
}

.eco-preco-bloco {
  text-align: center;
  padding: 4px 12px 2px;
  z-index: 1;
}
.eco-preco-valor {
  font-size: 86px;
  font-weight: 900;
  color: #111;
  line-height: 1;
  letter-spacing: -3px;
  font-family: Arial Black, Arial, sans-serif;
}
.eco-preco-label {
  font-size: 13px;
  color: #555;
  margin-top: 2px;
}

.eco-validade {
  text-align: center;
  font-size: 13px;
  color: #222;
  padding: 2px 12px 4px;
  z-index: 1;
}

.eco-descricao {
  font-size: 12px;
  color: #333;
  text-align: center;
  padding: 4px 16px 6px;
  line-height: 1.4;
  font-weight: 500;
  z-index: 1;
}

.eco-rodape {
  flex: 1;
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  padding: 6px 14px 12px;
  z-index: 1;
}

.eco-qr-canvas {
  width: 90px !important;
  height: 90px !important;
  display: block;
}

.eco-frase {
  font-size: 13px;
  font-weight: 900;
  color: #1b5e20;
  text-align: right;
  max-width: 55%;
  line-height: 1.3;
}

/* ── Gôndola Zebra ── */
.gondola-grid {
  display: flex; flex-wrap: wrap; gap: 10px;
  background: #e8e8e8; padding: 12px; border-radius: 8px; min-height: 100px;
}
.gondola-etiqueta {
  background: white; border: 1px solid #bbb; border-left: 5px solid #1565C0;
  display: flex; flex-direction: column; justify-content: space-between;
  padding: 5px 7px; box-shadow: 0 1px 4px rgba(0,0,0,.1);
  overflow: hidden; position: relative;
}
.gon-nome { font-weight: bold; color: #1a1a1a; line-height: 1.2; word-break: break-word; }
.gon-preco { font-weight: 900; color: #1565C0; line-height: 1; display: flex; align-items: baseline; gap: 2px; }
.gon-preco-rs { font-size: 0.45em; font-weight: bold; padding-bottom: 2px; }
.gon-por-unidade { font-size: 9px; color: #555; }
.gon-validade { font-size: 8px; color: #888; }
.gon-barcode-area { position: absolute; right: 4px; top: 4px; bottom: 4px; width: 60px;
  display: flex; flex-direction: column; align-items: center; justify-content: center; }
.gon-barcode-bars { display: flex; align-items: stretch; height: 70%; }
.gon-bar { display: inline-block; height: 100%; }
.gon-barcode-num { font-family: monospace; font-size: 6px; color: #333; margin-top: 2px; text-align: center; }
.gon-plu { font-size: 8px; color: #666; font-family: monospace; }

/* ── Pote 9×9cm ── */
.etiqueta-pote {
  width: 340px; height: 340px; display: flex; flex-direction: column;
  background: white; overflow: hidden;
  box-shadow: 0 2px 8px rgba(0,0,0,.12); transition: box-shadow .2s;
}
.etiqueta-pote:hover { box-shadow: 0 4px 16px rgba(0,0,0,.2); }
.pote-nome { text-align: center; font-weight: bold; font-size: 15px;
  padding: 10px 12px; word-break: break-word; line-height: 1.25; color: white; }
.pote-corpo { flex: 1; display: flex; flex-direction: column;
  justify-content: center; align-items: center; gap: 8px; padding: 10px 12px; }
.pote-descricao { font-size: 11px; color: #555; text-align: center; line-height: 1.4; }
.pote-plu { display: flex; align-items: center; gap: 5px; font-size: 11px; color: #666; }
.pote-plu-valor { font-family: monospace; font-weight: bold; font-size: 13px; color: #333; }
.pote-preco-bloco { text-align: center; }
.pote-preco-valor { font-size: 32px; font-weight: bold; line-height: 1; }
.pote-preco-label { font-size: 11px; color: #666; margin-top: 2px; }
.pote-validade { font-size: 11px; color: #555; display: flex; align-items: center; }
.pote-frase { text-align: center; font-style: italic; font-weight: bold;
  font-size: 12px; padding: 8px 10px; letter-spacing: 0.3px; border-top: 1px solid; }

/* ── Templates padrão ── */
.etiqueta { border: 1px solid #ccc; background: white; display: flex;
  flex-direction: column; justify-content: space-between;
  box-sizing: border-box; overflow: hidden; cursor: default; transition: box-shadow .2s; }
.etiqueta:hover { box-shadow: 0 2px 8px rgba(0,0,0,.15); }
.etq-preco { color: #1565C0; }
.etq-de { text-decoration: line-through; font-size: .75em; color: #888; }
.etq-ean { font-family: monospace; font-size: .7em; letter-spacing: 2px; }
.etq-rodape { font-size: .68em; color: #666; display: flex; gap: 4px; flex-wrap: wrap; }
</style>
