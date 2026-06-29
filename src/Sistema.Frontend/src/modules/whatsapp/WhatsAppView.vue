<template>
  <v-container fluid>
    <v-row align="center" class="mb-4">
      <v-col>
        <h2 class="text-h5 font-weight-bold">WhatsApp Business</h2>
        <div class="text-caption text-medium-emphasis">Catálogo, pedidos e mensagens automáticas</div>
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
      <v-tab value="mensagens">Mensagens Automáticas</v-tab>
      <v-tab value="templates">Templates</v-tab>
      <v-tab value="historico">Histórico</v-tab>
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

      <!-- Mensagens Automáticas -->
      <v-window-item value="mensagens">
        <v-row>
          <!-- Cards de disparos configurados -->
          <v-col cols="12" md="4">
            <v-card color="success" variant="tonal" class="mb-4">
              <v-card-text>
                <div class="d-flex align-center justify-space-between">
                  <div>
                    <div class="text-h6 font-weight-bold">Aniversariantes</div>
                    <div class="text-caption">Disparo automático às {{ cfgMsg.horaDisparo }}h</div>
                  </div>
                  <v-icon size="40" color="success">mdi-cake-variant</v-icon>
                </div>
                <v-switch v-model="cfgMsg.enviarAniversario" color="success" density="compact"
                  label="Ativo" hide-details @update:model-value="salvarCfgMsg" />
              </v-card-text>
            </v-card>

            <v-card color="orange" variant="tonal" class="mb-4">
              <v-card-text>
                <div class="d-flex align-center justify-space-between">
                  <div>
                    <div class="text-h6 font-weight-bold">Promoções</div>
                    <div class="text-caption">Disparado automaticamente com artes de validade</div>
                  </div>
                  <v-icon size="40" color="orange">mdi-tag-multiple</v-icon>
                </div>
                <v-switch v-model="cfgMsg.enviarPromocoes" color="orange" density="compact"
                  label="Ativo" hide-details @update:model-value="salvarCfgMsg" />
              </v-card-text>
            </v-card>

            <v-card color="primary" variant="tonal" class="mb-4">
              <v-card-text>
                <div class="d-flex align-center justify-space-between">
                  <div>
                    <div class="text-h6 font-weight-bold">Novidades</div>
                    <div class="text-caption">Disparo manual para todos os clientes</div>
                  </div>
                  <v-icon size="40" color="primary">mdi-newspaper-variant</v-icon>
                </div>
                <v-switch v-model="cfgMsg.enviarNovidades" color="primary" density="compact"
                  label="Ativo" hide-details @update:model-value="salvarCfgMsg" />
              </v-card-text>
            </v-card>

            <v-card variant="outlined">
              <v-card-text>
                <div class="text-subtitle-2 mb-2">Hora do disparo automático</div>
                <v-slider v-model="cfgMsg.horaDisparo" :min="6" :max="20" :step="1"
                  thumb-label color="success" @update:model-value="salvarCfgMsg">
                  <template #thumb-label="{ modelValue }">{{ modelValue }}h</template>
                </v-slider>
              </v-card-text>
            </v-card>
          </v-col>

          <!-- Disparo de campanhas -->
          <v-col cols="12" md="8">
            <v-card class="mb-4">
              <v-card-title>Disparar Campanha Agora</v-card-title>
              <v-card-text>
                <v-alert type="info" variant="tonal" density="compact" class="mb-3">
                  Enfileira o disparo imediato para todos os clientes ativos com telefone cadastrado.
                  Promoções usam os produtos em oferta de hoje. Novidades envia para toda a base.
                </v-alert>
                <div class="d-flex gap-3 flex-wrap">
                  <v-btn color="orange" prepend-icon="mdi-tag-multiple" :loading="disparandoPromocao"
                    @click="dispararPromocao">
                    Disparar Promoções
                  </v-btn>
                  <v-btn color="primary" prepend-icon="mdi-newspaper-variant" :loading="disparandoNovidade"
                    @click="dispararNovidade">
                    Disparar Novidade
                  </v-btn>
                </div>
              </v-card-text>
            </v-card>

            <v-card>
              <v-card-title>Envio Manual</v-card-title>
              <v-card-text>
                <v-alert type="info" variant="tonal" class="mb-3">
                  Use para enviar uma mensagem personalizada para um cliente específico.
                  O template precisa estar aprovado na Meta.
                </v-alert>
                <v-row>
                  <v-col cols="12" sm="6">
                    <v-text-field v-model="envioManual.telefone" label="Telefone (com DDD)"
                      placeholder="11999999999" prepend-inner-icon="mdi-phone" />
                  </v-col>
                  <v-col cols="12" sm="6">
                    <v-text-field v-model="envioManual.nomeDestinatario" label="Nome do destinatário" />
                  </v-col>
                  <v-col cols="12" sm="6">
                    <v-select v-model="envioManual.templateId" :items="templates"
                      item-title="nomeMeta" item-value="id" label="Template" return-object
                      @update:model-value="t => envioManual.templateName = t?.nomeMeta" />
                  </v-col>
                  <v-col cols="12" sm="6">
                    <v-select v-model="envioManual.tipoDisparo" label="Tipo"
                      :items="tiposDisparo" />
                  </v-col>
                  <v-col cols="12">
                    <v-textarea v-model="envioManual.variaveisTexto" label="Variáveis (uma por linha)"
                      rows="3" hint="Ex: João Silva&#10;30%" persistent-hint />
                  </v-col>
                </v-row>
              </v-card-text>
              <v-card-actions>
                <v-spacer />
                <v-btn color="success" prepend-icon="mdi-send" :loading="enviando"
                  @click="enviarManual" :disabled="!envioManual.telefone || !envioManual.templateName">
                  Enviar Mensagem
                </v-btn>
              </v-card-actions>
            </v-card>
          </v-col>
        </v-row>
      </v-window-item>

      <!-- Templates -->
      <v-window-item value="templates">
        <v-row class="mb-4">
          <v-col>
            <div class="text-subtitle-1 font-weight-medium">Templates Cadastrados</div>
            <div class="text-caption text-medium-emphasis">
              Templates precisam estar aprovados na Meta Business Manager antes de ser usados.
            </div>
          </v-col>
          <v-col cols="auto">
            <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirDialogTemplate(null)">
              Novo Template
            </v-btn>
            <v-btn variant="outlined" class="ml-2" prepend-icon="mdi-cloud-download"
              @click="importarTemplatesMeta" :loading="importandoTemplates">
              Importar da Meta
            </v-btn>
          </v-col>
        </v-row>

        <v-row>
          <v-col v-for="t in templates" :key="t.id" cols="12" md="6" lg="4">
            <v-card variant="outlined">
              <v-card-item>
                <template #prepend>
                  <v-icon :color="corTipoDisparo(t.tipoDisparo)">{{ iconeTipoDisparo(t.tipoDisparo) }}</v-icon>
                </template>
                <v-card-title>{{ t.nomeMeta }}</v-card-title>
                <v-card-subtitle>{{ t.tipoDisparo }} · {{ t.idioma }}</v-card-subtitle>
                <template #append>
                  <v-btn icon="mdi-pencil" size="small" variant="text" @click="abrirDialogTemplate(t)" />
                </template>
              </v-card-item>
              <v-card-text v-if="t.exemploTexto" class="pt-0">
                <div class="text-caption bg-grey-lighten-4 rounded pa-2">{{ t.exemploTexto }}</div>
              </v-card-text>
              <v-card-text class="pt-0">
                <v-chip size="x-small" :color="corTipoDisparo(t.tipoDisparo)">{{ t.tipoDisparo }}</v-chip>
              </v-card-text>
            </v-card>
          </v-col>
          <v-col v-if="templates.length === 0" cols="12">
            <v-alert type="info" variant="tonal">
              Nenhum template cadastrado. Crie templates na Meta Business Manager e cadastre-os aqui.
            </v-alert>
          </v-col>
        </v-row>
      </v-window-item>

      <!-- Histórico -->
      <v-window-item value="historico">
        <v-row class="mb-3" align="center">
          <v-col cols="12" sm="4">
            <v-select v-model="filtroHistorico.tipo" label="Tipo de disparo" clearable
              :items="tiposDisparo" density="compact" @update:model-value="carregarHistorico" />
          </v-col>
          <v-col cols="12" sm="4">
            <v-select v-model="filtroHistorico.status" label="Status" clearable density="compact"
              :items="['Pendente','Enviada','Entregue','Lida','Falhou']"
              @update:model-value="carregarHistorico" />
          </v-col>
          <v-col cols="auto">
            <v-btn variant="outlined" prepend-icon="mdi-refresh" @click="carregarHistorico">Atualizar</v-btn>
          </v-col>
        </v-row>

        <!-- Cards resumo -->
        <v-row class="mb-3">
          <v-col v-for="s in resumoHistorico" :key="s.label" cols="6" sm="3">
            <v-card variant="tonal" :color="s.cor">
              <v-card-text class="text-center py-2">
                <div class="text-h5 font-weight-bold">{{ s.qtd }}</div>
                <div class="text-caption">{{ s.label }}</div>
              </v-card-text>
            </v-card>
          </v-col>
        </v-row>

        <v-data-table :headers="headersHistorico" :items="historico" :loading="carregandoHistorico"
          items-per-page="20">
          <template #item.status="{ item }">
            <v-chip :color="corStatus(item.status)" size="small" :prepend-icon="iconeStatus(item.status)">
              {{ item.status }}
            </v-chip>
          </template>
          <template #item.tipoDisparo="{ item }">
            <v-chip size="x-small" :color="corTipoDisparo(item.tipoDisparo)">{{ item.tipoDisparo }}</v-chip>
          </template>
        </v-data-table>
      </v-window-item>

      <!-- Configuração -->
      <v-window-item value="config">
        <!-- Config catálogo (existente) -->
        <v-card max-width="700" class="mb-4">
          <v-card-title>Catálogo & Pedidos</v-card-title>
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
            <v-btn color="primary" @click="salvarConfig">Salvar Configuração do Catálogo</v-btn>
          </v-card-actions>
        </v-card>

        <!-- Config mensagens automáticas (nova) -->
        <v-card max-width="700" class="mb-4">
          <v-card-title>API de Mensagens Automáticas (Cloud API)</v-card-title>
          <v-card-text>
            <v-alert type="warning" variant="tonal" class="mb-4">
              <strong>Como configurar:</strong><br>
              1. Acesse <strong>developers.facebook.com</strong> → crie um App → adicione o produto WhatsApp<br>
              2. Em <em>API Setup</em>, copie o <strong>Phone Number ID</strong> e gere um <strong>System User Token</strong> permanente<br>
              3. O <strong>Business Account ID (WABA ID)</strong> está em WhatsApp → Configuration<br>
              4. Cole a URL do webhook abaixo no painel da Meta para receber confirmações de leitura
            </v-alert>

            <v-row>
              <v-col cols="12" sm="6">
                <v-text-field v-model="cfgMsg.phoneNumberId" label="Phone Number ID" density="compact" />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field v-model="cfgMsg.businessAccountId" label="Business Account ID (WABA ID)" density="compact" />
              </v-col>
              <v-col cols="12">
                <v-text-field v-model="cfgMsg.accessToken" label="Access Token (System User — permanente)"
                  :type="mostrarToken ? 'text' : 'password'" density="compact"
                  :append-inner-icon="mostrarToken ? 'mdi-eye-off' : 'mdi-eye'"
                  @click:append-inner="mostrarToken = !mostrarToken"
                  hint="Deixe em branco para manter o token atual" persistent-hint />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field v-model="cfgMsg.appId" label="App ID (Meta)" density="compact" />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field v-model="cfgMsg.webhookVerifyToken" label="Webhook Verify Token (crie um texto qualquer)" density="compact" />
              </v-col>
              <v-col cols="12">
                <v-text-field
                  :model-value="webhookUrl"
                  label="URL do Webhook (configure na Meta)"
                  readonly
                  append-inner-icon="mdi-content-copy"
                  @click:append-inner="copiarWebhookUrl"
                  density="compact" />
              </v-col>
              <v-col cols="12">
                <v-switch v-model="cfgMsg.ativo" color="success" label="Mensagens automáticas ativas" density="compact" hide-details />
              </v-col>
            </v-row>
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn color="primary" @click="salvarCfgMsg">Salvar Config. Mensagens</v-btn>
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

    <!-- Dialog Template WhatsApp -->
    <v-dialog v-model="dialogTemplate" max-width="540">
      <v-card>
        <v-card-title>{{ templateEditando ? 'Editar Template' : 'Novo Template' }}</v-card-title>
        <v-card-text>
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            O nome do template deve ser exatamente igual ao aprovado na Meta Business Manager.
          </v-alert>
          <v-text-field v-model="novoTemplate.nomeMeta" label="Nome do Template (Meta)" class="mb-2"
            hint="Ex: aniversario_cliente" persistent-hint />
          <v-select v-model="novoTemplate.tipoDisparo" :items="tiposDisparo" label="Tipo de Disparo" class="mb-2" />
          <v-select v-model="novoTemplate.idioma" label="Idioma"
            :items="['pt_BR','en_US','es_ES']" class="mb-2" />
          <v-textarea v-model="novoTemplate.exemploTexto" label="Exemplo do texto (para referência)"
            rows="3" hint="Preencha com o texto final que o cliente vai receber" persistent-hint class="mb-2" />
          <v-textarea v-model="novoTemplate.variaveisJson" label="Mapeamento de variáveis (JSON)"
            rows="3" hint='Ex: [{"posicao":1,"campo":"primeiro_nome"},{"posicao":2,"campo":"desconto"}]'
            persistent-hint />
          <div class="text-caption mt-2 text-medium-emphasis">
            Campos disponíveis: nome_cliente, primeiro_nome, telefone, data_aniversario, desconto,
            produto_nome, produto_preco, produto_preco_promo, data_validade, link_catalogo, nome_empresa
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="dialogTemplate = false">Cancelar</v-btn>
          <v-btn color="primary" @click="salvarTemplate">Salvar Template</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

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

// ─── Mensagens automáticas ─────────────────────────────────────────────────

const tiposDisparo = ['Aniversario', 'Promocao', 'Novidade', 'BemVindo', 'LembreteCobranca', 'Personalizado']

const cfgMsg = ref({
  phoneNumberId: '', accessToken: '', businessAccountId: '',
  webhookVerifyToken: '', appId: '',
  ativo: false, enviarAniversario: true, enviarPromocoes: true,
  enviarNovidades: false, horaDisparo: 8,
})
const mostrarToken = ref(false)

const webhookUrl = computed(() =>
  `${window.location.origin.replace('5173', '5131')}/api/whatsapp/webhook`
)

function copiarWebhookUrl() {
  navigator.clipboard.writeText(webhookUrl.value)
  notif.ok('URL do webhook copiada!')
}

async function carregarCfgMsg() {
  try {
    const { data } = await api.get('/whatsapp/mensagem/config', { params: { empresaId: auth.empresaId } })
    cfgMsg.value = { ...cfgMsg.value, ...data, accessToken: data.accessTokenMask ?? '' }
  } catch {}
}

async function salvarCfgMsg() {
  try {
    await api.put('/whatsapp/mensagem/config', cfgMsg.value, { params: { empresaId: auth.empresaId } })
    notif.ok('Configuração de mensagens salva!')
  } catch {
    notif.erro('Erro ao salvar configuração.')
  }
}

// Templates
const templates = ref<any[]>([])
const dialogTemplate = ref(false)
const templateEditando = ref<any>(null)
const importandoTemplates = ref(false)
const novoTemplate = ref<any>({
  nomeMeta: '', tipoDisparo: 'Aniversario', idioma: 'pt_BR',
  exemploTexto: '', variaveisJson: '',
})

async function carregarTemplates() {
  try {
    const { data } = await api.get('/whatsapp/mensagem/templates', { params: { empresaId: auth.empresaId } })
    templates.value = Array.isArray(data) ? data : []
  } catch {}
}

function abrirDialogTemplate(t: any) {
  templateEditando.value = t
  novoTemplate.value = t
    ? { nomeMeta: t.nomeMeta, tipoDisparo: t.tipoDisparo, idioma: t.idioma, exemploTexto: t.exemploTexto, variaveisJson: t.variaveisJson }
    : { nomeMeta: '', tipoDisparo: 'Aniversario', idioma: 'pt_BR', exemploTexto: '', variaveisJson: '' }
  dialogTemplate.value = true
}

async function salvarTemplate() {
  try {
    if (templateEditando.value) {
      await api.put(`/whatsapp/mensagem/templates/${templateEditando.value.id}`, novoTemplate.value)
    } else {
      await api.post('/whatsapp/mensagem/templates', novoTemplate.value, { params: { empresaId: auth.empresaId } })
    }
    notif.ok('Template salvo!')
    dialogTemplate.value = false
    await carregarTemplates()
  } catch {
    notif.erro('Erro ao salvar template.')
  }
}

async function importarTemplatesMeta() {
  importandoTemplates.value = true
  try {
    const { data } = await api.get('/whatsapp/mensagem/templates/meta', { params: { empresaId: auth.empresaId } })
    if (Array.isArray(data) && data.length > 0) {
      notif.ok(`${data.length} templates aprovados encontrados na Meta. Cadastre os que deseja usar.`)
    } else {
      notif.aviso('Nenhum template aprovado encontrado. Verifique o Business Account ID e o token.')
    }
  } catch {
    notif.erro('Erro ao buscar templates na Meta. Verifique a configuração.')
  } finally {
    importandoTemplates.value = false
  }
}

function corTipoDisparo(tipo: string) {
  const m: Record<string, string> = {
    Aniversario: 'success', Promocao: 'orange', Novidade: 'primary',
    BemVindo: 'teal', LembreteCobranca: 'red', Personalizado: 'grey',
  }
  return m[tipo] ?? 'grey'
}

function iconeTipoDisparo(tipo: string) {
  const m: Record<string, string> = {
    Aniversario: 'mdi-cake-variant', Promocao: 'mdi-tag-multiple',
    Novidade: 'mdi-newspaper-variant', BemVindo: 'mdi-hand-wave',
    LembreteCobranca: 'mdi-bell-ring', Personalizado: 'mdi-message-text',
  }
  return m[tipo] ?? 'mdi-message'
}

// Envio manual
const enviando = ref(false)
const envioManual = ref({
  telefone: '', nomeDestinatario: '', templateId: null as any,
  templateName: '', tipoDisparo: 'Personalizado', variaveisTexto: '',
})

async function enviarManual() {
  enviando.value = true
  try {
    const variaveis = envioManual.value.variaveisTexto
      .split('\n').map(v => v.trim()).filter(Boolean)
    await api.post('/whatsapp/mensagem/enviar', {
      empresaId:         auth.empresaId,
      telefone:          envioManual.value.telefone,
      nomeDestinatario:  envioManual.value.nomeDestinatario,
      templateName:      envioManual.value.templateName,
      tipoDisparo:       envioManual.value.tipoDisparo,
      variaveis,
    })
    notif.ok('Mensagem enviada com sucesso!')
    envioManual.value = { telefone: '', nomeDestinatario: '', templateId: null, templateName: '', tipoDisparo: 'Personalizado', variaveisTexto: '' }
    await carregarHistorico()
  } catch {
    notif.erro('Falha ao enviar mensagem. Verifique a configuração da API.')
  } finally {
    enviando.value = false
  }
}

// Histórico
const historico = ref<any[]>([])
const carregandoHistorico = ref(false)
const filtroHistorico = ref({ tipo: null as string | null, status: null as string | null })

const headersHistorico = [
  { title: 'Data', key: 'enviadoEm' },
  { title: 'Destinatário', key: 'nomeDestinatario' },
  { title: 'Telefone', key: 'telefone' },
  { title: 'Tipo', key: 'tipoDisparo' },
  { title: 'Template', key: 'templateName' },
  { title: 'Status', key: 'status' },
]

const resumoHistorico = computed(() => {
  const ct = (s: string) => historico.value.filter(h => h.status === s).length
  return [
    { label: 'Enviadas',   qtd: ct('Enviada'),  cor: 'primary' },
    { label: 'Entregues',  qtd: ct('Entregue'), cor: 'success' },
    { label: 'Lidas',      qtd: ct('Lida'),     cor: 'teal'    },
    { label: 'Falhas',     qtd: ct('Falhou'),   cor: 'error'   },
  ]
})

function corStatus(status: string) {
  const m: Record<string, string> = {
    Pendente: 'grey', Enviada: 'primary', Entregue: 'success', Lida: 'teal', Falhou: 'error'
  }
  return m[status] ?? 'grey'
}

function iconeStatus(status: string) {
  const m: Record<string, string> = {
    Pendente: 'mdi-clock', Enviada: 'mdi-check', Entregue: 'mdi-check-all',
    Lida: 'mdi-check-all', Falhou: 'mdi-close-circle',
  }
  return m[status] ?? 'mdi-help'
}

// Disparos de campanha
const disparandoPromocao = ref(false)
const disparandoNovidade  = ref(false)

async function dispararPromocao() {
  disparandoPromocao.value = true
  try {
    await api.post('/whatsapp/mensagem/disparar-promocao', null, { params: { empresaId: auth.empresaId } })
    notif.ok('Disparo de promoções enfileirado! As mensagens serão enviadas em instantes.')
    setTimeout(carregarHistorico, 3000)
  } catch {
    notif.erro('Erro ao disparar promoções. Verifique se o WhatsApp está configurado.')
  } finally {
    disparandoPromocao.value = false
  }
}

async function dispararNovidade() {
  disparandoNovidade.value = true
  try {
    await api.post('/whatsapp/mensagem/disparar-novidade', null, { params: { empresaId: auth.empresaId } })
    notif.ok('Disparo de novidade enfileirado! As mensagens serão enviadas em instantes.')
    setTimeout(carregarHistorico, 3000)
  } catch {
    notif.erro('Erro ao disparar novidade. Verifique se o WhatsApp está configurado.')
  } finally {
    disparandoNovidade.value = false
  }
}

async function carregarHistorico() {
  carregandoHistorico.value = true
  try {
    const { data } = await api.get('/whatsapp/mensagem/historico', {
      params: { empresaId: auth.empresaId, ...filtroHistorico.value }
    })
    historico.value = data?.itens ?? []
  } finally {
    carregandoHistorico.value = false
  }
}

onMounted(() => {
  listarCatalogo()
  listarPedidos()
  carregarCfgMsg()
  carregarTemplates()
  carregarHistorico()
})
</script>


