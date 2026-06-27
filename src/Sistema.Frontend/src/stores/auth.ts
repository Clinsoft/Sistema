import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '@/composables/useApi'

interface Usuario { id: string; nome: string; email: string; role: string }

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem('token'))
  const usuario = ref<Usuario | null>((() => {
    try { return JSON.parse(localStorage.getItem('usuario') ?? 'null') }
    catch { localStorage.removeItem('usuario'); return null }
  })())
  const empresaId = ref<string>(localStorage.getItem('empresaId') ?? '')

  const logado = computed(() => !!token.value)
  const iniciais = computed(() =>
    usuario.value?.nome.split(' ').map(n => n[0]).slice(0, 2).join('').toUpperCase() ?? '??'
  )

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
  }

  function sair() {
    token.value = null
    usuario.value = null
    empresaId.value = ''
    localStorage.clear()
    delete api.defaults.headers.common['Authorization']
    window.location.hash = '#/login'
  }

  // Restaura token no axios ao recarregar
  if (token.value)
    api.defaults.headers.common['Authorization'] = `Bearer ${token.value}`

  return { token, usuario, empresaId, logado, iniciais, login, sair }
})
