import { reactive } from 'vue'
import api from '@/composables/useApi'

/**
 * Marca da INSTÂNCIA (white-label). O mesmo build roda em várias instâncias
 * (ex.: EcoGranel e Natural Sistemas); cada uma responde a sua marca em GET /api/branding.
 * Os valores abaixo são os PADRÕES da EcoGranel — se a API não responder ou uma chave
 * faltar, o app continua idêntico à EcoGranel. Componentes e utilitários importam este
 * objeto reativo diretamente: `import { branding } from '@/branding'`.
 */
export interface Branding {
  nome: string
  slogan: string
  logoUrl: string
  siteUrl: string
  publicBaseUrl: string
  catalogoProdutoUrl: string
  emailContato: string
  corPrimaria: string
  corSecundaria: string
  corAccent: string
  corFundo: string
}

const PADRAO: Branding = {
  nome: 'EcoGranel',
  slogan: 'Produtos Naturais',
  logoUrl: '/logo-ecogranel.png',
  siteUrl: 'https://ecogranel.com.br',
  publicBaseUrl: 'https://sistema.ecogranel.com.br',
  catalogoProdutoUrl: 'https://ecogranel.com.br/produtos/produto.php?p=',
  emailContato: 'contato@ecogranel.com.br',
  corPrimaria: '#5C2D0C',
  corSecundaria: '#8B4513',
  corAccent: '#6AAF2E',
  corFundo: '#FAF7F4',
}

export const branding = reactive<Branding>({ ...PADRAO })

/** Busca a marca da instância na API (público). Silencioso: em falha mantém o padrão. */
export async function carregarBranding(): Promise<void> {
  try {
    const { data } = await api.get('/branding', { _quiet: true } as any)
    // Só sobrescreve as chaves que a API mandou (não zera com undefined).
    for (const k of Object.keys(PADRAO) as (keyof Branding)[]) {
      if (data?.[k]) (branding as any)[k] = data[k]
    }
  } catch { /* mantém o padrão */ }
}

/** Aplica título, favicon e cores do tema a partir da marca já carregada. */
export function aplicarBranding(vuetify: any): void {
  try { document.title = `${branding.nome} — ${branding.slogan}` } catch { /* ignora */ }
  try {
    let link = document.querySelector<HTMLLinkElement>('link[rel="icon"]')
    if (!link) {
      link = document.createElement('link')
      link.rel = 'icon'
      document.head.appendChild(link)
    }
    link.href = branding.logoUrl
  } catch { /* ignora */ }

  // Sobrescreve as cores dos temas Vuetify (mantém os nomes ecoGranelLight/Dark
  // usados nas telas, só troca as cores da marca).
  try {
    const themes = vuetify?.theme?.themes?.value ?? vuetify?.theme?.themes
    if (themes?.ecoGranelLight) {
      themes.ecoGranelLight.colors.primary = branding.corPrimaria
      themes.ecoGranelLight.colors.secondary = branding.corSecundaria
      themes.ecoGranelLight.colors.accent = branding.corAccent
      themes.ecoGranelLight.colors.background = branding.corFundo
    }
  } catch { /* ignora — mantém o tema padrão */ }
}
