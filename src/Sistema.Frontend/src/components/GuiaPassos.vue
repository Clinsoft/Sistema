<template>
  <div class="mb-4">
    <v-expand-transition>
      <v-alert
        v-if="aberto"
        :icon="false"
        variant="tonal"
        color="primary"
        rounded="xl"
        class="guia-passos"
        border="start"
      >
        <div class="d-flex align-center mb-1">
          <v-icon size="20" color="primary" class="mr-2">mdi-map-marker-path</v-icon>
          <span class="text-subtitle-2 font-weight-bold flex-grow-1">{{ titulo }}</span>
          <v-btn
            size="x-small"
            variant="text"
            prepend-icon="mdi-chevron-up"
            title="Ocultar guia (fica um botão para reabrir)"
            @click="fechar"
          >Ocultar</v-btn>
        </div>
        <ol class="guia-lista">
          <li v-for="(passo, i) in passos" :key="i" class="text-body-2 mb-1">
            <span v-html="passo" />
          </li>
        </ol>
      </v-alert>
    </v-expand-transition>

    <!-- Colapsado: botão discreto para reabrir o guia -->
    <v-btn
      v-if="!aberto"
      size="small"
      variant="tonal"
      color="primary"
      prepend-icon="mdi-help-circle-outline"
      class="guia-toggle"
      @click="abrir"
    >{{ titulo }}</v-btn>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const props = defineProps<{
  id: string          // chave única para lembrar o estado (aberto/oculto) por tela
  titulo: string
  passos: string[]
}>()

// Guarda o estado por id. Compatível com a chave antiga (guia_oculto_*): '1' = oculto.
const chave = `guia_oculto_${props.id}`
function lerOculto() {
  try { return localStorage.getItem(chave) === '1' } catch { return false }
}
const aberto = ref(!lerOculto())

function fechar() {
  aberto.value = false
  try { localStorage.setItem(chave, '1') } catch { /* ignora */ }
}
function abrir() {
  aberto.value = true
  try { localStorage.setItem(chave, '0') } catch { /* ignora */ }
}
</script>

<style scoped>
.guia-lista {
  padding-left: 1.4rem;
  margin: 0;
}
.guia-lista li {
  padding-left: 0.2rem;
}
.guia-lista li::marker {
  font-weight: 700;
  color: rgb(var(--v-theme-primary));
}
.guia-toggle {
  text-transform: none;
  letter-spacing: normal;
}
</style>
