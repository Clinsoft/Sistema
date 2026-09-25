import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/composables/useApi'

/**
 * Assinatura da empresa logada — controla o que o app libera por plano.
 * Carregada no login e no boot (App.vue). Enquanto não carrega, assume acesso
 * total (fail-open) para não esconder telas por engano; o gate REAL é no backend.
 */
export interface AssinaturaInfo {
  temAssinatura: boolean
  podeUsar: boolean
  situacao: string            // TrialAtivo | TrialExpirado | Ativa | EmTolerancia | Bloqueada | Cancelada
  plano: string
  planoEfetivo: string
  status: string
  recursos: string[]          // nomes de Recurso liberados (ex.: 'Pdv','Fiscal','Whatsapp')
  limites: { maxUsuarios: number; maxLojas: number }
  trialAte: string | null
  diasRestantesTrial: number
  proximoVencimento: string | null
}

const PERMISSIVO: AssinaturaInfo = {
  temAssinatura: false, podeUsar: true, situacao: 'Ativa', plano: 'Ilimitado',
  planoEfetivo: 'Ilimitado', status: 'Ativa', recursos: [], limites: { maxUsuarios: 9e9, maxLojas: 9e9 },
  trialAte: null, diasRestantesTrial: 0, proximoVencimento: null,
}

export const useAssinaturaStore = defineStore('assinatura', () => {
  const info = ref<AssinaturaInfo>({ ...PERMISSIVO })
  const carregado = ref(false)

  async function carregar() {
    try {
      const { data } = await api.get<AssinaturaInfo>('/minha-assinatura', { _quiet: true } as any)
      info.value = data
    } catch {
      info.value = { ...PERMISSIVO }   // fail-open na UI; backend é a trava
    } finally {
      carregado.value = true
    }
  }

  // Empresa sem assinatura (instalações antigas) = tudo liberado.
  const irrestrito = computed(() => !info.value.temAssinatura)

  function temRecurso(nome: string): boolean {
    if (irrestrito.value) return true
    return info.value.recursos.includes(nome)
  }

  const podeUsar = computed(() => info.value.podeUsar)
  const emTrial = computed(() => info.value.situacao === 'TrialAtivo')
  const diasTrial = computed(() => info.value.diasRestantesTrial)
  const plano = computed(() => info.value.plano)
  const situacao = computed(() => info.value.situacao)
  const bloqueado = computed(() => info.value.temAssinatura && !info.value.podeUsar)

  return { info, carregado, carregar, temRecurso, podeUsar, emTrial, diasTrial, plano, situacao, bloqueado, irrestrito }
})
