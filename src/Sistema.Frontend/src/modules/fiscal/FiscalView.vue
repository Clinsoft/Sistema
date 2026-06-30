<template>
  <div>
    <div class="d-flex align-center mb-3">
      <div class="text-h6 font-weight-bold flex-grow-1">Documentos Fiscais</div>
    </div>

    <v-tabs v-model="aba" bg-color="transparent" class="mb-4">
      <v-tab value="emitidas" prepend-icon="mdi-file-send-outline">NF-e Emitidas</v-tab>
      <v-tab value="recebidas" prepend-icon="mdi-file-download-outline">
        NF-e Recebidas
        <v-chip v-if="resumoRecebidas.semManifestacao > 0" color="warning" size="x-small" class="ml-2">
          {{ resumoRecebidas.semManifestacao }} pendente(s)
        </v-chip>
      </v-tab>
      <v-tab value="entradas" prepend-icon="mdi-file-import-outline">Importar XML</v-tab>
    </v-tabs>

    <!-- ─────────────── ABA: EMITIDAS ─────────────── -->
    <div v-if="aba === 'emitidas'">
      <v-row class="mb-3">
        <v-col v-for="c in cardsEmitidas" :key="c.label" cols="6" sm="3">
          <v-card rounded="xl" elevation="1">
            <v-card-text class="pa-3 d-flex align-center">
              <v-avatar :color="c.cor" size="40" class="mr-3">
                <v-icon :icon="c.icon" color="white" size="20" />
              </v-avatar>
              <div>
                <div class="text-h6 font-weight-bold">{{ c.valor }}</div>
                <div class="text-caption text-medium-emphasis">{{ c.label }}</div>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
        <v-row dense>
          <v-col cols="12" sm="3">
            <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f }" />
          </v-col>
          <v-col cols="12" sm="3">
            <v-text-field v-model="filtros.inicio" label="Início" type="date"
              variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="12" sm="3">
            <v-text-field v-model="filtros.fim" label="Fim" type="date"
              variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="12" sm="3">
            <v-select v-model="filtros.status" label="Status"
              :items="['Todos','Autorizada','Cancelada','Rejeitada']"
              variant="outlined" density="compact" hide-details />
          </v-col>
        </v-row>
        <div class="d-flex justify-end mt-2">
          <v-btn color="primary" variant="tonal" rounded="lg" :loading="carregando" @click="carregarEmitidas">
            Buscar
          </v-btn>
        </div>
      </v-card>

      <v-card rounded="xl" elevation="1">
        <v-data-table :headers="headersEmitidas" :items="notas" :loading="carregando" density="compact" hover>
          <template #item.modelo="{ item }">
            <v-chip size="small" :color="item.modelo===55?'primary':'secondary'" variant="tonal">
              {{ item.modelo===55?'NF-e':'NFC-e' }}
            </v-chip>
          </template>
          <template #item.status="{ item }">
            <v-chip size="small" :color="corStatusEmitida(item.status)" variant="tonal">{{ item.status }}</v-chip>
          </template>
          <template #item.totalNota="{ item }">R$ {{ fmt(item.totalNota) }}</template>
          <template #item.dataEmissao="{ item }">{{ fmtData(item.dataEmissao) }}</template>
          <template #item.actions="{ item }">
            <v-btn v-if="item.status==='EmDigitacao'"
              icon="mdi-send-clock-outline" size="x-small" variant="text" color="primary"
              @click="transmitir(item)" title="Transmitir" />
            <v-btn v-if="item.status==='Autorizada'"
              icon="mdi-download-outline" size="x-small" variant="text" color="info"
              @click="baixarXml(item)" title="Baixar XML" />
            <v-btn v-if="item.status==='Autorizada'"
              icon="mdi-file-pdf-box" size="x-small" variant="text" color="error"
              @click="baixarDanfe(item.id)" title="Baixar DANFE" />
            <v-btn v-if="item.status==='Autorizada'"
              icon="mdi-close-circle-outline" size="x-small" variant="text" color="error"
              @click="cancelar(item)" title="Cancelar" />
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- ─────────────── ABA: RECEBIDAS ─────────────── -->
    <div v-if="aba === 'recebidas'">

      <!-- Botão Habilitar -->
      <v-card rounded="xl" elevation="1" class="mb-3 pa-3 d-flex align-center justify-space-between flex-wrap gap-3">
        <div>
          <div class="text-subtitle-1 font-weight-medium">Monitoramento de NF-e</div>
          <div class="text-caption text-medium-emphasis">Receba automaticamente as NF-e emitidas pelos seus fornecedores</div>
        </div>
        <v-btn color="indigo" variant="flat" rounded="lg" :loading="habilitando"
          @click="dialogHabilitar = true" prepend-icon="mdi-link-plus">
          Habilitar Busca de NF-e
        </v-btn>
      </v-card>

      <!-- Cards resumo -->
      <v-row class="mb-3">
        <v-col cols="6" sm="3">
          <v-card rounded="xl" elevation="1">
            <v-card-text class="pa-3 d-flex align-center">
              <v-avatar color="primary" size="40" class="mr-3">
                <v-icon icon="mdi-file-multiple-outline" color="white" size="20" />
              </v-avatar>
              <div>
                <div class="text-h6 font-weight-bold">{{ resumoRecebidas.total }}</div>
                <div class="text-caption text-medium-emphasis">NF-e recebidas</div>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="6" sm="3">
          <v-card rounded="xl" elevation="1">
            <v-card-text class="pa-3 d-flex align-center">
              <v-avatar color="warning" size="40" class="mr-3">
                <v-icon icon="mdi-clock-alert-outline" color="white" size="20" />
              </v-avatar>
              <div>
                <div class="text-h6 font-weight-bold">{{ resumoRecebidas.semManifestacao }}</div>
                <div class="text-caption text-medium-emphasis">Sem manifestação</div>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="6" sm="3">
          <v-card rounded="xl" elevation="1">
            <v-card-text class="pa-3 d-flex align-center">
              <v-avatar color="success" size="40" class="mr-3">
                <v-icon icon="mdi-check-circle-outline" color="white" size="20" />
              </v-avatar>
              <div>
                <div class="text-h6 font-weight-bold">{{ resumoRecebidas.confirmadas }}</div>
                <div class="text-caption text-medium-emphasis">Confirmadas</div>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="6" sm="3">
          <v-card rounded="xl" elevation="1">
            <v-card-text class="pa-3 d-flex align-center">
              <v-avatar color="info" size="40" class="mr-3">
                <v-icon icon="mdi-currency-usd" color="white" size="20" />
              </v-avatar>
              <div>
                <div class="text-h6 font-weight-bold">R$ {{ fmt(resumoRecebidas.valorTotal) }}</div>
                <div class="text-caption text-medium-emphasis">Valor total recebido</div>
              </div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <!-- Barra de ações + filtros -->
      <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
        <v-row dense align="center">
          <v-col cols="12" sm="3">
            <v-text-field v-model="filtrosRec.emitente" label="Emitente (nome ou CNPJ)"
              variant="outlined" density="compact" hide-details clearable
              prepend-inner-icon="mdi-magnify" />
          </v-col>
          <v-col cols="12" sm="2">
            <FiltroMes @selecionar="(i, f) => { filtrosRec.dataInicio = i; filtrosRec.dataFim = f }" />
          </v-col>
          <v-col cols="12" sm="2">
            <v-text-field v-model="filtrosRec.dataInicio" label="De" type="date"
              variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="12" sm="2">
            <v-text-field v-model="filtrosRec.dataFim" label="Até" type="date"
              variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="12" sm="3">
            <v-select v-model="filtrosRec.manifestacao" label="Manifestação"
              :items="opcoesManifestacaoFiltro" variant="outlined" density="compact"
              hide-details clearable />
          </v-col>
          <v-col cols="12" sm="2" class="d-flex gap-2 align-center">
            <v-btn color="primary" variant="tonal" rounded="lg" size="small" :loading="carregandoRec" @click="carregarRecebidas">
              Buscar
            </v-btn>
            <v-btn color="success" variant="tonal" rounded="lg" size="small" icon :loading="consultandoSefaz"
              @click="consultarSefaz" title="Buscar novas NF-e na SEFAZ">
              <v-icon>mdi-cloud-download-outline</v-icon>
            </v-btn>
          </v-col>
        </v-row>

        <v-alert v-if="alertaCertificado" type="warning" variant="tonal" density="compact" class="mt-3">
          <strong>Certificado A1 não configurado.</strong>
          A consulta à SEFAZ requer o certificado digital da empresa.
          <router-link to="/configuracoes" class="ml-1">Configurar agora</router-link>
        </v-alert>
      </v-card>

      <!-- Tabela NF-e recebidas -->
      <v-card rounded="xl" elevation="1">
        <v-data-table :headers="headersRecebidas" :items="notasRecebidas"
          :loading="carregandoRec" density="comfortable" hover
          no-data-text="Nenhuma NF-e recebida. Clique no botão ☁ para consultar a SEFAZ.">
          <template #item.emitente="{ item }">
            <div class="text-body-2 font-weight-medium">{{ item.emitenteNome }}</div>
            <div class="text-caption text-medium-emphasis">{{ fmtCnpj(item.emitenteCnpj) }}</div>
          </template>
          <template #item.numero="{ item }">
            <div class="text-caption">
              Série {{ item.serie }} Nº {{ item.numero }}
            </div>
            <div class="text-caption text-medium-emphasis" style="font-size:10px">
              {{ item.chaveAcesso?.substring(0, 20) }}…
            </div>
          </template>
          <template #item.valorTotal="{ item }">
            <span class="font-weight-medium">R$ {{ fmt(item.valorTotal) }}</span>
          </template>
          <template #item.dataEmissao="{ item }">{{ fmtData(item.dataEmissao) }}</template>
          <template #item.situacao="{ item }">
            <v-chip size="small" :color="item.situacao==='Autorizada'?'success':'error'" variant="tonal">
              {{ item.situacao }}
            </v-chip>
          </template>
          <template #item.manifestacao="{ item }">
            <v-chip v-if="item.manifestacao" size="small"
              :color="corManifestacao(item.manifestacao)" variant="tonal">
              <v-icon start size="12">{{ iconeManifestacao(item.manifestacao) }}</v-icon>
              {{ labelManifestacao(item.manifestacao) }}
            </v-chip>
            <v-chip v-else size="small" color="warning" variant="tonal">
              <v-icon start size="12">mdi-clock-outline</v-icon>
              Pendente
            </v-chip>
          </template>
          <template #item.acoes="{ item }">
            <v-menu>
              <template #activator="{ props }">
                <v-btn v-bind="props" icon="mdi-dots-vertical" size="small" variant="text" />
              </template>
              <v-list density="compact" min-width="240">
                <v-list-subheader>Manifestar</v-list-subheader>
                <v-list-item prepend-icon="mdi-eye-outline" title="Ciência da Operação"
                  subtitle="Ciente, aguardando NF-e completa"
                  @click="manifestar(item, 'CienciaOperacao')" />
                <v-list-item prepend-icon="mdi-check-circle-outline" title="Confirmação da Operação"
                  subtitle="Operação ocorreu conforme descrito"
                  class="text-success"
                  @click="manifestar(item, 'ConfirmacaoOperacao')" />
                <v-list-item prepend-icon="mdi-help-circle-outline" title="Desconhecimento"
                  subtitle="Não reconheço esta operação"
                  class="text-warning"
                  @click="manifestar(item, 'DesconhecimentoOperacao')" />
                <v-list-item prepend-icon="mdi-cancel" title="Operação Não Realizada"
                  subtitle="Operação não ocorreu"
                  class="text-error"
                  @click="abrirNaoRealizada(item)" />
                <v-divider />
                <v-list-item v-if="item.temXml"
                  prepend-icon="mdi-download-outline" title="Baixar XML"
                  @click="baixarXmlRecebida(item)" />
                <v-divider />
                <v-list-item
                  prepend-icon="mdi-file-import-outline" title="Escriturar Entrada"
                  subtitle="Lançar no estoque e financeiro"
                  class="text-primary font-weight-medium"
                  @click="escriturarEntrada(item)" />
              </v-list>
            </v-menu>
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- ─────────────── ABA: ENTRADAS ESCRITURADAS ─────────────── -->
    <div v-if="aba === 'entradas'">
      <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
        <v-row dense align="center">
          <v-col cols="12" sm="2">
            <FiltroMes @selecionar="(i, f) => { filtrosEnt.dataInicio = i; filtrosEnt.dataFim = f }" />
          </v-col>
          <v-col cols="12" sm="2">
            <v-text-field v-model="filtrosEnt.dataInicio" label="De" type="date"
              variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="12" sm="2">
            <v-text-field v-model="filtrosEnt.dataFim" label="Até" type="date"
              variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="12" sm="3">
            <v-select v-model="filtrosEnt.status" label="Status" clearable
              :items="[{title:'Em Edição',value:'EmEdicao'},{title:'Processada',value:'Processada'},{title:'Estornada',value:'Estornada'}]"
              variant="outlined" density="compact" hide-details />
          </v-col>
          <v-col cols="12" sm="3" class="d-flex gap-2">
            <v-btn color="primary" variant="tonal" rounded="lg" :loading="carregandoEnt" @click="carregarEntradas">
              Buscar
            </v-btn>
            <v-btn color="success" rounded="lg" prepend-icon="mdi-file-import-outline"
              @click="dlgImportarXml = true">
              Importar XML
            </v-btn>
          </v-col>
        </v-row>
      </v-card>

      <v-card rounded="xl" elevation="1">
        <v-data-table :headers="headersEntradas" :items="entradas" :loading="carregandoEnt"
          density="comfortable" hover
          no-data-text="Nenhuma entrada escriturada. Use 'Importar XML' ou Escriturar via NF-e Recebidas.">
          <template #item.emitente="{ item }">
            <div class="text-body-2 font-weight-medium">{{ item.emitenteNome }}</div>
            <div class="text-caption text-medium-emphasis">{{ fmtCnpj(item.emitenteCnpj) }}</div>
          </template>
          <template #item.chave="{ item }">
            <span class="text-caption" style="font-size:10px">{{ item.chaveAcesso?.substring(0, 22) }}…</span>
          </template>
          <template #item.dataEmissao="{ item }">{{ fmtData(item.dataEmissao) }}</template>
          <template #item.dataEntrada="{ item }">{{ fmtData(item.dataEntrada) }}</template>
          <template #item.valorTotal="{ item }">
            <span class="font-weight-medium">R$ {{ fmt(item.valorTotal) }}</span>
          </template>
          <template #item.status="{ item }">
            <v-chip size="small" :color="corStatusEntrada(item.status)" variant="tonal">
              {{ labelStatusEntrada(item.status) }}
            </v-chip>
          </template>
          <template #item.totalItens="{ item }">
            <span class="text-body-2">{{ item.totalItens }}</span>
          </template>
          <template #item.acoes="{ item }">
            <v-btn icon="mdi-arrow-right-circle-outline" size="small" variant="text" color="primary"
              @click="abrirEntrada(item)" title="Abrir escrituração" />
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- ══════════════════════════════════════════════════════
         DIALOG: EMISSÃO DE NF-e (Stepper 4 etapas)
    ══════════════════════════════════════════════════════ -->
    <v-dialog v-model="dlgNovaNFe" fullscreen persistent transition="dialog-bottom-transition">
      <v-card>
        <v-toolbar color="primary" density="compact">
          <v-btn icon="mdi-close" @click="fecharNovaNFe" />
          <v-toolbar-title>Emissão de NF-e</v-toolbar-title>
          <v-spacer />
          <v-chip v-if="nfeEmitida" color="white" text-color="primary" variant="flat" class="mr-2">
            <v-icon start>mdi-check-circle</v-icon>
            NF-e {{ nfeEmitida.numero }} Emitida
          </v-chip>
        </v-toolbar>

        <v-container class="pa-4" style="max-width:900px">
          <v-stepper v-model="nfeStep"
            :items="['Destinatário', 'Itens', 'Impostos e Pagamento', 'Revisão e Emissão']"
            alt-labels flat>

            <!-- ── Step 1: Destinatário ── -->
            <template #item.1>
              <v-card flat class="mt-2">
                <v-row dense>
                  <v-col cols="12" sm="4">
                    <v-select v-model="nfeForm.destinatario.tipo" label="Tipo *"
                      :items="[{title:'Pessoa Física',value:'PF'},{title:'Pessoa Jurídica',value:'PJ'},{title:'Consumidor Final',value:'CF'}]"
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" sm="4">
                    <v-text-field v-model="nfeForm.destinatario.cpfCnpj"
                      :label="nfeForm.destinatario.tipo==='PJ'?'CNPJ *':'CPF *'"
                      variant="outlined" density="compact"
                      :hint="nfeForm.destinatario.tipo==='PJ'?'14 dígitos':'11 dígitos'"
                      persistent-hint />
                  </v-col>
                  <v-col cols="12" sm="4">
                    <v-text-field v-model="nfeForm.destinatario.nome" label="Nome / Razão Social *"
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="12" sm="4">
                    <v-text-field v-model="nfeForm.destinatario.email" label="E-mail"
                      variant="outlined" density="compact" type="email" />
                  </v-col>
                  <v-col cols="12" sm="8">
                    <v-select v-model="nfeForm.naturezaOperacao" label="Natureza da Operação *"
                      :items="naturezasOperacao" item-title="title" item-value="value"
                      variant="outlined" density="compact" />
                  </v-col>
                </v-row>
                <div class="d-flex justify-end mt-4">
                  <v-btn color="primary" rounded="lg" @click="nfeStep++"
                    :disabled="!nfeForm.destinatario.nome || !nfeForm.destinatario.cpfCnpj">
                    Próximo <v-icon end>mdi-chevron-right</v-icon>
                  </v-btn>
                </div>
              </v-card>
            </template>

            <!-- ── Step 2: Itens ── -->
            <template #item.2>
              <v-card flat class="mt-2">
                <div class="d-flex align-center mb-3">
                  <div class="text-body-1 font-weight-medium flex-grow-1">Itens da Nota</div>
                  <v-btn color="primary" variant="tonal" rounded="lg" prepend-icon="mdi-plus"
                    @click="abrirAddItem">Adicionar Item</v-btn>
                </div>

                <v-data-table :headers="headersItensNFe" :items="nfeForm.itens" density="compact"
                  no-data-text="Nenhum item adicionado" hide-default-footer items-per-page="-1">
                  <template #item.valorUnitario="{ item }">R$ {{ fmt(item.valorUnitario) }}</template>
                  <template #item.total="{ item }">
                    <strong>R$ {{ fmt(item.quantidade * item.valorUnitario - (item.valorDesconto ?? 0)) }}</strong>
                  </template>
                  <template #item.acoes="{ item, index }">
                    <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error"
                      @click="nfeForm.itens.splice(index, 1)" />
                  </template>
                </v-data-table>

                <v-card v-if="nfeForm.itens.length" rounded="lg" color="grey-lighten-4" class="mt-3 pa-3">
                  <div class="d-flex justify-end">
                    <span class="text-body-1 font-weight-bold">
                      Total: <span class="text-success">R$ {{ fmt(totalItensNFe) }}</span>
                    </span>
                  </div>
                </v-card>

                <div class="d-flex justify-space-between mt-4">
                  <v-btn variant="text" @click="nfeStep--">
                    <v-icon start>mdi-chevron-left</v-icon> Voltar
                  </v-btn>
                  <v-btn color="primary" rounded="lg" @click="nfeStep++"
                    :disabled="nfeForm.itens.length === 0">
                    Próximo <v-icon end>mdi-chevron-right</v-icon>
                  </v-btn>
                </div>
              </v-card>
            </template>

            <!-- ── Step 3: Impostos e Pagamento ── -->
            <template #item.3>
              <v-card flat class="mt-2">
                <v-alert type="info" variant="tonal" density="compact" class="mb-4">
                  <strong>Regime Tributário:</strong> Simples Nacional — os impostos (ICMS/PIS/COFINS)
                  são calculados automaticamente pelo servidor conforme as alíquotas configuradas.
                </v-alert>

                <v-select v-model="nfeCfopPadrao" label="CFOP padrão para todos os itens"
                  :items="cfops" item-title="title" item-value="value"
                  variant="outlined" density="compact" class="mb-4"
                  hint="Aplica o mesmo CFOP em todos os itens" persistent-hint
                  @update:model-value="aplicarCfopPadrao" />

                <div class="text-body-2 font-weight-medium mb-2">Formas de Pagamento</div>
                <v-row v-for="(pag, i) in nfeForm.pagamentos" :key="i" dense align="center">
                  <v-col cols="6">
                    <v-select v-model="pag.tipo" label="Forma"
                      :items="formasPagamento" item-title="title" item-value="value"
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="5">
                    <v-text-field v-model.number="pag.valor" label="Valor (R$)"
                      type="number" step="0.01" prefix="R$"
                      variant="outlined" density="compact" />
                  </v-col>
                  <v-col cols="1">
                    <v-btn icon="mdi-delete-outline" variant="text" color="error" size="small"
                      @click="nfeForm.pagamentos.splice(i, 1)"
                      :disabled="nfeForm.pagamentos.length <= 1" />
                  </v-col>
                </v-row>
                <v-btn prepend-icon="mdi-plus" variant="tonal" size="small" class="mt-2"
                  @click="nfeForm.pagamentos.push({ tipo: 'Dinheiro', valor: 0 })">
                  Adicionar forma de pagamento
                </v-btn>

                <v-card rounded="lg" color="grey-lighten-4" class="mt-4 pa-3">
                  <div class="d-flex justify-space-between text-body-2">
                    <span>Total da nota:</span>
                    <span class="font-weight-bold">R$ {{ fmt(totalItensNFe) }}</span>
                  </div>
                  <div class="d-flex justify-space-between text-body-2 mt-1">
                    <span>Total pago:</span>
                    <span :class="totalPagoNFe >= totalItensNFe ? 'text-success font-weight-bold' : 'text-error font-weight-bold'">
                      R$ {{ fmt(totalPagoNFe) }}
                    </span>
                  </div>
                  <div v-if="totalPagoNFe < totalItensNFe" class="text-caption text-error mt-1">
                    Faltam R$ {{ fmt(totalItensNFe - totalPagoNFe) }} para cobrir o total da nota.
                  </div>
                </v-card>

                <div class="d-flex justify-space-between mt-4">
                  <v-btn variant="text" @click="nfeStep--">
                    <v-icon start>mdi-chevron-left</v-icon> Voltar
                  </v-btn>
                  <v-btn color="primary" rounded="lg" @click="nfeStep++">
                    Próximo <v-icon end>mdi-chevron-right</v-icon>
                  </v-btn>
                </div>
              </v-card>
            </template>

            <!-- ── Step 4: Revisão e Emissão ── -->
            <template #item.4>
              <v-card flat class="mt-2">
                <!-- Resultado pós-emissão -->
                <template v-if="nfeEmitida">
                  <v-alert type="success" variant="tonal" class="mb-4">
                    <div class="text-body-1 font-weight-bold mb-2">NF-e Emitida com Sucesso!</div>
                    <div class="text-body-2">Número: <strong>{{ nfeEmitida.numero }}</strong></div>
                    <div class="text-body-2 mt-1">Status: <strong>{{ nfeEmitida.status }}</strong></div>
                    <div class="text-body-2 mt-1">Protocolo: <strong>{{ nfeEmitida.protocolo ?? '—' }}</strong></div>
                    <div class="text-body-2 mt-1 text-caption" style="word-break:break-all">
                      Chave: {{ nfeEmitida.chaveAcesso }}
                    </div>
                    <div v-if="nfeEmitida.mensagem" class="text-caption mt-1 text-medium-emphasis">
                      {{ nfeEmitida.mensagem }}
                    </div>
                  </v-alert>
                  <div class="d-flex gap-3 flex-wrap">
                    <v-btn color="error" variant="tonal" rounded="lg" prepend-icon="mdi-file-pdf-box"
                      @click="baixarDanfe(nfeEmitida.id)">Baixar DANFE</v-btn>
                    <v-btn color="info" variant="tonal" rounded="lg" prepend-icon="mdi-xml"
                      @click="baixarXmlNfe(nfeEmitida.id)">Baixar XML</v-btn>
                    <v-btn variant="text" @click="fecharNovaNFe">Fechar</v-btn>
                  </div>
                </template>

                <!-- Revisão antes de emitir -->
                <template v-else>
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-card rounded="lg" variant="outlined" class="pa-3 mb-3">
                        <div class="text-caption text-medium-emphasis font-weight-medium mb-2">DESTINATÁRIO</div>
                        <div class="text-body-2 font-weight-bold">{{ nfeForm.destinatario.nome }}</div>
                        <div class="text-caption">{{ nfeForm.destinatario.cpfCnpj }}</div>
                        <div class="text-caption">{{ nfeForm.destinatario.email }}</div>
                        <div class="mt-1">
                          <v-chip size="x-small" color="primary" variant="tonal">
                            {{ naturezasOperacao.find(n => n.value === nfeForm.naturezaOperacao)?.title }}
                          </v-chip>
                        </div>
                      </v-card>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-card rounded="lg" variant="outlined" class="pa-3 mb-3">
                        <div class="text-caption text-medium-emphasis font-weight-medium mb-2">RESUMO FINANCEIRO</div>
                        <div class="text-body-2">Itens: <strong>{{ nfeForm.itens.length }}</strong></div>
                        <div class="text-body-2">
                          Total nota: <strong class="text-success">R$ {{ fmt(totalItensNFe) }}</strong>
                        </div>
                        <div class="text-body-2">Total pago: <strong>R$ {{ fmt(totalPagoNFe) }}</strong></div>
                        <div class="text-caption mt-1">
                          <span v-for="pag in nfeForm.pagamentos" :key="pag.tipo" class="mr-2">
                            {{ formasPagamento.find(f => f.value === pag.tipo)?.title }}: R$ {{ fmt(pag.valor) }}
                          </span>
                        </div>
                      </v-card>
                    </v-col>
                  </v-row>

                  <v-card rounded="lg" variant="outlined" class="pa-3 mb-3">
                    <div class="text-caption text-medium-emphasis font-weight-medium mb-2">
                      ITENS ({{ nfeForm.itens.length }})
                    </div>
                    <div v-for="(item, i) in nfeForm.itens" :key="i"
                      class="text-body-2 d-flex justify-space-between py-1"
                      :class="i < nfeForm.itens.length - 1 ? 'border-b' : ''">
                      <span>{{ item.quantidade }}x {{ item.descricao }}
                        <span class="text-medium-emphasis text-caption">(CFOP {{ item.cfop }}, NCM {{ item.ncm }})</span>
                      </span>
                      <span class="font-weight-medium ml-4">
                        R$ {{ fmt(item.quantidade * item.valorUnitario - (item.valorDesconto ?? 0)) }}
                      </span>
                    </div>
                  </v-card>

                  <v-textarea v-model="nfeForm.informacoesAdicionais"
                    label="Informações Adicionais (opcional)" rows="2"
                    variant="outlined" density="compact" class="mb-4" />

                  <div class="d-flex justify-space-between">
                    <v-btn variant="text" @click="nfeStep--">
                      <v-icon start>mdi-chevron-left</v-icon> Voltar
                    </v-btn>
                    <v-btn color="success" size="large" rounded="lg" :loading="emitindoNFe"
                      prepend-icon="mdi-send" @click="emitirNFe">
                      Emitir NF-e
                    </v-btn>
                  </div>
                </template>
              </v-card>
            </template>
          </v-stepper>
        </v-container>
      </v-card>
    </v-dialog>

    <!-- Dialog: Adicionar Item à NF-e -->
    <v-dialog v-model="dlgAddItem" max-width="640" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon start color="primary">mdi-package-variant-plus</v-icon>
          Adicionar Item
        </v-card-title>
        <v-card-text>
          <v-autocomplete v-model="itemBusca" label="Buscar produto (opcional)"
            :items="produtosBusca" item-title="descricao" item-value="id"
            variant="outlined" density="compact" clearable return-object
            :loading="buscandoProduto" class="mb-3"
            @update:search="buscarProdutos"
            @update:model-value="preencherItemDeProduto"
            no-data-text="Digite para buscar produtos" />

          <v-divider class="mb-3">
            <span class="text-caption text-medium-emphasis px-2">ou preencha manualmente</span>
          </v-divider>

          <v-row dense>
            <v-col cols="6" sm="3">
              <v-text-field v-model="itemForm.codigo" label="Código"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field v-model="itemForm.ncm" label="NCM *"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="itemForm.descricao" label="Descrição *"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="4" sm="2">
              <v-text-field v-model="itemForm.unidade" label="Un."
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="4" sm="2">
              <v-text-field v-model="itemForm.cfop" label="CFOP *"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="4" sm="2">
              <v-text-field v-model.number="itemForm.quantidade" label="Qtd *"
                type="number" step="0.001" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field v-model.number="itemForm.valorUnitario" label="Vl. Unit. *"
                type="number" step="0.01" prefix="R$" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field v-model.number="itemForm.valorDesconto" label="Desconto"
                type="number" step="0.01" prefix="R$" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-card rounded="lg" color="grey-lighten-4" class="pa-2 text-body-2">
                Total do item:
                <strong class="text-success">
                  R$ {{ fmt((itemForm.quantidade || 0) * (itemForm.valorUnitario || 0) - (itemForm.valorDesconto || 0)) }}
                </strong>
              </v-card>
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgAddItem = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" @click="confirmarAddItem"
            :disabled="!itemForm.descricao || !itemForm.ncm || !itemForm.cfop
              || !(itemForm.quantidade > 0) || !(itemForm.valorUnitario > 0)">
            Adicionar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Habilitar busca de NF-e -->
    <v-dialog v-model="dialogHabilitar" max-width="480" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="indigo">mdi-link-plus</v-icon>
          Habilitar Busca de NF-e
        </v-card-title>
        <v-card-text class="pa-4">
          <p class="text-body-2 mb-3">
            Este processo conecta seu sistema à SEFAZ para monitorar automaticamente
            as NF-e emitidas pelos seus fornecedores com destino ao seu CNPJ.
          </p>
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            <strong>Requisito:</strong> certificado digital A1 instalado e ambiente configurado como
            <strong>Produção</strong>.
          </v-alert>
          <v-alert v-if="habilitarResultado" :type="habilitarResultado.startsWith('❌') ? 'error' : 'success'"
            variant="tonal" density="compact" class="mt-2">
            {{ habilitarResultado }}
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogHabilitar = false; habilitarResultado = null">Fechar</v-btn>
          <v-btn color="indigo" variant="flat" :loading="habilitando"
            :disabled="!!habilitarResultado && !habilitarResultado.startsWith('❌')"
            @click="habilitarNFe">
            <v-icon start>mdi-link-plus</v-icon>Habilitar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Importar XML diretamente -->
    <v-dialog v-model="dlgImportarXml" max-width="500" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="primary">mdi-file-xml-box</v-icon>
          Importar XML de NF-e
        </v-card-title>
        <v-card-text>
          <v-alert type="info" variant="tonal" density="compact" class="mb-4">
            Faça o upload do arquivo XML autorizado pela SEFAZ. O sistema irá extrair
            emitente, itens, totais e duplicatas automaticamente.
          </v-alert>

          <v-file-input v-model="xmlFile" label="Arquivo XML da NF-e *"
            accept=".xml,text/xml,application/xml"
            variant="outlined" density="compact" prepend-icon="mdi-paperclip"
            hint="Selecione o arquivo .xml da NF-e autorizada" persistent-hint
            :rules="[r => !!r || 'Selecione o arquivo XML']" />

          <v-select v-model="localImportacaoId" label="Local de estoque de destino *"
            :items="locaisEstoque" item-title="nome" item-value="id"
            variant="outlined" density="compact" class="mt-3"
            :rules="[r => !!r || 'Selecione o local de estoque']" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgImportarXml = false" :disabled="importando">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="importando"
            :disabled="!xmlFile || !localImportacaoId" @click="importarXml">
            <v-icon start>mdi-upload</v-icon>
            Importar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog SPED -->
    <v-dialog v-model="dialogSped" max-width="400">
      <v-card rounded="xl" class="pa-4">
        <v-card-title class="mb-3">Download SPED EFD</v-card-title>
        <v-row dense>
          <v-col cols="6">
            <v-text-field v-model.number="sped.mes" label="Mês" type="number"
              min="1" max="12" variant="outlined" density="compact" />
          </v-col>
          <v-col cols="6">
            <v-text-field v-model.number="sped.ano" label="Ano" type="number"
              variant="outlined" density="compact" />
          </v-col>
        </v-row>
        <v-btn color="primary" block rounded="lg" :loading="baixandoSped" @click="baixarSped">
          Gerar e Baixar
        </v-btn>
      </v-card>
    </v-dialog>

    <!-- Dialog: Operação Não Realizada (justificativa obrigatória) -->
    <v-dialog v-model="dlgNaoRealizada" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="error">mdi-cancel</v-icon>
          Operação Não Realizada
        </v-card-title>
        <v-card-text>
          <div class="text-body-2 mb-3">
            Emitente: <strong>{{ notaSelecionada?.emitenteNome }}</strong>
          </div>
          <v-textarea v-model="justificativaNaoRealizada"
            label="Justificativa *" rows="3" variant="outlined" density="compact"
            hint="Mínimo 15 caracteres" persistent-hint
            :rules="[r => !!r || 'Obrigatória', r => r.length >= 15 || 'Mínimo 15 caracteres']" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgNaoRealizada = false" :disabled="manifestando">Cancelar</v-btn>
          <v-btn color="error" rounded="lg" :loading="manifestando"
            @click="manifestar(notaSelecionada, 'OperacaoNaoRealizada', justificativaNaoRealizada)">
            Confirmar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Iniciar Escrituração -->
    <v-dialog v-model="dlgEscriturar" max-width="420" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="primary">mdi-file-import-outline</v-icon>
          Escriturar Entrada
        </v-card-title>
        <v-card-text>
          <div class="text-body-2 mb-3">
            <strong>{{ notaParaEscriturar?.emitenteNome }}</strong><br>
            <span class="text-caption text-medium-emphasis">
              Valor: R$ {{ fmt(notaParaEscriturar?.valorTotal) }}
            </span>
          </div>
          <v-select v-model="localEscrituracaoId" label="Local de estoque de destino *"
            :items="locaisEstoque" item-title="nome" item-value="id"
            variant="outlined" density="compact"
            :rules="[r => !!r || 'Selecione o local de estoque']" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgEscriturar = false" :disabled="iniciandoEscriturar">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="iniciandoEscriturar"
            :disabled="!localEscrituracaoId" @click="confirmarEscriturar">
            <v-icon start>mdi-arrow-right</v-icon>
            Iniciar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Inutilizar Numeração -->
    <v-dialog v-model="dlgInutilizar" max-width="480" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="error">mdi-close-box-outline</v-icon>
          Inutilizar Numeração
        </v-card-title>
        <v-card-text>
          <v-alert type="warning" variant="tonal" density="compact" class="mb-3">
            Use quando números foram pulados e nunca serão emitidos. Operação irreversível.
          </v-alert>
          <v-row dense>
            <v-col cols="6">
              <v-select v-model="inut.modelo" label="Modelo" variant="outlined" density="compact"
                :items="[{title:'NF-e (55)',value:55},{title:'NFC-e (65)',value:65}]" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="inut.serie" label="Série"
                type="number" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="inut.numeroInicial" label="Nº Inicial"
                type="number" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="inut.numeroFinal" label="Nº Final"
                type="number" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-textarea v-model="inut.justificativa" label="Justificativa *"
                rows="2" variant="outlined" density="compact"
                hint="Mínimo 15 caracteres" persistent-hint />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgInutilizar = false" :disabled="inutilizando">Cancelar</v-btn>
          <v-btn color="error" rounded="lg" :loading="inutilizando" @click="confirmarInutilizar">
            Inutilizar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-speed-dial v-if="aba === 'emitidas' || aba === 'entradas'" location="bottom right"
      transition="fade-transition" class="fixed-fab">
      <template #activator="{ props }">
        <v-fab v-bind="props" icon="mdi-plus" color="primary" size="large" />
      </template>
      <v-btn key="nfe" prepend-icon="mdi-file-send-outline" text="Nova NF-e"
        color="primary" variant="flat" @click="abrirNovaNFe" />
      <v-btn key="inut" prepend-icon="mdi-close-box-outline" text="Inutilizar numeração"
        color="surface" variant="flat" @click="dlgInutilizar = true" />
      <v-btn key="sped" prepend-icon="mdi-file-export-outline" text="SPED EFD"
        color="surface" variant="flat" @click="dialogSped = true" />
      <v-btn key="xmls" prepend-icon="mdi-zip-box-outline" text="XMLs do período"
        color="surface" variant="flat" @click="baixarXmls" />
    </v-speed-dial>
  </div>
</template>

<script setup lang="ts">
import FiltroMes from '@/components/FiltroMes.vue'
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import { formatarCnpj } from '@/utils/documento'

const auth = useAuthStore()
const notif = useNotifStore()
const router = useRouter()
const aba = ref('emitidas')

// ── Emitidas ──
const carregando = ref(false)
const baixandoSped = ref(false)
const notas = ref<any[]>([])
const dialogSped = ref(false)
const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
  status: 'Todos',
})
const sped = ref({ mes: new Date().getMonth() + 1, ano: new Date().getFullYear() })

const cardsEmitidas = computed(() => [
  { label: 'NF-e emitidas', valor: notas.value.filter((n: any) => n.modelo === 55 && n.status === 'Autorizada').length, icon: 'mdi-file-check-outline', cor: 'success' },
  { label: 'NFC-e emitidas', valor: notas.value.filter((n: any) => n.modelo === 65 && n.status === 'Autorizada').length, icon: 'mdi-receipt-text-check-outline', cor: 'primary' },
  { label: 'Canceladas', valor: notas.value.filter((n: any) => n.status === 'Cancelada').length, icon: 'mdi-file-cancel-outline', cor: 'error' },
  { label: 'Total faturado', valor: `R$ ${fmt(notas.value.filter((n: any) => n.status === 'Autorizada').reduce((s: number, n: any) => s + n.totalNota, 0))}`, icon: 'mdi-currency-usd', cor: 'info' },
])

const headersEmitidas = [
  { title: 'Modelo', key: 'modelo' }, { title: 'Série', key: 'serie' }, { title: 'Nº', key: 'numero' },
  { title: 'Destinatário', key: 'nomeDestinatario' }, { title: 'Emissão', key: 'dataEmissao' },
  { title: 'Total', key: 'totalNota' }, { title: 'Status', key: 'status' }, { title: '', key: 'actions', sortable: false },
]

// ── Recebidas ──
const carregandoRec = ref(false)
const consultandoSefaz = ref(false)
const habilitando = ref(false)
const dialogHabilitar = ref(false)
const habilitarResultado = ref<string | null>(null)
const manifestando = ref(false)
const notasRecebidas = ref<any[]>([])
const alertaCertificado = ref(false)
const dlgNaoRealizada = ref(false)
const notaSelecionada = ref<any>(null)
const justificativaNaoRealizada = ref('')

const resumoRecebidas = ref({
  total: 0, semManifestacao: 0, confirmadas: 0,
  ciencia: 0, desconhecidas: 0, naoRealizadas: 0, valorTotal: 0,
})

const filtrosRec = ref({
  emitente: '',
  dataInicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  dataFim: new Date().toISOString().slice(0, 10),
  manifestacao: null as string | null,
})

const opcoesManifestacaoFiltro = [
  { title: 'Pendentes', value: null },
  { title: 'Ciência da Operação', value: 'CienciaOperacao' },
  { title: 'Confirmação da Operação', value: 'ConfirmacaoOperacao' },
  { title: 'Desconhecimento', value: 'DesconhecimentoOperacao' },
  { title: 'Operação Não Realizada', value: 'OperacaoNaoRealizada' },
]

const headersRecebidas = [
  { title: 'Emitente', key: 'emitente', sortable: false },
  { title: 'Nota', key: 'numero', sortable: false },
  { title: 'Emissão', key: 'dataEmissao' },
  { title: 'Valor Total', key: 'valorTotal' },
  { title: 'Situação', key: 'situacao' },
  { title: 'Manifestação', key: 'manifestacao' },
  { title: '', key: 'acoes', sortable: false, align: 'end' as const },
]

function corStatusEmitida(s: string) {
  return ({ Autorizada: 'success', Cancelada: 'error', Rejeitada: 'warning', EmDigitacao: 'default', Transmitindo: 'info' } as any)[s] ?? 'default'
}

function corManifestacao(m: string) {
  return ({ ConfirmacaoOperacao: 'success', CienciaOperacao: 'info', DesconhecimentoOperacao: 'warning', OperacaoNaoRealizada: 'error' } as any)[m] ?? 'default'
}

function iconeManifestacao(m: string) {
  return ({ ConfirmacaoOperacao: 'mdi-check-circle', CienciaOperacao: 'mdi-eye', DesconhecimentoOperacao: 'mdi-help-circle', OperacaoNaoRealizada: 'mdi-cancel' } as any)[m] ?? 'mdi-circle'
}

function labelManifestacao(m: string) {
  return ({ ConfirmacaoOperacao: 'Confirmada', CienciaOperacao: 'Ciência', DesconhecimentoOperacao: 'Desconhecida', OperacaoNaoRealizada: 'Não Realizada' } as any)[m] ?? m
}

const fmtCnpj = formatarCnpj
const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtData = (v: string) => v ? new Date(v).toLocaleDateString('pt-BR') : '-'

async function carregarEmitidas() {
  carregando.value = true
  try {
    const r = await api.get('/fiscal/notas', { params: { empresaId: auth.empresaId, ...filtros.value } })
    notas.value = r.data
  } finally { carregando.value = false }
}

async function carregarRecebidas() {
  carregandoRec.value = true
  try {
    const [notasRes, resumoRes] = await Promise.all([
      api.get('/fiscal/nfes-recebidas', { params: { empresaId: auth.empresaId, ...filtrosRec.value } }),
      api.get('/fiscal/nfes-recebidas/resumo', { params: { empresaId: auth.empresaId } }),
    ])
    notasRecebidas.value = notasRes.data
    resumoRecebidas.value = resumoRes.data
  } catch {
    notif.erro('Erro ao carregar NF-e recebidas.')
  } finally { carregandoRec.value = false }
}

async function habilitarNFe() {
  habilitando.value = true
  habilitarResultado.value = null
  try {
    const r = await api.post(`/fiscal/nfes-recebidas/habilitar?empresaId=${auth.empresaId}`)
    habilitarResultado.value = r.data.mensagem
    await carregarRecebidas()
  } catch (e: any) {
    const msg = e.response?.data?.mensagem ?? 'Erro ao habilitar. Verifique o certificado digital.'
    habilitarResultado.value = '❌ ' + msg
  } finally { habilitando.value = false }
}

async function consultarSefaz() {
  consultandoSefaz.value = true
  alertaCertificado.value = false
  try {
    const r = await api.post(`/fiscal/nfes-recebidas/consultar?empresaId=${auth.empresaId}`)
    if (r.data.novasNotas > 0)
      notif.ok(`${r.data.novasNotas} nova(s) NF-e encontrada(s) na SEFAZ!`)
    else
      notif.ok('Consulta realizada. Nenhuma NF-e nova encontrada.')
    await carregarRecebidas()
  } catch (e: any) {
    const msg = e.response?.data?.mensagem ?? ''
    if (msg.includes('certificado') || msg.includes('Certificado')) {
      alertaCertificado.value = true
    } else {
      notif.erro(msg || 'Erro ao consultar SEFAZ.')
    }
  } finally { consultandoSefaz.value = false }
}

async function manifestar(item: any, tipo: string, justificativa?: string) {
  if (tipo === 'OperacaoNaoRealizada' && !justificativa) return
  manifestando.value = true
  try {
    await api.post(`/fiscal/nfes-recebidas/${item.id}/manifestar?empresaId=${auth.empresaId}`,
      { tipo, justificativa: justificativa ?? null })
    notif.ok(`Manifestação "${labelManifestacao(tipo)}" registrada.`)
    dlgNaoRealizada.value = false
    await carregarRecebidas()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao manifestar.')
  } finally { manifestando.value = false }
}

function abrirNaoRealizada(item: any) {
  notaSelecionada.value = item
  justificativaNaoRealizada.value = ''
  dlgNaoRealizada.value = true
}

async function baixarXmlRecebida(item: any) {
  try {
    const r = await api.get(`/fiscal/nfes-recebidas/${item.id}/xml`,
      { params: { empresaId: auth.empresaId }, responseType: 'blob' })
    const url = URL.createObjectURL(r.data)
    const a = document.createElement('a')
    a.href = url; a.download = `NFe_${item.chaveAcesso}.xml`; a.click()
  } catch {
    notif.erro('XML não disponível. Manifeste com Confirmação ou Ciência primeiro.')
  }
}

// ── Escrituração ──
const dlgEscriturar = ref(false)
const notaParaEscriturar = ref<any>(null)
const localEscrituracaoId = ref<string | null>(null)
const iniciandoEscriturar = ref(false)
const locaisEstoque = ref<any[]>([])

async function carregarLocaisEstoque() {
  try {
    const r = await api.get('/estoque/locais', { params: { empresaId: auth.empresaId } })
    locaisEstoque.value = r.data
  } catch {}
}

function escriturarEntrada(item: any) {
  notaParaEscriturar.value = item
  localEscrituracaoId.value = locaisEstoque.value[0]?.id ?? null
  dlgEscriturar.value = true
}

async function confirmarEscriturar() {
  if (!localEscrituracaoId.value) return
  iniciandoEscriturar.value = true
  try {
    const r = await api.post(
      `/fiscal/entradas/de-nfe-recebida/${notaParaEscriturar.value.id}`,
      { empresaId: auth.empresaId, localEstoqueId: localEscrituracaoId.value })
    notif.ok('Escrituração iniciada!')
    dlgEscriturar.value = false
    router.push(`/fiscal/entradas/${r.data.id}`)
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao iniciar escrituração.')
  } finally { iniciandoEscriturar.value = false }
}

// ── Inutilizar Numeração ──
const dlgInutilizar = ref(false)
const inutilizando = ref(false)
const inut = ref({ modelo: 55, serie: 1, numeroInicial: 1, numeroFinal: 1, justificativa: '' })

async function confirmarInutilizar() {
  if (!inut.value.justificativa || inut.value.justificativa.length < 15) {
    notif.erro('Justificativa deve ter no mínimo 15 caracteres.')
    return
  }
  inutilizando.value = true
  try {
    const r = await api.post('/fiscal/notas/inutilizar', {
      empresaId: auth.empresaId, ...inut.value,
    })
    notif.ok(r.data.mensagem)
    dlgInutilizar.value = false
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao inutilizar.')
  } finally { inutilizando.value = false }
}

async function transmitir(nota: any) {
  await api.post(`/fiscal/notas/${nota.id}/transmitir`)
  notif.ok('NF-e transmitida!')
  await carregarEmitidas()
}

async function cancelar(nota: any) {
  await api.post(`/fiscal/notas/${nota.id}/cancelar`, { motivo: 'Cancelamento solicitado pelo emissor' })
  notif.ok('NF-e cancelada.')
  await carregarEmitidas()
}

async function baixarXml(nota: any) {
  if (!nota.xmlAutorizacao) return notif.aviso('XML não disponível.')
  const blob = new Blob([nota.xmlAutorizacao], { type: 'application/xml' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a'); a.href = url; a.download = `NFe_${nota.chaveAcesso}.xml`; a.click()
}

async function baixarXmls() {
  const r = await api.get('/fiscal/relatorios/download-xml', {
    params: { empresaId: auth.empresaId, inicio: filtros.value.inicio, fim: filtros.value.fim },
    responseType: 'blob',
  })
  const url = URL.createObjectURL(r.data)
  const a = document.createElement('a'); a.href = url
  a.download = `XMLs_${filtros.value.inicio}_${filtros.value.fim}.zip`; a.click()
}

async function baixarSped() {
  baixandoSped.value = true
  try {
    const r = await api.get('/fiscal/sped/efd-icms', {
      params: { empresaId: auth.empresaId, ano: sped.value.ano, mes: sped.value.mes },
      responseType: 'blob',
    })
    const url = URL.createObjectURL(r.data)
    const a = document.createElement('a'); a.href = url
    a.download = `SPED_EFD_${sped.value.ano}${String(sped.value.mes).padStart(2, '0')}.txt`; a.click()
    dialogSped.value = false
  } finally { baixandoSped.value = false }
}

// ── Entradas Escrituradas ──
const carregandoEnt = ref(false)
const importando = ref(false)
const dlgImportarXml = ref(false)
const entradas = ref<any[]>([])
const xmlFile = ref<File | null>(null)
const localImportacaoId = ref<string | null>(null)

const filtrosEnt = ref({
  dataInicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  dataFim: new Date().toISOString().slice(0, 10),
  status: null as string | null,
})

const headersEntradas = [
  { title: 'Emitente', key: 'emitente', sortable: false },
  { title: 'Chave', key: 'chave', sortable: false },
  { title: 'Emissão', key: 'dataEmissao' },
  { title: 'Entrada', key: 'dataEntrada' },
  { title: 'Valor Total', key: 'valorTotal' },
  { title: 'Itens', key: 'totalItens', width: 60 },
  { title: 'Status', key: 'status' },
  { title: '', key: 'acoes', sortable: false, align: 'end' as const },
]

function corStatusEntrada(s: string) {
  return ({ EmEdicao: 'warning', Processada: 'success', Estornada: 'error' } as any)[s] ?? 'default'
}

function labelStatusEntrada(s: string) {
  return ({ EmEdicao: 'Em Edição', Processada: 'Processada', Estornada: 'Estornada' } as any)[s] ?? s
}

async function carregarEntradas() {
  carregandoEnt.value = true
  try {
    const params: any = { empresaId: auth.empresaId, ...filtrosEnt.value }
    if (!params.status) delete params.status
    const r = await api.get('/fiscal/entradas', { params })
    entradas.value = r.data
  } catch {
    notif.erro('Erro ao carregar entradas.')
  } finally { carregandoEnt.value = false }
}

async function importarXml() {
  if (!xmlFile.value || !localImportacaoId.value) return
  importando.value = true
  try {
    const form = new FormData()
    form.append('arquivo', xmlFile.value)
    const r = await api.post('/fiscal/entradas/importar-xml', form, {
      params: { empresaId: auth.empresaId, localEstoqueId: localImportacaoId.value },
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    const res = r.data
    const aviso = res.itensPendentes > 0
      ? `${res.itensPendentes} produto(s) precisam de vinculação manual.`
      : 'Todos os produtos foram vinculados automaticamente.'
    notif.ok(`NF ${res.numeroNF} importada — ${res.totalItens} itens. ${aviso}`)
    if (res.avisos?.length) res.avisos.forEach((a: string) => notif.aviso(a))
    if (res.duplicatas?.length)
      sessionStorage.setItem(`entrada_duplicatas_${res.id}`, JSON.stringify(res.duplicatas))
    dlgImportarXml.value = false
    xmlFile.value = null
    localImportacaoId.value = locaisEstoque.value[0]?.id ?? null
    router.push(`/fiscal/entradas/${res.id}`)
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao importar XML.')
  } finally { importando.value = false }
}

function abrirEntrada(item: any) {
  router.push(`/fiscal/entradas/${item.id}`)
}

// ══════════════════════════════════════════════════════════════════
// NOVA NF-e — Stepper 4 etapas
// ══════════════════════════════════════════════════════════════════

const dlgNovaNFe = ref(false)
const nfeStep = ref(1)
const emitindoNFe = ref(false)
const nfeEmitida = ref<any>(null)

// Lookups estáticos
const naturezasOperacao = [
  { title: 'Venda de Produto', value: 1 },
  { title: 'Venda ao Consumidor', value: 2 },
  { title: 'Devolução', value: 3 },
  { title: 'Transferência', value: 4 },
  { title: 'Remessa', value: 5 },
]

const cfops = [
  { title: '5102 — Venda de mercadoria adquirida (dentro do estado)', value: '5102' },
  { title: '5103 — Venda de produção do estabelecimento (dentro do estado)', value: '5103' },
  { title: '6102 — Venda de mercadoria adquirida (fora do estado)', value: '6102' },
  { title: '6103 — Venda de produção do estabelecimento (fora do estado)', value: '6103' },
  { title: '5405 — Venda para não contribuinte (simples nacional)', value: '5405' },
  { title: '5906 — Remessa para industrialização', value: '5906' },
  { title: '5910 — Remessa em bonificação', value: '5910' },
]

const formasPagamento = [
  { title: 'Dinheiro', value: 'Dinheiro' },
  { title: 'PIX', value: 'Pix' },
  { title: 'Cartão de Crédito', value: 'CartaoCredito' },
  { title: 'Cartão de Débito', value: 'CartaoDebito' },
  { title: 'Boleto', value: 'Boleto' },
  { title: 'Crediário', value: 'Crediario' },
  { title: 'Outros', value: 'Outros' },
]

interface NFeItem {
  produtoId?: string
  codigo: string
  descricao: string
  ncm: string
  cfop: string
  unidade: string
  quantidade: number
  valorUnitario: number
  valorDesconto: number
}

interface NFePagamento {
  tipo: string
  valor: number
}

interface NFeForm {
  destinatario: { tipo: string; cpfCnpj: string; nome: string; email: string }
  naturezaOperacao: number
  itens: NFeItem[]
  pagamentos: NFePagamento[]
  informacoesAdicionais: string
}

const nfeFormPadrao = (): NFeForm => ({
  destinatario: { tipo: 'PF', cpfCnpj: '', nome: '', email: '' },
  naturezaOperacao: 1,
  itens: [],
  pagamentos: [{ tipo: 'Dinheiro', valor: 0 }],
  informacoesAdicionais: '',
})

const nfeForm = ref<NFeForm>(nfeFormPadrao())
const nfeCfopPadrao = ref('5102')

const headersItensNFe = [
  { title: 'Código', key: 'codigo', width: 90 },
  { title: 'Descrição', key: 'descricao' },
  { title: 'NCM', key: 'ncm', width: 90 },
  { title: 'CFOP', key: 'cfop', width: 80 },
  { title: 'Un.', key: 'unidade', width: 60 },
  { title: 'Qtd', key: 'quantidade', width: 80 },
  { title: 'Vl. Unit.', key: 'valorUnitario', width: 110 },
  { title: 'Total', key: 'total', width: 110 },
  { title: '', key: 'acoes', sortable: false, width: 48 },
]

const totalItensNFe = computed(() =>
  nfeForm.value.itens.reduce((s, i) => s + i.quantidade * i.valorUnitario - (i.valorDesconto ?? 0), 0)
)

const totalPagoNFe = computed(() =>
  nfeForm.value.pagamentos.reduce((s, p) => s + (p.valor ?? 0), 0)
)

function aplicarCfopPadrao(cfop: string) {
  nfeForm.value.itens.forEach(i => { i.cfop = cfop })
}

function abrirNovaNFe() {
  nfeForm.value = nfeFormPadrao()
  nfeStep.value = 1
  nfeEmitida.value = null
  nfeCfopPadrao.value = '5102'
  dlgNovaNFe.value = true
}

function fecharNovaNFe() {
  dlgNovaNFe.value = false
  nfeEmitida.value = null
  if (aba.value === 'emitidas') carregarEmitidas()
}

// ── Adicionar item ──
const dlgAddItem = ref(false)
const buscandoProduto = ref(false)
const produtosBusca = ref<any[]>([])
const itemBusca = ref<any>(null)

const itemFormPadrao = () => ({
  produtoId: undefined as string | undefined,
  codigo: '',
  descricao: '',
  ncm: '',
  cfop: nfeCfopPadrao.value || '5102',
  unidade: 'UN',
  quantidade: 1,
  valorUnitario: 0,
  valorDesconto: 0,
})
const itemForm = ref(itemFormPadrao())

function abrirAddItem() {
  itemForm.value = itemFormPadrao()
  itemBusca.value = null
  produtosBusca.value = []
  dlgAddItem.value = true
}

async function buscarProdutos(busca: string) {
  if (!busca || busca.length < 2) return
  buscandoProduto.value = true
  try {
    const r = await api.get('/produtos', { params: { empresaId: auth.empresaId, busca } })
    produtosBusca.value = r.data
  } catch {
    produtosBusca.value = []
  } finally { buscandoProduto.value = false }
}

function preencherItemDeProduto(produto: any) {
  if (!produto) return
  itemForm.value.produtoId = produto.id
  itemForm.value.codigo = produto.codigoInterno ?? produto.codigoBarras ?? ''
  itemForm.value.descricao = produto.descricao ?? produto.nome ?? ''
  itemForm.value.ncm = produto.ncm ?? ''
  itemForm.value.cfop = nfeCfopPadrao.value || '5102'
  itemForm.value.unidade = produto.unidadeMedida ?? 'UN'
  itemForm.value.valorUnitario = produto.precoVenda ?? 0
}

function confirmarAddItem() {
  nfeForm.value.itens.push({ ...itemForm.value })
  dlgAddItem.value = false
}

// ── Emitir NF-e ──
async function emitirNFe() {
  emitindoNFe.value = true
  try {
    const payload = {
      empresaId: auth.empresaId,
      modelo: '55',
      destinatario: nfeForm.value.destinatario,
      naturezaOperacao: nfeForm.value.naturezaOperacao,
      itens: nfeForm.value.itens,
      pagamentos: nfeForm.value.pagamentos,
      informacoesAdicionais: nfeForm.value.informacoesAdicionais || undefined,
    }
    const r = await api.post('/fiscal/notas', payload)
    const nota = r.data

    // Se status for EmDigitacao, tentar transmitir automaticamente
    if (nota.status === 'EmDigitacao') {
      try {
        const rt = await api.post(`/fiscal/notas/${nota.id}/transmitir`)
        nfeEmitida.value = { ...nota, ...rt.data }
      } catch {
        nfeEmitida.value = nota
        notif.aviso('NF-e salva mas transmissão falhou. Use o botão Transmitir na tabela.')
      }
    } else {
      nfeEmitida.value = nota
    }
    notif.ok(`NF-e ${nota.numero ?? ''} criada com sucesso!`)
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao emitir NF-e.')
  } finally { emitindoNFe.value = false }
}

async function baixarDanfe(id: string) {
  try {
    const r = await api.get(`/fiscal/notas/${id}/danfe`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data)
    const a = document.createElement('a'); a.href = url; a.download = `DANFE_${id}.pdf`; a.click()
  } catch { notif.erro('DANFE não disponível.') }
}

async function baixarXmlNfe(id: string) {
  try {
    const r = await api.get(`/fiscal/notas/${id}/xml`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data)
    const a = document.createElement('a'); a.href = url; a.download = `NFe_${id}.xml`; a.click()
  } catch { notif.erro('XML não disponível.') }
}

watch(aba, (v) => {
  if (v === 'recebidas') carregarRecebidas()
  if (v === 'entradas') carregarEntradas()
})

onMounted(async () => {
  await Promise.all([carregarEmitidas(), carregarLocaisEstoque()])
})
</script>
