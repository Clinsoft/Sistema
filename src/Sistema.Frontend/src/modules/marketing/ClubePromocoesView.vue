<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div>
        <div class="text-h6 font-weight-bold">Clube de Promoções</div>
        <div class="text-caption text-medium-emphasis">Programa de fidelidade com cashback e benefícios exclusivos</div>
      </div>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-cog-outline" variant="tonal" class="mr-2" @click="dialogConfig = true">
        Configurações
      </v-btn>
      <v-btn color="success" prepend-icon="mdi-account-plus" @click="abrirNovoMembro">
        Novo Membro
      </v-btn>
    </div>

    <!-- Cards de resumo -->
    <v-row class="mb-4">
      <v-col v-for="card in cardsResumo" :key="card.label" cols="12" sm="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="d-flex align-center pa-4">
            <v-avatar :color="card.cor" size="48" class="mr-3">
              <v-icon :icon="card.icon" color="white" size="24" />
            </v-avatar>
            <div>
              <div class="text-h6 font-weight-bold">{{ card.valor }}</div>
              <div class="text-caption text-medium-emphasis">{{ card.label }}</div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Tabs: Membros / Cashback / Configurações -->
    <v-card rounded="xl" elevation="1">
      <v-tabs v-model="tab" color="primary">
        <v-tab value="membros" prepend-icon="mdi-account-group">Membros</v-tab>
        <v-tab value="cashback" prepend-icon="mdi-cash-refund">Saldo de Cashback</v-tab>
        <v-tab value="historico" prepend-icon="mdi-history">Histórico</v-tab>
      </v-tabs>
      <v-divider />

      <v-window v-model="tab">
        <!-- Membros -->
        <v-window-item value="membros">
          <v-card-text class="pa-4">
            <v-row dense class="mb-3">
              <v-col cols="12" md="5">
                <v-text-field v-model="buscaMembro" placeholder="Buscar por nome ou CPF..."
                  prepend-inner-icon="mdi-magnify" variant="outlined" density="compact" hide-details clearable />
              </v-col>
              <v-col cols="12" md="3">
                <v-select v-model="filtroStatus" :items="[{title:'Todos',value:null},{title:'Ativos',value:'Ativo'},{title:'Inativos',value:'Inativo'}]"
                  label="Status" variant="outlined" density="compact" hide-details />
              </v-col>
            </v-row>
            <v-data-table :headers="headersMembros" :items="membros" :loading="carregando" density="compact" hover>
              <template #item.nome="{ item }">
                <div class="font-weight-medium">{{ item.nome }}</div>
                <div class="text-caption text-medium-emphasis">{{ item.cpf }}</div>
              </template>
              <template #item.saldoCashback="{ item }">
                <span class="font-weight-bold text-success">R$ {{ fmt(item.saldoCashback ?? 0) }}</span>
              </template>
              <template #item.totalCompras="{ item }">R$ {{ fmt(item.totalCompras ?? 0) }}</template>
              <template #item.status="{ item }">
                <v-chip :color="item.status === 'Ativo' ? 'success' : 'default'" size="small" variant="tonal">
                  {{ item.status }}
                </v-chip>
              </template>
              <template #item.acoes="{ item }">
                <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="editarMembro(item)" />
                <v-btn icon="mdi-cash-plus" size="x-small" variant="text" color="success"
                  title="Ajuste manual de cashback" @click="abrirAjuste(item)" />
              </template>
            </v-data-table>
          </v-card-text>
        </v-window-item>

        <!-- Saldo de Cashback -->
        <v-window-item value="cashback">
          <v-card-text class="pa-4">
            <v-alert type="info" variant="tonal" class="mb-4">
              <strong>Cashback automático:</strong> configurado em {{ config.percentualCashback }}% sobre o valor da compra.
              O saldo é creditado automaticamente após cada venda finalizada no PDV para clientes do clube.
            </v-alert>
            <v-data-table :headers="headersCashback" :items="movimentosCashback" :loading="carregando" density="compact">
              <template #item.valor="{ item }">
                <span :class="item.tipo === 'Credito' ? 'text-success' : 'text-error'" class="font-weight-bold">
                  {{ item.tipo === 'Credito' ? '+' : '-' }}R$ {{ fmt(item.valor) }}
                </span>
              </template>
              <template #item.tipo="{ item }">
                <v-chip :color="item.tipo === 'Credito' ? 'success' : 'warning'" size="x-small" variant="tonal">
                  {{ item.tipo }}
                </v-chip>
              </template>
              <template #item.data="{ item }">{{ fmtData(item.data) }}</template>
            </v-data-table>
          </v-card-text>
        </v-window-item>

        <!-- Histórico -->
        <v-window-item value="historico">
          <v-card-text class="pa-4">
            <v-data-table :headers="headersHistorico" :items="historico" :loading="carregando" density="compact">
              <template #item.desconto="{ item }">R$ {{ fmt(item.desconto ?? 0) }}</template>
              <template #item.cashbackGerado="{ item }">
                <span class="text-success">+R$ {{ fmt(item.cashbackGerado ?? 0) }}</span>
              </template>
              <template #item.data="{ item }">{{ fmtData(item.data) }}</template>
            </v-data-table>
          </v-card-text>
        </v-window-item>
      </v-window>
    </v-card>

    <!-- Dialog Membro -->
    <v-dialog v-model="dialogMembro" max-width="560" persistent>
      <v-card rounded="xl">
        <div class="cad-header">
          <div class="d-flex align-center gap-3">
            <v-avatar color="purple" size="40">
              <v-icon color="white">mdi-account-star</v-icon>
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">{{ editandoMembro ? 'Editar Membro' : 'Novo Membro' }}</div>
              <div class="text-caption text-medium-emphasis">Cadastro no Clube de Promoções</div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialogMembro = false" />
        </div>
        <v-card-text class="pa-4">
          <v-row dense>
            <v-col cols="12" md="6">
              <v-autocomplete v-model="formMembro.clienteId" :items="clientes"
                item-title="nome" item-value="id" label="Cliente *"
                variant="outlined" density="compact"
                :rules="[r => !!r || 'Obrigatório']"
                hint="Busque pelo nome ou CPF/CNPJ" persistent-hint />
            </v-col>
            <v-col cols="12" md="3">
              <v-select v-model="formMembro.status" :items="['Ativo','Inativo']"
                label="Status" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" md="3">
              <v-text-field v-model="formMembro.dataAdesao" label="Adesão"
                type="date" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="formMembro.observacao" label="Observação"
                variant="outlined" density="compact" rows="2" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogMembro = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvando" @click="salvarMembro">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog Ajuste Manual de Cashback -->
    <v-dialog v-model="dialogAjuste" max-width="400" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon icon="mdi-cash-plus" color="success" class="mr-2" />
          Ajuste de Cashback
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <div class="text-body-2 mb-3 text-medium-emphasis">
            Cliente: <strong>{{ membroAjuste?.nome }}</strong><br>
            Saldo atual: <strong class="text-success">R$ {{ fmt(membroAjuste?.saldoCashback ?? 0) }}</strong>
          </div>
          <v-select v-model="ajuste.tipo" :items="['Credito','Debito']"
            label="Tipo de ajuste" variant="outlined" density="compact" class="mb-3" />
          <v-text-field v-model.number="ajuste.valor" label="Valor (R$)" type="number"
            variant="outlined" density="compact" prefix="R$" class="mb-3" />
          <v-text-field v-model="ajuste.motivo" label="Motivo *"
            variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogAjuste = false">Cancelar</v-btn>
          <v-btn color="success" :loading="salvando" @click="salvarAjuste">Aplicar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog Configurações do Clube -->
    <v-dialog v-model="dialogConfig" max-width="600" persistent>
      <v-card rounded="xl">
        <div class="cad-header">
          <div class="d-flex align-center gap-3">
            <v-avatar color="deep-purple" size="40">
              <v-icon color="white">mdi-star-settings</v-icon>
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">Configurações do Clube</div>
              <div class="text-caption text-medium-emphasis">Regras de cashback e benefícios</div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialogConfig = false" />
        </div>
        <v-card-text class="pa-0">
          <div class="cad-body">
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-cash-refund</v-icon>
                Cashback
              </div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="12" md="6">
                    <v-text-field v-model.number="config.percentualCashback" label="% de cashback por compra"
                      type="number" suffix="%" variant="outlined" density="compact"
                      hint="Ex: 5 = 5% do valor da compra vira crédito" persistent-hint />
                  </v-col>
                  <v-col cols="12" md="6">
                    <v-text-field v-model.number="config.validade" label="Validade do cashback (dias)"
                      type="number" suffix="dias" variant="outlined" density="compact"
                      hint="0 = sem validade" persistent-hint />
                  </v-col>
                  <v-col cols="12" md="6">
                    <v-text-field v-model.number="config.minimoResgate" label="Mínimo para resgate (R$)"
                      type="number" prefix="R$" variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" md="6">
                    <v-text-field v-model.number="config.limiteUsoPercent" label="Limite de uso por compra"
                      type="number" suffix="%" variant="outlined" density="compact"
                      hint="Ex: 50 = cliente usa até 50% do total em cashback" persistent-hint />
                  </v-col>
                </v-row>
              </div>
            </div>
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-account-star-outline</v-icon>
                Benefícios
              </div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="12">
                    <v-text-field v-model="config.nomeClubeExibicao" label="Nome do clube (exibição)"
                      variant="outlined" density="compact"
                      hint="Ex: Clube Natural, EcoClub, Fidelidade EcoGranel" persistent-hint />
                  </v-col>
                  <v-col cols="12" md="6">
                    <v-text-field v-model.number="config.descontoMembro" label="Desconto fixo para membros"
                      type="number" suffix="%" variant="outlined" density="compact"
                      hint="Desconto automático na venda além do cashback" persistent-hint />
                  </v-col>
                  <v-col cols="12" md="6">
                    <v-switch v-model="config.aniversarianteDuplo" label="Cashback dobrado no aniversário"
                      color="primary" density="compact" hide-details />
                  </v-col>
                  <v-col cols="12">
                    <v-switch v-model="config.ativo" label="Clube ativo (aceita novos membros e aplica cashback)"
                      color="success" density="compact" hide-details />
                  </v-col>
                </v-row>
              </div>
            </div>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="dialogConfig = false">Cancelar</v-btn>
          <v-btn color="primary" size="large" rounded="lg" :loading="salvandoConfig" @click="salvarConfig">
            Salvar Configurações
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

const tab = ref('membros')
const carregando = ref(false)
const salvando = ref(false)
const salvandoConfig = ref(false)
const dialogMembro = ref(false)
const dialogAjuste = ref(false)
const dialogConfig = ref(false)
const editandoMembro = ref<string | null>(null)
const buscaMembro = ref('')
const filtroStatus = ref<string | null>(null)

const membros = ref<any[]>([])
const clientes = ref<any[]>([])
const movimentosCashback = ref<any[]>([])
const historico = ref<any[]>([])
const membroAjuste = ref<any>(null)

const formMembro = ref({ clienteId: '', status: 'Ativo', dataAdesao: new Date().toISOString().slice(0, 10), observacao: '' })
const ajuste = ref({ tipo: 'Credito', valor: 0, motivo: '' })

const config = ref({
  percentualCashback: 5,
  validade: 180,
  minimoResgate: 10,
  limiteUsoPercent: 50,
  descontoMembro: 0,
  aniversarianteDuplo: true,
  ativo: true,
  nomeClubeExibicao: 'Clube de Promoções',
})

const cardsResumo = computed(() => [
  { label: 'Membros ativos',        valor: membros.value.filter(m => m.status === 'Ativo').length, icon: 'mdi-account-group', cor: 'purple' },
  { label: 'Cashback disponível',   valor: 'R$ ' + fmt(membros.value.reduce((s, m) => s + (m.saldoCashback ?? 0), 0)), icon: 'mdi-cash-refund', cor: 'success' },
  { label: 'Cashback distribuído',  valor: 'R$ ' + fmt(membros.value.reduce((s, m) => s + (m.totalCashback ?? 0), 0)), icon: 'mdi-gift-outline', cor: 'teal' },
  { label: 'Volume de compras',     valor: 'R$ ' + fmt(membros.value.reduce((s, m) => s + (m.totalCompras ?? 0), 0)), icon: 'mdi-cart-outline', cor: 'indigo' },
])

const headersMembros = [
  { title: 'Cliente', key: 'nome' },
  { title: 'Adesão', key: 'dataAdesao', width: 110 },
  { title: 'Total Compras', key: 'totalCompras', width: 130 },
  { title: 'Cashback Disp.', key: 'saldoCashback', width: 130 },
  { title: 'Status', key: 'status', width: 100 },
  { title: '', key: 'acoes', sortable: false, width: 80 },
]
const headersCashback = [
  { title: 'Cliente', key: 'clienteNome' },
  { title: 'Tipo', key: 'tipo', width: 100 },
  { title: 'Valor', key: 'valor', width: 120 },
  { title: 'Motivo', key: 'motivo' },
  { title: 'Data', key: 'data', width: 110 },
]
const headersHistorico = [
  { title: 'Cliente', key: 'clienteNome' },
  { title: 'Venda #', key: 'vendaNumero', width: 90 },
  { title: 'Desconto usado', key: 'desconto', width: 130 },
  { title: 'Cashback gerado', key: 'cashbackGerado', width: 140 },
  { title: 'Data', key: 'data', width: 110 },
]

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtData = (v: string) => v ? new Date(v).toLocaleDateString('pt-BR') : '-'

async function carregar() {
  carregando.value = true
  try {
    const [m, mv, h] = await Promise.all([
      api.get('/clube/membros', { params: { empresaId: auth.empresaId, q: buscaMembro.value, status: filtroStatus.value } }),
      api.get('/clube/cashback', { params: { empresaId: auth.empresaId } }),
      api.get('/clube/historico', { params: { empresaId: auth.empresaId } }),
    ])
    membros.value = m.data ?? []
    movimentosCashback.value = mv.data ?? []
    historico.value = h.data ?? []
  } catch { membros.value = [] }
  finally { carregando.value = false }
}

async function carregarClientes() {
  try {
    const r = await api.get('/clientes', { params: { empresaId: auth.empresaId } })
    clientes.value = r.data ?? []
  } catch {}
}

async function carregarConfig() {
  try {
    const r = await api.get('/clube/config', { params: { empresaId: auth.empresaId } })
    if (r.data) config.value = { ...config.value, ...r.data }
  } catch {}
}

function abrirNovoMembro() {
  editandoMembro.value = null
  formMembro.value = { clienteId: '', status: 'Ativo', dataAdesao: new Date().toISOString().slice(0, 10), observacao: '' }
  dialogMembro.value = true
}

function editarMembro(item: any) {
  editandoMembro.value = item.id
  formMembro.value = { clienteId: item.clienteId, status: item.status, dataAdesao: item.dataAdesao?.slice(0, 10) ?? '', observacao: item.observacao ?? '' }
  dialogMembro.value = true
}

async function salvarMembro() {
  if (!formMembro.value.clienteId) { notif.erro('Selecione um cliente.'); return }
  salvando.value = true
  try {
    if (editandoMembro.value) {
      await api.put(`/clube/membros/${editandoMembro.value}`, formMembro.value)
    } else {
      await api.post('/clube/membros', { ...formMembro.value, empresaId: auth.empresaId })
    }
    notif.ok('Membro salvo com sucesso!')
    dialogMembro.value = false
    await carregar()
  } catch { notif.erro('Erro ao salvar membro.') }
  finally { salvando.value = false }
}

function abrirAjuste(item: any) {
  membroAjuste.value = item
  ajuste.value = { tipo: 'Credito', valor: 0, motivo: '' }
  dialogAjuste.value = true
}

async function salvarAjuste() {
  if (!ajuste.value.valor || !ajuste.value.motivo) { notif.erro('Preencha valor e motivo.'); return }
  salvando.value = true
  try {
    await api.post(`/clube/membros/${membroAjuste.value.id}/ajuste-cashback`, {
      ...ajuste.value, empresaId: auth.empresaId
    })
    notif.ok('Cashback ajustado!')
    dialogAjuste.value = false
    await carregar()
  } catch { notif.erro('Erro ao ajustar cashback.') }
  finally { salvando.value = false }
}

async function salvarConfig() {
  salvandoConfig.value = true
  try {
    await api.put('/clube/config', { ...config.value, empresaId: auth.empresaId })
    notif.ok('Configurações salvas!')
    dialogConfig.value = false
  } catch { notif.erro('Erro ao salvar configurações.') }
  finally { salvandoConfig.value = false }
}

onMounted(() => { carregar(); carregarClientes(); carregarConfig() })
</script>

<style scoped>
.cad-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; }
.cad-body { background:#f5f6f8; padding:16px; display:flex; flex-direction:column; gap:12px; }
.cad-secao { background:white; border-radius:12px; border:1px solid #e8edf3; overflow:hidden; }
.cad-secao-header { display:flex; align-items:center; gap:6px; padding:10px 16px; background:#f8f9fb; border-bottom:1px solid #e8edf3; font-size:0.75rem; font-weight:700; text-transform:uppercase; letter-spacing:0.07em; color:rgb(var(--v-theme-primary)); }
.cad-secao-body { padding:16px; }
</style>
