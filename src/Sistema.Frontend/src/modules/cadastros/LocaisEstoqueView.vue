<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Locais de Estoque</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNovo">Novo Local</v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="locais-estoque"
      titulo="Como usar os Locais de Estoque"
      :passos="[
        'Clique em <b>Novo Local</b>, escolha a <b>filial (CNPJ)</b> à qual o local pertence e dê um nome (ex.: Depósito, Loja, Câmara Fria).',
        'Você pode cadastrar <b>vários locais para a mesma filial/CNPJ</b> — cada um controla seu próprio estoque.',
        'Marque <b>Principal</b> no local padrão de cada filial (usado por padrão em entradas e vendas). Só um principal por filial.',
        'Use ✎ para <b>editar</b> ou <b>reassociar</b> o local a outra filial (ex.: quando o cliente abre uma nova filial). Use 🗑 para excluir (o principal não pode ser excluído).',
      ]"
    />

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="locais" :loading="carregando"
        density="compact" hover items-per-page="25">
        <template #item.filial="{ item }">
          <span>{{ nomeFilial(item.empresaId) }}</span>
        </template>
        <template #item.principal="{ item }">
          <v-chip v-if="item.principal" color="primary" size="x-small" variant="tonal">
            <v-icon start size="12">mdi-star</v-icon>Principal
          </v-chip>
          <span v-else class="text-medium-emphasis text-caption">—</span>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" color="secondary"
            title="Editar / reassociar filial" @click="abrirEditar(item)" />
          <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error"
            title="Excluir" @click="excluir(item)" />
        </template>
      </v-data-table>
    </v-card>

    <v-dialog v-model="dialog" max-width="520" persistent>
      <v-card rounded="xl" style="display:flex;flex-direction:column;max-height:90vh">
        <div class="cad-header">
          <div class="d-flex align-center" style="gap:12px">
            <v-avatar color="primary" size="40"><v-icon>mdi-warehouse</v-icon></v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">{{ editandoId ? 'Editar Local de Estoque' : 'Novo Local de Estoque' }}</div>
              <div class="text-caption text-medium-emphasis">Cadastro de local de armazenamento</div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialog = false" />
        </div>
        <v-card-text class="pa-0" style="overflow-y:auto">
          <div class="cad-body">
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-warehouse</v-icon>
                Dados do Local
              </div>
              <div class="cad-secao-body">
                <v-select v-model="form.empresaId" label="Filial (CNPJ) *"
                  :items="filiaisItems" item-title="titulo" item-value="id"
                  variant="outlined" density="compact" class="mb-3"
                  :rules="[r => !!r || 'Obrigatório']"
                  :hint="editandoId ? 'Trocar aqui reassocia o local a outra filial' : 'Filial à qual este local pertence'"
                  persistent-hint />
                <v-text-field v-model="form.nome" label="Nome *" variant="outlined" density="compact"
                  autofocus placeholder="Ex.: Depósito Principal" :rules="[r => !!r || 'Obrigatório']" class="mb-3" />
                <v-text-field v-model="form.descricao" label="Descrição" variant="outlined" density="compact" class="mb-3" />
                <v-switch v-model="form.principal" label="Local principal desta filial" color="primary"
                  density="compact" hide-details
                  hint="Usado por padrão em entradas de NF-e e no PDV" />
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
import GuiaPassos from '@/components/GuiaPassos.vue'
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const carregando = ref(false)
const salvando = ref(false)
const dialog = ref(false)
const locais = ref<any[]>([])
const editandoId = ref<string | null>(null)
const form = ref<{ empresaId: string; nome: string; descricao: string; principal: boolean }>({
  empresaId: '', nome: '', descricao: '', principal: false,
})

// Filiais do grupo (cada filial é uma empresa com CNPJ próprio)
const filiaisItems = computed(() => {
  const lista = auth.filiais.length ? auth.filiais : (auth.empresaAtual ? [auth.empresaAtual] : [])
  return lista.map(f => ({ id: f.id, titulo: `${f.nomeFantasia} — ${f.cnpj}` }))
})
function nomeFilial(id: string) {
  const f = auth.filiais.find(x => x.id === id)
  return f ? f.nomeFantasia : (id === auth.empresaId ? (auth.empresaAtual?.nomeFantasia ?? 'Filial atual') : '—')
}

const headers = [
  { title: 'Nome', key: 'nome', sortable: true },
  { title: 'Filial', key: 'filial', sortable: false },
  { title: 'Descrição', key: 'descricao' },
  { title: 'Principal', key: 'principal', width: 110 },
  { title: 'Ações', key: 'actions', sortable: false, width: 100 },
]

async function listar() {
  carregando.value = true
  try {
    const ids = auth.filiais.length ? auth.filiais.map(f => f.id) : [auth.empresaId]
    const r = await api.get('/locais-estoque', { params: { empresaId: auth.empresaId, empresaIds: ids } })
    locais.value = r.data
  } finally { carregando.value = false }
}

function abrirNovo() {
  editandoId.value = null
  form.value = { empresaId: auth.empresaId, nome: '', descricao: '', principal: false }
  dialog.value = true
}

function abrirEditar(item: any) {
  editandoId.value = item.id
  form.value = {
    empresaId: item.empresaId ?? auth.empresaId,
    nome: item.nome, descricao: item.descricao ?? '', principal: !!item.principal,
  }
  dialog.value = true
}

async function salvar() {
  if (!form.value.nome || !form.value.empresaId) { notif.erro('Informe a filial e o nome.'); return }
  salvando.value = true
  try {
    const payload = {
      empresaId: form.value.empresaId,
      nome: form.value.nome,
      descricao: form.value.descricao || null,
      principal: form.value.principal,
    }
    if (editandoId.value) {
      await api.put(`/locais-estoque/${editandoId.value}`, payload)
      notif.ok('Local atualizado!')
    } else {
      await api.post('/locais-estoque', payload)
      notif.ok('Local criado!')
    }
    dialog.value = false
    await listar()
  } catch (e: any) { notif.erro(e?.response?.data ?? 'Erro ao salvar.') }
  finally { salvando.value = false }
}

async function excluir(item: any) {
  if (!confirm(`Excluir local "${item.nome}"?`)) return
  try {
    await api.delete(`/locais-estoque/${item.id}`)
    notif.ok('Local excluído.')
    await listar()
  } catch (e: any) { notif.erro(e?.response?.data ?? 'Erro ao excluir.') }
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
