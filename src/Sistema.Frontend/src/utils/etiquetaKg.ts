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
function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }
function fmtData(d?: string | null) {
  if (!d) return ''
  const [y, m, dd] = String(d).slice(0, 10).split('-')
  return `${dd}/${m}/${y}`
}
function esc(s: string) {
  return (s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

/** Gera o HTML de uma etiqueta kg (usado no preview e na impressão). */
async function cardHtml(it: EtiquetaKgData): Promise<string> {
  const url = QR_BASE + (it.slug || slugify(it.nome))
  let qr = ''
  try { qr = await QRCode.toDataURL(url, { width: 240, margin: 1 }) } catch { /* ignora */ }
  const preco100 = (it.precoVenda ?? 0) / 10
  const plu = it.codigoPlu ? ` <span class="plu">-${String(it.codigoPlu).padStart(4, '0')}</span>` : ''
  return `
    <div class="etq">
      <div class="etq-nome">${esc((it.nome || '').toUpperCase())}${plu}</div>
      <div class="etq-preco"><span class="v">${fmt(preco100)}</span><span class="l">cada 100g</span></div>
      ${it.validade ? `<div class="etq-val">Validade: <b>${fmtData(it.validade)}</b></div>` : ''}
      <div class="etq-desc">${esc(it.descricao || TEXTO_PADRAO)}</div>
      <div class="etq-rodape">
        ${qr ? `<img class="qr" src="${qr}"/>` : '<span></span>'}
        <div class="frase">Natural como<br>deve ser!</div>
      </div>
    </div>`
}

const CSS = `
  * { box-sizing: border-box; margin: 0; padding: 0; }
  @page { size: A4 portrait; margin: 6mm; }
  body { font-family: Arial, Helvetica, sans-serif; }
  .folha { display: grid; grid-template-columns: 1fr 1fr; grid-auto-rows: 92mm; gap: 4mm; }
  .etq {
    border: 1px solid #2e7d32; border-radius: 6px; padding: 5mm;
    background: linear-gradient(160deg,#ffffff 0%,#e8f5e9 60%,#c8e6c9 100%);
    display: flex; flex-direction: column; overflow: hidden; page-break-inside: avoid;
  }
  .etq-nome { font-size: 12pt; font-weight: 900; color: #111; text-align: center; line-height: 1.15; }
  .etq-nome .plu { font-weight: 700; font-size: .85em; }
  .etq-preco { text-align: center; margin: 2mm 0 1mm; }
  .etq-preco .v { font-size: 40pt; font-weight: 900; color: #111; line-height: 1; }
  .etq-preco .v::before { content: "R$ "; font-size: .4em; vertical-align: super; }
  .etq-preco .l { display: block; font-size: 8pt; color: #444; }
  .etq-val { text-align: center; font-size: 10pt; color: #222; margin-bottom: 1mm; }
  .etq-desc { font-size: 8pt; color: #333; text-align: center; line-height: 1.35; flex: 1; }
  .etq-rodape { display: flex; align-items: flex-end; justify-content: space-between; margin-top: 2mm; }
  .etq-rodape .qr { width: 20mm; height: 20mm; }
  .etq-rodape .frase { font-size: 9pt; font-weight: 900; color: #1b5e20; text-align: right; line-height: 1.2; }
`

/**
 * Abre a janela de impressão com as etiquetas kg (6 por A4).
 * @param itens produtos a imprimir
 * @param copiasPorItem quantas etiquetas por produto (padrão 1)
 */
export async function imprimirEtiquetasKg(itens: EtiquetaKgData[], copiasPorItem = 1): Promise<void> {
  const lista: EtiquetaKgData[] = []
  for (const it of itens) for (let i = 0; i < Math.max(1, copiasPorItem); i++) lista.push(it)
  if (!lista.length) return

  const cards = (await Promise.all(lista.map(cardHtml))).join('')
  const html = `<!doctype html><html><head><meta charset="utf-8"><title>Etiquetas EcoGranel</title>
    <style>${CSS}</style></head>
    <body><div class="folha">${cards}</div>
    <script>window.onload=function(){window.print();setTimeout(function(){window.close()},300)}<\/script>
    </body></html>`

  const w = window.open('', '_blank')
  if (!w) { alert('Permita pop-ups para imprimir as etiquetas.'); return }
  w.document.write(html)
  w.document.close()
}
