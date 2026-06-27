<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Unidades de Medida</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNova">Nova Unidade</v-btn>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="unidades" :loading="carregando"
        density="compact" hover items-per-page="25">
        <template #item.pesavel="{ item }">
          <v-chip v-if="item.pesavel" color="blue" size="x-small" variant="tonal"
            prepend-icon="mdi-scale">
            Pesável (3 decimais)
          </v-chip>
          <v-chip v-else color="grey" size="x-small" variant="tonal"
            prepend-icon="mdi-numeric">
            Inteiro
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" color="primary"
            @click="abrirEditar(item)" />
          <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error"
            @click="excluir(item)" />
        </template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="dialog" max-width="480" persistent>
      <v-card rounded="xl" style="display:flex;flex-direction:column;max-height:90vh">
        <div class="cad-header">
          <div class="d-flex align-center" style="gap:12px">
            <v-avatar color="primary" size="40">
              <v-icon>mdi-ruler-square</v-icon>
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">
                {{ editandoId ? 'Editar' : 'Nova' }} Unidade de Medida
              </div>
              <div class="text-caption text-medium-emphasis">Cadastro de unidade de medida</div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialog = false" />
        </div>
        <v-card-text class="pa-0" style="overflow-y:auto">
          <div class="cad-body">
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-ruler-square</v-icon>
                Identificação
              </div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="4">
                    <v-text-field v-model="form.sigla" label="Sigla *" variant="outlined" density="compact"
                      autofocus placeholder="UN" :rules="[r => !!r || 'Obrigatório']"
                      style="text-transform:uppercase" @input="form.sigla = form.sigla.toUpperCase()" />
                  </v-col>
                  <v-col cols="8">
                    <v-text-field v-model="form.descricao" label="Descrição *" variant="outlined" density="compact"
                      placeholder="Unidade" :rules="[r => !!r || 'Obrigatório']" />
                  </v-col>
                  <v-col cols="12">
                    <v-switch v-model="form.pesavel" color="blue" hide-details density="compact"
                      class="mt-1">
                      <template #label>
                        <div>
                          <span class="font-weight-medium">Unidade pesável</span>
                          <div class="text-caption text-medium-emphasis">
                            Permite 3 casas decimais na quantidade (ex: KG, LT)
                          </div>
                        </div>
                      </template>
                    </v-switch>
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
const editandoId = ref<string | null>(null)
const unidades = ref<any[]>([])
const form = ref({ sigla: '', descricao: '', pesavel: false })

const headers = [
  { title: 'Sigla',     key: 'sigla',     width: 90 },
  { title: 'Descrição', key: 'descricao', sortable: true },
  { title: 'Contagem',  key: 'pesavel',   width: 180 },
  { title: 'Ações',     key: 'actions',   sortable: false, width: 90 },
]

async function listar() {
  carregando.value = true
  try {
    const r = await api.get('/unidades-medida', { params: { empresaId: auth.empresaId } })
    unidades.value = r.data
  } finally { carregando.value = false }
}

function abrirNova() {
  editandoId.value = null
  form.value = { sigla: '', descricao: '', pesavel: false }
  dialog.value = true
}

function abrirEditar(item: any) {
  editandoId.value = item.id
  form.value = { sigla: item.sigla, descricao: item.descricao, pesavel: item.pesavel }
  dialog.value = true
}

async function salvar() {
  if (!form.value.sigla || !form.value.descricao) return
  salvando.value = true
  try {
    if (editandoId.value) {
      await api.put(`/unidades-medida/${editandoId.value}`, {
        empresaId: auth.empresaId,
        sigla: form.value.sigla,
        descricao: form.value.descricao,
        pesavel: form.value.pesavel,
      })
      notif.ok('Unidade atualizada!')
    } else {
      await api.post('/unidades-medida', {
        empresaId: auth.empresaId,
        sigla: form.value.sigla,
        descricao: form.value.descricao,
        pesavel: form.value.pesavel,
      })
      notif.ok('Unidade criada!')
    }
    dialog.value = false
    await listar()
  } catch { notif.erro('Erro ao salvar.') }
  finally { salvando.value = false }
}

async function excluir(item: any) {
  if (!confirm(`Excluir unidade "${item.sigla} — ${item.descricao}"?`)) return
  try {
    await api.delete(`/unidades-medida/${item.id}`)
    notif.ok('Unidade excluída.')
    await listar()
  } catch { notif.erro('Erro ao excluir.') }
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
