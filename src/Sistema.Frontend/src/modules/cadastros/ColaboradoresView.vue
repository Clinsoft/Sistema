<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div>
        <div class="text-h6 font-weight-bold">Colaboradores</div>
        <div class="text-caption text-medium-emphasis">Funcionários da empresa — dados de RH e acesso ao sistema (opcional)</div>
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
        'Clique em <b>Novo Colaborador</b> e informe os dados de funcionário (nome, CPF, cargo, salário, admissão). O <b>acesso ao sistema é opcional</b>.',
        'Quem só recebe salário fica <b>sem login</b> — e mesmo assim aparece como beneficiário no <b>Contas a Pagar</b>. Para dar acesso, ligue a chave <b>Acesso ao Sistema</b> e informe e-mail, senha e perfil.',
        'O <b>perfil</b> (Administrador, Atendente, Financeiro, Contador) define as permissões — a tabela mostra o que cada um pode ver, adicionar, editar e excluir.',
        'Use ✎ para editar, 🔑 para <b>redefinir a senha</b> (só quem tem acesso) e 🚫/✅ para <b>desativar/reativar</b>. Apenas <b>Administradores</b> gerenciam colaboradores.',
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
              <div class="text-caption text-medium-emphasis">
                {{ item.email || item.cpf || '—' }}
              </div>
            </div>
          </div>
        </template>

        <template #item.cargo="{ item }">
          <div class="text-body-2">{{ item.cargo || '—' }}</div>
          <div v-if="item.salario != null" class="text-caption text-medium-emphasis">
            {{ fmtSalario(item.salario) }}
          </div>
        </template>

        <template #item.acesso="{ item }">
          <v-chip v-if="item.temAcesso" size="small" :color="corPerfil(item.perfil || '')"
            variant="tonal" :prepend-icon="iconePerfil(item.perfil || '')">
            {{ item.perfil }}
          </v-chip>
          <v-chip v-else size="small" color="grey" variant="tonal" prepend-icon="mdi-account-off-outline">
            Sem acesso
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
          <v-btn v-if="item.temAcesso" icon="mdi-lock-reset" size="x-small" variant="text"
            color="warning" title="Alterar senha" @click="abrirSenha(item)" />
          <v-btn v-if="item.ativo" icon="mdi-account-off-outline" size="x-small"
            variant="text" color="error" title="Desativar" @click="desativar(item.id)" />
          <v-btn v-else icon="mdi-account-check-outline" size="x-small"
            variant="text" color="success" title="Reativar" @click="reativar(item.id)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- ══ Dialog: Novo / Editar ══════════════════════════════════════ -->
    <v-dialog v-model="dialogForm" max-width="720" persistent>
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
            <!-- Seção: Dados do colaborador -->
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-account-tie-outline</v-icon>
                Dados do colaborador
              </div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="12">
                    <v-text-field v-model="form.nome" label="Nome completo *"
                      variant="outlined" density="compact"
                      :rules="[r => !!r || 'Obrigatório']" />
                  </v-col>
                  <v-col cols="12" sm="6">
                    <v-text-field v-model="form.cpf" label="CPF"
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" sm="6">
                    <v-text-field v-model="form.telefone" label="Telefone"
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" sm="6">
                    <v-text-field v-model="form.cargo" label="Cargo"
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" sm="6">
                    <v-text-field v-model.number="form.salario" label="Salário" type="number"
                      prefix="R$" variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" sm="6">
                    <v-text-field v-model="form.dataAdmissao" label="Data de admissão" type="date"
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12">
                    <v-textarea v-model="form.observacao" label="Observação" rows="2" auto-grow
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12">
                    <v-checkbox v-model="form.ehCliente" density="compact" hide-details
                      :disabled="editandoTinhaCliente"
                      color="primary"
                      label="Também é cliente (cadastrar na carteira de clientes)" />
                    <div v-if="editandoTinhaCliente" class="text-caption text-medium-emphasis ml-8">
                      Já está cadastrado como cliente.
                    </div>
                  </v-col>
                </v-row>
              </div>
            </div>

            <!-- Seção: Acesso ao Sistema (opcional) -->
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-account-circle-outline</v-icon>
                Acesso ao Sistema
              </div>
              <div class="cad-secao-body">
                <v-switch v-model="form.darAcesso" color="primary" density="compact" hide-details
                  :label="form.darAcesso ? 'Este colaborador acessa o sistema (login)' : 'Sem acesso ao sistema (só cadastro/folha)'"
                  class="mb-2" />
                <v-row dense v-if="form.darAcesso">
                  <v-col cols="12">
                    <v-text-field v-model="form.email" label="E-mail de acesso *" type="email"
                      variant="outlined" density="compact"
                      :disabled="editandoTinhaAcesso"
                      :hint="editandoTinhaAcesso ? 'O e-mail de acesso não é alterado por aqui.' : ''"
                      persistent-hint />
                  </v-col>
                  <!-- Senha só ao conceder acesso (não tinha antes) -->
                  <template v-if="!editandoTinhaAcesso">
                    <v-col cols="12" md="6">
                      <v-text-field v-model="form.senha" label="Senha *"
                        :type="mostrarSenha ? 'text' : 'password'"
                        variant="outlined" density="compact"
                        :append-inner-icon="mostrarSenha ? 'mdi-eye-off' : 'mdi-eye'"
                        @click:append-inner="mostrarSenha = !mostrarSenha" />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field v-model="form.confirmarSenha" label="Confirmar senha *"
                        :type="mostrarSenha ? 'text' : 'password'"
                        variant="outlined" density="compact" />
                    </v-col>
                  </template>
                  <v-col cols="12">
                    <v-select v-model="form.perfil" label="Perfil de acesso *"
                      :items="perfis" variant="outlined" density="compact">
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
                  <v-col v-if="editandoTinhaAcesso" cols="12">
                    <div class="text-caption text-medium-emphasis">
                      Para trocar a senha, use o botão 🔑 na lista.
                    </div>
                  </v-col>
                </v-row>
              </div>
            </div>
            <!-- Seção: Permissões do Perfil -->
            <div class="cad-secao" v-if="form.darAcesso && form.perfil">
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
  id: string; nome: string; email: string | null
  cpf: string | null; telefone: string | null; cargo: string | null
  salario: number | null; dataAdmissao: string | null; observacao: string | null
  perfil: string | null; temAcesso: boolean; ehCliente: boolean; ativo: boolean; ultimoAcesso: string | null
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
const editandoTinhaAcesso = ref(false)
const editandoTinhaCliente = ref(false)
const idAlterandoSenha = ref('')
const nomeAlterandoSenha = ref('')
const novaSenha = ref('')
const confirmarNovaSenha = ref('')

const formPadrao = () => ({
  nome: '', cpf: '', telefone: '', cargo: '', salario: null as number | null,
  dataAdmissao: '', observacao: '', ehCliente: false,
  darAcesso: false, email: '', senha: '', confirmarSenha: '', perfil: 'Atendente',
})
const form = ref(formPadrao())

const headers = [
  { title: 'Colaborador', key: 'nome', sortable: true },
  { title: 'Cargo', key: 'cargo', width: 160 },
  { title: 'Acesso', key: 'acesso', width: 160 },
  { title: 'Status', key: 'ativo', width: 100 },
  { title: 'Último acesso', key: 'ultimoAcesso', width: 170 },
  { title: '', key: 'actions', sortable: false, width: 160 },
]

const fmtSalario = (v: number | null) =>
  v == null ? '—' : 'R$ ' + v.toLocaleString('pt-BR', { minimumFractionDigits: 2 })

const perfis = [
  { title: 'Administrador', value: 'Administrador' },
  { title: 'Atendente', value: 'Atendente' },
  { title: 'Financeiro', value: 'Financeiro' },
  { title: 'Contador', value: 'Contador' },
]

function corPerfil(p: string) {
  return { Administrador: 'error', Atendente: 'primary', Financeiro: 'success', Contador: 'warning' }[p] ?? 'default'
}
function iconePerfil(p: string) {
  return {
    Administrador: 'mdi-shield-crown-outline',
    Atendente: 'mdi-cash-register',
    Financeiro: 'mdi-currency-usd',
    Contador: 'mdi-calculator-variant-outline',
  }[p] ?? 'mdi-account-outline'
}
function descricaoPerfil(p: string) {
  return {
    Administrador: 'Acesso total ao sistema: cadastros, vendas, estoque, financeiro, fiscal e configurações.',
    Atendente: 'PDV/Vendas, Clientes, Etiquetas (só produtos), Controle de Validade e Marketing (Artes, Clube e Promoções). Sem acesso a financeiro, fiscal, estoque, configurações e templates.',
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
  Atendente: [
    { modulo: 'PDV / Vendas',               ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Clientes',                   ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Etiquetas (só produtos)',    ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Controle de Validade',       ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Marketing — Artes',          ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Marketing — Clube (s/ config)', ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Marketing — Promoções',      ver: T, adicionar: T, editar: T, excluir: F },
    { modulo: 'Demais módulos',             ver: F, adicionar: F, editar: F, excluir: F },
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
  editandoTinhaAcesso.value = false
  editandoTinhaCliente.value = false
  form.value = formPadrao()
  mostrarSenha.value = false
  dialogForm.value = true
}

function abrirEdicao(item: Colaborador) {
  editandoId.value = item.id
  editandoTinhaAcesso.value = item.temAcesso
  editandoTinhaCliente.value = item.ehCliente
  form.value = {
    nome: item.nome, cpf: item.cpf ?? '', telefone: item.telefone ?? '',
    cargo: item.cargo ?? '', salario: item.salario, dataAdmissao: item.dataAdmissao?.slice(0, 10) ?? '',
    observacao: item.observacao ?? '', ehCliente: item.ehCliente,
    darAcesso: item.temAcesso, email: item.email ?? '', senha: '', confirmarSenha: '',
    perfil: item.perfil ?? 'Atendente',
  }
  mostrarSenha.value = false
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

function validarAcesso(): string | null {
  const f = form.value
  if (!f.darAcesso) return null
  if (!f.email.trim()) return 'Informe o e-mail de acesso.'
  // Senha obrigatória só quando está concedendo acesso agora (não tinha antes).
  if (!editandoTinhaAcesso.value) {
    if (f.senha.length < 6) return 'A senha deve ter ao menos 6 caracteres.'
    if (f.senha !== f.confirmarSenha) return 'As senhas não conferem.'
  }
  return null
}

async function salvar() {
  const f = form.value
  if (!f.nome.trim()) { notif.erro('Informe o nome do colaborador.'); return }
  const erroAcesso = validarAcesso()
  if (erroAcesso) { notif.erro(erroAcesso); return }

  const dados = {
    empresaId: auth.empresaId, nome: f.nome.trim(),
    cpf: f.cpf.trim() || null, telefone: f.telefone.trim() || null,
    cargo: f.cargo.trim() || null, salario: f.salario,
    dataAdmissao: f.dataAdmissao || null, observacao: f.observacao.trim() || null,
  }
  const acesso = { email: f.email.trim(), senha: f.senha, perfil: f.perfil }

  salvando.value = true
  try {
    if (editandoId.value) {
      const id = editandoId.value
      await api.put(`/usuarios/${id}`, dados)
      // Ajusta o acesso conforme a mudança
      if (editandoTinhaAcesso.value && !f.darAcesso) {
        await api.delete(`/usuarios/${id}/acesso`)
      } else if (!editandoTinhaAcesso.value && f.darAcesso) {
        await api.post(`/usuarios/${id}/acesso`, acesso)
      } else if (editandoTinhaAcesso.value && f.darAcesso && f.perfil !== '') {
        await api.patch(`/usuarios/${id}/perfil`, { perfil: f.perfil })
      }
      notif.ok('Colaborador atualizado!')
    } else {
      await api.post('/usuarios', { ...dados, acesso: f.darAcesso ? acesso : null })
      notif.ok('Colaborador criado!')
    }
    // Também é cliente → garante o cadastro na carteira (idempotente por CPF).
    if (f.ehCliente && !editandoTinhaCliente.value) {
      await api.post('/clientes/garantir', {
        empresaId: auth.empresaId, nome: f.nome.trim(),
        cpfCnpj: f.cpf.trim() || null, telefone: f.telefone.trim() || null,
      }).catch(() => null)
    }
    dialogForm.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? e?.response?.data ?? 'Erro ao salvar.')
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
