/**
 * Utilitários para CPF, CNPJ e CPF/CNPJ.
 *
 * CNPJ Alfanumérico (Instrução Normativa RFB nº 2.229/2024):
 *   A partir de julho/2026 os 8 dígitos da raiz (posições 1–8) passam a aceitar
 *   letras maiúsculas A–Z além de 0–9. O formato de exibição permanece
 *   XX.XXX.XXX/XXXX-XX mas os "X" deixam de ser somente dígitos.
 *
 *   Impactos:
 *   - Não usar `\D` para "limpar" CNPJ (removeria as letras)
 *   - Não validar por length === 14 com apenas dígitos
 *   - Usar a função `cnpjRaw()` que remove pontuação mantendo alfanuméricos
 */

// ── Formatação ────────────────────────────────────────────────────────────────

/** Remove máscara do CNPJ mantendo letras (novo formato alfanumérico RFB). */
export function cnpjRaw(v: string): string {
  // Remove apenas . / - e espaços; preserva letras
  return v.replace(/[.\-/ ]/g, '').toUpperCase()
}

/** Remove máscara do CPF (apenas dígitos). */
export function cpfRaw(v: string): string {
  return v.replace(/\D/g, '')
}

/** Aplica máscara XX.XXX.XXX/XXXX-XX — funciona com alfanumérico. */
export function formatarCnpj(v: string): string {
  if (!v) return '-'
  const r = cnpjRaw(v)
  if (r.length !== 14) return v
  return `${r.slice(0,2)}.${r.slice(2,5)}.${r.slice(5,8)}/${r.slice(8,12)}-${r.slice(12)}`
}

/** Aplica máscara 000.000.000-00 no CPF. */
export function formatarCpf(v: string): string {
  const d = cpfRaw(v).slice(0, 11)
  if (d.length <= 3) return d
  if (d.length <= 6) return `${d.slice(0,3)}.${d.slice(3)}`
  if (d.length <= 9) return `${d.slice(0,3)}.${d.slice(3,6)}.${d.slice(6)}`
  return `${d.slice(0,3)}.${d.slice(3,6)}.${d.slice(6,9)}-${d.slice(9)}`
}

/**
 * Detecta se é CPF (11 chars alfanum.) ou CNPJ (14 chars alfanum.) e formata.
 * Usado em campos CPF/CNPJ unificados (cliente, destinatário NFC-e).
 */
export function formatarCpfCnpj(v: string): string {
  if (!v) return ''
  const r = cnpjRaw(v) // preserva letras, remove pontuação
  if (r.length <= 11) return formatarCpf(r)
  return formatarCnpj(r)
}

// ── Validação ─────────────────────────────────────────────────────────────────

/** Valida dígitos verificadores do CPF. */
export function validarCpf(v: string): boolean {
  const d = cpfRaw(v)
  if (d.length !== 11 || /^(.)\1+$/.test(d)) return false
  const calc = (len: number) =>
    d.slice(0, len).split('').reduce((s, c, i) => s + +c * (len + 1 - i), 0)
  const dv = (s: number) => { const r = (s * 10) % 11; return r >= 10 ? 0 : r }
  return dv(calc(9)) === +d[9] && dv(calc(10)) === +d[10]
}

/**
 * Valida dígitos verificadores do CNPJ (numérico e alfanumérico).
 * Algoritmo conforme Instrução Normativa RFB nº 2.229/2024.
 * Para CNPJ alfanumérico: letras convertem por (charCode - 48) antes do mod 11.
 */
export function validarCnpj(v: string): boolean {
  const r = cnpjRaw(v)
  if (r.length !== 14) return false
  if (/^(.)\1+$/.test(r)) return false // todos iguais

  const peso1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
  const peso2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]

  const charVal = (c: string) => c.charCodeAt(0) - 48 // '0'=0, 'A'=17, etc.

  const dv = (digits: string, pesos: number[]) => {
    const soma = digits.split('').reduce((s, c, i) => s + charVal(c) * pesos[i], 0)
    const r = soma % 11
    return r < 2 ? 0 : 11 - r
  }

  return dv(r.slice(0, 12), peso1) === charVal(r[12])
      && dv(r.slice(0, 13), peso2) === charVal(r[13])
}

/** Retorna true se for CPF válido ou CNPJ válido. */
export function validarCpfOuCnpj(v: string): boolean {
  const r = cnpjRaw(v)
  return r.length === 11 ? validarCpf(r) : validarCnpj(r)
}

// ── Máscara input ────────────────────────────────────────────────────────────

/**
 * Handler para @input em campo CPF/CNPJ unificado.
 * Detecta automaticamente e aplica a máscara correta.
 * Aceita CNPJ alfanumérico (letras nas posições 1–8 da raiz).
 */
export function maskCpfCnpj(v: string): string {
  // Mantém letras e dígitos, remove pontuação para re-formatar
  const raw = v.replace(/[.\-/ ]/g, '').toUpperCase().slice(0, 14)
  return formatarCpfCnpj(raw)
}

/**
 * Máscara progressiva para CNPJ alfanumérico (IN RFB 2.229/2024).
 * Formato: AA.AAA.AAA/DDDD-DD  (A = letra ou dígito, D = dígito)
 * Aplica os separadores conforme o usuário digita, sem esperar 14 chars.
 */
export function maskCnpj(v: string): string {
  // Remove pontuação, mantém letras e dígitos, limita 14 chars
  const raw = v.replace(/[.\-/\s]/g, '').toUpperCase().slice(0, 14)
  const r = raw

  // Aplica progressivamente: XX.XXX.XXX/XXXX-XX
  if (r.length <= 2)  return r
  if (r.length <= 5)  return `${r.slice(0,2)}.${r.slice(2)}`
  if (r.length <= 8)  return `${r.slice(0,2)}.${r.slice(2,5)}.${r.slice(5)}`
  if (r.length <= 12) return `${r.slice(0,2)}.${r.slice(2,5)}.${r.slice(5,8)}/${r.slice(8)}`
  return `${r.slice(0,2)}.${r.slice(2,5)}.${r.slice(5,8)}/${r.slice(8,12)}-${r.slice(12)}`
}
