<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Categorias de Produtos</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNova">Nova Categoria</v-btn>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="categorias" :loading="carregando"
        density="compact" hover items-per-page="25">
        <template #item.actions="{ item }">
          <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error"
            @click="excluir(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog nova categoria -->
    <v-dialog v-model="dialog" max-width="420" persistent>
      <v-card rounded="xl" style="display:flex;flex-direction:column;max-height:90vh">
        <div class="cad-header">
          <div class="d-flex align-center gap-3" style="gap:12px">
            <v-avatar color="primary" size="40">
              <v-icon>mdi-tag-multiple-outline</v-icon>
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">Nova Categoria</div>
              <div class="text-caption text-medium-emphasis">Cadastro de categoria de produto</div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialog = false" />
        </div>
        <v-card-text class="pa-0" style="overflow-y:auto">
          <div class="cad-body">
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-tag-multiple-outline</v-icon>
                Identificação
              </div>
              <div class="cad-secao-body">
                <v-text-field v-model="form.nome" label="Nome *" variant="outlined" density="compact"
                  autofocus :rules="[r => !!r || 'Obrigatório']" ref="campoNome" />
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
const categorias = ref<any[]>([])
const form = ref({ nome: '' })
const headers = [
  { title: 'Nome', key: 'nome', sortable: true },
  { title: 'Ações', key: 'actions', sortable: false, width: 80 },
]

async function listar() {
  carregando.value = true
  try {
    const r = await api.get('/categorias', { params: { empresaId: auth.empresaId } })
    categorias.value = r.data
  } finally { carregando.value = false }
}

function abrirNova() {
  form.value = { nome: '' }
  dialog.value = true
}

async function salvar() {
  if (!form.value.nome) return
  salvando.value = true
  try {
    await api.post('/categorias', { empresaId: auth.empresaId, nome: form.value.nome })
    notif.ok('Categoria criada!')
    dialog.value = false
    await listar()
  } catch { notif.erro('Erro ao salvar.') }
  finally { salvando.value = false }
}

async function excluir(item: any) {
  if (!confirm(`Excluir categoria "${item.nome}"?`)) return
  try {
    await api.delete(`/categorias/${item.id}`)
    notif.ok('Categoria excluída.')
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
