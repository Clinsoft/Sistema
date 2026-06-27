<template>
  <v-select
    v-model="mes"
    :items="opcoes"
    item-title="label"
    item-value="value"
    label="Mês"
    variant="outlined"
    density="compact"
    hide-details
    prepend-inner-icon="mdi-calendar-month-outline"
    @update:model-value="aplicar"
  />
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

const emit = defineEmits<{
  selecionar: [inicio: string, fim: string]
}>()

const hoje = new Date()
const mes = ref(`${hoje.getFullYear()}-${String(hoje.getMonth() + 1).padStart(2, '0')}`)

const opcoes = computed(() => {
  const lista: { label: string; value: string }[] = []
  const now = new Date()
  // Próximos 2 meses + 23 meses anteriores (janela de 25 meses)
  for (let i = -2; i <= 23; i++) {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1)
    const val = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
    const nome = d.toLocaleDateString('pt-BR', { month: 'long' })
    const label = `${nome.charAt(0).toUpperCase()}${nome.slice(1)} ${d.getFullYear()}`
    lista.push({ label, value: val })
  }
  return lista
})

function aplicar(val: string) {
  if (!val) return
  const [ano, m] = val.split('-').map(Number)
  const inicio = new Date(ano, m - 1, 1).toISOString().slice(0, 10)
  const fim    = new Date(ano, m, 0).toISOString().slice(0, 10)
  emit('selecionar', inicio, fim)
}
</script>
