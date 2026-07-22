<template>
  <v-container fluid>
    <v-row align="center" class="mb-4">
      <v-col>
        <h2 class="text-h5 font-weight-bold">WhatsApp Business</h2>
        <div class="text-caption text-medium-emphasis">Catálogo, pedidos e mensagens automáticas</div>
      </v-col>
      <v-col cols="auto">
        <v-btn color="success" prepend-icon="mdi-content-copy" @click="copiarFeedUrl">
          Copiar URL do feed
        </v-btn>
      </v-col>
    </v-row>

    <v-tabs v-model="tab" class="mb-4">
      <v-tab value="conversas">
        Conversas
        <v-badge v-if="totalNaoLidas > 0" :content="totalNaoLidas" color="error" inline />
      </v-tab>
      <v-tab value="pedidos">Pedidos</v-tab>
      <v-tab value="mensagens">Mensagens Automáticas</v-tab>
      <v-tab value="templates">Templates</v-tab>
      <v-tab value="historico">Histórico</v-tab>
      <v-tab value="config">Configuração</v-tab>
    </v-tabs>

    <v-window v-model="tab">
      <!-- Caixa de entrada / conversas -->
      <v-window-item value="conversas">
        <v-row>
          <v-col cols="12" md="4">
            <v-card variant="outlined" rounded="lg">
              <v-toolbar density="compact" color="transparent">
                <v-toolbar-title class="text-body-1 font-weight-medium">Conversas</v-toolbar-title>
                <v-btn icon="mdi-refresh" size="small" variant="text" @click="carregarConversas" />
              </v-toolbar>
              <v-divider />
              <v-list v-if="conversas.length" density="compact" style="max-height:60vh;overflow:auto">
                <v-list-item v-for="c in conversas" :key="c.telefone"
                  :active="conversaAtiva?.telefone === c.telefone" @click="abrirConversa(c)">
                  <v-list-item-title class="font-weight-medium">
                    {{ c.nome || c.telefone }}
                  </v-list-item-title>
                  <v-list-item-subtitle class="text-truncate">{{ c.ultimaMensagem }}</v-list-item-subtitle>
                  <template #append>
                    <v-badge v-if="c.naoLidas > 0" :content="c.naoLidas" color="error" inline />
                  </template>
                </v-list-item>
              </v-list>
              <v-card-text v-else class="text-center text-medium-emphasis py-8">
                Nenhuma conversa ainda. Quando um cliente enviar mensagem, ela aparece aqui.
              </v-card-text>
            </v-card>
          </v-col>

          <v-col cols="12" md="8">
            <v-card variant="outlined" rounded="lg" v-if="conversaAtiva">
              <v-toolbar density="compact" color="transparent">
                <v-toolbar-title class="text-body-1 font-weight-medium">
                  {{ conversaAtiva.nome || conversaAtiva.telefone }}
                </v-toolbar-title>
              </v-toolbar>
              <v-divider />
              <div ref="threadEl" style="height:52vh;overflow:auto" class="pa-4 d-flex flex-column ga-2">
                <div v-for="m in mensagens" :key="m.id"
                  :class="['msg-bolha', m.direcao === 'Enviada' ? 'align-self-end' : 'align-self-start']">
                  <div class="text-body-2" style="white-space:pre-wrap">{{ m.texto }}</div>
                  <div class="text-caption text-medium-emphasis text-right">{{ fmtHora(m.dataHora) }}</div>
                </div>
              </div>
              <v-divider />
              <div class="pa-2 d-flex ga-2">
                <v-text-field v-model="respostaTexto" placeholder="Digite uma resposta…"
                  density="compact" hide-details variant="outlined" @keyup.enter="responder" />
                <v-btn color="success" icon="mdi-send" :loading="respondendo"
                  :disabled="!respostaTexto.trim()" @click="responder" />
              </div>
            </v-card>
            <v-card variant="outlined" rounded="lg" v-else class="d-flex align-center justify-center"
              style="height:60vh">
              <div class="text-medium-emphasis">Selecione uma conversa para ver as mensagens.</div>
            </v-card>
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
                <v-autocomplete v-model="promoSelecionada" :items="promocoes" item-title="nome" item-value="id"
                  label="Preencher de uma promoção (opcional)" prepend-inner-icon="mdi-tag" clearable
                  density="compact" class="mb-2" :loading="carregandoPromo"
                  hint="Preenche automaticamente a imagem e as variáveis (nome, desconto, produto, preços)"
                  persistent-hint @update:model-value="preencherDaPromocao" />
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
                      @update:model-value="t => { envioManual.templateName = t?.nomeMeta; envioManual.idioma = t?.idioma || 'pt_BR' }" />
                  </v-col>
                  <v-col cols="12" sm="6">
                    <v-select v-model="envioManual.tipoDisparo" label="Tipo"
                      :items="tiposDisparo" />
                  </v-col>
                  <v-col cols="12">
                    <v-text-field v-model="envioManual.headerImageUrl"
                      label="URL da imagem do cabeçalho (só templates de mídia)"
                      prepend-inner-icon="mdi-image" clearable density="compact"
                      hint="Deixe em branco para templates sem imagem. Ex.: https://sistema.ecogranel.com.br/uploads/produtos/xxx.jpg"
                      persistent-hint />
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
            <v-btn color="success" prepend-icon="mdi-send-check" @click="criarTemplatePromocao"
              :loading="criandoTemplate">
              Criar template de promoção (p/ análise)
            </v-btn>
            <v-btn color="primary" class="ml-2" prepend-icon="mdi-plus" @click="abrirDialogTemplate(null)">
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
              <v-card-text class="pt-0 d-flex align-center ga-2">
                <v-chip size="x-small" :color="corTipoDisparo(t.tipoDisparo)">{{ t.tipoDisparo }}</v-chip>
                <v-chip v-if="statusMeta[t.nomeMeta]" size="x-small" variant="flat"
                  :color="corStatusTpl(statusMeta[t.nomeMeta])">
                  {{ rotuloStatusTpl(statusMeta[t.nomeMeta]) }}
                </v-chip>
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
        <!-- Catálogo por feed (a Meta puxa sozinha) -->
        <v-card max-width="700" class="mb-4">
          <v-card-title>Catálogo (sincronização por feed)</v-card-title>
          <v-card-text>
            <v-alert type="success" variant="tonal" density="comfortable" class="mb-4">
              O catálogo é sincronizado <strong>automaticamente</strong>: a Meta baixa o feed abaixo
              por agendamento. <strong>Não precisa de token nem de permissão de catálogo.</strong>
            </v-alert>

            <v-text-field :model-value="feedUrl" label="URL do feed (formato Meta Commerce)" readonly
              density="compact" class="mb-1" prepend-inner-icon="mdi-rss">
              <template #append-inner>
                <v-btn icon="mdi-content-copy" size="small" variant="text" @click="copiarFeedUrl" title="Copiar URL" />
                <v-btn icon="mdi-open-in-new" size="small" variant="text" :href="feedUrl" target="_blank" title="Abrir feed" />
              </template>
            </v-text-field>

            <v-alert type="info" variant="tonal" density="compact" class="mb-4">
              <strong>Como conectar na Meta (uma vez só):</strong><br>
              1. <strong>Commerce Manager</strong> → seu catálogo → <em>Fontes de dados</em> →
              <em>Adicionar itens → Arquivo de dados → Usar URL agendada</em><br>
              2. Cole a URL acima · Moeda: <strong>BRL</strong> · Agendamento: <strong>Diário</strong> (ou de hora em hora)<br>
              3. A Meta baixa e importa. <em>Só entram produtos ativos, com preço e com foto.</em>
            </v-alert>

            <v-text-field v-model="config.numeroWhatsApp" label="Número do WhatsApp (para o link wa.me)"
              hint="Ex.: +5518999998888" persistent-hint density="compact" class="mb-1" />
            <v-text-field v-model="config.catalogId" label="Catalog ID (opcional, informativo)"
              density="compact" class="mt-3" />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn color="primary" @click="salvarConfig">Salvar</v-btn>
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

    <!-- Dialog: templates aprovados encontrados na Meta (cadastro em 1 clique) -->
    <v-dialog v-model="dialogMetaTemplates" max-width="720">
      <v-card>
        <v-card-title>Templates aprovados na Meta</v-card-title>
        <v-card-subtitle>
          Escolha o tipo de disparo e clique em Cadastrar para vincular ao sistema.
        </v-card-subtitle>
        <v-card-text>
          <v-list lines="two">
            <v-list-item v-for="mt in templatesMeta" :key="mt.name" class="px-0">
              <template #prepend>
                <v-icon :color="corTipoDisparo(mt._tipo)">{{ iconeTipoDisparo(mt._tipo) }}</v-icon>
              </template>
              <v-list-item-title class="font-weight-medium">
                {{ mt.name }}
                <v-chip size="x-small" class="ml-1" :color="corStatusTpl(mt.status)" variant="flat">
                  {{ rotuloStatusTpl(mt.status) }}
                </v-chip>
              </v-list-item-title>
              <v-list-item-subtitle>{{ mt.category }} · {{ mt.language }}</v-list-item-subtitle>
              <template #append>
                <div class="d-flex align-center ga-2">
                  <v-select v-if="mt.status === 'APPROVED'" v-model="mt._tipo" :items="tiposDisparo"
                    density="compact" hide-details variant="outlined" style="min-width: 170px"
                    :disabled="jaCadastrado(mt.name)" />
                  <v-btn v-if="jaCadastrado(mt.name)" size="small" color="success" variant="tonal"
                    prepend-icon="mdi-check" disabled>Cadastrado</v-btn>
                  <v-btn v-else-if="mt.status === 'APPROVED'" size="small" color="primary" prepend-icon="mdi-plus"
                    @click="cadastrarTemplateMeta(mt)">Cadastrar</v-btn>
                  <span v-else class="text-caption text-medium-emphasis">
                    {{ mt.status === 'PENDING' ? 'Aguardando aprovação' : 'Indisponível' }}
                  </span>
                </div>
              </template>
            </v-list-item>
          </v-list>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="dialogMetaTemplates = false">Fechar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </v-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import api from '@/composables/useApi'
import { useNotifStore } from '@/stores/notif'
import { useAuthStore } from '@/stores/auth'

const notif = useNotifStore()
const auth = useAuthStore()

const tab = ref('conversas')

// ─── Caixa de entrada (conversas) ───────────────────────────────
const conversas = ref<any[]>([])
const conversaAtiva = ref<any>(null)
const mensagens = ref<any[]>([])
const respostaTexto = ref('')
const respondendo = ref(false)
const totalNaoLidas = ref(0)
const threadEl = ref<HTMLElement | null>(null)

const fmtHora = (v: string) => {
  const d = new Date(v)
  return isNaN(d.getTime()) ? '' : d.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' })
}

async function carregarConversas() {
  try {
    const { data } = await api.get('/whatsapp/conversas', { params: { empresaId: auth.empresaId } })
    conversas.value = Array.isArray(data) ? data : []
  } catch { conversas.value = [] }
  carregarNaoLidas()
}

async function carregarNaoLidas() {
  try {
    const { data } = await api.get('/whatsapp/conversas/nao-lidas', { params: { empresaId: auth.empresaId } })
    totalNaoLidas.value = data?.total ?? 0
  } catch { /* silencioso */ }
}

async function abrirConversa(c: any) {
  conversaAtiva.value = c
  try {
    const { data } = await api.get(`/whatsapp/conversas/${c.telefone}/mensagens`, { params: { empresaId: auth.empresaId } })
    mensagens.value = Array.isArray(data) ? data : []
    c.naoLidas = 0
    carregarNaoLidas()
    await nextTick()
    if (threadEl.value) threadEl.value.scrollTop = threadEl.value.scrollHeight
  } catch { mensagens.value = [] }
}

async function responder() {
  const texto = respostaTexto.value.trim()
  if (!texto || !conversaAtiva.value) return
  respondendo.value = true
  try {
    await api.post(`/whatsapp/conversas/${conversaAtiva.value.telefone}/responder`,
      { empresaId: auth.empresaId, texto })
    respostaTexto.value = ''
    await abrirConversa(conversaAtiva.value)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Falha ao enviar a resposta.')
  } finally { respondendo.value = false }
}
const pedidos = ref<any[]>([])
const carregandoPedidos = ref(false)

const config = ref({
  catalogId: '', numeroWhatsApp: ''
})

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

// URL pública do feed do catálogo (a Meta baixa por agendamento).
const feedUrl = computed(() =>
  `${window.location.origin}/api/produtos/feed-catalogo?empresaId=${auth.empresaId}`
)
async function copiarFeedUrl() {
  try {
    await navigator.clipboard.writeText(feedUrl.value)
    notif.ok('URL do feed copiada! Cole no Commerce Manager → Fontes de dados.')
  } catch {
    notif.aviso('Copie manualmente a URL do feed exibida na tela.')
  }
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
    // Feed: só grava número (link wa.me) e Catalog ID informativo — não mexe no token.
    await api.put('/whatsapp/configuracao', {
      empresaId: auth.empresaId,
      catalogId: config.value.catalogId || null,
      numeroWhatsApp: config.value.numeroWhatsApp || null,
    })
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

// Status de cada template na Meta (nomeMeta → APPROVED/PENDING/REJECTED), para
// exibir o selo nos cards de "Templates Cadastrados".
const statusMeta = ref<Record<string, string>>({})

async function carregarTemplates() {
  try {
    const { data } = await api.get('/whatsapp/mensagem/templates', { params: { empresaId: auth.empresaId } })
    templates.value = Array.isArray(data) ? data : []
  } catch {}
  // Busca os status reais na Meta (silencioso — se não estiver configurado, ignora).
  try {
    const { data } = await api.get('/whatsapp/mensagem/templates/meta', { params: { empresaId: auth.empresaId } })
    const mapa: Record<string, string> = {}
    for (const t of (Array.isArray(data) ? data : [])) mapa[t.name] = t.status
    statusMeta.value = mapa
  } catch { statusMeta.value = {} }
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

const dialogMetaTemplates = ref(false)
const templatesMeta = ref<any[]>([])
const criandoTemplate = ref(false)

// Cria um template de promoção padrão na Meta e envia para análise.
async function criarTemplatePromocao() {
  criandoTemplate.value = true
  try {
    const { data } = await api.post('/whatsapp/mensagem/templates/criar-promocao', { empresaId: auth.empresaId })
    const img = data?.comImagem ? ' (com cabeçalho de imagem)' : ''
    notif.ok(`Template "${data?.nome}" enviado para análise da Meta${img}. Status: ${data?.status ?? 'PENDING'}. A aprovação costuma levar de minutos a algumas horas.`)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao criar o template na Meta.')
  } finally {
    criandoTemplate.value = false
  }
}

async function importarTemplatesMeta() {
  importandoTemplates.value = true
  try {
    const { data } = await api.get('/whatsapp/mensagem/templates/meta', { params: { empresaId: auth.empresaId } })
    if (Array.isArray(data) && data.length > 0) {
      // Prepara cada template com um tipo de disparo sugerido para cadastro em 1 clique.
      templatesMeta.value = data.map((t: any) => ({ ...t, _tipo: sugerirTipo(t.name, t.category) }))
      dialogMetaTemplates.value = true
    } else {
      notif.aviso('Conexão OK, mas nenhum template encontrado na Meta ainda. Crie um pelo botão "Criar template de promoção".')
    }
  } catch (e: any) {
    // Mostra o motivo real devolvido pela Meta (token inválido, WABA errado, permissão, etc.)
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao buscar templates na Meta. Verifique a configuração.')
  } finally {
    importandoTemplates.value = false
  }
}

// Já cadastrado localmente? (compara pelo nome exato da Meta)
function jaCadastrado(nomeMeta: string) {
  return templates.value.some(t => t.nomeMeta === nomeMeta)
}

// Palpite do tipo de disparo a partir do nome/categoria do template.
function sugerirTipo(nome = '', categoria = '') {
  const n = `${nome} ${categoria}`.toLowerCase()
  if (/anivers|birthday/.test(n)) return 'Aniversario'
  if (/promo|desconto|oferta|marketing/.test(n)) return 'Promocao'
  if (/novidade|lancamento|news/.test(n)) return 'Novidade'
  if (/bem.?vindo|welcome/.test(n)) return 'BemVindo'
  if (/cobranc|lembrete|pagamento|vencimento|utility/.test(n)) return 'LembreteCobranca'
  return 'Personalizado'
}

async function cadastrarTemplateMeta(mt: any) {
  try {
    await api.post('/whatsapp/mensagem/templates', {
      nomeMeta: mt.name,
      tipoDisparo: mt._tipo,
      idioma: mt.language || 'pt_BR',
      variaveisJson: '',
      exemploTexto: '',
    }, { params: { empresaId: auth.empresaId } })
    notif.ok(`Template "${mt.name}" cadastrado como ${mt._tipo}.`)
    await carregarTemplates()
  } catch {
    notif.erro('Erro ao cadastrar template.')
  }
}

function corStatusTpl(status: string) {
  return { APPROVED: 'success', PENDING: 'warning', REJECTED: 'error', PAUSED: 'grey' }[status] ?? 'grey'
}
function rotuloStatusTpl(status: string) {
  return { APPROVED: 'Aprovado', PENDING: 'Em análise', REJECTED: 'Recusado', PAUSED: 'Pausado' }[status] ?? status
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
  templateName: '', idioma: 'pt_BR', tipoDisparo: 'Personalizado',
  variaveisTexto: '', headerImageUrl: '',
})

// Auto-preenchimento do envio manual a partir de uma promoção.
const promocoes = ref<any[]>([])
const promoSelecionada = ref<string | null>(null)
const carregandoPromo = ref(false)

async function carregarPromocoes() {
  try {
    const { data } = await api.get('/promocoes', { params: { empresaId: auth.empresaId } })
    promocoes.value = Array.isArray(data) ? data : []
  } catch { promocoes.value = [] }
}

async function preencherDaPromocao(promoId: string | null) {
  if (!promoId) return
  carregandoPromo.value = true
  try {
    const { data } = await api.get(`/promocoes/${promoId}/mensagem`)
    // Imagem do cabeçalho: usa a arte (Feed) já gerada da promoção, se houver.
    envioManual.value.headerImageUrl = data.arteUrl ? `${window.location.origin}${data.arteUrl}` : ''
    // Variáveis na ordem do template padrão: {{1}} nome, {{2}} desconto, {{3}} produto, {{4}} de, {{5}} por.
    envioManual.value.variaveisTexto = [
      envioManual.value.nomeDestinatario || 'Cliente',
      data.descontoTxt || '',
      data.produtoNome || data.nomePromocao || '',
      data.precoDE || '',
      data.precoPOR || '',
    ].join('\n')
    envioManual.value.tipoDisparo = 'Promocao'
    if (!data.arteUrl)
      notif.aviso('Promoção sem arte gerada. Gere as artes em Promoções para incluir a imagem.')
  } catch {
    notif.erro('Não foi possível carregar os dados da promoção.')
  } finally {
    carregandoPromo.value = false
  }
}

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
      idioma:            envioManual.value.idioma,
      tipoDisparo:       envioManual.value.tipoDisparo,
      variaveis,
      headerImageUrl:    envioManual.value.headerImageUrl || null,
    })
    notif.ok('Mensagem enviada com sucesso!')
    envioManual.value = { telefone: '', nomeDestinatario: '', templateId: null, templateName: '', idioma: 'pt_BR', tipoDisparo: 'Personalizado', variaveisTexto: '', headerImageUrl: '' }
    await carregarHistorico()
  } catch (e: any) {
    // Mostra o motivo real da Meta (janela 24h, nº de variáveis, template pausado, etc.)
    const d = e?.response?.data
    notif.erro(d?.erro ? `${d.mensagem ?? 'Falha no envio'}: ${d.erro}`
      : (d?.mensagem ?? 'Falha ao enviar mensagem. Verifique a configuração da API.'))
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

async function carregarConfigCatalogo() {
  try {
    const { data } = await api.get('/whatsapp/configuracao', { params: { empresaId: auth.empresaId } })
    if (data) config.value = {
      catalogId: data.catalogId ?? '', numeroWhatsApp: data.numeroWhatsApp ?? '',
    }
  } catch { /* silencioso */ }
}

onMounted(() => {
  carregarConversas()
  listarPedidos()
  carregarCfgMsg()
  carregarConfigCatalogo()
  carregarTemplates()
  carregarHistorico()
  carregarPromocoes()
})
</script>

<style scoped>
.msg-bolha {
  max-width: 75%;
  padding: 8px 12px;
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.05);
}
.msg-bolha.align-self-end {
  background: rgba(76, 175, 80, 0.18);
}
</style>


