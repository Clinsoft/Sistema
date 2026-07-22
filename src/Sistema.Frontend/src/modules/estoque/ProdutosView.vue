<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col cols="12" sm=""><h2 class="text-h5 font-weight-bold">Produtos</h2></v-col>
      <v-col cols="12" sm="auto" class="d-flex flex-wrap gap-2">
        <v-btn v-if="!ehAtendente" color="warning" variant="tonal" prepend-icon="mdi-merge" :block="mobile"
          @click="abrirUnificar">Unificar duplicados</v-btn>
        <v-btn v-if="!ehAtendente" color="green-darken-1" variant="tonal" prepend-icon="mdi-cloud-sync-outline" :block="mobile"
          :loading="sincronizandoSite" @click="sincronizarSite"
          title="Envia os produtos por kg (nome, descrição, foto, categoria e tabela nutricional) para o site ecogranel.com.br">
          Sincronizar site</v-btn>
        <v-btn v-if="!ehAtendente" color="primary" prepend-icon="mdi-plus" :block="mobile" @click="abrirNovo">Novo Produto</v-btn>
      </v-col>
    </v-row>

    <!-- Dialog: Ajuste de estoque rápido -->
    <v-dialog v-model="dlgAjuste" max-width="480">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2">
          <v-icon color="teal">mdi-tune-variant</v-icon>
          Ajustar estoque
        </v-card-title>
        <v-card-text>
          <div class="text-body-2 font-weight-medium mb-1">{{ ajuste.produtoNome }}</div>
          <div class="text-caption text-medium-emphasis mb-3">
            Estoque atual no sistema: <b>{{ ajuste.estoqueAtual }}</b>
          </div>
          <v-select v-model="ajuste.localEstoqueId" :items="locaisEstoque" item-title="nome" item-value="id"
            label="Local de estoque *" variant="outlined" density="compact" class="mb-2" hide-details />
          <v-row dense class="mt-1">
            <v-col cols="6">
              <v-text-field v-model.number="ajuste.quantidadeContada" label="Qtd. física contada *"
                type="number" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="6">
              <v-text-field :model-value="ajusteDiferenca" label="Diferença" readonly
                variant="outlined" density="compact" hide-details
                :prefix="ajusteDiferenca > 0 ? '+' : ''"
                :color="ajusteDiferenca > 0 ? 'success' : ajusteDiferenca < 0 ? 'error' : undefined" />
            </v-col>
          </v-row>
          <v-text-field v-model="ajuste.observacao" label="Observação" variant="outlined"
            density="compact" hide-details class="mt-2" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgAjuste = false">Cancelar</v-btn>
          <v-btn color="teal" rounded="lg" :loading="ajustando"
            :disabled="!ajuste.localEstoqueId || ajusteDiferenca === 0"
            @click="confirmarAjuste">Aplicar ajuste</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Unificar produtos duplicados -->
    <v-dialog v-model="dlgUnificar" max-width="700" scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2">
          <v-icon color="warning">mdi-merge</v-icon>
          Unificar produtos duplicados
        </v-card-title>
        <v-card-text>
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            Escolha em cada grupo qual produto <b>manter</b>. Os demais serão fundidos nele:
            estoque somado e todo o histórico (vendas, entradas, movimentações) repontado.
            <b>Ação irreversível.</b>
          </v-alert>

          <!-- Seleção manual: unir quaisquer produtos, mesmo sem auto-detecção -->
          <v-card variant="outlined" rounded="lg" class="mb-4 pa-3">
            <div class="d-flex align-center gap-2 mb-2">
              <v-icon size="small" color="primary">mdi-cursor-default-click-outline</v-icon>
              <span class="font-weight-medium">Seleção manual</span>
              <span class="text-caption text-medium-emphasis">— escolha você mesmo quais unir</span>
            </div>
            <v-autocomplete v-model="selManual" :items="produtosOrdenados"
              :item-title="itemTituloProduto" item-value="id" multiple chips closable-chips
              label="Buscar e selecionar produtos (2 ou mais)" variant="outlined" density="compact"
              hide-details clearable class="mb-2" />
            <template v-if="produtosSelManual.length >= 2">
              <div class="text-caption text-medium-emphasis mb-1">Qual <b>manter</b>? (os outros serão fundidos nele)</div>
              <v-radio-group v-model="manterManual" density="compact" hide-details>
                <v-radio v-for="p in produtosSelManual" :key="p.id" :value="p.id">
                  <template #label>
                    <div class="d-flex align-center gap-2 flex-wrap">
                      <span class="font-weight-medium">{{ p.descricao }}</span>
                      <v-chip size="x-small" variant="tonal">cód. {{ p.codigo }}</v-chip>
                      <span class="text-caption text-medium-emphasis">estoque {{ p.estoqueAtual }} · R$ {{ fmtN(p.precoVenda) }}</span>
                    </div>
                  </template>
                </v-radio>
              </v-radio-group>
              <v-btn color="warning" size="small" rounded="lg" class="mt-2" :loading="unificando"
                :disabled="!manterManual" @click="unificarManual">
                Unificar selecionados ({{ produtosSelManual.length }})
              </v-btn>
            </template>
            <div v-else-if="selManual.length === 1" class="text-caption text-medium-emphasis">
              Selecione pelo menos mais um produto para unir.
            </div>
          </v-card>

          <div class="text-caption text-medium-emphasis mb-2">Grupos detectados automaticamente:</div>

          <div v-if="carregandoDup" class="text-center py-6">
            <v-progress-circular indeterminate color="warning" />
          </div>
          <div v-else-if="!gruposDup.length" class="text-center text-medium-emphasis py-6">
            <v-icon icon="mdi-check-circle-outline" color="success" size="40" />
            <div class="mt-2">Nenhum produto duplicado encontrado.</div>
          </div>

          <v-card v-for="(g, gi) in gruposDup" :key="gi" variant="outlined" rounded="lg" class="mb-3 pa-3"
            :color="g.similar ? 'warning' : undefined">
            <div class="d-flex align-center gap-2 mb-2">
              <span class="text-caption text-medium-emphasis">{{ g.chave }}</span>
              <v-chip v-if="g.similar" size="x-small" color="warning" variant="flat">
                <v-icon start size="x-small">mdi-alert</v-icon>similar — confira
              </v-chip>
            </div>
            <v-radio-group v-model="g._manter" density="compact" hide-details>
              <v-radio v-for="p in g.produtos" :key="p.id" :value="p.id">
                <template #label>
                  <div class="d-flex align-center gap-2 flex-wrap">
                    <span class="font-weight-medium">{{ p.descricao }}</span>
                    <v-chip size="x-small" variant="tonal">cód. {{ p.codigo }}</v-chip>
                    <span class="text-caption text-medium-emphasis">
                      estoque {{ p.estoqueAtual }} · R$ {{ fmtN(p.precoVenda) }}
                      · criado {{ fmtData(p.criadoEm) }}
                    </span>
                  </div>
                </template>
              </v-radio>
            </v-radio-group>
            <div class="text-caption text-medium-emphasis mt-1">
              Os outros {{ g.produtos.length - 1 }} serão fundidos no selecionado.
            </div>
          </v-card>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgUnificar = false">Fechar</v-btn>
          <v-btn v-if="gruposDup.length" color="warning" rounded="lg" :loading="unificando"
            @click="confirmarUnificar">Unificar ({{ gruposDup.length }})</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <GuiaPassos
      id="produtos"
      titulo="Como cadastrar e manter produtos"
      :passos="[
        'Clique em <b>Novo Produto</b> e preencha as abas <b>Geral</b> (descrição, categoria, unidade), <b>Fiscal</b> (NCM, CFOP, CST/CSOSN) e <b>Preços</b> (custo e markup).',
        'O <b>código interno</b> é sequencial (a partir de 3001) e é o número usado na balança — não use código aleatório.',
        'Na aba <b>Nutricional</b>, busque o alimento na base <b>TACO</b> pelo nome para preencher a tabela automaticamente.',
        'Produtos criados durante a importação de NF-e já vêm com dados fiscais e fornecedor vinculados — complete aqui o que faltar.',
        'Use os ícones da tabela para <b>editar</b>, <b>inativar</b> ou <b>excluir</b>. A lista atualiza sozinha após cada ação.',
      ]"
    />

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" md="5">
          <v-text-field v-model="busca" placeholder="Buscar por nome, código ou EAN…"
            prepend-inner-icon="mdi-magnify" variant="outlined" density="compact" hide-details
            clearable @update:model-value="listar" />
        </v-col>
        <v-col cols="12" md="3">
          <v-select v-model="filtroCategoria" :items="categorias" item-title="nome" item-value="id"
            label="Categoria" variant="outlined" density="compact" hide-details clearable
            @update:model-value="listar" />
        </v-col>
        <v-col cols="12" md="3">
          <v-autocomplete v-model="filtroFornecedor" :items="fornecedoresComProduto"
            item-title="razaoSocial" item-value="id" multiple chips closable-chips
            label="Fornecedor" variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" md="2">
          <v-select v-model="filtroAtivo"
            :items="[{title:'Ativos',value:true},{title:'Inativos',value:false},{title:'Todos',value:null}]"
            label="Status" variant="outlined" density="compact" hide-details
            @update:model-value="listar" />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" @click="listar">Filtrar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="produtosFiltrados" :loading="carregando" density="compact"
        hover :items-per-page="50" items-per-page-text="Itens por página"
        :items-per-page-options="[
          { title: '25', value: 25 },
          { title: '50', value: 50 },
          { title: '100', value: 100 },
          { title: 'Todos', value: -1 },
        ]">
        <template #item.unidadeSigla="{ item }">{{ item.unidadeSigla || '—' }}</template>
        <template #item.precoVenda="{ item }">R$ {{ fmtN(item.precoVenda) }}</template>
        <template #item.custoUnitario="{ item }">R$ {{ fmtN(item.custoUnitario) }}</template>
        <template #item.preco100g="{ item }">
          <span v-if="ehPorPeso(item)">R$ {{ fmtN(Math.round((item.precoVenda / 10) * 100) / 100) }}</span>
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <template #item.markup="{ item }">{{ item.markup ? fmtN(item.markup) : '—' }}</template>
        <template #item.estoqueAtual="{ item }">
          <v-chip :color="item.estoqueAtual <= item.estoqueMinimo ? 'error' : 'success'"
            size="small" variant="tonal">{{ item.estoqueAtual }}</v-chip>
        </template>
        <template #item.ativo="{ item }">
          <v-chip :color="item.ativo ? 'success' : 'default'" size="small" variant="tonal">
            {{ item.ativo ? 'Ativo' : 'Inativo' }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn v-if="!mobile" icon="mdi-tune-variant" size="x-small" variant="text" color="teal"
            title="Ajustar estoque" @click="abrirAjuste(item)" />
          <v-btn v-if="!mobile" icon="mdi-content-copy" size="x-small" variant="text" color="indigo"
            title="Duplicar produto" @click="duplicarProduto(item)" />
          <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" color="primary"
            title="Editar" @click="abrirEdicao(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- ===================== DIALOG PRODUTO ===================== -->
    <v-dialog v-model="dialog" max-width="860" persistent scrollable>
      <v-card rounded="xl">

        <!-- Cabeçalho fixo -->
        <div class="prod-header">
          <div class="d-flex align-center gap-3">
            <v-avatar color="primary" variant="tonal" size="42" rounded="lg">
              <v-icon :icon="editando ? 'mdi-package-variant' : 'mdi-plus-box'" size="22" />
            </v-avatar>
            <div>
              <div class="text-subtitle-1 font-weight-bold lh-1">
                {{ editando ? (form.descricao || 'Editar Produto') : 'Novo Produto' }}
              </div>
              <div class="text-caption text-medium-emphasis">
                {{ editando ? 'Código: ' + form.codigo : 'Preencha os dados e salve' }}
              </div>
            </div>
          </div>
          <v-btn icon="mdi-close" variant="text" size="small" @click="fecharDialog" />
        </div>

        <v-divider />

        <v-card-text class="pa-0" style="max-height:78vh;overflow-y:auto">
          <v-form ref="formulario">
            <div class="prod-form-body">

              <!-- ── SEÇÃO: IDENTIFICAÇÃO ───────────────────────── -->
              <div class="prod-secao">
                <div class="prod-secao-header">
                  <v-icon icon="mdi-information-outline" size="16" />
                  <span>Identificação</span>
                </div>

                <div class="prod-secao-body">
                  <!-- Imagem + dados lado a lado -->
                  <div class="d-flex gap-4 mb-4">
                    <!-- Foto -->
                    <div class="prod-foto-col">
                      <div class="prod-foto-box" @click="$refs.inputImgRef?.click()" :title="'Clique para ' + (form.imagemUrl ? 'trocar' : 'adicionar') + ' foto'">
                        <v-img v-if="form.imagemUrl || previewImagem"
                          :src="previewImagem || form.imagemUrl || ''"
                          cover width="110" height="110" />
                        <div v-else class="prod-foto-empty">
                          <v-icon icon="mdi-image-plus" size="32" color="grey-lighten-1" />
                          <span>Foto</span>
                        </div>
                      </div>
                      <input ref="inputImgRef" type="file" accept="image/jpeg,image/png,image/webp"
                        style="display:none" @change="onFileImagem" />
                      <v-btn v-if="form.imagemUrl" size="x-small" variant="text" color="error"
                        class="mt-1" :loading="removendoImagem" @click="removerImagem">
                        Remover
                      </v-btn>
                      <v-btn v-if="form.codigoBarras" size="x-small" variant="tonal" color="primary"
                        class="mt-1" prepend-icon="mdi-barcode-scan"
                        :loading="buscandoImagem" @click="buscarImagemPorCodigoBarras"
                        title="Buscar a foto do produto pelo código de barras (Open Food Facts)">
                        Buscar foto (EAN)
                      </v-btn>
                    </div>

                    <!-- Campos principais -->
                    <div class="flex-grow-1">
                      <v-text-field v-model="form.descricao" label="Nome do produto *"
                        variant="outlined" density="compact" class="mb-2" autofocus
                        :rules="[r => !!r || 'Obrigatório']" />
                      <v-row dense>
                        <v-col cols="5">
                          <v-text-field v-model="form.codigo" label="Código"
                            variant="outlined" density="compact"
                            placeholder="auto" persistent-placeholder
                            hint="Deixe em branco para gerar automático" />
                        </v-col>
                        <v-col cols="7">
                          <v-text-field v-model="form.codigoBarras" label="Código de barras (EAN)"
                            variant="outlined" density="compact"
                            prepend-inner-icon="mdi-barcode" />
                        </v-col>
                      </v-row>
                    </div>
                  </div>

                  <v-row dense>
                    <v-col cols="12" md="4">
                      <v-select v-model="form.categoriaId" :items="categorias" item-title="nome" item-value="id"
                        label="Categoria *" variant="outlined" density="compact"
                        :rules="[r => !!r || 'Obrigatório']"
                        @update:model-value="aplicarBalancaAuto">
                        <template #append-inner>
                          <v-btn icon="mdi-plus" size="x-small" variant="text" density="compact" tabindex="-1"
                            title="Adicionar categoria" @click.stop="abrirQuickAdd('categoria')" />
                        </template>
                      </v-select>
                    </v-col>
                    <v-col cols="12" md="4">
                      <v-select v-model="form.marcaId" :items="marcas" item-title="nome" item-value="id"
                        label="Marca *" variant="outlined" density="compact"
                        :rules="[r => !!r || 'Obrigatório']">
                        <template #append-inner>
                          <v-btn icon="mdi-plus" size="x-small" variant="text" density="compact" tabindex="-1"
                            title="Adicionar marca" @click.stop="abrirQuickAdd('marca')" />
                        </template>
                      </v-select>
                    </v-col>
                    <v-col cols="12" md="4">
                      <v-select v-model="form.unidadeMedidaId" :items="unidades" item-title="sigla" item-value="id"
                        label="Unidade *" variant="outlined" density="compact"
                        :rules="[r => !!r || 'Obrigatório']"
                        @update:model-value="aplicarBalancaAuto">
                        <template #append-inner>
                          <v-btn icon="mdi-plus" size="x-small" variant="text" density="compact" tabindex="-1"
                            title="Adicionar unidade" @click.stop="abrirQuickAdd('unidade')" />
                        </template>
                      </v-select>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-select v-model="form.fornecedorPrincipalId" :items="fornecedores"
                        item-title="razaoSocial" item-value="id"
                        label="Fornecedor principal" variant="outlined" density="compact" clearable />
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-select v-model="form.marcador" :items="marcadores" item-title="label" item-value="value"
                        label="Marcador" variant="outlined" density="compact" clearable>
                        <template #selection="{ item }">
                          <v-chip :color="item.raw.cor" size="small">
                            <v-icon :icon="item.raw.icone" size="x-small" class="mr-1"/>{{ item.raw.label }}
                          </v-chip>
                        </template>
                        <template #item="{ item, props }">
                          <v-list-item v-bind="props">
                            <template #prepend><v-icon :icon="item.raw.icone" :color="item.raw.cor"/></template>
                          </v-list-item>
                        </template>
                      </v-select>
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-text-field v-model="form.tags" label="Tags"
                        variant="outlined" density="compact"
                        prepend-inner-icon="mdi-tag-outline"
                        placeholder="ex: orgânico, sem glúten" />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-textarea v-model="form.descricaoComplementar" label="Descrição complementar"
                        variant="outlined" density="compact" rows="2" auto-grow
                        hint="Ex.: benefícios do produto. Use ✨ para sugerir com IA.">
                        <template #append-inner>
                          <v-btn :icon="true" size="x-small" variant="text" color="primary"
                            :loading="sugerindoDesc" :disabled="!form.descricao"
                            title="Sugerir descrição com IA (Gemini)" @click="sugerirDescricao">
                            <v-icon>mdi-creation</v-icon>
                          </v-btn>
                        </template>
                      </v-textarea>
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-text-field v-model="form.referencia" label="Referência interna"
                        variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-select v-model="form.tipoVariacao"
                        :items="[{title:'Produto simples',value:'Simples'},{title:'Com variação',value:'ComVariacao'}]"
                        label="Tipo" variant="outlined" density="compact" />
                    </v-col>
                  </v-row>

                  <!-- Opções booleanas em chips visuais.
                       Atenção: NÃO usar v-model no v-chip — no Vuetify o modelValue
                       controla a existência do chip (false = não renderiza), então a
                       opção desligada sumia da tela. Alternamos no @click. -->
                  <div class="prod-opcoes mt-2">
                    <v-chip v-for="o in opcoesProduto" :key="o.campo" size="small"
                      :color="(form as any)[o.campo] ? o.cor : undefined"
                      :variant="(form as any)[o.campo] ? 'flat' : 'outlined'"
                      @click="(form as any)[o.campo] = !(form as any)[o.campo]">
                      <v-icon start :icon="(form as any)[o.campo] ? 'mdi-check' : o.icone" />{{ o.rotulo }}
                    </v-chip>
                  </div>

                  <v-row dense class="mt-2" v-if="form.produtoBalanca || form.controlarValidade">
                    <v-col cols="6" md="3" v-if="form.produtoBalanca">
                      <v-text-field v-model.number="form.codigoPlu" label="Código PLU"
                        type="number" variant="outlined" density="compact"
                        hint="Número do produto na balança" persistent-hint />
                    </v-col>
                    <v-col cols="6" md="4" v-if="form.produtoBalanca || form.controlarValidade">
                      <v-text-field v-model.number="form.validadeEmDias" label="Validade (dias)"
                        type="number" variant="outlined" density="compact"
                        :hint="form.produtoBalanca
                          ? 'Dias que a balança soma à data de embalagem ao imprimir a etiqueta'
                          : 'Dias de validade após a fabricação'"
                        persistent-hint />
                    </v-col>
                  </v-row>
                  <v-alert v-if="form.produtoBalanca && !form.validadeEmDias" type="warning"
                    variant="tonal" density="compact" class="mt-2">
                    Produto de balança sem <b>validade (dias)</b>: a etiqueta da balança sai com validade zerada.
                  </v-alert>
                </div>
              </div>

              <!-- ── SEÇÃO: PREÇOS ──────────────────────────────── -->
              <div v-if="!ehAtendente" class="prod-secao">
                <div class="prod-secao-header">
                  <v-icon icon="mdi-tag-outline" size="16" />
                  <span>Preços</span>
                </div>
                <div class="prod-secao-body">
                  <!-- Resumo calculado -->
                  <div class="prod-preco-resumo mb-4">
                    <div class="prod-preco-item">
                      <span class="label">Custo</span>
                      <span class="val">R$ {{ fmtN(form.custoUnitario) }}</span>
                    </div>
                    <v-icon icon="mdi-chevron-right" color="grey-lighten-1" />
                    <div class="prod-preco-item destaque">
                      <span class="label">Preço de Venda</span>
                      <span class="val">R$ {{ fmtN(form.precoVenda) }}</span>
                    </div>
                    <div class="prod-preco-item badge">
                      <span class="label">Markup</span>
                      <span class="val">{{ markupExibir }}×</span>
                    </div>
                    <div class="prod-preco-item badge">
                      <span class="label">Margem</span>
                      <span class="val">{{ margemExibir }}%</span>
                    </div>
                  </div>

                  <v-row dense>
                    <v-col cols="6" md="3">
                      <v-text-field v-model.number="form.custoUnitario" label="Custo (R$)"
                        type="number" variant="outlined" density="compact" prefix="R$"
                        @update:model-value="recalcularPrecos" />
                    </v-col>
                    <v-col cols="6" md="3">
                      <v-text-field v-model.number="form.precoFornecedor" label="Preço fornecedor (R$)"
                        type="number" variant="outlined" density="compact" prefix="R$" />
                    </v-col>
                    <v-col cols="6" md="3">
                      <v-text-field v-model.number="form.markupMinimo" label="Markup mínimo"
                        type="number" variant="outlined" density="compact" step="0.01"
                        @update:model-value="recalcularPrecos" />
                    </v-col>
                    <v-col cols="6" md="3">
                      <v-text-field v-model.number="form.precoMinimo" label="Preço mínimo (R$)"
                        type="number" variant="outlined" density="compact" prefix="R$"
                        readonly bg-color="grey-lighten-4" />
                    </v-col>
                    <v-col cols="6" md="4">
                      <v-text-field v-model.number="form.precoVenda" label="Preço de venda *"
                        type="number" variant="outlined" density="compact" prefix="R$"
                        :rules="[r => r > 0 || 'Obrigatório']" />
                    </v-col>
                    <v-col cols="6" md="4">
                      <v-text-field v-model.number="form.precoAtacado" label="Preço atacado (R$)"
                        type="number" variant="outlined" density="compact" prefix="R$" clearable />
                    </v-col>
                    <v-col cols="6" md="2">
                      <v-text-field :model-value="markupExibir" label="Markup"
                        readonly variant="outlined" density="compact" bg-color="grey-lighten-4" />
                    </v-col>
                    <v-col cols="6" md="2">
                      <v-text-field :model-value="margemExibir + '%'" label="Margem"
                        readonly variant="outlined" density="compact" bg-color="grey-lighten-4" />
                    </v-col>
                  </v-row>
                </div>
              </div>

              <!-- ── SEÇÃO: FISCAL ──────────────────────────────── -->
              <div v-if="!ehAtendente" class="prod-secao">
                <div class="prod-secao-header">
                  <v-icon icon="mdi-file-certificate-outline" size="16" />
                  <span>Fiscal</span>
                </div>
                <div class="prod-secao-body">
                  <v-row dense>
                    <v-col cols="6" md="3">
                      <v-text-field v-model="form.ncm" label="NCM" variant="outlined" density="compact" placeholder="0000.00.00" />
                    </v-col>
                    <v-col cols="6" md="3">
                      <v-text-field v-model="form.cest" label="CEST" variant="outlined" density="compact" placeholder="00.000.00" />
                    </v-col>
                    <v-col cols="6" md="2">
                      <v-text-field v-model="form.cfop" label="CFOP" variant="outlined" density="compact" placeholder="5102" />
                    </v-col>
                    <v-col cols="6" md="4">
                      <v-select v-model="form.origem" :items="origensNcm" item-title="label" item-value="value"
                        label="Origem" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="4" md="2">
                      <v-text-field v-model="form.cstIcms" label="CST ICMS" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="4" md="2">
                      <v-text-field v-model="form.csosnIcms" label="CSOSN" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="4" md="2">
                      <v-text-field v-model="form.cstPisCofins" label="CST PIS/COFINS" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="4" md="2">
                      <v-text-field v-model.number="form.aliquotaIcms" label="ICMS %" type="number" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="4" md="2">
                      <v-text-field v-model.number="form.aliquotaPis" label="PIS %" type="number" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="4" md="2">
                      <v-text-field v-model.number="form.aliquotaCofins" label="COFINS %" type="number" variant="outlined" density="compact" />
                    </v-col>
                    <v-col cols="12" md="4">
                      <v-text-field v-model="form.codigoFci" label="Código FCI" variant="outlined" density="compact" />
                    </v-col>
                  </v-row>
                </div>
              </div>

              <!-- ── SEÇÃO: EMBALAGENS ──────────────────────────── -->
              <div v-if="!ehAtendente" class="prod-secao">
                <div class="prod-secao-header">
                  <v-icon icon="mdi-package-variant-closed" size="16" />
                  <span>Embalagens / Múltiplos GTINs</span>
                  <v-spacer />
                  <v-btn v-if="editando" size="x-small" color="primary" variant="tonal"
                    prepend-icon="mdi-plus" @click="abrirNovaEmb">Adicionar</v-btn>
                </div>
                <div class="prod-secao-body">
                  <v-alert v-if="!editando" type="info" variant="tonal" density="compact" rounded="lg">
                    Salve o produto primeiro para adicionar embalagens.
                  </v-alert>
                  <v-data-table v-else :headers="headersEmb" :items="embalagens"
                    density="compact" :loading="carregandoEmb" hide-default-footer>
                    <template #item.precoVenda="{ item }">
                      {{ item.precoVenda ? 'R$ ' + fmtN(item.precoVenda) : '—' }}
                    </template>
                    <template #item.actions="{ item }">
                      <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" color="primary" @click="abrirEdicaoEmb(item)" />
                      <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error" @click="excluirEmb(item.id)" />
                    </template>
                  </v-data-table>
                </div>
              </div>

              <!-- ── SEÇÃO: TABELA NUTRICIONAL ──────────────────── -->
              <div class="prod-secao">
                <div class="prod-secao-header">
                  <v-icon icon="mdi-food-apple-outline" size="16" />
                  <span>Tabela Nutricional</span>
                  <v-spacer />
                  <v-btn size="x-small" color="success" variant="tonal" prepend-icon="mdi-content-save"
                    :loading="salvandoNutri" @click="salvarNutricional">
                    Salvar Nutricional
                  </v-btn>
                </div>
                <div class="prod-secao-body">
                  <v-row dense class="mb-2">
                    <v-col cols="12" md="5">
                      <v-text-field v-model="buscaTaco" label="Buscar na tabela TACO/TBCA"
                        variant="outlined" density="compact" prepend-inner-icon="mdi-magnify"
                        clearable @update:model-value="buscarTaco"
                        placeholder="Ex: arroz, feijão, castanha…" />
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-select v-model="filtroGrupo" :items="gruposTaco" label="Grupo"
                        variant="outlined" density="compact" clearable
                        @update:model-value="buscarTaco" />
                    </v-col>
                  </v-row>
                  <v-list v-if="resultadosTaco.length" density="compact" class="mb-3 rounded-lg border">
                    <v-list-item v-for="a in resultadosTaco" :key="a.id"
                      :title="a.nome" :subtitle="a.grupo + (a.caloriasKcal ? ' · ' + a.caloriasKcal + ' kcal/100g' : '')"
                      @click="preencherNutricional(a)" style="cursor:pointer">
                      <template #append>
                        <v-btn size="x-small" variant="tonal" color="success">Usar</v-btn>
                      </template>
                    </v-list-item>
                  </v-list>

                  <v-row dense>
                    <v-col cols="6" md="3"><v-text-field v-model="nutri.porcao" label="Porção" variant="outlined" density="compact" placeholder="100g" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.caloriasKcal" label="Calorias (kcal)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.carboidratos" label="Carboidratos (g)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.proteinas" label="Proteínas (g)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.lipidiosTotais" label="Gorduras totais (g)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.gordurasSaturadas" label="Gord. saturadas (g)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.gordurasTrans" label="Gord. trans (g)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.fibraAlimentar" label="Fibra alimentar (g)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.sodio" label="Sódio (mg)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.calcio" label="Cálcio (mg)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.ferro" label="Ferro (mg)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.zinco" label="Zinco (mg)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.potassio" label="Potássio (mg)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.magnesio" label="Magnésio (mg)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.vitaminaC" label="Vitamina C (mg)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="6" md="3"><v-text-field v-model.number="nutri.colesterol" label="Colesterol (mg)" type="number" variant="outlined" density="compact" /></v-col>
                    <v-col cols="12"><v-textarea v-model="nutri.ingredientes" label="Ingredientes" variant="outlined" density="compact" rows="2" /></v-col>
                    <v-col cols="12" md="6"><v-textarea v-model="nutri.alergenicos" label="Alérgenos" variant="outlined" density="compact" rows="2" /></v-col>
                    <v-col cols="12" md="6"><v-textarea v-model="nutri.modoConservacao" label="Modo de conservação" variant="outlined" density="compact" rows="2" /></v-col>
                  </v-row>
                </div>
              </div>

              <!-- ── SEÇÃO: EXTRAS ──────────────────────────────── -->
              <div class="prod-secao">
                <div class="prod-secao-header">
                  <v-icon icon="mdi-text-box-outline" size="16" />
                  <span>Informações Adicionais</span>
                </div>
                <div class="prod-secao-body">
                  <v-row dense>
                    <v-col cols="12">
                      <v-textarea v-model="form.informacaoAdicional" label="Informação adicional"
                        variant="outlined" density="compact" rows="3"
                        placeholder="Observações, modo de uso, alertas, instruções…" />
                    </v-col>
                  </v-row>

                  <!-- Ficha técnica PDF -->
                  <div class="prod-secao-header mt-3 mb-2" style="border:none;padding:0;background:none">
                    <v-icon icon="mdi-file-pdf-box" color="error" size="16" />
                    <span>Ficha Técnica (PDF)</span>
                  </div>
                  <div v-if="form.fichaTecnicaUrl" class="mb-2">
                    <v-alert type="success" variant="tonal" density="compact">
                      <div class="d-flex align-center justify-space-between flex-wrap gap-2">
                        <span>Ficha técnica enviada.</span>
                        <div class="d-flex gap-2">
                          <v-btn size="small" color="primary" variant="tonal"
                            prepend-icon="mdi-eye-outline" :href="form.fichaTecnicaUrl" target="_blank">
                            Visualizar
                          </v-btn>
                          <v-btn size="small" color="error" variant="tonal"
                            prepend-icon="mdi-delete-outline"
                            :loading="removendoFicha" @click="removerFicha">
                            Remover
                          </v-btn>
                        </div>
                      </div>
                    </v-alert>
                  </div>
                  <v-alert v-if="!editando && !form.fichaTecnicaUrl" type="info" variant="tonal" density="compact" rounded="lg">
                    Salve o produto primeiro para enviar a ficha técnica.
                  </v-alert>
                  <div v-else-if="editando" class="d-flex align-center gap-3 flex-wrap">
                    <v-file-input v-model="arquivoFicha" accept="application/pdf,.pdf"
                      label="Selecionar PDF" variant="outlined" density="compact"
                      prepend-icon="mdi-paperclip" hide-details style="max-width:340px" />
                    <v-btn color="error" :loading="enviandoFicha" :disabled="!arquivoFicha"
                      prepend-icon="mdi-upload" @click="enviarFicha">
                      Enviar PDF
                    </v-btn>
                  </div>
                </div>
              </div>

              <!-- ── SEÇÃO: VALIDADE E LOTES ────────────────────── -->
              <div class="prod-secao" v-if="editando && (form.controlarValidade || form.controlarLote || lotes.length > 0)">
                <div class="prod-secao-header">
                  <v-icon icon="mdi-calendar-clock" size="16" />
                  <span>Controle de Validade e Lotes</span>
                </div>
                <div class="prod-secao-body">
                  <div class="d-flex align-center mb-3">
                    <div class="text-caption text-medium-emphasis flex-grow-1">
                      <v-icon size="14" class="mr-1">mdi-information-outline</v-icon>
                      <template v-if="form.validadeEmDias">Validade padrão: <b>{{ form.validadeEmDias }} dias</b> após a fabricação.</template>
                      <template v-else>Registre os lotes com sua data de validade para controle e alertas.</template>
                    </div>
                    <v-btn size="small" color="primary" variant="tonal" prepend-icon="mdi-plus"
                      @click="abrirNovoLote">Adicionar lote</v-btn>
                  </div>

                  <div v-if="carregandoLotes" class="d-flex justify-center pa-4">
                    <v-progress-circular indeterminate color="primary" size="28" />
                  </div>
                  <div v-else-if="!lotes.length" class="text-center text-medium-emphasis pa-4">
                    <v-icon size="32" class="mb-1">mdi-calendar-blank-outline</v-icon>
                    <div class="text-body-2">Nenhum lote registrado. Clique em "Adicionar lote".</div>
                  </div>
                  <v-table v-else density="compact">
                    <thead>
                      <tr>
                        <th>Lote</th><th>Fabricação</th><th>Validade</th>
                        <th class="text-right">Qtd</th><th>Local</th><th>Situação</th><th></th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="l in lotes" :key="l.id">
                        <td class="text-body-2 font-weight-medium">{{ l.numeroLote }}</td>
                        <td class="text-body-2">{{ l.dataFabricacao ? new Date(l.dataFabricacao).toLocaleDateString('pt-BR') : '—' }}</td>
                        <td class="text-body-2">{{ l.dataValidade ? new Date(l.dataValidade).toLocaleDateString('pt-BR') : '—' }}</td>
                        <td class="text-right text-body-2">{{ l.quantidade }}</td>
                        <td class="text-body-2">{{ nomeLocal(l.localEstoqueId) }}</td>
                        <td>
                          <v-chip :color="statusLote(l).cor" size="x-small" variant="tonal">{{ statusLote(l).label }}</v-chip>
                        </td>
                        <td class="text-right" style="white-space:nowrap">
                          <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" color="primary" @click="abrirEditarLote(l)" />
                          <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error" @click="excluirLote(l.id)" />
                        </td>
                      </tr>
                    </tbody>
                  </v-table>
                </div>
              </div>

            </div><!-- prod-form-body -->
          </v-form>
        </v-card-text>

        <v-divider />
        <v-card-actions class="pa-4">
          <v-btn variant="text" @click="fecharDialog">Cancelar</v-btn>
          <v-btn v-if="editando && !ehAtendente" color="error" variant="tonal" prepend-icon="mdi-delete-outline"
            :loading="excluindo" @click="excluirProduto">Excluir</v-btn>
          <v-btn v-if="editando && !ehAtendente && form.ativo" color="warning" variant="tonal" prepend-icon="mdi-eye-off-outline"
            :loading="inativando" @click="inativarProduto">Inativar</v-btn>
          <v-btn v-if="editando && !ehAtendente && !form.ativo" color="success" variant="tonal" prepend-icon="mdi-eye-check-outline"
            :loading="inativando" @click="reativarProduto">Reativar</v-btn>
          <v-spacer />
          <v-btn color="primary" :loading="salvando" @click="salvar"
            prepend-icon="mdi-content-save" size="large" rounded="lg">
            {{ editando ? 'Salvar Alterações' : 'Criar Produto' }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Mini-dialog: Lote -->
    <v-dialog v-model="dialogLote" max-width="460" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2">
          <v-icon icon="mdi-calendar-clock" color="primary" />
          {{ loteEditandoId ? 'Editar Lote' : 'Novo Lote' }}
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <v-row dense>
            <v-col cols="12" sm="6">
              <v-text-field v-model="loteForm.numeroLote" label="Número do lote *"
                variant="outlined" density="compact" autofocus />
            </v-col>
            <v-col cols="12" sm="6">
              <v-select v-model="loteForm.localEstoqueId" label="Local de estoque *"
                :items="locaisEstoque" item-title="nome" item-value="id"
                variant="outlined" density="compact" :disabled="!!loteEditandoId" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="loteForm.dataFabricacao" label="Fabricação" type="date"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model="loteForm.dataValidade" label="Validade" type="date"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="loteForm.quantidade" label="Quantidade" type="number"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="loteForm.custoUnitario" label="Custo unit. (R$)" type="number"
                prefix="R$" variant="outlined" density="compact" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogLote = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvandoLote" @click="salvarLote">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Quick-add inline (Categoria / Marca / Unidade) -->
    <v-dialog v-model="quickAdd.open" max-width="380" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2">
          <v-icon :icon="quickAdd.icon" color="primary" />
          Adicionar {{ quickAdd.label }}
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <v-text-field v-if="quickAdd.tipo !== 'unidade'"
            v-model="quickAdd.nome" :label="'Nome *'"
            variant="outlined" density="compact" autofocus
            @keyup.enter="salvarQuickAdd" />
          <template v-else>
            <v-row dense>
              <v-col cols="4">
                <v-text-field v-model="quickAdd.sigla" label="Sigla *" variant="outlined" density="compact"
                  autofocus @keyup.enter="salvarQuickAdd" />
              </v-col>
              <v-col cols="8">
                <v-text-field v-model="quickAdd.nome" label="Descrição" variant="outlined" density="compact"
                  @keyup.enter="salvarQuickAdd" />
              </v-col>
            </v-row>
          </template>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="quickAdd.open = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="quickAdd.salvando" @click="salvarQuickAdd">Adicionar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog embalagem -->
    <v-dialog v-model="dialogEmb" max-width="520" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          {{ embEditandoId ? 'Editar Embalagem' : 'Nova Embalagem' }}
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <v-row dense>
            <v-col cols="12" md="6">
              <v-text-field v-model="formEmb.descricao" label="Descrição *"
                variant="outlined" density="compact" placeholder="Ex: Caixa com 12 unidades" />
            </v-col>
            <v-col cols="12" md="6">
              <v-select v-model="formEmb.unidadeMedidaId" :items="unidades"
                item-title="sigla" item-value="id"
                label="Unidade *" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model.number="formEmb.multiplicador" label="Qtd. de itens *"
                type="number" variant="outlined" density="compact"
                hint="Ex: 12 para caixa com 12 un." persistent-hint />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model="formEmb.codigoBarras" label="GTIN / Código de barras"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" md="4">
              <v-text-field v-model.number="formEmb.precoVenda" label="Preço de venda (R$)"
                type="number" variant="outlined" density="compact" prefix="R$" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogEmb = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvandoEmb" @click="salvarEmb">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useDisplay } from 'vuetify'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import GuiaPassos from '@/components/GuiaPassos.vue'

const auth = useAuthStore()
const notif = useNotifStore()
const { mobile } = useDisplay()

// Atendente edita o cadastro, mas não vê preços nem dados fiscais, e não cria/exclui.
const ehAtendente = computed(() => auth.usuario?.role === 'Atendente')

const carregando = ref(false)
const salvando = ref(false)
const sincronizandoSite = ref(false)

// Sincroniza os produtos por kg (balança) com o site público ecogranel.com.br.
async function sincronizarSite() {
  sincronizandoSite.value = true
  try {
    const { data } = await api.post('/produtos/sincronizar-site', null,
      { params: { empresaId: auth.empresaId } })
    if (data?.ok) notif.ok(`${data.qtd ?? 0} produto(s) sincronizado(s) com o site.`)
    else notif.aviso(data?.mensagem || 'Não foi possível sincronizar com o site.')
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem || 'Falha ao sincronizar com o site.')
  } finally {
    sincronizandoSite.value = false
  }
}
const sugerindoDesc = ref(false)
const excluindo = ref(false)
const inativando = ref(false)
const dialog = ref(false)
const editando = ref(false)

const produtos = ref<any[]>([])
const categorias = ref<any[]>([])
const marcas = ref<any[]>([])
const unidades = ref<any[]>([])
const fornecedores = ref<any[]>([])
const locaisEstoque = ref<any[]>([])

// ─── Lotes / Validade ────────────────────────────────────────────
const lotes = ref<any[]>([])
const carregandoLotes = ref(false)
const dialogLote = ref(false)
const loteEditandoId = ref<string | null>(null)
const salvandoLote = ref(false)
const loteForm = ref<any>({})

const busca = ref('')
const filtroCategoria = ref<string | null>(null)
const filtroFornecedor = ref<string[]>([])
const filtroAtivo = ref<boolean | null>(true)
const formulario = ref<any>(null)
const produtoEditandoId = ref<string | null>(null)

// Só fornecedores que realmente têm produtos aparecem no filtro — evita poluir com
// colaboradores, prestadores de serviço, órgãos e pessoas físicas sem produtos.
const fornecedoresComProduto = computed(() => {
  const ids = new Set(produtos.value.map((p: any) => p.fornecedorPrincipalId).filter(Boolean))
  return fornecedores.value
    .filter((f: any) => ids.has(f.id))
    .sort((a: any, b: any) => a.razaoSocial.localeCompare(b.razaoSocial))
})

// Filtro por fornecedor (múltipla seleção) aplicado no cliente (a lista já traz todos os produtos)
const produtosFiltrados = computed(() =>
  filtroFornecedor.value?.length
    ? produtos.value.filter((p: any) => filtroFornecedor.value.includes(p.fornecedorPrincipalId))
    : produtos.value
)

// ─── formulário produto ──────────────────────────────────────────
const formPadrao = () => ({
  codigo: '', referencia: '', descricao: '', descricaoComplementar: '',
  tipoVariacao: 'Simples',
  categoriaId: null as string | null, marcaId: null as string | null,
  fornecedorPrincipalId: null as string | null,
  unidadeMedidaId: null as string | null,
  codigoBarras: '',
  ativo: true, produtoBalanca: false, ocultarNasVendas: false,
  requisitarVendedor: false, vendidoFracionado: false,
  // Novos produtos já nascem com controle de validade ligado e prazo padrão —
  // o usuário altera o prazo ou desliga o controle se quiser.
  controlarLote: false, controlarValidade: true,
  validadeEmDias: 60 as number | null, codigoPlu: null as number | null,
  precoFornecedor: 0, custoUnitario: 0,
  markupMinimo: 0, precoMinimo: 0,
  precoVenda: 0, precoAtacado: null as number | null, markupAtacado: null as number | null,
  ncm: '', cest: '', cfop: '', origem: '0',
  cstIcms: '', csosnIcms: '', cstPisCofins: '',
  aliquotaIcms: 0, aliquotaPis: 0.65, aliquotaCofins: 3,
  codigoFci: '',
  imagemUrl: '' as string | null, fichaTecnicaUrl: null as string | null,
  tags: '', marcador: null as string | null, informacaoAdicional: '',
})
const form = ref(formPadrao())

// Opções booleanas do produto (chips na seção Identificação)
const opcoesProduto = [
  { campo: 'ativo',              rotulo: 'Ativo',             cor: 'success',   icone: 'mdi-check-circle-outline' },
  { campo: 'produtoBalanca',     rotulo: 'Balança',           cor: 'primary',   icone: 'mdi-scale-balance' },
  { campo: 'vendidoFracionado',  rotulo: 'Fracionado',        cor: 'secondary', icone: 'mdi-scissors-cutting' },
  { campo: 'ocultarNasVendas',   rotulo: 'Ocultar no PDV',    cor: 'warning',   icone: 'mdi-eye-off-outline' },
  { campo: 'requisitarVendedor', rotulo: 'Req. vendedor',     cor: 'info',      icone: 'mdi-account-tie-outline' },
  { campo: 'controlarLote',      rotulo: 'Controlar lote',    cor: 'teal',      icone: 'mdi-barcode-scan' },
  { campo: 'controlarValidade',  rotulo: 'Controlar validade',cor: 'orange',    icone: 'mdi-calendar-clock' },
]

// ─── markup calculado ────────────────────────────────────────────
const markupExibir = computed(() => {
  const f = form.value
  return f.custoUnitario > 0 ? (f.precoVenda / f.custoUnitario).toFixed(2) : '—'
})
const margemExibir = computed(() => {
  const f = form.value
  return f.precoVenda > 0
    ? (((f.precoVenda - f.custoUnitario) / f.precoVenda) * 100).toFixed(1)
    : '—'
})
const markupAtacadoExibir = computed(() => {
  const f = form.value
  if (!f.precoAtacado || !f.custoUnitario) return '—'
  return (f.precoAtacado / f.custoUnitario).toFixed(2)
})

function recalcularPrecos() {
  const f = form.value
  if (f.markupMinimo > 0 && f.custoUnitario > 0)
    f.precoMinimo = parseFloat((f.custoUnitario * f.markupMinimo).toFixed(2))
}

// ─── upload imagem ───────────────────────────────────────────────
const arquivoImagem = ref<File | null>(null)
const enviandoImagem = ref(false)
const removendoImagem = ref(false)
const buscandoImagem = ref(false)
const previewImagem = ref<string | null>(null)

// Busca a foto do produto pelo código de barras (Open Food Facts) e associa.
async function buscarImagemPorCodigoBarras() {
  const ean = (form.value.codigoBarras || '').trim()
  if (!ean) { notif.aviso('Preencha o código de barras (EAN) primeiro.'); return }
  if (!produtoEditandoId.value) { notif.aviso('Salve o produto primeiro para buscar a foto.'); return }
  buscandoImagem.value = true
  try {
    const { data } = await api.post(`/produtos/${produtoEditandoId.value}/imagem-codigo-barras`, { codigoBarras: ean })
    if (data?.url) {
      form.value.imagemUrl = data.url + '?t=' + Date.now()  // quebra cache pra mostrar a nova
      notif.ok('Foto encontrada e associada ao produto!')
    } else {
      notif.aviso('Nenhuma foto encontrada para este código de barras.')
    }
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem || 'Nenhuma foto encontrada para este código de barras.')
  } finally {
    buscandoImagem.value = false
  }
}

function previewImagemLocal() {
  if (!arquivoImagem.value) { previewImagem.value = null; return }
  previewImagem.value = URL.createObjectURL(arquivoImagem.value)
}

function onFileImagem(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0] ?? null
  arquivoImagem.value = file
  previewImagemLocal()
}

// Sugere a descrição complementar (benefícios) do produto via IA (Gemini).
async function sugerirDescricao() {
  const nome = (form.value.descricao || '').trim()
  if (!nome) { notif.aviso('Preencha a descrição do produto primeiro.'); return }
  sugerindoDesc.value = true
  try {
    const { data } = await api.post('/produtos/sugerir-descricao', { nome })
    if (data?.texto) {
      form.value.descricaoComplementar = data.texto
      notif.ok('Sugestão gerada! Revise antes de salvar.')
    } else {
      notif.aviso('A IA não retornou texto. Tente novamente.')
    }
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem || 'Não foi possível gerar a sugestão.')
  } finally {
    sugerindoDesc.value = false
  }
}

async function enviarImagem() {
  if (!arquivoImagem.value) return
  if (!produtoEditandoId.value) { notif.ok('Salve o produto primeiro para enviar a imagem.'); return }
  if (arquivoImagem.value.size > 5_000_000) { notif.erro('Imagem muito grande. Máximo 5 MB.'); return }

  enviandoImagem.value = true
  try {
    const fd = new FormData()
    fd.append('arquivo', arquivoImagem.value)
    const r = await api.post(`/produtos/${produtoEditandoId.value}/imagem`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    form.value.imagemUrl = r.data.url + '?t=' + Date.now()  // quebra cache pra mostrar a nova
    arquivoImagem.value = null
    previewImagem.value = null
    notif.ok('Imagem enviada com sucesso!')
  } catch { notif.erro('Erro ao enviar imagem.') }
  finally { enviandoImagem.value = false }
}

async function removerImagem() {
  if (!produtoEditandoId.value) return
  removendoImagem.value = true
  try {
    await api.delete(`/produtos/${produtoEditandoId.value}/imagem`)
    form.value.imagemUrl = null
    notif.ok('Imagem removida.')
  } catch { notif.erro('Erro ao remover imagem.') }
  finally { removendoImagem.value = false }
}

// ─── ficha técnica PDF ───────────────────────────────────────────
const arquivoFicha = ref<File | null>(null)
const enviandoFicha = ref(false)
const removendoFicha = ref(false)

async function enviarFicha() {
  if (!arquivoFicha.value || !produtoEditandoId.value) return
  if (arquivoFicha.value.size > 20_000_000) { notif.erro('Arquivo muito grande. Máximo 20 MB.'); return }
  enviandoFicha.value = true
  try {
    const fd = new FormData()
    fd.append('arquivo', arquivoFicha.value)
    const r = await api.post(`/produtos/${produtoEditandoId.value}/ficha-tecnica`, fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    form.value.fichaTecnicaUrl = r.data.url
    arquivoFicha.value = null
    notif.ok('Ficha técnica enviada!')
  } catch { notif.erro('Erro ao enviar ficha técnica.') }
  finally { enviandoFicha.value = false }
}

async function removerFicha() {
  if (!produtoEditandoId.value) return
  removendoFicha.value = true
  try {
    await api.delete(`/produtos/${produtoEditandoId.value}/ficha-tecnica`)
    form.value.fichaTecnicaUrl = null
    notif.ok('Ficha técnica removida.')
  } catch { notif.erro('Erro ao remover ficha técnica.') }
  finally { removendoFicha.value = false }
}

// ─── embalagens ──────────────────────────────────────────────────
const embalagens = ref<any[]>([])
const carregandoEmb = ref(false)
const dialogEmb = ref(false)
const salvandoEmb = ref(false)
const embEditandoId = ref<string | null>(null)
const formEmbPadrao = () => ({
  descricao: '', unidadeMedidaId: null as string | null,
  multiplicador: 1, codigoBarras: '', precoVenda: null as number | null,
})
const formEmb = ref(formEmbPadrao())
const headersEmb = [
  { title: 'Descrição', key: 'descricao' },
  { title: 'Qtd.', key: 'multiplicador', width: 80 },
  { title: 'GTIN', key: 'codigoBarras', width: 160 },
  { title: 'Preço', key: 'precoVenda', width: 110 },
  { title: '', key: 'actions', sortable: false, width: 80 },
]

async function carregarEmbalagens() {
  if (!produtoEditandoId.value) return
  carregandoEmb.value = true
  try {
    const r = await api.get(`/produtos/${produtoEditandoId.value}/embalagens`)
    embalagens.value = r.data
  } finally { carregandoEmb.value = false }
}
function abrirNovaEmb() { embEditandoId.value = null; formEmb.value = formEmbPadrao(); dialogEmb.value = true }
function abrirEdicaoEmb(item: any) { embEditandoId.value = item.id; formEmb.value = { ...item }; dialogEmb.value = true }
async function salvarEmb() {
  salvandoEmb.value = true
  try {
    if (embEditandoId.value)
      await api.put(`/produtos/${produtoEditandoId.value}/embalagens/${embEditandoId.value}`, formEmb.value)
    else
      await api.post(`/produtos/${produtoEditandoId.value}/embalagens`, formEmb.value)
    dialogEmb.value = false
    await carregarEmbalagens()
    notif.ok('Embalagem salva!')
  } catch { notif.erro('Erro ao salvar embalagem.') }
  finally { salvandoEmb.value = false }
}
async function excluirEmb(id: string) {
  await api.delete(`/produtos/${produtoEditandoId.value}/embalagens/${id}`)
  await carregarEmbalagens()
  notif.ok('Embalagem removida.')
}

// ─── tabela nutricional ──────────────────────────────────────────
const nutriPadrao = () => ({
  porcao: '', caloriasKcal: null as number | null, carboidratos: null as number | null,
  proteinas: null as number | null, lipidiosTotais: null as number | null,
  gordurasSaturadas: null as number | null, gordurasTrans: null as number | null,
  fibraAlimentar: null as number | null, sodio: null as number | null,
  calcio: null as number | null, ferro: null as number | null,
  zinco: null as number | null, potassio: null as number | null,
  magnesio: null as number | null, selenio: null as number | null,
  vitaminaC: null as number | null, vitaminaA: null as number | null,
  vitaminaB6: null as number | null, vitaminaB12: null as number | null,
  acidoFolico: null as number | null, colesterol: null as number | null,
  ingredientes: '', alergenicos: '', modoConservacao: '',
})
const nutri = ref(nutriPadrao())
const salvandoNutri = ref(false)

const buscaTaco = ref('')
const filtroGrupo = ref<string | null>(null)
const resultadosTaco = ref<any[]>([])
const gruposTaco = ref<string[]>([])
let tacoTimer: ReturnType<typeof setTimeout>

async function buscarTaco() {
  clearTimeout(tacoTimer)
  tacoTimer = setTimeout(async () => {
    if (!buscaTaco.value && !filtroGrupo.value) { resultadosTaco.value = []; return }
    const r = await api.get('/alimentos-taco', {
      params: { q: buscaTaco.value || undefined, grupo: filtroGrupo.value || undefined }
    })
    resultadosTaco.value = r.data
  }, 300)
}

function preencherNutricional(a: any) {
  nutri.value = {
    porcao: nutri.value.porcao || '100g',
    caloriasKcal: a.caloriasKcal, carboidratos: a.carboidratos,
    proteinas: a.proteinas, lipidiosTotais: a.lipidiosTotais,
    gordurasSaturadas: a.gordurasSaturadas, gordurasTrans: a.gordurasTrans,
    fibraAlimentar: a.fibraAlimentar, sodio: a.sodio,
    calcio: a.calcio, ferro: a.ferro, zinco: a.zinco,
    potassio: a.potassio, magnesio: a.magnesio, selenio: a.selenio,
    vitaminaC: a.vitaminaC, vitaminaA: a.vitaminaA,
    vitaminaB6: a.vitaminaB6, vitaminaB12: a.vitaminaB12,
    acidoFolico: a.acidoFolico, colesterol: a.colesterol,
    ingredientes: nutri.value.ingredientes,
    alergenicos: nutri.value.alergenicos,
    modoConservacao: nutri.value.modoConservacao,
  }
  notif.ok(`Valores de "${a.nome}" preenchidos — confira e ajuste se necessário.`)
}

async function salvarNutricional() {
  if (!produtoEditandoId.value) { notif.ok('Salve o produto primeiro.'); return }
  salvandoNutri.value = true
  try {
    await api.post(`/tabela-nutricional/${produtoEditandoId.value}`, {
      ...nutri.value, produtoId: produtoEditandoId.value,
    })
    notif.ok('Informações nutricionais salvas!')
  } catch { notif.erro('Erro ao salvar nutricional.') }
  finally { salvandoNutri.value = false }
}

// ─── quick-add (categoria / marca / unidade) ─────────────────────
const quickAdd = ref({
  open: false, salvando: false,
  tipo: '' as 'categoria' | 'marca' | 'unidade',
  label: '', icon: '', nome: '', sigla: '',
})

function abrirQuickAdd(tipo: 'categoria' | 'marca' | 'unidade') {
  const mapa = {
    categoria: { label: 'Categoria',        icon: 'mdi-tag-multiple-outline' },
    marca:     { label: 'Marca',            icon: 'mdi-watermark' },
    unidade:   { label: 'Unidade de Medida',icon: 'mdi-ruler-square' },
  }
  quickAdd.value = { open: true, salvando: false, tipo, nome: '', sigla: '', ...mapa[tipo] }
}

async function salvarQuickAdd() {
  const qa = quickAdd.value
  if (qa.tipo === 'unidade' && !qa.sigla) { notif.erro('Sigla é obrigatória.'); return }
  if (qa.tipo !== 'unidade' && !qa.nome) { notif.erro('Nome é obrigatório.'); return }
  qa.salvando = true
  try {
    let novoId: string
    if (qa.tipo === 'categoria') {
      const r = await api.post('/categorias', { nome: qa.nome, empresaId: auth.empresaId })
      novoId = r.data.id
      await api.get('/categorias', { params: { empresaId: auth.empresaId } }).then(res => { categorias.value = res.data })
      form.value.categoriaId = novoId
    } else if (qa.tipo === 'marca') {
      const r = await api.post('/marcas', { nome: qa.nome, empresaId: auth.empresaId })
      novoId = r.data.id
      await api.get('/marcas', { params: { empresaId: auth.empresaId } }).then(res => { marcas.value = res.data })
      form.value.marcaId = novoId
    } else {
      const r = await api.post('/unidades-medida', { sigla: qa.sigla, descricao: qa.nome, empresaId: auth.empresaId })
      novoId = r.data.id
      await api.get('/unidades-medida', { params: { empresaId: auth.empresaId } }).then(res => { unidades.value = res.data })
      form.value.unidadeMedidaId = novoId
    }
    notif.ok(`${qa.label} adicionada com sucesso!`)
    qa.open = false
  } catch { notif.erro(`Erro ao adicionar ${qa.label}.`) }
  finally { qa.salvando = false }
}

// ─── lookups ─────────────────────────────────────────────────────
const origensNcm = [
  { value: '0', label: '0 - Nacional' },
  { value: '1', label: '1 - Estrangeira (importação direta)' },
  { value: '2', label: '2 - Estrangeira (adquirida no mercado interno)' },
  { value: '3', label: '3 - Nacional com mais de 40% de conteúdo estrangeiro' },
  { value: '4', label: '4 - Nacional (processos produtivos básicos)' },
  { value: '5', label: '5 - Nacional com até 40% de conteúdo estrangeiro' },
  { value: '6', label: '6 - Estrangeira (importação direta, sem similar)' },
  { value: '7', label: '7 - Estrangeira (adquirida no mercado interno, sem similar)' },
  { value: '8', label: '8 - Nacional com mais de 70% de conteúdo estrangeiro' },
]
const marcadores = [
  { value: 'vermelho',   label: 'Vermelho',   cor: 'red',    icone: 'mdi-circle' },
  { value: 'laranja',    label: 'Laranja',    cor: 'orange', icone: 'mdi-circle' },
  { value: 'amarelo',    label: 'Amarelo',    cor: 'amber',  icone: 'mdi-circle' },
  { value: 'verde',      label: 'Verde',      cor: 'green',  icone: 'mdi-circle' },
  { value: 'azul',       label: 'Azul',       cor: 'blue',   icone: 'mdi-circle' },
  { value: 'roxo',       label: 'Roxo',       cor: 'purple', icone: 'mdi-circle' },
  { value: 'destaque',   label: 'Destaque',   cor: 'pink',   icone: 'mdi-star' },
  { value: 'promocao',   label: 'Promoção',   cor: 'error',  icone: 'mdi-tag' },
  { value: 'lancamento', label: 'Lançamento', cor: 'success',icone: 'mdi-new-box' },
]

const headersCompletos = [
  { title: 'Código', key: 'codigo', width: 90 },
  { title: 'Descrição', key: 'descricao', sortable: true },
  { title: 'Un.', key: 'unidadeSigla', width: 70 },
  { title: 'Estoque', key: 'estoqueAtual', width: 100 },
  { title: 'Custo', key: 'custoUnitario', width: 100 },
  { title: 'Markup', key: 'markup', width: 90 },
  { title: 'Preço', key: 'precoVenda', width: 100 },
  { title: 'Preço/100g', key: 'preco100g', width: 110, sortable: false },
  { title: 'Status', key: 'ativo', width: 90 },
  { title: '', key: 'actions', sortable: false, width: 110 },
]
// No celular mostramos só o essencial (o resto abre no detalhe do produto).
const headersMobile = [
  { title: 'Produto', key: 'descricao', sortable: true },
  { title: 'Estoque', key: 'estoqueAtual', width: 76 },
  { title: 'Preço', key: 'precoVenda', width: 84 },
  { title: '', key: 'actions', sortable: false, width: 52 },
]
// Colunas de valores (custo/markup/preço) ficam ocultas para o Atendente.
const colunasPreco = ['custoUnitario', 'markup', 'precoVenda', 'preco100g']
const headers = computed(() => {
  const base = mobile.value ? headersMobile : headersCompletos
  return ehAtendente.value ? base.filter(h => !colunasPreco.includes(h.key)) : base
})

// Produto vendido por peso (KG) → mostra preço por 100g (= preço/kg ÷ 10)
function ehPorPeso(p: any) {
  return p.vendidoFracionado === true || (p.unidadeSigla || '').toUpperCase() === 'KG'
}
function fmtN(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }
function fmtData(d?: string) { return d ? new Date(d).toLocaleDateString('pt-BR') : '—' }

// ─── listar / carregar ───────────────────────────────────────────
async function listar() {
  carregando.value = true
  try {
    const r = await api.get('/produtos', {
      params: { empresaId: auth.empresaId, termo: busca.value || undefined,
        categoriaId: filtroCategoria.value || undefined, ativo: filtroAtivo.value,
        pagina: 1, tamanhoPagina: 5000 }   // carrega todos (paginação é feita na tabela)
    })
    produtos.value = r.data.itens ?? r.data
  } finally { carregando.value = false }
}

async function carregarCatalogo() {
  try {
    const [c, m, u, f, g, l] = await Promise.all([
      api.get('/categorias',     { params: { empresaId: auth.empresaId } }),
      api.get('/marcas',         { params: { empresaId: auth.empresaId } }),
      api.get('/unidades-medida',{ params: { empresaId: auth.empresaId } }),
      api.get('/fornecedores',   { params: { empresaId: auth.empresaId } }),
      api.get('/alimentos-taco/grupos'),
      api.get('/locais-estoque', { params: { empresaId: auth.empresaId } }),
    ])
    categorias.value = c.data
    marcas.value = m.data
    unidades.value = u.data
    fornecedores.value = f.data
    gruposTaco.value = g.data
    locaisEstoque.value = l.data
  } catch { /* silencioso */ }
}

// ─── abrir / fechar dialog ───────────────────────────────────────
// ─── Unificar duplicados ─────────────────────────────────────────────
const dlgUnificar = ref(false)
const carregandoDup = ref(false)
const unificando = ref(false)
const gruposDup = ref<any[]>([])

// ── Seleção manual de duplicados ──
const selManual = ref<string[]>([])
const manterManual = ref<string | null>(null)

const produtosOrdenados = computed(() =>
  [...produtos.value].sort((a: any, b: any) => a.descricao.localeCompare(b.descricao)))

const produtosSelManual = computed(() =>
  produtos.value.filter((p: any) => selManual.value.includes(p.id)))

const itemTituloProduto = (p: any) => `${p.descricao} — cód. ${p.codigo}`

// Mantém a seleção do "manter" válida conforme a lista muda.
watch(selManual, (ids) => {
  if (!ids.includes(manterManual.value as string)) manterManual.value = ids[0] ?? null
})

async function unificarManual() {
  const ids = selManual.value
  if (ids.length < 2 || !manterManual.value) return
  const destino = produtos.value.find((p: any) => p.id === manterManual.value)
  const origemIds = ids.filter(id => id !== manterManual.value)
  if (!confirm(`Unificar ${origemIds.length} produto(s) em "${destino?.descricao}"? Ação irreversível.`)) return
  unificando.value = true
  try {
    await api.post('/produtos/unificar', { destinoId: manterManual.value, origemIds })
    notif.ok(`${origemIds.length} produto(s) unificado(s) em "${destino?.descricao}".`)
    selManual.value = []
    manterManual.value = null
    await listar()
    // Recarrega a auto-detecção (os fundidos somem)
    await abrirUnificar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao unificar produtos.')
  } finally { unificando.value = false }
}

async function abrirUnificar() {
  dlgUnificar.value = true
  carregandoDup.value = true
  gruposDup.value = []
  try {
    const r = await api.get('/produtos/duplicados', { params: { empresaId: auth.empresaId } })
    // pré-seleciona manter o mais antigo (primeiro da lista) de cada grupo
    gruposDup.value = (r.data ?? []).map((g: any) => ({ ...g, _manter: g.produtos[0]?.id }))
  } catch { gruposDup.value = [] }
  finally { carregandoDup.value = false }
}

async function confirmarUnificar() {
  const grupos = gruposDup.value.filter(g => g._manter && g.produtos.length > 1)
  if (!grupos.length) { notif.aviso('Nada a unificar.'); return }
  if (!confirm(`Unificar ${grupos.length} grupo(s) de duplicados? Ação irreversível.`)) return
  unificando.value = true
  try {
    let total = 0
    for (const g of grupos) {
      const origemIds = g.produtos.filter((p: any) => p.id !== g._manter).map((p: any) => p.id)
      if (!origemIds.length) continue
      await api.post('/produtos/unificar', { destinoId: g._manter, origemIds })
      total += origemIds.length
    }
    notif.ok(`${total} produto(s) duplicado(s) unificado(s).`)
    dlgUnificar.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao unificar produtos.')
  } finally { unificando.value = false }
}

function abrirNovo() {
  editando.value = false
  produtoEditandoId.value = null
  form.value = formPadrao()
  nutri.value = nutriPadrao()
  embalagens.value = []
  arquivoImagem.value = null
  previewImagem.value = null
  dialog.value = true
}

// ─── Ajuste de estoque rápido (inline) ───────────────────────────────
const dlgAjuste = ref(false)
const ajustando = ref(false)
const ajuste = ref({
  produtoId: null as string | null, produtoNome: '', estoqueAtual: 0,
  localEstoqueId: null as string | null, quantidadeContada: 0, observacao: '',
})
const ajusteDiferenca = computed(() =>
  Number(ajuste.value.quantidadeContada || 0) - Number(ajuste.value.estoqueAtual || 0))

function abrirAjuste(p: any) {
  ajuste.value = {
    produtoId: p.id, produtoNome: p.descricao, estoqueAtual: p.estoqueAtual ?? 0,
    localEstoqueId: locaisEstoque.value[0]?.id ?? null,
    quantidadeContada: p.estoqueAtual ?? 0, observacao: '',
  }
  dlgAjuste.value = true
}

async function confirmarAjuste() {
  if (!ajuste.value.produtoId || !ajuste.value.localEstoqueId || ajusteDiferenca.value === 0) return
  ajustando.value = true
  try {
    await api.post('/ajuste-estoque/unitario', {
      empresaId: auth.empresaId,
      produtoId: ajuste.value.produtoId,
      localEstoqueId: ajuste.value.localEstoqueId,
      quantidadeContada: Number(ajuste.value.quantidadeContada),
      usuarioId: auth.usuario?.id ?? null,
      observacao: ajuste.value.observacao || null,
    })
    notif.ok(`Estoque ajustado (${ajusteDiferenca.value > 0 ? '+' : ''}${ajusteDiferenca.value}).`)
    dlgAjuste.value = false
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? e?.response?.data?.detalhe ?? e?.response?.data?.title ?? 'Erro ao ajustar estoque.')
  } finally { ajustando.value = false }
}

// ─── Duplicar produto (cria novo a partir de um existente) ───────────
async function duplicarProduto(item: any) {
  try {
    const r = await api.get(`/produtos/${item.id}`)
    const p = r.data
    editando.value = false
    produtoEditandoId.value = null
    form.value = {
      ...formPadrao(),
      codigo: '', codigoBarras: '',            // novos: código gerado, EAN em branco
      referencia: p.referencia ?? '',
      descricao: (p.descricao ?? '') + ' (cópia)',
      descricaoComplementar: p.descricaoComplementar ?? '',
      tipoVariacao: p.tipoVariacao ?? 'Simples',
      categoriaId: p.categoriaId, marcaId: p.marcaId,
      fornecedorPrincipalId: p.fornecedorPrincipalId ?? null,
      unidadeMedidaId: p.unidadeMedidaId,
      ativo: true, produtoBalanca: p.produtoBalanca,
      ocultarNasVendas: p.ocultarNasVendas, requisitarVendedor: p.requisitarVendedor,
      vendidoFracionado: p.vendidoFracionado,
      controlarLote: p.controlarLote, controlarValidade: p.controlarValidade,
      validadeEmDias: p.validadeEmDias, codigoPlu: null,
      precoFornecedor: p.precoFornecedor ?? 0, custoUnitario: p.custoUnitario,
      markupMinimo: p.markupMinimo ?? 0, precoMinimo: p.precoMinimo ?? 0,
      precoVenda: p.precoVenda, precoAtacado: p.precoAtacado ?? null,
      markupAtacado: p.markupAtacado ?? null,
      ncm: p.ncm ?? '', cest: p.cest ?? '', cfop: p.cfop ?? '',
      origem: p.origem ?? '0', cstIcms: p.cstIcms ?? '',
      csosnIcms: p.csosnIcms ?? '', cstPisCofins: p.cstPisCofins ?? '',
      aliquotaIcms: p.aliquotaIcms, aliquotaPis: p.aliquotaPis,
      aliquotaCofins: p.aliquotaCofins, codigoFci: p.codigoFci ?? '',
      imagemUrl: null, fichaTecnicaUrl: null,
      tags: p.tags ?? '', marcador: p.marcador ?? null,
      informacaoAdicional: p.informacaoAdicional ?? '',
    }
    nutri.value = nutriPadrao()
    embalagens.value = []
    arquivoImagem.value = null
    previewImagem.value = null
    dialog.value = true
    notif.aviso('Cópia carregada. Confira os dados e salve para criar o novo produto.')
  } catch {
    notif.erro('Não foi possível carregar o produto para duplicar.')
  }
}

async function abrirEdicao(item: any) {
  editando.value = true
  produtoEditandoId.value = item.id
  dialog.value = true
  try {
    const r = await api.get(`/produtos/${item.id}`)
    const p = r.data
    form.value = {
      codigo: p.codigo, referencia: p.referencia ?? '',
      descricao: p.descricao, descricaoComplementar: p.descricaoComplementar ?? '',
      tipoVariacao: p.tipoVariacao ?? 'Simples',
      categoriaId: p.categoriaId, marcaId: p.marcaId,
      fornecedorPrincipalId: p.fornecedorPrincipalId ?? null,
      unidadeMedidaId: p.unidadeMedidaId, codigoBarras: p.codigoBarras ?? '',
      ativo: p.ativo, produtoBalanca: p.produtoBalanca,
      ocultarNasVendas: p.ocultarNasVendas, requisitarVendedor: p.requisitarVendedor,
      vendidoFracionado: p.vendidoFracionado,
      controlarLote: p.controlarLote, controlarValidade: p.controlarValidade,
      validadeEmDias: p.validadeEmDias, codigoPlu: p.codigoPlu,
      precoFornecedor: p.precoFornecedor ?? 0, custoUnitario: p.custoUnitario,
      markupMinimo: p.markupMinimo ?? 0, precoMinimo: p.precoMinimo ?? 0,
      precoVenda: p.precoVenda, precoAtacado: p.precoAtacado ?? null,
      markupAtacado: p.markupAtacado ?? null,
      ncm: p.ncm ?? '', cest: p.cest ?? '', cfop: p.cfop ?? '',
      origem: p.origem ?? '0', cstIcms: p.cstIcms ?? '',
      csosnIcms: p.csosnIcms ?? '', cstPisCofins: p.cstPisCofins ?? '',
      aliquotaIcms: p.aliquotaIcms, aliquotaPis: p.aliquotaPis,
      aliquotaCofins: p.aliquotaCofins, codigoFci: p.codigoFci ?? '',
      imagemUrl: p.imagemUrl ?? null, fichaTecnicaUrl: p.fichaTecnicaUrl ?? null,
      tags: p.tags ?? '', marcador: p.marcador ?? null,
      informacaoAdicional: p.informacaoAdicional ?? '',
    }
    embalagens.value = p.embalagens ?? []
    nutri.value = p.nutricional ? { ...nutriPadrao(), ...p.nutricional } : nutriPadrao()
    await carregarLotes()
  } catch { /* silencioso */ }
}

function fecharDialog() {
  dialog.value = false
  arquivoImagem.value = null
  previewImagem.value = null
}

// ─── salvar produto ──────────────────────────────────────────────
async function excluirProduto() {
  if (!produtoEditandoId.value) return
  if (!confirm('Excluir este produto definitivamente? Esta ação não pode ser desfeita.')) return
  excluindo.value = true
  try {
    await api.delete(`/produtos/${produtoEditandoId.value}`)
    notif.ok('Produto excluído.')
    fecharDialog()
    await listar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao excluir produto.')
  } finally { excluindo.value = false }
}

async function inativarProduto() {
  if (!produtoEditandoId.value) return
  if (!confirm('Inativar este produto? Ele não aparecerá nas vendas mas o histórico será mantido.')) return
  inativando.value = true
  try {
    await api.patch(`/produtos/${produtoEditandoId.value}/inativar`)
    notif.ok('Produto inativado.')
    fecharDialog()
    await listar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao inativar produto.')
  } finally { inativando.value = false }
}

async function reativarProduto() {
  if (!produtoEditandoId.value) return
  inativando.value = true
  try {
    await api.patch(`/produtos/${produtoEditandoId.value}/reativar`)
    notif.ok('Produto reativado.')
    fecharDialog()
    await listar()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao reativar produto.')
  } finally { inativando.value = false }
}

// ─── Detecção automática de produto de Balança (venda por peso) ──
// Quando a categoria é por peso (Kilo/Granel) ou a unidade é KG, o produto
// é preparado para a balança: marca "Balança", ativa validade e sugere 60 dias.
function aplicarBalancaAuto() {
  const cat = categorias.value.find((c: any) => c.id === form.value.categoriaId)
  const uni = unidades.value.find((u: any) => u.id === form.value.unidadeMedidaId)
  const nomeCat = (cat?.nome ?? '').toLowerCase()
  const sigla = (uni?.sigla ?? '').toUpperCase()
  const ehPeso = /kilo|quilo|granel|peso|fracion|a\s*granel/.test(nomeCat) || sigla === 'KG'
  if (!ehPeso || form.value.produtoBalanca) return

  form.value.produtoBalanca = true
  form.value.vendidoFracionado = true
  if (!form.value.controlarValidade) form.value.controlarValidade = true
  if (!form.value.validadeEmDias) form.value.validadeEmDias = 60
  if (!form.value.codigoPlu) {
    // PLU sugerido a partir do código numérico do produto (usado pela balança)
    const n = parseInt(form.value.codigo ?? '', 10)
    if (!isNaN(n)) form.value.codigoPlu = n
  }
  notif.aviso('Produto por peso: marcado para Balança, validade 60 dias e PLU sugerido.')
}

// ─── Lotes / Validade ────────────────────────────────────────────
async function carregarLotes() {
  if (!produtoEditandoId.value) { lotes.value = []; return }
  carregandoLotes.value = true
  try {
    const r = await api.get(`/lotes/produto/${produtoEditandoId.value}`, { params: { empresaId: auth.empresaId } })
    lotes.value = r.data ?? []
  } catch { lotes.value = [] }
  finally { carregandoLotes.value = false }
}

function statusLote(lote: any) {
  if (!lote.dataValidade) return { label: 'Sem validade', cor: 'grey' }
  const hoje = new Date(new Date().toISOString().slice(0, 10) + 'T12:00:00')
  const val = new Date(lote.dataValidade.slice(0, 10) + 'T12:00:00')
  const dias = Math.round((val.getTime() - hoje.getTime()) / 86400000)
  if (dias < 0) return { label: `Vencido há ${Math.abs(dias)}d`, cor: 'error' }
  if (dias === 0) return { label: 'Vence hoje', cor: 'error' }
  if (dias <= (form.value.validadeEmDias && form.value.validadeEmDias < 30 ? form.value.validadeEmDias : 30))
    return { label: `Vence em ${dias}d`, cor: 'warning' }
  return { label: `Vence em ${dias}d`, cor: 'success' }
}

function abrirNovoLote() {
  loteEditandoId.value = null
  const dataFab = new Date().toISOString().slice(0, 10)
  const dataVal = form.value.validadeEmDias
    ? new Date(Date.now() + form.value.validadeEmDias * 86400000).toISOString().slice(0, 10)
    : ''
  loteForm.value = {
    numeroLote: '', quantidade: 0, custoUnitario: form.value.custoUnitario ?? 0,
    dataFabricacao: dataFab, dataValidade: dataVal,
    localEstoqueId: locaisEstoque.value.find((l: any) => l.principal)?.id ?? locaisEstoque.value[0]?.id ?? '',
  }
  dialogLote.value = true
}

function abrirEditarLote(item: any) {
  loteEditandoId.value = item.id
  loteForm.value = {
    numeroLote: item.numeroLote ?? '',
    quantidade: item.quantidade ?? 0,
    custoUnitario: item.custoUnitario ?? 0,
    dataFabricacao: item.dataFabricacao?.slice(0, 10) ?? '',
    dataValidade: item.dataValidade?.slice(0, 10) ?? '',
    localEstoqueId: item.localEstoqueId ?? '',
  }
  dialogLote.value = true
}

async function salvarLote() {
  if (!loteForm.value.numeroLote || !loteForm.value.localEstoqueId) {
    notif.erro('Informe o número do lote e o local de estoque.'); return
  }
  salvandoLote.value = true
  try {
    if (loteEditandoId.value) {
      await api.put(`/lotes/${loteEditandoId.value}`, {
        numeroLote: loteForm.value.numeroLote,
        quantidade: loteForm.value.quantidade,
        custoUnitario: loteForm.value.custoUnitario,
        dataFabricacao: loteForm.value.dataFabricacao || null,
        dataValidade: loteForm.value.dataValidade || null,
      })
    } else {
      await api.post('/lotes', {
        empresaId: auth.empresaId, produtoId: produtoEditandoId.value,
        localEstoqueId: loteForm.value.localEstoqueId,
        numeroLote: loteForm.value.numeroLote,
        quantidade: loteForm.value.quantidade,
        custoUnitario: loteForm.value.custoUnitario,
        dataFabricacao: loteForm.value.dataFabricacao || null,
        dataValidade: loteForm.value.dataValidade || null,
      })
    }
    notif.ok('Lote salvo!')
    dialogLote.value = false
    await carregarLotes()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao salvar lote.') }
  finally { salvandoLote.value = false }
}

async function excluirLote(id: string) {
  if (!confirm('Excluir este lote?')) return
  try {
    await api.delete(`/lotes/${id}`)
    notif.ok('Lote excluído.')
    await carregarLotes()
  } catch { notif.erro('Erro ao excluir lote.') }
}

const nomeLocal = (id: string) => locaisEstoque.value.find((l: any) => l.id === id)?.nome ?? '—'

async function salvar() {
  const ok = await formulario.value?.validate()
  if (!ok?.valid) return

  salvando.value = true
  try {
    if (editando.value && produtoEditandoId.value) {
      await api.put(`/produtos/${produtoEditandoId.value}`, { ...form.value })
      // Sobe a foto nova, se foi selecionada (o PUT já não mexe na imagem).
      if (arquivoImagem.value) await enviarImagem()
      notif.ok('Produto atualizado!')
    } else {
      const r = await api.post('/produtos', { empresaId: auth.empresaId, ...form.value })
      produtoEditandoId.value = r.data.id
      editando.value = true
      // Envia imagem pendente automaticamente se selecionada
      if (arquivoImagem.value) await enviarImagem()
      notif.ok('Produto criado! Preencha os demais campos e salve novamente se necessário.')
    }
    await listar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.title ?? e?.response?.data?.mensagem ?? 'Erro ao salvar.')
  } finally { salvando.value = false }
}

onMounted(() => {
  listar().catch(() => {})
  carregarCatalogo()
})
</script>

<style scoped>
/* ── Header do dialog ─────────────────────────────────────── */
.prod-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
}

/* ── Body com fundo levemente cinza para dar profundidade ─── */
.prod-form-body {
  background: #f5f6f8;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* ── Seções com card branco ───────────────────────────────── */
.prod-secao {
  background: white;
  border-radius: 12px;
  border: 1px solid #e8edf3;
  overflow: hidden;
}

.prod-secao-header {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 10px 16px;
  background: #f8f9fb;
  border-bottom: 1px solid #e8edf3;
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  color: rgb(var(--v-theme-primary));
}

.prod-secao-body {
  padding: 16px;
}

/* ── Foto do produto ──────────────────────────────────────── */
.prod-foto-col {
  display: flex;
  flex-direction: column;
  align-items: center;
  flex-shrink: 0;
}

.prod-foto-box {
  width: 110px;
  height: 110px;
  border-radius: 12px;
  border: 2px dashed #c8d0dc;
  overflow: hidden;
  cursor: pointer;
  transition: border-color .2s;
  background: #f5f6f8;
}
.prod-foto-box:hover { border-color: rgb(var(--v-theme-primary)); }

.prod-foto-empty {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 4px;
  font-size: 0.7rem;
  color: #9aa;
}

/* ── Opções booleanas (chips clicáveis) ───────────────────── */
.prod-opcoes {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

/* ── Resumo de preços ─────────────────────────────────────── */
.prod-preco-resumo {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.prod-preco-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  background: #f5f6f8;
  border: 1px solid #e4e8ef;
  border-radius: 10px;
  padding: 8px 16px;
  min-width: 90px;
}
.prod-preco-item .label {
  font-size: 0.68rem;
  color: #8896aa;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: .05em;
}
.prod-preco-item .val {
  font-size: 1rem;
  font-weight: 700;
  color: #2a3550;
  margin-top: 2px;
}
.prod-preco-item.destaque {
  background: rgb(var(--v-theme-primary));
  border-color: rgb(var(--v-theme-primary));
}
.prod-preco-item.destaque .label { color: rgba(255,255,255,.7); }
.prod-preco-item.destaque .val { color: white; }
.prod-preco-item.badge {
  background: #eef4ff;
  border-color: #d0e0ff;
}
.prod-preco-item.badge .label { color: #5577bb; }
.prod-preco-item.badge .val { color: #2255bb; }

/* ── Herança do título antigo (caso algum lugar ainda use) ── */
.secao-titulo {
  font-size: 0.75rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  color: rgb(var(--v-theme-primary));
  display: flex;
  align-items: center;
  gap: 4px;
}
</style>
