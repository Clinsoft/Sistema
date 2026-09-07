<template>
  <div>
    <div class="d-flex align-center mb-4 flex-wrap ga-2">
      <div class="text-h6 font-weight-bold flex-grow-1">
        <v-icon class="mr-1" color="primary">mdi-store-outline</v-icon>DRE por Loja
      </div>
      <v-select v-model.number="mes" label="Mês" :items="meses" item-title="label" item-value="value"
        variant="outlined" density="compact" hide-details style="max-width:160px" @update:model-value="carregar" />
      <v-text-field v-model.number="ano" label="Ano" type="number"
        variant="outlined" density="compact" hide-details style="max-width:110px" @change="carregar" />
      <v-btn color="primary" variant="tonal" prepend-icon="mdi-magnify" :loading="carregando" @click="carregar">Gerar</v-btn>
    </div>

    <GuiaPassos
      id="dre-lojas"
      titulo="Como funciona o DRE por Loja"
      :passos="[
        '<b>Receita</b> e <b>CMV</b> são exatos por loja (vêm das vendas de cada loja).',
        'As <b>despesas marcadas com a loja</b> (ex.: contas da escrituração de NF-e entram na loja que recebeu a mercadoria) contam direto para aquela loja.',
        'As <b>despesas sem loja</b> (compartilhadas/matriz) são <b>rateadas</b> entre as lojas pela participação no faturamento.',
        'Para melhorar a exatidão, marque a <b>Loja</b> nas contas em <b>Contas a Pagar</b>.',
      ]"
    />

    <div v-if="!carregando && dados">
      <!-- Cartões por loja -->
      <v-row dense class="mb-2">
        <v-col v-for="l in dados.lojas" :key="l.lojaId" cols="12" md="6">
          <v-card rounded="xl" elevation="2" class="pa-4 h-100">
            <div class="d-flex align-center mb-2">
              <v-icon color="blue-grey" class="mr-2">mdi-store</v-icon>
              <div class="text-subtitle-1 font-weight-bold flex-grow-1">{{ l.loja }}</div>
              <v-chip size="small" :color="l.resultado >= 0 ? 'success' : 'error'" variant="tonal" label>
                {{ l.resultado >= 0 ? 'Lucro' : 'Prejuízo' }} {{ pct(l.margemLiquida) }}%
              </v-chip>
            </div>
            <div class="d-flex justify-space-between py-1">
              <span>Receita</span><b>R$ {{ fmt(l.receita) }}</b>
            </div>
            <div class="d-flex justify-space-between py-1 text-medium-emphasis">
              <span>(−) CMV</span><span>R$ {{ fmt(l.cmv) }}</span>
            </div>
            <div class="d-flex justify-space-between py-1">
              <span>= Lucro bruto <span class="text-caption text-medium-emphasis">({{ pct(l.margemBruta) }}%)</span></span>
              <b>R$ {{ fmt(l.lucroBruto) }}</b>
            </div>
            <div v-if="l.perdas > 0" class="d-flex justify-space-between py-1 text-medium-emphasis">
              <span>(−) Perdas (vencidos)</span><span>R$ {{ fmt(l.perdas) }}</span>
            </div>
            <div class="d-flex justify-space-between py-1 text-medium-emphasis">
              <span>(−) Despesas diretas</span><span>R$ {{ fmt(l.despesasDiretas) }}</span>
            </div>
            <div class="d-flex justify-space-between py-1 text-medium-emphasis">
              <span>(−) Despesas rateadas <span class="text-caption">({{ pct(l.participacaoReceita) }}% do compart.)</span></span>
              <span>R$ {{ fmt(l.despesasRateadas) }}</span>
            </div>
            <v-divider class="my-2" />
            <div class="d-flex justify-space-between align-center">
              <span class="font-weight-bold">Resultado líquido</span>
              <span class="text-h6 font-weight-bold" :class="l.resultado >= 0 ? 'text-success' : 'text-error'">
                R$ {{ fmt(l.resultado) }}
              </span>
            </div>
          </v-card>
        </v-col>
      </v-row>

      <!-- Tabela comparativa -->
      <v-card rounded="xl" elevation="1" class="pa-2 mb-3">
        <v-table density="comfortable">
          <thead>
            <tr>
              <th>Loja</th>
              <th class="text-right">Receita</th>
              <th class="text-right">CMV</th>
              <th class="text-right">Lucro bruto</th>
              <th class="text-right">Desp. diretas</th>
              <th class="text-right">Desp. rateadas</th>
              <th class="text-right">Resultado</th>
              <th class="text-right">Margem</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="l in dados.lojas" :key="l.lojaId">
              <td class="font-weight-medium">{{ l.loja }}</td>
              <td class="text-right">{{ fmt(l.receita) }}</td>
              <td class="text-right text-medium-emphasis">{{ fmt(l.cmv) }}</td>
              <td class="text-right">{{ fmt(l.lucroBruto) }}</td>
              <td class="text-right text-medium-emphasis">{{ fmt(l.despesasDiretas) }}</td>
              <td class="text-right text-medium-emphasis">{{ fmt(l.despesasRateadas) }}</td>
              <td class="text-right font-weight-bold" :class="l.resultado >= 0 ? 'text-success' : 'text-error'">{{ fmt(l.resultado) }}</td>
              <td class="text-right">{{ pct(l.margemLiquida) }}%</td>
            </tr>
          </tbody>
          <tfoot>
            <tr class="font-weight-bold">
              <td>TOTAL</td>
              <td class="text-right">{{ fmt(dados.totais.receita) }}</td>
              <td colspan="2" class="text-right">Lucro bruto: {{ fmt(dados.totais.lucroBruto) }}</td>
              <td colspan="2"></td>
              <td class="text-right" :class="dados.totais.resultado >= 0 ? 'text-success' : 'text-error'">{{ fmt(dados.totais.resultado) }}</td>
              <td></td>
            </tr>
          </tfoot>
        </v-table>
      </v-card>

      <v-alert type="info" variant="tonal" density="comfortable" rounded="lg">
        <div class="text-body-2">
          <b>Despesas compartilhadas rateadas:</b> R$ {{ fmt(dados.compartilhado.total) }}
          (despesas R$ {{ fmt(dados.compartilhado.despesas) }}<span v-if="dados.compartilhado.perdas > 0"> + perdas R$ {{ fmt(dados.compartilhado.perdas) }}</span>).
          {{ dados.compartilhado.observacao }}
        </div>
        <div class="text-caption mt-1 text-medium-emphasis">
          Observação: o CMV é o custo do que foi <b>vendido</b> por loja; a compra de mercadoria e o imobilizado não entram como despesa (evita duplicidade). Financiamento entra só pelo juro.
        </div>
      </v-alert>
    </div>

    <v-skeleton-loader v-else-if="carregando" type="card, card" />
    <v-alert v-else type="info" variant="tonal">Selecione o período e clique em Gerar.</v-alert>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const carregando = ref(false)
const dados = ref<any>(null)
const mes = ref(new Date().getMonth() + 1)
const ano = ref(new Date().getFullYear())

const meses = [
  { value: 1, label: 'Janeiro' }, { value: 2, label: 'Fevereiro' },
  { value: 3, label: 'Março' },   { value: 4, label: 'Abril' },
  { value: 5, label: 'Maio' },    { value: 6, label: 'Junho' },
  { value: 7, label: 'Julho' },   { value: 8, label: 'Agosto' },
  { value: 9, label: 'Setembro' },{ value: 10, label: 'Outubro' },
  { value: 11, label: 'Novembro' },{ value: 12, label: 'Dezembro' },
]

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const pct = (v: number) => (v ?? 0).toFixed(1)

async function carregar() {
  carregando.value = true
  dados.value = null
  try {
    const res = await api.get('/financeiro/dre/por-lojas', {
      params: { empresaId: auth.empresaId, ano: ano.value, mes: mes.value },
    })
    dados.value = res.data
  } finally { carregando.value = false }
}

onMounted(carregar)
</script>
