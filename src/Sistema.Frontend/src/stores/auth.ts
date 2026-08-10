import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/composables/useApi'

interface Usuario { id: string; nome: string; email: string; role: string; localEstoqueId?: string | null }
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
  // Loja (local de estoque) atualmente selecionada — separa a operação por unidade.
  const lojas = ref<{ id: string; nome: string }[]>((() => {
    try { return JSON.parse(localStorage.getItem('lojas') ?? '[]') }
    catch { return [] }
  })())
  const lojaAtualId = ref<string | null>(localStorage.getItem('lojaAtualId') || null)
  const lojaAtual = computed(() => lojas.value.find(l => l.id === lojaAtualId.value) ?? null)

  const logado = computed(() => !!token.value)
  const iniciais = computed(() =>
    usuario.value?.nome.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase() ?? '??'
  )
  const empresaAtual = computed(() =>
    filiais.value.find(f => f.id === empresaId.value) ?? null
  )
  const temFiliais = computed(() => filiais.value.length > 1)

  async function login(email: string, senha: string) {
    // O backend responde achatado: { token, nome, perfil, expiracao, empresaId, usuarioId }.
    // Montamos o objeto `usuario` a partir daí — antes o código lia res.data.usuario
    // (inexistente), então auth.usuario.id ficava undefined e caixa/vendas iam sem usuário.
    const res = await api.post<{
      token: string; nome: string; perfil: string; empresaId: string; usuarioId: string
      localEstoqueId?: string | null
    }>('/auth/login', { email, senha })

    const u: Usuario = {
      id: res.data.usuarioId,
      nome: res.data.nome,
      email,
      role: res.data.perfil,
      localEstoqueId: res.data.localEstoqueId ?? null,
    }
    token.value = res.data.token
    usuario.value = u
    empresaId.value = res.data.empresaId
    localStorage.setItem('token', res.data.token)
    localStorage.setItem('usuario', JSON.stringify(u))
    localStorage.setItem('empresaId', res.data.empresaId)
    api.defaults.headers.common['Authorization'] = `Bearer ${res.data.token}`

    // Carrega grupo de filiais e as lojas (locais); default = unidade do colaborador.
    await carregarFiliais()
    await carregarLojas()
    lojaAtualId.value = (u.localEstoqueId ?? lojas.value[0]?.id) ?? null
    localStorage.setItem('lojaAtualId', lojaAtualId.value ?? '')
  }

  async function carregarLojas() {
    if (!empresaId.value) return
    try {
      const r = await api.get('/locais-estoque', { params: { empresaId: empresaId.value } })
      lojas.value = (r.data ?? []).map((l: any) => ({ id: l.id, nome: l.nome }))
      localStorage.setItem('lojas', JSON.stringify(lojas.value))
    } catch { lojas.value = [] }
  }

  function setLoja(id: string | null) {
    // Atendente é preso à loja de origem — não pode trocar nem ver "todas as lojas".
    if (usuario.value?.role === 'Atendente') {
      const fixa = usuario.value.localEstoqueId ?? null
      if (lojaAtualId.value !== fixa) {
        lojaAtualId.value = fixa
        localStorage.setItem('lojaAtualId', fixa ?? '')
      }
      return
    }
    lojaAtualId.value = id
    localStorage.setItem('lojaAtualId', id ?? '')
    window.location.reload()   // recarrega para reaplicar o filtro em todas as telas
  }

  // Garante que o atendente sempre fique na própria loja (mesmo com localStorage antigo).
  function fixarLojaAtendente() {
    if (usuario.value?.role === 'Atendente') {
      const fixa = usuario.value.localEstoqueId ?? null
      if (lojaAtualId.value !== fixa) {
        lojaAtualId.value = fixa
        localStorage.setItem('lojaAtualId', fixa ?? '')
      }
    }
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
    lojas, lojaAtualId, lojaAtual, carregarLojas, setLoja, fixarLojaAtendente,
    login, sair, carregarFiliais, trocarFilial,
  }
})
