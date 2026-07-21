<template>
  <v-container fluid>
    <v-row align="center" class="mb-4">
      <v-col>
        <h2 class="text-h5 font-weight-bold">Marketing — Artes para Redes Sociais</h2>
        <div class="text-caption text-medium-emphasis">Crie peças visuais para Instagram, Facebook e WhatsApp</div>
      </v-col>
      <v-col cols="auto" class="d-flex gap-2">
        <v-btn color="deep-purple" prepend-icon="mdi-robot-happy-outline" @click="abrirIA">
          Gerar com IA
        </v-btn>
        <v-btn color="primary" prepend-icon="mdi-image-plus" @click="novaArte">Nova Arte</v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="marketing-artes"
      titulo="Como criar Artes para Redes Sociais"
      :passos="[
        'Clique em <b>Gerar com IA</b> para criar a arte automaticamente com o <b>gpt-image-1</b> (OpenAI): escolha o formato (Feed/Story/Banner), descreva a arte no <b>prompt</b> (produto, oferta, estilo, cores) e clique em <b>Gerar</b>.',
        'A imagem gerada é salva na <b>Galeria</b>. Use <b>Nova Arte</b> ou um <b>Template</b> para montar manualmente com texto, preço e fundo.',
        'Na galeria, cada arte tem <b>Editar</b> (✏️), <b>Baixar PNG</b> (⬇), <b>Compartilhar no WhatsApp</b> e <b>Excluir</b> (🗑️).',
        'Na aba <b>Agendamentos</b>, escolha uma arte, a plataforma (Instagram/Facebook/WhatsApp) e a data/hora para agendar a publicação.',
      ]"
    />

    <v-tabs v-model="tab" class="mb-4">
      <v-tab value="galeria">Galeria</v-tab>
      <v-tab value="templates">Templates</v-tab>
      <v-tab value="agendamentos">Agendamentos</v-tab>
    </v-tabs>

    <v-window v-model="tab">
      <!-- Galeria de artes criadas -->
      <v-window-item value="galeria">
        <v-row class="mb-3">
          <v-col cols="12" md="4">
            <v-select v-model="filtroTipo" :items="tiposArte" label="Tipo" density="compact"
              hide-details clearable @update:modelValue="listarArtes" />
          </v-col>
        </v-row>
        <v-row>
          <v-col v-for="arte in artes" :key="arte.id" cols="12" sm="6" md="4" lg="3">
            <v-card>
              <v-img :src="arteThumb(arte)" :aspect-ratio="arte.formato === 'StoryVertical' ? 9/16 : 1"
                cover>
                <v-chip class="ma-2" size="small" :color="corFormato(arte.formato)">{{ labelFormato(arte.formato) }}</v-chip>
              </v-img>
              <v-card-text class="pb-1">
                <div class="font-weight-medium text-truncate">{{ arte.titulo }}</div>
                <div class="text-caption text-medium-emphasis">{{ arte.tipo }} • {{ formatarData(arte.criadoEm) }}</div>
              </v-card-text>
              <v-card-actions class="pt-0">
                <v-btn size="small" variant="text" prepend-icon="mdi-pencil" @click="editarArte(arte)">Editar</v-btn>
                <v-spacer />
                <v-btn size="small" icon="mdi-download" variant="text" @click="baixarArte(arte)" />
                <v-btn size="small" icon="mdi-share-variant" variant="text" @click="compartilhar(arte)" />
                <v-btn size="small" icon="mdi-delete" variant="text" color="error" @click="excluirArte(arte.id)" />
              </v-card-actions>
            </v-card>
          </v-col>
          <v-col v-if="artes.length === 0 && !carregando" cols="12">
            <v-alert type="info" text="Nenhuma arte criada. Crie sua primeira arte usando os templates!" />
          </v-col>
        </v-row>
      </v-window-item>

      <!-- Templates -->
      <v-window-item value="templates">
        <v-row class="mb-3">
          <v-col v-for="cat in categoriasTemplate" :key="cat.valor" cols="auto">
            <v-btn :variant="filtroTemplate === cat.valor ? 'flat' : 'tonal'"
              :color="filtroTemplate === cat.valor ? 'primary' : undefined"
              size="small" @click="filtroTemplate = cat.valor">
              {{ cat.label }}
            </v-btn>
          </v-col>
        </v-row>
        <v-row>
          <v-col v-for="tpl in templatesFiltrados" :key="tpl.id" cols="12" sm="6" md="4" lg="3">
            <v-card class="cursor-pointer" @click="usarTemplate(tpl)" hover>
              <v-img :src="tpl.previewUrl || '/placeholder.png'" :aspect-ratio="1" cover />
              <v-card-text class="text-center">
                <div class="font-weight-medium">{{ tpl.nome }}</div>
                <div class="text-caption text-medium-emphasis">{{ tpl.categoria }} • {{ tpl.formato }}</div>
              </v-card-text>
              <v-card-actions class="justify-center pt-0">
                <v-btn color="primary" variant="tonal" size="small" @click.stop="usarTemplate(tpl)">
                  Usar este template
                </v-btn>
              </v-card-actions>
            </v-card>
          </v-col>
        </v-row>
      </v-window-item>

      <!-- Agendamentos -->
      <v-window-item value="agendamentos">
        <v-btn prepend-icon="mdi-plus" class="mb-4" @click="dialogAgendamento = true">
          Agendar Publicação
        </v-btn>
        <v-data-table :headers="headersAgendamento" :items="agendamentos" :loading="carregando">
          <template #item.status="{ item }">
            <v-chip :color="corStatusAgendamento(item.status)" size="small">{{ item.status }}</v-chip>
          </template>
          <template #item.dataAgendamento="{ item }">
            {{ new Date(item.dataAgendamento).toLocaleString('pt-BR') }}
          </template>
          <template #item.acoes="{ item }">
            <v-btn v-if="item.status === 'Pendente'" icon="mdi-cancel" size="small" variant="text"
              color="error" @click="cancelarAgendamento(item.id)" />
          </template>
        </v-data-table>
      </v-window-item>
    </v-window>

    <!-- Dialog Editor de Arte -->
    <v-dialog v-model="dialogEditor" fullscreen transition="dialog-bottom-transition">
      <v-card>
        <v-toolbar color="primary" dark>
          <v-btn icon="mdi-close" @click="dialogEditor = false" />
          <v-toolbar-title>{{ arteEditando ? 'Editar Arte' : 'Nova Arte' }}</v-toolbar-title>
          <v-spacer />
          <v-toolbar-items>
            <v-btn prepend-icon="mdi-download" @click="exportarArte">Exportar PNG</v-btn>
            <v-btn prepend-icon="mdi-content-save" @click="salvarArte">Salvar</v-btn>
          </v-toolbar-items>
        </v-toolbar>

        <v-row class="fill-height ma-0">
          <!-- Painel lateral de propriedades -->
          <v-col cols="3" class="border-e pa-4" style="overflow-y:auto">
            <div class="text-subtitle-2 mb-3">Propriedades</div>
            <v-text-field v-model="arteForm.titulo" label="Título da Arte" density="compact" class="mb-2" />
            <v-select v-model="arteForm.tipo" :items="tiposArte" label="Tipo" density="compact" class="mb-2" />
            <v-select v-model="arteForm.formato" :items="formatos" label="Formato" density="compact" class="mb-2" />

            <v-divider class="my-3" />
            <div class="text-subtitle-2 mb-2">Texto Principal</div>
            <v-textarea v-model="arteForm.textoPrincipal" label="Texto" rows="3" density="compact" class="mb-2" />
            <v-text-field v-model="arteForm.textoPrincipalCor" label="Cor do texto" type="color"
              density="compact" class="mb-2" />
            <v-text-field v-model.number="arteForm.textoPrincipalTamanho" label="Tamanho da fonte"
              type="number" density="compact" class="mb-2" />

            <v-divider class="my-3" />
            <div class="text-subtitle-2 mb-2">Produto Destaque</div>
            <v-autocomplete v-model="arteForm.produtoId" :items="produtosOpcoes" item-title="descricao"
              item-value="id" label="Produto" density="compact" class="mb-2"
              @update:search="buscarProdutos" />
            <v-text-field v-model="arteForm.precoDE" label="Preço DE" prefix="R$"
              density="compact" class="mb-2" />
            <v-text-field v-model="arteForm.precoPOR" label="Preço POR" prefix="R$"
              density="compact" class="mb-2" />

            <v-divider class="my-3" />
            <div class="text-subtitle-2 mb-2">Fundo</div>
            <v-text-field v-model="arteForm.corFundo" label="Cor de fundo" type="color"
              density="compact" class="mb-2" />
            <v-file-input label="Imagem de fundo" density="compact" accept="image/*"
              @change="carregarImagemFundo" class="mb-2" />
          </v-col>

          <!-- Canvas de pré-visualização -->
          <v-col cols="9" class="d-flex align-center justify-center bg-grey-lighten-4">
            <div class="arte-canvas elevation-4" :style="estiloCanvas">
              <div class="arte-texto-principal" :style="estiloTexto">
                {{ arteForm.textoPrincipal || 'Texto principal aqui' }}
              </div>
              <div v-if="arteForm.precoPOR" class="arte-preco">
                <span v-if="arteForm.precoDE" class="arte-preco-de">R$ {{ arteForm.precoDE }}</span>
                <span class="arte-preco-por">R$ {{ arteForm.precoPOR }}</span>
              </div>
              <div class="arte-logo">
                <v-icon color="white" size="18">mdi-leaf</v-icon>
                <span class="text-white text-caption ml-1">Clinsoft Produtos Naturais</span>
              </div>
            </div>
          </v-col>
        </v-row>
      </v-card>
    </v-dialog>

    <!-- Dialog Geração com IA (Nano Banana 2) -->
    <v-dialog v-model="dialogIA" max-width="820" persistent scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 d-flex align-center gap-2">
          <v-avatar color="deep-purple" size="36"><v-icon color="white">mdi-robot-happy-outline</v-icon></v-avatar>
          Gerar Arte com IA
          <v-chip size="x-small" color="deep-purple" variant="tonal" class="ml-1">gpt-image-1 · OpenAI</v-chip>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialogIA = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-row>
            <v-col cols="12" md="5">
              <v-text-field v-model="iaForm.titulo" label="Título da arte"
                variant="outlined" density="compact" class="mb-2" />
              <v-select v-model="iaForm.formato" :items="formatosIA" item-title="label" item-value="value"
                label="Formato" variant="outlined" density="compact" class="mb-2" />
              <v-select v-model="iaForm.tipo" :items="tiposArte" label="Tipo" variant="outlined"
                density="compact" class="mb-2" />
              <v-textarea v-model="iaForm.prompt" label="Descreva a arte (prompt) *"
                variant="outlined" density="compact" rows="5" auto-grow
                placeholder="Ex: Banner de promoção de granola artesanal, fundo verde natural com folhas, texto '30% OFF' em destaque, estilo clean e apetitoso"
                hint="Quanto mais detalhes (produto, oferta, cores, estilo), melhor o resultado" persistent-hint />
              <v-btn color="deep-purple" block class="mt-3" size="large"
                prepend-icon="mdi-auto-fix" :loading="gerandoIA"
                :disabled="!iaForm.prompt" @click="gerarComIA">
                Gerar Imagem
              </v-btn>
            </v-col>
            <v-col cols="12" md="7">
              <div class="ia-preview d-flex align-center justify-center">
                <div v-if="!imagemGeradaUrl && !gerandoIA" class="text-center text-medium-emphasis pa-6">
                  <v-icon icon="mdi-image-outline" size="60" class="mb-2" opacity="0.4" />
                  <div>A imagem gerada aparecerá aqui</div>
                </div>
                <v-progress-circular v-else-if="gerandoIA" indeterminate color="deep-purple" size="64" />
                <img v-else :src="imagemGeradaUrl" class="ia-img" />
              </div>
              <div v-if="imagemGeradaUrl" class="d-flex gap-2 mt-3 justify-center">
                <v-btn color="deep-purple" variant="tonal" prepend-icon="mdi-download" @click="baixarGerada">
                  Baixar PNG
                </v-btn>
                <v-btn variant="text" prepend-icon="mdi-check" @click="dialogIA = false">
                  Concluir (salvo na galeria)
                </v-btn>
              </div>
            </v-col>
          </v-row>
        </v-card-text>
      </v-card>
    </v-dialog>

    <!-- Dialog Agendamento -->
    <v-dialog v-model="dialogAgendamento" max-width="480">
      <v-card>
        <v-card-title>Agendar Publicação</v-card-title>
        <v-card-text>
          <v-select v-model="agendamentoForm.arteId" :items="artes" item-title="titulo" item-value="id"
            label="Arte" class="mb-2" />
          <v-select v-model="agendamentoForm.plataforma" :items="['Instagram','Facebook','WhatsApp Status']"
            label="Plataforma" class="mb-2" />
          <v-text-field v-model="agendamentoForm.dataAgendamento" label="Data e Hora"
            type="datetime-local" class="mb-2" />
          <v-textarea v-model="agendamentoForm.legenda" label="Legenda / Texto da publicação" rows="3" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="dialogAgendamento = false">Cancelar</v-btn>
          <v-btn color="primary" @click="salvarAgendamento">Agendar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import api from '@/composables/useApi'
import { useNotifStore } from '@/stores/notif'
import { useAuthStore } from '@/stores/auth'


const notif = useNotifStore()
const auth = useAuthStore()

// ── Geração com IA (Nano Banana 2 / Gemini) ──
const dialogIA = ref(false)
const gerandoIA = ref(false)
const imagemGeradaUrl = ref('')
const arteGeradaId = ref<string | null>(null)
const iaForm = ref({ titulo: '', formato: 'FeedQuadrado', tipo: 'Promocao', prompt: '' })
const formatosIA = [
  { label: 'Feed (1:1 — 1080x1080)', value: 'FeedQuadrado' },
  { label: 'Story (9:16 — 1080x1920)', value: 'StoryVertical' },
  { label: 'Banner (1200x628)', value: 'BannerHorizontal' },
]

function abrirIA() {
  iaForm.value = { titulo: '', formato: 'FeedQuadrado', tipo: 'Promocao', prompt: '' }
  imagemGeradaUrl.value = ''
  arteGeradaId.value = null
  dialogIA.value = true
}

async function gerarComIA() {
  if (!iaForm.value.prompt) { notif.erro('Descreva a arte no prompt.'); return }
  gerandoIA.value = true
  imagemGeradaUrl.value = ''
  try {
    const { data } = await api.post('/marketing/artes/gerar-ia', {
      empresaId: auth.empresaId,
      prompt: iaForm.value.prompt,
      titulo: iaForm.value.titulo || 'Arte IA',
      formato: iaForm.value.formato,
      tipo: iaForm.value.tipo,
    })
    arteGeradaId.value = data.id
    // Exibe via endpoint /exportar (passa pelo proxy /api e é anônimo); cache-bust
    imagemGeradaUrl.value = `/api/marketing/artes/${data.id}/exportar?t=${Date.now()}`
    notif.ok(`Arte gerada com ${data.modelo}!`)
    await listarArtes()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao gerar arte com IA.')
  } finally {
    gerandoIA.value = false
  }
}

function baixarGerada() {
  if (arteGeradaId.value) window.open(`/api/marketing/artes/${arteGeradaId.value}/exportar`, '_blank')
}

const tab = ref('galeria')
const artes = ref<any[]>([])
const agendamentos = ref<any[]>([])
const carregando = ref(false)
const dialogEditor = ref(false)
const dialogAgendamento = ref(false)
const arteEditando = ref<any>(null)
const filtroTipo = ref<string | null>(null)
const filtroTemplate = ref('Todos')
const produtosOpcoes = ref<any[]>([])
const agendamentoForm = ref<any>({})

const arteForm = ref({
  titulo: '',
  tipo: 'Promocao',
  formato: 'Feed',
  textoPrincipal: '',
  textoPrincipalCor: '#ffffff',
  textoPrincipalTamanho: 32,
  corFundo: '#2E7D32',
  produtoId: null,
  precoDE: '',
  precoPOR: '',
})

const tiposArte = ['Promocao', 'Lancamento', 'Aniversario', 'DataComemorativa', 'ClubPromocoes', 'Cardapio']
const formatos = ['Feed (1080x1080)', 'Story (1080x1920)', 'Banner (1200x628)']

const categoriasTemplate = [
  { label: 'Todos', valor: 'Todos' },
  { label: 'Promoção', valor: 'Promocao' },
  { label: 'Lançamento', valor: 'Lancamento' },
  { label: 'Data Comemorativa', valor: 'DataComemorativa' },
  { label: 'Clube', valor: 'ClubPromocoes' },
]

// Templates de fábrica
const templates = ref([
  { id: 't1', nome: 'Oferta Verde', categoria: 'Promocao', formato: 'Feed', previewUrl: '', corFundo: '#2E7D32' },
  { id: 't2', nome: 'Lançamento Natural', categoria: 'Lancamento', formato: 'Feed', previewUrl: '', corFundo: '#1B5E20' },
  { id: 't3', nome: 'Story Promoção', categoria: 'Promocao', formato: 'Story', previewUrl: '', corFundo: '#4CAF50' },
  { id: 't4', nome: 'Data Comemorativa', categoria: 'DataComemorativa', formato: 'Feed', previewUrl: '', corFundo: '#E65100' },
  { id: 't5', nome: 'Clube de Promoções', categoria: 'ClubPromocoes', formato: 'Banner', previewUrl: '', corFundo: '#6A1B9A' },
])

const templatesFiltrados = computed(() =>
  filtroTemplate.value === 'Todos' ? templates.value : templates.value.filter(t => t.categoria === filtroTemplate.value)
)

const estiloCanvas = computed(() => {
  const isStory = arteForm.value.formato?.includes('1920')
  return {
    width: isStory ? '270px' : '480px',
    height: isStory ? '480px' : '480px',
    backgroundColor: arteForm.value.corFundo,
    position: 'relative' as const,
    borderRadius: '8px',
    overflow: 'hidden',
    display: 'flex',
    flexDirection: 'column' as const,
    alignItems: 'center',
    justifyContent: 'center',
    padding: '24px',
  }
})

const estiloTexto = computed(() => ({
  color: arteForm.value.textoPrincipalCor,
  fontSize: `${arteForm.value.textoPrincipalTamanho}px`,
  fontWeight: 'bold',
  textAlign: 'center' as const,
  marginBottom: '16px',
}))

const headersAgendamento = [
  { title: 'Arte', key: 'arteTitulo' },
  { title: 'Plataforma', key: 'plataforma' },
  { title: 'Agendado para', key: 'dataAgendamento' },
  { title: 'Status', key: 'status' },
  { title: 'Ações', key: 'acoes', sortable: false },
]

async function listarArtes() {
  carregando.value = true
  try {
    const { data } = await api.get('/marketing/artes', {
      params: { empresaId: auth.empresaId, tipo: filtroTipo.value || undefined }
    })
    artes.value = Array.isArray(data) ? data : []
  } finally {
    carregando.value = false
  }
}

async function listarAgendamentos() {
  const { data } = await api.get('/marketing/agendamentos', { params: { empresaId: auth.empresaId } })
  agendamentos.value = Array.isArray(data) ? data : []
}

function novaArte() {
  arteEditando.value = null
  arteForm.value = {
    titulo: '', tipo: 'Promocao', formato: 'Feed',
    textoPrincipal: '', textoPrincipalCor: '#ffffff', textoPrincipalTamanho: 32,
    corFundo: '#2E7D32', produtoId: null, precoDE: '', precoPOR: '',
  }
  dialogEditor.value = true
}

function editarArte(arte: any) {
  arteEditando.value = arte
  arteForm.value = { ...arte }
  dialogEditor.value = true
}

function usarTemplate(tpl: any) {
  arteForm.value = {
    titulo: tpl.nome, tipo: tpl.categoria, formato: tpl.formato,
    textoPrincipal: 'Oferta Especial!', textoPrincipalCor: '#ffffff',
    textoPrincipalTamanho: 32, corFundo: tpl.corFundo, produtoId: null, precoDE: '', precoPOR: '',
  }
  arteEditando.value = null
  dialogEditor.value = true
  tab.value = 'galeria'
}

function formatoEnum(f: string) {
  if (!f) return 'FeedQuadrado'
  if (f.includes('Story') || f.includes('1920')) return 'StoryVertical'
  if (f.includes('Banner') || f.includes('628')) return 'BannerHorizontal'
  return 'FeedQuadrado'
}

async function salvarArte() {
  try {
    const payload = {
      empresaId: auth.empresaId,
      nome: arteForm.value.titulo || 'Arte sem título',
      tipo: arteForm.value.tipo,
      formato: formatoEnum(arteForm.value.formato),
      layoutJson: JSON.stringify(arteForm.value),
    }
    if (arteEditando.value) {
      await api.put(`/marketing/artes/${arteEditando.value.id}`, payload)
    } else {
      await api.post('/marketing/artes', payload)
    }
    notif.ok('Arte salva!')
    dialogEditor.value = false
    await listarArtes()
  } catch {
    notif.erro('Erro ao salvar arte.')
  }
}

async function excluirArte(id: string) {
  try {
    await api.delete(`/marketing/artes/${id}`)
    await listarArtes()
  } catch {
    notif.erro('Erro ao excluir arte.')
  }
}

async function exportarArte() {
  try {
    const { data } = await api.get(`/marketing/artes/${arteEditando.value?.id}/exportar`,
      { responseType: 'blob' })
    const url = URL.createObjectURL(new Blob([data]))
    const a = document.createElement('a')
    a.href = url
    a.download = `arte_${Date.now()}.png`
    a.click()
    URL.revokeObjectURL(url)
  } catch {
    notif.erro('Erro ao exportar. Salve a arte primeiro.')
  }
}

function baixarArte(arte: any) {
  window.open(`/api/marketing/artes/${arte.id}/exportar`, '_blank')
}

function compartilhar(arte: any) {
  const msg = encodeURIComponent(`Confira nossa oferta: ${arte.titulo}`)
  window.open(`https://wa.me/?text=${msg}`, '_blank')
}

async function salvarAgendamento() {
  try {
    await api.post('/marketing/agendamentos', { ...agendamentoForm.value, empresaId: auth.empresaId })
    notif.ok('Publicação agendada!')
    dialogAgendamento.value = false
    await listarAgendamentos()
  } catch {
    notif.erro('Erro ao agendar publicação.')
  }
}

async function cancelarAgendamento(id: string) {
  try {
    await api.delete(`/marketing/agendamentos/${id}`)
    await listarAgendamentos()
  } catch {
    notif.erro('Erro ao cancelar agendamento.')
  }
}

async function buscarProdutos(q: string) {
  if (!q || q.length < 2) return
  const { data } = await api.get('/produtos/buscar', { params: { empresaId: auth.empresaId, q } })
  produtosOpcoes.value = Array.isArray(data) ? data : []
}

function carregarImagemFundo(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (file) {
    const url = URL.createObjectURL(file)
    arteForm.value.corFundo = `url(${url})`
  }
}

function formatarData(dt: string) {
  return dt ? new Date(dt).toLocaleDateString('pt-BR') : '-'
}

function arteThumb(arte: any) {
  return arte.urlExportada ? `/api/marketing/artes/${arte.id}/exportar` : '/placeholder.png'
}

function labelFormato(formato: string) {
  return ({ FeedQuadrado: 'Feed', StoryVertical: 'Story', BannerHorizontal: 'Banner' } as any)[formato] ?? formato
}

function corFormato(formato: string) {
  return formato === 'StoryVertical' ? 'purple' : formato === 'BannerHorizontal' ? 'blue' : 'green'
}

function corStatusAgendamento(status: string) {
  return status === 'Publicado' ? 'success' : status === 'Cancelado' ? 'error' : 'warning'
}

onMounted(() => { listarArtes(); listarAgendamentos() })
</script>

<style scoped>
.arte-canvas { user-select: none; }
.arte-preco { text-align: center; }
.arte-preco-de { color: rgba(255,255,255,0.6); text-decoration: line-through; margin-right: 8px; font-size: 18px; }
.arte-preco-por { color: #FFD600; font-size: 36px; font-weight: bold; }
.arte-logo { position: absolute; bottom: 12px; display: flex; align-items: center; opacity: 0.8; }
.ia-preview { min-height: 340px; background: #f3f0fa; border: 1px dashed #b39ddb; border-radius: 12px; overflow: hidden; }
.ia-img { max-width: 100%; max-height: 460px; border-radius: 8px; display: block; }
</style>


