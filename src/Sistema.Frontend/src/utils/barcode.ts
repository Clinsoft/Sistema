// Gera um código de barras EAN-13 (ou UPC-A de 12 dígitos) REAL e escaneável, em SVG.
// Retorna '' quando o código não é um EAN-13/UPC-A válido (ex.: vazio, EAN-8, com letras).

const L = ['0001101','0011001','0010011','0111101','0100011','0110001','0101111','0111011','0110111','0001011']
const G = ['0100111','0110011','0011011','0100001','0011101','0111001','0000101','0010001','0001001','0010111']
const R = ['1110010','1100110','1101100','1000010','1011100','1001110','1010000','1000100','1001000','1110100']
// padrão de paridade dos 6 dígitos da esquerda, definido pelo 1º dígito
const PARIDADE = ['LLLLLL','LLGLGG','LLGGLG','LLGGGL','LGLLGG','LGGLLG','LGGGLL','LGLGLG','LGLGGL','LGGLGL']

function digitoVerificadorEan13(d12: string): number {
  let soma = 0
  for (let i = 0; i < 12; i++) soma += (+d12[i]) * (i % 2 === 0 ? 1 : 3)
  return (10 - (soma % 10)) % 10
}

/** Normaliza para 13 dígitos válidos (calcula/valida o dígito verificador) ou null. */
function normalizarEan13(codigo: string): string | null {
  const d = (codigo || '').replace(/\D/g, '')
  let ean: string
  if (d.length === 13) ean = d
  else if (d.length === 12) ean = d + digitoVerificadorEan13(d)   // UPC-A → EAN-13
  else return null
  // confere o dígito verificador; se não bater, recalcula (tolera cadastro com DV errado)
  if (+ean[12] !== digitoVerificadorEan13(ean.slice(0, 12)))
    ean = ean.slice(0, 12) + digitoVerificadorEan13(ean.slice(0, 12))
  return ean
}

function modulosEan13(ean: string): string {
  const primeiro = +ean[0]
  const par = PARIDADE[primeiro]
  let bits = '101'                                   // guarda inicial
  for (let i = 0; i < 6; i++) bits += par[i] === 'L' ? L[+ean[1 + i]] : G[+ean[1 + i]]
  bits += '01010'                                    // guarda central
  for (let i = 0; i < 6; i++) bits += R[+ean[7 + i]]
  bits += '101'                                      // guarda final
  return bits                                        // 95 módulos
}

/** SVG de um EAN-13 escaneável. moduleW = largura de 1 módulo (px); height = altura total. */
export function ean13Svg(codigo: string, opt: { moduleW?: number; height?: number } = {}): string {
  const ean = normalizarEan13(codigo)
  if (!ean) return ''

  const mw = opt.moduleW ?? 1.6
  const h = opt.height ?? 34
  const quiet = 9                                    // zona de silêncio (cada lado)
  const bits = modulosEan13(ean)
  const totalMod = 95 + quiet * 2
  const w = totalMod * mw
  const barH = h - 9                                 // barras normais
  const guardH = h - 5                               // guardas um pouco mais altas

  let rects = ''
  for (let i = 0; i < bits.length; i++) {
    if (bits[i] !== '1') continue
    const guarda = i < 3 || (i >= 45 && i < 50) || i >= 92
    const x = (quiet + i) * mw
    rects += `<rect x="${x.toFixed(2)}" y="0" width="${mw}" height="${(guarda ? guardH : barH).toFixed(1)}"/>`
  }

  const ty = h - 1
  const fs = Math.max(6, Math.round(mw * 5.5))
  const xPrim = (quiet - 4) * mw
  const xEsq = (quiet + 3 + 21) * mw                 // centro dos 6 dígitos da esquerda
  const xDir = (quiet + 50 + 21) * mw                // centro dos 6 da direita

  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w.toFixed(1)}" height="${h}" `
    + `viewBox="0 0 ${w.toFixed(1)} ${h}" shape-rendering="crispEdges">`
    + `<rect x="0" y="0" width="${w.toFixed(1)}" height="${h}" fill="#fff"/>`
    + `<g fill="#000">${rects}</g>`
    + `<g fill="#000" font-family="monospace" font-size="${fs}" text-anchor="middle">`
    + `<text x="${xPrim.toFixed(1)}" y="${ty}">${ean[0]}</text>`
    + `<text x="${xEsq.toFixed(1)}" y="${ty}">${ean.slice(1, 7)}</text>`
    + `<text x="${xDir.toFixed(1)}" y="${ty}">${ean.slice(7, 13)}</text>`
    + `</g></svg>`
}
