import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/composables/useApi'

interface Usuario { id: string; nome: string; email: string; role: string }
interface EmpresaResumo { id: string; nomeFantasia: string; cnpj: string; tipoUnidade: string }

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('token'))
  const usuario = ref<Usuario | null>((() => {
    try { return JSON.parse(localStorage.getItem('usuario') ?? 'null') }
    catch { localStorage.removeItem('usuario'); return null }
  })())
  const empresaId = ref<string>(localStorage.getItem('empresaId') ?? '')
  const filiais = ref<EmpresaResumo[]>((() => {
    try { return JSON.parse(localStorage.getItem('filiais') ?? '[]') }
    catch { return [] }
  })())

  const logado = computed(() => !!token.value)
  const iniciais = computed(() =>
    usuario.value?.nome.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase() ?? '??'
  )
  const empresaAtual = computed(() =>
    filiais.value.find(f => f.id === empresaId.value) ?? null
  )
  const temFiliais = computed(() => filiais.value.length > 1)

  async function login(email: string, senha: string) {
    const res = await api.post<{ token: string; usuario: Usuario; empresaId: string }>(
      '/auth/login', { email, senha }
    )
    token.value = res.data.token
    usuario.value = res.data.usuario
    empresaId.value = res.data.empresaId
    localStorage.setItem('token', res.data.token)
    localStorage.setItem('usuario', JSON.stringify(res.data.usuario))
    localStorage.setItem('empresaId', res.data.empresaId)
    api.defaults.headers.common['Authorization'] = `Bearer ${res.data.token}`

    // Carrega grupo de filiais
    await carregarFiliais()
  }

  async function carregarFiliais() {
    if (!empresaId.value) return
    try {
      const r = await api.get(`/empresas/${empresaId.value}/grupo`)
      filiais.value = r.data
      localStorage.setItem('filiais', JSON.stringify(r.data))
    } catch {
      filiais.value = []
    }
  }

  function trocarFilial(id: string) {
    empresaId.value = id
    localStorage.setItem('empresaId', id)
    // Recarrega a página para limpar todos os dados em memória
    window.location.reload()
  }

  function sair() {
    token.value = null
    usuario.value = null
    empresaId.value = ''
    filiais.value = []
    localStorage.clear()
    delete api.defaults.headers.common['Authorization']
    window.location.hash = '#/login'
  }

  // Restaura token no axios ao recarregar
  if (token.value)
    api.defaults.headers.common['Authorization'] = `Bearer ${token.value}`

  return {
    token, usuario, empresaId, logado, iniciais,
    filiais, empresaAtual, temFiliais,
    login, sair, carregarFiliais, trocarFilial,
  }
})
