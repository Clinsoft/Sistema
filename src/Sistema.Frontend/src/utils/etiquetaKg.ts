import QRCode from 'qrcode'

/**
 * Template PADRÃO de etiqueta para produtos vendidos por peso (kg).
 * Impressão em 6 etiquetas por folha A4. QR Code gerado automaticamente
 * a partir de https://ecogranel.com.br/produtos/produto.php?p={slug}.
 */

export interface EtiquetaKgData {
  nome: string
  codigoPlu?: number | string | null
  precoVenda: number        // preço por KG (o template exibe por 100g)
  validade?: string | null  // yyyy-MM-dd
  descricao?: string | null // texto descritivo (descrição complementar)
  slug?: string | null      // usado no QR; se ausente, gerado do nome
}

const QR_BASE = 'https://ecogranel.com.br/produtos/produto.php?p='
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
  const url = QR_BASE + (it.slug || slugify(it.nome))
  let qr = ''
  try { qr = await QRCode.toDataURL(url, { width: 240, margin: 1 }) } catch { /* ignora */ }
  const preco100 = (it.precoVenda ?? 0) / 10
  const cod = it.codigoPlu != null && it.codigoPlu !== '' ? String(it.codigoPlu) : ''
  const codFmt = /^\d+$/.test(cod) ? cod.padStart(4, '0') : cod
  const plu = codFmt ? ` <span class="plu">-${codFmt}</span>` : ''
  return `
    <div class="etq">
      <div class="etq-wm"></div>
      <div class="etq-nome">${esc((it.nome || '').toUpperCase())}${plu}</div>
      <div class="etq-preco"><span class="v">${fmt(preco100)}</span><span class="l">${esc(o.rotuloPreco || 'cada 100g')}</span></div>
      ${it.validade ? `<div class="etq-val">Validade: <b>${fmtData(it.validade)}</b></div>` : ''}
      <div class="etq-desc">${esc(it.descricao || TEXTO_PADRAO)}</div>
      <div class="etq-rodape">
        ${qr ? `<img class="qr" src="${qr}"/>` : '<span></span>'}
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
}

// Logo padrão (semente EcoGranel) servida na raiz do site.
const LOGO_PADRAO = '/logo-ecogranel.png'

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
  const corTexto  = o.corTexto  || '#111'
  const corPreco  = o.corPreco  || '#111'
  const corRotulo = o.corRotulo || '#444'
  const fundoCor  = o.fundoCor  || '#e8f5e9'
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
    border: ${espessuraPx}px solid ${cor}; border-radius: ${espessuraPx * 2 + 4}px; padding: 5mm;
    background: linear-gradient(160deg,#ffffff 0%,${fundoCor} 60%,${fundoCor} 100%);
    display: flex; flex-direction: column; overflow: hidden; page-break-inside: avoid;
  }
  ${marcaUrl ? `.etq-wm {
    position: absolute; inset: 0; z-index: 0;
    background: url("${marcaUrl}") center 45% / 62% no-repeat;
    opacity: ${opac}; pointer-events: none;
  }` : ''}
  .etq > *:not(.etq-wm) { position: relative; z-index: 1; }
  .etq-nome { font-size: ${(12 * escNome).toFixed(1)}pt; font-weight: 900; color: ${corTexto}; text-align: center; line-height: 1.15; }
  .etq-nome .plu { font-weight: 700; font-size: .85em; }
  .etq-preco { display: table; margin: 2mm auto 1mm; }
  .etq-preco .v { font-size: ${(40 * escPreco).toFixed(1)}pt; font-weight: 900; color: ${corPreco}; line-height: 1; }
  .etq-preco .l { display: block; font-size: 8pt; color: ${corRotulo}; text-align: right; }
  .etq-val { text-align: center; font-size: 10pt; color: #222; margin-bottom: 1mm; }
  .etq-desc { font-size: 8pt; color: #333; text-align: center; line-height: 1.35; flex: 1;
    display: flex; align-items: center; justify-content: center; }
  .etq-rodape { display: flex; align-items: flex-end; justify-content: space-between; margin-top: 2mm; }
  .etq-rodape .qr { width: 20mm; height: 20mm; }
  .etq-rodape .frase { font-size: 9pt; font-weight: 900; color: ${cor}; text-align: right; line-height: 1.2; }
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
  const marcaUrl = await toDataUrl(o.marcaDaguaUrl || LOGO_PADRAO)

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
