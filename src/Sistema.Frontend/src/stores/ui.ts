import { defineStore } from 'pinia'
import { ref } from 'vue'

/**
 * Estado de UI compartilhado — hoje só o menu lateral (drawer), para que telas
 * em tela cheia (como o PDV no celular) possam abrir/fechar o menu sem depender
 * da app-bar global.
 */
export const useUiStore = defineStore('ui', () => {
  const drawer = ref(true)
  function abrirMenu() { drawer.value = true }
  function alternarMenu() { drawer.value = !drawer.value }
  return { drawer, abrirMenu, alternarMenu }
})
