<template>
  <v-container fluid>
    <v-row align="center" class="mb-4">
      <v-col>
        <h2 class="text-h5 font-weight-bold">Catálogo WhatsApp</h2>
        <div class="text-caption text-medium-emphasis">Gerencie seu catálogo e pedidos pelo WhatsApp</div>
      </v-col>
      <v-col cols="auto">
        <v-btn color="success" prepend-icon="mdi-whatsapp" @click="sincronizarCatalogo" :loading="sincronizando">
          Sincronizar Catálogo
        </v-btn>
      </v-col>
    </v-row>

    <v-tabs v-model="tab" class="mb-4">
      <v-tab value="catalogo">Catálogo</v-tab>
      <v-tab value="pedidos">Pedidos</v-tab>
      <v-tab value="config">Configuração</v-tab>
    </v-tabs>

    <v-window v-model="tab">
      <!-- Catálogo -->
      <v-window-item value="catalogo">
        <v-row>
          <v-col cols="12" md="4">
            <v-btn prepend-icon="mdi-plus" variant="tonal" @click="adicionarProduto">
              Adicionar Produto ao Catálogo
            </v-btn>
          </v-col>
          <v-col cols="12" md="4">
            <v-text-field v-model="buscaCatalogo" label="Buscar produto" prepend-inner-icon="mdi-magnify"
              density="compact" hide-details clearable />
          </v-col>
        </v-row>

        <v-row class="mt-2">
          <v-col v-for="item in itensFiltrados" :key="item.id" cols="12" sm="6" md="4" lg="3">
            <v-card>
              <v-img :src="item.foto || '/placeholder.png'" height="140" cover>
                <template #error>
                  <v-icon size="64" class="ma-auto d-flex" color="grey-lighten-2">mdi-image-off</v-icon>
                </template>
              </v-img>
              <v-card-text class="pb-1">
                <div class="font-weight-medium text-truncate">{{ item.descricao }}</div>
                <div class="text-caption text-medium-emphasis">{{ item.categoria }}</div>
                <div class="text-h6 text-success">R$ {{ item.precoWhatsApp?.toFixed(2) }}</div>
              </v-card-text>
              <v-card-actions class="pt-0">
                <v-btn size="small" variant="text" @click="editarItemCatalogo(item)">Editar</v-btn>
                <v-spacer />
                <v-btn size="small" icon="mdi-delete" variant="text" color="error"
                  @click="removerItemCatalogo(item.id)" />
              </v-card-actions>
            </v-card>
          </v-col>
          <v-col v-if="itensFiltrados.length === 0" cols="12">
            <v-alert type="info" text="Nenhum produto no catálogo. Adicione produtos para começar." />
          </v-col>
        </v-row>
      </v-window-item>

      <!-- Pedidos -->
      <v-window-item value="pedidos">
        <v-row class="mb-2">
          <v-col v-for="s in statusPedidos" :key="s.status" cols="6" md="3">
            <v-card variant="tonal" :color="s.cor">
              <v-card-text class="text-center py-2">
                <div class="text-h5 font-weight-bold">{{ s.qtd }}</div>
                <div class="text-caption">{{ s.label }}</div>
              </v-card-text>
            </v-card>
          </v-col>
        </v-row>

        <v-data-table :headers="headersPedidos" :items="pedidos" :loading="carregandoPedidos">
          <template #item.status="{ item }">
            <v-chip :color="corStatusPedido(item.status)" size="small">{{ item.status }}</v-chip>
          </template>
          <template #item.total="{ item }">R$ {{ item.total?.toFixed(2) }}</template>
          <template #item.acoes="{ item }">
            <v-btn-group density="compact" variant="tonal">
              <v-btn size="small" @click="avancarStatus(item)">Avançar</v-btn>
              <v-btn size="small" icon="mdi-whatsapp" color="success" @click="responderWhatsApp(item)" />
            </v-btn-group>
          </template>
        </v-data-table>
      </v-window-item>

      <!-- Configuração -->
      <v-window-item value="config">
        <v-card max-width="600">
          <v-card-title>Configuração WhatsApp Business</v-card-title>
          <v-card-text>
            <v-alert type="info" class="mb-4" variant="tonal">
              Para integrar com o WhatsApp Business, você precisa de uma conta Meta Business e acesso à API.
            </v-alert>
            <v-text-field v-model="config.phoneNumberId" label="Phone Number ID (Meta)" class="mb-2" />
            <v-text-field v-model="config.accessToken" label="Access Token" type="password" class="mb-2" />
            <v-text-field v-model="config.catalogId" label="Catalog ID" class="mb-2" />
            <v-text-field v-model="config.numeroWhatsApp" label="Número WhatsApp (para link)" 
              hint="+5511999999999" class="mb-2" />
            <v-switch v-model="config.ativo" label="Integração Ativa" color="success" />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn color="primary" @click="salvarConfig">Salvar Configuração</v-btn>
          </v-card-actions>
        </v-card>

        <v-card class="mt-4" max-width="600">
          <v-card-title>Link de Compartilhamento</v-card-title>
          <v-card-text>
            <v-text-field :value="linkCatalogo" label="Link do Catálogo" readonly
              append-inner-icon="mdi-content-copy" @click:append-inner="copiarLink" />
            <v-btn color="success" prepend-icon="mdi-whatsapp" :href="'https://wa.me/' + config.numeroWhatsApp" target="_blank">
              Abrir no WhatsApp
            </v-btn>
          </v-card-text>
        </v-card>
      </v-window-item>
    </v-window>

    <!-- Dialog Adicionar/Editar Item -->
    <v-dialog v-model="dialogItem" max-width="520">
      <v-card>
        <v-card-title>{{ itemEditando ? 'Editar Item' : 'Adicionar ao Catálogo' }}</v-card-title>
        <v-card-text>
          <v-autocomplete v-if="!itemEditando" v-model="novoItem.produtoId" :items="produtosBusca"
            item-title="descricao" item-value="id" label="Produto" @update:search="buscarProdutos" />
          <v-text-field v-model.number="novoItem.precoWhatsApp" label="Preço no Catálogo (R$)"
            type="number" prefix="R$" />
          <v-textarea v-model="novoItem.descricaoWhatsApp" label="Descrição para WhatsApp" rows="3"
            hint="Texto que aparecerá na mensagem quando o cliente selecionar o produto" />
          <v-switch v-model="novoItem.disponivel" label="Disponível no catálogo" color="success" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="dialogItem = false">Cancelar</v-btn>
          <v-btn color="primary" @click="salvarItem">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useNotifStore } from '@/stores/notif'
import { useAuthStore } from '@/stores/auth'


const notif = useNotifStore()
const auth = useAuthStore()

const tab = ref('catalogo')
const itensCatalogo = ref<any[]>([])
const pedidos = ref<any[]>([])
const carregandoPedidos = ref(false)
const sincronizando = ref(false)
const buscaCatalogo = ref('')
const dialogItem = ref(false)
const itemEditando = ref<any>(null)
const novoItem = ref<any>({})
const produtosBusca = ref<any[]>([])

const config = ref({
  phoneNumberId: '', accessToken: '', catalogId: '',
  numeroWhatsApp: '', ativo: false
})

const itensFiltrados = computed(() =>
  itensCatalogo.value.filter(i =>
    !buscaCatalogo.value || i.descricao?.toLowerCase().includes(buscaCatalogo.value.toLowerCase())
  )
)

const statusPedidos = computed(() => {
  const contagem = (s: string) => pedidos.value.filter(p => p.status === s).length
  return [
    { status: 'Novo', label: 'Novos', qtd: contagem('Novo'), cor: 'warning' },
    { status: 'EmSeparacao', label: 'Em Separação', qtd: contagem('EmSeparacao'), cor: 'info' },
    { status: 'Enviado', label: 'Enviados', qtd: contagem('Enviado'), cor: 'primary' },
    { status: 'Entregue', label: 'Entregues', qtd: contagem('Entregue'), cor: 'success' },
  ]
})

const headersPedidos = [
  { title: 'Data', key: 'data' },
  { title: 'Cliente', key: 'clienteNome' },
  { title: 'Total', key: 'total', align: 'end' as const },
  { title: 'Status', key: 'status' },
  { title: 'Ações', key: 'acoes', sortable: false },
]

const linkCatalogo = computed(() =>
  `https://wa.me/${config.value.numeroWhatsApp}?text=Quero+ver+o+catálogo`
)

async function listarCatalogo() {
  try {
    const { data } = await api.get('/whatsapp/catalogo', { params: { empresaId: auth.empresaId } })
    itensCatalogo.value = Array.isArray(data) ? data : []
  } catch {}
}

async function listarPedidos() {
  carregandoPedidos.value = true
  try {
    const { data } = await api.get('/whatsapp/pedidos', { params: { empresaId: auth.empresaId } })
    pedidos.value = Array.isArray(data) ? data : []
  } finally {
    carregandoPedidos.value = false
  }
}

async function sincronizarCatalogo() {
  sincronizando.value = true
  try {
    await api.post('/whatsapp/catalogo/sincronizar', { empresaId: auth.empresaId })
    notif.ok('Catálogo sincronizado com sucesso!')
  } catch {
    notif.erro('Erro ao sincronizar. Verifique as configurações.')
  } finally {
    sincronizando.value = false
  }
}

function adicionarProduto() {
  itemEditando.value = null
  novoItem.value = { disponivel: true, precoWhatsApp: 0 }
  dialogItem.value = true
}

function editarItemCatalogo(item: any) {
  itemEditando.value = item
  novoItem.value = { ...item }
  dialogItem.value = true
}

async function removerItemCatalogo(id: string) {
  try {
    await api.delete(`/whatsapp/catalogo/${id}`)
    await listarCatalogo()
  } catch {
    notif.erro('Erro ao remover item.')
  }
}

async function buscarProdutos(q: string) {
  if (!q || q.length < 2) return
  const { data } = await api.get('/produtos/buscar', { params: { empresaId: auth.empresaId, q } })
  produtosBusca.value = Array.isArray(data) ? data : []
}

async function salvarItem() {
  try {
    const payload = { ...novoItem.value, empresaId: auth.empresaId }
    if (itemEditando.value) {
      await api.put(`/whatsapp/catalogo/${itemEditando.value.id}`, payload)
    } else {
      await api.post('/whatsapp/catalogo', payload)
    }
    notif.ok('Item salvo!')
    dialogItem.value = false
    await listarCatalogo()
  } catch {
    notif.erro('Erro ao salvar item.')
  }
}

async function avancarStatus(pedido: any) {
  const fluxo: Record<string, string> = {
    Novo: 'EmSeparacao', EmSeparacao: 'Enviado', Enviado: 'Entregue'
  }
  const novoStatus = fluxo[pedido.status]
  if (!novoStatus) return
  try {
    await api.patch(`/whatsapp/pedidos/${pedido.id}/status`, { status: novoStatus })
    await listarPedidos()
  } catch {
    notif.erro('Erro ao atualizar status.')
  }
}

function responderWhatsApp(pedido: any) {
  const msg = encodeURIComponent(`Olá ${pedido.clienteNome}! Seu pedido está ${pedido.status}.`)
  window.open(`https://wa.me/${pedido.clienteTelefone}?text=${msg}`, '_blank')
}

async function salvarConfig() {
  try {
    await api.put('/whatsapp/configuracao', { ...config.value, empresaId: auth.empresaId })
    notif.ok('Configuração salva!')
  } catch {
    notif.erro('Erro ao salvar configuração.')
  }
}

function copiarLink() {
  navigator.clipboard.writeText(linkCatalogo.value)
  notif.ok('Link copiado!')
}

function corStatusPedido(status: string) {
  const mapa: Record<string, string> = {
    Novo: 'warning', EmSeparacao: 'info', Enviado: 'primary', Entregue: 'success'
  }
  return mapa[status] ?? 'default'
}

onMounted(() => { listarCatalogo(); listarPedidos() })
</script>


