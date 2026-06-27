<template>
  <v-app theme="ecoGranelLight">
    <v-main>
      <v-container max-width="700" class="py-8">
        <div v-if="carregando" class="d-flex justify-center py-16">
          <v-progress-circular indeterminate color="primary" size="60" />
        </div>

        <div v-else-if="!produto" class="text-center py-16">
          <v-icon icon="mdi-package-variant-remove" size="80" color="grey-lighten-2" class="mb-4" />
          <div class="text-h6 text-medium-emphasis">Produto não encontrado</div>
        </div>

        <template v-else>
          <!-- Cabeçalho -->
          <v-card rounded="xl" elevation="2" class="mb-4 overflow-hidden">
            <div class="pa-6">
              <div class="text-caption text-medium-emphasis text-uppercase mb-1">
                {{ produto.categoria }} · {{ produto.marca }}
              </div>
              <h1 class="text-h5 font-weight-bold mb-2">{{ produto.descricao }}</h1>
              <div v-if="produto.descricaoComplementar" class="text-body-2 text-medium-emphasis mb-3">
                {{ produto.descricaoComplementar }}
              </div>
              <div class="d-flex align-center gap-4">
                <div class="text-h4 font-weight-bold text-primary">
                  R$ {{ fmt(produto.precoVenda) }}
                </div>
                <v-chip v-if="produto.codigoBarras" size="small" variant="outlined" class="font-mono">
                  {{ produto.codigoBarras }}
                </v-chip>
              </div>
            </div>
          </v-card>

          <!-- Tabela Nutricional -->
          <v-card v-if="produto.tabelaNutricional" rounded="xl" elevation="1" class="mb-4">
            <v-card-title class="pa-4 pb-2 d-flex align-center">
              <v-icon icon="mdi-food-apple-outline" class="mr-2" color="success" />
              Informação Nutricional
            </v-card-title>
            <v-divider />
            <v-card-text class="pa-0">
              <div class="pa-4 pb-2">
                <span class="text-body-2 font-weight-medium">Porção: </span>
                <span class="text-body-2">{{ produto.tabelaNutricional.porcao }}</span>
              </div>
              <v-table density="compact">
                <tbody>
                  <tr v-for="item in nutrientes" :key="item.label" class="nutriente-row">
                    <td class="text-body-2 font-weight-medium py-2 pl-4">{{ item.label }}</td>
                    <td class="text-body-2 text-right pr-4">
                      {{ item.valor != null ? item.valor + item.unidade : '—' }}
                    </td>
                  </tr>
                </tbody>
              </v-table>
            </v-card-text>
          </v-card>

          <!-- Receitas -->
          <v-card v-if="produto.receitas?.length" rounded="xl" elevation="1" class="mb-4">
            <v-card-title class="pa-4 pb-2 d-flex align-center">
              <v-icon icon="mdi-chef-hat" class="mr-2" color="orange" />
              Receitas
            </v-card-title>
            <v-expansion-panels variant="accordion" flat>
              <v-expansion-panel v-for="r in produto.receitas" :key="r.titulo" :title="r.titulo">
                <v-expansion-panel-text>
                  <div v-if="r.descricao" class="text-body-2 text-medium-emphasis mb-3">{{ r.descricao }}</div>
                  <div v-if="r.ingredientes" class="mb-3">
                    <div class="text-caption font-weight-bold text-uppercase mb-1">Ingredientes</div>
                    <div class="text-body-2" style="white-space:pre-line">{{ r.ingredientes }}</div>
                  </div>
                  <div v-if="r.modoPreparo">
                    <div class="text-caption font-weight-bold text-uppercase mb-1">Modo de Preparo</div>
                    <div class="text-body-2" style="white-space:pre-line">{{ r.modoPreparo }}</div>
                  </div>
                  <div v-if="r.dicas" class="mt-3">
                    <v-alert type="info" variant="tonal" density="compact" :text="r.dicas" />
                  </div>
                  <div v-if="r.tempoPreparoMin || r.porcoes" class="d-flex gap-3 mt-2">
                    <v-chip v-if="r.tempoPreparoMin" size="small" prepend-icon="mdi-clock-outline">
                      {{ r.tempoPreparoMin }} min
                    </v-chip>
                    <v-chip v-if="r.porcoes" size="small" prepend-icon="mdi-account-group-outline">
                      {{ r.porcoes }} porções
                    </v-chip>
                  </div>
                </v-expansion-panel-text>
              </v-expansion-panel>
            </v-expansion-panels>
          </v-card>

          <!-- Sugestões -->
          <v-card v-if="produto.sugestoes?.length" rounded="xl" elevation="1" class="mb-4">
            <v-card-title class="pa-4 pb-2 d-flex align-center">
              <v-icon icon="mdi-lightbulb-outline" class="mr-2" color="amber" />
              Sugestões de Uso
            </v-card-title>
            <v-list density="compact" class="pa-2">
              <v-list-item v-for="s in produto.sugestoes" :key="s.titulo"
                :title="s.titulo" :subtitle="s.descricao"
                prepend-icon="mdi-check-circle-outline" color="success" rounded="lg" />
            </v-list>
          </v-card>

          <!-- Rodapé -->
          <div class="text-center text-caption text-medium-emphasis mt-6">
            Powered by EcoGranel · Loja de Produtos Naturais
          </div>
        </template>
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import api from '@/composables/useApi'

const route = useRoute()
const carregando = ref(true)
const produto = ref<any>(null)

function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }

const nutrientes = computed(() => {
  const t = produto.value?.tabelaNutricional
  if (!t) return []
  return [
    { label: 'Calorias', valor: t.calorias, unidade: ' kcal' },
    { label: 'Carboidratos', valor: t.carboidratosTotais, unidade: ' g' },
    { label: 'Proteínas', valor: t.proteinas, unidade: ' g' },
    { label: 'Gorduras Totais', valor: t.gordurasTotais, unidade: ' g' },
    { label: 'Gorduras Saturadas', valor: t.gordurasSaturadas, unidade: ' g' },
    { label: 'Gorduras Trans', valor: t.gordurasTrans, unidade: ' g' },
    { label: 'Fibras Alimentares', valor: t.fibrasDieteticas, unidade: ' g' },
    { label: 'Sódio', valor: t.sodio, unidade: ' mg' },
    { label: 'Açúcares', valor: t.acucares, unidade: ' g' },
  ].filter(n => n.valor != null)
})

onMounted(async () => {
  try {
    const r = await api.get(`/publico/produtos/${route.params.id}`)
    produto.value = r.data
  } catch { produto.value = null }
  finally { carregando.value = false }
})
</script>

<style scoped>
.nutriente-row:nth-child(even) { background: rgba(0,0,0,.03); }
.font-mono { font-family: monospace; }
</style>
