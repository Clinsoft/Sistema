<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div>
        <div class="text-h6 font-weight-bold">Custos Fixos Mensais</div>
        <div class="text-body-2 text-medium-emphasis">Base para cálculo do ponto de equilíbrio</div>
      </div>
      <v-spacer />
      <v-btn prepend-icon="mdi-plus" color="primary" @click="abrirNovo">Novo Custo</v-btn>
    </div>

    <!-- Totais por categoria -->
    <v-row class="mb-4">
      <v-col v-for="cat in resumoCategorias" :key="cat.categoria" cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="text-caption text-medium-emphasis">{{ cat.categoria }}</div>
            <div class="text-body-1 font-weight-bold text-error">{{ fmt(cat.total) }}</div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="6" md="3">
        <v-card rounded="xl" elevation="1" color="error-lighten-5">
          <v-card-text class="pa-3">
            <div class="text-caption text-medium-emphasis">TOTAL MENSAL</div>
            <div class="text-body-1 font-weight-bold text-error">{{ fmt(totalGeral) }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <v-card-text v-if="carregando" class="d-flex justify-center pa-8">
        <v-progress-circular indeterminate color="primary" />
      </v-card-text>

      <v-table v-else density="comfortable">
        <thead>
          <tr>
            <th>Descrição</th>
            <th>Categoria</th>
            <th class="text-right">Valor Mensal</th>
            <th class="text-center">Ações</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!custos.length">
            <td colspan="4" class="text-center text-medium-emphasis pa-8">
              Nenhum custo fixo cadastrado. Adicione para calcular o ponto de equilíbrio.
            </td>
          </tr>
          <tr v-for="c in custos" :key="c.id">
            <td class="py-2">{{ c.descricao }}</td>
            <td>
              <v-chip size="x-small" :color="corCategoria(c.categoria)" label>
                {{ c.categoria ?? 'Geral' }}
              </v-chip>
            </td>
            <td class="text-right font-weight-bold text-error">{{ fmt(c.valor) }}</td>
            <td class="text-center">
              <v-btn icon="mdi-pencil-outline" size="small" variant="text" @click="editar(c)" />
              <v-btn icon="mdi-delete-outline" size="small" variant="text" color="error" @click="excluir(c.id)" />
            </td>
          </tr>
        </tbody>
      </v-table>
    </v-card>

    <!-- Dialog criar/editar -->
    <v-dialog v-model="dialog" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">{{ editandoId ? 'Editar' : 'Novo' }} Custo Fixo</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.descricao" label="Descrição *" variant="outlined" density="compact" class="mb-3" />
          <v-select
            v-model="form.categoria"
            :items="categorias"
            label="Categoria"
            variant="outlined"
            density="compact"
            clearable
            class="mb-3"
          />
          <v-text-field
            v-model.number="form.valor"
            label="Valor Mensal (R$) *"
            type="number"
            prefix="R$"
            variant="outlined"
            density="compact"
          />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvando" @click="salvar">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import api from '@/composables/useApi'

const auth = useAuthStore()
const notif = useNotifStore()

interface CustoFixo { id: string; descricao: string; valor: number; categoria?: string }

const custos = ref<CustoFixo[]>([])
const carregando = ref(true)
const dialog = ref(false)
const salvando = ref(false)
const editandoId = ref<string | null>(null)
const form = ref({ descricao: '', valor: 0, categoria: '' })

const categorias = ['Aluguel', 'Pessoal', 'Energia', 'Telefone/Internet', 'Contabilidade', 'Marketing', 'Seguro', 'Manutenção', 'Outros']

const fmt = (v: number) => 'R$ ' + (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })

const totalGeral = computed(() => custos.value.reduce((s, c) => s + c.valor, 0))

const resumoCategorias = computed(() => {
  const map = new Map<string, number>()
  custos.value.forEach(c => {
    const cat = c.categoria ?? 'Geral'
    map.set(cat, (map.get(cat) ?? 0) + c.valor)
  })
  return [...map.entries()].map(([categoria, total]) => ({ categoria, total }))
    .sort((a, b) => b.total - a.total)
    .slice(0, 3)
})

function corCategoria(cat?: string) {
  const cores: Record<string, string> = {
    'Aluguel': 'deep-purple', 'Pessoal': 'blue', 'Energia': 'amber',
    'Contabilidade': 'teal', 'Marketing': 'pink', 'Seguro': 'indigo'
  }
  return cores[cat ?? ''] ?? 'grey'
}

async function listar() {
  try {
    const r = await api.get<CustoFixo[]>('/custos-fixos', { params: { empresaId: auth.empresaId } })
    custos.value = r.data
  } catch { /* silencioso */ } finally { carregando.value = false }
}

function abrirNovo() {
  editandoId.value = null
  form.value = { descricao: '', valor: 0, categoria: '' }
  dialog.value = true
}

function editar(c: CustoFixo) {
  editandoId.value = c.id
  form.value = { descricao: c.descricao, valor: c.valor, categoria: c.categoria ?? '' }
  dialog.value = true
}

async function salvar() {
  if (!form.value.descricao || !form.value.valor) return
  salvando.value = true
  try {
    const body = { empresaId: auth.empresaId, ...form.value }
    if (editandoId.value) await api.put(`/custos-fixos/${editandoId.value}`, body)
    else await api.post('/custos-fixos', body)
    notif.ok('Custo fixo salvo.')
    dialog.value = false
    await listar()
  } catch { notif.erro('Erro ao salvar.') } finally { salvando.value = false }
}

async function excluir(id: string) {
  if (!confirm('Remover este custo fixo?')) return
  try {
    await api.delete(`/custos-fixos/${id}`)
    await listar()
  } catch { notif.erro('Erro ao remover.') }
}

onMounted(listar)
</script>
