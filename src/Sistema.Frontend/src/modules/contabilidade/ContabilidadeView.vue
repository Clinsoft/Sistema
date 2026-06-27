<template>
  <div>
    <div class="text-h6 font-weight-bold mb-4">Contabilidade</div>

    <v-tabs v-model="aba" bg-color="transparent" class="mb-4">
      <v-tab value="plano">Plano de Contas</v-tab>
      <v-tab value="lancamentos">Lançamentos</v-tab>
      <v-tab value="balancete">Balancete</v-tab>
      <v-tab value="xml">Download XML</v-tab>
      <v-tab value="contadores">Contadores</v-tab>
    </v-tabs>

    <!-- ── PLANO DE CONTAS ──────────────────────────────── -->
    <div v-if="aba==='plano'">
      <div class="d-flex align-center mb-3">
        <v-spacer />
        <v-btn color="secondary" variant="tonal" prepend-icon="mdi-auto-fix"
          :loading="gerando" @click="seedPlano" class="mr-2">Gerar Plano Padrão CFC</v-btn>
        <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg"
          @click="abrirNovaConta">Nova Conta</v-btn>
      </div>
      <v-card rounded="xl" elevation="1">
        <v-data-table :headers="headersPlano" :items="contas" :loading="carregandoPlano" density="compact" hover>
          <template #item.tipo="{ item }">
            <v-chip size="small" :color="item.tipo==='Analitica'?'primary':'default'" variant="tonal">{{ item.tipo }}</v-chip>
          </template>
          <template #item.actions="{ item }">
            <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error"
              @click="desativarConta(item.id)" v-if="item.aceitaLancamento" />
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- ── LANÇAMENTOS ──────────────────────────────────── -->
    <div v-if="aba==='lancamentos'">
      <div class="d-flex align-center mb-3 gap-2 flex-wrap">
        <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f }" style="min-width:180px" />
        <v-text-field v-model="filtros.inicio" label="Início" type="date"
          variant="outlined" density="compact" hide-details style="max-width:160px" />
        <v-text-field v-model="filtros.fim" label="Fim" type="date"
          variant="outlined" density="compact" hide-details style="max-width:160px" />
        <v-btn color="primary" variant="tonal" :loading="carregandoLanc" @click="carregarLancamentos">Buscar</v-btn>
        <v-spacer />
        <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovoLanc">Novo</v-btn>
      </div>
      <v-card rounded="xl" elevation="1">
        <v-data-table :headers="headersLanc" :items="lancamentos" :loading="carregandoLanc" density="compact" hover>
          <template #item.dataCompetencia="{ item }">{{ new Date(item.dataCompetencia).toLocaleDateString('pt-BR') }}</template>
          <template #item.estornado="{ item }">
            <v-chip v-if="item.estornado" color="error" size="small" variant="tonal">Estornado</v-chip>
          </template>
          <template #item.actions="{ item }">
            <v-btn v-if="!item.estornado" icon="mdi-undo" size="x-small" variant="text"
              color="warning" @click="estornar(item)" title="Estornar" />
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- ── BALANCETE ────────────────────────────────────── -->
    <div v-if="aba==='balancete'">
      <div class="d-flex align-center mb-3 gap-2 flex-wrap">
        <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f }" style="min-width:180px" />
        <v-text-field v-model="filtros.inicio" label="Início" type="date"
          variant="outlined" density="compact" hide-details style="max-width:160px" />
        <v-text-field v-model="filtros.fim" label="Fim" type="date"
          variant="outlined" density="compact" hide-details style="max-width:160px" />
        <v-btn color="primary" variant="tonal" :loading="carregandoBalancete" @click="carregarBalancete">Gerar</v-btn>
      </div>
      <v-card v-if="balancete" rounded="xl" elevation="1">
        <v-card-text>
          <div class="d-flex justify-space-between mb-2 text-body-2">
            <span>Total Débitos</span>
            <span class="font-weight-bold">R$ {{ fmt(balancete.totalDebitos) }}</span>
          </div>
          <div class="d-flex justify-space-between mb-3 text-body-2">
            <span>Total Créditos</span>
            <span class="font-weight-bold">R$ {{ fmt(balancete.totalCreditos) }}</span>
          </div>
        </v-card-text>
        <v-data-table :headers="headersBalancete" :items="balancete.contas"
          density="compact" items-per-page="-1" hide-default-footer>
          <template #item.totalDebitos="{ item }">R$ {{ fmt(item.totalDebitos) }}</template>
          <template #item.totalCreditos="{ item }">R$ {{ fmt(item.totalCreditos) }}</template>
          <template #item.saldo="{ item }">
            <span :class="item.saldo>=0?'text-success':'text-error'" class="font-weight-bold">
              R$ {{ fmt(Math.abs(item.saldo)) }} {{ item.saldo<0?' C':' D' }}
            </span>
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- ── DOWNLOAD XML ─────────────────────────────────── -->
    <div v-if="aba==='xml'">
      <v-card rounded="xl" elevation="1" class="mb-4 pa-4">
        <div class="text-subtitle-1 font-weight-bold mb-3">
          <v-icon icon="mdi-file-xml-box" color="primary" class="mr-1"/>
          Download de XMLs por Competência
        </div>
        <v-row dense align="center">
          <v-col cols="12" md="3">
            <v-select v-model="xmlAno" :items="anosDisponiveis" label="Ano"
              variant="outlined" density="compact" hide-details @update:model-value="carregarCompetencias" />
          </v-col>
          <v-col cols="12" md="3">
            <v-select v-model="xmlModelo" label="Tipo"
              :items="[{title:'Todos',value:''},{title:'Apenas NF-e',value:'NFe'},{title:'Apenas NFC-e',value:'NFCe'}]"
              variant="outlined" density="compact" hide-details />
          </v-col>
        </v-row>
      </v-card>

      <v-card rounded="xl" elevation="1">
        <v-data-table :headers="headersXml" :items="competencias" :loading="carregandoXml"
          density="comfortable" hover>
          <template #item.mes="{ item }">
            {{ nomeMes(item.mes) }} / {{ item.ano }}
          </template>
          <template #item.qtdNFe="{ item }">
            <v-chip size="small" color="primary" variant="tonal" v-if="item.qtdNFe">{{ item.qtdNFe }} NF-e</v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.qtdNFCe="{ item }">
            <v-chip size="small" color="success" variant="tonal" v-if="item.qtdNFCe">{{ item.qtdNFCe }} NFC-e</v-chip>
            <span v-else class="text-medium-emphasis">—</span>
          </template>
          <template #item.actions="{ item }">
            <v-btn size="small" color="primary" variant="tonal"
              prepend-icon="mdi-download-box-outline"
              :loading="baixandoXml === item.ano + '-' + item.mes"
              @click="baixarXml(item.ano, item.mes)">
              Baixar ZIP
            </v-btn>
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- ── CONTADORES ───────────────────────────────────── -->
    <div v-if="aba==='contadores'">
      <div class="d-flex align-center mb-3">
        <div class="text-body-2 text-medium-emphasis">
          Cadastre contadores/escritórios com acesso ao Painel do Contador.
        </div>
        <v-spacer />
        <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg"
          @click="abrirNovoContador">Novo Contador</v-btn>
      </div>

      <v-card rounded="xl" elevation="1">
        <v-data-table :headers="headersContador" :items="contadores"
          :loading="carregandoContadores" density="compact" hover>
          <template #item.ativo="{ item }">
            <v-chip size="small" :color="item.ativo?'success':'default'" variant="tonal">
              {{ item.ativo ? 'Ativo' : 'Inativo' }}
            </v-chip>
          </template>
          <template #item.criadoEm="{ item }">
            {{ new Date(item.criadoEm).toLocaleDateString('pt-BR') }}
          </template>
          <template #item.actions="{ item }">
            <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" color="primary"
              @click="abrirEdicaoContador(item)" />
            <v-btn v-if="item.ativo" icon="mdi-account-off-outline" size="x-small"
              variant="text" color="error" @click="desativarContador(item.id)" />
            <v-btn v-else icon="mdi-account-check-outline" size="x-small"
              variant="text" color="success" @click="reativarContador(item.id)" />
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- Dialog nova conta contábil -->
    <v-dialog v-model="dialogConta" max-width="500">
      <v-card rounded="xl" class="pa-4">
        <v-card-title class="mb-3">Nova Conta Contábil</v-card-title>
        <v-form ref="formConta" @submit.prevent="salvarConta">
          <v-row dense>
            <v-col cols="4">
              <v-text-field v-model="novaConta.codigo" label="Código *"
                variant="outlined" density="compact" :rules="[r=>!!r||'Obrigatório']" />
            </v-col>
            <v-col cols="8">
              <v-text-field v-model="novaConta.nome" label="Nome *"
                variant="outlined" density="compact" :rules="[r=>!!r||'Obrigatório']" />
            </v-col>
            <v-col cols="6">
              <v-select v-model="novaConta.natureza" label="Natureza"
                :items="['Ativo','Passivo','PatrimonioLiquido','Receita','Custo','Despesa']"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-select v-model="novaConta.tipo" label="Tipo"
                :items="['Sintetica','Analitica']" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model.number="novaConta.nivel" label="Nível" type="number"
                variant="outlined" density="compact" />
            </v-col>
          </v-row>
        </v-form>
        <v-card-actions class="justify-end">
          <v-btn variant="text" @click="dialogConta=false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvando" @click="salvarConta">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog contador -->
    <v-dialog v-model="dialogContador" max-width="540" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          {{ contadorEditandoId ? 'Editar Contador' : 'Novo Contador' }}
        </v-card-title>
        <v-card-text class="pa-4 pt-0">
          <v-row dense>
            <v-col cols="12">
              <v-text-field v-model="formContador.nome" label="Nome completo / Razão social *"
                variant="outlined" density="compact" :rules="[r=>!!r||'Obrigatório']" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="formContador.cpfCnpj" label="CPF ou CNPJ *"
                variant="outlined" density="compact" :rules="[r=>!!r||'Obrigatório']"
                :disabled="!!contadorEditandoId" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="formContador.crc" label="CRC (Registro)"
                variant="outlined" density="compact" placeholder="CRC/SP 123456" />
            </v-col>
            <v-col cols="12" md="7">
              <v-text-field v-model="formContador.email" label="E-mail *"
                type="email" variant="outlined" density="compact"
                :rules="[r=>!!r||'Obrigatório']" />
            </v-col>
            <v-col cols="12" md="5">
              <v-text-field v-model="formContador.telefone" label="Telefone"
                variant="outlined" density="compact" />
            </v-col>
          </v-row>
          <v-alert type="info" variant="tonal" density="compact" class="mt-2 text-caption">
            O contador acessa o sistema pelo login padrão usando o e-mail cadastrado acima.
            O perfil <strong>Contador</strong> limita o acesso somente às telas fiscais e contábeis.
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogContador=false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvandoContador" @click="salvarContador">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import FiltroMes from '@/components/FiltroMes.vue'
import { ref, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const aba = ref('plano')

// ── Plano / Lançamentos / Balancete ──────────────────────────────
const carregandoPlano = ref(false); const carregandoLanc = ref(false); const carregandoBalancete = ref(false)
const gerando = ref(false); const salvando = ref(false)
const contas = ref<any[]>([]); const lancamentos = ref<any[]>([]); const balancete = ref<any>(null)
const dialogConta = ref(false); const formConta = ref()
const novaConta = ref({ codigo:'', nome:'', natureza:'Ativo', tipo:'Analitica', nivel:4 })
const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0,10),
  fim: new Date().toISOString().slice(0,10)
})

const headersPlano = [
  { title:'Código', key:'codigo', sortable:true }, { title:'Nome', key:'nome', sortable:true },
  { title:'Natureza', key:'natureza' }, { title:'Tipo', key:'tipo' },
  { title:'Nível', key:'nivel' }, { title:'', key:'actions', sortable:false },
]
const headersLanc = [
  { title:'Número', key:'numero' }, { title:'Competência', key:'dataCompetencia' },
  { title:'Histórico', key:'historico' }, { title:'Tipo', key:'tipo' },
  { title:'', key:'estornado' }, { title:'', key:'actions', sortable:false },
]
const headersBalancete = [
  { title:'Código', key:'codigo' }, { title:'Conta', key:'nome' }, { title:'Natureza', key:'natureza' },
  { title:'Débitos', key:'totalDebitos' }, { title:'Créditos', key:'totalCreditos' }, { title:'Saldo', key:'saldo' },
]
const fmt = (v: number) => (v??0).toLocaleString('pt-BR', { minimumFractionDigits:2 })

async function carregarPlano() {
  carregandoPlano.value = true
  try { const r = await api.get('/contabilidade/plano-contas', { params:{ empresaId:auth.empresaId } }); contas.value = r.data }
  finally { carregandoPlano.value = false }
}
async function seedPlano() {
  gerando.value = true
  try { await api.post('/contabilidade/plano-contas/seed', null, { params:{ empresaId:auth.empresaId } }); notif.ok('Plano CFC criado!'); await carregarPlano() }
  finally { gerando.value = false }
}
function abrirNovaConta() { novaConta.value = { codigo:'', nome:'', natureza:'Ativo', tipo:'Analitica', nivel:4 }; dialogConta.value = true }
async function salvarConta() {
  salvando.value = true
  try { await api.post('/contabilidade/plano-contas', { ...novaConta.value, empresaId:auth.empresaId }); notif.ok('Conta criada!'); dialogConta.value = false; await carregarPlano() }
  finally { salvando.value = false }
}
async function desativarConta(id: string) { await api.delete(`/contabilidade/plano-contas/${id}`); await carregarPlano() }
async function carregarLancamentos() {
  carregandoLanc.value = true
  try { const r = await api.get('/contabilidade/lancamentos', { params:{ empresaId:auth.empresaId, ...filtros.value } }); lancamentos.value = r.data }
  finally { carregandoLanc.value = false }
}
async function estornar(item: any) {
  await api.post(`/contabilidade/lancamentos/${item.id}/estornar`); notif.ok('Estorno criado!'); await carregarLancamentos()
}
function abrirNovoLanc() { /* TODO: dialog partidas */ }
async function carregarBalancete() {
  carregandoBalancete.value = true
  try { const r = await api.get('/contabilidade/lancamentos/balancete', { params:{ empresaId:auth.empresaId, ...filtros.value } }); balancete.value = r.data }
  finally { carregandoBalancete.value = false }
}

// ── Download XML ──────────────────────────────────────────────────
const competencias = ref<any[]>([])
const carregandoXml = ref(false)
const baixandoXml = ref('')
const xmlAno = ref(new Date().getFullYear())
const xmlModelo = ref('')
const anosDisponiveis = Array.from({ length: 5 }, (_, i) => new Date().getFullYear() - i)

const headersXml = [
  { title: 'Competência', key: 'mes', sortable: false },
  { title: 'NF-e', key: 'qtdNFe', width: 110 },
  { title: 'NFC-e', key: 'qtdNFCe', width: 110 },
  { title: 'Total', key: 'total', width: 80 },
  { title: '', key: 'actions', sortable: false, width: 140 },
]

const meses = ['Janeiro','Fevereiro','Março','Abril','Maio','Junho',
               'Julho','Agosto','Setembro','Outubro','Novembro','Dezembro']
function nomeMes(m: number) { return meses[m - 1] }

async function carregarCompetencias() {
  carregandoXml.value = true
  try {
    const r = await api.get('/contabilidade/xml/competencias', { params: { empresaId: auth.empresaId } })
    competencias.value = r.data.filter((c: any) => c.ano === xmlAno.value)
  } catch { /* silencioso */ }
  finally { carregandoXml.value = false }
}

async function baixarXml(ano: number, mes: number) {
  baixandoXml.value = `${ano}-${mes}`
  try {
    const r = await api.get('/contabilidade/xml/download', {
      params: { empresaId: auth.empresaId, ano, mes, modelo: xmlModelo.value || undefined },
      responseType: 'blob',
    })
    const url = URL.createObjectURL(new Blob([r.data], { type: 'application/zip' }))
    const a = document.createElement('a')
    a.href = url
    a.download = `XMLs_${nomeMes(mes)}_${ano}.zip`
    a.click()
    URL.revokeObjectURL(url)
  } catch { notif.erro('Nenhum XML encontrado para o período selecionado.') }
  finally { baixandoXml.value = '' }
}

// ── Contadores ────────────────────────────────────────────────────
const contadores = ref<any[]>([])
const carregandoContadores = ref(false)
const salvandoContador = ref(false)
const dialogContador = ref(false)
const contadorEditandoId = ref<string | null>(null)
const formContador = ref({ nome:'', cpfCnpj:'', email:'', telefone:'', crc:'' })

const headersContador = [
  { title: 'Nome', key: 'nome' },
  { title: 'CPF/CNPJ', key: 'cpfCnpj', width: 160 },
  { title: 'CRC', key: 'crc', width: 130 },
  { title: 'E-mail', key: 'email' },
  { title: 'Status', key: 'ativo', width: 90 },
  { title: 'Cadastro', key: 'criadoEm', width: 110 },
  { title: '', key: 'actions', sortable: false, width: 110 },
]

async function carregarContadores() {
  carregandoContadores.value = true
  try {
    const r = await api.get('/contabilidade/contadores', { params: { empresaId: auth.empresaId } })
    contadores.value = r.data
  } finally { carregandoContadores.value = false }
}

function abrirNovoContador() {
  contadorEditandoId.value = null
  formContador.value = { nome:'', cpfCnpj:'', email:'', telefone:'', crc:'' }
  dialogContador.value = true
}

function abrirEdicaoContador(item: any) {
  contadorEditandoId.value = item.id
  formContador.value = { nome: item.nome, cpfCnpj: item.cpfCnpj, email: item.email, telefone: item.telefone ?? '', crc: item.crc ?? '' }
  dialogContador.value = true
}

async function salvarContador() {
  salvandoContador.value = true
  try {
    if (contadorEditandoId.value) {
      await api.put(`/contabilidade/contadores/${contadorEditandoId.value}`, {
        nome: formContador.value.nome,
        email: formContador.value.email,
        telefone: formContador.value.telefone || null,
        crc: formContador.value.crc || null,
      })
    } else {
      await api.post('/contabilidade/contadores', {
        empresaId: auth.empresaId,
        ...formContador.value,
        telefone: formContador.value.telefone || null,
        crc: formContador.value.crc || null,
      })
    }
    notif.ok('Contador salvo!')
    dialogContador.value = false
    await carregarContadores()
  } catch (e: any) {
    notif.erro(e?.response?.data ?? 'Erro ao salvar contador.')
  } finally { salvandoContador.value = false }
}

async function desativarContador(id: string) {
  await api.delete(`/contabilidade/contadores/${id}`)
  await carregarContadores()
  notif.ok('Contador desativado.')
}

async function reativarContador(id: string) {
  await api.patch(`/contabilidade/contadores/${id}/reativar`)
  await carregarContadores()
  notif.ok('Contador reativado.')
}

onMounted(() => {
  carregarPlano()
  carregarContadores()
  carregarCompetencias()
})
</script>
