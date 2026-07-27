<template>
  <div>
    <div class="d-flex align-center mb-1">
      <div class="flex-grow-1">
        <div class="text-h6 font-weight-bold">Contadores</div>
        <div class="text-caption text-medium-emphasis">Contabilidade — acesso ao sistema e vínculo com o fornecedor de honorários</div>
      </div>
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">Novo Contador</v-btn>
    </div>

    <v-data-table :headers="headers" :items="contadores" :loading="carregando" density="comfortable" class="mt-3">
      <template #item.temAcesso="{ item }">
        <v-chip v-if="item.temAcesso" size="small" color="teal" variant="tonal" prepend-icon="mdi-key">Com acesso</v-chip>
        <v-chip v-else size="small" variant="tonal">Sem acesso</v-chip>
      </template>
      <template #item.fornecedorNome="{ item }">{{ item.fornecedorNome || '—' }}</template>
      <template #item.ultimoAcesso="{ item }">
        <span class="text-caption">{{ item.ultimoAcesso ? new Date(item.ultimoAcesso).toLocaleString('pt-BR') : 'Nunca' }}</span>
      </template>
      <template #item.ativo="{ item }">
        <v-chip size="small" :color="item.ativo ? 'success' : 'grey'" variant="tonal">{{ item.ativo ? 'Ativo' : 'Inativo' }}</v-chip>
      </template>
      <template #item.acoes="{ item }">
        <v-btn icon="mdi-pencil-outline" size="x-small" color="primary" variant="text" @click="editar(item)" />
        <v-btn :icon="item.ativo ? 'mdi-cancel' : 'mdi-check'" size="x-small"
          :color="item.ativo ? 'error' : 'success'" variant="text" @click="alternarAtivo(item)" />
      </template>
    </v-data-table>

    <v-dialog v-model="dialog" max-width="520" persistent>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center">
          <v-icon start color="primary">mdi-calculator-variant-outline</v-icon>
          {{ editando ? 'Editar contador' : 'Novo contador' }}
        </v-card-title>
        <v-card-text>
          <v-text-field v-model="form.nome" label="Nome *" variant="outlined" density="compact" autofocus class="mb-2" />
          <v-text-field v-model="form.email" label="E-mail de acesso *" type="email"
            variant="outlined" density="compact" class="mb-2"
            :disabled="editando" :hint="editando ? 'O e-mail de acesso não é alterado por aqui.' : ''" persistent-hint />
          <v-text-field v-model="form.telefone" label="Telefone" variant="outlined" density="compact" class="mb-2" />
          <v-text-field v-model="form.senha"
            :label="editando ? 'Nova senha (deixe vazio para manter)' : 'Senha *'"
            :type="mostrarSenha ? 'text' : 'password'" variant="outlined" density="compact" class="mb-2"
            :append-inner-icon="mostrarSenha ? 'mdi-eye-off' : 'mdi-eye'"
            @click:append-inner="mostrarSenha = !mostrarSenha"
            hint="Mínimo 6 caracteres" persistent-hint />
          <v-select v-model="form.fornecedorId" label="Fornecedor de honorários (opcional)"
            :items="fornecedores" item-title="razaoSocial" item-value="id" clearable
            variant="outlined" density="compact"
            hint="Vincula a contabilidade ao fornecedor que recebe a mensalidade" persistent-hint />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando"
            :disabled="!form.nome || !form.email" @click="salvar">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const carregando = ref(false)
const salvando = ref(false)
const dialog = ref(false)
const mostrarSenha = ref(false)
const editando = ref<string | null>(null)
const contadores = ref<any[]>([])
const fornecedores = ref<any[]>([])
const form = ref<any>({})

const headers = [
  { title: 'Nome', key: 'nome' },
  { title: 'E-mail', key: 'email' },
  { title: 'Fornecedor (honorários)', key: 'fornecedorNome' },
  { title: 'Acesso', key: 'temAcesso' },
  { title: 'Último acesso', key: 'ultimoAcesso' },
  { title: 'Situação', key: 'ativo' },
  { title: '', key: 'acoes', sortable: false, align: 'end' as const },
]

async function listar() {
  carregando.value = true
  try {
    const { data } = await api.get('/contadores', { params: { empresaId: auth.empresaId } })
    contadores.value = data
  } finally { carregando.value = false }
}

async function carregarFornecedores() {
  try {
    const { data } = await api.get('/fornecedores', { params: { empresaId: auth.empresaId } })
    fornecedores.value = Array.isArray(data) ? data : (data.itens ?? [])
  } catch { fornecedores.value = [] }
}

function abrirNovo() {
  editando.value = null
  form.value = { nome: '', email: '', telefone: '', senha: '', fornecedorId: null }
  mostrarSenha.value = false
  dialog.value = true
}

function editar(item: any) {
  editando.value = item.id
  form.value = { nome: item.nome, email: item.email, telefone: item.telefone, senha: '', fornecedorId: item.fornecedorId }
  mostrarSenha.value = false
  dialog.value = true
}

async function salvar() {
  salvando.value = true
  try {
    const payload = {
      empresaId: auth.empresaId, nome: form.value.nome, email: form.value.email,
      telefone: form.value.telefone || null, senha: form.value.senha || null,
      fornecedorId: form.value.fornecedorId || null,
    }
    if (editando.value) await api.put(`/contadores/${editando.value}`, payload)
    else await api.post('/contadores', payload)
    notif.ok('Contador salvo com sucesso!')
    dialog.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem || 'Erro ao salvar contador.')
  } finally { salvando.value = false }
}

async function alternarAtivo(item: any) {
  try {
    await api.patch(`/contadores/${item.id}/${item.ativo ? 'desativar' : 'reativar'}`)
    await listar()
  } catch { notif.erro('Erro ao alterar situação.') }
}

onMounted(() => { listar(); carregarFornecedores() })
</script>
