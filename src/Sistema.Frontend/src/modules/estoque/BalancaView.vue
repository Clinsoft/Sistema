<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Exportação para Balança</div>
      <v-btn color="primary" prepend-icon="mdi-download" :loading="exportando"
        :disabled="!produtos.length" @click="exportar">
        Exportar ({{ produtos.length }} produtos)
      </v-btn>
    </div>

    <v-row>
      <!-- Configurações -->
      <v-col cols="12" md="5">
        <v-card rounded="xl" elevation="1" class="mb-4">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold">
            <v-icon icon="mdi-cog-outline" class="mr-2" />Configurações da Balança
          </v-card-title>
          <v-card-text class="pt-0">

            <!-- Modelo -->
            <div class="text-body-2 font-weight-medium mb-1">Modelo da balança *</div>
            <v-select v-model="cfg.modelo" :items="modelos" item-title="label" item-value="id"
              variant="outlined" density="compact" class="mb-4" hide-details />

            <v-divider class="mb-4" />

            <!-- Identificador pesável -->
            <div class="text-body-2 font-weight-medium mb-1">
              Identificador de produto pesável
              <v-tooltip text="Dígito inicial do código EAN-13 que a balança usa para identificar produto pesável. Geralmente 2." location="top">
                <template #activator="{ props }"><v-icon v-bind="props" icon="mdi-help-circle-outline" size="14" class="ml-1" /></template>
              </v-tooltip>
            </div>
            <v-slider v-model="cfg.identificadorPesavel" :min="1" :max="9" :step="1"
              thumb-label show-ticks="always" tick-size="4" color="primary" class="mb-3" />
            <div class="text-caption text-center text-medium-emphasis mb-4">
              Valor selecionado: <strong>{{ cfg.identificadorPesavel }}</strong>
              — barcode começa com {{ cfg.identificadorPesavel }}
            </div>

            <!-- Tamanho do código -->
            <div class="text-body-2 font-weight-medium mb-1">
              Tamanho do código do produto (dígitos)
              <v-tooltip location="top">
                <template #activator="{ props }"><v-icon v-bind="props" icon="mdi-help-circle-outline" size="14" class="ml-1" /></template>
                <span>Número de dígitos do PLU no EAN-13 da balança.<br>
                  Ex.: IP=2 + TC=5 + 5 dígitos valor + 1 check = 13 dígitos</span>
              </v-tooltip>
            </div>
            <v-btn-toggle v-model="cfg.tamanhoCodigoProduto" mandatory divided density="compact" class="mb-1">
              <v-btn :value="4" size="small">4 dígitos</v-btn>
              <v-btn :value="5" size="small">5 dígitos</v-btn>
              <v-btn :value="6" size="small">6 dígitos</v-btn>
            </v-btn-toggle>
            <div class="text-caption text-medium-emphasis mb-4">
              Estrutura EAN-13:
              <code>[{{ cfg.identificadorPesavel }}][{{ 'P'.repeat(cfg.tamanhoCodigoProduto) }}][{{ 'V'.repeat(11 - cfg.tamanhoCodigoProduto) }}][C]</code>
              <span class="ml-1">({{ cfg.tipoInformacao === 'PrecoTotal' ? 'V=preço' : 'V=peso' }})</span>
            </div>

            <!-- Tipo de informação -->
            <div class="text-body-2 font-weight-medium mb-2">
              Tipo de informação para balança
            </div>
            <v-btn-toggle v-model="cfg.tipoInformacao" mandatory divided density="compact" class="mb-2">
              <v-btn value="PrecoTotal" size="small" prepend-icon="mdi-currency-brl">Preço total</v-btn>
              <v-btn value="Peso" size="small" prepend-icon="mdi-weight-kilogram">Peso</v-btn>
            </v-btn-toggle>
            <div class="text-caption text-medium-emphasis mb-2">
              <span v-if="cfg.tipoInformacao === 'PrecoTotal'">
                A balança codifica o preço total (R$) no EAN-13 ao pesar o produto.
              </span>
              <span v-else>
                A balança codifica o peso (kg/g) no EAN-13 — o preço é calculado no caixa.
              </span>
            </div>

          </v-card-text>
        </v-card>

        <!-- Preview do EAN-13 -->
        <v-card rounded="xl" elevation="1" variant="tonal" color="primary">
          <v-card-text>
            <div class="text-body-2 font-weight-bold mb-2">Exemplo de EAN-13 gerado pela balança</div>
            <div v-if="produtos.length" class="font-mono text-body-1 letter-spacing-1">
              <span class="text-primary font-weight-bold">{{ cfg.identificadorPesavel }}</span><!--
              --><span class="text-success font-weight-bold">{{ exemploEan.plu }}</span><!--
              --><span class="text-warning font-weight-bold">{{ exemploEan.valor }}</span><!--
              --><span class="text-error font-weight-bold">{{ exemploEan.check }}</span>
            </div>
            <div v-if="produtos.length" class="text-caption text-medium-emphasis mt-1">
              <span class="text-primary">■ id pesável</span> &nbsp;
              <span class="text-success">■ PLU {{ exemploEan.pluNum }}</span> &nbsp;
              <span class="text-warning">■ {{ cfg.tipoInformacao === 'PrecoTotal' ? 'preço' : 'peso' }}</span> &nbsp;
              <span class="text-error">■ check</span>
            </div>
            <div v-else class="text-caption text-medium-emphasis">
              Nenhum produto pesável encontrado.
            </div>
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Lista de produtos -->
      <v-col cols="12" md="7">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 d-flex align-center text-body-1 font-weight-bold">
            <v-icon icon="mdi-scale" class="mr-2" />Produtos para Balança
            <v-spacer />
            <v-chip size="small" color="primary" variant="tonal">{{ produtos.length }} produtos</v-chip>
          </v-card-title>
          <div v-if="carregando" class="pa-6 text-center">
            <v-progress-circular indeterminate color="primary" />
          </div>
          <div v-else-if="!produtos.length" class="pa-6 text-center text-medium-emphasis">
            <v-icon icon="mdi-scale-off" size="48" class="mb-2" />
            <div>Nenhum produto pesável encontrado.</div>
            <div class="text-caption mt-1">
              Entram aqui os produtos ativos com unidade pesável (KG) ou marcados como
              Balança/Fracionado. O PLU usa o código da balança e, na falta dele, o código do produto.
            </div>
          </div>
          <v-data-table v-else :headers="headers" :items="produtos" density="compact" hover
            :items-per-page="20">
            <template #item.codigoPlu="{ item }">
              <code class="text-primary font-weight-bold">
                {{ item.codigoPlu?.toString().padStart(cfg.tamanhoCodigoProduto, '0') }}
              </code>
            </template>
            <template #item.precoVenda="{ item }">
              R$ {{ item.precoVenda?.toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }}
            </template>
            <template #item.validadeEmDias="{ item }">
              <span v-if="item.validadeEmDias">
                <v-chip size="x-small" color="info" variant="tonal">{{ item.validadeEmDias }} dias</v-chip>
                <span class="text-caption ml-1 text-medium-emphasis">→ {{ dataValidade(item.validadeEmDias) }}</span>
              </span>
              <span v-else class="text-medium-emphasis text-caption">Não definida</span>
            </template>
          </v-data-table>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const carregando = ref(false)
const exportando = ref(false)
const produtos = ref<any[]>([])

const modelos = [
  { id: 'FILIZOLA_SMART', label: 'Filizola SMART' },
  { id: 'TOLEDO_MGV5',    label: 'Toledo MGV5' },
  { id: 'TOLEDO_MGV6',    label: 'Toledo MGV6' },
  { id: 'TOLEDO_MGV7',    label: 'Toledo MGV7' },
  { id: 'RAMUZA_ATENA',   label: 'Ramuza Atena' },
  { id: 'URANO_INTEGRA',  label: 'Urano Integra' },
]

const cfg = reactive({
  modelo: 'FILIZOLA_SMART',
  identificadorPesavel: 2,
  tamanhoCodigoProduto: 5,
  tipoInformacao: 'PrecoTotal',
})

const headers = [
  { title: 'PLU', key: 'plu', width: 90 },
  { title: 'Produto', key: 'descricao' },
  { title: 'Preço', key: 'precoVenda', width: 110 },
  { title: 'Validade cadastrada', key: 'validadeEmDias', width: 200 },
]

const exemploEan = computed(() => {
  const p = produtos.value[0]
  if (!p) return { plu: '', valor: '', check: '', pluNum: '' }
  const pluNum = p.plu ?? 1
  const plu = pluNum.toString().padStart(cfg.tamanhoCodigoProduto, '0')
  const valorDigits = 11 - cfg.tamanhoCodigoProduto
  // Encode preço (sem separadores) nos dígitos de valor
  const valorRaw = cfg.tipoInformacao === 'PrecoTotal'
    ? Math.round(p.precoVenda * 100).toString().padStart(valorDigits, '0').slice(-valorDigits)
    : '0'.repeat(valorDigits)
  const completo = `${cfg.identificadorPesavel}${plu}${valorRaw}`
  // Cálculo do dígito verificador EAN-13
  let sum = 0
  for (let i = 0; i < 12; i++) sum += parseInt(completo[i]) * (i % 2 === 0 ? 1 : 3)
  const check = ((10 - (sum % 10)) % 10).toString()
  return { plu, valor: valorRaw, check, pluNum: pluNum.toString() }
})

function dataValidade(dias: number) {
  const d = new Date()
  d.setDate(d.getDate() + dias)
  return d.toLocaleDateString('pt-BR')
}

// Usa o mesmo critério da exportação (produtos pesáveis, com PLU resolvido a
// partir do CodigoPlu ou do código interno), evitando divergência tela × arquivo.
async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/balanca/produtos', { params: { empresaId: auth.empresaId } })
    produtos.value = r.data ?? []
  } catch { produtos.value = [] } finally {
    carregando.value = false
  }
}

async function exportar() {
  if (!produtos.value.length) return
  exportando.value = true
  try {
    const r = await api.get('/balanca/exportar', {
      params: {
        empresaId: auth.empresaId,
        modelo: cfg.modelo,
        identificadorPesavel: cfg.identificadorPesavel,
        tamanhoCodigoProduto: cfg.tamanhoCodigoProduto,
        tipoInformacao: cfg.tipoInformacao,
      },
      responseType: 'blob',
    })
    const url = URL.createObjectURL(r.data)
    const a = document.createElement('a')
    a.href = url
    a.download = `balanca_${cfg.modelo.toLowerCase()}_${new Date().toISOString().slice(0, 10)}.txt`
    a.click()
    URL.revokeObjectURL(url)
    notif.ok(`Arquivo exportado com ${produtos.value.length} produtos!`)
  } catch {
    notif.erro('Erro ao exportar arquivo para balança.')
  } finally {
    exportando.value = false
  }
}

onMounted(carregar)
</script>

<style>
.letter-spacing-1 { letter-spacing: 2px; font-family: monospace; font-size: 1.1rem; }
.font-mono { font-family: monospace; }
</style>
