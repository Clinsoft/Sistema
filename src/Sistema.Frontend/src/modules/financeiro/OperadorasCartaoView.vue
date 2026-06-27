<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div>
        <div class="text-h6 font-weight-bold">Operadoras de Cartão</div>
        <div class="text-caption text-medium-emphasis">Cadastre bandeiras, taxas e prazos de repasse</div>
      </div>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNova">Nova Operadora</v-btn>
    </div>

    <v-row>
      <v-col v-for="op in operadoras" :key="op.id" cols="12" md="6" lg="4">
        <v-card rounded="xl" elevation="1" hover>
          <v-card-text class="pa-4">
            <div class="d-flex align-center mb-3">
              <v-avatar :color="op.cor ?? 'primary'" size="44" rounded="lg" class="mr-3">
                <v-icon :icon="op.icone ?? 'mdi-credit-card-outline'" color="white" size="24" />
              </v-avatar>
              <div class="flex-grow-1">
                <div class="text-body-1 font-weight-bold">{{ op.nome }}</div>
                <div class="d-flex flex-wrap gap-1 mt-1">
                  <v-chip v-for="b in (op.bandeiras ?? [])" :key="b" size="x-small" variant="tonal" color="primary">{{ b }}</v-chip>
                </div>
              </div>
              <div class="d-flex gap-1">
                <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="editar(op)" />
                <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="excluir(op.id)" />
              </div>
            </div>

            <div class="op-taxas">
              <div class="op-taxa-linha" v-if="op.taxaDebito">
                <div class="d-flex align-center gap-1">
                  <v-icon icon="mdi-credit-card-minus-outline" size="14" color="teal" />
                  <span>Débito</span>
                </div>
                <div class="d-flex align-center gap-2">
                  <span class="font-weight-bold">{{ op.taxaDebito }}%</span>
                  <v-chip size="x-small" color="teal" variant="tonal">D+{{ op.prazoDebito ?? 1 }}</v-chip>
                </div>
              </div>
              <div class="op-taxa-linha" v-if="op.taxaCreditoVista">
                <div class="d-flex align-center gap-1">
                  <v-icon icon="mdi-credit-card-outline" size="14" color="indigo" />
                  <span>Crédito à vista</span>
                </div>
                <div class="d-flex align-center gap-2">
                  <span class="font-weight-bold">{{ op.taxaCreditoVista }}%</span>
                  <v-chip size="x-small" color="indigo" variant="tonal">D+{{ op.prazoCreditoVista ?? 30 }}</v-chip>
                </div>
              </div>
              <div class="op-taxa-linha" v-if="op.taxaCreditoParcelado">
                <div class="d-flex align-center gap-1">
                  <v-icon icon="mdi-credit-card-multiple-outline" size="14" color="deep-purple" />
                  <span>Crédito parcelado</span>
                </div>
                <div class="d-flex align-center gap-2">
                  <span class="font-weight-bold">{{ op.taxaCreditoParcelado }}%</span>
                  <v-chip size="x-small" color="deep-purple" variant="tonal">D+{{ op.prazoCreditoParcelado ?? 30 }}/parcela</v-chip>
                </div>
              </div>
              <div class="op-taxa-linha" v-if="op.taxaPix">
                <div class="d-flex align-center gap-1">
                  <v-icon icon="mdi-qrcode" size="14" color="green" />
                  <span>Pix</span>
                </div>
                <div class="d-flex align-center gap-2">
                  <span class="font-weight-bold">{{ op.taxaPix }}%</span>
                  <v-chip size="x-small" color="green" variant="tonal">D+{{ op.prazoPix ?? 0 }}</v-chip>
                </div>
              </div>
              <v-divider class="my-2" v-if="op.taxaAntecipacao" />
              <div class="op-taxa-linha" v-if="op.taxaAntecipacao">
                <div class="d-flex align-center gap-1">
                  <v-icon icon="mdi-calendar-fast-forward-outline" size="14" color="warning" />
                  <span>Taxa de antecipação</span>
                </div>
                <span class="font-weight-bold text-warning">{{ op.taxaAntecipacao }}% a.m.</span>
              </div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>

      <v-col v-if="!operadoras.length && !carregando" cols="12">
        <v-card rounded="xl" elevation="1" class="pa-8 text-center">
          <v-icon icon="mdi-credit-card-off-outline" size="48" class="mb-3 text-medium-emphasis" />
          <div class="text-body-1 text-medium-emphasis">Nenhuma operadora cadastrada</div>
          <v-btn class="mt-3" color="primary" prepend-icon="mdi-plus" @click="abrirNova">
            Cadastrar operadora
          </v-btn>
        </v-card>
      </v-col>
    </v-row>

    <!-- Dialog Nova/Editar Operadora -->
    <v-dialog v-model="dialog" max-width="640" persistent>
      <v-card rounded="xl" style="display:flex;flex-direction:column;max-height:90vh">
        <div class="cad-header">
          <div class="d-flex align-center gap-3">
            <v-avatar :color="form.cor ?? 'primary'" size="40" rounded="lg">
              <v-icon :icon="form.icone ?? 'mdi-credit-card-outline'" color="white" />
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">{{ editando ? 'Editar Operadora' : 'Nova Operadora' }}</div>
              <div class="text-caption text-medium-emphasis">{{ form.nome || 'Dados da operadora e taxas' }}</div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialog = false" />
        </div>

        <v-card-text class="pa-0" style="overflow-y:auto">
          <div class="cad-body">

            <!-- Identificação -->
            <div class="cad-secao">
              <div class="cad-secao-header"><v-icon size="14">mdi-domain</v-icon> Identificação</div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="12" md="6">
                    <v-text-field v-model="form.nome" label="Nome da operadora *"
                      variant="outlined" density="compact" :rules="[r => !!r || 'Obrigatório']"
                      placeholder="Ex: Cielo, Rede, Stone, PagSeguro" />
                  </v-col>
                  <v-col cols="12" md="6">
                    <v-combobox v-model="form.bandeiras" :items="bandeirasSugestao"
                      label="Bandeiras aceitas" variant="outlined" density="compact"
                      multiple chips closable-chips
                      hint="Digite e pressione Enter para adicionar" persistent-hint />
                  </v-col>
                  <v-col cols="6" md="3">
                    <v-select v-model="form.icone" :items="icones" item-title="label" item-value="value"
                      label="Ícone" variant="outlined" density="compact">
                      <template #selection="{ item }">
                        <v-icon :icon="item.value" class="mr-1" />
                      </template>
                      <template #item="{ item, props }">
                        <v-list-item v-bind="props">
                          <template #prepend><v-icon :icon="item.value" /></template>
                        </v-list-item>
                      </template>
                    </v-select>
                  </v-col>
                  <v-col cols="6" md="3">
                    <v-select v-model="form.cor" :items="cores" item-title="label" item-value="value"
                      label="Cor" variant="outlined" density="compact">
                      <template #selection="{ item }">
                        <v-avatar :color="item.value" size="18" class="mr-2" />{{ item.label }}
                      </template>
                      <template #item="{ item, props }">
                        <v-list-item v-bind="props">
                          <template #prepend><v-avatar :color="item.value" size="18" /></template>
                        </v-list-item>
                      </template>
                    </v-select>
                  </v-col>
                </v-row>
              </div>
            </div>

            <!-- Taxas e Prazos -->
            <div class="cad-secao">
              <div class="cad-secao-header"><v-icon size="14">mdi-percent</v-icon> Taxas e Prazos de Repasse</div>
              <div class="cad-secao-body">
                <v-row dense>
                  <!-- Débito -->
                  <v-col cols="12">
                    <div class="text-caption font-weight-bold text-teal mb-2" style="text-transform:uppercase;letter-spacing:.05em">
                      <v-icon icon="mdi-credit-card-minus-outline" size="14" color="teal" class="mr-1" />Débito
                    </div>
                  </v-col>
                  <v-col cols="6" md="3">
                    <v-text-field v-model.number="form.taxaDebito" label="Taxa (%)"
                      type="number" step="0.01" variant="outlined" density="compact" suffix="%" />
                  </v-col>
                  <v-col cols="6" md="3">
                    <v-text-field v-model.number="form.prazoDebito" label="Prazo (dias)"
                      type="number" variant="outlined" density="compact"
                      prefix="D+" hint="Ex: 1 = próximo dia útil" persistent-hint />
                  </v-col>

                  <!-- Crédito à vista -->
                  <v-col cols="12" class="mt-2">
                    <div class="text-caption font-weight-bold text-indigo mb-2" style="text-transform:uppercase;letter-spacing:.05em">
                      <v-icon icon="mdi-credit-card-outline" size="14" color="indigo" class="mr-1" />Crédito à Vista
                    </div>
                  </v-col>
                  <v-col cols="6" md="3">
                    <v-text-field v-model.number="form.taxaCreditoVista" label="Taxa (%)"
                      type="number" step="0.01" variant="outlined" density="compact" suffix="%" />
                  </v-col>
                  <v-col cols="6" md="3">
                    <v-text-field v-model.number="form.prazoCreditoVista" label="Prazo (dias)"
                      type="number" variant="outlined" density="compact" prefix="D+" />
                  </v-col>

                  <!-- Crédito parcelado -->
                  <v-col cols="12" class="mt-2">
                    <div class="text-caption font-weight-bold text-deep-purple mb-2" style="text-transform:uppercase;letter-spacing:.05em">
                      <v-icon icon="mdi-credit-card-multiple-outline" size="14" color="deep-purple" class="mr-1" />Crédito Parcelado (2x+)
                    </div>
                  </v-col>
                  <v-col cols="6" md="3">
                    <v-text-field v-model.number="form.taxaCreditoParcelado" label="Taxa (%)"
                      type="number" step="0.01" variant="outlined" density="compact" suffix="%" />
                  </v-col>
                  <v-col cols="6" md="3">
                    <v-text-field v-model.number="form.prazoCreditoParcelado" label="Prazo por parcela"
                      type="number" variant="outlined" density="compact" prefix="D+"
                      hint="Ex: 30 = cada parcela cai 30 dias após a compra" persistent-hint />
                  </v-col>

                  <!-- Pix -->
                  <v-col cols="12" class="mt-2">
                    <div class="text-caption font-weight-bold text-green mb-2" style="text-transform:uppercase;letter-spacing:.05em">
                      <v-icon icon="mdi-qrcode" size="14" color="green" class="mr-1" />Pix
                    </div>
                  </v-col>
                  <v-col cols="6" md="3">
                    <v-text-field v-model.number="form.taxaPix" label="Taxa (%)"
                      type="number" step="0.01" variant="outlined" density="compact" suffix="%" />
                  </v-col>
                  <v-col cols="6" md="3">
                    <v-text-field v-model.number="form.prazoPix" label="Prazo (dias)"
                      type="number" variant="outlined" density="compact" prefix="D+"
                      hint="Normalmente D+0 ou D+1" persistent-hint />
                  </v-col>
                </v-row>
              </div>
            </div>

            <!-- Antecipação -->
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-calendar-fast-forward-outline</v-icon> Antecipação de Recebíveis
              </div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="12" md="4">
                    <v-text-field v-model.number="form.taxaAntecipacao" label="Taxa de antecipação (% a.m.)"
                      type="number" step="0.01" variant="outlined" density="compact" suffix="% a.m."
                      hint="Taxa mensal cobrada pela antecipação" persistent-hint />
                  </v-col>
                  <v-col cols="12" md="8">
                    <v-textarea v-model="form.observacao" label="Observações / Contrato"
                      variant="outlined" density="compact" rows="2"
                      hint="Número de contrato, contato comercial, etc." persistent-hint />
                  </v-col>
                </v-row>
              </div>
            </div>
          </div>
        </v-card-text>

        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancelar</v-btn>
          <v-btn color="primary" size="large" rounded="lg" :loading="salvando" @click="salvar">Salvar</v-btn>
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
const editando = ref<string | null>(null)
const operadoras = ref<any[]>([])

const bandeirasSugestao = ['Visa', 'Mastercard', 'Elo', 'American Express', 'Hipercard', 'Cabal', 'Banescard']

const icones = [
  { value: 'mdi-credit-card-outline', label: 'Cartão' },
  { value: 'mdi-bank-outline', label: 'Banco' },
  { value: 'mdi-qrcode', label: 'Pix/QR' },
  { value: 'mdi-cellphone-check', label: 'Celular' },
]

const cores = [
  { value: 'primary',     label: 'Verde (padrão)' },
  { value: 'blue',        label: 'Azul' },
  { value: 'indigo',      label: 'Índigo' },
  { value: 'deep-purple', label: 'Roxo' },
  { value: 'teal',        label: 'Teal' },
  { value: 'orange',      label: 'Laranja' },
  { value: 'red',         label: 'Vermelho' },
  { value: 'grey-darken-1', label: 'Cinza' },
]

const formPadrao = () => ({
  nome: '', bandeiras: [] as string[],
  icone: 'mdi-credit-card-outline', cor: 'primary',
  taxaDebito: null as number | null,       prazoDebito: 1,
  taxaCreditoVista: null as number | null, prazoCreditoVista: 30,
  taxaCreditoParcelado: null as number | null, prazoCreditoParcelado: 30,
  taxaPix: null as number | null,          prazoPix: 0,
  taxaAntecipacao: null as number | null,
  observacao: '',
})
const form = ref(formPadrao())

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/financeiro/operadoras-cartao', { params: { empresaId: auth.empresaId } })
    operadoras.value = r.data ?? []
  } catch { operadoras.value = [] }
  finally { carregando.value = false }
}

function abrirNova() {
  editando.value = null
  form.value = formPadrao()
  dialog.value = true
}

function editar(item: any) {
  editando.value = item.id
  form.value = { ...formPadrao(), ...item }
  dialog.value = true
}

async function salvar() {
  if (!form.value.nome) { notif.erro('Nome é obrigatório.'); return }
  salvando.value = true
  try {
    if (editando.value) {
      await api.put(`/financeiro/operadoras-cartao/${editando.value}`, form.value)
    } else {
      await api.post('/financeiro/operadoras-cartao', { ...form.value, empresaId: auth.empresaId })
    }
    notif.ok('Operadora salva com sucesso!')
    dialog.value = false
    await carregar()
  } catch { notif.erro('Erro ao salvar operadora.') }
  finally { salvando.value = false }
}

async function excluir(id: string) {
  try {
    await api.delete(`/financeiro/operadoras-cartao/${id}`)
    await carregar()
    notif.ok('Operadora removida.')
  } catch { notif.erro('Erro ao remover operadora.') }
}

onMounted(carregar)
</script>

<style scoped>
.cad-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; }
.cad-body { background:#f5f6f8; padding:16px; display:flex; flex-direction:column; gap:12px; }
.cad-secao { background:white; border-radius:12px; border:1px solid #e8edf3; overflow:hidden; }
.cad-secao-header { display:flex; align-items:center; gap:6px; padding:10px 16px; background:#f8f9fb; border-bottom:1px solid #e8edf3; font-size:0.75rem; font-weight:700; text-transform:uppercase; letter-spacing:0.07em; color:rgb(var(--v-theme-primary)); }
.cad-secao-body { padding:16px; }
.op-taxas { background:#f8f9fb; border-radius:8px; padding:12px; display:flex; flex-direction:column; gap:6px; }
.op-taxa-linha { display:flex; justify-content:space-between; align-items:center; font-size:0.82rem; }
</style>
