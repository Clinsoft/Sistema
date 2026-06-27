import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useNotifStore = defineStore('notif', () => {
  const visivel = ref(false)
  const mensagem = ref('')
  const cor = ref<'success' | 'error' | 'warning' | 'info'>('success')

  function mostrar(msg: string, tipo: typeof cor.value = 'success') {
    mensagem.value = msg
    cor.value = tipo
    visivel.value = true
  }

  function fechar() { visivel.value = false }

  const ok = (msg: string) => mostrar(msg, 'success')
  const erro = (msg: string) => mostrar(msg, 'error')
  const aviso = (msg: string) => mostrar(msg, 'warning')

  return { visivel, mensagem, cor, ok, erro, aviso, fechar }
})
