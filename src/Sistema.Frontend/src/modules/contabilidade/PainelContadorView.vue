<template>
  <v-app>
    <v-app-bar color="primary" elevation="2">
      <v-app-bar-title>
        <v-icon icon="mdi-calculator-variant-outline" class="mr-2" />
        Painel do Contador
      </v-app-bar-title>
      <template #append>
        <div class="text-caption text-medium-emphasis mr-3">
          {{ nomeContador }}
        </div>
        <v-btn icon="mdi-logout" variant="text" title="Sair" @click="sair" />
      </template>
    </v-app-bar>

    <v-main>
      <v-container class="py-6" max-width="1100">

        <!-- Login do contador -->
        <v-card v-if="!logado" rounded="xl" elevation="3" max-width="420" class="mx-auto mt-12 pa-6">
          <div class="text-h6 font-weight-bold mb-1">Acesso do Contador</div>
          <div class="text-body-2 text-medium-emphasis mb-4">
            Use o e-mail cadastrado pelo lojista para acessar.
          </div>
          <v-form @submit.prevent="login">
            <v-text-field v-model="loginForm.email" label="E-mail" type="email"
              variant="outlined" density="compact" class="mb-3"
              :rules="[v => !!v || 'Obrigatório']" />
            <v-text-field v-model="loginForm.senha" label="Senha"
              :type="mostrarSenha ? 'text' : 'password'"
              variant="outlined" density="compact" class="mb-4"
              :append-inner-icon="mostrarSenha ? 'mdi-eye-off' : 'mdi-eye'"
              @click:append-inner="mostrarSenha = !mostrarSenha"
              :rules="[v => !!v || 'Obrigatório']" />
            <v-alert v-if="erroLogin" type="error" variant="tonal" density="compact" class="mb-3">
              {{ erroLogin }}
            </v-alert>
            <v-btn type="submit" color="primary" block :loading="fazendoLogin">Entrar</v-btn>
          </v-form>
        </v-card>

        <!-- Painel principal -->
        <div v-else>
          <div class="text-h6 font-weight-bold mb-4">Bem-vindo, {{ nomeContador }}</div>

          <v-tabs v-model="aba" class="mb-4">
            <v-tab value="xml">
              <v-icon icon="mdi-file-xml-box" class="mr-1" />
              Download XML
            </v-tab>
            <v-tab value="fiscal">
              <v-icon icon="mdi-chart-bar" class="mr-1" />
              Resumo Fiscal
            </v-tab>
            <v-tab value="empresa">
              <v-icon icon="mdi-domain" class="mr-1" />
              Dados da Empresa
            </v-tab>
          </v-tabs>

          <!-- ── Download XML ──────────────────────────── -->
          <div v-if="aba === 'xml'">
            <v-card rounded="xl" elevation="1" class="mb-4 pa-4">
              <v-row dense align="center">
                <v-col cols="12" md="3">
                  <v-select v-model="xmlAno" :items="anos" label="Ano"
                    variant="outlined" density="compact" hide-details
                    @update:model-value="carregarCompetencias" />
                </v-col>
                <v-col cols="12" md="3">
                  <v-select v-model="xmlModelo" label="Tipo"
                    :items="[{title:'Todos',value:''},{title:'Apenas NF-e',value:'NFe'},{title:'Apenas NFC-e',value:'NFCe'}]"
                    variant="outlined" density="compact" hide-details />
                </v-col>
              </v-row>
            </v-card>

            <v-card rounded="xl" elevation="1">
              <v-data-table :headers="headersXml" :items="competencias"
                :loading="carregandoComp" density="comfortable" hover>
                <template #item.mes="{ item }">
                  {{ nomeMes(item.mes) }} / {{ item.ano }}
                </template>
                <template #item.qtdNFe="{ item }">
                  <v-chip size="small" color="primary" variant="tonal" v-if="item.qtdNFe">
                    {{ item.qtdNFe }} NF-e
                  </v-chip>
                  <span v-else class="text-medium-emphasis">—</span>
                </template>
                <template #item.qtdNFCe="{ item }">
                  <v-chip size="small" color="success" variant="tonal" v-if="item.qtdNFCe">
                    {{ item.qtdNFCe }} NFC-e
                  </v-chip>
                  <span v-else class="text-medium-emphasis">—</span>
                </template>
                <template #item.actions="{ item }">
                  <v-btn size="small" color="primary" variant="tonal"
                    prepend-icon="mdi-download-box-outline"
                    :loading="baixando === item.ano + '-' + item.mes"
                    @click="baixarXml(item.ano, item.mes)">
                    Baixar ZIP
                  </v-btn>
                </template>
              </v-data-table>
            </v-card>
          </div>

          <!-- ── Resumo Fiscal ─────────────────────────── -->
          <div v-if="aba === 'fiscal'">
            <v-card rounded="xl" elevation="1" class="mb-4 pa-4">
              <v-row dense align="center">
                <v-col cols="6" md="2">
                  <v-select v-model="rfAno" :items="anos" label="Ano"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6" md="2">
                  <v-select v-model="rfMes" :items="listaMeses" label="Mês"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col>
                  <v-btn color="primary" variant="tonal" :loading="carregandoRF"
                    @click="carregarResumo">Consultar</v-btn>
                </v-col>
              </v-row>
            </v-card>

            <div v-if="resumo">
              <v-row dense class="mb-4">
                <v-col cols="12" md="6">
                  <v-card rounded="xl" elevation="1">
                    <v-card-title class="text-subtitle-2 pa-3 pb-1">Saídas (Emitidas)</v-card-title>
                    <v-list density="compact">
                      <v-list-item v-for="s in resumo.saidas" :key="s.modelo">
                        <template #title>{{ s.modelo }}</template>
                        <template #subtitle>{{ s.qtd }} documentos</template>
                        <template #append>
                          <div class="text-right">
                            <div class="text-body-2 font-weight-bold">R$ {{ fmt(s.totalProdutos) }}</div>
                            <div class="text-caption text-medium-emphasis">
                              ICMS R$ {{ fmt(s.totalIcms) }} · PIS R$ {{ fmt(s.totalPis) }} · COFINS R$ {{ fmt(s.totalCofins) }}
                            </div>
                          </div>
                        </template>
                      </v-list-item>
                      <v-list-item v-if="!resumo.saidas?.length">
                        <template #title><span class="text-medium-emphasis">Nenhuma saída no período</span></template>
                      </v-list-item>
                    </v-list>
                  </v-card>
                </v-col>
                <v-col cols="12" md="6">
                  <v-card rounded="xl" elevation="1">
                    <v-card-title class="text-subtitle-2 pa-3 pb-1">Entradas (Recebidas)</v-card-title>
                    <v-list density="compact">
                      <v-list-item v-if="resumo.entradas?.qtd">
                        <template #title>{{ resumo.entradas.qtd }} notas recebidas</template>
                        <template #append>
                          <div class="text-right">
                            <div class="text-body-2 font-weight-bold">R$ {{ fmt(resumo.entradas.totalProdutos) }}</div>
                            <div class="text-caption text-medium-emphasis">
                              ICMS R$ {{ fmt(resumo.entradas.totalIcms) }}
                            </div>
                          </div>
                        </template>
                      </v-list-item>
                      <v-list-item v-else>
                        <template #title><span class="text-medium-emphasis">Nenhuma entrada no período</span></template>
                      </v-list-item>
                    </v-list>
                  </v-card>
                </v-col>
              </v-row>
            </div>
            <v-card v-else-if="!carregandoRF" rounded="xl" elevation="1" class="pa-6 text-center">
              <v-icon icon="mdi-chart-bar" size="48" color="medium-emphasis" />
              <div class="text-body-2 text-medium-emphasis mt-2">Selecione o período e clique em Consultar.</div>
            </v-card>
          </div>

          <!-- ── Dados da Empresa ──────────────────────── -->
          <div v-if="aba === 'empresa'">
            <v-card v-if="empresa" rounded="xl" elevation="1" class="pa-5">
              <div class="text-subtitle-1 font-weight-bold mb-3">Identificação</div>
              <v-row dense>
                <v-col cols="12" md="6">
                  <div class="text-caption text-medium-emphasis">Razão Social</div>
                  <div class="text-body-1 font-weight-medium">{{ empresa.razaoSocial }}</div>
                </v-col>
                <v-col cols="12" md="6">
                  <div class="text-caption text-medium-emphasis">Nome Fantasia</div>
                  <div class="text-body-1">{{ empresa.nomeFantasia }}</div>
                </v-col>
                <v-col cols="12" md="4">
                  <div class="text-caption text-medium-emphasis mt-2">CNPJ</div>
                  <div class="text-body-1">{{ empresa.cnpj }}</div>
                </v-col>
                <v-col cols="12" md="4">
                  <div class="text-caption text-medium-emphasis mt-2">IE</div>
                  <div class="text-body-1">{{ empresa.inscricaoEstadual || '—' }}</div>
                </v-col>
                <v-col cols="12" md="4">
                  <div class="text-caption text-medium-emphasis mt-2">Regime Tributário</div>
                  <v-chip size="small" color="primary" variant="tonal">{{ empresa.regimeTributario }}</v-chip>
                </v-col>
                <v-col cols="12" class="mt-3">
                  <div class="text-caption text-medium-emphasis">Endereço</div>
                  <div class="text-body-1">
                    {{ empresa.logradouro }}, {{ empresa.numero }}
                    <span v-if="empresa.complemento"> — {{ empresa.complemento }}</span>,
                    {{ empresa.bairro }}, {{ empresa.cidade }}/{{ empresa.uf }} — CEP {{ empresa.cep }}
                  </div>
                </v-col>
              </v-row>

              <v-divider class="my-4" />
              <div class="text-subtitle-1 font-weight-bold mb-3">Configuração Fiscal</div>
              <v-row dense v-if="configFiscal">
                <v-col cols="6" md="3">
                  <div class="text-caption text-medium-emphasis">Série NF-e</div>
                  <div class="text-body-2">{{ configFiscal.serieNFe }}</div>
                </v-col>
                <v-col cols="6" md="3">
                  <div class="text-caption text-medium-emphasis">Série NFC-e</div>
                  <div class="text-body-2">{{ configFiscal.serieNFCe }}</div>
                </v-col>
                <v-col cols="6" md="3">
                  <div class="text-caption text-medium-emphasis">Ambiente</div>
                  <v-chip size="small" :color="configFiscal.ambiente===1?'error':'warning'" variant="tonal">
                    {{ configFiscal.ambiente === 1 ? 'Produção' : 'Homologação' }}
                  </v-chip>
                </v-col>
                <v-col cols="6" md="3">
                  <div class="text-caption text-medium-emphasis">UF</div>
                  <div class="text-body-2">{{ configFiscal.uf }}</div>
                </v-col>
              </v-row>
              <div v-else class="text-body-2 text-medium-emphasis">Configuração fiscal não disponível.</div>
            </v-card>
            <v-skeleton-loader v-else type="article" rounded="xl" />
          </div>
        </div>
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import axios from 'axios'

const API = '/api'

const logado = ref(false)
const nomeContador = ref('')
const token = ref('')
const empresaId = ref('')

const loginForm = ref({ email: '', senha: '' })
const erroLogin = ref('')
const fazendoLogin = ref(false)
const mostrarSenha = ref(false)

const aba = ref('xml')

// XML
const competencias = ref<any[]>([])
const carregandoComp = ref(false)
const baixando = ref('')
const xmlAno = ref(new Date().getFullYear())
const xmlModelo = ref('')
const anos = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i)

const mesesNome = ['Janeiro','Fevereiro','Março','Abril','Maio','Junho',
                   'Julho','Agosto','Setembro','Outubro','Novembro','Dezembro']
function nomeMes(m: number) { return mesesNome[m - 1] }
const listaMeses = mesesNome.map((n, i) => ({ title: n, value: i + 1 }))

const headersXml = [
  { title: 'Competência', key: 'mes' },
  { title: 'NF-e', key: 'qtdNFe', width: 110 },
  { title: 'NFC-e', key: 'qtdNFCe', width: 110 },
  { title: 'Total', key: 'total', width: 80 },
  { title: '', key: 'actions', sortable: false, width: 140 },
]

// Resumo fiscal
const resumo = ref<any>(null)
const carregandoRF = ref(false)
const rfAno = ref(new Date().getFullYear())
const rfMes = ref(new Date().getMonth() + 1)

// Empresa
const empresa = ref<any>(null)
const configFiscal = ref<any>(null)

function apiHeaders() {
  return { Authorization: `Bearer ${token.value}` }
}

async function login() {
  erroLogin.value = ''
  fazendoLogin.value = true
  try {
    const r = await axios.post(`${API}/auth/login`, loginForm.value)
    const data = r.data
    if (!data.perfis?.includes('Contador')) {
      erroLogin.value = 'Acesso negado. Este painel é exclusivo para contadores.'
      return
    }
    token.value = data.token
    nomeContador.value = data.nome
    empresaId.value = data.empresaId
    logado.value = true
    carregarDados()
  } catch {
    erroLogin.value = 'E-mail ou senha inválidos.'
  } finally {
    fazendoLogin.value = false }
}

function sair() {
  logado.value = false
  token.value = ''
  nomeContador.value = ''
  empresaId.value = ''
  loginForm.value = { email: '', senha: '' }
}

async function carregarDados() {
  await Promise.all([carregarCompetencias(), carregarEmpresa()])
}

async function carregarCompetencias() {
  carregandoComp.value = true
  try {
    const r = await axios.get(`${API}/contabilidade/xml/competencias`,
      { headers: apiHeaders(), params: { empresaId: empresaId.value } })
    competencias.value = r.data.filter((c: any) => c.ano === xmlAno.value)
  } finally { carregandoComp.value = false }
}

async function baixarXml(ano: number, mes: number) {
  baixando.value = `${ano}-${mes}`
  try {
    const r = await axios.get(`${API}/contabilidade/xml/download`, {
      headers: apiHeaders(),
      params: { empresaId: empresaId.value, ano, mes, modelo: xmlModelo.value || undefined },
      responseType: 'blob',
    })
    const url = URL.createObjectURL(new Blob([r.data], { type: 'application/zip' }))
    const a = document.createElement('a')
    a.href = url
    a.download = `XMLs_${nomeMes(mes)}_${ano}.zip`
    a.click()
    URL.revokeObjectURL(url)
  } finally { baixando.value = '' }
}

async function carregarResumo() {
  carregandoRF.value = true
  try {
    const r = await axios.get(`${API}/contabilidade/xml/resumo-fiscal`, {
      headers: apiHeaders(),
      params: { empresaId: empresaId.value, ano: rfAno.value, mes: rfMes.value },
    })
    resumo.value = r.data
  } finally { carregandoRF.value = false }
}

async function carregarEmpresa() {
  try {
    const r = await axios.get(`${API}/empresas/${empresaId.value}`, { headers: apiHeaders() })
    empresa.value = r.data
    const rc = await axios.get(`${API}/fiscal/configuracao`, {
      headers: apiHeaders(),
      params: { empresaId: empresaId.value },
    })
    configFiscal.value = rc.data
  } catch { /* silencioso */ }
}

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
</script>
