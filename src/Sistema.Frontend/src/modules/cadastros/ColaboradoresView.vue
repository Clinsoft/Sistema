<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div>
        <div class="text-h6 font-weight-bold">Colaboradores</div>
        <div class="text-caption text-medium-emphasis">Usuários do sistema com acesso e perfil de permissão</div>
      </div>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">
        Novo Colaborador
      </v-btn>
    </div>

    <GuiaPassos
      id="colaboradores"
      titulo="Como usar o cadastro de Colaboradores"
      :passos="[
        'Clique em <b>Novo Colaborador</b> e informe nome, e-mail e senha — o e-mail e a senha são usados para <b>acessar o sistema</b>.',
        'Escolha o <b>perfil de acesso</b> (Administrador, Vendedor, Financeiro, Contador). A tabela de permissões mostra exatamente o que cada perfil pode <b>ver, adicionar, editar e excluir</b>.',
        'Use ✎ para editar nome/perfil, 🔑 para <b>redefinir a senha</b> e 🚫/✅ para <b>desativar/reativar</b> o acesso.',
        'Apenas <b>Administradores</b> podem gerenciar colaboradores. O e-mail não pode ser alterado após criado.',
      ]"
    />

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table
        :headers="headers"
        :items="colaboradores"
        :loading="carregando"
        density="comfortable"
        hover
      >
        <template #item.nome="{ item }">
          <div class="d-flex align-center ga-3 py-1">
            <v-avatar color="primary" size="36" variant="tonal">
              <span class="text-caption font-weight-bold">{{ iniciais(item.nome) }}</span>
            </v-avatar>
            <div>
              <div class="text-body-2 font-weight-medium">{{ item.nome }}</div>
              <div class="text-caption text-medium-emphasis">{{ item.email }}</div>
            </div>
          </div>
        </template>

        <template #item.perfil="{ item }">
          <v-chip
            size="small"
            :color="corPerfil(item.perfil)"
            variant="tonal"
            :prepend-icon="iconePerfil(item.perfil)"
          >
            {{ item.perfil }}
          </v-chip>
        </template>

        <template #item.ativo="{ item }">
          <v-chip size="small" :color="item.ativo ? 'success' : 'default'" variant="tonal">
            {{ item.ativo ? 'Ativo' : 'Inativo' }}
          </v-chip>
        </template>

        <template #item.ultimoAcesso="{ item }">
          <span class="text-caption text-medium-emphasis">
            {{ item.ultimoAcesso ? new Date(item.ultimoAcesso).toLocaleString('pt-BR') : 'Nunca' }}
          </span>
        </template>

        <template #item.actions="{ item }">
          <v-btn icon="mdi-pencil-outline" size="x-small" variant="text"
            color="primary" title="Editar" @click="abrirEdicao(item)" />
          <v-btn icon="mdi-lock-reset" size="x-small" variant="text"
            color="warning" title="Alterar senha" @click="abrirSenha(item)" />
          <v-btn v-if="item.ativo" icon="mdi-account-off-outline" size="x-small"
            variant="text" color="error" title="Desativar" @click="desativar(item.id)" />
          <v-btn v-else icon="mdi-account-check-outline" size="x-small"
            variant="text" color="success" title="Reativar" @click="reativar(item.id)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- ══ Dialog: Novo / Editar ══════════════════════════════════════ -->
    <v-dialog v-model="dialogForm" max-width="560" persistent>
      <v-card rounded="xl" style="display:flex;flex-direction:column;max-height:90vh">
        <div class="cad-header">
          <div class="d-flex align-center" style="gap:12px">
            <v-avatar color="primary" size="40">
              <v-icon>mdi-account-tie-outline</v-icon>
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">
                {{ editandoId ? 'Editar Colaborador' : 'Novo Colaborador' }}
              </div>
              <div class="text-caption text-medium-emphasis">Acesso e permissões do usuário</div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialogForm = false" />
        </div>
        <v-card-text class="pa-0" style="overflow-y:auto">
          <div class="cad-body">
            <!-- Seção: Acesso ao Sistema -->
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-account-circle-outline</v-icon>
                Acesso ao Sistema
              </div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="12">
                    <v-text-field v-model="form.nome" label="Nome completo *"
                      variant="outlined" density="compact"
                      :rules="[r => !!r || 'Obrigatório']" />
                  </v-col>
                  <v-col cols="12">
                    <v-text-field v-model="form.email" label="E-mail *" type="email"
                      variant="outlined" density="compact"
                      :disabled="!!editandoId"
                      :rules="[r => !!r || 'Obrigatório']" />
                  </v-col>
                  <!-- Senha só no cadastro inicial -->
                  <template v-if="!editandoId">
                    <v-col cols="12" md="6">
                      <v-text-field v-model="form.senha" label="Senha *"
                        :type="mostrarSenha ? 'text' : 'password'"
                        variant="outlined" density="compact"
                        :append-inner-icon="mostrarSenha ? 'mdi-eye-off' : 'mdi-eye'"
                        @click:append-inner="mostrarSenha = !mostrarSenha"
                        :rules="[r => !!r || 'Obrigatório', r => r.length >= 6 || 'Mínimo 6 caracteres']" />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field v-model="form.confirmarSenha" label="Confirmar senha *"
                        :type="mostrarSenha ? 'text' : 'password'"
                        variant="outlined" density="compact"
                        :rules="[r => r === form.senha || 'Senhas não conferem']" />
                    </v-col>
                  </template>
                  <v-col cols="12">
                    <v-select v-model="form.perfil" label="Perfil de acesso *"
                      :items="perfis" variant="outlined" density="compact"
                      :rules="[r => !!r || 'Obrigatório']">
                      <template #item="{ item, props }">
                        <v-list-item v-bind="props">
                          <template #prepend>
                            <v-icon :icon="iconePerfil(item.value)" class="mr-2" />
                          </template>
                          <template #subtitle>{{ descricaoPerfil(item.value) }}</template>
                        </v-list-item>
                      </template>
                    </v-select>
                  </v-col>
                </v-row>
              </div>
            </div>
            <!-- Seção: Permissões do Perfil -->
            <div class="cad-secao" v-if="form.perfil">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-shield-outline</v-icon>
                Permissões do perfil {{ form.perfil }}
              </div>
              <div class="cad-secao-body">
                <v-alert type="info" variant="tonal" density="compact" rounded="lg" class="mb-3">
                  <div class="text-caption">{{ descricaoPerfil(form.perfil) }}</div>
                </v-alert>
                <v-table density="compact" class="perm-tabela">
                  <thead>
                    <tr>
                      <th>Módulo</th>
                      <th class="text-center">Ver</th>
                      <th class="text-center">Adicionar</th>
                      <th class="text-center">Editar</th>
                      <th class="text-center">Excluir</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="p in permissoesPerfil(form.perfil)" :key="p.modulo">
                      <td class="text-body-2">{{ p.modulo }}</td>
                      <td class="text-center"><v-icon :color="p.ver ? 'success' : 'grey-lighten-1'" size="16">{{ p.ver ? 'mdi-check-circle' : 'mdi-minus' }}</v-icon></td>
                      <td class="text-center"><v-icon :color="p.adicionar ? 'success' : 'grey-lighten-1'" size="16">{{ p.adicionar ? 'mdi-check-circle' : 'mdi-minus' }}</v-icon></td>
                      <td class="text-center"><v-icon :color="p.editar ? 'success' : 'grey-lighten-1'" size="16">{{ p.editar ? 'mdi-check-circle' : 'mdi-minus' }}</v-icon></td>
                      <td class="text-center"><v-icon :color="p.excluir ? 'success' : 'grey-lighten-1'" size="16">{{ p.excluir ? 'mdi-check-circle' : 'mdi-minus' }}</v-icon></td>
                    </tr>
                  </tbody>
                </v-table>
                <div class="text-caption text-medium-emphasis mt-2">
                  <v-icon size="12">mdi-information-outline</v-icon>
                  As permissões são aplicadas pelo perfil. Para acesso diferente, altere o perfil do colaborador.
                </div>
              </div>
            </div>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="dialogForm = false">Cancelar</v-btn>
          <v-btn color="primary" size="large" rounded="lg" :loading="salvando" @click="salvar">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ══ Dialog: Alterar senha ══════════════════════════════════════ -->
    <v-dialog v-model="dialogSenha" max-width="400">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">Alterar Senha</v-card-title>
        <v-card-text class="pa-4 pt-2">
          <div class="text-body-2 text-medium-emphasis mb-3">
            Colaborador: <strong>{{ nomeAlterandoSenha }}</strong>
          </div>
          <v-text-field v-model="novaSenha" label="Nova senha *"
            :type="mostrarNovaSenha ? 'text' : 'password'"
            variant="outlined" density="compact"
            :append-inner-icon="mostrarNovaSenha ? 'mdi-eye-off' : 'mdi-eye'"
            @click:append-inner="mostrarNovaSenha = !mostrarNovaSenha"
            :rules="[r => r.length >= 6 || 'Mínimo 6 caracteres']"
            class="mb-2" />
          <v-text-field v-model="confirmarNovaSenha" label="Confirmar senha *"
            :type="mostrarNovaSenha ? 'text' : 'password'"
            variant="outlined" density="compact"
            :rules="[r => r === novaSenha || 'Senhas não conferem']" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogSenha = false">Cancelar</v-btn>
          <v-btn color="warning" :loading="salvandoSenha" @click="salvarSenha">Alterar</v-btn>
        </v-card-actions>
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

const auth = useAuthStore()
const notif = useNotifStore()

interface Colaborador {
  id: string; nome: string; email: string
  perfil: string; ativo: boolean; ultimoAcesso: string | null
}

const colaboradores = ref<Colaborador[]>([])
const carregando = ref(false)
const dialogForm = ref(false)
const dialogSenha = ref(false)
const salvando = ref(false)
const salvandoSenha = ref(false)
const mostrarSenha = ref(false)
const mostrarNovaSenha = ref(false)
const editandoId = ref<string | null>(null)
const idAlterandoSenha = ref('')
const nomeAlterandoSenha = ref('')
const novaSenha = ref('')
const confirmarNovaSenha = ref('')

const form = ref({ nome: '', email: '', senha: '', confirmarSenha: '', perfil: '' })

const headers = [
  { title: 'Colaborador', key: 'nome', sortable: true },
  { title: 'Perfil', key: 'perfil', width: 150 },
  { title: 'Status', key: 'ativo', width: 100 },
  { title: 'Último acesso', key: 'ultimoAcesso', width: 180 },
  { title: '', key: 'actions', sortable: false, width: 130 },
]

const perfis = [
  { title: 'Administrador', value: 'Administrador' },
  { title: 'Vendedor', value: 'Vendedor' },
  { title: 'Financeiro', value: 'Financeiro' },
  { title: 'Contador', value: 'Contador' },
]

function corPerfil(p: string) {
  return { Administrador: 'error', Vendedor: 'primary', Financeiro: 'success', Contador: 'warning' }[p] ?? 'default'
}
function iconePerfil(p: string) {
  return {
    Administrador: 'mdi-shield-crown-outline',
    Vendedor: 'mdi-cash-register',
    Financeiro: 'mdi-currency-usd',
    Contador: 'mdi-calculator-variant-outline',
  }[p] ?? 'mdi-account-outline'
}
function descricaoPerfil(p: string) {
  return {
    Administrador: 'Acesso total ao sistema: cadastros, vendas, estoque, financeiro, fiscal e configurações.',
    Vendedor: 'Acesso ao PDV, histórico de vendas e consulta de produtos. Sem acesso a financeiro e configurações.',
    Financeiro: 'Acesso ao módulo financeiro (contas, DRE, fluxo de caixa) e relatórios. Sem acesso ao PDV.',
    Contador: 'Acesso exclusivo ao Painel do Contador: XMLs, resumo fiscal e dados da empresa.',
  }[p] ?? ''
}
function iniciais(nome: string) {
  return nome.trim().split(' ').slice(0, 2).map(n => n[0].toUpperCase()).join('')
}

// Matriz de permissões por perfil, refletindo o controle de acesso real da API (Authorize Roles).
// V/A/E/X = Ver / Adicionar / Editar / Excluir
interface PermModulo { modulo: string; ver: boolean; adicionar: boolean; editar: boolean; excluir: boolean }
const T = true, F = false
const matrizPermissoes: Record<string, PermModulo[]> = {
  Administrador: [
    { modulo: 'PDV / Vendas',            ver: T, adicionar: T, editar: T, excluir: T },
    { modulo: 'Cadastros (clientes, produtos…)', ver: T, adicionar: T, editar: T, excluir: T },
    { modulo: 'Estoque e Compras',       ver: T, adicionar: T, editar: T, excluir: T },
    { modulo: 'Financeiro (contas, DRE)', ver: T, adicionar: T, editar: T, excluir: T },
    { modulo: 'Fiscal (NF-e, SPED)',      ver: T, adicionar: T, editar: T, excluir: T },
    { modulo: 'Contabilidade',            ver: T, adicionar: T, editar: T, excluir: T },
    { modulo: 'Configurações e Empresa',  ver: T, adicionar: T, editar: T, excluir: T },
    { modulo: 'Colaboradores / Usuários', ver: T, adicionar: T, editar: T, excluir: T },
  ],
  Vendedor: [
    { modulo: 'PDV / Vendas',            ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Cadastros (clientes, produtos…)', ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Estoque e Compras',       ver: T, adicionar: F, editar: F, excluir: F },
    { modulo: 'Financeiro (contas, DRE)', ver: F, adicionar: F, editar: F, excluir: F },
    { modulo: 'Fiscal (NF-e, SPED)',      ver: F, adicionar: F, editar: F, excluir: F },
    { modulo: 'Contabilidade',            ver: F, adicionar: F, editar: F, excluir: F },
    { modulo: 'Configurações e Empresa',  ver: F, adicionar: F, editar: F, excluir: F },
    { modulo: 'Colaboradores / Usuários', ver: F, adicionar: F, editar: F, excluir: F },
  ],
  Financeiro: [
    { modulo: 'PDV / Vendas',            ver: T, adicionar: F, editar: F, excluir: F },
    { modulo: 'Cadastros (clientes, produtos…)', ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Estoque e Compras',       ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Financeiro (contas, DRE)', ver: T, adicionar: T, editar: T, excluir: T },
    { modulo: 'Fiscal (NF-e, SPED)',      ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Contabilidade',            ver: F, adicionar: F, editar: F, excluir: F },
    { modulo: 'Configurações e Empresa',  ver: F, adicionar: F, editar: F, excluir: F },
    { modulo: 'Colaboradores / Usuários', ver: F, adicionar: F, editar: F, excluir: F },
  ],
  Contador: [
    { modulo: 'PDV / Vendas',            ver: F, adicionar: F, editar: F, excluir: F },
    { modulo: 'Cadastros (clientes, produtos…)', ver: T, adicionar: F, editar: F, excluir: F },
    { modulo: 'Estoque e Compras',       ver: F, adicionar: F, editar: F, excluir: F },
    { modulo: 'Financeiro (contas, DRE)', ver: T, adicionar: F, editar: F, excluir: F },
    { modulo: 'Fiscal (NF-e, SPED)',      ver: T, adicionar: F, editar: F, excluir: F },
    { modulo: 'Contabilidade',            ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Configurações e Empresa',  ver: T, adicionar: F, editar: T, excluir: F },
    { modulo: 'Colaboradores / Usuários', ver: F, adicionar: F, editar: F, excluir: F },
  ],
}
function permissoesPerfil(p: string): PermModulo[] {
  return matrizPermissoes[p] ?? []
}

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/usuarios', { params: { empresaId: auth.empresaId } })
    colaboradores.value = r.data
  } finally { carregando.value = false }
}

function abrirNovo() {
  editandoId.value = null
  form.value = { nome: '', email: '', senha: '', confirmarSenha: '', perfil: 'Vendedor' }
  mostrarSenha.value = false
  dialogForm.value = true
}

function abrirEdicao(item: Colaborador) {
  editandoId.value = item.id
  form.value = { nome: item.nome, email: item.email, senha: '', confirmarSenha: '', perfil: item.perfil }
  dialogForm.value = true
}

function abrirSenha(item: Colaborador) {
  idAlterandoSenha.value = item.id
  nomeAlterandoSenha.value = item.nome
  novaSenha.value = ''
  confirmarNovaSenha.value = ''
  mostrarNovaSenha.value = false
  dialogSenha.value = true
}

async function salvar() {
  salvando.value = true
  try {
    if (editandoId.value) {
      await api.put(`/usuarios/${editandoId.value}`, { nome: form.value.nome, perfil: form.value.perfil })
      notif.ok('Colaborador atualizado!')
    } else {
      await api.post('/usuarios', {
        empresaId: auth.empresaId,
        nome: form.value.nome,
        email: form.value.email,
        senha: form.value.senha,
        perfil: form.value.perfil,
      })
      notif.ok('Colaborador criado!')
    }
    dialogForm.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e?.response?.data ?? 'Erro ao salvar.')
  } finally { salvando.value = false }
}

async function salvarSenha() {
  if (novaSenha.value !== confirmarNovaSenha.value) {
    notif.erro('Senhas não conferem.')
    return
  }
  salvandoSenha.value = true
  try {
    await api.patch(`/usuarios/${idAlterandoSenha.value}/senha`, { novaSenha: novaSenha.value })
    notif.ok('Senha alterada!')
    dialogSenha.value = false
  } catch (e: any) {
    notif.erro(e?.response?.data ?? 'Erro ao alterar senha.')
  } finally { salvandoSenha.value = false }
}

async function desativar(id: string) {
  await api.patch(`/usuarios/${id}/desativar`)
  notif.ok('Colaborador desativado.')
  await carregar()
}

async function reativar(id: string) {
  await api.patch(`/usuarios/${id}/reativar`)
  notif.ok('Colaborador reativado.')
  await carregar()
}

onMounted(carregar)
</script>

<style scoped>
.cad-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; }
.cad-body { background:#f5f6f8; padding:16px; display:flex; flex-direction:column; gap:12px; }
.cad-secao { background:white; border-radius:12px; border:1px solid #e8edf3; overflow:hidden; }
.cad-secao-header { display:flex; align-items:center; gap:6px; padding:10px 16px; background:#f8f9fb; border-bottom:1px solid #e8edf3; font-size:0.75rem; font-weight:700; text-transform:uppercase; letter-spacing:0.07em; color:rgb(var(--v-theme-primary)); }
.cad-secao-body { padding:16px; }
.perm-tabela th { font-size:0.7rem; font-weight:700; text-transform:uppercase; letter-spacing:0.04em; color:#64748b; }
.perm-tabela td, .perm-tabela th { padding:4px 8px !important; height:auto !important; }
</style>
