<template>
  <v-container fluid>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Fornecedores</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNovo">Novo Fornecedor</v-btn>
      </v-col>
    </v-row>

    <v-card>
      <v-card-text>
        <v-row class="mb-2">
          <v-col cols="12" md="5">
            <v-text-field v-model="busca" label="Buscar por nome ou CNPJ" prepend-inner-icon="mdi-magnify"
              clearable density="compact" hide-details @update:modelValue="listar" />
          </v-col>
          <v-col cols="12" md="3">
            <v-select v-model="filtroAtivo" :items="[{title:'Todos',value:null},{title:'Ativos',value:true},{title:'Inativos',value:false}]"
              label="Status" density="compact" hide-details @update:modelValue="listar" />
          </v-col>
        </v-row>

        <v-data-table :headers="headers" :items="fornecedores" :loading="carregando"
          items-per-page="20" class="elevation-0">
          <template #item.razaoSocial="{ item }">
            <div class="font-weight-medium">{{ item.razaoSocial }}</div>
            <div class="text-caption text-medium-emphasis">{{ item.nomeFantasia }}</div>
          </template>
          <template #item.tipos="{ item }">
            <div class="d-flex flex-wrap gap-1">
              <v-chip v-for="t in (item.tipos ?? ['Fornecedor'])" :key="t"
                :color="tiposOpcoes.find(o=>o.value===t)?.cor ?? 'primary'"
                size="x-small" variant="tonal">
                {{ tiposOpcoes.find(o=>o.value===t)?.label ?? t }}
              </v-chip>
            </div>
          </template>
          <template #item.cnpj="{ item }">{{ formatarCnpj(item.cnpj) }}</template>
          <template #item.ativo="{ item }">
            <v-chip :color="item.ativo ? 'success' : 'error'" size="small">
              {{ item.ativo ? 'Ativo' : 'Inativo' }}
            </v-chip>
          </template>
          <template #item.acoes="{ item }">
            <v-btn icon="mdi-pencil" size="small" variant="text" @click="editar(item)" />
            <v-btn :icon="item.ativo ? 'mdi-delete' : 'mdi-restore'" size="small" variant="text"
              :color="item.ativo ? 'error' : 'success'" @click="alternarAtivo(item)" />
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>

    <!-- Dialog Novo/Editar -->
    <v-dialog v-model="dialog" max-width="800" persistent>
      <v-card rounded="xl" style="display:flex;flex-direction:column;max-height:90vh">
        <div class="cad-header">
          <div class="d-flex align-center" style="gap:12px">
            <v-avatar :color="tipoAvatarCor" size="40">
              <v-icon>{{ tipoAvatarIcon }}</v-icon>
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">{{ editando ? 'Editar Parceiro' : 'Novo Parceiro' }}</div>
              <div class="text-caption text-medium-emphasis">{{ tiposLabel }}</div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialog = false" />
        </div>
        <v-card-text class="pa-0" style="overflow-y:auto">
          <v-form ref="formRef">
            <div class="cad-body">
              <!-- Seção: Tipo de Parceiro -->
              <div class="cad-secao">
                <div class="cad-secao-header">
                  <v-icon size="14">mdi-tag-multiple-outline</v-icon>
                  Tipo de Parceiro
                </div>
                <div class="cad-secao-body py-3">
                  <div class="d-flex flex-wrap gap-2">
                    <v-chip
                      v-for="t in tiposOpcoes" :key="t.value"
                      :color="form.tipos?.includes(t.value) ? t.cor : 'default'"
                      :variant="form.tipos?.includes(t.value) ? 'tonal' : 'outlined'"
                      :prepend-icon="t.icon"
                      size="default"
                      style="cursor:pointer"
                      @click="toggleTipo(t.value)"
                    >{{ t.label }}</v-chip>
                  </div>
                  <div v-if="!form.tipos?.length" class="text-caption text-error mt-1">
                    Selecione ao menos um tipo
                  </div>
                </div>
              </div>
              <!-- Seção: Dados da Empresa -->
              <div class="cad-secao">
                <div class="cad-secao-header">
                  <v-icon size="14">mdi-domain</v-icon>
                  Dados da Empresa
                </div>
                <div class="cad-secao-body">
                  <v-row dense>
                    <v-col cols="12" md="4">
                      <v-text-field v-model="form.cnpj" label="CNPJ *"
                        variant="outlined" density="compact"
                        placeholder="00.000.000/0000-00"
                        :rules="[r => !!r || 'Obrigatório']"
                        :loading="buscandoCnpj"
                        :append-inner-icon="cnpjStatus === 'ok' ? 'mdi-check-circle' : cnpjStatus === 'erro' ? 'mdi-alert-circle-outline' : undefined"
                        :color="cnpjStatus === 'ok' ? 'success' : cnpjStatus === 'erro' ? 'error' : undefined"
                        hint="Preenche dados automaticamente"
                        persistent-hint
                        @input="form.cnpj = maskCnpj(($event.target as HTMLInputElement).value); cnpjStatus = 'idle'"
                        @blur="buscarCnpj" />
                    </v-col>
                    <v-col cols="12" md="8">
                      <v-text-field v-model="form.razaoSocial" label="Razão Social *"
                        variant="outlined" density="compact" :rules="[r => !!r || 'Obrigatório']" />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field v-model="form.nomeFantasia" label="Nome Fantasia"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field v-model="form.email" label="E-mail" type="email"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="4">
                      <v-text-field v-model="form.telefone" label="Telefone" v-mask="'(##) ####-####'"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="4">
                      <v-text-field v-model="form.celular" label="Celular" v-mask="'(##) #####-####'"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="4">
                      <v-text-field v-model="form.prazoEntregaDias" label="Prazo de Entrega (dias)" type="number"
                        variant="outlined" density="compact" />
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
                    <v-col cols="12" md="3">
                      <v-text-field v-model="form.cep" label="CEP" v-mask="'#####-###'"
                        variant="outlined" density="compact"
                        append-inner-icon="mdi-magnify" @click:append-inner="buscarCep" @blur="buscarCep" />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field v-model="form.logradouro" label="Logradouro"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-text-field v-model="form.numero" label="Número"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="4">
                      <v-text-field v-model="form.bairro" label="Bairro"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="5">
                      <v-text-field v-model="form.cidade" label="Cidade"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-text-field v-model="form.uf" label="UF" maxlength="2"
                        variant="outlined" density="compact" />
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
  </v-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useNotifStore } from '@/stores/notif'
import { useAuthStore } from '@/stores/auth'
import { cnpjRaw, maskCnpj, formatarCnpj } from '@/utils/documento'


const notif = useNotifStore()
const auth = useAuthStore()

const fornecedores = ref<any[]>([])
const carregando = ref(false)
const dialog = ref(false)
const editando = ref<string | null>(null)
const salvando = ref(false)
const busca = ref('')
const filtroAtivo = ref<boolean | null>(true)
const form = ref<any>({})
const formRef = ref()
const buscandoCnpj = ref(false)
const cnpjStatus = ref<'idle'|'ok'|'erro'>('idle')

const tiposOpcoes = [
  { value: 'Fornecedor',          label: 'Fornecedor',          icon: 'mdi-truck-delivery-outline',    cor: 'primary'  },
  { value: 'Transportadora',      label: 'Transportadora',      icon: 'mdi-truck-outline',             cor: 'teal'     },
  { value: 'Representante',       label: 'Representante',       icon: 'mdi-account-tie-outline',       cor: 'indigo'   },
  { value: 'ParceiroCom',         label: 'Parceiro Comercial',  icon: 'mdi-handshake-outline',         cor: 'deep-purple' },
]

const tipoAvatarCor = computed(() => {
  const t = tiposOpcoes.find(o => form.value.tipos?.includes(o.value))
  return t?.cor ?? 'primary'
})
const tipoAvatarIcon = computed(() => {
  const t = tiposOpcoes.find(o => form.value.tipos?.includes(o.value))
  return t?.icon ?? 'mdi-domain'
})
const tiposLabel = computed(() => {
  const labels = (form.value.tipos ?? []).map(v => tiposOpcoes.find(o => o.value === v)?.label).filter(Boolean)
  return labels.length ? labels.join(' · ') : 'Selecione o tipo de parceiro'
})

function toggleTipo(v: string) {
  const arr: string[] = form.value.tipos ?? []
  form.value.tipos = arr.includes(v) ? arr.filter(x => x !== v) : [...arr, v]
}

const headers = [
  { title: 'Razão Social', key: 'razaoSocial' },
  { title: 'Tipos', key: 'tipos', sortable: false },
  { title: 'CNPJ', key: 'cnpj' },
  { title: 'Telefone', key: 'telefone' },
  { title: 'Status', key: 'ativo', align: 'center' as const },
  { title: 'Ações', key: 'acoes', sortable: false, align: 'end' as const },
]

async function listar() {
  carregando.value = true
  try {
    const params: any = { empresaId: auth.empresaId }
    if (busca.value) params.q = busca.value
    if (filtroAtivo.value !== null) params.ativo = filtroAtivo.value
    const { data } = await api.get('/fornecedores', { params })
    fornecedores.value = Array.isArray(data) ? data : (data.itens ?? [])
  } finally {
    carregando.value = false
  }
}

function abrirNovo() {
  editando.value = null
  form.value = { ativo: true, tipos: ['Fornecedor'] }
  cnpjStatus.value = 'idle'
  dialog.value = true
}

function editar(item: any) {
  editando.value = item.id
  form.value = { ...item }
  cnpjStatus.value = 'idle'
  dialog.value = true
}

async function salvar() {
  const { valid } = await formRef.value?.validate()
  if (!valid) return
  salvando.value = true
  try {
    if (editando.value) {
      await api.put(`/fornecedores/${editando.value}`, form.value)
    } else {
      await api.post('/fornecedores', { ...form.value, empresaId: auth.empresaId })
    }
    notif.ok('Fornecedor salvo com sucesso!')
    dialog.value = false
    await listar()
  } catch {
    notif.erro('Erro ao salvar fornecedor.')
  } finally {
    salvando.value = false
  }
}

async function alternarAtivo(item: any) {
  try {
    if (item.ativo) {
      await api.delete(`/fornecedores/${item.id}`)
    } else {
      await api.patch(`/fornecedores/${item.id}/reativar`)
    }
    await listar()
  } catch {
    notif.erro('Erro ao alterar status.')
  }
}

async function buscarCep() {
  const cep = form.value.cep?.replace(/\D/g, '')
  if (cep?.length !== 8) return
  try {
    const res = await fetch(`https://viacep.com.br/ws/${cep}/json/`)
    const dados = await res.json()
    if (!dados.erro) {
      form.value.logradouro = dados.logradouro
      form.value.bairro = dados.bairro
      form.value.cidade = dados.localidade
      form.value.uf = dados.uf
    }
  } catch {}
}

async function buscarCnpj() {
  const cnpj = cnpjRaw(form.value.cnpj ?? '')
  if (cnpj?.length !== 14) return
  buscandoCnpj.value = true
  cnpjStatus.value = 'idle'
  try {
    const res = await fetch(`https://brasilapi.com.br/api/cnpj/v1/${cnpj}`)
    if (!res.ok) { cnpjStatus.value = 'erro'; notif.erro('CNPJ não encontrado na Receita Federal.'); return }
    const d = await res.json()
    form.value.razaoSocial = d.razao_social ?? form.value.razaoSocial
    form.value.nomeFantasia = d.nome_fantasia ?? ''
    if (d.email) form.value.email = d.email.toLowerCase()
    if (d.ddd_telefone_1) form.value.telefone = d.ddd_telefone_1.replace(/\D/g,'').replace(/^(\d{2})(\d{4,5})(\d{4})/, '($1) $2-$3')
    if (d.ddd_telefone_2) form.value.celular = d.ddd_telefone_2.replace(/\D/g,'').replace(/^(\d{2})(\d{4,5})(\d{4})/, '($1) $2-$3')
    if (d.cep) { form.value.cep = d.cep.replace(/\D/g,'').replace(/(\d{5})(\d{3})/, '$1-$2'); await buscarCep() }
    else { form.value.logradouro = d.logradouro ?? ''; form.value.bairro = d.bairro ?? ''; form.value.cidade = d.municipio ?? ''; form.value.uf = d.uf ?? '' }
    if (d.numero) form.value.numero = d.numero
    cnpjStatus.value = 'ok'
    notif.ok('Dados da Receita Federal preenchidos automaticamente!')
  } catch { cnpjStatus.value = 'erro'; notif.erro('Erro ao consultar CNPJ.') }
  finally { buscandoCnpj.value = false }
}

// formatarCnpj e maskCnpj importados de @/utils/documento

onMounted(listar)
</script>

<style scoped>
.cad-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; }
.cad-body { background:#f5f6f8; padding:16px; display:flex; flex-direction:column; gap:12px; }
.cad-secao { background:white; border-radius:12px; border:1px solid #e8edf3; overflow:hidden; }
.cad-secao-header { display:flex; align-items:center; gap:6px; padding:10px 16px; background:#f8f9fb; border-bottom:1px solid #e8edf3; font-size:0.75rem; font-weight:700; text-transform:uppercase; letter-spacing:0.07em; color:rgb(var(--v-theme-primary)); }
.cad-secao-body { padding:16px; }
</style>
