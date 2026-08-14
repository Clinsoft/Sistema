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
      <v-tab v-if="!ehAtendente" value="mensagens">Mensagens Automáticas</v-tab>
      <v-tab v-if="!ehAtendente" value="templates">Templates</v-tab>
      <v-tab v-if="!ehAtendente" value="historico">Histórico</v-tab>
      <v-tab v-if="!ehAtendente" value="config">Configuração</v-tab>
    </v-tabs>

    <v-window v-model="tab">
      <!-- Caixa de entrada / conversas -->
      <v-window-item value="conversas">
        <v-row no-gutters style="height:70vh" class="border rounded-lg overflow-hidden">
          <!-- Lista de conversas -->
          <v-col cols="12" md="4" class="d-flex flex-column border-e" style="height:100%">
            <div class="pa-2 d-flex ga-2">
              <v-text-field v-model="buscaConversa" placeholder="Buscar conversa…" density="compact"
                hide-details variant="solo-filled" flat prepend-inner-icon="mdi-magnify" clearable
                @update:model-value="carregarConversas" />
              <v-btn icon="mdi-message-plus-outline" color="success" variant="tonal"
                title="Nova conversa (do cadastro de clientes)" @click="abrirNovaConversa" />
            </div>
            <v-divider />
            <div style="flex:1;overflow:auto">
              <v-list v-if="conversas.length" density="compact" class="py-0">
                <v-list-item v-for="c in conversas" :key="c.telefone"
                  :active="conversaAtiva?.telefone === c.telefone" @click="abrirConversa(c)">
                  <template #prepend>
                    <v-avatar :color="corAvatar(c.telefone)" size="40">
                      <span class="text-white">{{ inicial(c.nome || c.telefone) }}</span>
                    </v-avatar>
                  </template>
                  <v-list-item-title class="font-weight-medium">{{ c.nome || c.telefone }}</v-list-item-title>
                  <v-list-item-subtitle class="text-truncate">{{ c.ultimaMensagem }}</v-list-item-subtitle>
                  <template #append>
                    <div class="d-flex flex-column align-end" style="gap:4px">
                      <span class="text-caption text-medium-emphasis">{{ fmtHoraCurta(c.ultimaData) }}</span>
                      <v-badge v-if="c.naoLidas > 0" :content="c.naoLidas" color="success" inline />
                    </div>
                  </template>
                </v-list-item>
              </v-list>
              <div v-else class="text-center text-medium-emphasis pa-8">
                Nenhuma conversa ainda.
              </div>
            </div>
          </v-col>

          <!-- Thread -->
          <v-col cols="12" md="8" class="d-flex flex-column" style="height:100%">
            <template v-if="conversaAtiva">
              <div class="pa-3 d-flex align-center ga-3 border-b">
                <v-avatar :color="corAvatar(conversaAtiva.telefone)" size="36">
                  <span class="text-white">{{ inicial(conversaAtiva.nome || conversaAtiva.telefone) }}</span>
                </v-avatar>
                <div>
                  <div class="font-weight-medium">{{ conversaAtiva.nome || conversaAtiva.telefone }}</div>
                  <div class="text-caption text-medium-emphasis">+{{ conversaAtiva.telefone }}</div>
                </div>
              </div>
              <v-divider />

              <div ref="threadEl" style="flex:1;overflow:auto;background:rgba(0,0,0,.02)"
                class="pa-4 d-flex flex-column ga-1">
                <template v-for="(m, i) in mensagens" :key="m.id">
                  <div v-if="mostrarData(i)" class="text-center my-2">
                    <span class="text-caption px-3 py-1 rounded-pill"
                      style="background:rgba(0,0,0,.06)">{{ separadorData(m.dataHora) }}</span>
                  </div>
                  <div :class="['msg-bolha', m.direcao === 'Enviada' ? 'align-self-end' : 'align-self-start']">
                    <!-- Mídia -->
                    <img v-if="m.tipo === 'image' && m.midiaUrl" :src="m.midiaUrl"
                      style="max-width:240px;border-radius:8px;cursor:pointer" @click="abrirImagem(m.midiaUrl)" />
                    <audio v-else-if="m.tipo === 'audio' && m.midiaUrl" :src="m.midiaUrl" controls
                      style="max-width:240px" />
                    <a v-else-if="m.midiaUrl" :href="m.midiaUrl" target="_blank"
                      class="d-flex align-center ga-2 text-decoration-none">
                      <v-icon color="error">mdi-file-document-outline</v-icon>
                      <span class="text-body-2">{{ m.midiaNome || 'documento' }}</span>
                    </a>
                    <div v-if="m.texto" class="text-body-2" style="white-space:pre-wrap">{{ m.texto }}</div>
                    <div class="d-flex align-center justify-end ga-1 mt-1">
                      <span class="text-caption text-medium-emphasis">{{ fmtHoraCurta(m.dataHora) }}</span>
                      <v-icon v-if="m.direcao === 'Enviada'" size="14" :color="corTique(m.status)">
                        {{ m.status === 'Falhou' ? 'mdi-alert-circle' : m.status === 'Enviada' ? 'mdi-check' : 'mdi-check-all' }}
                      </v-icon>
                    </div>
                  </div>
                </template>
              </div>

              <v-divider />
              <!-- Janela de 24h fechada → só template -->
              <div v-if="conversaAtiva.dentroDe24h === false" class="pa-2">
                <v-alert type="warning" variant="tonal" density="compact" class="mb-2">
                  Passou de 24h desde a última mensagem do cliente. Só é possível enviar um <b>template aprovado</b>.
                </v-alert>
                <div class="d-flex ga-2">
                  <v-select v-model="templateResposta" :items="templates" item-title="nomeMeta" item-value="nomeMeta"
                    label="Template" density="compact" hide-details variant="outlined" />
                  <v-btn color="primary" :disabled="!templateResposta" :loading="respondendo"
                    @click="responderTemplate">Enviar template</v-btn>
                </div>
              </div>
              <!-- Dentro das 24h → texto/emoji/mídia -->
              <div v-else class="pa-2 d-flex align-center ga-1">
                <v-menu :close-on-content-click="false" location="top">
                  <template #activator="{ props }">
                    <v-btn v-bind="props" icon="mdi-emoticon-outline" variant="text" size="small" />
                  </template>
                  <v-card max-width="260"><v-card-text class="d-flex flex-wrap ga-1 pa-2">
                    <span v-for="e in emojis" :key="e" style="cursor:pointer;font-size:22px"
                      @click="respostaTexto += e">{{ e }}</span>
                  </v-card-text></v-card>
                </v-menu>
                <v-btn icon="mdi-paperclip" variant="text" size="small" @click="anexoInput?.click()" />
                <input ref="anexoInput" type="file" hidden
                  accept="image/*,audio/*,video/*,application/pdf" @change="enviarAnexo" />
                <v-menu location="top">
                  <template #activator="{ props }">
                    <v-btn v-bind="props" icon="mdi-storefront-outline" variant="text" size="small"
                      title="Catálogo" :loading="respondendo" />
                  </template>
                  <v-list density="compact">
                    <v-list-item prepend-icon="mdi-cart-outline" title="Catálogo no WhatsApp"
                      subtitle="Mostra os produtos dentro do chat" @click="enviarCatalogoNativo" />
                    <v-list-item prepend-icon="mdi-link-variant" title="Link do site"
                      subtitle="Envia o link ecogranel.com.br/produtos" @click="enviarLinkCatalogo" />
                  </v-list>
                </v-menu>
                <template v-if="!gravando">
                  <v-text-field v-model="respostaTexto" placeholder="Digite uma mensagem…"
                    density="compact" hide-details variant="solo-filled" flat @keyup.enter="responder" />
                  <v-btn v-if="respostaTexto.trim()" color="success" icon="mdi-send" :loading="respondendo"
                    @click="responder" />
                  <v-btn v-else color="success" icon="mdi-microphone" variant="text"
                    title="Gravar áudio" @click="iniciarGravacao" />
                </template>
                <template v-else>
                  <div class="flex-grow-1 d-flex align-center ga-2 px-2 text-error">
                    <v-icon color="error" class="pulse">mdi-record-circle</v-icon>
                    <span>Gravando… {{ tempoGravacao }}s</span>
                  </div>
                  <v-btn icon="mdi-close" variant="text" size="small" @click="cancelarGravacao" />
                  <v-btn color="success" icon="mdi-send" :loading="respondendo" @click="pararEnviarGravacao" />
                </template>
              </div>
            </template>
            <div v-else class="d-flex align-center justify-center text-medium-emphasis" style="height:100%">
              Selecione uma conversa para ver as mensagens.
            </div>
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
              <v-btn size="small" :disabled="!proximoStatusPedido(item)"
                :color="proximoStatusPedido(item) ? 'primary' : undefined"
                @click="avancarStatus(item)">{{ rotuloAvancar(item) }}</v-btn>
              <v-btn v-if="item.status !== 'Cancelado' && item.status !== 'Entregue'"
                size="small" icon="mdi-close-circle-outline" color="error"
                title="Cancelar pedido" @click="cancelarPedido(item)" />
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
            <v-btn color="success" prepend-icon="mdi-send-check" @click="abrirCustom">
              Criar template (p/ análise)
            </v-btn>
            <v-btn color="success" variant="tonal" class="ml-2" prepend-icon="mdi-tag-plus"
              @click="criarTemplatePromocao" :loading="criandoTemplate">
              Promoção rápida
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
                  <v-btn icon="mdi-delete-outline" size="small" variant="text" color="error"
                    @click="excluirTemplate(t)" />
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
        <!-- Vitrine / Loja Online (e-commerce próprio) -->
        <v-card max-width="700" class="mb-4">
          <v-card-title><v-icon icon="mdi-storefront" class="mr-2" />Vitrine / Loja Online</v-card-title>
          <v-card-text>
            <v-alert type="success" variant="tonal" density="comfortable" class="mb-4">
              Sua <strong>loja online própria</strong>: o cliente navega os produtos, monta o carrinho
              e envia o pedido. Os pedidos caem na aba <strong>Pedidos</strong> aqui do sistema.
            </v-alert>
            <div class="d-flex flex-column flex-sm-row gap-4 align-center">
              <div class="text-center">
                <canvas ref="qrVitrine" class="qr-vitrine" />
                <div class="text-caption text-medium-emphasis">Aponte a câmera / imprima na loja</div>
              </div>
              <div class="flex-grow-1" style="width:100%">
                <v-text-field :model-value="linkVitrine" label="Link da loja online" readonly
                  density="compact" prepend-inner-icon="mdi-link-variant">
                  <template #append-inner>
                    <v-btn icon="mdi-content-copy" size="small" variant="text" @click="copiarLinkVitrine" title="Copiar link" />
                    <v-btn icon="mdi-open-in-new" size="small" variant="text" :href="linkVitrine" target="_blank" title="Abrir loja" />
                  </template>
                </v-text-field>
                <v-btn color="primary" variant="tonal" size="small" @click="baixarQrVitrine">
                  <v-icon start>mdi-download</v-icon>Baixar QR code
                </v-btn>
              </div>
            </div>
          </v-card-text>
        </v-card>

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
              <v-col cols="12">
                <v-divider class="mb-2" />
                <v-switch v-model="cfgMsg.iaAtendimentoAtiva" color="deep-purple" density="compact" hide-details
                  label="🤖 Atendente por IA (responde e monta pedido automático)" />
                <div class="text-caption text-medium-emphasis ml-2">
                  Quando o cliente manda mensagem, a IA responde usando o catálogo/preços da loja e vai montando o pedido.
                  A conversa fica no inbox e o atendente pode assumir a qualquer momento.
                </div>
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
            <b>Campos disponíveis</b> (no <code>campo</code> do JSON):<br>
            <b>Cliente:</b> nome_cliente, primeiro_nome, telefone, data_aniversario<br>
            <b>Empresa:</b> nome_empresa, link_catalogo<br>
            <b>Promoção:</b> produto_nome, produto_preco, produto_preco_promo, desconto, data_validade
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

    <!-- Criar template personalizado e enviar para análise da Meta -->
    <v-dialog v-model="dialogCustom" max-width="600">
      <v-card rounded="xl">
        <v-card-title>Criar template (enviar para análise)</v-card-title>
        <v-card-subtitle>A Meta analisa e aprova; depois é só importar e usar.</v-card-subtitle>
        <v-card-text>
          <v-row dense>
            <v-col cols="12" sm="7">
              <v-text-field v-model="custom.nome" label="Nome do template" density="compact"
                hint="minúsculas e _ (ex.: comecar_conversa)" persistent-hint />
            </v-col>
            <v-col cols="12" sm="5">
              <v-select v-model="custom.categoria" :items="['UTILITY','MARKETING']" label="Categoria"
                density="compact" hint="Saudação/atendimento = UTILITY" persistent-hint />
            </v-col>
          </v-row>
          <v-textarea v-model="custom.corpo" label="Corpo da mensagem" rows="4" class="mt-2"
            hint="Use {{1}}, {{2}}… para variáveis. Ex.: Olá {{1}}, aqui é a EcoGranel!" persistent-hint />
          <v-textarea v-model="custom.exemplos" label="Exemplos das variáveis (um por linha)" rows="2"
            class="mt-3" hint="Um valor por {{n}}, na ordem. Ex.: João" persistent-hint />
          <v-switch v-model="custom.comImagem" color="success" density="compact" hide-details
            label="Incluir cabeçalho de imagem (usa uma arte já gerada)" class="mt-2" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="dialogCustom = false">Cancelar</v-btn>
          <v-btn color="success" :loading="criandoCustom"
            :disabled="!custom.nome || !custom.corpo" @click="enviarCustom">Enviar para análise</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Nova conversa a partir do cadastro de clientes -->
    <v-dialog v-model="dialogNovaConversa" max-width="520">
      <v-card rounded="xl">
        <v-card-title>Nova conversa</v-card-title>
        <v-card-subtitle>Escolha um cliente cadastrado (ou digite o número).</v-card-subtitle>
        <v-card-text>
          <v-autocomplete v-model="novaConv.cliente" :items="clientesBusca" return-object
            :item-title="c => `${c.nome} — ${c.celular || c.telefone || 's/ telefone'}`" item-value="id"
            label="Cliente" density="compact" clearable no-filter
            :loading="buscandoCliente" @update:search="buscarClientes"
            @update:model-value="onClienteSelecionado" />
          <v-row dense class="mt-1">
            <v-col cols="7">
              <v-text-field v-model="novaConv.telefone" label="Telefone (com DDD)" density="compact"
                placeholder="5518999998888" prepend-inner-icon="mdi-phone" />
            </v-col>
            <v-col cols="5">
              <v-text-field v-model="novaConv.nome" label="Nome" density="compact" />
            </v-col>
          </v-row>
          <v-alert type="info" variant="tonal" density="compact" class="mb-2">
            Conversas iniciadas pela empresa exigem um <b>template aprovado</b> (regra do WhatsApp).
            O cliente respondendo, abre a janela de 24h para texto livre.
          </v-alert>
          <v-select v-model="novaConv.template" :items="templates" item-title="nomeMeta" item-value="nomeMeta"
            label="Template para iniciar" density="compact" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn @click="dialogNovaConversa = false">Cancelar</v-btn>
          <v-btn color="success" :loading="iniciandoConversa"
            :disabled="!novaConv.telefone || !novaConv.template" @click="iniciarConversa">Iniciar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </v-container>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted, nextTick } from 'vue'
import api from '@/composables/useApi'
import { useNotifStore } from '@/stores/notif'
import { useAuthStore } from '@/stores/auth'

const notif = useNotifStore()
const auth = useAuthStore()
// Atendente vê apenas Conversas e Pedidos; o resto (config/templates) é do gestor.
const ehAtendente = computed(() => auth.usuario?.role === 'Atendente')

const tab = ref('conversas')
// Renderiza o QR da vitrine quando a aba Configuração é aberta (canvas só existe então).
watch(tab, v => { if (v === 'config') renderQrVitrine() })

// ─── Caixa de entrada (conversas) ───────────────────────────────
const conversas = ref<any[]>([])
const conversaAtiva = ref<any>(null)
const mensagens = ref<any[]>([])
const respostaTexto = ref('')
const respondendo = ref(false)
const totalNaoLidas = ref(0)
const threadEl = ref<HTMLElement | null>(null)
const buscaConversa = ref('')
const templateResposta = ref<string | null>(null)
const anexoInput = ref<HTMLInputElement | null>(null)
const emojis = ['😀','😁','😂','🥰','😊','😉','😍','😘','👍','🙏','👏','🎉','❤️','🔥','✅','⚠️','🛒','📦','💰','🌿','☕','🍫','😢','😅','🤝','👋']
let pollTimer: any = null

const inicial = (s: string) => (s || '?').trim().charAt(0).toUpperCase()
function corAvatar(tel: string) {
  const cores = ['#43a047','#1e88e5','#8e24aa','#e53935','#fb8c00','#00897b','#3949ab','#6d4c41']
  let h = 0; for (const ch of (tel || '')) h = (h * 31 + ch.charCodeAt(0)) % cores.length
  return cores[h]
}
function corTique(status?: string) {
  return status === 'Lida' ? 'info' : status === 'Falhou' ? 'error' : 'grey'
}
// As datas vêm do backend em UTC (sem o sufixo 'Z'); trata como UTC para exibir no
// fuso local correto (o navegador converte na hora de formatar).
function paraData(v: string): Date {
  if (!v) return new Date(NaN)
  const s = /[zZ]|[+-]\d\d:?\d\d$/.test(v) ? v : v + 'Z'
  return new Date(s)
}
const fmtHoraCurta = (v: string) => {
  const d = paraData(v)
  return isNaN(d.getTime()) ? '' : d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}
function separadorData(v: string) {
  const d = paraData(v); const hoje = new Date(); const ontem = new Date(); ontem.setDate(hoje.getDate() - 1)
  if (d.toDateString() === hoje.toDateString()) return 'Hoje'
  if (d.toDateString() === ontem.toDateString()) return 'Ontem'
  return d.toLocaleDateString('pt-BR')
}
function mostrarData(i: number) {
  if (i === 0) return true
  return paraData(mensagens.value[i].dataHora).toDateString() !== paraData(mensagens.value[i - 1].dataHora).toDateString()
}
function abrirImagem(url: string) { window.open(url, '_blank') }

async function carregarConversas() {
  try {
    const { data } = await api.get('/whatsapp/conversas', {
      params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined, busca: buscaConversa.value || undefined },
    })
    conversas.value = Array.isArray(data) ? data : []
    // Mantém sincronizado o dentroDe24h da conversa aberta.
    if (conversaAtiva.value) {
      const atual = conversas.value.find(c => c.telefone === conversaAtiva.value.telefone)
      if (atual) conversaAtiva.value.dentroDe24h = atual.dentroDe24h
    }
  } catch { conversas.value = [] }
  carregarNaoLidas()
}

async function carregarNaoLidas() {
  try {
    const { data } = await api.get('/whatsapp/conversas/nao-lidas', { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
    totalNaoLidas.value = data?.total ?? 0
  } catch { /* silencioso */ }
}

async function abrirConversa(c: any, manterScroll = false) {
  conversaAtiva.value = c
  try {
    const { data } = await api.get(`/whatsapp/conversas/${c.telefone}/mensagens`, { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
    const antes = mensagens.value.length
    mensagens.value = Array.isArray(data) ? data : []
    c.naoLidas = 0
    carregarNaoLidas()
    if (!manterScroll || mensagens.value.length !== antes) {
      await nextTick()
      if (threadEl.value) threadEl.value.scrollTop = threadEl.value.scrollHeight
    }
  } catch { mensagens.value = [] }
}

async function responder() {
  const texto = respostaTexto.value.trim()
  if (!texto || !conversaAtiva.value) return
  respondendo.value = true
  try {
    await api.post(`/whatsapp/conversas/${conversaAtiva.value.telefone}/responder`,
      { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined, texto })
    respostaTexto.value = ''
    await abrirConversa(conversaAtiva.value)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Falha ao enviar a resposta.')
  } finally { respondendo.value = false }
}

async function enviarCatalogoNativo() {
  if (!conversaAtiva.value) return
  respondendo.value = true
  try {
    await api.post(`/whatsapp/conversas/${conversaAtiva.value.telefone}/enviar-catalogo`,
      { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined, texto: '' })
    await abrirConversa(conversaAtiva.value)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Falha ao enviar o catálogo.')
  } finally { respondendo.value = false }
}

// ── Nova conversa (agenda telefônica dos clientes) ──────────────
const dialogNovaConversa = ref(false)
const clientesBusca = ref<any[]>([])
const buscandoCliente = ref(false)
const iniciandoConversa = ref(false)
const novaConv = ref<{ cliente: any; telefone: string; nome: string; template: string | null }>(
  { cliente: null, telefone: '', nome: '', template: null })

function abrirNovaConversa() {
  novaConv.value = { cliente: null, telefone: '', nome: '', template: templates.value[0]?.nomeMeta ?? null }
  dialogNovaConversa.value = true
  carregarClientes()   // pré-carrega a lista (agenda) ao abrir
}

async function carregarClientes(termo?: string) {
  buscandoCliente.value = true
  try {
    const { data } = await api.get('/clientes', {
      params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined, termo: termo || undefined, tamanhoPagina: 100 },
    })
    const lista = Array.isArray(data) ? data : (data?.itens ?? [])
    // Prioriza quem tem telefone/celular; mantém os demais no fim (dá pra digitar o número).
    clientesBusca.value = [...lista].sort((a: any, b: any) =>
      (b.celular || b.telefone ? 1 : 0) - (a.celular || a.telefone ? 1 : 0))
  } catch { clientesBusca.value = [] }
  finally { buscandoCliente.value = false }
}

let clienteTimer: any
function buscarClientes(termo: string) {
  clearTimeout(clienteTimer)
  clienteTimer = setTimeout(() => carregarClientes(termo), 300)
}

function onClienteSelecionado(c: any) {
  if (!c) return
  novaConv.value.nome = c.nome
  novaConv.value.telefone = (c.celular || c.telefone || '').replace(/\D/g, '')
}

async function iniciarConversa() {
  const tel = (novaConv.value.telefone || '').replace(/\D/g, '')
  if (!tel || !novaConv.value.template) return
  iniciandoConversa.value = true
  try {
    const tpl = templates.value.find(t => t.nomeMeta === novaConv.value.template)
    await api.post(`/whatsapp/conversas/${tel}/responder-template`, {
      empresaId: auth.empresaId,
      localEstoqueId: auth.lojaAtualId || undefined,
      templateName: novaConv.value.template,
      idioma: tpl?.idioma || 'pt_BR',
      nome: novaConv.value.nome || null,
    })
    dialogNovaConversa.value = false
    await carregarConversas()
    const c = conversas.value.find(x => x.telefone === tel)
      ?? { telefone: tel, nome: novaConv.value.nome, dentroDe24h: false }
    await abrirConversa(c)
    tab.value = 'conversas'
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Falha ao iniciar a conversa.')
  } finally { iniciandoConversa.value = false }
}

async function enviarLinkCatalogo() {
  if (!conversaAtiva.value) return
  const texto = '🌿 Confira nosso catálogo de produtos naturais EcoGranel:\n'
    + 'https://ecogranel.com.br/produtos\n\nQualquer dúvida é só chamar! 🛒'
  respondendo.value = true
  try {
    await api.post(`/whatsapp/conversas/${conversaAtiva.value.telefone}/responder`,
      { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined, texto })
    await abrirConversa(conversaAtiva.value)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Falha ao enviar o link do catálogo.')
  } finally { respondendo.value = false }
}

async function responderTemplate() {
  if (!templateResposta.value || !conversaAtiva.value) return
  respondendo.value = true
  try {
    const tpl = templates.value.find(t => t.nomeMeta === templateResposta.value)
    await api.post(`/whatsapp/conversas/${conversaAtiva.value.telefone}/responder-template`,
      { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined, templateName: templateResposta.value, idioma: tpl?.idioma || 'pt_BR' })
    templateResposta.value = null
    await abrirConversa(conversaAtiva.value)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Falha ao enviar o template.')
  } finally { respondendo.value = false }
}

async function enviarAnexo(ev: Event) {
  const input = ev.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !conversaAtiva.value) return
  respondendo.value = true
  try {
    const fd = new FormData()
    fd.append('empresaId', auth.empresaId as any)
    if (auth.lojaAtualId) fd.append('localEstoqueId', auth.lojaAtualId)
    fd.append('arquivo', file)
    if (respostaTexto.value.trim()) fd.append('legenda', respostaTexto.value.trim())
    await api.post(`/whatsapp/conversas/${conversaAtiva.value.telefone}/responder-midia`, fd,
      { headers: { 'Content-Type': 'multipart/form-data' }, timeout: 120000 })
    respostaTexto.value = ''
    await abrirConversa(conversaAtiva.value)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Falha ao enviar o anexo.')
  } finally { respondendo.value = false; if (input) input.value = '' }
}

// Gravação de áudio (mensagem de voz) via microfone.
const gravando = ref(false)
const tempoGravacao = ref(0)
let mediaRecorder: MediaRecorder | null = null
let chunks: Blob[] = []
let gravTimer: any = null

async function iniciarGravacao() {
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    chunks = []
    mediaRecorder = new MediaRecorder(stream)
    mediaRecorder.ondataavailable = e => { if (e.data.size > 0) chunks.push(e.data) }
    mediaRecorder.onstop = () => stream.getTracks().forEach(t => t.stop())
    mediaRecorder.start()
    gravando.value = true
    tempoGravacao.value = 0
    gravTimer = setInterval(() => { tempoGravacao.value++ }, 1000)
  } catch {
    notif.erro('Não foi possível acessar o microfone. Permita o acesso no navegador.')
  }
}

function encerrarGravacao() {
  if (gravTimer) { clearInterval(gravTimer); gravTimer = null }
  gravando.value = false
}

function cancelarGravacao() {
  if (mediaRecorder && mediaRecorder.state !== 'inactive') mediaRecorder.stop()
  mediaRecorder = null; chunks = []
  encerrarGravacao()
}

async function pararEnviarGravacao() {
  if (!mediaRecorder || !conversaAtiva.value) return
  const rec = mediaRecorder
  const mime = rec.mimeType || 'audio/ogg'
  await new Promise<void>(resolve => { rec.onstop = () => resolve(); rec.stop() })
  encerrarGravacao()
  const blob = new Blob(chunks, { type: mime })
  chunks = []; mediaRecorder = null
  if (blob.size === 0) return
  respondendo.value = true
  try {
    const ext = mime.includes('ogg') ? '.ogg' : mime.includes('webm') ? '.webm' : '.m4a'
    const fd = new FormData()
    fd.append('empresaId', auth.empresaId as any)
    if (auth.lojaAtualId) fd.append('localEstoqueId', auth.lojaAtualId)
    fd.append('arquivo', new File([blob], `audio${ext}`, { type: mime }))
    await api.post(`/whatsapp/conversas/${conversaAtiva.value.telefone}/responder-midia`, fd,
      { headers: { 'Content-Type': 'multipart/form-data' }, timeout: 120000 })
    await abrirConversa(conversaAtiva.value)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Falha ao enviar o áudio.')
  } finally { respondendo.value = false }
}

// Auto-atualização: a cada 6s recarrega conversas e, se houver uma aberta, as mensagens.
function iniciarPolling() {
  pararPolling()
  pollTimer = setInterval(async () => {
    if (tab.value !== 'conversas') return
    await carregarConversas()
    if (conversaAtiva.value) await abrirConversa(conversaAtiva.value, true)
  }, 6000)
}
function pararPolling() { if (pollTimer) { clearInterval(pollTimer); pollTimer = null } }
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

// Vitrine / loja online própria (rota pública /loja/:empresaId — hash history).
const linkVitrine = computed(() =>
  `${window.location.origin}/#/loja/${auth.empresaId}`
)
const qrVitrine = ref<HTMLCanvasElement | null>(null)
async function renderQrVitrine() {
  await nextTick()
  if (!qrVitrine.value) return
  try {
    const QRCode = (await import('qrcode')).default
    await QRCode.toCanvas(qrVitrine.value, linkVitrine.value, { width: 150, margin: 1 })
  } catch { /* qr opcional */ }
}
async function copiarLinkVitrine() {
  try {
    await navigator.clipboard.writeText(linkVitrine.value)
    notif.ok('Link da loja online copiado!')
  } catch {
    notif.aviso('Copie manualmente o link exibido na tela.')
  }
}
function baixarQrVitrine() {
  if (!qrVitrine.value) return
  const a = document.createElement('a')
  a.href = qrVitrine.value.toDataURL('image/png')
  a.download = 'loja-online-qrcode.png'
  a.click()
}

// URL pública do feed do catálogo (a Meta baixa por agendamento).
const feedUrl = computed(() =>
  `${window.location.origin}/api/produtos/feed-catalogo?empresaId=${auth.empresaId}`
  + (auth.lojaAtualId ? `&localEstoqueId=${auth.lojaAtualId}` : '')
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
    const { data } = await api.get('/whatsapp/pedidos', { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
    pedidos.value = Array.isArray(data) ? data : []
  } finally {
    carregandoPedidos.value = false
  }
}

// Próximo status do fluxo. Retirada vai para "ProntoParaRetirada"; entrega para "Enviado".
// Cobre também "Confirmado" (status que o atendente de IA usa) — antes ficava de fora e o
// botão não fazia nada. Retorna null quando o pedido já está no fim (Entregue/Cancelado).
function proximoStatusPedido(pedido: any): string | null {
  const retirada = pedido?.tipoEntrega === 'Retirada'
  const fluxo: Record<string, string> = {
    Novo: 'Confirmado',
    Confirmado: 'EmSeparacao',
    EmSeparacao: retirada ? 'ProntoParaRetirada' : 'Enviado',
    ProntoParaRetirada: 'Entregue',
    Enviado: 'Entregue',
  }
  return fluxo[pedido?.status] ?? null
}
const rotuloStatusPedido: Record<string, string> = {
  Confirmado: 'Confirmar', EmSeparacao: 'Separar', ProntoParaRetirada: 'Pronto p/ retirada',
  Enviado: 'Enviar', Entregue: 'Marcar entregue',
}
function rotuloAvancar(pedido: any): string {
  const p = proximoStatusPedido(pedido)
  return p ? (rotuloStatusPedido[p] ?? 'Avançar') : 'Concluído'
}

async function avancarStatus(pedido: any) {
  const novoStatus = proximoStatusPedido(pedido)
  if (!novoStatus) return
  try {
    await api.patch(`/whatsapp/pedidos/${pedido.id}/status`, { status: novoStatus })
    await listarPedidos()
    notif.ok(`Pedido ${pedido.numero ?? ''} → ${novoStatus}`)
  } catch {
    notif.erro('Erro ao atualizar status.')
  }
}

async function cancelarPedido(pedido: any) {
  if (!confirm(`Cancelar o pedido ${pedido.numero ?? ''}?`)) return
  try {
    await api.patch(`/whatsapp/pedidos/${pedido.id}/status`, { status: 'Cancelado' })
    await listarPedidos()
    notif.ok(`Pedido ${pedido.numero ?? ''} cancelado.`)
  } catch {
    notif.erro('Erro ao cancelar o pedido.')
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
      localEstoqueId: auth.lojaAtualId || null,
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
  enviarNovidades: false, horaDisparo: 8, iaAtendimentoAtiva: false,
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
    const { data } = await api.get('/whatsapp/mensagem/config', { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
    cfgMsg.value = { ...cfgMsg.value, ...data, accessToken: data.accessTokenMask ?? '' }
  } catch {}
}

async function salvarCfgMsg() {
  try {
    await api.put('/whatsapp/mensagem/config', cfgMsg.value, { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
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
    const { data } = await api.get('/whatsapp/mensagem/templates', { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
    templates.value = Array.isArray(data) ? data : []
  } catch {}
  // Busca os status reais na Meta (silencioso — se não estiver configurado, ignora).
  try {
    const { data } = await api.get('/whatsapp/mensagem/templates/meta', { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
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

// Criar template personalizado e enviar para análise.
const dialogCustom = ref(false)
const criandoCustom = ref(false)
const custom = ref({ nome: '', categoria: 'UTILITY', corpo: '', exemplos: '', comImagem: false })

function abrirCustom() {
  custom.value = { nome: '', categoria: 'UTILITY', corpo: '', exemplos: '', comImagem: false }
  dialogCustom.value = true
}

async function enviarCustom() {
  const exemplos = custom.value.exemplos.split('\n').map(v => v.trim()).filter(Boolean)
  // Nº de variáveis {{n}} no corpo precisa bater com o nº de exemplos (a Meta exige).
  const nVars = new Set((custom.value.corpo.match(/\{\{\s*\d+\s*\}\}/g) || [])).size
  if (nVars > exemplos.length) {
    notif.erro(`O corpo tem ${nVars} variável(is) {{n}}, mas você deu ${exemplos.length} exemplo(s). `
      + 'Informe um exemplo por variável (uma por linha) — senão a Meta recusa por INVALID_FORMAT.')
    return
  }
  criandoCustom.value = true
  try {
    const { data } = await api.post('/whatsapp/mensagem/templates/criar-custom', {
      empresaId: auth.empresaId,
      nome: custom.value.nome,
      corpo: custom.value.corpo,
      categoria: custom.value.categoria,
      exemplos,
      comImagem: custom.value.comImagem,
    })
    dialogCustom.value = false
    notif.ok(`Template "${data?.nome}" enviado para análise (${data?.status ?? 'PENDING'}). Acompanhe o status pelo selo no card.`)
    carregarTemplates()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Erro ao enviar o template para análise.')
  } finally { criandoCustom.value = false }
}

async function excluirTemplate(t: any) {
  if (!confirm(`Excluir o template "${t.nomeMeta}" da lista? (não afeta a Meta)`)) return
  try {
    await api.delete(`/whatsapp/mensagem/templates/${t.id}`)
    notif.ok('Template removido da lista.')
    await carregarTemplates()
  } catch {
    notif.erro('Erro ao excluir o template.')
  }
}

async function importarTemplatesMeta() {
  importandoTemplates.value = true
  try {
    const { data } = await api.get('/whatsapp/mensagem/templates/meta', { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
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
    await api.post('/whatsapp/mensagem/disparar-promocao', null, { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
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
    await api.post('/whatsapp/mensagem/disparar-novidade', null, { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
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
      params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined, ...filtroHistorico.value }
    })
    historico.value = data?.itens ?? []
  } finally {
    carregandoHistorico.value = false
  }
}

async function carregarConfigCatalogo() {
  try {
    const { data } = await api.get('/whatsapp/configuracao', { params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId || undefined } })
    if (data) config.value = {
      catalogId: data.catalogId ?? '', numeroWhatsApp: data.numeroWhatsApp ?? '',
    }
  } catch { /* silencioso */ }
}

onMounted(() => {
  carregarConversas()
  iniciarPolling()
  listarPedidos()
  carregarCfgMsg()
  carregarConfigCatalogo()
  carregarTemplates()
  carregarHistorico()
  carregarPromocoes()
})
onUnmounted(pararPolling)
</script>

<style scoped>
.qr-vitrine {
  width: 150px;
  height: 150px;
  border: 1px solid rgba(0, 0, 0, 0.08);
  border-radius: 8px;
}
.gap-4 { gap: 16px; }
.msg-bolha {
  max-width: 75%;
  padding: 8px 12px;
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.05);
}
.msg-bolha.align-self-end {
  background: rgba(76, 175, 80, 0.18);
}
.pulse { animation: pulse 1s infinite; }
@keyframes pulse { 0%,100% { opacity: 1 } 50% { opacity: .35 } }
</style>


