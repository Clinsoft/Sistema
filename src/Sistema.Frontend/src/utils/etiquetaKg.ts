import QRCode from 'qrcode'
import { branding } from '@/branding'

/**
 * Template PADRÃO de etiqueta para produtos vendidos por peso (kg).
 * Impressão em 6 etiquetas por folha A4. QR Code gerado automaticamente
 * a partir de https://ecogranel.com.br/produtos/produto.php?p={slug}.
 */

export interface EtiquetaKgData {
  nome: string
  codigoPlu?: number | string | null
  precoVenda: number        // preço por KG (o template exibe por 100g)
  precoOriginal?: number | null  // preço CHEIO por KG (Black Friday: mostra "de" riscado)
  validade?: string | null  // yyyy-MM-dd
  descricao?: string | null // texto descritivo (descrição complementar)
  slug?: string | null      // usado no QR; se ausente, gerado do nome
}

const TEXTO_PADRAO = 'Produto 100% natural, sem conservantes. Conserve em local seco e arejado.'

function slugify(nome: string): string {
  return (nome ?? '').toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9\s-]/g, '').trim().replace(/\s+/g, '-')
}
function fmt(v: number) {
  return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}
function fmtData(d?: string | null) {
  if (!d) return ''
  const [y, m, dd] = String(d).slice(0, 10).split('-')
  return `${dd}/${m}/${y}`
}
function esc(s: string) {
  return (s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/** Gera o HTML de uma etiqueta kg (usado no preview e na impressão). */
async function cardHtml(it: EtiquetaKgData, o: EtiquetaKgOpts = {}): Promise<string> {
  const bf = !!o.blackFriday
  const url = branding.catalogoProdutoUrl + (it.slug || slugify(it.nome))
  let qr = ''
  try {
    // No Black Friday o QR é branco (fundo transparente) para aparecer sobre o preto.
    qr = await QRCode.toDataURL(url, { width: 240, margin: 1,
      ...(bf ? { color: { dark: '#ffffff', light: '#00000000' } } : {}) })
  } catch { /* ignora */ }
  const preco100 = (it.precoVenda ?? 0) / 10
  const cod = it.codigoPlu != null && it.codigoPlu !== '' ? String(it.codigoPlu) : ''
  const codFmt = /^\d+$/.test(cod) ? cod.padStart(4, '0') : cod
  if (bf) {
    const gid = 'pr' + Math.random().toString(36).slice(2, 8)
    const grad = `<defs><linearGradient id="${gid}" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffffff"/><stop offset=".5" stop-color="#dcdcdc"/><stop offset="1" stop-color="#7d7d7d"/></linearGradient></defs>`
    // Franjas prata (degradê) nos 4 cantos.
    const franja = `<g stroke="url(#${gid})" stroke-width="3" stroke-linecap="round"><line x1="0" y1="14" x2="14" y2="0"/><line x1="0" y1="23" x2="23" y2="0"/><line x1="0" y1="32" x2="32" y2="0"/></g>`
    const cantos = ['tl', 'tr', 'bl', 'br'].map(c =>
      `<svg class="bf-c ${c}" viewBox="0 0 40 40">${grad}${franja}</svg>`).join('')
    // Tagzinha de preço com "%".
    const tag = `<svg class="bf-tag" viewBox="0 0 24 24"><path d="M21.41 11.58 12.41 2.58A2 2 0 0 0 11 2H4a2 2 0 0 0-2 2v7a2 2 0 0 0 .59 1.41l9 9a2 2 0 0 0 2.82 0l7-7a2 2 0 0 0 0-2.83Z" fill="none" stroke="#fff" stroke-width="1.3"/><circle cx="6.5" cy="6.5" r="1.4" fill="#fff"/><text x="12.5" y="15.5" text-anchor="middle" font-family="Arial" font-size="7" font-weight="bold" fill="#fff">%</text></svg>`
    const friday = `<svg class="bf-friday" viewBox="0 0 300 62" preserveAspectRatio="xMidYMid meet">${grad}<text x="150" y="50" text-anchor="middle" font-family="Arial" font-weight="900" font-size="56" fill="url(#${gid})">FRIDAY</text></svg>`
    return `
    <div class="etq bf">
      ${cantos}
      <div class="bf-top">
        ${tag}
        <div class="bf-black">BLACK</div>
        ${friday}
      </div>
      <div class="bf-nome">${esc((it.nome || '').toUpperCase())}</div>
      ${codFmt ? `<div class="bf-cod">-${codFmt}-</div>` : ''}
      ${it.precoOriginal && it.precoOriginal > it.precoVenda
        ? `<div class="bf-de">de <s>R$ ${fmt((it.precoOriginal ?? 0) / 10)}</s> por</div>` : ''}
      <div class="bf-preco">${fmt(preco100)}</div>
      <div class="bf-100">${esc(o.rotuloPreco || 'cada 100g')}</div>
      ${it.validade ? `<div class="bf-hr"></div><div class="bf-val">Validade: <b>${fmtData(it.validade)}</b></div><div class="bf-hr"></div>` : ''}
      <div class="bf-foot">
        ${qr ? `<img class="bf-qr" src="${qr}"/>` : '<span></span>'}
        <div class="bf-frase">${esc(o.fraseRodape || 'APROVEITE A BLACK FRIDAY!')}</div>
      </div>
    </div>`
  }

  const plu = codFmt ? ` <span class="plu">-${codFmt}</span>` : ''
  return `
    <div class="etq">
      <div class="etq-wm"></div>
      <div class="etq-nome">${esc((it.nome || '').toUpperCase())}${plu}</div>
      <div class="etq-preco"><span class="v">${fmt(preco100)}</span><span class="l">${esc(o.rotuloPreco || 'cada 100g')}</span></div>
      ${it.validade ? `<div class="etq-val">Validade: <b>${fmtData(it.validade)}</b></div>` : ''}
      <div class="etq-desc">${esc(it.descricao || TEXTO_PADRAO)}</div>
      <div class="etq-rodape">
        ${qr ? `<div class="qr-wrap"><img class="qr" src="${qr}"/><div class="qr-saiba">Saiba mais pelo QR Code</div></div>` : '<span></span>'}
        <div class="frase">${esc(o.fraseRodape || 'Natural como deve ser!')}</div>
      </div>
    </div>`
}

export interface EtiquetaKgOpts {
  copiasPorItem?: number
  bordaCor?: string
  bordaEspessura?: number   // em px, como no preview
  marcaDaguaUrl?: string    // imagem de fundo (logo/semente); default = logo EcoGranel
  // Personalização do template (vindos do editor de etiquetas)
  rotuloPreco?: string      // texto sob o preço (default "cada 100g")
  fraseRodape?: string      // frase do rodapé
  corTexto?: string
  corPreco?: string
  corRotulo?: string
  fundoCor?: string
  marcaOpacidade?: number   // % (0-100)
  escalaNome?: number       // % do tamanho do nome
  escalaPreco?: number      // % do tamanho do preço
  blackFriday?: boolean     // tema preto Black Friday (fundo preto, texto branco, cabeçalho BLACK FRIDAY)
}

// Logo padrão da marca (servida na raiz do site).
const LOGO_PADRAO = () => branding.logoUrl

/** Baixa uma imagem e converte para data URL (garante que imprime no pop-up). */
async function toDataUrl(url: string): Promise<string> {
  if (!url) return ''
  if (url.startsWith('data:')) return url
  try {
    const abs = url.startsWith('http') ? url : window.location.origin + url
    const res = await fetch(abs)
    const blob = await res.blob()
    return await new Promise<string>(resolve => {
      const fr = new FileReader()
      fr.onload = () => resolve(fr.result as string)
      fr.onerror = () => resolve('')
      fr.readAsDataURL(blob)
    })
  } catch { return '' }
}

const buildCss = (cor: string, espessuraPx: number, marcaUrl: string, o: EtiquetaKgOpts) => {
  const bf        = !!o.blackFriday
  const corTexto  = bf ? '#ffffff' : (o.corTexto  || '#111')
  const corPreco  = bf ? '#ffffff' : (o.corPreco  || '#111')
  const corRotulo = bf ? '#e0e0e0' : (o.corRotulo || '#444')
  const corFrase  = bf ? '#ffffff' : cor
  const corBorda  = bf ? '#000000' : cor
  const corVal    = bf ? '#ffffff' : '#222'
  const fundo     = bf ? '#0a0a0a' : `linear-gradient(160deg,#ffffff 0%,${o.fundoCor || '#e8f5e9'} 60%,${o.fundoCor || '#e8f5e9'} 100%)`
  const opac      = (o.marcaOpacidade ?? 10) / 100
  const escNome   = (o.escalaNome  ?? 100) / 100
  const escPreco  = (o.escalaPreco ?? 100) / 100
  return `
  * { box-sizing: border-box; margin: 0; padding: 0; }
  @page { size: A4 portrait; margin: 6mm; }
  body { font-family: Arial, Helvetica, sans-serif; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
  .folha { display: grid; grid-template-columns: 1fr 1fr; grid-auto-rows: 92mm; gap: 4mm; }
  .etq {
    position: relative;
    border: ${espessuraPx}px solid ${corBorda}; border-radius: ${espessuraPx * 2 + 4}px; padding: 5mm;
    background: ${fundo};
    display: flex; flex-direction: column; overflow: hidden; page-break-inside: avoid;
  }
  ${marcaUrl && !bf ? `.etq-wm {
    position: absolute; inset: 0; z-index: 0;
    background: url("${marcaUrl}") center 45% / 62% no-repeat;
    opacity: ${opac}; pointer-events: none;
  }` : ''}
  .etq > *:not(.etq-wm) { position: relative; z-index: 1; }
  .etq.bf { align-items: center; text-align: center; justify-content: flex-start; padding: 4mm 5mm 4.5mm; }
  .etq.bf > svg.bf-c { position: absolute; width: 12mm; height: 12mm; z-index: 0; }
  .bf-c.tl { top: 0; left: 0; }
  .bf-c.tr { top: 0; right: 0; transform: scaleX(-1); }
  .bf-c.bl { bottom: 0; left: 0; transform: scaleY(-1); }
  .bf-c.br { bottom: 0; right: 0; transform: scale(-1, -1); }
  .bf-top { text-align: center; width: 100%; }
  .bf-tag { width: 8mm; height: 8mm; display: block; margin: 0 auto 0.5mm; }
  .bf-black { font-size: 11pt; letter-spacing: 6px; font-weight: 700; color: #fff; line-height: 1; padding-left: 6px; }
  .bf-friday { display: block; width: 56%; height: auto; margin: -0.5mm auto 0; }
  .bf-nome { font-size: 11pt; font-weight: 900; color: #fff; text-align: center; line-height: 1.1; margin-top: 1.5mm; }
  .bf-cod { font-size: 8.5pt; color: #bdbdbd; text-align: center; margin-top: 0.3mm; }
  .bf-de { font-size: 8.5pt; color: #cfcfcf; text-align: center; margin-top: 1mm; }
  .bf-de s { color: #9a9a9a; }
  .bf-preco { font-size: 33pt; font-weight: 900; color: #fff; line-height: 1; text-align: center; margin-top: 0.5mm; }
  .bf-100 { font-size: 8.5pt; color: #e0e0e0; text-align: center; margin-top: 1.5mm; }
  .bf-hr { width: 78%; height: 0; border-top: 1.5px solid #9a9a9a; margin: 1.5mm auto; }
  .bf-val { font-size: 9.5pt; color: #fff; text-align: center; }
  .bf-foot { margin-top: auto; display: flex; align-items: center; justify-content: space-between; width: 100%; }
  .bf-qr { width: 17mm; height: 17mm; }
  .bf-frase { font-size: 9.5pt; font-weight: 900; color: #fff; text-align: right; line-height: 1.15; max-width: 48%; }
  .etq-nome { font-size: ${(12 * escNome).toFixed(1)}pt; font-weight: 900; color: ${corTexto}; text-align: center; line-height: 1.15; }
  .etq-nome .plu { font-weight: 700; font-size: .85em; }
  .etq-preco { display: table; margin: 2mm auto 1mm; }
  .etq-preco .v { font-size: ${(40 * escPreco).toFixed(1)}pt; font-weight: 900; color: ${corPreco}; line-height: 1; }
  .etq-preco .l { display: block; font-size: 8pt; color: ${corRotulo}; text-align: right; }
  .etq-val { text-align: center; font-size: 10pt; color: ${corVal}; margin-bottom: 1mm; }
  .etq-desc { font-size: 8pt; color: #333; text-align: center; line-height: 1.35;
    flex: 1 1 auto; min-height: 0;
    display: -webkit-box; -webkit-line-clamp: 5; -webkit-box-orient: vertical; overflow: hidden; }
  .etq-rodape { flex: 0 0 auto; display: flex; align-items: flex-end; justify-content: space-between; margin-top: 2mm; }
  .qr-wrap { display: flex; flex-direction: column; align-items: center; }
  .etq-rodape .qr { width: 20mm; height: 20mm; }
  .qr-saiba { font-size: 6.5pt; font-weight: 900; color: ${corFrase}; text-align: center; margin-top: 0.5mm; }
  .etq-rodape .frase { font-size: 9pt; font-weight: 900; color: ${corFrase}; text-align: right; max-width: 50%; line-height: 1.2; }
`}

/**
 * Abre a janela de impressão com as etiquetas kg (6 por A4).
 * @param itens produtos a imprimir
 * @param copiasPorItem quantas etiquetas por produto (padrão 1)
 */
export async function imprimirEtiquetasKg(
  itens: EtiquetaKgData[], opts: number | EtiquetaKgOpts = 1): Promise<void> {
  // Compatível com a chamada antiga (segundo argumento = nº de cópias)
  const o: EtiquetaKgOpts = typeof opts === 'number' ? { copiasPorItem: opts } : opts
  const copiasPorItem = Math.max(1, o.copiasPorItem ?? 1)
  const bordaCor = o.bordaCor || '#2e7d32'
  const bordaEspessura = o.bordaEspessura && o.bordaEspessura > 0 ? o.bordaEspessura : 5

  const lista: EtiquetaKgData[] = []
  for (const it of itens) for (let i = 0; i < copiasPorItem; i++) lista.push(it)
  if (!lista.length) return

  // Semente/logo de fundo: usa a informada ou a logo padrão da EcoGranel.
  const marcaUrl = await toDataUrl(o.marcaDaguaUrl || LOGO_PADRAO())

  const cards = (await Promise.all(lista.map(it => cardHtml(it, o)))).join('')
  const html = `<!doctype html><html><head><meta charset="utf-8"><title>Etiquetas EcoGranel</title>
    <style>${buildCss(bordaCor, bordaEspessura, marcaUrl, o)}</style></head>
    <body><div class="folha">${cards}</div>
    <script>window.onload=function(){window.print();setTimeout(function(){window.close()},300)}<\/script>
    </body></html>`

  const w = window.open('', '_blank')
  if (!w) { alert('Permita pop-ups para imprimir as etiquetas.'); return }
  w.document.write(html)
  w.document.close()
}
