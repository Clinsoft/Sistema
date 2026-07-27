<template>
  <v-container fluid>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Fornecedores</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNovo">Novo Fornecedor</v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="fornecedores"
      titulo="Como usar o cadastro de Fornecedores"
      :passos="[
        'Clique em <b>Novo Fornecedor</b> e escolha o(s) <b>tipo(s) de parceiro</b> (Fornecedor, Transportadora, Representante, Parceiro).',
        'Digite o <b>CPF/CNPJ</b> — para CNPJ os dados da Receita são preenchidos automaticamente. O <b>CEP</b> completa o endereço.',
        'Informe <b>contato</b>, <b>prazo de pagamento</b> e demais dados. Use ✎ para editar depois.',
        'Use o botão 🗑/↻ para <b>inativar/reativar</b>. Filtre por <b>Status</b> para ver inativos e recuperá-los.',
      ]"
    />

    <v-card>
      <v-card-text>
        <v-row class="mb-2">
          <v-col cols="12" md="5">
            <v-text-field v-model="busca" label="Buscar por nome ou CPF/CNPJ" prepend-inner-icon="mdi-magnify"
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
          <template #item.cnpj="{ item }">{{ formatarCpfCnpj(item.cnpj ?? '') }}</template>
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
                  <v-checkbox v-model="form.ehCliente" density="compact" hide-details color="primary"
                    :disabled="editandoTinhaCliente" class="mt-1"
                    label="Também é cliente (cadastrar na carteira de clientes)" />
                  <div v-if="editandoTinhaCliente" class="text-caption text-medium-emphasis ml-8">
                    Já está cadastrado como cliente.
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
                      <v-text-field v-model="form.cnpj" label="CPF / CNPJ"
                        variant="outlined" density="compact"
                        placeholder="000.000.000-00 ou 00.000.000/0000-00"
                        :loading="buscandoCnpj"
                        :append-inner-icon="cnpjStatus === 'ok' ? 'mdi-check-circle' : cnpjStatus === 'erro' ? 'mdi-alert-circle-outline' : undefined"
                        :color="cnpjStatus === 'ok' ? 'success' : cnpjStatus === 'erro' ? 'error' : undefined"
                        hint="Preenche dados automaticamente para CNPJ"
                        persistent-hint
                        @input="form.cnpj = maskCpfCnpj(($event.target as HTMLInputElement).value); cnpjStatus = 'idle'"
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
                    <v-col cols="12" md="6">
                      <v-text-field v-model="form.contato" label="Pessoa de Contato"
                        variant="outlined" density="compact" placeholder="Nome do responsável" />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field v-model.number="form.prazoPagamentoDias" label="Prazo de Pagamento (dias)" type="number"
                        variant="outlined" density="compact" hint="Prazo padrão para pagamento das compras" persistent-hint />
                    </v-col>
                  </v-row>
                </div>
              </div>
              <!-- Seção: Mensalidade fixa (recorrente) -->
              <div class="cad-secao">
                <div class="cad-secao-header">
                  <v-icon size="14">mdi-calendar-sync-outline</v-icon>
                  Mensalidade fixa (recorrente)
                </div>
                <div class="cad-secao-body">
                  <div class="text-caption text-medium-emphasis mb-2">
                    Para prestadores com valor mensal (ex.: contador, aluguel). Todo mês o sistema
                    lança sozinho uma conta a pagar no dia de vencimento informado.
                  </div>
                  <v-row dense>
                    <v-col cols="12" md="4">
                      <v-text-field v-model.number="form.mensalidadeValor" label="Valor mensal (R$)"
                        type="number" prefix="R$" variant="outlined" density="compact"
                        hint="Deixe vazio se não tem mensalidade" persistent-hint />
                    </v-col>
                    <v-col cols="6" md="3">
                      <v-text-field v-model.number="form.mensalidadeDiaVencimento" label="Dia do vencimento"
                        type="number" min="1" max="31" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="5">
                      <v-select v-model="form.mensalidadeCategoria" label="Categoria" clearable
                        :items="['Despesas Administrativas','Despesas Operacionais','Despesas Variáveis','Impostos','Pessoas']"
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
import GuiaPassos from '@/components/GuiaPassos.vue'
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useNotifStore } from '@/stores/notif'
import { useAuthStore } from '@/stores/auth'
import { cnpjRaw, maskCpfCnpj, formatarCpfCnpj } from '@/utils/documento'


const notif = useNotifStore()
const auth = useAuthStore()

// Monta uma mensagem legível a partir de qualquer formato de erro do backend
function msgErro(e: any, fallback: string): string {
  const d = e?.response?.data
  if (!d) return e?.message ?? fallback
  if (typeof d === 'string') return d
  if (Array.isArray(d?.erros) && d.erros.length)
    return d.erros.map((x: any) => `${x.campo ? x.campo + ': ' : ''}${x.mensagem}`).join(' | ')
  if (d?.detalhe) return `${d.mensagem ?? 'Erro'} — ${d.detalhe}`
  return d?.mensagem ?? fallback
}

const fornecedores = ref<any[]>([])
const carregando = ref(false)
const dialog = ref(false)
const editando = ref<string | null>(null)
const editandoTinhaCliente = ref(false)
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
  { title: 'CPF/CNPJ', key: 'cnpj' },
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
  editandoTinhaCliente.value = false
  form.value = { ativo: true, tipos: ['Fornecedor'], prazoPagamentoDias: 30, ehCliente: false }
  cnpjStatus.value = 'idle'
  dialog.value = true
}

async function editar(item: any) {
  editando.value = item.id
  editandoTinhaCliente.value = !!item.ehCliente
  form.value = { ...item }   // dados imediatos da lista
  cnpjStatus.value = 'idle'
  dialog.value = true
  // Busca o registro completo (endereço, IE, observação) para não perder campos ao salvar
  try {
    const { data } = await api.get(`/fornecedores/${item.id}`)
    form.value = { ...data, ehCliente: !!item.ehCliente }
  } catch { /* mantém dados da lista */ }
}

async function salvar() {
  const { valid } = await formRef.value?.validate()
  if (!valid) return
  salvando.value = true
  try {
    const payload = {
      ...form.value,
      tipos: (form.value.tipos ?? []).join(','),
      cnpj: cnpjRaw(form.value.cnpj ?? '') || null,             // remove máscara (. / -), preserva letras do CNPJ alfanumérico
      cep: (form.value.cep ?? '').replace(/\D/g, '') || null,   // só dígitos (coluna tem 8)
      telefone: (form.value.telefone ?? '').slice(0, 20) || null,
      celular: (form.value.celular ?? '').slice(0, 20) || null,
    }
    const temMensalidade = !!form.value.mensalidadeValor && !!form.value.mensalidadeDiaVencimento
    if (editando.value) {
      await api.put(`/fornecedores/${editando.value}`, payload)
    } else {
      const { data } = await api.post('/fornecedores', { ...payload, empresaId: auth.empresaId })
      // O comando de criação não trata mensalidade → grava via PUT quando houver.
      if (data?.id && temMensalidade) await api.put(`/fornecedores/${data.id}`, payload)
    }
    // Já lança a conta a pagar da mensalidade do mês corrente (idempotente).
    if (temMensalidade) {
      const r = await api.post('/despesas-fixas/gerar').catch(() => null)
      if (r?.data?.contasGeradas > 0) notif.ok(`Mensalidade lançada no Contas a Pagar (${r.data.contasGeradas}).`)
    }
    // Também é cliente → garante o cadastro na carteira (idempotente por CPF/CNPJ).
    if (form.value.ehCliente && !editandoTinhaCliente.value) {
      await api.post('/clientes/garantir', {
        empresaId: auth.empresaId, nome: form.value.razaoSocial,
        cpfCnpj: (form.value.cnpj ?? '').replace(/\D/g, '') || null,
        email: form.value.email || null,
        telefone: (form.value.telefone ?? '').slice(0, 20) || null,
      }).catch(() => null)
    }
    notif.ok('Fornecedor salvo com sucesso!')
    dialog.value = false
    await listar()
  } catch (e: any) {
    notif.erro(msgErro(e, 'Erro ao salvar fornecedor.'))
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
  if (cnpj.length !== 14) return  // CPF = 11 chars, não tem lookup
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


onMounted(listar)
</script>

<style scoped>
.cad-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; }
.cad-body { background:#f5f6f8; padding:16px; display:flex; flex-direction:column; gap:12px; }
.cad-secao { background:white; border-radius:12px; border:1px solid #e8edf3; overflow:hidden; }
.cad-secao-header { display:flex; align-items:center; gap:6px; padding:10px 16px; background:#f8f9fb; border-bottom:1px solid #e8edf3; font-size:0.75rem; font-weight:700; text-transform:uppercase; letter-spacing:0.07em; color:rgb(var(--v-theme-primary)); }
.cad-secao-body { padding:16px; }
</style>
