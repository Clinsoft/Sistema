// Converte o valor CRU de um enum de status (ex.: "EmSeparacao", "PagoParcialmente")
// no rótulo em português para exibir na tela ("Em separação", "Pago parcialmente").
// O valor do enum continua sendo usado na lógica/API — isto é só a apresentação.

// Palavras que precisam de acento (na forma minúscula, sem acento).
const ACENTOS: Record<string, string> = {
  separacao: 'separação',
  edicao: 'edição',
  digitacao: 'digitação',
  producao: 'produção',
  emissao: 'emissão',
  transmissao: 'transmissão',
  devolucao: 'devolução',
  inutilizada: 'inutilizada',
  denegadasefaz: 'denegada SEFAZ',
}

/** Embeleza um status: quebra o camelCase e acentua as palavras conhecidas. */
export function rotuloStatus(status?: string | null): string {
  if (!status) return ''
  // "EmSeparacao" -> "Em Separacao" -> ["Em","Separacao"]
  const palavras = status.replace(/([a-z0-9])([A-Z])/g, '$1 $2').split(' ')
  return palavras
    .map((p, i) => {
      const lower = p.toLowerCase()
      const acc = ACENTOS[lower] ?? lower
      // primeira palavra com inicial maiúscula; as demais em minúscula.
      return i === 0 ? acc.charAt(0).toUpperCase() + acc.slice(1) : acc
    })
    .join(' ')
}
