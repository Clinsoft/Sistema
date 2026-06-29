<template>
  <div class="arte-wrapper" :style="wrapperStyle">
    <svg :viewBox="`0 0 ${dim.w} ${dim.h}`" :width="previewW" :height="previewH"
      xmlns="http://www.w3.org/2000/svg" :ref="el => svgEl = el as SVGSVGElement">

      <!-- Fundo degradê -->
      <defs>
        <linearGradient :id="`bg-${uid}`" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" :stop-color="cores.fundo1" />
          <stop offset="100%" :stop-color="cores.fundo2" />
        </linearGradient>
        <linearGradient :id="`dest-${uid}`" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" :stop-color="cores.destaque" stop-opacity="0.9" />
          <stop offset="100%" :stop-color="cores.destaque" stop-opacity="0.7" />
        </linearGradient>
        <!-- Círculos decorativos -->
        <circle :id="`c1-${uid}`" :cx="dim.w * 0.85" :cy="dim.h * 0.12" :r="dim.w * 0.28" />
        <circle :id="`c2-${uid}`" :cx="dim.w * 0.12" :cy="dim.h * 0.88" :r="dim.w * 0.20" />
      </defs>

      <!-- Fundo -->
      <rect width="100%" height="100%" :fill="`url(#bg-${uid})`" />

      <!-- Círculos decorativos com opacidade -->
      <circle :cx="dim.w * 0.88" :cy="dim.h * 0.10" :r="dim.w * 0.30"
        :fill="cores.fundo2" opacity="0.18" />
      <circle :cx="dim.w * 0.10" :cy="dim.h * 0.90" :r="dim.w * 0.22"
        :fill="cores.fundo2" opacity="0.13" />
      <circle :cx="dim.w * 0.50" :cy="dim.h * 0.0" :r="dim.w * 0.40"
        :fill="cores.fundo1" opacity="0.10" />

      <!-- Linha horizontal decorativa topo -->
      <rect x="0" y="0" :width="dim.w" :height="dim.h * 0.008" :fill="cores.destaque" />

      <!-- Logo / Nome da loja (topo esquerdo) -->
      <g :transform="`translate(${dim.mx}, ${dim.my})`">
        <rect :width="dim.logoW" :height="dim.logoH" rx="8" :fill="cores.destaque" opacity="0.2" />
        <text :x="dim.logoW / 2" :y="dim.logoH * 0.65"
          text-anchor="middle" :font-size="dim.logoFontSz"
          font-weight="bold" :fill="cores.destaque" font-family="Arial, sans-serif">
          🌿 EcoGranel
        </text>
      </g>

      <!-- Chip de tipo -->
      <g :transform="`translate(${dim.mx}, ${dim.my + dim.logoH + dim.gap})`">
        <rect :width="dim.chipW" :height="dim.chipH" :rx="dim.chipH / 2"
          :fill="cores.destaque" />
        <text :x="dim.chipW / 2" :y="dim.chipH * 0.72"
          text-anchor="middle" :font-size="dim.chipFontSz"
          font-weight="bold" fill="white" font-family="Arial, sans-serif">
          {{ labelTipo.toUpperCase() }}
        </text>
      </g>

      <!-- Bloco de destaque: desconto -->
      <g :transform="`translate(${dim.mx}, ${dim.descontoY})`">
        <text x="0" :y="dim.descontoFontSz"
          :font-size="dim.descontoFontSz" font-weight="900"
          :fill="cores.destaque" font-family="Arial, sans-serif" letter-spacing="-2">
          {{ descontoStr }}
        </text>
        <text x="0" :y="dim.descontoFontSz + dim.subDescontoFontSz + dim.gap * 0.5"
          :font-size="dim.subDescontoFontSz" font-weight="600"
          :fill="cores.texto" opacity="0.8" font-family="Arial, sans-serif">
          {{ subDescontoStr }}
        </text>
      </g>

      <!-- Linha separadora -->
      <rect :x="dim.mx" :y="dim.sepY" :width="dim.w * 0.5" :height="2"
        :fill="cores.destaque" opacity="0.4" />

      <!-- Nome da promoção -->
      <text :x="dim.mx" :y="dim.nomeY"
        :font-size="dim.nomeFontSz" font-weight="bold"
        :fill="cores.texto" font-family="Arial, sans-serif">
        <tspan v-for="(linha, i) in linhasNome" :key="i"
          x="dim.mx" :dy="i === 0 ? 0 : dim.nomeFontSz * 1.25">{{ linha }}</tspan>
      </text>

      <!-- Período -->
      <text v-if="periodoStr" :x="dim.mx" :y="dim.periodoY"
        :font-size="dim.periodoFontSz" :fill="cores.texto" opacity="0.75"
        font-family="Arial, sans-serif">
        📅 {{ periodoStr }}
      </text>

      <!-- Aplica em -->
      <text :x="dim.mx" :y="dim.aplicaY"
        :font-size="dim.periodoFontSz" :fill="cores.texto" opacity="0.70"
        font-family="Arial, sans-serif">
        {{ aplicaEmStr }}
      </text>

      <!-- Badge clube -->
      <g v-if="layout.apenasClube"
        :transform="`translate(${dim.w - dim.mx - dim.clubeW}, ${dim.my + dim.logoH + dim.gap})`">
        <rect :width="dim.clubeW" :height="dim.chipH" :rx="dim.chipH / 2"
          fill="#7c3aed" />
        <text :x="dim.clubeW / 2" :y="dim.chipH * 0.72"
          text-anchor="middle" :font-size="dim.chipFontSz"
          font-weight="bold" fill="white" font-family="Arial, sans-serif">
          ⭐ EXCLUSIVO CLUBE
        </text>
      </g>

      <!-- Rodapé -->
      <rect x="0" :y="dim.h - dim.rodapeH" :width="dim.w" :height="dim.rodapeH"
        :fill="cores.fundo2" opacity="0.6" />
      <text :x="dim.w / 2" :y="dim.h - dim.rodapeH * 0.25"
        text-anchor="middle" :font-size="dim.periodoFontSz * 0.85"
        :fill="cores.texto" opacity="0.6" font-family="Arial, sans-serif">
        www.ecogranel.com.br
      </text>
    </svg>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'

const props = defineProps<{
  layout: Record<string, any>
  previewW?: number
}>()

const uid = Math.random().toString(36).slice(2, 7)
const svgEl = ref<SVGSVGElement | null>(null)

// Paleta de cores por tipo de promoção
const paletasTipo: Record<string, { fundo1: string; fundo2: string; destaque: string; texto: string }> = {
  Desconto:            { fundo1: '#0f172a', fundo2: '#1e293b', destaque: '#10b981', texto: '#ffffff' },
  LeveXPagueY:         { fundo1: '#1e3a5f', fundo2: '#0c2340', destaque: '#f59e0b', texto: '#ffffff' },
  DescontoProgressivo: { fundo1: '#312e81', fundo2: '#1e1b4b', destaque: '#818cf8', texto: '#ffffff' },
  Combo:               { fundo1: '#134e4a', fundo2: '#0f3e3a', destaque: '#34d399', texto: '#ffffff' },
  Pix:                 { fundo1: '#064e3b', fundo2: '#022c22', destaque: '#6ee7b7', texto: '#ffffff' },
  Aniversariante:      { fundo1: '#831843', fundo2: '#500724', destaque: '#f9a8d4', texto: '#ffffff' },
}
const cores = computed(() =>
  paletasTipo[props.layout.tipoPromocao] ?? paletasTipo.Desconto
)

// Dimensões por formato
const formatoDim: Record<string, [number, number]> = {
  FeedQuadrado:     [1080, 1080],
  StoryVertical:    [1080, 1920],
  BannerHorizontal: [1200, 628],
}
const [W, H] = formatoDim[props.layout.formato] ?? [1080, 1080]
const dim = computed(() => {
  const w = W, h = H
  const mx = w * 0.075
  const my = h * 0.06
  const gap = h * 0.015
  const logoH = h * 0.055
  const logoW = w * 0.35
  const logoFontSz = logoH * 0.58
  const chipH = h * 0.038
  const chipW = w * 0.30
  const chipFontSz = chipH * 0.55
  const clubeW = w * 0.34
  const descontoY = my + logoH + chipH + gap * 3.5
  const descontoFontSz = h * 0.17
  const subDescontoFontSz = h * 0.038
  const sepY = descontoY + descontoFontSz + subDescontoFontSz + gap * 2
  const nomeFontSz = h * 0.040
  const nomeY = sepY + gap * 2.5 + nomeFontSz
  const periodoFontSz = h * 0.030
  const periodoY = nomeY + nomeFontSz * 2.6
  const aplicaY = periodoY + periodoFontSz * 1.8
  const rodapeH = h * 0.045
  return {
    w, h, mx, my, gap, logoH, logoW, logoFontSz,
    chipH, chipW, chipFontSz, clubeW,
    descontoY, descontoFontSz, subDescontoFontSz,
    sepY, nomeFontSz, nomeY,
    periodoFontSz, periodoY, aplicaY, rodapeH,
  }
})

const previewW = computed(() => props.previewW ?? 320)
const previewH = computed(() => Math.round(previewW.value * (H / W)))
const wrapperStyle = computed(() => ({
  width: previewW.value + 'px',
  height: previewH.value + 'px',
  display: 'inline-block',
}))

const labelTipos: Record<string, string> = {
  Desconto: 'Desconto especial', LeveXPagueY: 'Leve X Pague Y',
  DescontoProgressivo: 'Desconto progressivo', Combo: 'Combo / Kit',
  Pix: 'Desconto no Pix', Aniversariante: 'Desconto aniversariante',
}
const labelTipo = computed(() => labelTipos[props.layout.tipoPromocao] ?? 'Promoção')

const descontoStr = computed(() => {
  const d = props.layout.desconto ?? 0
  return props.layout.tipoDesconto === 'Percentual' ? `${d}% OFF` : `R$ ${d.toLocaleString('pt-BR', { minimumFractionDigits: 2 })} OFF`
})
const subDescontoStr = computed(() => {
  if (props.layout.tipoDesconto === 'Percentual') return 'de desconto'
  return 'de desconto em valor'
})

// Quebra o nome em linhas de ~28 chars
const linhasNome = computed(() => {
  const nome: string = props.layout.nomePromocao ?? ''
  const max = props.layout.formato === 'BannerHorizontal' ? 40 : 28
  const palavras = nome.split(' ')
  const linhas: string[] = []
  let linha = ''
  for (const p of palavras) {
    if ((linha + ' ' + p).trim().length <= max) linha = (linha + ' ' + p).trim()
    else { linhas.push(linha); linha = p }
  }
  if (linha) linhas.push(linha)
  return linhas.slice(0, 2)
})

const fmtData = (d?: string) => d ? new Date(d).toLocaleDateString('pt-BR') : null
const periodoStr = computed(() => {
  const ini = fmtData(props.layout.dataInicio)
  const fim = fmtData(props.layout.dataFim)
  if (!ini) return ''
  return fim ? `${ini} até ${fim}` : `A partir de ${ini}`
})

const aplicaEmStr = computed(() => {
  const ap = props.layout.aplicaEm ?? 'Todos'
  if (ap === 'Todos') return '✅ Válido para todos os produtos'
  return `✅ Válido para: ${ap}`
})

// Exporta como PNG
defineExpose({
  exportarPng: async () => {
    const svg = svgEl.value
    if (!svg) return
    const svgData = new XMLSerializer().serializeToString(svg)
    const img = new Image()
    img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svgData)
    await new Promise(res => { img.onload = res })
    const canvas = document.createElement('canvas')
    canvas.width = W; canvas.height = H
    const ctx = canvas.getContext('2d')!
    ctx.drawImage(img, 0, 0, W, H)
    const url = canvas.toDataURL('image/png')
    const a = document.createElement('a')
    a.href = url
    a.download = `arte_${props.layout.formato}_${Date.now()}.png`
    a.click()
  }
})
</script>
