<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Clientes</div>
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">Novo Cliente</v-btn>
    </div>

    <GuiaPassos
      id="clientes"
      titulo="Como usar o cadastro de Clientes"
      :passos="[
        'Clique em <b>Novo Cliente</b>. Digite o <b>CPF/CNPJ</b> — para CNPJ, os dados da Receita são preenchidos automaticamente.',
        'Preencha nome, contato e <b>endereço</b> (o CEP completa o restante). Defina o <b>limite de crediário</b> se o cliente for comprar a prazo.',
        'Use ✎ para <b>editar</b>, 🕘 para ver o <b>histórico de compras</b> e 🚫 para <b>inativar</b> o cliente.',
        'Ative <b>Mostrar inativos</b> para ver e <b>reativar</b> clientes desativados.',
      ]"
    />

    <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
      <div class="d-flex align-center ga-3">
        <v-text-field v-model="busca" placeholder="Buscar por nome, CPF/CNPJ ou telefone..."
          prepend-inner-icon="mdi-magnify" variant="outlined" density="compact" hide-details clearable
          class="flex-grow-1" @update:model-value="carregar" />
        <v-switch v-model="mostrarInativos" label="Mostrar inativos" color="primary"
          density="compact" hide-details @update:model-value="carregar" />
      </div>
    </v-card>
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="clientes" :loading="carregando" density="compact" hover>
        <template #item.ativo="{ item }">
          <v-chip :color="item.ativo ? 'success' : 'error'" size="small" variant="tonal">
            {{ item.ativo ? 'Ativo' : 'Inativo' }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary"
            @click="abrirEdicao(item)" title="Editar" />
          <v-btn icon="mdi-history" size="x-small" variant="text" color="info"
            @click="verHistorico(item)" title="Histórico de compras" />
          <v-btn v-if="item.ativo" icon="mdi-account-cancel-outline" size="x-small" variant="text" color="error"
            @click="inativar(item)" title="Inativar cliente" />
          <v-btn v-else icon="mdi-account-check-outline" size="x-small" variant="text" color="success"
            @click="reativar(item)" title="Reativar cliente" />
        </template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="dialog" max-width="740" persistent>
      <v-card rounded="xl" style="display:flex;flex-direction:column;max-height:90vh">
        <div class="cad-header">
          <div class="d-flex align-center" style="gap:12px">
            <v-avatar color="primary" size="40">
              <v-icon>mdi-account-group-outline</v-icon>
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">{{ editando ? 'Editar Cliente' : 'Novo Cliente' }}</div>
              <div class="text-caption text-medium-emphasis">Cadastro de cliente</div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialog = false" />
        </div>
        <v-card-text class="pa-0" style="overflow-y:auto">
          <v-form ref="form" @submit.prevent="salvar">
            <div class="cad-body">
              <!-- Seção: Identificação -->
              <div class="cad-secao">
                <div class="cad-secao-header">
                  <v-icon size="14">mdi-account-outline</v-icon>
                  Identificação
                </div>
                <div class="cad-secao-body">
                  <v-row dense>
                    <v-col cols="12" sm="6">
                      <v-text-field v-model="fd.cpfCnpj" label="CPF / CNPJ"
                        variant="outlined" density="compact"
                        :loading="buscandoDoc"
                        :append-inner-icon="docStatus === 'ok' ? 'mdi-check-circle' : docStatus === 'erro' ? 'mdi-alert-circle-outline' : undefined"
                        :color="docStatus === 'ok' ? 'success' : docStatus === 'erro' ? 'error' : undefined"
                        placeholder="000.000.000-00 ou 00.000.000/0000-00"
                        hint="Digite e aguarde — CNPJ preenche automaticamente"
                        persistent-hint
                        @input="onInputDoc"
                        @blur="buscarDoc" />
                    </v-col>
                    <v-col cols="12" sm="6">
                      <v-select v-model="fd.tipoPessoa" label="Tipo de pessoa"
                        :items="[{title:'Pessoa Física',value:'Fisica'},{title:'Pessoa Jurídica',value:'Juridica'}]"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12">
                      <v-text-field v-model="fd.nome" label="Nome / Razão Social *"
                        variant="outlined" density="compact" :rules="[r => !!r || 'Obrigatório']"
                        :loading="buscandoDoc" />
                    </v-col>
                    <v-col cols="12" sm="6">
                      <v-text-field v-model="fd.telefone" label="Telefone / WhatsApp"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" sm="6">
                      <v-text-field v-model="fd.email" label="E-mail"
                        type="email" variant="outlined" density="compact" />
                    </v-col>
                  </v-row>
                </div>
              </div>
              <!-- Seção: Endereço -->
              <div class="cad-secao">
                <div class="cad-secao-header">
                  <v-icon size="14">mdi-map-marker-outline</v-icon>
                  Endereço
                </div>
                <div class="cad-secao-body">
                  <v-row dense>
                    <v-col cols="12" sm="3">
                      <v-text-field v-model="fd.cep" label="CEP"
                        variant="outlined" density="compact" @blur="buscarCep" :loading="buscandoCep" />
                    </v-col>
                    <v-col cols="12" sm="7">
                      <v-text-field v-model="fd.logradouro" label="Logradouro" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" sm="2">
                      <v-text-field v-model="fd.numero" label="Nº" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" sm="4">
                      <v-text-field v-model="fd.bairro" label="Bairro" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" sm="6">
                      <v-text-field v-model="fd.cidade" label="Cidade" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" sm="2">
                      <v-text-field v-model="fd.uf" label="UF" variant="outlined" density="compact" maxlength="2" />
                    </v-col>
                  </v-row>
                </div>
              </div>
              <!-- Seção: Dados Financeiros -->
              <div class="cad-secao">
                <div class="cad-secao-header">
                  <v-icon size="14">mdi-currency-usd</v-icon>
                  Dados Financeiros
                </div>
                <div class="cad-secao-body">
                  <v-row dense>
                    <v-col cols="12" sm="6">
                      <v-text-field v-model="fd.dataNascimento" label="Nasc."
                        type="date" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" sm="6">
                      <v-text-field v-model.number="fd.limiteCredito" label="Limite Crediário (R$)"
                        type="number" prefix="R$" variant="outlined" density="compact" />
                    </v-col>
                  </v-row>
                </div>
              </div>
            </div>
          </v-form>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancelar</v-btn>
          <v-btn color="primary" size="large" rounded="lg" :loading="salvando" @click="salvar">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="dialogHistorico" max-width="700">
      <v-card rounded="xl">
        <v-card-title class="pa-4">Histórico — {{ clienteSel?.nome }}</v-card-title>
        <v-data-table :headers="headersHist" :items="historico" density="compact" :loading="loadHist"
          no-data-text="Nenhuma compra registrada para este cliente.">
          <template #item.total="{ item }">R$ {{ fmt(item.total) }}</template>
          <template #item.dataHora="{ item }">{{ new Date(item.dataHora).toLocaleString('pt-BR') }}</template>
        </v-data-table>
      </v-card>
    </v-dialog>
  </div>
</template>
<script setup lang="ts">
import GuiaPassos from '@/components/GuiaPassos.vue'
import { ref, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import { cnpjRaw, maskCpfCnpj } from '@/utils/documento'

const auth = useAuthStore(); const notif = useNotifStore()
const carregando = ref(false); const salvando = ref(false); const buscandoCep = ref(false)
const buscandoDoc = ref(false)
const docStatus = ref<'idle'|'ok'|'erro'>('idle')
const clientes = ref<any[]>([]); const busca = ref(''); const dialog = ref(false)
const mostrarInativos = ref(false)
const editando = ref(false); const dialogHistorico = ref(false)
const historico = ref<any[]>([]); const loadHist = ref(false); const clienteSel = ref<any>(null)
const form = ref()
const fd = ref({ id:'', nome:'', tipoPessoa:'Fisica', cpfCnpj:'', telefone:'', email:'',
  cep:'', logradouro:'', numero:'', bairro:'', cidade:'', uf:'', dataNascimento:'', limiteCredito:0 })
const headers = [
  { title:'Nome', key:'nome', sortable:true }, { title:'CPF/CNPJ', key:'cpfCnpj' },
  { title:'Telefone', key:'telefone' }, { title:'Cidade', key:'cidade' },
  { title:'Status', key:'ativo' }, { title:'Ações', key:'actions', sortable:false },
]
const headersHist = [{ title:'Nº', key:'numero' }, { title:'Data', key:'dataHora' }, { title:'Itens', key:'qtdItens' }, { title:'Total', key:'total' }]
const fmt = (v: number) => (v??0).toLocaleString('pt-BR', { minimumFractionDigits:2 })
async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/clientes', { params: { empresaId: auth.empresaId, termo: busca.value, incluirInativos: mostrarInativos.value } })
    clientes.value = r.data?.itens ?? r.data ?? []
  }
  finally { carregando.value = false }
}
const fdPadrao = () => ({ id:'', nome:'', tipoPessoa:'Fisica', cpfCnpj:'', telefone:'', email:'',
  cep:'', logradouro:'', numero:'', bairro:'', cidade:'', uf:'', dataNascimento:'', limiteCredito:0 })
function abrirNovo() { editando.value=false; fd.value=fdPadrao(); docStatus.value='idle'; dialog.value=true }
function abrirEdicao(item: any) { editando.value=true; fd.value={...fdPadrao(),...item}; docStatus.value='idle'; dialog.value=true }

function onInputDoc(e: Event) {
  const raw = (e.target as HTMLInputElement).value
  fd.value.cpfCnpj = maskCpfCnpj(raw)
  docStatus.value = 'idle'
  // auto-detectar tipo
  const digits = cnpjRaw(fd.value.cpfCnpj)
  fd.value.tipoPessoa = digits.length > 11 ? 'Juridica' : 'Fisica'
}

async function buscarCep() {
  const cep = fd.value.cep.replace(/\D/g,'')
  if (cep.length!==8) return; buscandoCep.value=true
  try {
    const r = await fetch(`https://viacep.com.br/ws/${cep}/json/`)
    const d = await r.json()
    if (!d.erro) { fd.value.logradouro=d.logradouro; fd.value.bairro=d.bairro; fd.value.cidade=d.localidade; fd.value.uf=d.uf }
  } finally { buscandoCep.value=false }
}

async function buscarDoc() {
  const raw = cnpjRaw(fd.value.cpfCnpj ?? '')
  if (raw.length === 11) {
    // CPF — sem API pública (LGPD); apenas valida
    docStatus.value = 'idle'
    return
  }
  if (raw.length !== 14) return
  buscandoDoc.value = true
  docStatus.value = 'idle'
  try {
    const r = await fetch(`https://brasilapi.com.br/api/cnpj/v1/${raw}`)
    if (!r.ok) { docStatus.value = 'erro'; notif.erro('CNPJ não encontrado na Receita Federal.'); return }
    const d = await r.json()
    fd.value.nome = d.razao_social ?? fd.value.nome
    fd.value.nomeFantasia = d.nome_fantasia ?? ''
    fd.value.tipoPessoa = 'Juridica'
    if (d.ddd_telefone_1) fd.value.telefone = d.ddd_telefone_1.replace(/\D/g,'').replace(/^(\d{2})(\d+)/, '($1) $2')
    if (d.email) fd.value.email = d.email.toLowerCase()
    if (d.cep) { fd.value.cep = d.cep.replace(/\D/g,'').replace(/(\d{5})(\d{3})/, '$1-$2'); await buscarCep() }
    else { fd.value.logradouro = d.logradouro ?? ''; fd.value.bairro = d.bairro ?? ''; fd.value.cidade = d.municipio ?? ''; fd.value.uf = d.uf ?? '' }
    if (d.numero) fd.value.numero = d.numero
    docStatus.value = 'ok'
    notif.ok('Dados da Receita Federal preenchidos automaticamente!')
  } catch { docStatus.value = 'erro'; notif.erro('Erro ao consultar CNPJ.') }
  finally { buscandoDoc.value = false }
}
async function salvar() {
  const { valid } = await form.value.validate(); if (!valid) return; salvando.value=true
  try {
    const payload = { ...fd.value, empresaId: auth.empresaId, cep: (fd.value.cep ?? '').replace(/\D/g,'') || null }
    if (editando.value) { await api.put(`/clientes/${fd.value.id}`, payload); notif.ok('Cliente atualizado!') }
    else { await api.post('/clientes', payload); notif.ok('Cliente cadastrado!') }
    dialog.value=false; await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? e?.response?.data ?? 'Erro ao salvar cliente.') }
  finally { salvando.value=false }
}
async function verHistorico(item: any) {
  clienteSel.value=item; dialogHistorico.value=true; loadHist.value=true; historico.value=[]
  try {
    const r = await api.get(`/clientes/${item.id}/historico-compras`, { params: { empresaId:auth.empresaId } })
    historico.value = r.data?.vendas ?? []
  }
  finally { loadHist.value=false }
}

async function inativar(item: any) {
  if (!confirm(`Inativar o cliente "${item.nome}"?`)) return
  try {
    await api.delete(`/clientes/${item.id}`)
    notif.ok('Cliente inativado.')
    await carregar()
  } catch { notif.erro('Erro ao inativar.') }
}

async function reativar(item: any) {
  try {
    await api.post(`/clientes/${item.id}/reativar`)
    notif.ok('Cliente reativado.')
    await carregar()
  } catch { notif.erro('Erro ao reativar.') }
}

onMounted(carregar)
</script>

<style scoped>
.cad-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; }
.cad-body { background:#f5f6f8; padding:16px; display:flex; flex-direction:column; gap:12px; }
.cad-secao { background:white; border-radius:12px; border:1px solid #e8edf3; overflow:hidden; }
.cad-secao-header { display:flex; align-items:center; gap:6px; padding:10px 16px; background:#f8f9fb; border-bottom:1px solid #e8edf3; font-size:0.75rem; font-weight:700; text-transform:uppercase; letter-spacing:0.07em; color:rgb(var(--v-theme-primary)); }
.cad-secao-body { padding:16px; }
</style>
