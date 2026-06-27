import axios from 'axios'
import { useNotifStore } from '@/stores/notif'

const api = axios.create({
  baseURL: '/api',
  timeout: 30000,
})

api.interceptors.response.use(
  res => res,
  err => {
    const notif = useNotifStore()
    const msg = err.response?.data?.detail
      ?? err.response?.data?.title
      ?? err.response?.data
      ?? err.message
      ?? 'Erro desconhecido'
    notif.erro(typeof msg === 'string' ? msg : JSON.stringify(msg))
    if (err.response?.status === 401) {
      localStorage.clear()
      window.location.hash = '#/login'
    }
    return Promise.reject(err)
  }
)

export default api
