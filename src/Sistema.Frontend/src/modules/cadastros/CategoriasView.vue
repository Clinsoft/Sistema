<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Categorias de Produtos</h2></v-col>
      <v-col cols="auto">
        <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNova">Nova Categoria</v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="categorias"
      titulo="Como usar as Categorias"
      :passos="[
        'Clique em <b>Nova Categoria</b> e informe o <b>nome</b>. Opcionalmente, escolha uma <b>categoria pai</b> para criar subcategorias.',
        'Use o ícone ✎ para <b>editar</b> o nome ou a hierarquia de uma categoria existente.',
        'Clique em <b>Ver produtos</b> (ícone 📦) para listar todos os produtos vinculados àquela categoria.',
        'Para <b>excluir</b> (🗑), a categoria não pode ter produtos nem subcategorias vinculadas — o sistema avisa se houver.',
      ]"
    />

    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="categorias" :loading="carregando"
        density="compact" hover items-per-page="25">
        <template #item.categoriaPaiNome="{ item }">
          {{ item.categoriaPaiNome ?? '—' }}
        </template>
        <template #item.qtdProdutos="{ item }">
          <v-chip size="x-small" :color="item.qtdProdutos > 0 ? 'primary' : 'grey'" variant="tonal">
            {{ item.qtdProdutos ?? 0 }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-package-variant-closed" size="x-small" variant="text" color="primary"
            title="Ver produtos" @click="verProdutos(item)" />
          <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" color="secondary"
            title="Editar" @click="abrirEditar(item)" />
          <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error"
            title="Excluir" @click="excluir(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog nova/editar categoria -->
    <v-dialog v-model="dialog" max-width="420" persistent>
      <v-card rounded="xl" style="display:flex;flex-direction:column;max-height:90vh">
        <div class="cad-header">
          <div class="d-flex align-center gap-3" style="gap:12px">
            <v-avatar color="primary" size="40">
              <v-icon>mdi-tag-multiple-outline</v-icon>
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">{{ editandoId ? 'Editar Categoria' : 'Nova Categoria' }}</div>
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
                  autofocus :rules="[r => !!r || 'Obrigatório']" class="mb-2"
                  @keyup.enter="salvar" />
                <v-select v-model="form.categoriaPaiId" label="Categoria pai (opcional)"
                  :items="categoriasPaiDisponiveis" item-title="nome" item-value="id"
                  variant="outlined" density="compact" clearable
                  hint="Deixe vazio para categoria principal" persistent-hint />
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

    <!-- Drawer ver produtos -->
    <v-navigation-drawer v-model="drawerProdutos" location="right" width="420" temporary>
      <v-toolbar flat>
        <v-toolbar-title class="text-body-1 font-weight-bold">
          Produtos — {{ categoriaSel?.nome }}
        </v-toolbar-title>
        <v-btn icon="mdi-close" @click="drawerProdutos = false" />
      </v-toolbar>
      <v-divider />
      <div class="pa-2">
        <div v-if="carregandoProdutos" class="d-flex justify-center pa-8">
          <v-progress-circular indeterminate color="primary" />
        </div>
        <div v-else-if="!produtosCategoria.length" class="text-center text-medium-emphasis pa-8">
          <v-icon size="40" class="mb-2">mdi-package-variant-remove</v-icon>
          <div class="text-body-2">Nenhum produto nesta categoria.</div>
        </div>
        <v-list v-else density="compact">
          <v-list-item v-for="p in produtosCategoria" :key="p.id" rounded="lg" class="mb-1">
            <template #prepend>
              <v-avatar size="32" color="primary" variant="tonal">
                <span class="text-caption font-weight-bold">{{ p.codigo }}</span>
              </v-avatar>
            </template>
            <v-list-item-title class="text-body-2">{{ p.descricao }}</v-list-item-title>
            <v-list-item-subtitle class="text-caption">
              {{ p.unidadeSigla }} · Estoque: {{ p.estoqueAtual }}
            </v-list-item-subtitle>
            <template #append>
              <span class="text-body-2 font-weight-bold text-success">R$ {{ fmt(p.precoVenda) }}</span>
            </template>
          </v-list-item>
        </v-list>
      </div>
    </v-navigation-drawer>
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
const categorias = ref<any[]>([])
const editandoId = ref<string | null>(null)
const form = ref({ nome: '', categoriaPaiId: null as string | null })

const drawerProdutos = ref(false)
const categoriaSel = ref<any>(null)
const produtosCategoria = ref<any[]>([])
const carregandoProdutos = ref(false)

const headers = [
  { title: 'Nome', key: 'nome', sortable: true },
  { title: 'Categoria pai', key: 'categoriaPaiNome', sortable: true },
  { title: 'Produtos', key: 'qtdProdutos', width: 100 },
  { title: 'Ações', key: 'actions', sortable: false, width: 130 },
]

// Ao editar, não permite escolher a própria categoria como pai
const categoriasPaiDisponiveis = computed(() =>
  categorias.value.filter(c => c.id !== editandoId.value)
)

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })

async function listar() {
  carregando.value = true
  try {
    const r = await api.get('/categorias', { params: { empresaId: auth.empresaId } })
    categorias.value = r.data
  } finally { carregando.value = false }
}

function abrirNova() {
  editandoId.value = null
  form.value = { nome: '', categoriaPaiId: null }
  dialog.value = true
}

function abrirEditar(item: any) {
  editandoId.value = item.id
  form.value = { nome: item.nome, categoriaPaiId: item.categoriaPaiId ?? null }
  dialog.value = true
}

async function salvar() {
  if (!form.value.nome) return
  salvando.value = true
  try {
    const payload = { empresaId: auth.empresaId, nome: form.value.nome, categoriaPaiId: form.value.categoriaPaiId || null }
    if (editandoId.value) {
      await api.put(`/categorias/${editandoId.value}`, payload)
      notif.ok('Categoria atualizada!')
    } else {
      await api.post('/categorias', payload)
      notif.ok('Categoria criada!')
    }
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
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao excluir.')
  }
}

async function verProdutos(item: any) {
  categoriaSel.value = item
  drawerProdutos.value = true
  produtosCategoria.value = []
  carregandoProdutos.value = true
  try {
    const r = await api.get('/produtos', {
      params: { empresaId: auth.empresaId, categoriaId: item.id, tamanhoPagina: 500 },
    })
    produtosCategoria.value = r.data?.itens ?? r.data ?? []
  } catch { produtosCategoria.value = [] }
  finally { carregandoProdutos.value = false }
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
