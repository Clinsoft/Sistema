<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div>
        <div class="text-h6 font-weight-bold">Promoções</div>
        <div class="text-caption text-medium-emphasis">Descontos, combos e campanhas promocionais</div>
      </div>
      <v-spacer />
      <v-btn v-if="!ehAtendente" color="primary" prepend-icon="mdi-plus" @click="abrirNova">Nova Promoção</v-btn>
    </div>

    <GuiaPassos
      id="promocoes"
      titulo="Como usar as Promoções"
      :passos="[
        'Clique em <b>Nova Promoção</b> e escolha o <b>tipo</b> (Desconto, Leve X Pague Y, Progressivo, Combo, Pix, Aniversariante). Preencha nome, tipo de desconto (%/R$), valor, período e limite de usos.',
        'Em <b>Aplicação</b>, defina se vale para <b>todos os produtos</b> ou para uma categoria/marca/produto específico. Marque <b>Exclusivo do Clube</b> ou <b>Acumulável</b> conforme a regra.',
        'Na tabela, cada promoção mostra desconto, período, usos e <b>status</b> (Agendada antes do início, Ativa no período, Encerrada após o fim). Use ✏️ para <b>editar</b>, ⏸/▶ para <b>ativar/encerrar</b> e 🗑️ para <b>excluir</b>.',
        'Clique no ícone 🎨 para <b>gerar as artes</b> (Feed, Story e Banner) da promoção e baixá-las em PNG para publicar nas redes.',
      ]"
    />

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" md="4">
          <v-text-field v-model="busca" placeholder="Buscar promoção..."
            prepend-inner-icon="mdi-magnify" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" md="3">
          <v-select v-model="filtroTipo" :items="[{title:'Todos os tipos',value:null},...tiposPromocao.map(t=>({title:t.label,value:t.value}))]"
            label="Tipo" variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" md="3">
          <v-select v-model="filtroStatus" :items="[{title:'Todas',value:null},{title:'Ativas',value:'Ativa'},{title:'Agendadas',value:'Agendada'},{title:'Encerradas',value:'Encerrada'}]"
            label="Status" variant="outlined" density="compact" hide-details />
        </v-col>
      </v-row>
    </v-card>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="promocoes" :loading="carregando" density="compact" hover>
        <template #item.nome="{ item }">
          <div class="font-weight-medium">{{ item.nome }}</div>
          <div class="d-flex align-center gap-1 mt-1">
            <v-chip :color="corTipo(item.tipo)" size="x-small" variant="tonal">{{ labelTipo(item.tipo) }}</v-chip>
            <v-chip v-if="item.apenasClube" color="purple" size="x-small" variant="tonal">
              <v-icon size="x-small" class="mr-1">mdi-star</v-icon>Clube
            </v-chip>
          </div>
        </template>
        <template #item.desconto="{ item }">
          <span class="font-weight-bold text-error">
            {{ item.tipoDesconto === 'Percentual' ? item.desconto + '%' : 'R$ ' + fmt(item.desconto) }}
          </span>
        </template>
        <template #item.periodo="{ item }">
          <div class="text-caption">
            {{ fmtData(item.dataInicio) }} até {{ item.dataFim ? fmtData(item.dataFim) : '∞' }}
          </div>
        </template>
        <template #item.status="{ item }">
          <v-chip :color="corStatus(item.status)" size="small" variant="tonal">{{ item.status }}</v-chip>
        </template>
        <template #item.acoes="{ item }">
          <v-btn icon="mdi-image-multiple-outline" size="x-small" variant="text"
            color="purple" title="Ver / Gerar Artes" @click="abrirArtes(item)" />
          <template v-if="!ehAtendente">
            <v-btn icon="mdi-pencil" size="x-small" variant="text" color="primary" @click="editar(item)" />
            <v-btn :icon="item.status === 'Ativa' ? 'mdi-pause' : 'mdi-play'" size="x-small" variant="text"
              :color="item.status === 'Ativa' ? 'warning' : 'success'" @click="alternarStatus(item)" />
            <v-btn icon="mdi-delete" size="x-small" variant="text" color="error" @click="excluir(item.id)" />
          </template>
        </template>
      </v-data-table>
    </v-card>

    <!-- Dialog Nova/Editar Promoção -->
    <v-dialog v-model="dialog" max-width="720" persistent>
      <v-card rounded="xl" style="display:flex;flex-direction:column;max-height:90vh">
        <div class="cad-header">
          <div class="d-flex align-center gap-3">
            <v-avatar color="error" size="40">
              <v-icon color="white">mdi-tag-multiple</v-icon>
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold">{{ editando ? 'Editar Promoção' : 'Nova Promoção' }}</div>
              <div class="text-caption text-medium-emphasis">{{ labelTipo(form.tipo) }}</div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialog = false" />
        </div>

        <v-card-text class="pa-0" style="overflow-y:auto">
          <div class="cad-body">
            <!-- Tipo de promoção -->
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-tag-outline</v-icon>
                Tipo de Promoção
              </div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col v-for="t in tiposPromocao" :key="t.value" cols="6" md="4">
                    <v-card
                      :variant="form.tipo === t.value ? 'tonal' : 'outlined'"
                      :color="form.tipo === t.value ? t.cor : undefined"
                      rounded="lg" class="pa-3 text-center cursor-pointer"
                      @click="form.tipo = t.value">
                      <v-icon :icon="t.icon" :color="form.tipo === t.value ? t.cor : 'grey'" class="mb-1" />
                      <div class="text-caption font-weight-medium">{{ t.label }}</div>
                    </v-card>
                  </v-col>
                </v-row>
              </div>
            </div>

            <!-- Dados gerais -->
            <div class="cad-secao">
              <div class="cad-secao-header">
                <v-icon size="14">mdi-information-outline</v-icon>
                Dados Gerais
              </div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="12">
                    <v-text-field v-model="form.nome" label="Nome da Promoção *"
                      variant="outlined" density="compact" :rules="[r => !!r || 'Obrigatório']" />
                  </v-col>
                  <v-col cols="12" md="4">
                    <v-select v-model="form.tipoDesconto"
                      :items="[{title:'Percentual (%)',value:'Percentual'},{title:'Valor fixo (R$)',value:'ValorFixo'}]"
                      label="Tipo de desconto" variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" md="4">
                    <v-text-field v-model.number="form.desconto" label="Desconto *"
                      type="number" :suffix="form.tipoDesconto === 'Percentual' ? '%' : 'R$'"
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" md="4">
                    <v-text-field v-model.number="form.limiteUso" label="Limite de usos (0 = ilimitado)"
                      type="number" variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" md="6">
                    <v-text-field v-model="form.dataInicio" label="Início *"
                      type="date" variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" md="6">
                    <v-text-field v-model="form.dataFim" label="Fim (vazio = sem vencimento)"
                      type="date" variant="outlined" density="compact" clearable />
                  </v-col>
                </v-row>
              </div>
            </div>

            <!-- Regras específicas por tipo -->
            <div class="cad-secao" v-if="form.tipo === 'LeveXPagueY'">
              <div class="cad-secao-header"><v-icon size="14">mdi-cart-arrow-right</v-icon> Regra Leve X Pague Y</div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="6">
                    <v-text-field v-model.number="form.qtdeLeve" label="Quantidade no carrinho (Leve)"
                      type="number" variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="6">
                    <v-text-field v-model.number="form.qtdePague" label="Quantidade cobrada (Pague)"
                      type="number" variant="outlined" density="compact" />
                  </v-col>
                </v-row>
              </div>
            </div>

            <div class="cad-secao" v-if="form.tipo === 'DescontoProgressivo'">
              <div class="cad-secao-header"><v-icon size="14">mdi-chart-line</v-icon> Desconto Progressivo</div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="6">
                    <v-text-field v-model.number="form.valorMinimoPedido" label="Valor mínimo do pedido (R$)"
                      type="number" prefix="R$" variant="outlined" density="compact" />
                  </v-col>
                </v-row>
              </div>
            </div>

            <!-- Aplicação -->
            <div class="cad-secao">
              <div class="cad-secao-header"><v-icon size="14">mdi-filter-outline</v-icon> Aplicação</div>
              <div class="cad-secao-body">
                <v-row dense>
                  <v-col cols="12" md="6">
                    <v-select v-model="form.aplicaEm"
                      :items="[{title:'Todos os produtos',value:'Todos'},{title:'Categoria específica',value:'Categoria'},{title:'Marca específica',value:'Marca'},{title:'Produto específico',value:'Produto'}]"
                      label="Aplicar em" variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" md="6" v-if="form.aplicaEm !== 'Todos'">
                    <v-autocomplete v-model="form.referenciaId" :items="itensFiltro"
                      item-title="nome" item-value="id"
                      :label="'Selecionar ' + form.aplicaEm"
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12">
                    <div class="d-flex gap-4">
                      <v-switch v-model="form.apenasClube" label="Exclusivo para membros do Clube"
                        color="purple" density="compact" hide-details />
                      <v-switch v-model="form.cumulativo" label="Acumulável com outras promoções"
                        color="teal" density="compact" hide-details />
                    </div>
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

    <!-- Dialog Artes da Promoção -->
    <v-dialog v-model="dialogArtes" max-width="820" scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 d-flex align-center gap-2">
          <v-icon icon="mdi-image-multiple" color="purple" />
          Artes — {{ promocaoArtes?.nome }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" density="compact" @click="dialogArtes = false" />
        </v-card-title>
        <v-divider />

        <v-card-text class="pa-4">
          <!-- Gerar artes -->
          <div v-if="!artesGeradas.length" class="text-center py-6">
            <v-icon icon="mdi-palette-outline" size="60" color="purple" class="mb-3" opacity="0.4" />
            <div class="text-body-1 font-weight-bold mb-1">Nenhuma arte gerada ainda</div>
            <div class="text-caption text-medium-emphasis mb-4">
              Gere automaticamente 3 layouts (Feed, Story e Banner) para esta promoção
            </div>
            <v-btn color="purple" size="large" prepend-icon="mdi-auto-fix"
              :loading="gerandoArtes" @click="gerarArtes">
              Gerar Artes Automaticamente
            </v-btn>
          </div>

          <template v-else>
            <!-- Abas por formato -->
            <v-tabs v-model="tabArte" density="compact" class="mb-4">
              <v-tab v-for="f in formatosArte" :key="f.id" :value="f.id">
                <v-icon :icon="f.icon" size="16" class="mr-1" />{{ f.label }}
              </v-tab>
            </v-tabs>

            <v-window v-model="tabArte">
              <v-window-item v-for="f in formatosArte" :key="f.id" :value="f.id">
                <div class="d-flex flex-column align-center gap-3">
                  <ArtePromoCanvas
                    :ref="el => canvasRefs[f.id] = el"
                    :layout="layoutParaFormato(f.id)"
                    :previewW="f.previewW" />
                  <div class="d-flex gap-2">
                    <v-btn prepend-icon="mdi-download" color="purple" variant="tonal"
                      @click="baixarArte(f.id)">
                      Baixar PNG
                    </v-btn>
                    <v-btn prepend-icon="mdi-refresh" variant="text"
                      :loading="gerandoArtes" @click="gerarArtes">
                      Regerar
                    </v-btn>
                  </div>
                </div>
              </v-window-item>
            </v-window>

            <!-- Botão reagendar -->
            <v-divider class="my-4" />
            <div class="d-flex align-center gap-2">
              <v-icon icon="mdi-calendar-clock" color="primary" />
              <span class="text-body-2">Agendar publicação dessas artes</span>
              <v-spacer />
              <v-btn size="small" color="primary" variant="tonal"
                prepend-icon="mdi-send" @click="$router.push('/marketing')">
                Ir para Marketing
              </v-btn>
            </div>
          </template>
        </v-card-text>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import ArtePromoCanvas from './ArtePromoCanvas.vue'

const auth = useAuthStore()
const notif = useNotifStore()
// Atendente só visualiza promoções (e pode gerar artes); não cria/edita/exclui.
const ehAtendente = computed(() => auth.usuario?.role === 'Atendente')

const carregando = ref(false)
const salvando = ref(false)
const dialog = ref(false)
const editando = ref<string | null>(null)
const busca = ref('')
const filtroTipo = ref<string | null>(null)
const filtroStatus = ref<string | null>(null)
const promocoes = ref<any[]>([])
const itensFiltro = ref<any[]>([])

// Artes
const dialogArtes = ref(false)
const promocaoArtes = ref<any>(null)
const artesGeradas = ref<any[]>([])
const gerandoArtes = ref(false)
const tabArte = ref('FeedQuadrado')
const canvasRefs = ref<Record<string, any>>({})

const formatosArte = [
  { id: 'FeedQuadrado',     label: 'Feed',   icon: 'mdi-square',           previewW: 340 },
  { id: 'StoryVertical',    label: 'Story',  icon: 'mdi-cellphone',        previewW: 220 },
  { id: 'BannerHorizontal', label: 'Banner', icon: 'mdi-panorama-wide-angle', previewW: 520 },
]

const tiposPromocao = [
  { value: 'Desconto',             label: 'Desconto simples',        icon: 'mdi-tag',                   cor: 'error'       },
  { value: 'LeveXPagueY',          label: 'Leve X Pague Y',          icon: 'mdi-cart-arrow-right',      cor: 'orange'      },
  { value: 'DescontoProgressivo',  label: 'Desconto progressivo',    icon: 'mdi-chart-line',            cor: 'indigo'      },
  { value: 'Combo',                label: 'Combo / Kit',             icon: 'mdi-package-variant',       cor: 'teal'        },
  { value: 'Pix',                  label: 'Desconto Pix',            icon: 'mdi-qrcode',                cor: 'green'       },
  { value: 'Aniversariante',       label: 'Aniversariante',          icon: 'mdi-cake-variant-outline',  cor: 'pink'        },
]

const formPadrao = () => ({
  nome: '', tipo: 'Desconto', tipoDesconto: 'Percentual', desconto: 0,
  dataInicio: new Date().toISOString().slice(0, 10), dataFim: '',
  aplicaEm: 'Todos', referenciaId: null as string | null,
  qtdeLeve: 3, qtdePague: 2, valorMinimoPedido: 0, limiteUso: 0,
  apenasClube: false, cumulativo: false,
})
const form = ref(formPadrao())

const headers = [
  { title: 'Promoção', key: 'nome' },
  { title: 'Desconto', key: 'desconto', width: 110 },
  { title: 'Período', key: 'periodo' },
  { title: 'Usos', key: 'qtdeUsada', width: 80 },
  { title: 'Status', key: 'status', width: 110 },
  { title: '', key: 'acoes', sortable: false, width: 100 },
]

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtData = (v: string) => v ? new Date(v).toLocaleDateString('pt-BR') : '—'
const labelTipo = (t: string) => tiposPromocao.find(x => x.value === t)?.label ?? t
const corTipo = (t: string) => tiposPromocao.find(x => x.value === t)?.cor ?? 'default'
const corStatus = (s: string) => s === 'Ativa' ? 'success' : s === 'Agendada' ? 'info' : 'default'

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/promocoes', {
      params: { empresaId: auth.empresaId, tipo: filtroTipo.value, status: filtroStatus.value, q: busca.value }
    })
    promocoes.value = r.data ?? []
  } catch { promocoes.value = [] }
  finally { carregando.value = false }
}

function abrirNova() {
  editando.value = null
  form.value = formPadrao()
  dialog.value = true
}

function editar(item: any) {
  editando.value = item.id
  form.value = {
    ...formPadrao(),
    ...item,
    // As datas vêm como ISO do backend; o input type=date espera yyyy-MM-dd
    dataInicio: item.dataInicio ? String(item.dataInicio).slice(0, 10) : '',
    dataFim: item.dataFim ? String(item.dataFim).slice(0, 10) : '',
  }
  dialog.value = true
}

async function salvar() {
  if (!form.value.nome) { notif.erro('Nome é obrigatório.'); return }
  salvando.value = true
  try {
    if (editando.value) {
      await api.put(`/promocoes/${editando.value}`, form.value)
      notif.ok('Promoção atualizada!')
    } else {
      await api.post('/promocoes', { ...form.value, empresaId: auth.empresaId })
      notif.ok('Promoção salva! Gerando artes...')
      // Gera artes automaticamente ao criar nova promoção
      gerarArtesAutomatico(form.value)
    }
    dialog.value = false
    await carregar()
  } catch { notif.erro('Erro ao salvar promoção.') }
  finally { salvando.value = false }
}

async function gerarArtesAutomatico(promocao: any) {
  try {
    await api.post('/marketing/artes/auto-promocao', {
      empresaId: auth.empresaId,
      nomePromocao: promocao.nome,
      desconto: promocao.desconto,
      tipoDesconto: promocao.tipoDesconto,
      tipoPromocao: promocao.tipo,
      aplicaEm: promocao.aplicaEm,
      dataInicio: promocao.dataInicio,
      dataFim: promocao.dataFim || null,
      apenasClube: promocao.apenasClube,
    })
    notif.ok('Artes geradas! Acesse o ícone 🎨 na promoção para visualizar.')
  } catch { /* silencioso — não bloqueia o fluxo */ }
}

function abrirArtes(item: any) {
  promocaoArtes.value = item
  artesGeradas.value = []
  tabArte.value = 'FeedQuadrado'
  dialogArtes.value = true
  // Carrega artes já existentes do backend
  carregarArtesDaPromocao(item)
}

async function carregarArtesDaPromocao(item: any) {
  try {
    const r = await api.get('/marketing/artes', { params: { empresaId: auth.empresaId } })
    const nome = item.nome
    artesGeradas.value = (r.data ?? []).filter((a: any) => a.titulo?.startsWith(nome))
  } catch { artesGeradas.value = [] }
}

async function gerarArtes() {
  if (!promocaoArtes.value) return
  gerandoArtes.value = true
  try {
    await api.post('/marketing/artes/auto-promocao', {
      empresaId: auth.empresaId,
      nomePromocao: promocaoArtes.value.nome,
      desconto: promocaoArtes.value.desconto,
      tipoDesconto: promocaoArtes.value.tipoDesconto,
      tipoPromocao: promocaoArtes.value.tipo,
      aplicaEm: promocaoArtes.value.aplicaEm,
      dataInicio: promocaoArtes.value.dataInicio,
      dataFim: promocaoArtes.value.dataFim || null,
      apenasClube: promocaoArtes.value.apenasClube ?? false,
    })
    await carregarArtesDaPromocao(promocaoArtes.value)
    notif.ok('Artes geradas com sucesso!')
  } catch { notif.erro('Erro ao gerar artes.') }
  finally { gerandoArtes.value = false }
}

function layoutParaFormato(formato: string) {
  const p = promocaoArtes.value
  if (!p) return {}
  return {
    gerador: 'AutoPromocao',
    formato,
    nomePromocao: p.nome,
    desconto: p.desconto,
    tipoDesconto: p.tipoDesconto,
    tipoPromocao: p.tipo,
    aplicaEm: p.aplicaEm,
    dataInicio: p.dataInicio,
    dataFim: p.dataFim,
    apenasClube: p.apenasClube ?? false,
  }
}

async function baixarArte(formato: string) {
  const ref = canvasRefs.value[formato]
  if (ref?.exportarPng) await ref.exportarPng()
}

async function alternarStatus(item: any) {
  try {
    const novoStatus = item.status === 'Ativa' ? 'Encerrada' : 'Ativa'
    await api.patch(`/promocoes/${item.id}/status`, { status: novoStatus })
    await carregar()
  } catch { notif.erro('Erro ao alterar status.') }
}

async function excluir(id: string) {
  try {
    await api.delete(`/promocoes/${id}`)
    await carregar()
    notif.ok('Promoção removida.')
  } catch { notif.erro('Erro ao remover.') }
}

onMounted(carregar)
</script>

<style scoped>
.cursor-pointer { cursor: pointer; }
.cad-header { display:flex; align-items:center; justify-content:space-between; padding:16px 20px; }
.cad-body { background:#f5f6f8; padding:16px; display:flex; flex-direction:column; gap:12px; }
.cad-secao { background:white; border-radius:12px; border:1px solid #e8edf3; overflow:hidden; }
.cad-secao-header { display:flex; align-items:center; gap:6px; padding:10px 16px; background:#f8f9fb; border-bottom:1px solid #e8edf3; font-size:0.75rem; font-weight:700; text-transform:uppercase; letter-spacing:0.07em; color:rgb(var(--v-theme-primary)); }
.cad-secao-body { padding:16px; }
</style>
