<template>
  <div>
    <!-- Cabeçalho -->
    <div class="d-flex align-center mb-4 gap-2">
      <v-btn icon="mdi-arrow-left" variant="text" @click="$router.back()" />
      <div class="flex-grow-1">
        <div class="text-h6 font-weight-bold">Escrituração de Entrada</div>
        <div class="text-caption text-medium-emphasis">
          {{ entrada?.emitenteNome }} ·
          Chave: {{ entrada?.chaveAcesso?.substring(0, 22) }}…
        </div>
      </div>
      <v-chip :color="corStatus(entrada?.status)" variant="tonal" class="font-weight-medium">
        {{ entrada?.status }}
      </v-chip>
      <v-menu>
        <template #activator="{ props }">
          <v-btn v-bind="props" icon="mdi-dots-vertical" variant="outlined" />
        </template>
        <v-list density="compact">
          <v-list-item prepend-icon="mdi-content-copy" title="Clonar entrada" @click="clonar" />
          <v-list-item prepend-icon="mdi-printer-outline" title="Imprimir etiquetas"
            @click="dlgEtiquetas = true" :disabled="entrada?.status !== 'Processada'" />
          <v-divider />
          <v-list-item prepend-icon="mdi-swap-horizontal" title="Clonar para Saída (NF-e)"
            subtitle="Gera NF-e de saída com estes itens"
            @click="clonarParaSaida" :disabled="entrada?.status !== 'Processada'" />
          <v-list-item prepend-icon="mdi-arrow-u-left-top" title="Devolver Mercadoria"
            subtitle="Gera NF-e de devolução ao fornecedor"
            class="text-warning"
            @click="dlgDevolucao = true" :disabled="entrada?.status !== 'Processada'" />
          <v-divider />
          <v-list-item prepend-icon="mdi-undo" title="Estornar" class="text-warning"
            @click="dlgEstorno = true" :disabled="entrada?.status !== 'Processada'" />
          <v-list-item prepend-icon="mdi-delete-outline" title="Excluir" class="text-error"
            @click="excluir" :disabled="entrada?.status === 'Processada'" />
        </v-list>
      </v-menu>
    </div>

    <!-- Cabeçalho do assistente (wizard) -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <div class="d-flex align-center flex-wrap" style="gap:4px">
        <template v-for="(s, i) in passosWizard" :key="s.n">
          <v-chip
            :color="passo === s.n ? 'primary' : passo > s.n ? 'success' : 'default'"
            :variant="passo >= s.n ? 'flat' : 'tonal'" size="small"
            :disabled="entrada?.status === 'EmEdicao' && s.n > passoMaximo"
            @click="irParaPasso(s.n)">
            <v-icon v-if="passo > s.n" start size="14">mdi-check</v-icon>
            <span v-else class="font-weight-bold mr-1">{{ i + 1 }}.</span>{{ s.label }}
          </v-chip>
          <v-icon v-if="i < passosWizard.length - 1" size="14" color="grey">mdi-chevron-right</v-icon>
        </template>
      </div>
    </v-card>

    <GuiaPassos
      id="entrada-nfe-wizard"
      titulo="Como escriturar esta entrada (assistente)"
      :passos="[
        '<b>1. Dados da Nota</b>: confira emitente, datas e valores. Selecione o <b>Local de Estoque</b> e informe o <b>Frete</b>. Clique em <b>Processar Nota Fiscal</b>.',
        '<b>2. Produtos</b>: o sistema já busca os produtos <b>já cadastrados</b> (código de barras, de-para do fornecedor, código). Vincule os que faltam ou use <b>Cadastrar os que faltam</b>.',
        '<b>3. Fator de Conversão</b>: informe a <b>unidade de estoque</b> e o <b>fator</b> (ex.: caixa com 12 → 12). A unidade já vem do cadastro do produto.',
        '<b>4. Custos e Preços</b>: confira o frete rateado e os impostos (editáveis), e defina o <b>markup</b> (vem do cadastro) — o preço sugerido é calculado aqui.',
        '<b>5. Financeiro</b>: revise as parcelas do XML e escolha a <b>categoria</b> do contas a pagar. <b>6. Finalização</b>: confirme para atualizar estoque, custos e lançar o financeiro.',
      ]"
    />

    <v-alert v-if="entrada?.status === 'Processada'" type="success" variant="tonal"
      density="compact" class="mb-4" icon="mdi-check-decagram">
      Entrada <b>processada</b> — estoque, custos e financeiro atualizados. Use o menu ⋮ para estornar.
    </v-alert>

    <!-- ─── ETAPA 1: DADOS DA NOTA ─── -->
    <div v-show="passo === 1">
      <v-row>
        <v-col cols="12" md="6">
          <v-card rounded="xl" elevation="1" class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-3">Emitente</div>
            <v-text-field :model-value="entrada?.emitenteNome" label="Razão Social"
              variant="outlined" density="compact" readonly />
            <v-text-field :model-value="fmtCnpj(entrada?.emitenteCnpj)" label="CNPJ"
              variant="outlined" density="compact" readonly />

            <!-- Entrada sem fornecedor: cadastra/vincula pelo CNPJ do emitente -->
            <v-alert v-if="entrada && !entrada.fornecedorId"
              type="warning" variant="tonal" density="compact" class="mt-1">
              <div class="text-body-2 mb-2">
                Esta nota <b>não tem fornecedor vinculado</b>. Cadastre o emitente para
                alimentar o contas a pagar e o de-para de produtos.
              </div>
              <v-btn size="small" color="warning" variant="flat" :loading="vinculandoAuto"
                prepend-icon="mdi-account-plus-outline" @click="vincularAutomatico">
                Cadastrar e vincular fornecedor
              </v-btn>
            </v-alert>
            <v-alert v-else-if="entrada?.fornecedorId" type="success" variant="tonal"
              density="compact" class="mt-1">
              <div class="d-flex align-center flex-wrap gap-2">
                <span>Fornecedor vinculado.</span>
                <v-spacer />
                <v-btn size="x-small" variant="text" :loading="vinculandoAuto"
                  prepend-icon="mdi-autorenew" @click="vincularAutomatico">
                  Aplicar aos produtos e contas
                </v-btn>
              </div>
            </v-alert>

            <!-- Destino dos itens: venda ou uso interno -->
            <v-divider class="my-3" />
            <div class="text-subtitle-2 font-weight-bold mb-2">O que está entrando?</div>
            <v-btn-toggle :model-value="entrada?.tipoEntrada ?? 'Mercadoria'" mandatory divided
              density="compact" class="mb-2" :disabled="entrada?.status !== 'EmEdicao'"
              @update:model-value="definirTipoEntrada">
              <v-btn value="Mercadoria" size="small" prepend-icon="mdi-cart-outline">
                Mercadoria para venda
              </v-btn>
              <v-btn value="MaterialConsumo" size="small" prepend-icon="mdi-package-variant-closed">
                Material de consumo
              </v-btn>
              <v-btn value="AtivoImobilizado" size="small" prepend-icon="mdi-desktop-classic">
                Ativo imobilizado
              </v-btn>
            </v-btn-toggle>
            <div class="text-caption text-medium-emphasis">
              <template v-if="ehMaterialConsumo">
                Os itens vão para <b>Materiais de Consumo</b> (uso interno): não entram no
                PDV, no catálogo nem na formação de preço.
              </template>
              <template v-else-if="ehAtivoImobilizado">
                Os itens viram <b>bens da empresa</b> (equipamento, móvel, veículo): sem estoque
                e sem venda — só valor, localização e depreciação.
              </template>
              <template v-else>
                Os itens vão para o cadastro de <b>Produtos</b> e ficam disponíveis para venda.
              </template>
            </div>
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card rounded="xl" elevation="1" class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-3">Dados Gerais</div>
            <v-row dense>
              <v-col cols="6">
                <v-text-field :model-value="fmtData(entrada?.dataEmissao)" label="Data Emissão"
                  variant="outlined" density="compact" readonly />
              </v-col>
              <v-col cols="6">
                <v-text-field :model-value="fmtData(entrada?.dataEntrada)" label="Data Entrada"
                  variant="outlined" density="compact" readonly />
              </v-col>
              <v-col cols="12">
                <v-select v-model="localEstoqueId" label="Local de Estoque *"
                  :items="locaisEstoque" item-title="nome" item-value="id"
                  variant="outlined" density="compact"
                  :disabled="entrada?.status === 'Processada'"
                  @update:model-value="salvarLocalEstoque" />
              </v-col>
              <v-col cols="12">
                <v-autocomplete v-model="pedidoCompraId" label="Vincular Ordem de Compra"
                  :items="pedidosCompra" item-title="label" item-value="id"
                  variant="outlined" density="compact" clearable
                  @update:model-value="salvarPedidoCompra" />
              </v-col>
            </v-row>
          </v-card>
        </v-col>
        <v-col cols="12">
          <v-card rounded="xl" elevation="1" class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-3">Valores da Nota</div>
            <v-row dense>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(entrada?.valorProdutos)" label="Produtos"
                  variant="outlined" density="compact" readonly prefix="R$" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(entrada?.valorFrete)" label="Frete (XML)"
                  variant="outlined" density="compact" readonly prefix="R$" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field
                  :model-value="'R$ ' + fmt(entrada?.valorFreteManual)"
                  label="Frete Manual"
                  variant="outlined" density="compact" readonly
                  :append-inner-icon="entrada?.status !== 'Processada' ? 'mdi-pencil' : undefined"
                  @click:append-inner="abrirDlgFrete"
                  @click="abrirDlgFrete"
                  style="cursor:pointer"
                />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(entrada?.valorIpi)" label="IPI"
                  variant="outlined" density="compact" readonly prefix="R$" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(entrada?.valorIcmsSt)" label="ICMS ST"
                  variant="outlined" density="compact" readonly prefix="R$" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(entrada?.valorTotal)" label="Total"
                  variant="outlined" density="compact" readonly prefix="R$"
                  class="font-weight-bold" />
              </v-col>
            </v-row>
          </v-card>
        </v-col>
      </v-row>
    </div>

    <!-- ─── ETAPAS 2-4: ITENS (colunas variam por etapa) ─── -->
    <div v-show="passo >= 2 && passo <= 4">

      <!-- Entrada sem itens (escriturada antes do XML completo) -->
      <v-alert v-if="itensEditaveis.length === 0" type="warning" variant="tonal"
        class="mb-3" icon="mdi-file-alert-outline">
        <div class="d-flex align-center flex-wrap gap-3">
          <div>
            Esta entrada está <b>sem itens</b> — provavelmente foi escriturada antes do
            XML completo ser baixado. Se a nota já foi manifestada, reimporte os itens do XML.
          </div>
          <v-spacer />
          <v-btn v-if="entrada?.status === 'EmEdicao'" color="warning" variant="flat"
            :loading="reimportando" prepend-icon="mdi-download" @click="reimportarItens">
            Reimportar itens do XML
          </v-btn>
        </div>
      </v-alert>


      <v-alert :type="passo === 2 ? 'info' : passo === 3 ? 'warning' : 'success'"
        variant="tonal" density="compact" class="mb-3">
        <template v-if="passo === 2 && ehMaterialConsumo">
          <b>Materiais de consumo.</b> Vincule cada item da nota a um <b>material de
          uso interno</b> — ou use <b>Cadastrar materiais que faltam</b>. Eles não vão
          para o cadastro de produtos nem para a venda.
          <b>{{ itensSemProduto }}</b> sem vínculo.
        </template>
        <template v-else-if="passo === 2 && ehAtivoImobilizado">
          <b>Bens do ativo imobilizado.</b> Vincule cada item a um <b>bem</b> — ou use
          <b>Cadastrar bens que faltam</b> (entram como Equipamento, 5 anos de vida útil;
          ajuste depois em Estoque → Ativo Imobilizado).
          <b>{{ itensSemProduto }}</b> sem vínculo.
        </template>
        <template v-else-if="passo === 2">
          <b>Etapa 2 — Produtos.</b> O sistema já procurou os produtos <b>já cadastrados</b>
          (por código de barras, de-para do fornecedor e código). Vincule os que faltam,
          crie novos (+) ou use <b>Cadastrar todos automaticamente</b>.
          <b>{{ itensSemProduto }}</b> sem vínculo.
        </template>
        <template v-else-if="passo === 3">
          <b>Etapa 3 — Fator de Conversão.</b> Informe a <b>unidade de estoque</b> e o
          <b>fator de conversão</b> (ex.: caixa com 12 → 12; pacote de 5 kg → 5).
          A unidade já vem do cadastro do produto quando existe.
        </template>
        <template v-else-if="ehUsoInterno">
          <b>Custos.</b> Confira o rateio do frete (proporcional ao valor) e o custo final.
          <b>IPI</b> e <b>ICMS-ST</b> vêm da nota e são <b>editáveis</b>.
          <template v-if="ehMaterialConsumo">
            O custo alimenta o <b>custo médio</b> no estoque de consumo.
          </template>
          <template v-else>
            O custo vira o <b>valor de aquisição</b> do bem, base da depreciação.
          </template>
          Não há markup nem preço de venda.
        </template>
        <template v-else>
          <b>Etapa 4 — Custos e Preços.</b> Confira o rateio do frete (proporcional ao valor),
          o custo final e defina o <b>markup</b> (vem do cadastro do produto) e o preço.
          <b>IPI</b> e <b>ICMS-ST</b> vêm da nota e são <b>editáveis</b>.
        </template>
      </v-alert>

      <!-- CFOP do emitente (venda) numa nota de uso e consumo -->
      <v-alert v-if="passo === 2 && itensCfopVenda > 0 && entrada?.status === 'EmEdicao'"
        type="warning" variant="tonal" density="compact" class="mb-3">
        <div class="d-flex align-center flex-wrap gap-2">
          <span>
            <b>{{ itensCfopVenda }}</b> item(ns) com <b>CFOP de venda</b> (veio do emitente no XML).
            Em compra de uso e consumo o correto é <b>1556</b> (mesmo estado) ou <b>2556</b> (outro estado).
          </span>
          <v-spacer />
          <v-btn size="small" color="warning" variant="flat" :loading="corrigindoCfop"
            prepend-icon="mdi-auto-fix" @click="corrigirCfopMaterial">
            Corrigir CFOP
          </v-btn>
        </div>
      </v-alert>

      <!-- Barra de ações -->
      <div v-if="entrada?.status === 'EmEdicao' && passo !== 4"
        class="d-flex align-center gap-3 pa-3 mb-3 rounded-lg"
        style="background:#1e1e2e; border:1px solid rgba(255,255,255,0.08)">

        <span class="text-body-2" style="color:rgba(255,255,255,0.7); white-space:nowrap">
          <v-icon size="14" class="mr-1" color="white">mdi-alert-circle-outline</v-icon>
          {{ itensSemProduto }} sem vínculo
          <span v-if="itensAlterados > 0" style="color:#ffa726" class="ml-2 font-weight-medium">
            · {{ itensAlterados }} alterado(s)
          </span>
        </span>

        <v-spacer />

        <!-- Nota de material de consumo: cadastra/vincula no estoque de uso interno -->
        <template v-if="passo === 2 && ehMaterialConsumo && itensSemProduto > 0">
          <v-btn size="small" color="teal" variant="flat"
            :loading="cadastrandoMateriais" prepend-icon="mdi-package-variant-closed"
            @click="cadastrarMateriaisFaltantes">
            Cadastrar materiais que faltam ({{ itensSemProduto }})
          </v-btn>
        </template>

        <template v-else-if="passo === 2 && ehAtivoImobilizado && itensSemProduto > 0">
          <v-btn size="small" color="indigo" variant="flat"
            :loading="cadastrandoAtivos" prepend-icon="mdi-desktop-classic"
            @click="cadastrarAtivosFaltantes">
            Cadastrar bens que faltam ({{ itensSemProduto }})
          </v-btn>
        </template>

        <template v-else-if="passo === 2 && itensSemProduto > 0">
          <v-btn size="small" variant="outlined" :loading="vinculandoAuto"
            prepend-icon="mdi-link-variant"
            style="color:white; border-color:rgba(255,255,255,0.3)"
            @click="vincularAutomatico">
            Buscar produtos já cadastrados
          </v-btn>
          <v-btn size="small" color="success" variant="flat"
            :loading="cadastrandoTodos" prepend-icon="mdi-database-plus-outline"
            @click="cadastrarTodosAutomaticamente">
            Cadastrar os que faltam ({{ itensSemProduto }})
          </v-btn>
        </template>

        <template v-if="entrada?.status === 'EmEdicao'">
          <!-- Menu ações em lote (markup) — etapa 4, junto dos preços.
               Material de consumo não tem markup. -->
          <v-menu v-if="passo === 4 && !ehUsoInterno" :close-on-content-click="false">
            <template #activator="{ props }">
              <v-btn v-bind="props" size="small" variant="outlined"
                append-icon="mdi-chevron-down"
                style="color:white; border-color:rgba(255,255,255,0.3)">
                Markup em lote
              </v-btn>
            </template>
            <v-list density="compact" min-width="320">
              <v-list-subheader>Aplicar a todos os itens vinculados</v-list-subheader>
              <v-list-item prepend-icon="mdi-tag-outline"
                title="Manter markup atual de cada produto"
                subtitle="Preço = custo × (1 + markup% / 100) do cadastro"
                @click="aplicarModoMarkup('manter_markup')" />
              <v-list-item prepend-icon="mdi-currency-usd"
                title="Manter preço atual de cada produto"
                subtitle="Markup = preço salvo ÷ custo do XML"
                @click="aplicarModoMarkup('manter_preco')" />
              <v-divider class="my-1" />
              <v-list-subheader>Markup único para todos</v-list-subheader>
              <v-list-item>
                <div class="d-flex align-center gap-2 py-1">
                  <v-text-field v-model.number="markupGlobal" type="number" min="0" step="5"
                    label="Markup %" suffix="%" variant="outlined" density="compact" hide-details
                    style="width:110px" />
                  <v-btn size="small" color="primary" variant="flat" @click="aplicarMarkupGlobal">
                    Aplicar
                  </v-btn>
                </div>
              </v-list-item>
            </v-list>
          </v-menu>

          <v-btn variant="outlined" size="small"
            :disabled="itensAlterados === 0"
            style="color:#ef9a9a; border-color:rgba(239,154,154,0.4)"
            @click="descartarItens">
            <v-icon start>mdi-undo</v-icon> Descartar
          </v-btn>

          <v-btn color="primary" size="small" variant="flat"
            :loading="salvandoTodos" :disabled="itensAlterados === 0"
            @click="salvarTodos">
            <v-icon start>mdi-content-save</v-icon>
            Salvar todos ({{ itensAlterados }})
          </v-btn>
        </template>
      </div>

      <!-- Grid editável -->
      <div v-for="item in itensEditaveis" :key="item.id">
        <v-card rounded="lg" elevation="1" class="mb-2 pa-3"
          :class="{ 'border-warning': !item._produtoId, 'border-success': item._alterado }">
          <v-row dense align="center">

            <!-- # e descrição (sempre) -->
            <v-col cols="12" sm="4">
              <div class="text-caption text-medium-emphasis">#{{ item.numeroItem }}
                <span v-if="item._produtoId" class="text-success">· vinculado</span>
              </div>
              <div class="text-body-2 font-weight-medium">{{ item._produtoDescricao || item.descricaoXml }}</div>
              <div class="text-caption text-medium-emphasis">
                NCM: {{ item.ncmXml }} · Cód: {{ item.codigoFornecedor ?? '—' }}
              </div>
              <div class="text-caption">
                <strong>XML:</strong> {{ item.quantidadeXml }} {{ item.unidadeXml }}
                · R$ {{ fmt(item.valorUnitarioXml) }} = R$ {{ fmt(item.valorTotalXml) }}
              </div>
            </v-col>

            <!-- Unid. de compra (etapa 3, somente leitura) -->
            <v-col v-if="passo === 3" cols="4" sm="1">
              <v-text-field :model-value="item.unidadeXml" label="Unid. compra"
                variant="outlined" density="compact" hide-details readonly />
            </v-col>

            <!-- Material de consumo (etapa 2, nota de uso interno) -->
            <v-col v-if="passo === 2 && ehMaterialConsumo" cols="12" sm="4">
              <v-autocomplete
                v-model="item.materialConsumoId"
                label="Material de consumo *"
                :items="materiais" item-title="descricao" item-value="id"
                variant="outlined" density="compact" clearable hide-details
                :disabled="entrada?.status === 'Processada'"
                :color="item.materialConsumoId ? 'success' : 'warning'"
                @update:model-value="onMaterialInline(item, $event)"
              />
            </v-col>

            <!-- Bem do ativo imobilizado (etapa 2) -->
            <v-col v-else-if="passo === 2 && ehAtivoImobilizado" cols="12" sm="4">
              <v-autocomplete
                v-model="item.ativoImobilizadoId"
                label="Bem cadastrado *"
                :items="ativos" item-title="descricao" item-value="id"
                variant="outlined" density="compact" clearable hide-details
                :disabled="entrada?.status === 'Processada'"
                :color="item.ativoImobilizadoId ? 'success' : 'warning'"
                @update:model-value="onAtivoInline(item, $event)"
              />
            </v-col>

            <!-- Produto (etapa 2) -->
            <v-col v-else-if="passo === 2" cols="12" sm="4">
              <div class="d-flex gap-2 align-center">
                <v-autocomplete
                  v-model="item._produtoId"
                  label="Produto cadastrado *"
                  :items="produtos" item-title="descricao" item-value="id"
                  :custom-filter="filtrarPorPalavras"
                  variant="outlined" density="compact" clearable hide-details
                  :disabled="entrada?.status === 'Processada'"
                  :color="item._produtoId ? 'success' : 'warning'"
                  @update:model-value="onProdutoInline(item, $event)"
                />
                <v-btn v-if="!item._produtoId && entrada?.status !== 'Processada'"
                  icon="mdi-plus" size="small" color="primary" variant="tonal"
                  title="Criar produto a partir dos dados do XML"
                  @click="abrirCriarProduto(item)" />
              </div>

              <!-- Sugestão: produto parecido já cadastrado (evita duplicar) -->
              <div v-if="!item._produtoId && sugestoes[item.id]?.length" class="mt-1">
                <div class="text-caption text-medium-emphasis mb-1">
                  <v-icon size="12" color="warning">mdi-lightbulb-on-outline</v-icon>
                  Já cadastrado? Parecido com:
                </div>
                <v-chip v-for="s in sugestoes[item.id]" :key="s.id" size="x-small"
                  color="warning" variant="tonal" class="mr-1 mb-1" style="cursor:pointer"
                  :title="`${s.descricao} — ${s.score}% parecido`"
                  @click="onProdutoInline(item, s.id)">
                  {{ s.descricao.length > 38 ? s.descricao.slice(0, 38) + '…' : s.descricao }}
                  <span class="ml-1 font-weight-bold">{{ s.score }}%</span>
                </v-chip>
              </div>
            </v-col>

            <!-- CFOP (etapa 2) -->
            <v-col v-if="passo === 2" cols="6" sm="1">
              <v-text-field v-model="item._cfop" label="CFOP"
                variant="outlined" density="compact" hide-details
                :disabled="entrada?.status === 'Processada'"
                @update:model-value="item._alterado = true" />
            </v-col>

            <!-- Conversão (etapa 3) -->
            <v-col v-if="passo === 3" cols="4" sm="1">
              <v-text-field v-model.number="item._fator" label="Fator conv. *"
                type="number" min="0.001" step="0.001"
                variant="outlined" density="compact" hide-details
                :error="!(item._fator > 0)"
                :disabled="entrada?.status === 'Processada'"
                @update:model-value="item._alterado = true" />
            </v-col>
            <v-col v-if="passo === 3" cols="4" sm="2">
              <v-autocomplete v-model="item._unidade" label="Unid. estoque *"
                :items="unidades" variant="outlined" density="compact" hide-details
                :error="!item._unidade"
                :disabled="entrada?.status === 'Processada'"
                auto-select-first
                @update:model-value="item._alterado = true" />
            </v-col>

            <!-- Qtd. estoque (etapas 3 e 4) -->
            <v-col v-if="passo === 3 || passo === 4" cols="4" sm="2">
              <v-text-field
                :model-value="`${fmtQtd(item.quantidadeXml * (item._fator || 1))} ${item._unidade}`"
                label="Qtd. estoque" variant="outlined" density="compact" hide-details readonly />
            </v-col>

            <!-- Markup (etapa 4 — junto dos custos/preços). Material de consumo
                 não é vendido: não tem markup nem preço. -->
            <v-col v-if="passo === 4 && !ehUsoInterno" cols="6" sm="1">
              <v-text-field v-model.number="item._markup" label="Markup %" suffix="%"
                type="number" min="0" step="1" variant="outlined" density="compact" hide-details
                :disabled="entrada?.status === 'Processada'"
                hint="Do último cadastro — editável"
                @update:model-value="onMarkupChange(item)" />
            </v-col>

            <!-- Conferência de custos (etapa 4, somente leitura) -->
            <template v-if="passo === 4">
              <v-col cols="6" sm="1">
                <v-text-field :model-value="fmt(item.valorTotalXml)" label="Vl. compra"
                  prefix="R$" variant="outlined" density="compact" hide-details readonly />
              </v-col>
              <v-col cols="6" sm="1">
                <v-text-field :model-value="fmt(freteRateado(item))" label="Frete rateado"
                  prefix="R$" variant="outlined" density="compact" hide-details readonly />
              </v-col>
              <v-col cols="6" sm="1">
                <v-text-field v-model.number="item._ipi" label="IPI" prefix="R$"
                  type="number" min="0" step="0.01"
                  variant="outlined" density="compact" hide-details
                  :disabled="entrada?.status === 'Processada'"
                  hint="Do XML — ajuste se necessário"
                  @update:model-value="onMarkupChange(item)" />
              </v-col>
              <v-col cols="6" sm="1">
                <v-text-field v-model.number="item._icmsSt" label="ICMS-ST" prefix="R$"
                  type="number" min="0" step="0.01"
                  variant="outlined" density="compact" hide-details
                  :disabled="entrada?.status === 'Processada'"
                  hint="Do XML — ajuste se necessário"
                  @update:model-value="onMarkupChange(item)" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(custoTotalItem(item))" label="Custo final"
                  prefix="R$" variant="outlined" density="compact" hide-details readonly
                  class="font-weight-bold" />
              </v-col>
              <v-col cols="6" sm="2">
                <v-text-field :model-value="fmt(custoUnitario(item))"
                  :label="`Custo unit. (${item._unidade || 'un'})`"
                  prefix="R$" variant="outlined" density="compact" hide-details readonly
                  class="font-weight-bold" />
              </v-col>
              <!-- Preço de venda: só para mercadoria. Material de consumo não é vendido. -->
              <template v-if="!ehUsoInterno">
                <!-- Preço atual cadastrado (referência) -->
                <v-col cols="6" sm="1">
                  <v-text-field :model-value="item._precoAtual ? fmt(item._precoAtual) : '—'"
                    label="Preço atual" prefix="R$" variant="outlined" density="compact"
                    hide-details readonly />
                </v-col>
                <!-- Novo preço (editável ⇄ markup) com indicador de variação -->
                <v-col cols="6" sm="2">
                  <v-text-field v-model.number="item._precoVenda"
                    :label="`Novo preço (${item._unidade || 'un'})`" prefix="R$"
                    type="number" min="0" step="0.01"
                    variant="outlined" density="compact" hide-details
                    :disabled="entrada?.status === 'Processada'"
                    :bg-color="statusPreco(item).bg"
                    class="font-weight-bold"
                    @update:model-value="onPrecoVendaChange(item)">
                    <template #append-inner>
                      <v-tooltip location="top" :text="statusPreco(item).dica">
                        <template #activator="{ props }">
                          <v-icon v-bind="props" :icon="statusPreco(item).icone"
                            :color="statusPreco(item).cor" size="20" />
                        </template>
                      </v-tooltip>
                    </template>
                  </v-text-field>
                </v-col>
                <!-- Valores por 100g (produtos por peso) -->
                <v-col cols="6" sm="1">
                  <v-text-field :model-value="fmt(custoUnitario(item) / 10)" label="Custo 100g"
                    prefix="R$" variant="outlined" density="compact" hide-details readonly />
                </v-col>
                <v-col cols="6" sm="1">
                  <v-text-field :model-value="fmt((item._precoVenda || 0) / 10)" label="Preço 100g"
                    prefix="R$" variant="outlined" density="compact" hide-details readonly
                    class="font-weight-bold text-success" />
                </v-col>
              </template>
            </template>

            <!-- Lote e Validade (etapa 3, junto da configuração de estoque) -->
            <v-col v-if="passo === 3" cols="6" sm="2">
              <v-text-field v-model="item._lote" label="Lote"
                variant="outlined" density="compact" hide-details
                :disabled="entrada?.status === 'Processada'"
                @update:model-value="item._alterado = true" />
            </v-col>
            <v-col v-if="passo === 3" cols="6" sm="2">
              <v-text-field v-model="item._validade" label="Validade" type="date"
                variant="outlined" density="compact" hide-details
                :disabled="entrada?.status === 'Processada'"
                @update:model-value="item._alterado = true" />
            </v-col>

          </v-row>
        </v-card>
      </div>
    </div>

    <!-- ─── ETAPA 5: FINANCEIRO ─── -->
    <div v-show="passo === 5">
      <v-card rounded="xl" elevation="1" class="pa-4 mb-4">
        <div class="d-flex justify-space-between align-center mb-3">
          <div class="text-subtitle-2 font-weight-bold">Faturas / Contas a Pagar</div>
          <v-btn v-if="entrada?.status !== 'Processada'"
            size="small" color="primary" variant="tonal"
            prepend-icon="mdi-plus" @click="adicionarFatura">
            Adicionar Fatura
          </v-btn>
        </div>

        <v-alert v-if="entrada?.status === 'Processada'" type="success" variant="tonal"
          density="compact" class="mb-3">
          Lançamentos registrados em Contas a Pagar.
        </v-alert>

        <!-- Classificação dos lançamentos gerados -->
        <v-row dense class="mb-2">
          <v-col cols="12" sm="4">
            <v-select v-model="categoriaFinanceira" label="Categoria (Contas a Pagar) *"
              :items="categoriasFinanceiras" variant="outlined" density="compact" hide-details
              :disabled="entrada?.status === 'Processada'" />
          </v-col>
          <v-col cols="12" sm="4">
            <v-select v-model="formaPagamento" label="Forma de pagamento"
              :items="formasPagamento" variant="outlined" density="compact" hide-details clearable
              :disabled="entrada?.status === 'Processada'" />
          </v-col>
        </v-row>
        <v-divider class="mb-3" />

        <div v-for="(f, i) in faturas" :key="i" class="d-flex align-center gap-2 mb-2">
          <v-text-field v-model="f.label" :label="`Fatura ${i + 1}`"
            variant="outlined" density="compact" hide-details readonly style="max-width:80px" />
          <v-text-field v-model.number="f.valor" label="Valor" type="number"
            variant="outlined" density="compact" hide-details prefix="R$" style="max-width:140px"
            :disabled="entrada?.status === 'Processada'" />
          <v-text-field v-model="f.vencimento" label="Vencimento" type="date"
            variant="outlined" density="compact" hide-details style="max-width:160px"
            :disabled="entrada?.status === 'Processada'" />
          <v-btn v-if="entrada?.status !== 'Processada'"
            icon="mdi-delete-outline" size="small" variant="text" color="error"
            @click="faturas.splice(i, 1)" />
        </div>

        <div v-if="faturas.length === 0" class="text-caption text-medium-emphasis text-center py-4">
          Nenhuma fatura adicionada. Use "+ Adicionar Fatura" para parcelar.
        </div>

        <v-divider class="my-3" />
        <div class="d-flex justify-space-between">
          <span class="text-body-2">Total das faturas:</span>
          <span class="font-weight-bold"
            :class="Math.abs(totalFaturas - (entrada?.valorTotal ?? 0)) > 0.01 ? 'text-error' : 'text-success'">
            R$ {{ fmt(totalFaturas) }}
            <span v-if="Math.abs(totalFaturas - (entrada?.valorTotal ?? 0)) > 0.01"
              class="text-caption ml-1">(diverge do total da nota: R$ {{ fmt(entrada?.valorTotal) }})</span>
          </span>
        </div>
      </v-card>
    </div>

    <!-- ─── ETAPA 6: FINALIZAÇÃO ─── -->
    <div v-show="passo === 6">
      <v-card rounded="xl" elevation="1" class="pa-4">
        <template v-if="entrada?.status === 'Processada'">
          <div class="text-center py-6">
            <v-icon icon="mdi-check-decagram" color="success" size="64" class="mb-3" />
            <div class="text-h6 font-weight-bold mb-1">Entrada escriturada com sucesso!</div>
            <div class="text-body-2 text-medium-emphasis">
              Estoque atualizado, custos dos produtos atualizados, movimentação registrada
              e contas a pagar geradas.
            </div>
            <v-btn class="mt-4" color="primary" variant="tonal" prepend-icon="mdi-arrow-left"
              @click="$router.push('/fiscal')">Voltar aos Documentos Fiscais</v-btn>
          </div>
        </template>
        <template v-else>
          <div class="text-subtitle-2 font-weight-bold mb-3">Resumo da entrada</div>
          <v-row dense>
            <v-col cols="6" sm="3">
              <v-text-field :model-value="itensEditaveis.length" label="Itens"
                variant="outlined" density="compact" readonly hide-details />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field :model-value="'R$ ' + fmt(entrada?.valorTotal)" label="Total da nota"
                variant="outlined" density="compact" readonly hide-details />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field :model-value="'R$ ' + fmt(entrada?.freteTotal)" label="Frete (rateado)"
                variant="outlined" density="compact" readonly hide-details />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field :model-value="faturas.length + 'x — R$ ' + fmt(totalFaturas)"
                label="Contas a pagar" variant="outlined" density="compact" readonly hide-details />
            </v-col>
          </v-row>

          <v-alert type="warning" variant="tonal" density="compact" class="mt-4">
            Ao <b>Finalizar</b>: o estoque será atualizado, os custos dos produtos serão gravados,
            a movimentação será registrada e as <b>contas a pagar</b> serão lançadas.
            Depois disso a nota fica travada (use <b>Estornar</b> para reverter).
          </v-alert>

          <v-switch v-model="lancarFinanceiro" color="primary" density="compact" hide-details
            class="mt-2"
            :label="lancarFinanceiro ? 'Lançar contas a pagar no financeiro' : 'NÃO lançar financeiro agora'" />
        </template>
      </v-card>
    </div>

    <!-- ─── NAVEGAÇÃO DO WIZARD ─── -->
    <div v-if="entrada?.status === 'EmEdicao'" class="d-flex align-center justify-space-between mt-4">
      <v-btn variant="text" :disabled="passo === 1" prepend-icon="mdi-chevron-left"
        @click="passoAnterior">Voltar</v-btn>

      <div class="d-flex align-center gap-2">
        <span v-if="!podeAvancar && passo === 2" class="text-caption text-error">
          Vincule ou cadastre todos os produtos ({{ itensSemProduto }} pendente(s)).
        </span>
        <span v-if="!podeAvancar && passo === 3" class="text-caption text-error">
          Defina unidade de estoque e fator de conversão em todos os itens.
        </span>

        <v-btn v-if="itensAlterados > 0 && passo >= 2 && passo <= 4"
          variant="tonal" :loading="salvandoTodos" prepend-icon="mdi-content-save"
          @click="salvarTodos">Salvar itens ({{ itensAlterados }})</v-btn>

        <v-btn v-if="passo < 6" color="primary" size="large" rounded="lg"
          append-icon="mdi-chevron-right" :disabled="!podeAvancar" :loading="avancando"
          @click="proximoPasso">
          {{ passo === 1 ? 'Processar Nota Fiscal' : 'Avançar' }}
        </v-btn>
        <v-btn v-else color="success" size="large" rounded="lg" :loading="processando"
          prepend-icon="mdi-check-all" @click="finalizarEntrada">
          Finalizar Entrada
        </v-btn>
      </div>
    </div>


    <!-- Dialog: Frete Manual -->
    <v-dialog v-model="dlgFrete" max-width="360" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="primary">mdi-truck-outline</v-icon>
          Frete Manual
        </v-card-title>
        <v-card-text>
          <v-text-field v-model="freteManualStr" label="Valor do frete (R$)"
            variant="outlined" density="compact" autofocus
            placeholder="0,00"
            @keydown.enter="salvarFreteManual" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgFrete = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvandoFrete" @click="salvarFreteManual">
            Salvar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Criar produto a partir do XML -->
    <v-dialog v-model="dlgCriarProduto" max-width="620" persistent scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="primary">mdi-package-variant-plus</v-icon>Criar produto
          <div class="text-caption text-medium-emphasis mt-1">Dados pré-preenchidos do XML da NF-e</div>
        </v-card-title>
        <v-card-text class="pt-2">
          <v-tabs v-model="abaCriarProduto" density="compact" class="mb-3">
            <v-tab value="geral">Geral</v-tab>
            <v-tab value="fiscal">Fiscal</v-tab>
            <v-tab value="fornecedor">Fornecedor</v-tab>
          </v-tabs>

          <!-- Aba Geral -->
          <v-window v-model="abaCriarProduto">
            <v-window-item value="geral">
              <v-row dense>
                <v-col cols="12">
                  <v-text-field v-model="novoProduto.descricao" label="Descrição *"
                    variant="outlined" density="compact" />
                </v-col>
                <v-col cols="6">
                  <v-text-field v-model="novoProduto.codigo" label="Código interno"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6">
                  <v-text-field v-model="novoProduto.codigoBarras" label="Código de barras (EAN)"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6" class="mt-3">
                  <v-select v-model="novoProduto.unidadeMedidaId" label="Unidade *"
                    :items="unidadesMedida" item-title="sigla" item-value="id"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6" class="mt-3">
                  <v-select v-model="novoProduto.categoriaId" label="Categoria"
                    :items="categorias" item-title="nome" item-value="id"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6" class="mt-3">
                  <v-select v-model="novoProduto.marcaId" label="Marca"
                    :items="marcas" item-title="nome" item-value="id"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6" class="mt-3">
                  <v-text-field v-model.number="novoProduto.custoUnitario" label="Custo unit. (R$)"
                    type="number" variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6" class="mt-3">
                  <v-text-field v-model.number="novoProduto.precoVenda" label="Preço de venda (R$)"
                    type="number" variant="outlined" density="compact" hide-details />
                </v-col>
              </v-row>
            </v-window-item>

            <!-- Aba Fiscal -->
            <v-window-item value="fiscal">
              <v-row dense>
                <v-col cols="6">
                  <v-text-field v-model="novoProduto.ncm" label="NCM"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6">
                  <v-text-field v-model="novoProduto.cfop" label="CFOP"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6" class="mt-3">
                  <v-text-field v-model="novoProduto.csosnIcms" label="CSOSN (Simples Nac.)"
                    variant="outlined" density="compact" hide-details
                    hint="Ex: 400 = outras operações" />
                </v-col>
                <v-col cols="6" class="mt-3">
                  <v-text-field v-model="novoProduto.cstIcms" label="CST ICMS (Lucro Pres.)"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="6" class="mt-3">
                  <v-text-field v-model="novoProduto.cstPisCofins" label="CST PIS/COFINS"
                    variant="outlined" density="compact" hide-details
                    hint="Ex: 07 = isento" />
                </v-col>
                <v-col cols="6" class="mt-3">
                  <v-select v-model="novoProduto.origem" label="Origem"
                    :items="[{t:'0 - Nacional',v:'0'},{t:'1 - Estrangeira (importação direta)',v:'1'},{t:'2 - Estrangeira (mercado interno)',v:'2'}]"
                    item-title="t" item-value="v"
                    variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="4" class="mt-3">
                  <v-text-field v-model.number="novoProduto.aliquotaIcms" label="Alíq. ICMS %"
                    type="number" variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="4" class="mt-3">
                  <v-text-field v-model.number="novoProduto.aliquotaPis" label="Alíq. PIS %"
                    type="number" variant="outlined" density="compact" hide-details />
                </v-col>
                <v-col cols="4" class="mt-3">
                  <v-text-field v-model.number="novoProduto.aliquotaCofins" label="Alíq. COFINS %"
                    type="number" variant="outlined" density="compact" hide-details />
                </v-col>
              </v-row>
            </v-window-item>

            <!-- Aba Fornecedor -->
            <v-window-item value="fornecedor">
              <div v-if="novoProduto._fornecedorExistente">
                <v-alert type="success" variant="tonal" rounded="lg" class="mb-3">
                  <strong>Fornecedor já cadastrado</strong><br>
                  {{ novoProduto._fornecedorExistente.razaoSocial }}
                </v-alert>
                <v-checkbox v-model="novoProduto._vincularFornecedor"
                  label="Definir como fornecedor principal deste produto" hide-details />
              </div>
              <div v-else>
                <v-alert type="info" variant="tonal" rounded="lg" class="mb-3">
                  <strong>Emitente da NF-e</strong><br>
                  {{ entrada?.emitenteNome }}<br>
                  <span class="text-caption">CNPJ: {{ entrada?.emitenteCnpj }}</span>
                </v-alert>
                <v-checkbox v-model="novoProduto._cadastrarFornecedor"
                  label="Cadastrar como fornecedor e definir como principal" hide-details />
              </div>
            </v-window-item>
          </v-window>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgCriarProduto = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvandoProduto" @click="salvarNovoProduto">
            Criar e vincular
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Estorno -->
    <v-dialog v-model="dlgEstorno" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="warning">mdi-undo</v-icon>
          Estornar Entrada
        </v-card-title>
        <v-card-text>
          <v-alert type="warning" variant="tonal" density="compact" class="mb-3">
            Isso irá estornar as movimentações de estoque e cancelar os lançamentos financeiros em aberto.
          </v-alert>
          <v-textarea v-model="motivoEstorno" label="Motivo do estorno *"
            rows="3" variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgEstorno = false">Cancelar</v-btn>
          <v-btn color="warning" rounded="lg" :loading="estornando" @click="estornar">
            Confirmar Estorno
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Imprimir Etiquetas -->
    <v-dialog v-model="dlgEtiquetas" max-width="480">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="primary">mdi-printer</v-icon>
          Imprimir Etiquetas
        </v-card-title>
        <v-card-text>
          <div class="text-body-2 mb-3">
            Serão geradas etiquetas para todos os {{ entrada?.itens?.length }} itens desta entrada.
          </div>
          <v-select v-model="templateEtiqueta" label="Template de etiqueta"
            :items="templatesEtiqueta" item-title="nome" item-value="id"
            variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgEtiquetas = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" @click="imprimirEtiquetas">
            <v-icon start>mdi-printer</v-icon>
            Gerar PDF
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Devolução -->
    <v-dialog v-model="dlgDevolucao" max-width="520" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="warning">mdi-arrow-u-left-top</v-icon>
          Devolver Mercadoria
        </v-card-title>
        <v-card-text>
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            Será gerada uma NF-e de devolução modelo 55. Após a devolução, acesse
            <strong>Documentos Fiscais → Emitidas</strong> para assinar e transmitir.
          </v-alert>
          <div class="text-body-2 mb-2 font-weight-medium">Selecione os itens a devolver:</div>
          <v-checkbox v-for="item in itensProdutoVinculado" :key="item.id"
            v-model="itensDevolucao" :value="item.id"
            :label="`${item.produtoDescricao} — ${item.quantidadeEstoque} ${item.unidadeEstoque ?? item.unidadeXml}`"
            density="compact" hide-details />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgDevolucao = false" :disabled="devolvendo">Cancelar</v-btn>
          <v-btn color="warning" rounded="lg" :loading="devolvendo"
            :disabled="!itensDevolucao.length" @click="confirmarDevolucao">
            Gerar NF-e de Devolução
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import { formatarCnpj } from '@/utils/documento'
import GuiaPassos from '@/components/GuiaPassos.vue'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const notif = useNotifStore()

const entradaId = route.params.id as string
const entrada = ref<any>(null)
const processando = ref(false)

// ─── Wizard (assistente de escrituração) ────────────────────────────────────
// Ordem: os produtos são vinculados PRIMEIRO (para herdar unidade e markup do
// cadastro), depois a conversão de estoque e só então custos/markup/preços.
// Material de consumo não é vendido: não tem conversão de unidade de venda,
// markup nem preço. O passo 3 é pulado e o 4 mostra só custo — os números dos
// passos seguem os mesmos, só a exibição muda.
const passosWizard = computed(() => {
  const todos = [
    { n: 1, label: 'Dados da Nota' },
    { n: 2, label: ehMaterialConsumo.value ? 'Materiais' : ehAtivoImobilizado.value ? 'Bens' : 'Produtos' },
    { n: 3, label: 'Fator de Conversão' },
    { n: 4, label: ehUsoInterno.value ? 'Custos' : 'Custos e Preços' },
    { n: 5, label: 'Financeiro' },
    { n: 6, label: 'Finalização' },
  ]
  return ehUsoInterno.value ? todos.filter(p => p.n !== 3) : todos
})
const passo = ref(1)
const avancando = ref(false)
const cadastrandoTodos = ref(false)
const vinculandoAuto = ref(false)
const reimportando = ref(false)

/** Reimporta os itens do XML quando a entrada ficou vazia (escriturada antes do XML). */
async function reimportarItens() {
  reimportando.value = true
  try {
    const r = await api.post(`/fiscal/entradas/${entradaId}/reimportar-itens`)
    notif.ok(`${r.data.itens} item(ns) importado(s) do XML`
      + (r.data.vinculados ? `, ${r.data.vinculados} vinculado(s) a produtos.` : '.'))
    await carregar()
    enriquecerItensComProduto()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao reimportar itens.')
  } finally { reimportando.value = false }
}

/**
 * Filtro do autocomplete por PALAVRAS, não por trecho contínuo. A descrição da
 * nota quase nunca é igual à do cadastro ("castanha caju sem sal" ×
 * "CASTANHA DE CAJU W1 240 TORRADA SEM SAL GREENLINE (NACIONAL)-11,34KG"), e o
 * filtro padrão do Vuetify exige substring — o produto existia e não aparecia.
 */
function filtrarPorPalavras(valor: string, consulta: string, item?: any) {
  const texto = normalizarTexto(`${valor ?? ''} ${item?.raw?.codigo ?? ''} ${item?.raw?.codigoBarras ?? ''}`)
  const palavras = normalizarTexto(consulta ?? '').split(' ').filter(Boolean)
  return palavras.every(p => texto.includes(p))
}

/** Minúsculas, sem acento e sem pontuação — para comparar texto digitado × cadastro. */
function normalizarTexto(s: string) {
  return (s ?? '').toLowerCase()
    .normalize('NFD').replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9\s]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
}

// Sugestões de produto parecido por item (evita cadastrar duplicado)
const sugestoes = ref<Record<string, any[]>>({})

async function carregarSugestoes() {
  if (ehUsoInterno.value || entrada.value?.status !== 'EmEdicao') return
  const pendentes = itensEditaveis.value.filter((i: any) => !i._produtoId)
  if (!pendentes.length) return

  const achados: Record<string, any[]> = {}
  await Promise.all(pendentes.map(async (i: any) => {
    try {
      const r = await api.get('/produtos/similares', {
        params: { empresaId: auth.empresaId, descricao: i.descricaoXml, limite: 3 },
      })
      if (r.data?.length) achados[i.id] = r.data
    } catch { /* sem sugestão para este item */ }
  }))
  sugestoes.value = achados
}

// ── Tipo da entrada: mercadoria (Produtos), material de consumo ou ativo ────
const ehMaterialConsumo = computed(() => entrada.value?.tipoEntrada === 'MaterialConsumo')
const ehAtivoImobilizado = computed(() => entrada.value?.tipoEntrada === 'AtivoImobilizado')
/** Material e ativo não são vendidos: sem conversão de unidade, markup ou preço. */
const ehUsoInterno = computed(() => ehMaterialConsumo.value || ehAtivoImobilizado.value)

const ativos = ref<any[]>([])
async function carregarAtivos() {
  try {
    const r = await api.get('/ativos-imobilizados', {
      params: { empresaId: auth.empresaId, ativo: true },
    })
    ativos.value = r.data.itens ?? []
  } catch { ativos.value = [] }
}

/** Cadastra os bens que faltam a partir dos itens da nota e vincula. */
const cadastrandoAtivos = ref(false)
async function cadastrarAtivosFaltantes() {
  cadastrandoAtivos.value = true
  try {
    const r = await api.post(`/fiscal/entradas/${entradaId}/ativos/cadastrar-faltantes`,
      { categoria: 'Equipamento', vidaUtilMeses: 60 })
    notif.ok(`${r.data.criados} bem(ns) cadastrado(s) e vinculado(s). `
      + 'Ajuste categoria e vida útil em Estoque → Ativo Imobilizado.')
    await carregar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao cadastrar bens.')
  } finally { cadastrandoAtivos.value = false }
}

/** Vincula o item a um bem já cadastrado. */
async function onAtivoInline(item: any, ativoId: string | null) {
  if (!ativoId) { item.ativoImobilizadoId = null; return }
  try {
    await api.patch(`/fiscal/entradas/${entradaId}/itens/${item.id}/ativo`,
      { ativoImobilizadoId: ativoId })
    await carregar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao vincular bem.')
  }
}

const materiais = ref<any[]>([])
async function carregarMateriais() {
  try {
    const r = await api.get('/materiais-consumo', {
      params: { empresaId: auth.empresaId, ativo: true },
    })
    materiais.value = r.data ?? []
  } catch { materiais.value = [] }
}


/** Vincula o item a um material já cadastrado. */
async function onMaterialInline(item: any, materialId: string | null) {
  if (!materialId) { item.materialConsumoId = null; return }
  try {
    await api.patch(`/fiscal/entradas/${entradaId}/itens/${item.id}/material`,
      { materialConsumoId: materialId })
    const m = materiais.value.find((x: any) => x.id === materialId)
    item.materialConsumoId = materialId
    item._produtoDescricao = m?.descricao ?? ''
    await carregar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao vincular material.')
  }
}

async function definirTipoEntrada(tipo: string) {
  if (!tipo || tipo === entrada.value?.tipoEntrada) return
  const rotulos: Record<string, string> = {
    Mercadoria: 'Mercadoria para venda',
    MaterialConsumo: 'Material de consumo',
    AtivoImobilizado: 'Ativo imobilizado',
  }
  const temVinculo = (itensEditaveis.value ?? []).some(
    (i: any) => i._produtoId || i.materialConsumoId || i.ativoImobilizadoId)
  if (temVinculo && !confirm(
    `Trocar para "${rotulos[tipo]}" desfaz os vínculos dos itens, `
    + 'porque os cadastros são diferentes. Continuar?')) return

  try {
    const r = await api.patch(`/fiscal/entradas/${entradaId}/tipo-entrada`, { tipo })
    await carregar()
    enriquecerItensComProduto()

    const destino: Record<string, string> = {
      Mercadoria: 'itens vão para o cadastro de Produtos.',
      MaterialConsumo: 'itens vão para o estoque de uso interno.',
      AtivoImobilizado: 'itens viram bens da empresa (sem estoque).',
    }
    notif.ok(`Nota marcada como ${rotulos[tipo]} — ${destino[tipo]}`
      + (r.data.cfopsCorrigidos ? ` CFOP de ${r.data.cfopsCorrigidos} item(ns) corrigido(s).` : ''))
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao definir o tipo da entrada.')
  }
}

/** Itens ainda com CFOP de venda (5xxx/6xxx) numa nota de uso e consumo. */
const itensCfopVenda = computed(() => ehUsoInterno.value
  ? itensEditaveis.value.filter((i: any) => /^[56]/.test(String(i._cfop ?? ''))).length
  : 0)

/** Corrige o CFOP dos itens (o backend deriva 1556/2556 do CFOP do emitente). */
const corrigindoCfop = ref(false)
async function corrigirCfopMaterial() {
  corrigindoCfop.value = true
  try {
    const r = await api.patch(`/fiscal/entradas/${entradaId}/tipo-entrada`, { tipo: 'MaterialConsumo' })
    await carregar()
    notif.ok(`CFOP de ${r.data.cfopsCorrigidos} item(ns) corrigido(s) para compra de uso/consumo.`)
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao corrigir o CFOP.')
  } finally { corrigindoCfop.value = false }
}

/** Cadastra os materiais que faltam a partir dos itens da nota e vincula. */
const cadastrandoMateriais = ref(false)
async function cadastrarMateriaisFaltantes() {
  const unidadeMedidaId = unidadesMedida.value?.find((u: any) => u.sigla === 'UN')?.id
    ?? unidadesMedida.value?.[0]?.id
  if (!unidadeMedidaId) { notif.erro('Nenhuma unidade de medida cadastrada.'); return }

  cadastrandoMateriais.value = true
  try {
    const r = await api.post(`/fiscal/entradas/${entradaId}/materiais/cadastrar-faltantes`, { unidadeMedidaId })
    notif.ok(`${r.data.criados} material(is) cadastrado(s), ${r.data.vinculados} vinculado(s).`)
    await carregar()   // recarrega a nota e a lista de materiais (para o autocomplete resolver os novos)
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao cadastrar materiais.')
  } finally { cadastrandoMateriais.value = false }
}

/** Re-executa a busca automática por produtos já cadastrados (EAN → de-para → código). */
async function vincularAutomatico() {
  vinculandoAuto.value = true
  try {
    const r = await api.post(`/fiscal/entradas/${entradaId}/vincular-automatico`)
    const { vinculados, pendentes, fornecedorNome, fornecedorNovo,
            contasCorrigidas, produtosVinculados } = r.data

    // Relata só o que realmente mudou
    const feito: string[] = []
    if (fornecedorNovo) feito.push(`fornecedor ${fornecedorNome} cadastrado e vinculado`)
    if (contasCorrigidas) feito.push(`${contasCorrigidas} conta(s) a pagar atualizada(s)`)
    if (produtosVinculados) feito.push(`${produtosVinculados} produto(s) vinculado(s) ao fornecedor`)
    if (vinculados) feito.push(`${vinculados} item(ns) vinculado(s) a produtos cadastrados`)

    if (feito.length) {
      notif.ok(feito.join(' · ') + '.' + (pendentes ? ` ${pendentes} item(ns) ainda sem vínculo.` : ''))
      await carregar()
      enriquecerItensComProduto()
    } else {
      notif.aviso(fornecedorNome
        ? `Nada a corrigir: fornecedor ${fornecedorNome}, contas e produtos já estão vinculados.`
        : 'Nenhum produto correspondente encontrado no cadastro.')
    }
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao vincular automaticamente.')
  } finally { vinculandoAuto.value = false }
}

// Até onde o usuário pode navegar (não pode pular etapas obrigatórias)
const passoMaximo = computed(() => {
  if (!localEstoqueId.value) return 1
  if (itensSemProduto.value > 0) return 2       // etapa 2: todos vinculados
  // Material de consumo não tem conversão de unidade de venda
  if (!ehUsoInterno.value && !todosFatoresOk.value) return 3
  return 6
})

const todosFatoresOk = computed(() =>
  itensEditaveis.value.length > 0 &&
  itensEditaveis.value.every((i: any) => !!i._unidade && (i._fator > 0)))

const podeAvancar = computed(() => {
  if (entrada.value?.status !== 'EmEdicao') return false
  switch (passo.value) {
    case 1: return !!localEstoqueId.value
    case 2: return itensSemProduto.value === 0   // produtos/materiais vinculados
    case 3: return todosFatoresOk.value          // fator de conversão
    default: return true
  }
})

/** Passo 3 (conversão) não existe na nota de material — a navegação pula. */
const passoOculto = (n: number) => ehUsoInterno.value && n === 3

function irParaPasso(n: number) {
  if (passoOculto(n)) n = 4
  if (entrada.value?.status === 'Processada') { passo.value = n; return }
  if (n <= passoMaximo.value) passo.value = n
}
function passoAnterior() {
  let n = passo.value - 1
  if (passoOculto(n)) n--
  if (n >= 1) passo.value = n
}

async function proximoPasso() {
  if (!podeAvancar.value) return
  avancando.value = true
  try {
    // Salva as alterações dos itens antes de avançar (etapas 2 a 4)
    if (itensAlterados.value > 0 && passo.value >= 2 && passo.value <= 4)
      await salvarTodos()
    let n = passo.value + 1
    if (passoOculto(n)) n++          // pula a conversão na nota de material
    if (n <= 6) passo.value = n
  } finally { avancando.value = false }
}

// Base do rateio = soma do valor dos itens (MESMA base do backend RatearFrete).
const baseRateioProdutos = computed(() =>
  itensEditaveis.value.reduce((s: number, i: any) => s + (i.valorTotalXml || 0), 0) || 1)

/** Rateio de frete PROPORCIONAL AO VALOR aplicado ao item (para conferência). */
function freteRateado(item: any) {
  const freteTotal = entrada.value?.freteTotal ?? 0
  return freteTotal * ((item.valorTotalXml || 0) / baseRateioProdutos.value)
}
/** Impostos que compõem o custo (editáveis; iniciam com os valores do XML). */
function impostosItem(item: any) {
  return (item._ipi ?? item.valorIpi ?? 0) + (item._icmsSt ?? item.valorIcmsSt ?? 0)
}
/** Custo final do item = valor de compra + frete rateado + impostos. */
function custoTotalItem(item: any) {
  return (item.valorTotalXml || 0) + freteRateado(item) + impostosItem(item)
}
/** Quantidade convertida para estoque. */
function qtdEstoque(item: any) {
  return (item.quantidadeXml || 0) * (item._fator || 1)
}
/** Custo unitário = custo final ÷ quantidade de estoque. */
function custoUnitario(item: any) {
  const q = qtdEstoque(item)
  return q > 0 ? custoTotalItem(item) / q : (item.custoUnitarioFinal || item.valorUnitarioXml || 0)
}
/** Preço de venda sugerido = custo unitário × (1 + markup%). */
function precoSugerido(item: any) {
  return custoUnitario(item) * (1 + (item._markup || 0) / 100)
}

// Markup ⇄ preço de venda: editar um recalcula o outro. O markup continua sendo
// o valor enviado ao backend (o preço é derivado dele lá).
function onMarkupChange(item: any) {
  item._precoVenda = Math.round(precoSugerido(item) * 100) / 100
  item._alterado = true
}
function onPrecoVendaChange(item: any) {
  const custo = custoUnitario(item)
  const preco = Number(item._precoVenda) || 0
  item._markup = custo > 0 ? Math.round((preco / custo - 1) * 10000) / 100 : 0
  item._alterado = true
}
/** Garante _precoVenda coerente com o markup atual (ao carregar/ao entrar na etapa 4). */
function sincronizarPrecoVenda(item: any) {
  if (item._precoVenda == null)
    item._precoVenda = Math.round(precoSugerido(item) * 100) / 100
}

/**
 * Indicador de variação do preço com base no CUSTO desta entrada.
 * Preço necessário para manter o markup atual = custo novo × (1 + markup atual).
 *  • acima do preço atual  → custo subiu, PRECISA aumentar (seta para cima).
 *  • abaixo do preço atual → custo caiu, PODE baixar ou manter (seta para baixo).
 */
function statusPreco(item: any) {
  const precoAtual = Number(item._precoAtual) || 0
  if (!precoAtual) {
    return { icone: 'mdi-new-box', cor: 'grey', bg: undefined,
      dica: 'Produto sem preço cadastrado — defina o preço de venda.' }
  }
  const necessario = custoUnitario(item) * (1 + (Number(item._markupAtual) || 0) / 100)
  const tol = precoAtual * 0.005
  if (necessario > precoAtual + tol) {
    return { icone: 'mdi-arrow-up-bold', cor: 'error', bg: 'red-lighten-5',
      dica: `Custo subiu: para manter o markup, aumente para ~R$ ${fmt(necessario)} `
        + `(atual R$ ${fmt(precoAtual)}).` }
  }
  if (necessario < precoAtual - tol) {
    return { icone: 'mdi-arrow-down-bold', cor: 'info', bg: 'blue-lighten-5',
      dica: `Custo caiu: dá para baixar até ~R$ ${fmt(necessario)} (ou manter os `
        + `R$ ${fmt(precoAtual)}).` }
  }
  return { icone: 'mdi-check-bold', cor: 'success', bg: undefined,
    dica: `Custo estável: o preço atual (R$ ${fmt(precoAtual)}) segue adequado.` }
}

/** Cadastra automaticamente todos os itens sem produto, usando os dados da NF-e. */
async function cadastrarTodosAutomaticamente() {
  const pendentes = itensEditaveis.value.filter((i: any) => !i._produtoId)
  if (!pendentes.length) return
  if (!confirm(`Cadastrar automaticamente ${pendentes.length} produto(s) com os dados da NF-e?`)) return

  cadastrandoTodos.value = true
  let criados = 0, falhas = 0
  try {
    for (const item of pendentes) {
      try {
        await criarProdutoDoItem(item)
        criados++
      } catch { falhas++ }
    }
    if (criados) await salvarTodos()
    notif.ok(`${criados} produto(s) cadastrado(s) e vinculado(s).`
      + (falhas ? ` ${falhas} falharam — cadastre manualmente.` : ''))
    await carregarAuxiliares()
  } finally { cadastrandoTodos.value = false }
}

async function finalizarEntrada() {
  processando.value = true
  try {
    if (itensAlterados.value > 0) await salvarTodos()
    const faturasEnviar = lancarFinanceiro.value
      ? faturas.value.map(f => ({ valor: f.valor, vencimento: f.vencimento }))
      : []
    await api.post(`/fiscal/entradas/${entradaId}/processar`, {
      faturas: faturasEnviar,
      categoria: lancarFinanceiro.value ? categoriaFinanceira.value : null,
      formaPagamento: lancarFinanceiro.value ? formaPagamento.value : null,
    })
    notif.ok('Entrada escriturada com sucesso!')
    await carregar()
    passo.value = 6
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao finalizar a entrada.')
  } finally { processando.value = false }
}
const estornando = ref(false)
const devolvendo = ref(false)
const dlgDevolucao = ref(false)
const itensDevolucao = ref<string[]>([])
const itensProdutoVinculado = computed(() =>
  (entrada.value?.itens ?? []).filter((i: any) => i.produtoId))

// Configuração Fiscal da empresa (tributação padrão para novos produtos)
const configFiscal = ref<any>(null)

/**
 * Tributação padrão vinda do cadastro base (Configurações → Fiscal).
 * Simples Nacional usa CSOSN; Lucro Presumido/Real usa CST ICMS.
 * Se não houver config, cai nos padrões conservadores.
 */
function tributacaoPadrao(cfopItem?: string) {
  const c = configFiscal.value
  const simples = (c?.regime ?? 'SimplesNacional') === 'SimplesNacional'
  return {
    origem:        c?.origemPadrao ?? '0',
    csosnIcms:     simples ? (c?.csosnPadrao ?? '400') : '',
    cstIcms:       simples ? '' : (c?.cstIcmsPadrao ?? '00'),
    aliquotaIcms:  c?.aliquotaIcmsPadrao ?? 0,
    cstPisCofins:  c?.cstPisPadrao ?? '07',
    aliquotaPis:   c?.aliquotaPisPadrao ?? 0,
    aliquotaCofins: c?.aliquotaCofinsPadrao ?? 0,
    cfop:          c?.cfopVendaEstadual || cfopItem || '',
  }
}

// Listas auxiliares
const locaisEstoque = ref<any[]>([])
const unidades = ref<string[]>([])
const unidadesMedida = ref<any[]>([])
const categorias = ref<any[]>([])
const marcas = ref<any[]>([])
const pedidosCompra = ref<any[]>([])
const produtos = ref<any[]>([])
const templatesEtiqueta = ref<any[]>([])

// Dialog criar produto a partir do XML
const dlgCriarProduto = ref(false)
const itemCriando = ref<any>(null)
const novoProduto = ref<any>({})
const salvandoProduto = ref(false)
const abaCriarProduto = ref('geral')

// Formulário frete e vínculos
const freteManual = ref(0)
const freteManualStr = ref('0')
const localEstoqueId = ref<string | null>(null)
const pedidoCompraId = ref<string | null>(null)

// Financeiro
const faturas = ref<{ label: string; valor: number; vencimento: string }[]>([])
const totalFaturas = computed(() => faturas.value.reduce((s, f) => s + (f.valor || 0), 0))
const lancarFinanceiro = ref(true)

// Classificação dos lançamentos de contas a pagar gerados pela entrada
const categoriasFinanceiras = ['Despesas Administrativas', 'Despesas Operacionais', 'Despesas Variáveis', 'Pessoas', 'Impostos']
const formasPagamento = ['Boleto', 'PIX', 'Transferência', 'Dinheiro', 'Cartão', 'Cheque']
const categoriaFinanceira = ref('Despesas Variáveis')  // compra de mercadoria
const formaPagamento = ref<string | null>('Boleto')

// Dialogs
const dlgFrete = ref(false)
const dlgEstorno = ref(false)
const dlgEtiquetas = ref(false)
const motivoEstorno = ref('')
const templateEtiqueta = ref<string | null>(null)

// Grid editável de itens
const itensEditaveis = ref<any[]>([])
const salvandoTodos = ref(false)
const salvandoFrete = ref(false)
const markupGlobal = ref(150)

// O vínculo do item depende do tipo da nota: produto, material ou bem
const itensSemProduto = computed(() => {
  const t = entrada.value?.tipoEntrada
  if (t === 'MaterialConsumo') return itensEditaveis.value.filter((i: any) => !i.materialConsumoId).length
  if (t === 'AtivoImobilizado') return itensEditaveis.value.filter((i: any) => !i.ativoImobilizadoId).length
  return itensEditaveis.value.filter((i: any) => !i._produtoId).length
})

const itensAlterados = computed(
  () => itensEditaveis.value.filter((i: any) => i._alterado).length)

function corStatus(s: string) {
  return ({ EmEdicao: 'warning', Processada: 'success', Estornada: 'error' } as any)[s] ?? 'default'
}

const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
const fmtQtd = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 0, maximumFractionDigits: 4 })
const fmtData = (v: string) => v ? new Date(v).toLocaleDateString('pt-BR') : '-'
const fmtCnpj = formatarCnpj

function popularItensEditaveis(itens: any[]) {
  itensEditaveis.value = itens.map((i: any) => ({
    ...i,
    _produtoId: i.produtoId ?? null,
    _produtoDescricao: i.produtoDescricao ?? '',
    _cfop: i.cfopUtilizado,
    _fator: i.fatorConversao ?? 1,
    _unidade: i.unidadeEstoque ?? i.unidadeXml,
    _lote: i.numeroLote ?? '',
    _validade: i.validade ? i.validade.slice(0, 10) : '',
    _markup: i.markupSugerido ? Math.round((i.markupSugerido - 1) * 10000) / 100 : 150,
    // Impostos que compõem o custo — começam com os valores do XML e são editáveis
    _ipi: i.valorIpi ?? 0,
    _icmsSt: i.valorIcmsSt ?? 0,
    _alterado: false,
  }))
}

async function carregar() {
  const r = await api.get(`/fiscal/entradas/${entradaId}`)
  entrada.value = r.data
  freteManual.value = r.data.valorFreteManual ?? 0
  freteManualStr.value = String(freteManual.value)
  localEstoqueId.value = r.data.localEstoqueId ?? null
  pedidoCompraId.value = r.data.pedidoCompraId ?? null
  popularItensEditaveis(r.data.itens ?? [])

  // Nota de uso interno: a etapa 2 vincula materiais/bens, não produtos. Recarrega
  // sempre — a lista precisa conter os itens vinculados, senão o autocomplete
  // exibe o id em vez da descrição.
  if (r.data.tipoEntrada === 'MaterialConsumo') await carregarMateriais()
  else if (r.data.tipoEntrada === 'AtivoImobilizado') await carregarAtivos()
  else carregarSugestoes()   // nota de mercadoria: sugere produtos parecidos já cadastrados

  // Nota já processada → abre direto na etapa de finalização (somente leitura)
  if (r.data.status === 'Processada') passo.value = 6

  // Faturas da etapa Financeiro, a partir das duplicatas do XML (salvas no banco)
  const dups: any[] = r.data.duplicatas ?? []
  const daDuplicata = (d: any, i: number) => ({
    label: d.numero ?? d.Numero ?? String(i + 1),
    valor: d.valor ?? d.Valor ?? 0,
    vencimento: (d.vencimento ?? d.Vencimento ?? '')?.slice(0, 10) || '',
  })

  if (r.data.status !== 'EmEdicao') {
    // Processada/estornada: a etapa é só leitura e mostra o que a nota trouxe.
    // Antes ficava vazia e parecia que as parcelas não vieram, embora o contas
    // a pagar já tivesse sido lançado.
    faturas.value = dups.map(daDuplicata)
  } else if (faturas.value.length === 0) {
    faturas.value = dups.length > 0
      ? dups.map(daDuplicata).map(f => ({
          ...f, vencimento: f.vencimento || new Date().toISOString().slice(0, 10),
        }))
      // Nota sem duplicata: sugere parcela única em 30 dias
      : [{ label: '1', valor: r.data.valorTotal, vencimento: emDias(30) }]
  }
}

/** Data de hoje + N dias no formato yyyy-MM-dd. */
function emDias(n: number) {
  const d = new Date()
  d.setDate(d.getDate() + n)
  return d.toISOString().slice(0, 10)
}

async function carregarAuxiliares() {
  const [locais, pedidos, prods, unds, cats, mrcs, cfg] = await Promise.all([
    api.get('/locais-estoque', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] })),
    api.get('/compras/pedidos', { params: { empresaId: auth.empresaId, status: 'Enviado' } }).catch(() => ({ data: [] })),
    api.get('/produtos', { params: { empresaId: auth.empresaId, tamanhoPagina: 2000 } }).catch(() => ({ data: [] })),
    api.get('/unidades-medida', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] })),
    api.get('/categorias', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] })),
    api.get('/marcas', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] })),
    // Tributação padrão da empresa (Configurações → Fiscal)
    api.get('/fiscal/configuracao', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: null })),
  ])
  configFiscal.value = cfg.data ?? null
  locaisEstoque.value = locais.data
  pedidosCompra.value = pedidos.data.map((p: any) => ({
    ...p,
    label: `OC #${p.numero} – ${fmtData(p.dataPedido)}`,
  }))
  produtos.value = prods.data?.itens ?? prods.data ?? []
  unidadesMedida.value = unds.data.items ?? unds.data
  unidades.value = unidadesMedida.value.map((u: any) => u.sigla ?? u.nome ?? u)
  categorias.value = cats.data.items ?? cats.data
  marcas.value = mrcs.data.items ?? mrcs.data
}

function abrirDlgFrete() {
  if (entrada.value?.status === 'Processada') return
  freteManualStr.value = String(freteManual.value)
  dlgFrete.value = true
}

async function salvarLocalEstoque(id: string | null) {
  if (!id) return
  await api.patch(`/fiscal/entradas/${entradaId}/local-estoque`, { localEstoqueId: id })
    .catch(() => {})
}

async function salvarFreteManual() {
  const valor = parseFloat(freteManualStr.value.replace(',', '.')) || 0
  salvandoFrete.value = true
  try {
    await api.patch(`/fiscal/entradas/${entradaId}/frete-manual`, { valor })
    notif.ok('Frete atualizado.')
    dlgFrete.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao salvar frete.')
  } finally {
    salvandoFrete.value = false
  }
}

async function salvarPedidoCompra(id: string | null) {
  if (!id) return
  await api.patch(`/fiscal/entradas/${entradaId}/pedido-compra`, { pedidoCompraId: id })
    .then(() => notif.ok('Ordem de compra vinculada.'))
}

// Aplica os padrões do produto cadastrado ao item: unidade de estoque e markup.
function aplicarPadraoProduto(item: any, prod: any) {
  if (!prod) return
  item._produtoDescricao = prod.descricao
  // Unidade de estoque = a do produto cadastrado (não a unidade comercial do XML)
  const sigla = prod.unidadeSigla ?? prod.unidadeMedida
  if (sigla) item._unidade = sigla
  // Markup = o último cadastrado no produto (multiplicador → %); mantém o atual se não houver
  if (prod.markup && prod.markup > 1) item._markup = Math.round((prod.markup - 1) * 100)
  // Referências para o indicador de variação: preço e markup atuais do cadastro
  item._precoAtual = prod.precoVenda || 0
  item._markupAtual = prod.markup && prod.markup > 1 ? Math.round((prod.markup - 1) * 100) : (item._markup || 0)
  item._precoVenda = Math.round(precoSugerido(item) * 100) / 100   // preço acompanha o markup
}

async function onProdutoInline(item: any, id: string | null) {
  const prod = produtos.value.find((p: any) => p.id === id)
  if (prod) aplicarPadraoProduto(item, prod)
  item._produtoId = id
  item._alterado = true
  // Vinculou agora: já traz a conversão usada na última entrada deste produto
  if (id) await aplicarConversaoDoProduto(item, id)
}

/** Aplica ao item a conversão (fator + unidade) da última entrada do produto. */
async function aplicarConversaoDoProduto(item: any, produtoId: string) {
  if (ehUsoInterno.value || item.unidadeEstoque) return
  try {
    const r = await api.get('/fiscal/entradas/conversoes-anteriores', {
      params: { empresaId: auth.empresaId, produtoIds: produtoId },
    })
    const c = (r.data ?? [])[0]
    if (!c) return
    item._fator = c.fatorConversao || 1
    item._unidade = c.unidadeEstoque || item._unidade
    item._alterado = true
  } catch { /* segue sem pré-preencher */ }
}

// Para itens já vinculados a um produto, puxa unidade/markup do produto quando o
// item ainda não tem valores próprios salvos (evita cair na unidade do XML / markup fixo).
function enriquecerItensComProduto() {
  for (const item of itensEditaveis.value) {
    if (!item._produtoId) continue
    const prod = produtos.value.find((p: any) => p.id === item._produtoId)
    if (!prod) continue
    if (!item.unidadeEstoque) {
      const sigla = prod.unidadeSigla ?? prod.unidadeMedida
      if (sigla) item._unidade = sigla
    }
    if (!item.markupSugerido && prod.markup && prod.markup > 1)
      item._markup = Math.round((prod.markup - 1) * 100)
    if (item._precoAtual == null) item._precoAtual = prod.precoVenda || 0
    if (item._markupAtual == null)
      item._markupAtual = prod.markup && prod.markup > 1 ? Math.round((prod.markup - 1) * 100) : (item._markup || 0)
    sincronizarPrecoVenda(item)
  }
}

/**
 * Pré-preenche a conversão com a usada na última entrada do mesmo produto —
 * quem já recebeu "caixa com 12" não precisa informar de novo. Só preenche o
 * que ainda não foi configurado nesta nota; o usuário edita se vier diferente.
 */
async function aplicarConversoesAnteriores() {
  if (ehUsoInterno.value || entrada.value?.status !== 'EmEdicao') return
  const pendentes = itensEditaveis.value.filter((i: any) => i._produtoId && !i.unidadeEstoque)
  if (!pendentes.length) return

  try {
    const ids = [...new Set(pendentes.map((i: any) => i._produtoId))].join(',')
    const r = await api.get('/fiscal/entradas/conversoes-anteriores', {
      params: { empresaId: auth.empresaId, produtoIds: ids },
    })
    const porProduto = new Map<string, any>((r.data ?? []).map((c: any) => [c.produtoId, c]))

    let aplicados = 0
    for (const item of pendentes) {
      const c = porProduto.get(item._produtoId)
      if (!c) continue
      item._fator = c.fatorConversao || 1
      item._unidade = c.unidadeEstoque || item._unidade
      item._alterado = true
      aplicados++
    }
    if (aplicados > 0)
      notif.aviso(`${aplicados} item(ns) com a conversão da última entrada — confira e ajuste se mudou.`)
  } catch { /* segue sem pré-preencher */ }
}

const CATEGORIA_KEYWORDS: Record<string, string[]> = {
  'Grãos integrais':            ['arroz','feijao','feijão','lentilha','milho','quinoa','aveia','trigo','cevada','centeio','ervilha','grao','grão','grao-de-bico','graodebi','chia','linhaca','linhaça','farro','sorgo','amaranto'],
  'Ervas e especiarias':        ['colorau','paprica','páprica','curcuma','cúrcuma','acafrao','açafrão','pimenta','canela','gengibre','oregano','orégano','manjericao','alecrim','tomilho','cominho','coentro','noz-moscada','cravo','cardamomo','tempero','condimento','erva-doce','louro','salvia'],
  'Frutas secas e cristalizadas':['damasco','ameixa','uva passa','uvap','tamara','tâmara','figo','cranberry','goji','maca seca','banana seca','fruta seca','cristalizada','mirtilo','blueberry','maca desidrat','banana desidrat'],
  'Chips naturais':              ['chips','snack','palito','crocante','biscoito','crackers','bolinha'],
  'Oleaginosas e sementes':     ['castanha','amendoim','amendoa','amêndoa','nozes','pistache','macadamia','macadâmia','avela','avelã','gergelim','girassol','abobora','abóbora','semente','mix nuts','nuts','oleaginosa'],
  'Mel e adoçantes naturais':   ['mel','melado','rapadura','mascavo','demerara','xilitol','stevia','stévia','agave','melaco','melaço','frutose','sucralose','eritritol','coco acucar','açúcar coco'],
  'Chás e infusões':            ['cha','chá','infusao','infusão','camomila','erva-mate','hibisco','hortela','hortelã','cidreira','boldo','capim-limao','capim-limão','erva','melissa','tilia'],
  'Chocolates':                 ['chocolate','cacau','nibs','achocolatado','cacau em po','cacau pó','70%','meio amargo','amargo'],
  'Zero Lactose':               ['lactose','zero lactose','s/ lactose','sem lactose'],
  'Sem Glúten':                 ['gluten','glúten','sem gluten','s/ gluten'],
  'Suplementos':                ['proteina','proteína','whey','creatina','vitamina','suplemento','colageno','colágeno','omega','ômega','maltodextrina','bcaa','aminoacido','aminoácido','hipercalorico','hipercalórico'],
}

function sugerirCategoria(descricao: string): string | undefined {
  const lower = descricao.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '')
  for (const [nome, palavras] of Object.entries(CATEGORIA_KEYWORDS)) {
    const normalizado = palavras.map(p => p.normalize('NFD').replace(/[̀-ͯ]/g, ''))
    if (normalizado.some(p => lower.includes(p))) {
      return categorias.value.find((c: any) => c.nome === nome)?.id
    }
  }
  return undefined
}

/**
 * Cria o produto a partir dos dados da NF-e (sem diálogo) e vincula ao item.
 * Usado no "Cadastrar todos automaticamente" da etapa 3.
 * Se já existir produto com o mesmo código de barras, apenas vincula.
 */
// Busca automática da foto do produto pelo EAN (Open Food Facts), em segundo
// plano — não bloqueia nem falha a importação se não achar imagem.
function buscarFotoAuto(produtoId: string, ean: string | null) {
  if (!produtoId || !ean) return
  api.post(`/produtos/${produtoId}/imagem-codigo-barras`, { codigoBarras: ean },
    { _quiet: true } as any).catch(() => null)
}

async function criarProdutoDoItem(item: any) {
  const custo = custoDisplay(item)
  const ean = /^\d{8,14}$/.test(item.codigoBarras ?? '') ? item.codigoBarras : null

  // Já existe produto com este código de barras? → vincula
  if (ean) {
    const rb = await api.get('/produtos/buscar', {
      params: { empresaId: auth.empresaId, q: ean },
    }).catch(() => null)
    const existente = (rb?.data ?? []).find((p: any) => p.codigoBarras === ean)
    if (existente) {
      item._produtoId = existente.id
      item._produtoDescricao = existente.descricao
      item._alterado = true
      return
    }
  }

  // Garante categoria/marca/unidade
  let categoriaId = sugerirCategoria(item.descricaoXml ?? '') ?? categorias.value[0]?.id
  if (!categoriaId) {
    const rc = await api.post('/categorias', { empresaId: auth.empresaId, nome: 'Geral' })
    categorias.value = [...categorias.value, rc.data]
    categoriaId = rc.data.id
  }
  let marcaId = marcas.value[0]?.id
  if (!marcaId) {
    const rm = await api.post('/marcas', { empresaId: auth.empresaId, nome: 'Sem marca' })
    marcas.value = [...marcas.value, rm.data]
    marcaId = rm.data.id
  }
  const unidadeMedidaId = unidadesMedida.value.find((u: any) =>
    u.sigla === item._unidade || u.sigla === item.unidadeXml)?.id ?? unidadesMedida.value[0]?.id
  if (!unidadeMedidaId) throw new Error('Sem unidade de medida cadastrada.')

  const precoVenda = Math.round(custo * (1 + (item._markup || 150) / 100) * 100) / 100 || 0.01

  const ncmLimpo = (item.ncmXml ?? '').replace(/\D/g, '').slice(0, 8) || null

  const r = await api.post('/produtos', {
    empresaId: auth.empresaId,
    codigo: null,                     // backend gera código único
    descricao: item.descricaoXml,
    codigoBarras: ean,
    ncm: ncmLimpo,
    categoriaId, marcaId, unidadeMedidaId,
    custoUnitario: Math.round(custo * 100) / 100,
    precoVenda,
    // Fornecedor da nota + código do produto nela (de-para), para as próximas
    // entradas deste fornecedor vincularem sozinhas.
    fornecedorPrincipalId: entrada.value?.fornecedorId ?? null,
    codigoFornecedorPrincipal: item.codigoFornecedor ?? null,
  })
  const novoId = r.data.id ?? r.data.Id

  // Grava a tributação padrão (Configuração Fiscal) no produto recém-criado
  const trib = tributacaoPadrao(item._cfop)
  await api.put(`/produtos/${novoId}`, {
    empresaId: auth.empresaId,
    descricao: item.descricaoXml,
    categoriaId, marcaId, unidadeMedidaId,
    custoUnitario: Math.round(custo * 100) / 100,
    precoVenda,
    ncm: ncmLimpo,
    // Fornecedor da nota + de-para (código do produto no fornecedor), para as
    // próximas entradas deste fornecedor vincularem sozinhas.
    fornecedorPrincipalId: entrada.value?.fornecedorId ?? null,
    cfop: trib.cfop || null,
    origem: trib.origem,
    csosnIcms: trib.csosnIcms || null,
    cstIcms: trib.cstIcms || null,
    cstPisCofins: trib.cstPisCofins || null,
    aliquotaIcms: trib.aliquotaIcms,
    aliquotaPis: trib.aliquotaPis,
    aliquotaCofins: trib.aliquotaCofins,
  }, { _quiet: true } as any).catch(() => null)

  // Foto automática pelo EAN (background)
  buscarFotoAuto(novoId, ean)

  // Vincula ao item e adiciona à lista local
  item._produtoId = novoId
  item._produtoDescricao = item.descricaoXml
  item._alterado = true
  if (!produtos.value.find((p: any) => p.id === novoId))
    produtos.value = [...produtos.value, { id: novoId, descricao: item.descricaoXml, codigo: r.data.codigo ?? '' }]
}

async function abrirCriarProduto(item: any) {
  itemCriando.value = item
  abaCriarProduto.value = 'geral'
  const custo = custoDisplay(item)

  // Verificar se emitente já é fornecedor cadastrado
  let fornecedorExistente: any = null
  if (entrada.value?.emitenteCnpj) {
    const cnpjLimpo = entrada.value.emitenteCnpj.replace(/\D/g, '')
    const rf = await api.get('/fornecedores', {
      params: { empresaId: auth.empresaId, termo: cnpjLimpo },
    }).catch(() => null)
    const lista = rf?.data?.items ?? rf?.data ?? []
    fornecedorExistente = lista.find((f: any) =>
      (f.cnpj ?? '').replace(/\D/g, '') === cnpjLimpo) ?? null
  }

  novoProduto.value = {
    // Geral
    descricao: item.descricaoXml ?? '',
    codigo: '',
    codigoBarras: item.codigoBarras ?? '',
    unidadeMedidaId: unidadesMedida.value.find((u: any) =>
      u.sigla === item._unidade || u.sigla === item.unidadeXml)?.id ?? '',
    categoriaId: sugerirCategoria(item.descricaoXml ?? '') ?? categorias.value[0]?.id ?? '',
    marcaId: marcas.value[0]?.id ?? '',
    custoUnitario: Math.round(custo * 100) / 100,
    precoVenda: Math.round(custo * (1 + (item._markup || 150) / 100) * 100) / 100,
    // Fiscal — herdado da Configuração Fiscal (tributação padrão do cadastro base)
    ncm: item.ncmXml ?? '',
    ...tributacaoPadrao(item._cfop),
    // Fornecedor
    _fornecedorExistente: fornecedorExistente,
    _vincularFornecedor: !!fornecedorExistente,
    _cadastrarFornecedor: !fornecedorExistente,
  }
  dlgCriarProduto.value = true
}

async function salvarNovoProduto() {
  const np = novoProduto.value
  if (!np.descricao || !np.unidadeMedidaId) {
    notif.erro('Preencha pelo menos Descrição e Unidade.')
    return
  }

  // Garantir categoria
  if (!np.categoriaId) {
    if (!categorias.value.length) {
      const rc = await api.post('/categorias', { empresaId: auth.empresaId, nome: 'Geral' }).catch(() => null)
      if (rc) { categorias.value = [rc.data]; np.categoriaId = rc.data.id }
    } else {
      np.categoriaId = categorias.value[0].id
    }
  }
  // Garantir marca
  if (!np.marcaId) {
    if (!marcas.value.length) {
      const rm = await api.post('/marcas', { empresaId: auth.empresaId, nome: 'Sem marca' }).catch(() => null)
      if (rm) { marcas.value = [rm.data]; np.marcaId = rm.data.id }
    } else {
      np.marcaId = marcas.value[0].id
    }
  }
  if (!np.categoriaId || !np.marcaId) {
    notif.erro('Não foi possível obter categoria ou marca.')
    return
  }

  salvandoProduto.value = true
  try {
    // 1. Cadastrar fornecedor se solicitado
    let fornecedorId: string | null = np._fornecedorExistente?.id ?? null
    if (!fornecedorId && np._cadastrarFornecedor && entrada.value?.emitenteCnpj) {
      const rf = await api.post('/fornecedores', {
        empresaId: auth.empresaId,
        razaoSocial: entrada.value.emitenteNome,
        cnpj: entrada.value.emitenteCnpj,
      }, { _quiet: true } as any).catch(() => null)
      fornecedorId = rf?.data?.id ?? null
    } else if (np._fornecedorExistente && np._vincularFornecedor) {
      fornecedorId = np._fornecedorExistente.id
    }

    // Sanitizar campos antes de enviar
    const ncmLimpo = (np.ncm ?? '').replace(/\D/g, '').slice(0, 8) || null
    const eanLimpo = /^\d{8,14}$/.test(np.codigoBarras ?? '') ? np.codigoBarras : null
    const precoFinal = (np.precoVenda ?? 0) > 0 ? np.precoVenda : Math.round((np.custoUnitario ?? 0) * 1.5 * 100) / 100 || 0.01

    // 1b. Se já existe produto com este código de barras, vincula ao existente
    //     (o código de barras identifica o produto) em vez de tentar criar de novo.
    if (eanLimpo) {
      const rb = await api.get('/produtos/buscar', {
        params: { empresaId: auth.empresaId, q: eanLimpo },
      }).catch(() => null)
      const existente = (rb?.data ?? []).find((p: any) => p.codigoBarras === eanLimpo)
      if (existente) {
        if (!produtos.value.find((p: any) => p.id === existente.id))
          produtos.value = [...produtos.value, existente]
        if (itemCriando.value) {
          itemCriando.value._produtoId = existente.id
          itemCriando.value._produtoDescricao = existente.descricao
          itemCriando.value._alterado = true
        }
        dlgCriarProduto.value = false
        notif.ok(`Produto "${existente.descricao}" já cadastrado (cód. ${existente.codigo}) — vinculado ao item.`)
        salvandoProduto.value = false
        return
      }
    }

    // 2. Criar produto
    const r = await api.post('/produtos', {
      empresaId: auth.empresaId,
      // Vazio → o backend gera um código único (evita colisão com produtos não carregados)
      codigo: (np.codigo ?? '').trim() || null,
      descricao: np.descricao,
      codigoBarras: eanLimpo,
      ncm: ncmLimpo,
      categoriaId: np.categoriaId,
      marcaId: np.marcaId,
      unidadeMedidaId: np.unidadeMedidaId,
      custoUnitario: np.custoUnitario ?? 0,
      precoVenda: precoFinal,
    })
    const novoProdId = r.data.id ?? r.data.Id

    // Foto automática pelo EAN (background)
    buscarFotoAuto(novoProdId, eanLimpo)

    // 3. Inserir na lista local para o autocomplete exibir o nome imediatamente
    if (!produtos.value.find((p: any) => p.id === novoProdId)) {
      produtos.value = [...produtos.value, { id: novoProdId, descricao: np.descricao, codigo: (np.codigo ?? '').trim() || (r.data.codigo ?? '') }]
    }
    // Vincular ao item e fechar dialog imediatamente
    if (itemCriando.value) {
      itemCriando.value._produtoId = novoProdId
      itemCriando.value._produtoDescricao = np.descricao
      itemCriando.value._alterado = true
    }
    dlgCriarProduto.value = false
    notif.ok('Produto criado e vinculado! Acesse Produtos → aba Nutricional para preencher a tabela TACO.')

    // 4. Em background: fiscal + fornecedor + reload lista (não bloqueia o dialog)
    if (novoProdId) {
      api.put(`/produtos/${novoProdId}`, {
        empresaId: auth.empresaId,
        descricao: np.descricao,
        categoriaId: np.categoriaId,
        marcaId: np.marcaId,
        unidadeMedidaId: np.unidadeMedidaId,
        custoUnitario: np.custoUnitario ?? 0,
        precoVenda: precoFinal,
        ncm: ncmLimpo,
        cfop: np.cfop || null,
        origem: np.origem ?? '0',
        csosnIcms: np.csosnIcms || null,
        cstIcms: np.cstIcms || null,
        cstPisCofins: np.cstPisCofins || null,
        aliquotaIcms: np.aliquotaIcms ?? 0,
        aliquotaPis: np.aliquotaPis ?? 0,
        aliquotaCofins: np.aliquotaCofins ?? 0,
        fornecedorPrincipalId: fornecedorId,
      }, { _quiet: true } as any).catch(() => null).then(() => {
        api.get('/produtos', { params: { empresaId: auth.empresaId, tamanhoPagina: 2000 } })
          .then(rp => { produtos.value = rp.data?.itens ?? rp.data ?? [] })
          .catch(() => null)
      })
    }
  } catch (e: any) {
    const errs = e.response?.data
    let msg = 'Erro ao criar produto.'
    if (typeof errs === 'string') msg = errs
    else if (errs?.mensagem) msg = errs.mensagem
    else if (errs?.errors) msg = Object.entries(errs.errors).map(([k, v]) => `${k}: ${(v as any[]).join(', ')}`).join(' | ')
    else if (errs?.title) msg = errs.title
    notif.erro(msg)
  } finally {
    salvandoProduto.value = false
  }
}

function proximoCodigo(): string {
  const numeros = produtos.value
    .map((p: any) => parseInt(p.codigo ?? '', 10))
    .filter((n: number) => !isNaN(n) && n >= 3001)
  const maior = numeros.length > 0 ? Math.max(...numeros) : 3000
  return String(maior + 1)
}

// Mantido por compatibilidade; delega para custoUnitario (mesma base do backend).
function custoDisplay(item: any) {
  return custoUnitario(item)
}

function aplicarMarkupGlobal() {
  if (!markupGlobal.value || markupGlobal.value <= 0) return
  itensEditaveis.value.forEach(item => {
    item._markup = markupGlobal.value   // já em %
    item._alterado = true
  })
}

function aplicarModoMarkup(modo: 'manter_markup' | 'manter_preco') {
  itensEditaveis.value.forEach(item => {
    const custo = item.custoUnitarioFinal || item.valorUnitarioXml || 0
    if (custo <= 0 || !item._produtoId) return
    const prod = produtos.value.find((p: any) => p.id === item._produtoId)
    if (!prod) return
    if (modo === 'manter_markup') {
      // prod.markup é multiplicador (ex: 3.9253); converter para %
      const mkp = prod.markup ?? prod.markupVenda ?? 0
      if (mkp > 0) { item._markup = Math.round((mkp - 1) * 10000) / 100; item._alterado = true }
    } else {
      const preco = prod.precoVenda ?? prod.preco ?? 0
      if (preco > 0) { item._markup = Math.round((preco / custo - 1) * 10000) / 100; item._alterado = true }
    }
  })
}

function descartarItens() {
  popularItensEditaveis(entrada.value?.itens ?? [])
}

async function salvarTodos() {
  const alterados = itensEditaveis.value.filter(i => i._alterado)
  if (!alterados.length) return
  salvandoTodos.value = true
  try {
    await Promise.all(alterados.map(item =>
      api.patch(`/fiscal/entradas/${entradaId}/itens/${item.id}`, {
        cfopUtilizado: item._cfop,
        produtoId: item._produtoId,
        produtoDescricao: item._produtoDescricao,
        fatorConversao: item._fator,
        unidadeEstoque: item._unidade,
        numeroLote: item._lote || null,
        validade: item._validade || null,
        markupSugerido: 1 + item._markup / 100,   // converter % → multiplicador para o backend
        valorIpi: item._ipi ?? 0,
        valorIcmsSt: item._icmsSt ?? 0,
        tags: null,
      })
    ))
    notif.ok(`${alterados.length} item(ns) salvos.`)
    await carregar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao salvar itens.')
  } finally { salvandoTodos.value = false }
}

function adicionarFatura() {
  const num = faturas.value.length + 1
  const venc = new Date()
  venc.setMonth(venc.getMonth() + num)
  faturas.value.push({
    label: String(num),
    valor: 0,
    vencimento: venc.toISOString().slice(0, 10),
  })
}

// Abre a confirmação — NÃO lança nada ainda. O financeiro só é lançado após conferência.
async function estornar() {
  if (!motivoEstorno.value) { notif.erro('Informe o motivo do estorno.'); return }
  estornando.value = true
  try {
    const r = await api.post(`/fiscal/entradas/${entradaId}/estornar`, { motivo: motivoEstorno.value })
    notif.ok(`Entrada estornada. ${r.data.lancamentosCancelados} lançamento(s) cancelado(s).`)
    dlgEstorno.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao estornar.')
  } finally { estornando.value = false }
}

async function clonar() {
  const r = await api.post(`/fiscal/entradas/${entradaId}/clonar`)
  notif.ok('Entrada clonada!')
  router.push(`/fiscal/entradas/${r.data.id}`)
}

async function clonarParaSaida() {
  try {
    const r = await api.post(`/fiscal/entradas/${entradaId}/clonar-para-saida`)
    notif.ok(r.data.mensagem)
    router.push('/fiscal')
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao clonar para saída.')
  }
}

async function confirmarDevolucao() {
  devolvendo.value = true
  try {
    const r = await api.post(`/fiscal/entradas/${entradaId}/devolver`, {
      itens: itensDevolucao.value.length ? itensDevolucao.value : null,
    })
    notif.ok(r.data.mensagem)
    dlgDevolucao.value = false
    router.push('/fiscal')
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao gerar devolução.')
  } finally { devolvendo.value = false }
}

async function excluir() {
  if (!confirm('Excluir esta entrada definitivamente?')) return
  try {
    await api.delete(`/fiscal/entradas/${entradaId}`)
    notif.ok('Entrada excluída.')
    router.push('/fiscal')
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao excluir.')
  }
}

async function imprimirEtiquetas() {
  const r = await api.get(`/fiscal/entradas/${entradaId}/etiquetas`, {
    params: { templateId: templateEtiqueta.value },
  })
  // Redireciona para o editor de etiquetas com os dados
  router.push({
    path: '/estoque/etiquetas',
    query: { preCarregado: JSON.stringify(r.data) },
  })
  dlgEtiquetas.value = false
}

onMounted(async () => {
  await Promise.all([carregar(), carregarAuxiliares()])
  // Depois que produtos e itens estão carregados, aplica unidade/markup do produto
  enriquecerItensComProduto()
  // …e a conversão usada na última entrada de cada produto
  await aplicarConversoesAnteriores()
})
</script>
