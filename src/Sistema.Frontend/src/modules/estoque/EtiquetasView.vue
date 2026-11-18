<template>
  <div>
    <!-- Confirmação da impressão (substitui o confirm nativo) -->
    <v-dialog v-model="dlgConfirmEtiq" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-title class="d-flex align-center pa-4 pb-2">
          <v-icon color="primary" class="mr-2">mdi-printer-check</v-icon>
          As etiquetas saíram?
        </v-card-title>
        <v-card-text class="pb-2">
          Confirme se as <b>{{ confirmEtiqIds.length }}</b> etiqueta(s) saíram corretamente na impressora.
          <div class="text-caption text-medium-emphasis mt-1">
            Se a impressora estava desligada ou sumiu, responda <b>Não saiu</b> — elas continuam na lista para reimprimir.
          </div>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-btn variant="text" color="error" @click="confirmarEtiqNao">Não saiu</v-btn>
          <v-spacer />
          <v-btn color="primary" variant="flat" prepend-icon="mdi-check" @click="confirmarEtiqSim">
            Sim, saiu tudo
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Editor de Etiquetas</h2></v-col>
      <v-col cols="auto" class="d-flex gap-2">
        <template v-if="isGondola">
          <v-btn color="primary" prepend-icon="mdi-printer" @click="imprimirGondola"
            :disabled="!produtosParaImprimir.length">
            Imprimir ({{ produtosParaImprimir.length }})
          </v-btn>
          <v-btn color="success" variant="outlined" prepend-icon="mdi-download" @click="baixarZpl"
            :disabled="!produtosParaImprimir.length">
            Gerar ZPL
          </v-btn>
          <v-btn color="secondary" variant="outlined" prepend-icon="mdi-usb"
            @click="enviarZebraDialog = true" :disabled="!produtosParaImprimir.length">
            Enviar para Zebra
          </v-btn>
        </template>
        <v-btn v-else color="primary" prepend-icon="mdi-printer" @click="imprimir"
          :disabled="!produtosParaImprimir.length">
          Imprimir ({{ produtosParaImprimir.length }})
        </v-btn>
      </v-col>
    </v-row>

    <GuiaPassos
      id="etiquetas"
      titulo="Como usar o Editor de Etiquetas"
      :passos="[
        '<b>1. Escolha o Template</b> (à esquerda): <b>EcoGranel</b> (etiqueta de preço 10×10cm com QR Code), <b>Gôndola Zebra</b> (impressora térmica ZPL), <b>Pote 9×9cm</b> ou os tamanhos padrão (40×25, 50×30, 100×50mm).',
        '<b>2. Ajuste os campos visíveis</b> (marque/desmarque nome, preço, código de barras, validade, PLU, etc.) e as opções do template — cor da borda, marca d\'água, URL do QR Code, texto descritivo.',
        '<b>3. Busque e adicione os produtos</b> no campo <b>Buscar produto</b>. Eles viram chips — clique no <b>×</b> do chip para remover. Defina a <b>Qtd por produto</b> e a <b>Validade</b>.',
        '<b>4. Confira a pré-visualização</b> à direita e <b>imprima</b> (templates comuns/EcoGranel/Pote) ou <b>Gere o ZPL</b> / envie para a <b>Zebra</b> (template Gôndola). Nada é salvo no banco — a etiqueta é gerada na hora a partir dos dados do produto.',
      ]"
    />

    <v-row>
      <!-- Painel esquerdo: configuração -->
      <v-col cols="12" md="4">
        <v-card rounded="xl" elevation="1" class="mb-4 pa-4">
          <div class="text-body-2 font-weight-bold mb-2">Template</div>
          <v-select v-model="template" :items="templates" item-title="nome" item-value="id"
            variant="outlined" density="compact" class="mb-4" hide-details
            prepend-inner-icon="mdi-tag-outline" />

          <!-- Configurações de aparência do template — ocultas para o atendente -->
          <div v-if="!ehAtendente">
          <!-- ── Gôndola Zebra ── -->
          <template v-if="isGondola">
            <div class="text-body-2 font-weight-bold mb-2">Tamanho</div>
            <v-btn-toggle v-model="gondolaTamanho" mandatory density="compact" class="mb-3 flex-wrap">
              <v-btn v-for="s in gondolaTamanhos" :key="s.id" :value="s.id" size="small">{{ s.nome }}</v-btn>
            </v-btn-toggle>

            <div class="text-body-2 font-weight-bold mb-2">Impressão</div>
            <v-text-field v-model.number="gondolaColunas" label="Produtos por linha"
              type="number" min="1" max="4" variant="outlined" density="compact" class="mb-2"
              hint="Padrão 2 (etiqueta de gôndola com 2 colunas)" persistent-hint />
            <v-text-field v-model.number="zebraDpi" label="DPI da impressora (ZPL)"
              type="number" variant="outlined" density="compact" class="mb-1"
              hint="203 = padrão, 300 = alta resolução" persistent-hint />

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Campos visíveis</div>
            <v-checkbox v-model="camposGondola.nome" label="Nome do produto" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.preco" label="Preço de venda" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.precoKg" label="Preço por kg / unidade" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.codBarras" label="Código de barras (EAN-13)" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.codigoPlu" label="Código PLU" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.validade" label="Validade" density="compact" hide-details />
            <v-checkbox v-model="camposGondola.unidade" label="Unidade de medida" density="compact" hide-details />
          </template>

          <!-- ── EcoGranel (etiqueta de preço com QR Code) ── -->
          <template v-else-if="template === 'ecogranel'">
            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Cor da Borda</div>
            <v-row dense>
              <v-col cols="7">
                <v-text-field v-model="borda.cor" label="Cor" variant="outlined"
                  density="compact" type="color" hide-details />
              </v-col>
              <v-col cols="5">
                <v-text-field v-model.number="borda.espessura" label="Espessura (px)"
                  type="number" variant="outlined" density="compact" :min="2" :max="20" hide-details />
              </v-col>
            </v-row>

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Marca d'água</div>
            <div class="text-caption text-medium-emphasis mb-2">
              Imagem exibida como fundo (logo, semente, produto, etc.)
            </div>
            <v-btn variant="outlined" size="small" prepend-icon="mdi-image-plus"
              @click="inputMarcaDagua?.click()" class="mb-2">
              {{ marcaDaguaUrl ? 'Trocar imagem' : 'Carregar imagem' }}
            </v-btn>
            <input ref="inputMarcaDagua" type="file" accept="image/*" class="d-none"
              @change="carregarMarcaDagua" />
            <div v-if="marcaDaguaUrl" class="d-flex align-center gap-2 mt-1">
              <img :src="marcaDaguaUrl" style="height:40px;border-radius:4px;opacity:.6" />
              <v-btn icon="mdi-close" size="x-small" variant="text" @click="marcaDaguaUrl=''" />
            </div>

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Campos visíveis</div>
            <v-checkbox v-model="camposEco.nome" label="Nome do produto" density="compact" hide-details />
            <v-checkbox v-model="camposEco.codigoPlu" label="Código PLU (após nome)" density="compact" hide-details />
            <v-checkbox v-model="camposEco.preco100g" label="Preço por 100g" density="compact" hide-details />
            <v-checkbox v-model="camposEco.validade" label="Validade" density="compact" hide-details />
            <v-checkbox v-model="camposEco.descricao" label="Texto descritivo" density="compact" hide-details />
            <v-checkbox v-model="camposEco.qrcode" label="QR Code" density="compact" hide-details />
            <v-checkbox v-model="camposEco.frase" label='Frase "Natural como deve ser!"' density="compact" hide-details />

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Texto descritivo (padrão)</div>
            <v-textarea v-model="textoDescritivoEco" variant="outlined" density="compact"
              rows="3" auto-grow hint="Usado quando o produto não tem descrição complementar" persistent-hint />

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">URL base do QR Code</div>
            <v-text-field v-model="qrBaseUrl" variant="outlined" density="compact"
              :placeholder="branding.catalogoProdutoUrl"
              hint="O slug do produto será adicionado automaticamente" persistent-hint />

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Textos</div>
            <v-text-field v-model="ecoCfg.rotuloPreco" label="Rótulo do preço"
              variant="outlined" density="compact" hide-details class="mb-2" />
            <v-text-field v-model="ecoCfg.fraseRodape" label="Frase do rodapé"
              variant="outlined" density="compact" hide-details />

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Cores</div>
            <v-row dense>
              <v-col cols="4">
                <v-text-field v-model="ecoCfg.corTexto" label="Nome" type="color"
                  variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="4">
                <v-text-field v-model="ecoCfg.corPreco" label="Preço" type="color"
                  variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="4">
                <v-text-field v-model="ecoCfg.fundoCor" label="Fundo" type="color"
                  variant="outlined" density="compact" hide-details />
              </v-col>
            </v-row>

            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Tamanhos e semente</div>
            <div class="text-caption">Tamanho do nome: {{ ecoCfg.escalaNome }}%</div>
            <v-slider v-model="ecoCfg.escalaNome" :min="60" :max="200" :step="5"
              density="compact" hide-details class="mb-1" />
            <div class="text-caption">Tamanho do preço: {{ ecoCfg.escalaPreco }}%</div>
            <v-slider v-model="ecoCfg.escalaPreco" :min="60" :max="160" :step="5"
              density="compact" hide-details class="mb-1" />
            <div class="text-caption">Opacidade da semente: {{ ecoCfg.marcaOpacidade }}%</div>
            <v-slider v-model="ecoCfg.marcaOpacidade" :min="0" :max="60" :step="1"
              density="compact" hide-details />

            <template v-if="!ehAtendente">
              <v-divider class="my-3" />
              <v-btn color="primary" variant="flat" block rounded="lg" :loading="salvandoEco"
                prepend-icon="mdi-content-save" @click="salvarTemplateEco">
                Salvar template
              </v-btn>
              <div class="d-flex align-center mt-2">
                <span class="text-caption text-medium-emphasis">
                  Salvo no servidor: vira o padrão da loja em todos os computadores.
                </span>
                <v-spacer />
                <v-btn size="x-small" variant="text" color="error"
                  @click="restaurarPadraoEco">Restaurar padrão</v-btn>
              </div>
            </template>
            <div v-else class="text-caption text-medium-emphasis mt-3">
              <v-icon size="14">mdi-lock-outline</v-icon>
              Você pode imprimir etiquetas de produtos, mas não editar o template padrão.
            </div>
          </template>

          <!-- ── Pote 9×9cm ── -->
          <template v-else-if="template === 'pote9x9'">
            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Borda</div>
            <v-row dense>
              <v-col cols="7">
                <v-text-field v-model="borda.cor" label="Cor da borda" variant="outlined"
                  density="compact" type="color" hide-details />
              </v-col>
              <v-col cols="5">
                <v-text-field v-model.number="borda.espessura" label="Espessura (px)"
                  type="number" variant="outlined" density="compact" :min="1" :max="20" hide-details />
              </v-col>
            </v-row>
            <v-divider class="my-3" />
            <div class="text-body-2 font-weight-bold mb-2">Campos visíveis</div>
            <v-checkbox v-model="camposPote.nome" label="Nome do produto" density="compact" hide-details />
            <v-checkbox v-model="camposPote.descricao" label="Descrição complementar" density="compact" hide-details />
            <v-checkbox v-model="camposPote.codigoPlu" label="Código PLU (balança)" density="compact" hide-details />
            <v-checkbox v-model="camposPote.preco100g" label="Preço por 100g" density="compact" hide-details />
            <v-checkbox v-model="camposPote.validade" label="Validade" density="compact" hide-details />
            <v-checkbox v-model="camposPote.frase" label='Frase "Natural como deve ser!"' density="compact" hide-details />
          </template>

          <!-- ── Black Friday (10×10cm, igual EcoGranel) ── -->
          <template v-else-if="template === 'blackfriday'">
            <v-alert type="info" variant="tonal" density="compact" class="mb-3">
              Etiqueta <b>Black Friday</b> (10×10cm, tamanho do EcoGranel). Mostra o <b>preço já com o
              desconto</b> da promoção e só os produtos que estão <b>em promoção Black Friday</b>.
            </v-alert>
            <v-btn color="black" prepend-icon="mdi-tag-multiple" :loading="carregandoBf"
              @click="adicionarBlackFriday" block class="mb-2">
              Carregar produtos da Black Friday
            </v-btn>
            <div class="text-caption text-medium-emphasis">
              Puxa os produtos das promoções "BLACK FRIDAY". O preço é por 100g já com desconto.
            </div>
          </template>

          <!-- ── EcoGranel Promoção (10×10cm, natural verde + preço vermelho) ── -->
          <template v-else-if="template === 'promocao'">
            <v-alert type="success" variant="tonal" density="compact" class="mb-3">
              Etiqueta <b>EcoGranel Promoção</b> (10×10cm). Mostra o <b>preço já com o desconto</b> e
              o <b>"de/por"</b>, só dos produtos que estão <b>em promoção ativa com desconto automático</b>.
            </v-alert>
            <v-btn color="green-darken-2" prepend-icon="mdi-sale" :loading="carregandoPromo"
              @click="adicionarPromocao" block class="mb-2">
              Carregar produtos em promoção
            </v-btn>
            <div class="text-caption text-medium-emphasis">
              Puxa as promoções ativas (Marketing → Promoções). O preço é por 100g já com desconto.
            </div>
          </template>

          <!-- ── Templates padrão ── -->
          <template v-else>
            <v-alert v-if="template === 'plaquinha'" type="info" variant="tonal" density="compact" class="mb-3">
              <b>Plaquinha 5×3cm</b> — sai numa folha A4 (várias por página). Imprime <b>só produtos por unidade (UN)</b>; os produtos por peso (kg) ficam de fora automaticamente.
            </v-alert>
            <div class="text-body-2 font-weight-bold mb-2">Campos visíveis</div>
            <v-checkbox v-model="campos.nome" label="Nome do produto" density="compact" hide-details />
            <v-checkbox v-model="campos.preco" label="Preço de venda" density="compact" hide-details />
            <v-checkbox v-model="campos.precoPor" label="Preço Promocional" density="compact" hide-details />
            <v-checkbox v-model="campos.codBarras" label="Código de barras (EAN)" density="compact" hide-details />
            <v-checkbox v-model="campos.validade" label="Validade" density="compact" hide-details />
            <v-checkbox v-model="campos.lote" label="Lote" density="compact" hide-details />
            <v-checkbox v-model="campos.ncm" label="NCM" density="compact" hide-details />
          </template>
          </div>

          <v-divider class="my-3" />
          <v-text-field v-model.number="qtdEtiquetas" label="Qtd por produto" type="number"
            variant="outlined" density="compact" :min="1" :max="100" />
          <v-text-field v-model="validade" label="Validade" type="date"
            variant="outlined" density="compact" class="mt-2" />
          <v-text-field v-if="!ehAtendente && !isGondola && template !== 'pote9x9' && template !== 'ecogranel'"
            v-model="lote" label="Lote (p/ todos)" variant="outlined" density="compact" />
          <v-text-field v-if="!ehAtendente && !isGondola && template !== 'pote9x9' && template !== 'ecogranel'"
            v-model.number="precoPromo" label="Preço Promocional (R$)"
            type="number" variant="outlined" density="compact" prefix="R$" />
        </v-card>

        <!-- Busca de produtos -->
        <v-card rounded="xl" elevation="1" class="pa-4">
          <div class="text-body-2 font-weight-bold mb-2">Produtos Selecionados</div>
          <v-text-field v-model="buscaProdutoTexto" ref="campoBusca"
            label="Buscar produto (nome, código ou leitor de código de barras)…"
            variant="outlined" density="compact" :loading="buscando" clearable autofocus
            prepend-inner-icon="mdi-barcode-scan" @update:model-value="buscarProdutos"
            @keyup.enter="buscarPorCodigoBarras"
            hint="Pode bipar o código de barras: o produto entra na lista sozinho"
            persistent-hint class="mb-2" />
          <div class="d-flex align-center gap-2 flex-wrap mb-3">
            <v-menu>
              <template #activator="{ props }">
                <v-btn v-bind="props" color="primary" variant="flat" size="small"
                  prepend-icon="mdi-playlist-plus" append-icon="mdi-chevron-down"
                  :loading="adicionandoKg || carregandoDesat || carregandoEnc">
                  Adicionar em lote
                </v-btn>
              </template>
              <v-list density="compact" min-width="280">
                <v-list-item @click="adicionarTodosKg" title="Todos os produtos por kg"
                  subtitle="Produtos de balança">
                  <template #prepend><v-icon color="primary">mdi-scale-balance</v-icon></template>
                </v-list-item>
                <v-list-item @click="carregarDesatualizadas" title="Etiquetas desatualizadas"
                  subtitle="Preço/validade mudou e não reimprimiu">
                  <template #prepend><v-icon color="deep-orange">mdi-tag-remove-outline</v-icon></template>
                </v-list-item>
                <v-list-item @click="carregarEncalhados('sem')" title="Encalhados sem etiqueta"
                  subtitle="Nunca etiquetados">
                  <template #prepend><v-icon color="blue-grey">mdi-tag-off-outline</v-icon></template>
                </v-list-item>
              </v-list>
            </v-menu>
            <v-btn v-if="auth.lojaAtualId" size="small" variant="text" color="deep-orange"
              prepend-icon="mdi-printer-alert" :loading="reabrindo" @click="reabrirUltimaImpressao">
              Perdi a última impressão
            </v-btn>
            <v-spacer />
            <v-btn v-if="produtosSel.length" size="small" variant="text" color="error"
              prepend-icon="mdi-close" @click="produtosSel = []">
              Limpar ({{ produtosSel.length }})
            </v-btn>
          </div>
          <v-list v-if="sugestoes.length" elevation="2" rounded="lg" class="mb-2"
            max-height="240" style="overflow-y:auto">
            <v-list-item v-for="p in sugestoes" :key="p.id"
              :title="p.descricao"
              :subtitle="`PLU: ${p.codigoPlu ?? '—'} · R$ ${fmt(p.precoVenda)}`"
              @click="adicionarProdutoObj(p)" hover>
              <template #prepend><v-icon size="18">mdi-plus-circle-outline</v-icon></template>
            </v-list-item>
          </v-list>
          <v-alert v-if="!produtosSel.length" type="info" variant="tonal" density="compact" class="mt-1">
            Nenhum produto na lista. Use <b>"Adicionar em lote → Etiquetas desatualizadas"</b> ou
            bipe o código de barras. Cada produto mostra a <b>data da última impressão</b>.
          </v-alert>
          <template v-else>
            <div class="d-flex align-center mb-1">
              <span class="text-caption text-medium-emphasis">
                {{ produtosParaImprimir.length }} de {{ produtosSel.length }} marcados p/ imprimir
              </span>
              <v-spacer />
              <v-btn size="x-small" variant="text" @click="marcarTodos(true)">Marcar todos</v-btn>
              <v-btn size="x-small" variant="text" @click="marcarTodos(false)">Desmarcar</v-btn>
            </div>
            <v-list density="compact" max-height="320" class="overflow-y-auto border rounded-lg">
              <v-list-item v-for="p in produtosSel" :key="p.id" density="compact">
                <template #prepend>
                  <v-checkbox-btn :model-value="vaiImprimir(p)" :disabled="!elegivel(p)"
                    density="compact" @update:model-value="v => marcados[p.id] = !!v" />
                </template>
                <v-list-item-title class="text-body-2">{{ p.descricao }}</v-list-item-title>
                <v-list-item-subtitle class="mt-1">
                  <v-chip v-if="ultimaImpressaoMap[p.id]" size="x-small" color="success"
                    variant="tonal" prepend-icon="mdi-printer-check">
                    Impressa em {{ fmtDataHora(ultimaImpressaoMap[p.id]) }}
                  </v-chip>
                  <v-chip v-else size="x-small" color="deep-orange" variant="tonal"
                    prepend-icon="mdi-printer-off">
                    Nunca impressa{{ auth.lojaAtualId ? ' nesta loja' : '' }}
                  </v-chip>
                </v-list-item-subtitle>
                <template #append>
                  <v-chip size="x-small" :color="ehKg(p) ? 'green' : 'grey'" variant="tonal" class="mr-1">
                    {{ p.unidadeSigla || 'UN' }}
                  </v-chip>
                  <v-btn icon="mdi-close" size="x-small" variant="text" color="error"
                    @click="removerProduto(p.id)" />
                </template>
              </v-list-item>
            </v-list>
            <div v-if="template === 'ecogranel' && produtosSel.some(p => !ehKg(p))"
              class="text-caption text-warning mt-1">
              O template <b>EcoGranel</b> é só para produtos por kg — os UN ficam desmarcados. Use Gôndola/40×25 para eles.
            </div>
          </template>
        </v-card>
      </v-col>

      <!-- Painel direito: preview -->
      <v-col cols="12" md="8">
        <v-card rounded="xl" elevation="1" class="pa-4">
          <div class="d-flex align-center mb-3 gap-3">
            <div class="text-body-2 font-weight-bold">Pré-visualização</div>
            <v-chip v-if="isGondola" color="success" size="small" prepend-icon="mdi-printer">Zebra ZPL</v-chip>
            <v-chip v-if="template === 'ecogranel'" color="green-darken-2" size="small" prepend-icon="mdi-qrcode">QR Code</v-chip>
          </div>

          <div v-if="!produtosSel.length" class="text-center py-8 text-medium-emphasis">
            <v-icon icon="mdi-tag-outline" size="60" class="mb-2" />
            <div>Selecione produtos para visualizar as etiquetas</div>
          </div>

          <!-- Preview gôndola -->
          <div v-if="isGondola" class="gondola-grid">
            <div v-for="p in etiquetasExpandidas" :key="p._key"
              class="gondola-etiqueta" :style="gondolaDimStyle">
              <div v-if="camposGondola.nome" class="gon-nome"
                :style="{ fontSize: gondolaCfg.nomeFontPx + 'px' }">{{ p.descricao }}</div>
              <div v-if="camposGondola.preco" class="gon-preco"
                :style="{ fontSize: gondolaCfg.precoFontPx + 'px' }">
                <span class="gon-preco-rs">R$</span>
                <span class="gon-preco-valor">{{ fmtPreco(p.precoVenda) }}</span>
              </div>
              <div v-if="camposGondola.precoKg" class="gon-por-unidade">{{ fmtPrecoKg(p) }}</div>
              <div v-if="camposGondola.validade && validade" class="gon-validade">Val: {{ fmtData(validade) }}</div>
              <div v-if="camposGondola.codBarras && ean13Svg(p.codigoBarras)" class="gon-barcode-area"
                v-html="ean13Svg(p.codigoBarras, { moduleW: 1, height: 22 })"></div>
              <div v-if="camposGondola.codigoPlu && p.codigoPlu" class="gon-plu">
                PLU: {{ String(p.codigoPlu).padStart(6, '0') }}
              </div>
            </div>
          </div>

          <!-- ── Preview EcoGranel ── -->
          <div v-else-if="template === 'ecogranel' || ehBf || ehPromo" id="area-impressao" class="etiquetas-grid">
            <div v-for="p in etiquetasExpandidas" :key="p._key"
              class="etiqueta-eco" :class="{ bf: ehBf, promo: ehPromo }"
              :style="ehBf
                ? { border: '3px solid #000', borderRadius: '6px', background: 'radial-gradient(130% 100% at 50% 0%,#1c1c1c 0%,#0a0a0a 62%)' }
                : ehPromo
                ? { border: '3px solid #2e7d32', borderRadius: '10px', background: 'linear-gradient(160deg,#ffffff 0%,#e8f5e9 55%,#dcedc8 100%)' }
                : { border: `${borda.espessura}px solid ${borda.cor}`, borderRadius: `${borda.espessura * 2 + 4}px`,
                    background: `linear-gradient(160deg,#ffffff 0%, ${ecoCfg.fundoCor} 60%, ${ecoCfg.fundoCor} 100%)` }">

              <!-- Marca d'água (não no Black Friday) -->
              <img v-if="marcaDaguaUrl && !ehBf" :src="marcaDaguaUrl" class="eco-marca-dagua"
                :style="{ opacity: ecoCfg.marcaOpacidade / 100 }" />

              <!-- ═══ Layout Black Friday ═══ -->
              <template v-if="ehBf">
                <span v-html="fernMarkup('tr')"></span>
                <span v-html="fernMarkup('tl')"></span>
                <span v-html="fernMarkup('br')"></span>
                <span v-html="fernMarkup('bl')"></span>
                <div class="bf-content">
                  <div class="eco-bf-black">BLACK</div>
                  <svg class="bf-friday" viewBox="0 0 300 66" preserveAspectRatio="xMidYMid meet"><defs><linearGradient id="bffg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffffff" /><stop offset=".35" stop-color="#f0f0f0" /><stop offset=".5" stop-color="#9c9c9c" /><stop offset=".62" stop-color="#d8d8d8" /><stop offset="1" stop-color="#6f6f6f" /></linearGradient></defs><text x="150" y="52" text-anchor="middle" font-family="Arial" font-weight="900" font-size="60" fill="url(#bffg)" stroke="#5a5a5a" stroke-width="0.6">FRIDAY</text></svg>
                  <div class="bf-hr"></div>
                  <div v-if="camposEco.nome" class="bf-nome">{{ p.descricao?.toUpperCase() }}</div>
                  <div v-if="camposEco.codigoPlu && codigoEtiqueta(p)" class="bf-cod">cód. {{ codigoEtiqueta(p) }}</div>
                  <div v-if="camposEco.preco100g && precoBf(p) < p.precoVenda" class="bf-de">de <s>R$ {{ fmtPrecoDisplay(preco100g(p.precoVenda)) }}</s> por</div>
                  <div v-if="camposEco.preco100g" class="bf-preco"><span class="cur">R$</span>{{ precoBfParts(p).int }}<span class="cent">,{{ precoBfParts(p).cent }}</span></div>
                  <div v-if="camposEco.preco100g" class="bf-100">cada 100g</div>
                  <div v-if="camposEco.validade && validadeProduto(p)" class="bf-val"><strong>Validade: {{ fmtData(validadeProduto(p)) }}</strong></div>
                  <div class="bf-foot">
                    <div v-if="camposEco.qrcode" class="eco-qr-wrap">
                      <canvas :ref="el => registrarQr(el, p)" class="eco-qr-canvas" />
                    </div>
                    <div v-if="camposEco.frase" class="bf-frase"><strong>APROVEITE A BLACK FRIDAY!</strong></div>
                  </div>
                </div>
              </template>

              <!-- ═══ Layout Promoção (natural verde + preço vermelho) ═══ -->
              <template v-else-if="ehPromo">
                <span v-html="fernMarkup('tr', '#2e7d32')"></span>
                <span v-html="fernMarkup('tl', '#2e7d32')"></span>
                <span v-html="fernMarkup('br', '#2e7d32')"></span>
                <span v-html="fernMarkup('bl', '#2e7d32')"></span>
                <div class="bf-content">
                  <div class="pr-badge">PROMOÇÃO</div>
                  <div class="bf-hr"></div>
                  <div v-if="camposEco.nome" class="bf-nome">{{ p.descricao?.toUpperCase() }}</div>
                  <div v-if="camposEco.codigoPlu && codigoEtiqueta(p)" class="bf-cod">cód. {{ codigoEtiqueta(p) }}</div>
                  <div v-if="camposEco.preco100g && precoPromoAuto(p) < p.precoVenda" class="bf-de">de <s>R$ {{ fmtPrecoDisplay(preco100g(p.precoVenda)) }}</s> por</div>
                  <div v-if="camposEco.preco100g" class="bf-preco"><span class="cur">R$</span>{{ precoPromoParts(p).int }}<span class="cent">,{{ precoPromoParts(p).cent }}</span></div>
                  <div v-if="camposEco.preco100g" class="bf-100">cada 100g</div>
                  <div v-if="camposEco.validade && validadeProduto(p)" class="bf-val"><strong>Validade: {{ fmtData(validadeProduto(p)) }}</strong></div>
                  <div class="bf-foot">
                    <div v-if="camposEco.qrcode" class="eco-qr-wrap">
                      <canvas :ref="el => registrarQr(el, p)" class="eco-qr-canvas" />
                    </div>
                    <div v-if="camposEco.frase" class="bf-frase"><strong>APROVEITE A PROMOÇÃO!</strong></div>
                  </div>
                </div>
              </template>

              <!-- ═══ Layout EcoGranel padrão ═══ -->
              <template v-else>
                <div v-if="camposEco.nome" class="eco-nome" :style="stNome">
                  {{ p.descricao?.toUpperCase() }}
                  <span v-if="camposEco.codigoPlu && codigoEtiqueta(p)" class="eco-nome-plu">
                    -{{ codigoEtiqueta(p) }}
                  </span>
                </div>
                <div v-if="camposEco.preco100g" class="eco-preco-bloco">
                  <span class="eco-preco-valor" :style="stPreco">{{ fmtPrecoDisplay(preco100g(p.precoVenda)) }}</span>
                  <div class="eco-preco-label" :style="stRotulo">cada 100g</div>
                </div>
                <div v-if="camposEco.validade && validadeProduto(p)" class="eco-validade">
                  <strong>Validade: {{ fmtData(validadeProduto(p)) }}</strong>
                </div>
                <div v-if="camposEco.descricao" class="eco-descricao">
                  {{ p.descricaoComplementar || textoDescritivoEco }}
                </div>
                <div class="eco-rodape">
                  <div v-if="camposEco.qrcode" class="eco-qr-wrap">
                    <canvas :ref="el => registrarQr(el, p)" class="eco-qr-canvas" />
                    <div class="eco-qr-saibamais">Saiba mais pelo QR Code</div>
                  </div>
                  <div v-if="camposEco.frase" class="eco-frase" :style="stFrase">
                    <strong>{{ ecoCfg.fraseRodape }}</strong>
                  </div>
                </div>
              </template>
            </div>
          </div>

          <!-- Preview pote 9×9cm -->
          <div v-else-if="template === 'pote9x9'" id="area-impressao" class="etiquetas-grid">
            <div v-for="p in etiquetasExpandidas" :key="p._key"
              class="etiqueta-pote"
              :style="{ border: `${borda.espessura}px solid ${borda.cor}`, borderRadius: `${borda.espessura * 2}px` }">
              <div v-if="camposPote.nome" class="pote-nome" :style="{ background: borda.cor }">{{ p.descricao }}</div>
              <div class="pote-corpo">
                <div v-if="camposPote.descricao && p.descricaoComplementar" class="pote-descricao">
                  {{ p.descricaoComplementar }}
                </div>
                <div v-if="camposPote.codigoPlu && p.codigoPlu" class="pote-plu">
                  <span class="pote-plu-label">Cód. Balança:</span>
                  <span class="pote-plu-valor">{{ String(p.codigoPlu).padStart(6, '0') }}</span>
                </div>
                <div v-if="camposPote.preco100g" class="pote-preco-bloco">
                  <div class="pote-preco-valor" :style="{ color: borda.cor }">R$ {{ fmt(preco100g(p.precoVenda)) }}</div>
                  <div class="pote-preco-label">cada 100g</div>
                </div>
                <div v-if="camposPote.validade && validade" class="pote-validade">
                  <v-icon icon="mdi-calendar-clock" size="12" class="mr-1" />
                  Validade: {{ fmtData(validade) }}
                </div>
              </div>
              <div v-if="camposPote.frase" class="pote-frase"
                :style="{ color: borda.cor, borderTopColor: borda.cor + '33' }">
                Natural como deve ser!
              </div>
            </div>
          </div>

          <!-- Preview templates padrão -->
          <div v-else id="area-impressao" class="etiquetas-grid">
            <div v-for="p in etiquetasExpandidas" :key="p._key" class="etiqueta" :style="tplAtual.style">
              <div v-if="campos.nome" class="etq-nome" :style="tplAtual.nomeStyle">{{ p.descricao }}</div>
              <div v-if="campos.precoPor && precoPromo" class="etq-de">DE R$ {{ fmt(p.precoVenda) }}</div>
              <div v-if="campos.preco" class="etq-preco" :style="tplAtual.precoStyle">
                R$ {{ fmt(precoPromo || p.precoVenda) }}
              </div>
              <div v-if="campos.codBarras && ean13Svg(p.codigoBarras)" class="etq-ean"
                v-html="ean13Svg(p.codigoBarras, { moduleW: 1.5, height: 30 })"></div>
              <div class="etq-rodape">
                <span v-if="campos.ncm && p.ncm">NCM: {{ p.ncm }}</span>
                <span v-if="campos.lote && lote"> Lote: {{ lote }}</span>
                <span v-if="campos.validade && validade"> Val: {{ fmtData(validade) }}</span>
              </div>
            </div>
          </div>

          <v-alert v-if="isGondola && produtosSel.length" type="info" variant="tonal"
            density="compact" class="mt-4" icon="mdi-information">
            O botão <strong>Gerar ZPL</strong> baixa o arquivo <code>.zpl</code> pronto para enviar à Zebra
            via USB, rede ou Zebra Setup Utilities.
          </v-alert>
        </v-card>
      </v-col>
    </v-row>

    <!-- Dialog: enviar para Zebra via rede -->
    <v-dialog v-model="enviarZebraDialog" max-width="440">
      <v-card rounded="xl">
        <v-card-title class="pa-4 text-body-1 font-weight-bold">Enviar para Zebra via Rede</v-card-title>
        <v-card-text class="pa-4">
          <v-text-field v-model="zebraIp" label="IP da impressora" placeholder="192.168.1.100"
            variant="outlined" density="compact" prepend-inner-icon="mdi-network" />
          <v-text-field v-model.number="zebraPorta" label="Porta" type="number"
            variant="outlined" density="compact" class="mt-2" />
          <v-alert type="warning" variant="tonal" density="compact" class="mt-2">
            Por enquanto, baixe o ZPL e use o <strong>Zebra Setup Utilities</strong> para enviar.
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn @click="enviarZebraDialog = false">Fechar</v-btn>
          <v-btn color="primary" @click="baixarZpl">Baixar ZPL</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, watch, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { ean13Svg } from '@/utils/barcode'
import QRCode from 'qrcode'
import { imprimirEtiquetasKg } from '@/utils/etiquetaKg'
import { FERN_PATH } from '@/utils/fernBlackFriday'
import GuiaPassos from '@/components/GuiaPassos.vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import { branding } from '@/branding'

const auth = useAuthStore()
const notif = useNotifStore()
const route = useRoute()

// Atalho vindo da tela de Produtos: já carrega as etiquetas desatualizadas ou os encalhados.
// Mapa produto → data da última impressão NESTA loja (para mostrar na lista).
const ultimaImpressaoMap = ref<Record<string, string>>({})
async function carregarUltimaImpressao() {
  if (!auth.empresaId || !auth.lojaAtualId) { ultimaImpressaoMap.value = {}; return }
  try {
    const r = await api.get('/produtos/etiquetas-ultima-impressao-loja', {
      params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId },
    })
    const mapa: Record<string, string> = {}
    for (const it of (r.data ?? [])) mapa[it.produtoId] = it.impressaEm
    ultimaImpressaoMap.value = mapa
  } catch { /* silencioso */ }
}

onMounted(() => {
  carregarUltimaImpressao()
  if (route.query.desatualizadas) carregarDesatualizadas()
  const enc = route.query.encalhados as string | undefined
  if (enc) carregarEncalhados(enc === 'com' ? 'com' : enc === 'todos' ? 'todos' : 'sem')
})
// Troca de loja no topo → recarrega as datas de última impressão.
watch(() => auth.lojaAtualId, carregarUltimaImpressao)
// Atendente pode imprimir etiquetas de produto, mas não editar/salvar o template.
const ehAtendente = computed(() => auth.usuario?.role === 'Atendente')
const buscaProdutoTexto = ref('')
const buscando = ref(false)
const sugestoes = ref<any[]>([])
const produtosSel = ref<any[]>([])
const template = ref('ecogranel')
const qtdEtiquetas = ref(1)

// Seleção por produto (checkbox) + regra: EcoGranel só imprime produtos por kg.
const marcados = ref<Record<string, boolean>>({})
function ehKg(p: any) { return !!p?.produtoBalanca || String(p?.unidadeSigla || '').toUpperCase() === 'KG' }
function elegivel(p: any) {
  // EcoGranel e Black Friday são etiqueta de pote (preço por 100g) → só produtos por peso.
  if (template.value === 'ecogranel' || template.value === 'blackfriday' || template.value === 'promocao') return ehKg(p)
  if (template.value === 'plaquinha') return !ehKg(p)  // Plaquinha: só produtos por unidade (UN)
  return true
}
function vaiImprimir(p: any) { return marcados.value[p.id] !== false && elegivel(p) }
const produtosParaImprimir = computed(() => produtosSel.value.filter(vaiImprimir))
function marcarTodos(v: boolean) { produtosSel.value.forEach((p: any) => { if (elegivel(p)) marcados.value[p.id] = v }) }
// Validade padrão: hoje + 60 dias (produtos por peso). Pré-preenchida para o
// template EcoGranel sempre imprimir com validade; o usuário pode ajustar.
function validadePadrao(): string {
  const d = new Date(); d.setDate(d.getDate() + 60)
  return d.toISOString().slice(0, 10)
}
const validade = ref(validadePadrao())
const lote = ref('')
const precoPromo = ref<number | null>(null)
const enviarZebraDialog = ref(false)
const zebraIp = ref('192.168.1.100')
const zebraPorta = ref(9100)
const zebraDpi = ref(203)
const gondolaColunas = ref(2)
const gondolaTamanho = ref('40x20')   // etiqueta física da loja (evita etiqueta em branco por ^LL maior)
const marcaDaguaUrl = ref(branding.logoUrl)  // logo da marca de fundo (padrão)
const inputMarcaDagua = ref<HTMLInputElement | null>(null)
const qrBaseUrl = ref(branding.catalogoProdutoUrl)
const textoDescritivoEco = ref('')

const borda = ref({ cor: '#2e7d32', espessura: 5 })

const camposEco = ref({
  nome: true, codigoPlu: true, preco100g: true, validade: true,
  descricao: true, qrcode: true, frase: true,
})

// ── Configuração editável do template EcoGranel (preview + impressão) ──────────
const ecoCfg = ref({
  rotuloPreco: 'cada 100g',
  fraseRodape: 'Natural como deve ser!',
  corTexto: '#111111',
  corPreco: '#111111',
  corRotulo: '#555555',
  fundoCor: '#e8f5e9',          // cor de destaque do fundo
  marcaOpacidade: 10,           // % da semente de fundo
  escalaNome: 100,              // % do tamanho do nome
  escalaPreco: 100,             // % do tamanho do preço
})

// Estilos calculados para o preview
const stNome = computed(() => ({ color: ecoCfg.value.corTexto, fontSize: (16 * ecoCfg.value.escalaNome / 100) + 'px' }))
const stPreco = computed(() => ({ color: ecoCfg.value.corPreco, fontSize: (86 * ecoCfg.value.escalaPreco / 100) + 'px' }))
const stRotulo = computed(() => ({ color: ecoCfg.value.corRotulo }))
const stFrase = computed(() => ({ color: borda.value.cor }))

// ── Persistência das preferências do template EcoGranel ────────────────────────
const ECO_KEY = 'ecogranel-template'

/** Monta o objeto de configuração atual do template. */
function snapshotEco() {
  return {
    borda: borda.value, camposEco: camposEco.value, ecoCfg: ecoCfg.value,
    textoDescritivoEco: textoDescritivoEco.value, qrBaseUrl: qrBaseUrl.value,
    marcaDaguaUrl: marcaDaguaUrl.value,
  }
}

/** Aplica uma configuração vinda do servidor ou do cache local. */
function aplicarEco(s: any) {
  if (!s) return
  if (s.borda) borda.value = { ...borda.value, ...s.borda }
  if (s.camposEco) camposEco.value = { ...camposEco.value, ...s.camposEco }
  if (s.ecoCfg) ecoCfg.value = { ...ecoCfg.value, ...s.ecoCfg }
  if (typeof s.textoDescritivoEco === 'string') textoDescritivoEco.value = s.textoDescritivoEco
  if (s.qrBaseUrl) qrBaseUrl.value = s.qrBaseUrl
  if (s.marcaDaguaUrl) marcaDaguaUrl.value = s.marcaDaguaUrl
}

// Cache local (usado como fallback e para impressão offline)
function salvarEco() {
  try { localStorage.setItem(ECO_KEY, JSON.stringify(snapshotEco())) } catch { /* ignora */ }
}

/** Carrega do servidor (padrão da empresa); cai no cache local se falhar. */
async function carregarEco() {
  try {
    const r = await api.get('/etiquetas/config', {
      params: { empresaId: auth.empresaId, template: 'ecogranel' },
    })
    if (r.data?.config) {
      aplicarEco(JSON.parse(r.data.config))
      salvarEco()
      return
    }
  } catch { /* sem conexão/config → usa cache */ }
  try {
    const raw = localStorage.getItem(ECO_KEY)
    if (raw) aplicarEco(JSON.parse(raw))
  } catch { /* ignora */ }
}
const salvandoEco = ref(false)

/** Salva o template no servidor (padrão da empresa) + cache local. */
async function salvarTemplateEco() {
  salvandoEco.value = true
  salvarEco()
  try {
    await api.put('/etiquetas/config', {
      empresaId: auth.empresaId,
      template: 'ecogranel',
      config: JSON.stringify(snapshotEco()),
    })
    notif.ok('Template salvo! Vale para todos os computadores e usuários da loja.')
  } catch {
    notif.aviso('Salvo apenas neste navegador — não foi possível salvar no servidor.')
  } finally { salvandoEco.value = false }
}

async function restaurarPadraoEco() {
  if (!confirm('Restaurar o template EcoGranel para o padrão original? Suas alterações serão perdidas.')) return
  localStorage.removeItem(ECO_KEY)
  try {
    await api.delete('/etiquetas/config', {
      params: { empresaId: auth.empresaId, template: 'ecogranel' },
    })
  } catch { /* segue restaurando localmente */ }
  borda.value = { cor: '#2e7d32', espessura: 5 }
  camposEco.value = { nome: true, codigoPlu: true, preco100g: true, validade: true, descricao: true, qrcode: true, frase: true }
  ecoCfg.value = {
    rotuloPreco: 'cada 100g', fraseRodape: 'Natural como deve ser!',
    corTexto: '#111111', corPreco: '#111111', corRotulo: '#555555', fundoCor: '#e8f5e9',
    marcaOpacidade: 10, escalaNome: 100, escalaPreco: 100,
  }
  textoDescritivoEco.value = ''
  qrBaseUrl.value = branding.catalogoProdutoUrl
  marcaDaguaUrl.value = branding.logoUrl
  notif.aviso('Template restaurado para o padrão original.')
}

const camposPote = ref({
  nome: true, descricao: true, codigoPlu: true,
  preco100g: true, validade: true, frase: true,
})

const camposGondola = ref({
  nome: true, preco: true, precoKg: true,
  codBarras: false, codigoPlu: false, validade: false, unidade: true,
})

const campos = ref({
  nome: true, preco: true, precoPor: false,
  codBarras: true, validade: false, lote: false, ncm: false,
})

const templates = [
  { id: 'ecogranel', nome: 'EcoGranel - Potes' },
  { id: 'blackfriday', nome: 'Black Friday 🖤' },
  { id: 'promocao', nome: 'EcoGranel Promoção 🏷️' },
  { id: 'gondola-70x40', nome: 'Gôndola Zebra' },
  { id: 'plaquinha', nome: 'Plaquinha (UN)' },
  { id: 'pote9x9', nome: 'Pote 9×9cm' },
  { id: '40x25', nome: '40×25mm' },
  { id: '50x30', nome: '50×30mm' },
  { id: '100x50', nome: '100×50mm' },
  { id: 'grande', nome: 'Grande' },
]

const gondolaTamanhos = [
  { id: '40x20', nome: '40×20mm', w: 40, h: 20 },
  { id: '50x25', nome: '50×25mm', w: 50, h: 25 },
  { id: '70x40', nome: '70×40mm', w: 70, h: 40 },
  { id: '100x60', nome: '100×60mm', w: 100, h: 60 },
  { id: '100x30', nome: '100×30mm', w: 100, h: 30 },
]

const isGondola = computed(() => template.value.startsWith('gondola'))
// Black Friday = mesmo render do EcoGranel, só com tema preto.
const ehBf = computed(() => template.value === 'blackfriday')
// Promoção = mesmo render do EcoGranel, tema natural (verde) com preço vermelho e "de/por".
const ehPromo = computed(() => template.value === 'promocao')

const gondolaTamanhoAtual = computed(() =>
  gondolaTamanhos.find(s => s.id === gondolaTamanho.value) ?? gondolaTamanhos[2]
)

const gondolaDimStyle = computed(() => {
  const { w, h } = gondolaTamanhoAtual.value
  return { width: `${w * 3.78}px`, height: `${h * 3.78}px` }
})

const gondolaCfg = computed(() => {
  const { w } = gondolaTamanhoAtual.value
  const scale = w / 70
  return {
    nomeFontPx: Math.round(10 * scale),
    precoFontPx: Math.round(22 * scale),
  }
})

const tplConfig: Record<string, any> = {
  '40x25':  { style: 'width:150px;height:94px;font-size:8px;padding:4px',  nomeStyle: 'font-size:8px;font-weight:bold;line-height:1.1', precoStyle: 'font-size:16px;font-weight:bold' },
  '50x30':  { style: 'width:189px;height:113px;font-size:9px;padding:5px', nomeStyle: 'font-size:9px;font-weight:bold', precoStyle: 'font-size:20px;font-weight:bold' },
  'plaquinha': { style: 'width:189px;height:113px;font-size:10px;padding:6px;border:6px solid #2e7d32;border-radius:8px', nomeStyle: 'font-size:11px;font-weight:bold;line-height:1.15', precoStyle: 'font-size:26px;font-weight:bold' },
  '100x50': { style: 'width:378px;height:189px;font-size:11px;padding:8px',nomeStyle: 'font-size:12px;font-weight:bold', precoStyle: 'font-size:28px;font-weight:bold' },
  'grande': { style: 'width:283px;height:170px;font-size:10px;padding:8px',nomeStyle: 'font-size:11px;font-weight:bold', precoStyle: 'font-size:24px;font-weight:bold' },
}

const tplAtual = computed(() => tplConfig[template.value] ?? tplConfig['40x25'])

const etiquetasExpandidas = computed(() => {
  const lista: any[] = []
  produtosParaImprimir.value.forEach(p => {
    for (let i = 0; i < qtdEtiquetas.value; i++)
      lista.push({ ...p, _key: `${p.id}_${i}` })
  })
  return lista
})

// ── Helpers ──────────────────────────────────────────────────────────────────
const OPTS_2 = { minimumFractionDigits: 2, maximumFractionDigits: 2 } as const
function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', OPTS_2) }
function fmtData(d: string) { if (!d) return ''; const [y, m, dd] = d.split('-'); return `${dd}/${m}/${y}` }
function fmtDataHora(iso: string) {
  if (!iso) return ''
  // O backend grava em UTC. Se a data vier SEM fuso ('Z'/offset), trata como UTC
  // para o navegador converter corretamente para o horário de Brasília.
  const s = /[zZ]|[+-]\d{2}:?\d{2}$/.test(iso) ? iso : iso + 'Z'
  const dt = new Date(s)
  if (isNaN(dt.getTime())) return ''
  return dt.toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
}
function preco100g(precoKg: number) { return Math.round(((precoKg ?? 0) / 10) * 100) / 100 }
// Preço usado na etiqueta Black Friday: o já com desconto da promoção (se veio), senão o normal.
function precoBf(p: any) { return p?.precoBlackFriday ?? p?.precoVenda ?? 0 }
// Preço usado na etiqueta Promoção: o já com desconto da promoção ativa, senão o normal.
function precoPromoAuto(p: any) { return p?.precoPromocao ?? p?.precoVenda ?? 0 }

// Formato especial: "7,69" (sem R$ e com vírgula)
function fmtPrecoDisplay(v: number) {
  return (v ?? 0).toLocaleString('pt-BR', OPTS_2)
}

// Folhagem (samambaia) da arte EcoGranel nos cantos do preview Black Friday.
function fernMarkup(c: string, fill = '#5f5f5f'): string {
  return `<svg class="fern ${c}" viewBox="216 105 48 44" preserveAspectRatio="xMidYMid meet"><g fill="${fill}">${FERN_PATH}</g></svg>`
}
// Preço BF separado em inteiro/centavos (para "R$" + inteiro grande + centavos elevados).
function precoBfParts(p: any): { int: string; cent: string } {
  const [int, cent] = (preco100g(precoBf(p)) ?? 0).toLocaleString('pt-BR', OPTS_2).split(',')
  return { int, cent: cent ?? '00' }
}
// Preço da Promoção separado em inteiro/centavos (mesmo formato do BF).
function precoPromoParts(p: any): { int: string; cent: string } {
  const [int, cent] = (preco100g(precoPromoAuto(p)) ?? 0).toLocaleString('pt-BR', OPTS_2).split(',')
  return { int, cent: cent ?? '00' }
}

function fmtPreco(v: number) { return (v ?? 0).toLocaleString('pt-BR', OPTS_2) }

// Código exibido na etiqueta: PLU da balança ou, na falta, o código interno
function codigoEtiqueta(p: any): string {
  const c = p.codigoPlu != null && p.codigoPlu !== '' ? String(p.codigoPlu) : (p.codigo ?? '')
  return /^\d+$/.test(c) ? c.padStart(4, '0') : c
}

function fmtPrecoKg(p: any) {
  const unidade = p.unidadeSigla || 'KG'
  return `R$ ${fmt(p.precoVenda)} / ${unidade}`
}

// Converte nome do produto em slug para URL do QR
function nomeParaSlug(nome: string): string {
  return (nome ?? '')
    .toLowerCase()
    .normalize('NFD').replace(/[̀-ͯ]/g, '') // remove acentos
    .replace(/[^a-z0-9\s-]/g, '')
    .trim()
    .replace(/\s+/g, '-')
}

// ── QR Code ──────────────────────────────────────────────────────────────────
// Guarda referência dos canvas por _key e gera QR quando monta
const qrCanvasMap = new Map<string, HTMLCanvasElement>()

function registrarQr(el: any, p: any) {
  if (!el) return
  const key = p._key
  if (qrCanvasMap.get(key) === el) return
  qrCanvasMap.set(key, el)
  renderizarQr(el, p)
}

async function renderizarQr(canvas: HTMLCanvasElement, p: any) {
  const slug = nomeParaSlug(p.descricao ?? '')
  const url = qrBaseUrl.value + slug
  try {
    await QRCode.toCanvas(canvas, url, {
      width: 100,
      margin: 1,
      // Black Friday tem fundo preto → QR branco; demais, QR preto.
      color: { dark: template.value === 'blackfriday' ? '#ffffff' : '#000000', light: '#ffffff00' },
    })
  } catch { /* silencia erros de canvas */ }
}

// Re-renderiza QR quando a URL base muda
watch(qrBaseUrl, async () => {
  await nextTick()
  for (const [key, canvas] of qrCanvasMap.entries()) {
    const p = etiquetasExpandidas.value.find(e => e._key === key)
    if (p) renderizarQr(canvas, p)
  }
})

watch(etiquetasExpandidas, async () => {
  if (template.value !== 'ecogranel' && template.value !== 'blackfriday' && template.value !== 'promocao') return
  await nextTick()
  for (const [key, canvas] of qrCanvasMap.entries()) {
    const p = etiquetasExpandidas.value.find(e => e._key === key)
    if (p) renderizarQr(canvas, p)
  }
}, { deep: true })

// Carrega preferências salvas do template EcoGranel e salva a cada alteração
carregarEco()
watch([borda, camposEco, ecoCfg, textoDescritivoEco, qrBaseUrl, marcaDaguaUrl], salvarEco, { deep: true })

// ── Marca d'água ─────────────────────────────────────────────────────────────
function carregarMarcaDagua(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  const reader = new FileReader()
  reader.onload = ev => { marcaDaguaUrl.value = ev.target?.result as string }
  reader.readAsDataURL(file)
}

// ── Gerador ZPL ──────────────────────────────────────────────────────────────
function gerarZpl(): string {
  const dpi = zebraDpi.value
  const dotsPerMm = dpi / 25.4
  const { w, h } = gondolaTamanhoAtual.value
  const labelW = Math.round(w * dotsPerMm)
  const labelH = Math.round(h * dotsPerMm)
  const mx = Math.round(2 * dotsPerMm)
  const my = Math.round(2 * dotsPerMm)

  const zplBlocks: string[] = []

  // Um bloco por produto selecionado; a quantidade é feita pela própria Zebra
  // via ^PQ (não duplicar aqui, senão a tiragem sai multiplicada).
  for (const p of produtosParaImprimir.value) {
    const nome    = zplSanitize(p.descricao ?? '')
    const precoStr = 'R$ ' + fmt(p.precoVenda)
    const porKg   = fmtPrecoKg(p)
    const ean     = p.codigoBarras ?? ''
    const plu     = p.codigoPlu ? 'PLU: ' + String(p.codigoPlu).padStart(6, '0') : ''
    const val     = validade.value ? 'Val: ' + fmtData(validade.value) : ''

    let zpl = `^XA\n^PW${labelW}\n^LL${labelH}\n^LH0,0\n`
    let y = my

    if (camposGondola.value.nome && nome) {
      const fh = Math.round(4 * dotsPerMm); const fw = Math.round(fh * 0.6)
      zpl += `^FO${mx},${y}^A0N,${fh},${fw}^FD${nome}^FS\n`; y += fh + Math.round(1 * dotsPerMm)
    }
    if (camposGondola.value.preco) {
      const fh = Math.round(h * 0.38 * dotsPerMm); const fw = Math.round(fh * 0.65)
      zpl += `^FO${mx},${y}^A0N,${fh},${fw}^FD${precoStr}^FS\n`; y += fh + Math.round(0.5 * dotsPerMm)
    }
    if (camposGondola.value.precoKg) {
      const fh = Math.round(3 * dotsPerMm); const fw = Math.round(fh * 0.6)
      zpl += `^FO${mx},${y}^A0N,${fh},${fw}^FD${porKg}^FS\n`; y += fh + Math.round(0.5 * dotsPerMm)
    }
    if (camposGondola.value.validade && val) {
      const fh = Math.round(2.5 * dotsPerMm); const fw = Math.round(fh * 0.6)
      zpl += `^FO${mx},${y}^A0N,${fh},${fw}^FD${val}^FS\n`; y += fh + Math.round(0.5 * dotsPerMm)
    }
    if (camposGondola.value.codigoPlu && plu) {
      const fh = Math.round(2.5 * dotsPerMm); const fw = Math.round(fh * 0.6)
      zpl += `^FO${mx},${y}^A0N,${fh},${fw}^FD${plu}^FS\n`
    }
    if (camposGondola.value.codBarras && ean) {
      const barcodeH2 = Math.max(Math.round((labelH - y - Math.round(3 * dotsPerMm))), Math.round(6 * dotsPerMm))
      if (w >= 60) {
        zpl += `^FO${labelW - Math.round(35 * dotsPerMm)},${my}^BY1,2,${barcodeH2}^BE^FD${ean}^FS\n`
      } else {
        const bh = Math.max(Math.round(h * 0.3 * dotsPerMm), Math.round(5 * dotsPerMm))
        zpl += `^FO${mx},${y}^BY1,2,${bh}^BE^FD${ean}^FS\n`
      }
    }
    zpl += `^PQ${qtdEtiquetas.value}\n^XZ\n`
    zplBlocks.push(zpl)
  }
  return zplBlocks.join('\n')
}

function zplSanitize(s: string): string {
  return s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^\x20-\x7E]/g, '').substring(0, 50)
}

function escHtml(s: string): string {
  return String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}

// Imprime as etiquetas de gôndola via diálogo do navegador (o usuário escolhe a
// impressora). Layout em grade: por padrão 2 produtos por linha (gondolaColunas).
// Após imprimir: marca as etiquetas como impressas (limpa o alerta/sininho)
// e remove os produtos da lista.
// Após mandar imprimir, confirma COM A ATENDENTE se as etiquetas realmente saíram antes
// de marcá-las como impressas. Se ela disser que NÃO saiu (impressora desligada, travou),
// os produtos continuam na lista para reimprimir — evita "mandei imprimir e sumiu".
// Confirmação da impressão por um diálogo DENTRO do sistema (mais confiável que o
// confirm() nativo, que abria atrás da janela de impressão e era fácil de perder).
const dlgConfirmEtiq = ref(false)
const confirmEtiqIds = ref<string[]>([])
function confirmarImpressao(ids: string[]) {
  if (!ids.length) return
  confirmEtiqIds.value = ids
  // pequeno atraso: a janela de impressão devolve o foco antes do diálogo abrir.
  setTimeout(() => { dlgConfirmEtiq.value = true }, 600)
}
function confirmarEtiqSim() {
  dlgConfirmEtiq.value = false
  finalizarImpressao(confirmEtiqIds.value)
}
function confirmarEtiqNao() {
  dlgConfirmEtiq.value = false
  notif.aviso('Impressão não confirmada — as etiquetas continuam na lista para você tentar de novo.')
}

async function finalizarImpressao(ids: string[]) {
  if (!ids.length) return
  // Com loja selecionada, registra a reimpressão NAQUELA unidade (controle por loja).
  try { await api.post('/produtos/etiquetas-impressas', { ids, localEstoqueId: auth.lojaAtualId ?? null }) } catch { /* silencioso */ }
  // Atualiza a data de "última impressão" na hora (marca agora para os recém-impressos).
  const agora = new Date().toISOString()
  ids.forEach(id => { ultimaImpressaoMap.value[id] = agora })
  produtosSel.value = produtosSel.value.filter((p: any) => !ids.includes(p.id))
  ids.forEach(id => { delete marcados.value[id] })
  notif.ok(`${ids.length} etiqueta(s) impressa(s) — removida(s) da lista e do alerta.`)
}

function imprimirGondola() {
  const idsImpressos = produtosParaImprimir.value.map((p: any) => p.id)
  const cols = Math.min(4, Math.max(1, Number(gondolaColunas.value) || 2))
  const { w, h } = gondolaTamanhoAtual.value
  const cfg = gondolaCfg.value
  const c = camposGondola.value

  const labels = etiquetasExpandidas.value.map((p: any) => {
    const val = validadeProduto(p)
    const partes: string[] = []
    if (c.nome) partes.push(`<div class="g-nome" style="font-size:${cfg.nomeFontPx}px">${escHtml(p.descricao)}</div>`)
    if (c.preco) partes.push(`<div class="g-preco" style="font-size:${cfg.precoFontPx}px"><span class="g-rs">R$</span>${fmtPreco(p.precoVenda)}</div>`)
    if (c.precoKg) partes.push(`<div class="g-kg">${escHtml(fmtPrecoKg(p))}</div>`)
    if (c.validade && val) partes.push(`<div class="g-val">Val: ${fmtData(val)}</div>`)
    if (c.codigoPlu && p.codigoPlu) partes.push(`<div class="g-plu">PLU: ${String(p.codigoPlu).padStart(6, '0')}</div>`)
    const svgBc = ean13Svg(p.codigoBarras ?? '', { moduleW: 1, height: 22 })
    const barcode = (c.codBarras && svgBc) ? `<div class="g-bc">${svgBc}</div>` : ''
    return `<div class="g-etq">${partes.join('')}${barcode}</div>`
  }).join('')

  const win = window.open('', '_blank')
  if (!win) { notif.erro('Permita pop-ups para imprimir.'); return }
  win.document.write(`
    <html><head><title>Etiquetas Gôndola</title><style>
      *{box-sizing:border-box;margin:0;padding:0}
      body{font-family:Arial,sans-serif;background:#fff}
      .grid{display:grid;grid-template-columns:repeat(${cols}, ${w}mm)}
      .g-etq{width:${w}mm;height:${h}mm;
        padding:2mm 3mm;position:relative;overflow:hidden;display:flex;flex-direction:column;
        justify-content:center;align-items:center;text-align:center;page-break-inside:avoid}
      .g-nome{font-weight:bold;color:#1a1a1a;line-height:1.15;word-break:break-word}
      .g-preco{font-weight:900;color:#111;line-height:1}
      .g-rs{font-size:.45em;font-weight:bold;margin-right:2px}
      .g-kg{font-size:9px;color:#555}
      .g-val{font-size:9px;color:#666}
      .g-plu{font-size:9px;color:#666;font-family:monospace}
      .g-bc{display:flex;align-items:center;justify-content:center;margin-top:1mm;max-width:100%}
      .g-bc svg{max-width:100%;height:auto}
      @page{margin:5mm}
    </style></head>
    <body><div class="grid">${labels}</div>
    <script>window.onload=function(){window.print();window.onafterprint=function(){window.close()}}<\/script>
    </body></html>`)
  win.document.close()
  confirmarImpressao(idsImpressos)
}

function baixarZpl() {
  const idsImpressos = produtosParaImprimir.value.map((p: any) => p.id)
  const zpl = gerarZpl()
  const blob = new Blob([zpl], { type: 'text/plain' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `gondola_${gondolaTamanho.value}_${new Date().toISOString().slice(0, 10)}.zpl`
  a.click()
  URL.revokeObjectURL(url)
  enviarZebraDialog.value = false
  confirmarImpressao(idsImpressos)
}

// ── Busca de produtos ────────────────────────────────────────────────────────
const adicionandoKg = ref(false)

// Carrega SÓ os produtos em promoção Black Friday (com o preço já descontado).
const carregandoBf = ref(false)
async function adicionarBlackFriday() {
  carregandoBf.value = true
  try {
    const r = await api.get('/promocoes/produtos-etiqueta', {
      params: { empresaId: auth.empresaId, termo: 'BLACK FRIDAY' },
    })
    const prods = (r.data ?? []) as any[]
    produtosSel.value = []   // a etiqueta BF é só dos produtos da campanha
    await Promise.all(prods.map((p: any) => enriquecerValidadeRegistrada(p)))
    prods.forEach((p: any) => produtosSel.value.push(p))
    if (!prods.length) notif.aviso('Nenhum produto em promoção Black Friday encontrado.')
    else notif.ok(`${prods.length} produto(s) da Black Friday carregado(s).`)
  } catch { notif.erro('Erro ao carregar os produtos da Black Friday.') }
  finally { carregandoBf.value = false }
}

// Carrega SÓ os produtos em promoção ATIVA com desconto automático (preço já descontado).
const carregandoPromo = ref(false)
async function adicionarPromocao() {
  carregandoPromo.value = true
  try {
    const r = await api.get('/promocoes/produtos-promocao-ativa', {
      params: { empresaId: auth.empresaId },
    })
    const prods = (r.data ?? []) as any[]
    produtosSel.value = []   // a etiqueta de Promoção é só dos produtos em promoção ativa
    await Promise.all(prods.map((p: any) => enriquecerValidadeRegistrada(p)))
    prods.forEach((p: any) => produtosSel.value.push(p))
    if (!prods.length) notif.aviso('Nenhum produto em promoção ativa (com desconto automático) encontrado.')
    else notif.ok(`${prods.length} produto(s) em promoção carregado(s).`)
  } catch { notif.erro('Erro ao carregar os produtos em promoção.') }
  finally { carregandoPromo.value = false }
}

// Ao escolher o modelo Black Friday / Promoção, já puxa os produtos automaticamente.
watch(template, (t) => {
  if (t === 'blackfriday' && produtosSel.value.length === 0) adicionarBlackFriday()
  if (t === 'promocao' && produtosSel.value.length === 0) adicionarPromocao()
})

// Adiciona de uma vez todos os produtos por peso (balança) à seleção.
async function adicionarTodosKg() {
  adicionandoKg.value = true
  try {
    const r = await api.get('/produtos', {
      params: { empresaId: auth.empresaId, ativo: true, pagina: 1, tamanhoPagina: 5000 },
    })
    const kg = (r.data?.itens ?? r.data ?? []).filter((p: any) => p.produtoBalanca)
    const novosItens = kg.filter((p: any) => !produtosSel.value.find(x => x.id === p.id))
    // Busca a validade registrada ANTES de empurrar para a lista reativa —
    // se setar depois, a escrita cai no objeto cru e o Vue não re-renderiza.
    await Promise.all(novosItens.map((p: any) => enriquecerValidadeRegistrada(p)))
    novosItens.forEach((p: any) => produtosSel.value.push(p))
    const novos = novosItens.length
    if (!kg.length) notif.aviso('Nenhum produto por kg (balança) encontrado.')
    else notif.ok(`${novos} produto(s) por kg adicionado(s)` +
      (novos < kg.length ? ` (${kg.length - novos} já estavam na lista).` : '.'))
  } catch { notif.erro('Erro ao carregar os produtos por kg.') }
  finally { adicionandoKg.value = false }
}

// Adiciona à seleção todos os produtos com etiqueta desatualizada (preço/validade mudou).
const carregandoDesat = ref(false)
async function carregarDesatualizadas() {
  carregandoDesat.value = true
  try {
    const r = await api.get('/produtos', {
      params: { empresaId: auth.empresaId, ativo: true, pagina: 1, tamanhoPagina: 5000 },
    })
    let desat = (r.data?.itens ?? r.data ?? []).filter((p: any) => p.etiquetaDesatualizada)
    // Com uma LOJA selecionada: só traz as PENDENTES NAQUELA loja (que ela ainda não
    // reimprimiu desde a mudança de preço) e que tenham ESTOQUE na loja.
    if (auth.lojaAtualId) {
      try {
        const pend = await api.get('/produtos/etiquetas-pendentes-loja', {
          params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId },
        })
        const idsPend = new Set((pend.data?.ids ?? []))
        desat = desat.filter((p: any) => idsPend.has(p.id))
      } catch { /* sem endpoint → mantém a lista pelo flag global */ }
      try {
        const pos = await api.get('/estoque/posicao-por-loja', {
          params: { empresaId: auth.empresaId, localEstoqueId: auth.lojaAtualId },
        })
        const idsLoja = new Set((pos.data?.produtos ?? []).map((p: any) => p.id))
        desat = desat.filter((p: any) => idsLoja.has(p.id))
      } catch { /* sem posição da loja → mantém a lista geral */ }
    }
    const novos = desat.filter((p: any) => !produtosSel.value.find(x => x.id === p.id))
    await Promise.all(novos.map((p: any) => enriquecerValidadeRegistrada(p)))
    novos.forEach((p: any) => produtosSel.value.push(p))
    const escopo = auth.lojaAtual ? ` da loja ${auth.lojaAtual.nome}` : ''
    if (!desat.length) notif.aviso(`Nenhuma etiqueta desatualizada${escopo} no momento.`)
    else notif.ok(`${novos.length} produto(s) com etiqueta desatualizada${escopo} adicionado(s).`)
  } catch { notif.erro('Erro ao carregar as etiquetas desatualizadas.') }
  finally { carregandoDesat.value = false }
}

// Reabre a ÚLTIMA remessa de etiquetas impressas desta loja — pra quando a impressão
// se perdeu (impressora desligada, travou, papel acabou) e as etiquetas "sumiram" da lista.
const reabrindo = ref(false)
async function reabrirUltimaImpressao() {
  if (!auth.lojaAtualId) { notif.aviso('Selecione uma loja para reabrir a última impressão.'); return }
  const escopo = auth.lojaAtual ? ` de ${auth.lojaAtual.nome}` : ''
  if (!confirm(`⚠️ ATENÇÃO — ler antes de confirmar:\n\nIsto vai REABRIR a última remessa de etiquetas${escopo} e marcar TODOS aqueles produtos como "etiqueta desatualizada" de novo — eles voltam para a fila de reimpressão e o alerta "imprimir etiquetas" reaparece.\n\nUse SOMENTE se a impressora falhou e as etiquetas NÃO saíram.\n\nSe as etiquetas saíram normalmente, clique em CANCELAR.`)) return
  reabrindo.value = true
  try {
    const r = await api.post('/produtos/etiquetas-reabrir-ultima', { localEstoqueId: auth.lojaAtualId })
    const n = r.data?.reabertos ?? 0
    if (!n) { notif.aviso('Nenhuma impressão recente para reabrir nesta loja.'); return }
    await carregarDesatualizadas()
    notif.ok(`${n} etiqueta(s) reaberta(s) para reimpressão. Confira a lista e mande imprimir de novo.`)
  } catch { notif.erro('Erro ao reabrir a última impressão.') }
  finally { reabrindo.value = false }
}

// Carrega os ENCALHADOS (nunca vendidos) para imprimir etiqueta em lote. grupo: 'sem' | 'com' | 'todos'.
const carregandoEnc = ref(false)
async function carregarEncalhados(grupo: string = 'sem') {
  carregandoEnc.value = true
  try {
    const { data } = await api.get('/produtos/encalhados', { params: { empresaId: auth.empresaId } })
    const ids = new Set<string>([
      ...(grupo !== 'com' ? (data?.semEtiqueta ?? []) : []),
      ...(grupo !== 'sem' ? (data?.comEtiqueta ?? []) : []),
    ])
    if (!ids.size) { notif.aviso('Nenhum produto encalhado para etiquetar.'); return }
    const r = await api.get('/produtos', {
      params: { empresaId: auth.empresaId, ativo: true, pagina: 1, tamanhoPagina: 5000 },
    })
    const todos = (r.data?.itens ?? r.data ?? [])
    const novos = todos.filter((p: any) => ids.has(p.id) && !produtosSel.value.find(x => x.id === p.id))
    await Promise.all(novos.map((p: any) => enriquecerValidadeRegistrada(p)))
    novos.forEach((p: any) => produtosSel.value.push(p))
    notif.ok(`${novos.length} produto(s) encalhado(s) adicionado(s) para etiquetar.`)
  } catch { notif.erro('Erro ao carregar os encalhados.') }
  finally { carregandoEnc.value = false }
}

let timer: any
async function buscarProdutos(q: string) {
  if (!q || q.length < 2) { sugestoes.value = []; return }
  clearTimeout(timer)
  timer = setTimeout(async () => {
    buscando.value = true
    try {
      const r = await api.get('/produtos/buscar', { params: { empresaId: auth.empresaId, q } })
      sugestoes.value = r.data ?? []
    } finally { buscando.value = false }
  }, 300)
}

/**
 * Enter no campo de busca = "bipe" do leitor de código de barras.
 * Procura o EAN exato e adiciona o produto direto; se não achar pelo EAN,
 * cai para o único resultado da busca por texto. O campo é limpo e mantém o
 * foco, permitindo bipar vários produtos em sequência.
 */
async function buscarPorCodigoBarras() {
  const q = (buscaProdutoTexto.value ?? '').trim()
  if (!q) return
  buscando.value = true
  try {
    const r = await api.get('/produtos/buscar', { params: { empresaId: auth.empresaId, q } })
    const achados: any[] = r.data ?? []
    const exato = achados.find(p => (p.codigoBarras ?? '') === q)
                ?? (achados.length === 1 ? achados[0] : null)
    if (exato) {
      const jaTinha = produtosSel.value.some(p => p.id === exato.id)
      await adicionarProdutoObj(exato)
      notif.ok(jaTinha ? `${exato.descricao} já estava na lista.` : `${exato.descricao} adicionado.`)
      buscaProdutoTexto.value = ''
      sugestoes.value = []
    } else if (achados.length) {
      sugestoes.value = achados            // vários: usuário escolhe na lista
    } else {
      notif.aviso(`Nenhum produto encontrado para "${q}".`)
    }
  } catch {
    notif.erro('Erro ao buscar produto.')
  } finally { buscando.value = false }
}

// Busca a validade REGISTRADA (Controle de Validade) do produto e guarda no objeto.
// Considera SÓ lotes com estoque (Quantidade > 0) — mesma regra do painel de
// Controle de Validade — para não pegar a validade de lote antigo/zerado.
// Escolhe o lote a vencer mais próximo (FEFO); se todos vencidos, o mais recente.
async function enriquecerValidadeRegistrada(p: any) {
  if (!p?.id) return
  try {
    const { data } = await api.get(`/lotes/produto/${p.id}`, { params: { empresaId: auth.empresaId } })
    const datas = (Array.isArray(data) ? data : [])
      .filter((l: any) => Number(l.quantidade ?? l.Quantidade ?? 0) > 0)
      .map((l: any) => (l.dataValidade ?? l.DataValidade))
      .filter(Boolean)
      .map((d: string) => String(d).slice(0, 10))
      .sort()
    if (!datas.length) return
    const hoje = new Date().toISOString().slice(0, 10)
    p._validadeRegistrada = datas.find((d: string) => d >= hoje) ?? datas[datas.length - 1]
  } catch { /* sem lote registrado: usa o padrão */ }
}

// Validade da etiqueta para um produto: prioriza a validade REGISTRADA no Controle
// de Validade; senão, hoje + validadeEmDias; por fim, a data global do formulário.
function validadeProduto(p: any): string {
  if (p?._validadeRegistrada) return p._validadeRegistrada
  // Produto por kg (balança) sem validade registrada no Controle de Validade:
  // usa hoje + 1 ano.
  if (p?.produtoBalanca) {
    const d = new Date(); d.setFullYear(d.getFullYear() + 1)
    return d.toISOString().slice(0, 10)
  }
  if (p?.validadeEmDias && Number(p.validadeEmDias) > 0) {
    const d = new Date(); d.setDate(d.getDate() + Number(p.validadeEmDias))
    return d.toISOString().slice(0, 10)
  }
  return validade.value || ''
}

async function adicionarProdutoObj(p: any) {
  if (!produtosSel.value.find(x => x.id === p.id)) {
    // Enriquecer ANTES do push: setar depois cai no objeto cru e o Vue não re-renderiza.
    await enriquecerValidadeRegistrada(p)
    produtosSel.value.push(p)
  }
  sugestoes.value = []
  buscaProdutoTexto.value = ''
}

function removerProduto(id: string) {
  produtosSel.value = produtosSel.value.filter(p => p.id !== id)
}

// ── Impressão ────────────────────────────────────────────────────────────────
function imprimir() {
  const idsImpressos = produtosParaImprimir.value.map((p: any) => p.id)
  // EcoGranel e Black Friday (produtos por peso) → template PADRÃO 6/A4 compartilhado.
  if (template.value === 'ecogranel' || ehBf.value || ehPromo.value) {
    imprimirEtiquetasKg(
      produtosParaImprimir.value.map((p: any) => ({
        nome: p.descricao,
        codigoPlu: p.codigoPlu ?? p.codigo,
        // Black Friday / Promoção: preço já com desconto; EcoGranel: preço normal
        precoVenda: ehBf.value ? precoBf(p) : ehPromo.value ? precoPromoAuto(p) : p.precoVenda,
        precoOriginal: (ehBf.value || ehPromo.value) ? p.precoVenda : undefined, // preço cheio (mostra "de" riscado)
        validade: validadeProduto(p) || validadePadrao(),
        descricao: p.descricaoComplementar || textoDescritivoEco.value,
      })),
      ehBf.value
        ? {
            copiasPorItem: Math.max(1, qtdEtiquetas.value || 1),
            blackFriday: true,
            rotuloPreco: 'cada 100g',
            fraseRodape: 'APROVEITE A BLACK FRIDAY!',
          }
        : ehPromo.value
        ? {
            copiasPorItem: Math.max(1, qtdEtiquetas.value || 1),
            promocao: true,
            rotuloPreco: 'cada 100g',
            fraseRodape: 'APROVEITE A PROMOÇÃO!',
          }
        : {
            copiasPorItem: Math.max(1, qtdEtiquetas.value || 1),
            bordaCor: borda.value.cor,
            bordaEspessura: borda.value.espessura,
            marcaDaguaUrl: marcaDaguaUrl.value || undefined,
            rotuloPreco: ecoCfg.value.rotuloPreco,
            fraseRodape: ecoCfg.value.fraseRodape,
            corTexto: ecoCfg.value.corTexto,
            corPreco: ecoCfg.value.corPreco,
            corRotulo: ecoCfg.value.corRotulo,
            fundoCor: ecoCfg.value.fundoCor,
            marcaOpacidade: ecoCfg.value.marcaOpacidade,
            escalaNome: ecoCfg.value.escalaNome,
            escalaPreco: ecoCfg.value.escalaPreco,
          }
    )
    confirmarImpressao(idsImpressos)
    return
  }

  const area = document.getElementById('area-impressao')
  if (!area) return
  const bordaCor = borda.value.cor
  const w = window.open('', '_blank')!
  w.document.write(`
    <html><head><title>Etiquetas EcoGranel</title>
    <style>
      * { box-sizing: border-box; margin: 0; padding: 0; }
      body { margin: 0; font-family: Arial, sans-serif; background: white; }
      .etiquetas-grid { display: flex; flex-wrap: wrap; gap: 6mm; padding: 5mm; }

      /* ── EcoGranel ── */
      .etiqueta-eco {
        width: 10cm; height: 10cm;
        display: flex; flex-direction: column;
        background: linear-gradient(160deg, #ffffff 0%, #e8f5e9 60%, #c8e6c9 100%);
        position: relative; overflow: hidden; page-break-inside: avoid;
        -webkit-print-color-adjust: exact; print-color-adjust: exact;
      }
      .etiqueta-eco.bf { background: radial-gradient(130% 100% at 50% 0%,#1c1c1c 0%,#0a0a0a 62%) !important; color: #fff; justify-content: center; }
      .etiqueta-eco.bf > .fern { position: absolute; width: 38mm; height: auto; z-index: 0; opacity: .5; }
      .etiqueta-eco.bf .fern.tr { top: -4mm; right: -5mm; transform: scaleY(-1); }
      .etiqueta-eco.bf .fern.tl { top: -4mm; left: -5mm; transform: scale(-1, -1); }
      .etiqueta-eco.bf .fern.br { bottom: -4mm; right: -5mm; }
      .etiqueta-eco.bf .fern.bl { bottom: -4mm; left: -5mm; transform: scaleX(-1); }
      .etiqueta-eco.bf .bf-content { position: relative; z-index: 1; width: 100%; text-align: center; }
      .etiqueta-eco.bf .bf-content > * { width: 100%; text-align: center; }
      .eco-bf-black { font-size: 11pt; letter-spacing: 9px; font-weight: 700; line-height: 1; color: #fff; text-indent: 9px; }
      .bf-friday { display: block; width: 60%; height: auto; margin: -1mm auto 0; }
      .etiqueta-eco.bf .bf-hr { width: 72%; height: 0; border-top: 1.5px solid #9a9a9a; margin: 1.5mm auto; }
      .etiqueta-eco.bf .bf-nome { font-size: 11pt; font-weight: 900; color: #fff; text-align: center; margin-top: 1.5mm; line-height: 1.08; letter-spacing: .3px; }
      .etiqueta-eco.bf .bf-cod { font-size: 8pt; color: #9a9a9a; text-align: center; letter-spacing: 1px; margin-top: 0.5mm; }
      .etiqueta-eco.bf .bf-de { font-size: 8.5pt; color: #cfcfcf; text-align: center; margin-top: 2mm; }
      .etiqueta-eco.bf .bf-de s { color: #9a9a9a; }
      .etiqueta-eco.bf .bf-preco { font-size: 46pt; font-weight: 900; color: #ff2e2e; text-align: center; line-height: 1; margin-top: 0.5mm; }
      .etiqueta-eco.bf .bf-preco .cur { font-size: 18pt; font-weight: 800; vertical-align: 28%; margin-right: 2px; }
      .etiqueta-eco.bf .bf-preco .cent { font-size: 24pt; vertical-align: 28%; }
      .etiqueta-eco.bf .bf-100 { font-size: 8.5pt; color: #e0e0e0; text-align: center; margin-top: 1.5mm; }
      .etiqueta-eco.bf .bf-val { font-size: 9pt; color: #fff; text-align: center; margin-top: 1mm; }
      .etiqueta-eco.bf .bf-foot { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 2mm; margin-top: 1.5mm; }
      .etiqueta-eco.bf .bf-frase { font-size: 10.5pt; font-weight: 900; color: #fff; text-align: center; max-width: 100%; line-height: 1.15; letter-spacing: .5px; }
      .eco-marca-dagua {
        position: absolute; right: 4mm; top: 30%; width: 42%; opacity: 0.18;
        pointer-events: none; user-select: none;
      }
      .eco-nome {
        font-size: 13pt; font-weight: 900; color: #111;
        text-align: center; padding: 5mm 4mm 2mm; line-height: 1.15;
        letter-spacing: 0.5px;
      }
      .eco-nome-plu { font-weight: 700; font-size: 0.85em; }
      .eco-preco-bloco { text-align: center; padding: 2mm 4mm 1mm; }
      .eco-preco-valor { font-size: 60pt; font-weight: 900; color: #111; line-height: 1; letter-spacing: -2px; }
      .eco-preco-label { font-size: 9pt; color: #444; margin-top: 1mm; }
      .eco-validade { text-align: center; font-size: 10pt; color: #222; padding: 1mm 4mm; }
      .eco-descricao { font-size: 8.5pt; color: #333; text-align: center;
        padding: 1.5mm 5mm; line-height: 1.35; font-weight: 500;
        flex: 1 1 auto; min-height: 0;
        display: -webkit-box; -webkit-line-clamp: 5; -webkit-box-orient: vertical; overflow: hidden; }
      .eco-rodape {
        flex: 0 0 auto; display: flex; align-items: flex-end;
        justify-content: space-between; padding: 2mm 4mm 3mm;
      }
      .eco-qr-wrap { display: flex; flex-direction: column; align-items: center; }
      .eco-qr-wrap canvas { width: 22mm !important; height: 22mm !important; display: block; }
      .eco-qr-saibamais { font-size: 7pt; font-weight: 900; color: #1b5e20; text-align: center; margin-top: 0.5mm; }
      .eco-frase { font-size: 10pt; font-weight: 900; color: #1b5e20;
        text-align: right; max-width: 52%; line-height: 1.3; }

      /* ── Pote ── */
      .etiqueta-pote {
        width: 9cm; height: 9cm; display: flex; flex-direction: column;
        page-break-inside: avoid; overflow: hidden; background: white;
      }
      .pote-nome { color: white; text-align: center; font-weight: bold;
        font-size: 14pt; padding: 6px 8px; word-break: break-word; line-height: 1.2; }
      .pote-corpo { flex: 1; display: flex; flex-direction: column;
        justify-content: center; align-items: center; gap: 6px; padding: 8px; }
      .pote-descricao { font-size: 9pt; color: #444; text-align: center; line-height: 1.3;
        white-space: pre-line; }
      .pote-plu { display: flex; align-items: center; gap: 4px; font-size: 8pt; color: #666; }
      .pote-plu-valor { font-family: monospace; font-weight: bold; font-size: 10pt; color: #333; }
      .pote-preco-bloco { text-align: center; }
      .pote-preco-valor { font-size: 22pt; font-weight: bold; color: ${bordaCor}; line-height: 1; }
      .pote-preco-label { font-size: 8pt; color: #666; margin-top: 1px; }
      .pote-validade { font-size: 8pt; color: #555; }
      .pote-frase { text-align: center; font-style: italic; font-weight: bold;
        font-size: 10pt; padding: 6px 8px; color: ${bordaCor};
        letter-spacing: 0.5px; border-top: 1px solid; }

      /* ── Padrão ── */
      .etiqueta { border: 1px solid #999; display: flex; flex-direction: column;
        justify-content: space-between; overflow: hidden; page-break-inside: avoid;
        background: white; }
      .etq-nome { font-weight: bold; word-break: break-word; }
      .etq-de { text-decoration: line-through; font-size: 0.8em; color: #888; }
      .etq-preco { font-weight: bold; color: #1565C0; }
      .etq-ean { font-family: monospace; font-size: 0.75em; }
      .etq-rodape { font-size: 0.7em; color: #555; display: flex; gap: 4px; flex-wrap: wrap; }
      @media print { @page { margin: 5mm; } }
    </style></head><body>
    ${area.outerHTML}
    <script>window.onload=()=>{ window.print(); window.close(); }<\/script>
    </body></html>
  `)
  w.document.close()
  confirmarImpressao(idsImpressos)
}
</script>

<style>
.etiquetas-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  background: #f0f0f0;
  padding: 16px;
  border-radius: 8px;
  min-height: 200px;
}

/* ── EcoGranel ── */
.etiqueta-eco {
  width: 380px;
  height: 380px;
  display: flex;
  flex-direction: column;
  background: linear-gradient(160deg, #ffffff 0%, #e8f5e9 60%, #c8e6c9 100%);
  position: relative;
  overflow: hidden;
  box-shadow: 0 3px 12px rgba(0,0,0,.15);
  transition: box-shadow .2s;
}
.etiqueta-eco:hover { box-shadow: 0 6px 20px rgba(0,0,0,.22); }
.etiqueta-eco.bf { background: radial-gradient(130% 100% at 50% 0%,#1c1c1c 0%,#0a0a0a 62%) !important; color: #fff; justify-content: center; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
.etiqueta-eco.bf > .fern { position: absolute; width: 145px; height: auto; z-index: 0; opacity: .5; }
.etiqueta-eco.bf .fern.tr { top: -15px; right: -18px; transform: scaleY(-1); }
.etiqueta-eco.bf .fern.tl { top: -15px; left: -18px; transform: scale(-1, -1); }
.etiqueta-eco.bf .fern.br { bottom: -15px; right: -18px; }
.etiqueta-eco.bf .fern.bl { bottom: -15px; left: -18px; transform: scaleX(-1); }
.etiqueta-eco.bf .bf-content { position: relative; z-index: 1; width: 100%; text-align: center; }
.etiqueta-eco.bf .bf-content > * { width: 100%; text-align: center; }
.eco-bf-black { font-size: 15px; letter-spacing: 9px; font-weight: 700; line-height: 1; color: #fff; text-indent: 9px; }
.bf-friday { display: block; width: 60%; height: auto; margin: -4px auto 0; }
.etiqueta-eco.bf .bf-hr { width: 72%; height: 0; border-top: 1.5px solid #9a9a9a; margin: 6px auto; }
.etiqueta-eco.bf .bf-nome { font-size: 15px; font-weight: 900; color: #fff; text-align: center; margin-top: 6px; line-height: 1.08; letter-spacing: .3px; }
.etiqueta-eco.bf .bf-cod { font-size: 11px; color: #9a9a9a; text-align: center; letter-spacing: 1px; margin-top: 2px; }
.etiqueta-eco.bf .bf-de { font-size: 12px; color: #cfcfcf; text-align: center; margin-top: 7px; }
.etiqueta-eco.bf .bf-de s { color: #9a9a9a; }
.etiqueta-eco.bf .bf-preco { font-size: 62px; font-weight: 900; color: #ff2e2e; text-align: center; line-height: 1; margin-top: 2px; }
.etiqueta-eco.bf .bf-preco .cur { font-size: 24px; font-weight: 800; vertical-align: 28%; margin-right: 2px; }
.etiqueta-eco.bf .bf-preco .cent { font-size: 32px; vertical-align: 28%; }
.etiqueta-eco.bf .bf-100 { font-size: 12px; color: #e0e0e0; text-align: center; margin-top: 6px; }
.etiqueta-eco.bf .bf-val { font-size: 13px; color: #fff; text-align: center; margin-top: 4px; }
.etiqueta-eco.bf .bf-foot { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; margin-top: 6px; }
.etiqueta-eco.bf .bf-frase { font-size: 14px; font-weight: 900; color: #fff; text-align: center; max-width: 100%; line-height: 1.15; letter-spacing: .5px; }

/* ── Promoção (natural verde + preço vermelho) ── */
.etiqueta-eco.promo { background: linear-gradient(160deg,#ffffff 0%,#e8f5e9 55%,#dcedc8 100%) !important; justify-content: center; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
.etiqueta-eco.promo > .fern { position: absolute; width: 140px; height: auto; z-index: 0; opacity: .4; }
.etiqueta-eco.promo .fern.tr { top: -15px; right: -18px; transform: scaleY(-1); }
.etiqueta-eco.promo .fern.tl { top: -15px; left: -18px; transform: scale(-1, -1); }
.etiqueta-eco.promo .fern.br { bottom: -15px; right: -18px; }
.etiqueta-eco.promo .fern.bl { bottom: -15px; left: -18px; transform: scaleX(-1); }
.etiqueta-eco.promo .bf-content { position: relative; z-index: 1; width: 100%; text-align: center; }
.etiqueta-eco.promo .bf-content > * { width: 100%; text-align: center; }
.etiqueta-eco.promo .pr-badge { display: inline-block; width: auto; background: #d32f2f; color: #fff; font-weight: 900; font-size: 17px; letter-spacing: 2px; padding: 4px 22px; border-radius: 999px; }
.etiqueta-eco.promo .bf-hr { width: 72%; height: 0; border-top: 1.5px solid #2e7d3288; margin: 8px auto; }
.etiqueta-eco.promo .bf-nome { font-size: 15px; font-weight: 900; color: #1b5e20; text-align: center; margin-top: 6px; line-height: 1.08; letter-spacing: .3px; }
.etiqueta-eco.promo .bf-cod { font-size: 11px; color: #777; text-align: center; letter-spacing: 1px; margin-top: 2px; }
.etiqueta-eco.promo .bf-de { font-size: 12px; color: #555; text-align: center; margin-top: 7px; }
.etiqueta-eco.promo .bf-de s { color: #999; }
.etiqueta-eco.promo .bf-preco { font-size: 62px; font-weight: 900; color: #d32f2f; text-align: center; line-height: 1; margin-top: 2px; }
.etiqueta-eco.promo .bf-preco .cur { font-size: 24px; font-weight: 800; vertical-align: 28%; margin-right: 2px; }
.etiqueta-eco.promo .bf-preco .cent { font-size: 32px; vertical-align: 28%; }
.etiqueta-eco.promo .bf-100 { font-size: 12px; color: #555; text-align: center; margin-top: 6px; }
.etiqueta-eco.promo .bf-val { font-size: 13px; color: #222; text-align: center; margin-top: 4px; }
.etiqueta-eco.promo .bf-foot { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 8px; margin-top: 6px; }
.etiqueta-eco.promo .bf-frase { font-size: 14px; font-weight: 900; color: #1b5e20; text-align: center; max-width: 100%; line-height: 1.15; letter-spacing: .5px; }

.eco-marca-dagua {
  position: absolute;
  right: 8px;
  top: 30%;
  width: 42%;
  opacity: 0.18;
  pointer-events: none;
  user-select: none;
}

.eco-nome {
  font-size: 16px;
  font-weight: 900;
  color: #111;
  text-align: center;
  padding: 14px 12px 6px;
  line-height: 1.2;
  letter-spacing: 0.3px;
  z-index: 1;
}
.eco-nome-plu {
  font-size: 0.85em;
  font-weight: 700;
}

.eco-preco-bloco {
  display: table;              /* encolhe à largura do valor e centraliza */
  margin: 0 auto;
  padding: 4px 12px 2px;
  z-index: 1;
}
.eco-preco-valor {
  font-size: 86px;
  font-weight: 900;
  color: #111;
  line-height: 1;
  letter-spacing: -3px;
  font-family: Arial Black, Arial, sans-serif;
}
.eco-preco-label {
  font-size: 13px;
  color: #555;
  margin-top: 2px;
  text-align: right;          /* alinhado à borda direita do valor */
}

.eco-validade {
  text-align: center;
  font-size: 13px;
  color: #222;
  padding: 2px 12px 4px;
  z-index: 1;
}

.eco-descricao {
  flex: 1 1 auto;              /* ocupa o espaço entre validade e QR, sem empurrar */
  min-height: 0;
  display: -webkit-box;        /* limita a 5 linhas (o resto é cortado com …) */
  -webkit-line-clamp: 5;
  -webkit-box-orient: vertical;
  overflow: hidden;
  font-size: 12px;
  color: #333;
  text-align: center;
  padding: 4px 16px 6px;
  line-height: 1.4;
  font-weight: 500;
  z-index: 1;
}

.eco-rodape {
  flex: 0 0 auto;              /* rodapé fixo no fim — QR nunca é cortado */
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  padding: 6px 14px 12px;
  z-index: 1;
}

.eco-qr-wrap { display: flex; flex-direction: column; align-items: center; }
.eco-qr-saibamais {
  font-size: 9px; font-weight: 900; color: #1b5e20; text-align: center; margin-top: 2px;
}

.eco-qr-canvas {
  width: 90px !important;
  height: 90px !important;
  display: block;
}

.eco-frase {
  font-size: 13px;
  font-weight: 900;
  color: #1b5e20;
  text-align: right;
  max-width: 55%;
  line-height: 1.3;
}

/* ── Gôndola Zebra ── */
.gondola-grid {
  display: flex; flex-wrap: wrap; gap: 10px;
  background: #e8e8e8; padding: 12px; border-radius: 8px; min-height: 100px;
}
.gondola-etiqueta {
  background: white;
  display: flex; flex-direction: column; justify-content: center; align-items: center; text-align: center;
  padding: 5px 7px; box-shadow: 0 1px 4px rgba(0,0,0,.1);
  overflow: hidden; position: relative;
}
.gon-nome { font-weight: bold; color: #1a1a1a; line-height: 1.2; word-break: break-word; }
.gon-preco { font-weight: 900; color: #111; line-height: 1; display: flex; align-items: baseline; justify-content: center; gap: 2px; }
.gon-preco-rs { font-size: 0.45em; font-weight: bold; padding-bottom: 2px; }
.gon-por-unidade { font-size: 9px; color: #555; }
.gon-validade { font-size: 8px; color: #888; }
.gon-barcode-area { display: flex; align-items: center; justify-content: center; margin-top: 3px; max-width: 100%; }
.gon-barcode-area svg { max-width: 100%; height: auto; }
.gon-plu { font-size: 8px; color: #666; font-family: monospace; }

/* ── Pote 9×9cm ── */
.etiqueta-pote {
  width: 340px; height: 340px; display: flex; flex-direction: column;
  background: white; overflow: hidden;
  box-shadow: 0 2px 8px rgba(0,0,0,.12); transition: box-shadow .2s;
}
.etiqueta-pote:hover { box-shadow: 0 4px 16px rgba(0,0,0,.2); }
.pote-nome { text-align: center; font-weight: bold; font-size: 15px;
  padding: 10px 12px; word-break: break-word; line-height: 1.25; color: white; }
.pote-corpo { flex: 1; display: flex; flex-direction: column;
  justify-content: center; align-items: center; gap: 8px; padding: 10px 12px; }
.pote-descricao { font-size: 11px; color: #555; text-align: center; line-height: 1.4;
  white-space: pre-line; }
.pote-plu { display: flex; align-items: center; gap: 5px; font-size: 11px; color: #666; }
.pote-plu-valor { font-family: monospace; font-weight: bold; font-size: 13px; color: #333; }
.pote-preco-bloco { text-align: center; }
.pote-preco-valor { font-size: 32px; font-weight: bold; line-height: 1; }
.pote-preco-label { font-size: 11px; color: #666; margin-top: 2px; }
.pote-validade { font-size: 11px; color: #555; display: flex; align-items: center; }
.pote-frase { text-align: center; font-style: italic; font-weight: bold;
  font-size: 12px; padding: 8px 10px; letter-spacing: 0.3px; border-top: 1px solid; }

/* ── Templates padrão ── */
.etiqueta { border: 1px solid #ccc; background: white; display: flex;
  flex-direction: column; justify-content: space-between;
  box-sizing: border-box; overflow: hidden; cursor: default; transition: box-shadow .2s; }
.etiqueta:hover { box-shadow: 0 2px 8px rgba(0,0,0,.15); }
.etq-preco { color: #1565C0; }
.etq-de { text-decoration: line-through; font-size: .75em; color: #888; }
.etq-ean { font-family: monospace; font-size: .7em; letter-spacing: 2px; }
.etq-rodape { font-size: .68em; color: #666; display: flex; gap: 4px; flex-wrap: wrap; }
</style>
