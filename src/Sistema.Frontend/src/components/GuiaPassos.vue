<template>
  <v-expand-transition>
    <v-alert
      v-if="visivel"
      :icon="false"
      variant="tonal"
      color="primary"
      rounded="xl"
      class="mb-4 guia-passos"
      border="start"
    >
      <div class="d-flex align-center mb-1">
        <v-icon size="20" color="primary" class="mr-2">mdi-map-marker-path</v-icon>
        <span class="text-subtitle-2 font-weight-bold flex-grow-1">{{ titulo }}</span>
        <v-btn
          size="x-small"
          variant="text"
          icon="mdi-close"
          title="Ocultar guia"
          @click="ocultar"
        />
      </div>
      <ol class="guia-lista">
        <li v-for="(passo, i) in passos" :key="i" class="text-body-2 mb-1">
          <span v-html="passo" />
        </li>
      </ol>
    </v-alert>
  </v-expand-transition>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const props = defineProps<{
  id: string          // chave única para lembrar se foi fechado
  titulo: string
  passos: string[]
}>()

const chave = `guia_oculto_${props.id}`
const visivel = ref(localStorage.getItem(chave) !== '1')

function ocultar() {
  visivel.value = false
  localStorage.setItem(chave, '1')
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
</style>
