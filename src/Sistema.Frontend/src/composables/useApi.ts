import axios from 'axios'
import { useNotifStore } from '@/stores/notif'

declare module 'axios' {
  interface AxiosRequestConfig {
    /** Suprime notificação de erro. Padrão: true para GET, false para outros métodos. */
    _quiet?: boolean
  }
}

const api = axios.create({
  baseURL: '/api',
  timeout: 30000,
})

api.interceptors.response.use(
  res => res,
  err => {
    const method = (err.config?.method ?? 'get').toLowerCase()
    const isGet = method === 'get'
    const quiet = err.config?._quiet ?? isGet

    if (!quiet) {
      const notif = useNotifStore()
      const d = err.response?.data
      let msg: string
      if (typeof d === 'string') {
        msg = d
      } else if (Array.isArray(d?.erros) && d.erros.length) {
        // Erros de validação do backend: [{ campo, mensagem }]
        msg = d.erros.map((x: any) => `${x.campo ? x.campo + ': ' : ''}${x.mensagem}`).join(' | ')
      } else if (d?.detalhe) {
        msg = `${d.mensagem ?? 'Erro'} — ${d.detalhe}`
      } else {
        msg = d?.mensagem ?? d?.detail ?? d?.title ?? err.message ?? 'Erro desconhecido'
      }
      notif.erro(msg)
    }
    if (err.response?.status === 401) {
      localStorage.clear()
      window.location.hash = '#/login'
    }
    return Promise.reject(err)
  }
)

export default api
