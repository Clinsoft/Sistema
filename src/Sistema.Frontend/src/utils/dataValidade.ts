// Proteção na digitação de datas de validade.
// Em <input type="date">, digitar o ano com 2 dígitos ("27") grava "0027-04-22".
// corrigirAnoData converte ano < 100 para 20xx (0027 -> 2027), evitando o
// falso "vencido". Use em @blur do campo de validade.

/** Corrige ano de 2 dígitos (0027 -> 2027). Mantém o valor se não for uma data yyyy-mm-dd. */
export function corrigirAnoData(valor: string | null | undefined): string {
  if (!valor) return valor ?? ''
  const m = /^(\d{1,4})-(\d{2})-(\d{2})$/.exec(valor.trim())
  if (!m) return valor
  let ano = parseInt(m[1], 10)
  if (ano < 100) ano += 2000 // "27"/"0027" -> 2027
  return `${String(ano).padStart(4, '0')}-${m[2]}-${m[3]}`
}

/** true se a data (já corrigida) for anterior a hoje — para avisar validade vencida no cadastro. */
export function validadeVencida(valor: string | null | undefined): boolean {
  const s = corrigirAnoData(valor)
  if (!s) return false
  const d = new Date(s + 'T12:00:00')
  if (isNaN(d.getTime())) return false
  const hoje = new Date(); hoje.setHours(0, 0, 0, 0)
  return d < hoje
}
