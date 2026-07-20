// OCR de data de validade a partir de uma foto da etiqueta/embalagem.
// Roda 100% no navegador (Tesseract.js) — sem custo e sem enviar a imagem
// para nenhum servidor. O Tesseract é carregado sob demanda (dynamic import)
// para não pesar no bundle principal.

export interface ResultadoOcr {
  dataIso: string | null   // yyyy-MM-dd (melhor candidato de validade)
  candidatas: string[]     // todas as datas encontradas (yyyy-MM-dd), mais recente primeiro
  texto: string            // texto bruto reconhecido (para depuração/exibição)
}

let workerPromise: Promise<any> | null = null

async function obterWorker() {
  if (!workerPromise) {
    workerPromise = (async () => {
      const { createWorker } = await import('tesseract.js')
      // 'por' cobre acentos de rótulos (VÁLIDO, VENCIMENTO); dígitos são o essencial.
      const worker = await createWorker('por')
      return worker
    })()
  }
  return workerPromise
}

/** Converte dia/mês/ano (com ano de 2 ou 4 dígitos) em Date, validando faixas. */
function montarData(d: number, m: number, a: number): Date | null {
  if (a < 100) a += 2000
  if (m < 1 || m > 12 || d < 1 || d > 31 || a < 2000 || a > 2100) return null
  const dt = new Date(a, m - 1, d)
  // Rejeita datas inválidas (ex.: 31/02) que o JS "corrige" para o mês seguinte.
  if (dt.getMonth() !== m - 1 || dt.getDate() !== d) return null
  return dt
}

/** Último dia do mês (para validades no formato MM/AAAA). */
function fimDoMes(m: number, a: number): Date | null {
  if (a < 100) a += 2000
  if (m < 1 || m > 12 || a < 2000 || a > 2100) return null
  return new Date(a, m, 0)
}

function iso(dt: Date): string {
  const p = (n: number) => String(n).padStart(2, '0')
  return `${dt.getFullYear()}-${p(dt.getMonth() + 1)}-${p(dt.getDate())}`
}

/**
 * Extrai datas de um texto. Reconhece dd/mm/aaaa, dd/mm/aa e mm/aaaa, com
 * separadores / . - ou espaço. Retorna as datas ordenadas da mais recente
 * para a mais antiga — a validade costuma ser a data mais à frente na etiqueta
 * (posterior à fabricação).
 */
export function extrairDatas(texto: string): string[] {
  const achadas: Date[] = []
  const t = texto.replace(/[oO]/g, '0')   // OCR confunde O/0 em datas

  // dd sep mm sep aaaa|aa
  const reCompleta = /\b(\d{1,2})\s*[/.\-\s]\s*(\d{1,2})\s*[/.\-\s]\s*(\d{2,4})\b/g
  for (const m of t.matchAll(reCompleta)) {
    const dt = montarData(+m[1], +m[2], +m[3])
    if (dt) achadas.push(dt)
  }

  // mm sep aaaa (validade só mês/ano) — evita colidir com as já achadas exigindo
  // que não haja um terceiro grupo numérico logo em seguida.
  const reMesAno = /\b(\d{1,2})\s*[/.\-]\s*(\d{4})\b/g
  for (const m of t.matchAll(reMesAno)) {
    const dt = fimDoMes(+m[1], +m[2])
    if (dt) achadas.push(dt)
  }

  const unicas = new Map<string, Date>()
  for (const d of achadas) unicas.set(iso(d), d)
  return [...unicas.values()]
    .sort((a, b) => b.getTime() - a.getTime())
    .map(iso)
}

export async function lerDataValidadeDaImagem(file: File): Promise<ResultadoOcr> {
  const worker = await obterWorker()
  const { data } = await worker.recognize(file)
  const texto: string = data?.text ?? ''
  const candidatas = extrairDatas(texto)
  // Melhor candidato: a data mais à frente que ainda seja futura; se nenhuma for
  // futura, a mais recente encontrada.
  const hoje = new Date(); hoje.setHours(0, 0, 0, 0)
  const futura = candidatas.find(c => new Date(c + 'T12:00:00') >= hoje)
  return { dataIso: futura ?? candidatas[0] ?? null, candidatas, texto }
}
