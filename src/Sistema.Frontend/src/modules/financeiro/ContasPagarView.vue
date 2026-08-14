<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Contas a Pagar</div>
      <v-btn color="teal" variant="tonal" rounded="lg" prepend-icon="mdi-account-cash-outline"
        class="mr-2" :loading="gerandoFolha" @click="gerarFolha">Prever folha</v-btn>
      <v-btn color="deep-purple" variant="tonal" rounded="lg" prepend-icon="mdi-bank-outline"
        class="mr-2" @click="abrirDas">Gerar DAS</v-btn>
      <v-btn color="green-darken-2" variant="tonal" rounded="lg" prepend-icon="mdi-file-upload-outline"
        class="mr-2" @click="abrirComprovantes">Importar comprovantes</v-btn>
      <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">Nova</v-btn>
    </div>

    <GuiaPassos
      id="contas-pagar"
      titulo="Como usar Contas a Pagar"
      :passos="[
        'Use o filtro de <b>Mês</b> ou as datas para listar os títulos do período e clique em <b>Buscar</b>.',
        'Clique em <b>Nova</b> para lançar uma conta. Escolha <b>Único</b>, <b>Parcelar</b> (divide o total) ou <b>Repetir</b> (mesmo valor por período).',
        'No campo <b>Fornecedor / Beneficiário</b>, clique no <b>+</b> ao lado para cadastrar na hora (nome, CPF/CNPJ e telefone) — ou digite o nome e use a opção <b>Cadastrar</b> que aparece quando não existir. O novo já fica selecionado na conta.',
        'Para lançar <b>várias contas iguais com beneficiários diferentes</b>: preencha uma e clique em <b>Salvar e nova</b> — os dados ficam, só o fornecedor é limpo. Na tabela, <b>⧉ Duplicar</b> faz o mesmo a partir de um título já lançado.',
        'Na tabela: <b>💲 Pagar</b> baixa o título, <b>✎ Editar</b> altera dados, <b>⧉ Duplicar</b> copia, <b>↻ Renegociar</b> reprograma valor e vencimento.',
        'Títulos vindos de NF-e já aparecem aqui automaticamente após processar a entrada.',
      ]"
    />

    <!-- Filtros -->
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
          <v-select v-model="filtros.categoria" label="Categoria"
            :items="['Todas', ...categorias]" variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-select v-model="filtros.status" label="Status"
            :items="['Todos', 'EmAberto', 'Pago', 'Vencido', 'Cancelado']"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-autocomplete v-model="filtros.fornecedor" label="Fornecedor / Beneficiário"
            :items="fornecedoresLista" variant="outlined" density="compact" hide-details clearable
            no-data-text="Sem contas no período" />
        </v-col>
      </v-row>
      <div class="d-flex align-center flex-wrap mt-3 filtro-acoes">
        <v-switch v-model="filtros.tudo" color="primary" density="compact" hide-details inset
          label="Ver todas (ignora as datas)" @update:model-value="carregar" />
        <v-spacer />
        <v-btn color="warning" variant="tonal" rounded="lg" prepend-icon="mdi-calendar-today"
          :loading="carregando" @click="filtrarHoje">Hoje</v-btn>
        <v-btn color="primary" rounded="lg" prepend-icon="mdi-magnify"
          :loading="carregando" @click="carregar">Buscar</v-btn>
      </div>
    </v-card>

    <!-- Totais por categoria -->
    <v-row class="mb-3">
      <v-col v-for="t in totaisCategorias" :key="t.label" cols="6" md="3">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="pa-3">
            <div class="d-flex align-center gap-1 mb-1">
              <v-icon :icon="t.icon" :color="t.cor" size="15" />
              <span class="text-caption text-medium-emphasis">{{ t.label }}</span>
            </div>
            <div class="text-h6 font-weight-bold" :class="`text-${t.cor}`">R$ {{ fmt(t.valor) }}</div>
            <div class="text-caption text-medium-emphasis">em aberto</div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" md="3">
        <v-card rounded="xl" elevation="1" color="grey-lighten-4">
          <v-card-text class="pa-3">
            <div class="text-caption text-medium-emphasis mb-1">Total em Aberto</div>
            <div class="text-h6 font-weight-bold text-error">R$ {{ fmt(totalAberto) }}</div>
            <div class="text-caption text-medium-emphasis">
              Vencidos: R$ {{ fmt(totalVencidos) }}
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <!-- Barra de baixa em lote (aparece ao selecionar contas) -->
      <v-slide-y-transition>
        <div v-if="selecionados.length" class="d-flex align-center ga-3 px-3 py-2 mb-2"
          style="background:rgba(var(--v-theme-primary),0.08);border-radius:10px">
          <v-icon icon="mdi-checkbox-multiple-marked-outline" color="primary" />
          <span class="text-body-2">
            <b>{{ qtdSelecionadasAbertas }}</b> conta(s) em aberto selecionada(s) —
            total <b>R$ {{ fmt(totalSelecionado) }}</b>
          </span>
          <v-spacer />
          <v-btn variant="text" size="small" @click="selecionados = []">Limpar</v-btn>
          <v-btn color="primary" prepend-icon="mdi-cash-multiple" :disabled="!qtdSelecionadasAbertas"
            @click="abrirBaixaLote">Pagar em lote (1 boleto)</v-btn>
        </div>
      </v-slide-y-transition>

      <v-data-table
        v-model="selecionados"
        :headers="headers"
        :items="lancamentosFiltrados"
        :loading="carregando"
        item-value="id"
        show-select
        density="compact"
        hover
      >
        <template #item.categoria="{ item }">
          <v-chip :color="corCategoria(item.categoria)" size="x-small" variant="tonal" label>
            <v-icon start size="11" :icon="iconCategoria(item.categoria)" />
            {{ item.categoria ?? '—' }}
          </v-chip>
        </template>
        <template #item.status="{ item }">
          <v-chip :color="corStatus(item.status)" size="small" variant="tonal">
            {{ rotuloStatus(item.status) }}
          </v-chip>
          <v-btn v-if="item.comprovanteUrl" :href="item.comprovanteUrl" target="_blank"
            icon="mdi-file-eye-outline" size="x-small" color="red-darken-1" variant="text"
            title="Ver comprovante de pagamento" />
        </template>
        <template #item.valorOriginal="{ item }">R$ {{ fmt(item.valorOriginal) }}</template>
        <template #item.saldo="{ item }">R$ {{ fmt(item.saldo) }}</template>
        <template #item.dataVencimento="{ item }">{{ fmtData(item.dataVencimento) }}</template>
        <template #item.actions="{ item }">
          <template v-if="!mobile">
            <v-btn icon="mdi-cash-check" size="x-small" color="success" variant="text"
              title="Pagar" @click="abrirPagamento(item)" :disabled="item.status === 'Pago'" />
            <v-btn icon="mdi-pencil-outline" size="x-small" color="primary" variant="text"
              title="Editar" @click="abrirEditar(item)" :disabled="item.status === 'Pago'" />
            <v-btn icon="mdi-content-copy" size="x-small" color="indigo" variant="text"
              title="Duplicar (mesmo valor, outro fornecedor)" @click="duplicarConta(item)" />
            <v-btn icon="mdi-refresh" size="x-small" color="warning" variant="text"
              title="Renegociar" @click="abrirRenegociar(item)" :disabled="item.status === 'Pago'" />
            <v-btn icon="mdi-paperclip" size="x-small" color="teal" variant="text"
              title="Anexar comprovante (imagem/PDF)" @click="anexarComprovante(item)" />
            <v-btn icon="mdi-cancel" size="x-small" color="error" variant="text"
              title="Cancelar título" @click="cancelarTitulo(item)" :disabled="item.status === 'Pago' || item.status === 'Cancelado'" />
          </template>
          <template v-else>
            <v-btn icon="mdi-cash-check" size="small" color="success" variant="text"
              title="Pagar" @click="abrirPagamento(item)" :disabled="item.status === 'Pago'" />
            <v-menu>
              <template #activator="{ props }">
                <v-btn icon="mdi-dots-vertical" size="small" variant="text" v-bind="props" />
              </template>
              <v-list density="compact">
                <v-list-item prepend-icon="mdi-pencil-outline" title="Editar"
                  :disabled="item.status === 'Pago'" @click="abrirEditar(item)" />
                <v-list-item prepend-icon="mdi-content-copy" title="Duplicar"
                  @click="duplicarConta(item)" />
                <v-list-item prepend-icon="mdi-refresh" title="Renegociar"
                  :disabled="item.status === 'Pago'" @click="abrirRenegociar(item)" />
                <v-list-item prepend-icon="mdi-paperclip" title="Anexar comprovante"
                  @click="anexarComprovante(item)" />
                <v-list-item prepend-icon="mdi-cancel" title="Cancelar título"
                  :disabled="item.status === 'Pago' || item.status === 'Cancelado'" @click="cancelarTitulo(item)" />
              </v-list>
            </v-menu>
          </template>
        </template>
      </v-data-table>
    </v-card>

    <!-- Input oculto para anexar comprovante (imagem/PDF) direto na linha, só para guardar -->
    <input ref="comprovanteInput" type="file" accept="image/*,application/pdf"
      class="d-none" @change="onComprovanteSelecionado" />

    <!-- Dialog: Nova Conta a Pagar -->
    <v-dialog v-model="dialogNovo" max-width="560" persistent scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon start color="error">mdi-arrow-up-circle-outline</v-icon>
          Nova Conta a Pagar
        </v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12">
              <v-text-field v-model="form.descricao" label="Descrição *"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-select v-model="form.categoria" label="Categoria *" :items="categorias"
                variant="outlined" density="compact">
                <template #item="{ item, props }">
                  <v-list-item v-bind="props">
                    <template #prepend>
                      <v-icon :icon="iconCategoria(item.value)" :color="corCategoria(item.value)" size="18" class="mr-2" />
                    </template>
                  </v-list-item>
                </template>
                <template #selection="{ item }">
                  <v-chip :color="corCategoria(item.value)" size="x-small" variant="tonal" label class="mr-1">
                    <v-icon start size="11" :icon="iconCategoria(item.value)" />{{ item.value }}
                  </v-chip>
                </template>
              </v-select>
            </v-col>
            <v-col cols="12" sm="6">
              <v-autocomplete v-model="form.fornecedorId" label="Fornecedor / Beneficiário"
                :items="fornecedores" item-title="nome" item-value="id"
                variant="outlined" density="compact" hide-details clearable auto-select-first
                :search="form._buscaForneced" @update:search="v => form._buscaForneced = v">
                <template #item="{ item, props }">
                  <v-list-item v-bind="props" :title="item.raw.nome"
                    :subtitle="item.raw.tipo + (item.raw.documento ? ' · ' + item.raw.documento : '')">
                    <template #prepend>
                      <v-icon size="18" :color="item.raw.tipo === 'Colaborador' ? 'indigo' : 'primary'">
                        {{ item.raw.tipo === 'Colaborador' ? 'mdi-account-tie-outline' : 'mdi-truck-outline' }}
                      </v-icon>
                    </template>
                  </v-list-item>
                </template>
                <template #append-inner>
                  <v-btn icon="mdi-plus" size="x-small" variant="text" density="compact"
                    tabindex="-1" title="Cadastrar fornecedor/beneficiário"
                    @click.stop="abrirNovoFornecedor('nova')" />
                </template>
                <template #no-data>
                  <v-list-item
                    v-if="form._buscaForneced && form._buscaForneced.trim().length >= 2"
                    :title="'Cadastrar ' + form._buscaForneced.trim()"
                    prepend-icon="mdi-plus-circle-outline"
                    @click="abrirNovoFornecedor('nova', form._buscaForneced.trim())" />
                  <v-list-item v-else title="Digite o nome para buscar ou cadastrar" disabled />
                </template>
              </v-autocomplete>
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model.number="form.valorOriginal" label="Valor total (R$) *"
                type="number" step="0.01" prefix="R$" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="form.dataVencimento" label="Primeiro vencimento *"
                type="date" variant="outlined" density="compact" />
            </v-col>

            <!-- Modo de repetição -->
            <v-col cols="12" class="mt-1">
              <v-btn-toggle v-model="form.modo" mandatory density="compact" rounded="lg" color="primary" class="w-100">
                <v-btn value="unico" class="flex-grow-1" size="small">Único</v-btn>
                <v-btn value="parcelar" class="flex-grow-1" size="small">Parcelar</v-btn>
                <v-btn value="repetir" class="flex-grow-1" size="small">Repetir</v-btn>
              </v-btn-toggle>
            </v-col>

            <!-- Parcelar: divide o valor total em N parcelas -->
            <template v-if="form.modo === 'parcelar'">
              <v-col :cols="form.periodo === 'prazos' ? 12 : 6">
                <v-select v-model="form.periodo" label="Intervalo"
                  :items="periodos" item-title="label" item-value="value"
                  variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col v-if="form.periodo !== 'prazos'" cols="6">
                <v-text-field v-model.number="form.quantas" label="Nº de parcelas"
                  type="number" min="2" max="360" variant="outlined" density="compact" hide-details />
              </v-col>
              <!-- Prazos em dias: cada nº é os dias após a data-base (ex.: 21/28/35/42/49) -->
              <v-col v-else cols="12">
                <v-text-field v-model="form.prazos" label="Prazos em dias (ex.: 21/28/35/42/49)"
                  variant="outlined" density="compact" hide-details
                  hint="A 1ª parcela vence 'primeiro vencimento' + o 1º prazo, e assim por diante." persistent-hint />
              </v-col>
              <v-col cols="12">
                <v-alert type="info" variant="tonal" density="compact" class="text-caption">
                  <template v-if="form.periodo === 'prazos'">
                    {{ prazosPreview.length }}x de R$ {{ fmtParcelaN(prazosPreview.length) }} — vencimentos: {{ prazosVencs }}
                  </template>
                  <template v-else>
                    {{ form.quantas || 1 }}x de R$ {{ fmtParcela }} — total R$ {{ fmt(form.valorOriginal || 0) }}
                  </template>
                </v-alert>
              </v-col>
            </template>

            <!-- Repetir: repete o mesmo valor N vezes -->
            <template v-if="form.modo === 'repetir'">
              <v-col cols="6">
                <v-text-field v-model.number="form.quantas" label="Quantas vezes"
                  type="number" min="2" max="360" variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="6">
                <v-select v-model="form.periodo" label="Periodicidade"
                  :items="periodosSemPrazos" item-title="label" item-value="value"
                  variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="12">
                <v-alert type="info" variant="tonal" density="compact" class="text-caption">
                  {{ form.quantas || 1 }}x de R$ {{ fmt(form.valorOriginal || 0) }} — total R$ {{ fmtTotalRepetir }}
                </v-alert>
              </v-col>
            </template>

            <v-col cols="12" class="mt-1">
              <v-text-field v-model="form.observacao" label="Observação"
                variant="outlined" density="compact" hide-details />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogNovo = false" :disabled="salvando">Cancelar</v-btn>
          <!-- Lançar várias contas iguais para beneficiários diferentes -->
          <v-btn variant="tonal" color="indigo" rounded="lg" :loading="salvando"
            title="Salva e mantém os dados para lançar outra, trocando só o beneficiário"
            @click="salvarNova(true)">Salvar e nova</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando" @click="salvarNova()">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Editar -->
    <v-dialog v-model="dialogEditar" max-width="560" persistent scrollable>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon start color="primary">mdi-pencil-outline</v-icon>Editar lançamento
        </v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12">
              <v-text-field v-model="edicao.descricao" label="Descrição *"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-select v-model="edicao.categoria" label="Categoria"
                :items="categorias" variant="outlined" density="compact" clearable hide-details />
            </v-col>
            <v-col cols="12" sm="6">
              <v-autocomplete v-model="edicao.fornecedorId" label="Fornecedor / Beneficiário"
                :items="fornecedores" item-title="nome" item-value="id"
                variant="outlined" density="compact" hide-details clearable auto-select-first
                :search="edicao._buscaForneced" @update:search="v => edicao._buscaForneced = v">
                <template #item="{ item, props }">
                  <v-list-item v-bind="props" :title="item.raw.nome"
                    :subtitle="item.raw.tipo + (item.raw.documento ? ' · ' + item.raw.documento : '')">
                    <template #prepend>
                      <v-icon size="18" :color="item.raw.tipo === 'Colaborador' ? 'indigo' : 'primary'">
                        {{ item.raw.tipo === 'Colaborador' ? 'mdi-account-tie-outline' : 'mdi-truck-outline' }}
                      </v-icon>
                    </template>
                  </v-list-item>
                </template>
                <template #append-inner>
                  <v-btn icon="mdi-plus" size="x-small" variant="text" density="compact"
                    tabindex="-1" title="Cadastrar fornecedor/beneficiário"
                    @click.stop="abrirNovoFornecedor('editar')" />
                </template>
                <template #no-data>
                  <v-list-item
                    v-if="edicao._buscaForneced && edicao._buscaForneced.trim().length >= 2"
                    :title="'Cadastrar ' + edicao._buscaForneced.trim()"
                    prepend-icon="mdi-plus-circle-outline"
                    @click="abrirNovoFornecedor('editar', edicao._buscaForneced.trim())" />
                  <v-list-item v-else title="Digite o nome para buscar ou cadastrar" disabled />
                </template>
              </v-autocomplete>
            </v-col>
            <v-col cols="12" sm="6" class="mt-2">
              <v-text-field v-model.number="edicao.valorOriginal" label="Valor total (R$)"
                type="number" prefix="R$" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" sm="6" class="mt-2">
              <v-text-field v-model="edicao.dataVencimento" label="Primeiro vencimento"
                type="date" variant="outlined" density="compact" hide-details />
            </v-col>

            <!-- Modo -->
            <v-col cols="12" class="mt-2">
              <v-btn-toggle v-model="edicao.modo" mandatory density="compact" rounded="lg" color="primary" class="w-100">
                <v-btn value="unico" class="flex-grow-1" size="small">Único</v-btn>
                <v-btn value="parcelar" class="flex-grow-1" size="small">Parcelar</v-btn>
                <v-btn value="repetir" class="flex-grow-1" size="small">Repetir</v-btn>
              </v-btn-toggle>
            </v-col>

            <template v-if="edicao.modo === 'parcelar'">
              <v-col cols="6">
                <v-text-field v-model.number="edicao.quantas" label="Nº de parcelas"
                  type="number" min="2" max="360" variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="6">
                <v-select v-model="edicao.periodo" label="Intervalo"
                  :items="periodosSemPrazos" item-title="label" item-value="value"
                  variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="12">
                <v-alert type="info" variant="tonal" density="compact" class="text-caption">
                  {{ edicao.quantas || 1 }}x de R$ {{ fmt(Math.round((edicao.valorOriginal||0)/(edicao.quantas||1)*100)/100) }} — total R$ {{ fmt(edicao.valorOriginal||0) }}
                </v-alert>
              </v-col>
            </template>

            <template v-if="edicao.modo === 'repetir'">
              <v-col cols="6">
                <v-text-field v-model.number="edicao.quantas" label="Quantas vezes"
                  type="number" min="2" max="360" variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="6">
                <v-select v-model="edicao.periodo" label="Periodicidade"
                  :items="periodosSemPrazos" item-title="label" item-value="value"
                  variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="12">
                <v-alert type="info" variant="tonal" density="compact" class="text-caption">
                  {{ edicao.quantas || 1 }}x de R$ {{ fmt(edicao.valorOriginal||0) }} — total R$ {{ fmt((edicao.valorOriginal||0)*(edicao.quantas||1)) }}
                </v-alert>
              </v-col>
            </template>

            <v-col cols="12" class="mt-1">
              <v-text-field v-model="edicao.observacao" label="Observação"
                variant="outlined" density="compact" hide-details />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogEditar = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando" @click="confirmarEdicao">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Renegociar -->
    <v-dialog v-model="dialogReneg" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          <v-icon start color="warning">mdi-refresh</v-icon>Renegociar
        </v-card-title>
        <v-card-text>
          <div class="text-body-2 mb-3">
            Saldo atual: <strong>R$ {{ fmt(reneg.saldo) }}</strong>
          </div>
          <v-row dense>
            <v-col cols="12" sm="6">
              <v-text-field v-model.number="reneg.novoValor" label="Novo valor (R$)"
                type="number" prefix="R$" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="reneg.novoVencimento" label="Novo vencimento"
                type="date" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12">
              <v-text-field v-model="reneg.motivo" label="Motivo"
                variant="outlined" density="compact" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogReneg = false">Cancelar</v-btn>
          <v-btn color="warning" rounded="lg" :loading="salvando" @click="confirmarReneg">Renegociar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Registrar Pagamento -->
    <v-dialog v-model="dialogPagamento" max-width="400">
      <v-card rounded="xl" class="pa-4">
        <v-card-title>Registrar Pagamento</v-card-title>
        <v-card-text>
          <v-text-field v-model.number="pagamento.valor" label="Valor pago (R$)"
            type="number" variant="outlined" density="compact" class="mb-2" />
          <v-text-field v-model="pagamento.data" label="Data pagamento"
            type="date" variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions class="justify-end">
          <v-btn variant="text" @click="dialogPagamento = false">Cancelar</v-btn>
          <v-btn color="success" :loading="salvando" @click="confirmarPagamento">Confirmar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: cadastrar fornecedor/beneficiário sem sair da conta a pagar -->
    <v-dialog v-model="dlgFornecedor" max-width="460" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="primary">mdi-account-plus-outline</v-icon>
          Novo fornecedor / beneficiário
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <v-btn-toggle v-model="formForneced.tipo" mandatory divided density="comfortable"
            color="primary" class="mb-3">
            <v-btn value="Fornecedor" size="small"><v-icon start>mdi-truck-outline</v-icon>Fornecedor</v-btn>
            <v-btn value="Colaborador" size="small"><v-icon start>mdi-account-tie-outline</v-icon>Colaborador</v-btn>
          </v-btn-toggle>
          <v-text-field v-model="formForneced.razaoSocial"
            :label="formForneced.tipo === 'Colaborador' ? 'Nome do colaborador *' : 'Nome / Razão Social *'"
            variant="outlined" density="compact" autofocus class="mb-2"
            @keyup.enter="salvarFornecedorRapido" />
          <v-text-field v-model="formForneced.cnpj" label="CPF / CNPJ"
            variant="outlined" density="compact" class="mb-2"
            hint="Opcional — 11 dígitos (CPF) ou 14 (CNPJ)" persistent-hint />
          <v-text-field v-model="formForneced.telefone" label="Telefone"
            variant="outlined" density="compact" hide-details />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgFornecedor = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvandoForneced"
            :disabled="!formForneced.razaoSocial.trim()"
            @click="salvarFornecedorRapido">Cadastrar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Gerar DAS (Simples Nacional) -->
    <v-dialog v-model="dlgDas" max-width="460">
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center">
          <v-icon start color="deep-purple">mdi-bank-outline</v-icon>Gerar DAS — Simples Nacional
        </v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="6">
              <v-select v-model="das.mes" :items="mesesOpcoes" item-title="label" item-value="value"
                label="Mês competência" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="das.ano" label="Ano" type="number"
                variant="outlined" density="compact" />
            </v-col>
          </v-row>
          <v-text-field v-model.number="das.faturamento" label="Faturamento do mês (R$) *"
            type="number" prefix="R$" variant="outlined" density="compact" class="mt-1" autofocus />
          <v-text-field v-model.number="das.aliquota" label="Alíquota efetiva (%)"
            type="number" suffix="%" variant="outlined" density="compact"
            hint="Anexo I — atualize quando mudar de faixa (RBT12)" persistent-hint />
          <v-alert type="info" variant="tonal" density="compact" class="mt-3">
            DAS previsto: <b>R$ {{ dasValor.toFixed(2) }}</b> · vence dia 20/{{ String(das.mes % 12 + 1).padStart(2,'0') }}
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgDas = false">Cancelar</v-btn>
          <v-btn color="deep-purple" rounded="lg" :loading="gerandoDas"
            :disabled="!das.faturamento || !das.aliquota" @click="gerarDas">Lançar DAS</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ── Dialog: Importar comprovantes de pagamento ── -->
    <v-dialog v-model="dlgComp" max-width="920" persistent scrollable>
      <v-card rounded="xl">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon icon="mdi-file-upload-outline" color="green-darken-2" class="mr-2" />
          Importar comprovantes de pagamento
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="dlgComp = false" />
        </v-card-title>

        <v-card-text>
          <v-alert type="info" variant="tonal" density="compact" rounded="lg" class="mb-3">
            Envie os <b>PDFs dos comprovantes</b> pagos no dia. O sistema lê cada um, sugere a
            conta correspondente e guarda o PDF. <b>Confira</b> os pareamentos antes de dar baixa.
          </v-alert>

          <v-file-input
            v-model="arquivosComp" label="Comprovantes (PDF) — pode selecionar vários"
            accept="application/pdf" multiple chips show-size prepend-icon="mdi-paperclip"
            variant="outlined" density="comfortable" :disabled="analisando" />
          <div class="d-flex justify-end mb-2">
            <v-btn color="green-darken-2" variant="flat" rounded="lg" prepend-icon="mdi-text-search"
              :loading="analisando" :disabled="!arquivosComp.length" @click="analisarComprovantes">
              Ler e sugerir baixas
            </v-btn>
          </div>

          <template v-if="resultadosComp.length">
            <v-divider class="mb-3" />
            <div class="text-caption text-medium-emphasis mb-2">
              Confira cada comprovante e a conta que receberá a baixa.
              Os <b>verdes</b> vêm pré-marcados (valor e beneficiário batem); os <b>amarelos</b> são só sugestão
              e entram <b>desmarcados</b> — confira antes de marcar.
            </div>
            <v-card v-for="(r, i) in resultadosComp" :key="i" variant="tonal"
              :color="!r.escolhaId ? 'grey-lighten-3' : (r.sugestao?.confiancaAlta ? 'success' : 'warning')"
              rounded="lg" class="mb-2">
              <v-card-text class="pa-3">
                <div class="d-flex align-center mb-2">
                  <v-checkbox-btn v-model="r.selecionado" :disabled="!r.escolhaId" color="success" class="flex-grow-0 mr-1" />
                  <v-icon icon="mdi-file-pdf-box" color="red-darken-1" class="mr-1" />
                  <a :href="r.comprovanteUrl" target="_blank" class="text-body-2 font-weight-medium text-truncate" style="max-width:220px">
                    {{ r.arquivo }}
                  </a>
                  <v-spacer />
                  <span class="text-caption text-medium-emphasis mr-3">Lido:</span>
                  <v-chip size="small" label class="mr-1" color="green-darken-2">
                    {{ r.valorLido != null ? 'R$ ' + fmt(r.valorLido) : 'valor?' }}
                  </v-chip>
                  <v-chip size="small" label variant="text">{{ r.dataLida ? fmtData(r.dataLida) : 'data?' }}</v-chip>
                  <v-chip v-if="r.escolhaId && r.escolhaId !== '__nova__' && !r.sugestao?.confiancaAlta"
                    size="small" label color="warning" class="ml-1" prepend-icon="mdi-alert-outline">
                    revisar (valor/beneficiário não batem)
                  </v-chip>
                </div>
                <div class="text-caption text-medium-emphasis mb-1">
                  Beneficiário lido: <b>{{ r.beneficiarioLido || '—' }}</b>
                  <span v-if="r.documentoLido"> · {{ r.documentoLido }}</span>
                </div>
                <v-select
                  v-model="r.escolhaId" :items="opcoesConta(r)" item-title="titulo" item-value="lancamentoId"
                  label="Dar baixa na conta" variant="outlined" density="compact" hide-details
                  clearable @update:model-value="r.selecionado = !!r.escolhaId" />
                <v-row v-if="r.escolhaId === '__nova__'" dense class="mt-1">
                  <v-col cols="12" sm="7">
                    <v-text-field v-model="r.novaDescricao" label="Descrição da nova conta"
                      variant="outlined" density="compact" hide-details prepend-inner-icon="mdi-tag-plus-outline" />
                  </v-col>
                  <v-col cols="12" sm="5">
                    <v-select v-model="r.novaCategoria" :items="categorias" label="Categoria *"
                      variant="outlined" density="compact" hide-details />
                  </v-col>
                </v-row>
              </v-card-text>
            </v-card>

            <div class="d-flex align-center mt-3">
              <span class="text-body-2 text-medium-emphasis">
                {{ selecionadosComp.length }} de {{ resultadosComp.length }} marcados para baixa
              </span>
              <v-spacer />
              <v-btn variant="text" @click="dlgComp = false">Fechar</v-btn>
              <v-btn color="success" rounded="lg" prepend-icon="mdi-cash-check"
                :loading="confirmandoComp" :disabled="!selecionadosComp.length" @click="confirmarComprovantes">
                Confirmar {{ selecionadosComp.length }} baixa(s)
              </v-btn>
            </div>
          </template>
        </v-card-text>
      </v-card>
    </v-dialog>

    <!-- Baixa em lote: 1 pagamento/comprovante para várias contas -->
    <v-dialog v-model="dlgLote" max-width="480" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-1 d-flex align-center">
          <v-icon icon="mdi-cash-multiple" color="primary" class="mr-2" />
          Pagar em lote
        </v-card-title>
        <v-card-text>
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            <b>{{ qtdSelecionadasAbertas }}</b> conta(s) — total <b>R$ {{ fmt(totalSelecionado) }}</b>.
            Confira se bate com o valor do boleto.
          </v-alert>
          <v-list density="compact" class="pa-0 mb-2" style="max-height:160px;overflow-y:auto">
            <v-list-item v-for="l in abertasSelecionadas" :key="l.id" class="px-2" min-height="30">
              <v-list-item-title class="text-caption">{{ l.descricao }}</v-list-item-title>
              <template #append><span class="text-caption font-weight-medium">R$ {{ fmt(l.saldo ?? l.valorOriginal) }}</span></template>
            </v-list-item>
          </v-list>
          <v-text-field v-model="lote.data" type="date" label="Data do pagamento" variant="outlined" density="compact" class="mb-2" />
          <v-file-input v-model="lote.arquivo" accept="application/pdf,image/*" label="Comprovante / boleto (1 arquivo p/ todas)"
            prepend-icon="mdi-paperclip" variant="outlined" density="compact" show-size hide-details />
        </v-card-text>
        <v-card-actions class="pa-3 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgLote = false">Cancelar</v-btn>
          <v-btn color="primary" :loading="salvandoLote" :disabled="!qtdSelecionadasAbertas || !lote.data" @click="confirmarBaixaLote">
            Confirmar baixa ({{ qtdSelecionadasAbertas }})
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { rotuloStatus } from '@/utils/status'
import FiltroMes from '@/components/FiltroMes.vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useDisplay } from 'vuetify'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

// Anexar comprovante (imagem/PDF) direto na linha — só guarda, não lê nada.
const comprovanteInput = ref<HTMLInputElement | null>(null)
const anexarItemId = ref<string | null>(null)
function anexarComprovante(item: any) {
  anexarItemId.value = item.id
  comprovanteInput.value?.click()
}
async function onComprovanteSelecionado(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  const id = anexarItemId.value
  if (file && id) {
    const fd = new FormData()
    fd.append('arquivo', file)
    try {
      await api.post(`/contas-pagar/${id}/comprovante`, fd,
        { headers: { 'Content-Type': 'multipart/form-data' } })
      notif.ok('Comprovante anexado!')
      await carregar()
    } catch { notif.erro('Erro ao anexar o comprovante.') }
  }
  input.value = ''
  anexarItemId.value = null
}
const { mobile } = useDisplay()
const carregando = ref(false)

// ── Importar comprovantes de pagamento ───────────────────────────────────────
interface CandidatoConta {
  lancamentoId: string; descricao: string; beneficiario: string
  valorOriginal: number; saldo: number; vencimento: string; score: number
  confiancaAlta?: boolean; valorExato?: boolean
}
interface ResultadoComp {
  arquivo: string; comprovanteUrl: string
  valorLido: number | null; dataLida: string | null; vencimentoLido: string | null
  beneficiarioLido: string | null; documentoLido: string | null
  sugestao: CandidatoConta | null; candidatos: CandidatoConta[]
  escolhaId: string | null; selecionado: boolean; novaDescricao: string; novaCategoria: string
}
const NOVA = '__nova__'
const dlgComp = ref(false)
const arquivosComp = ref<File[]>([])
const analisando = ref(false)
const confirmandoComp = ref(false)
const resultadosComp = ref<ResultadoComp[]>([])
const selecionadosComp = computed(() => resultadosComp.value.filter(r => r.selecionado && r.escolhaId))

function abrirComprovantes() {
  arquivosComp.value = []
  resultadosComp.value = []
  dlgComp.value = true
}

function opcoesConta(r: ResultadoComp) {
  const lista = (r.candidatos ?? []).map(c => ({
    lancamentoId: c.lancamentoId,
    titulo: `${c.descricao} — ${c.beneficiario || 's/ beneficiário'} · R$ ${fmt(c.saldo || c.valorOriginal)} (venc ${fmtData(c.vencimento)})`
  }))
  // Sempre permite criar uma nova conta (ex.: comprovante de conta não cadastrada).
  lista.push({ lancamentoId: NOVA, titulo: '➕ Criar nova conta a pagar (não cadastrada) e dar baixa' })
  return lista
}

async function analisarComprovantes() {
  if (!arquivosComp.value.length) return
  analisando.value = true
  try {
    const fd = new FormData()
    fd.append('empresaId', auth.empresaId as string)
    for (const f of arquivosComp.value) fd.append('arquivos', f)
    const r = await api.post('/contas-pagar/comprovantes/analisar', fd,
      { headers: { 'Content-Type': 'multipart/form-data' } })
    const rows: ResultadoComp[] = (r.data ?? []).map((x: any) => ({
      ...x,
      escolhaId: null,
      // Só pré-marca para baixa quando a confiança é ALTA (valor igual + beneficiário/CNPJ bate).
      // As demais entram DESMARCADAS, com aviso de "revisar" — evita baixa em conta errada.
      selecionado: !!x.sugestao?.confiancaAlta,
      novaDescricao: String(x.beneficiarioLido || x.arquivo || 'Pagamento (comprovante)').slice(0, 120),
      novaCategoria: 'Despesas Variáveis',
    }))
    // Atribuição EXCLUSIVA: cada conta candidata é usada no máx. 1 vez (maior score
    // primeiro). Assim dois pagamentos de mesmo valor não caem na mesma conta —
    // o que sobra sem conta cai em "criar nova".
    const usadas = new Set<string>()
    ;[...rows].sort((a, b) => (b.sugestao?.score ?? 0) - (a.sugestao?.score ?? 0)).forEach(r => {
      const cand = (r.candidatos ?? []).find(c => !usadas.has(c.lancamentoId))
      if (cand) { r.escolhaId = cand.lancamentoId; usadas.add(cand.lancamentoId) }
      else r.escolhaId = NOVA
    })
    resultadosComp.value = rows
    const novos = rows.filter(x => x.escolhaId === NOVA).length
    if (novos) notif.aviso(`${novos} comprovante(s) sem conta cadastrada — marcados para "criar conta e dar baixa".`)
  } catch (e: any) {
    notif.erro(e?.response?.data ?? 'Falha ao ler os comprovantes.')
  } finally { analisando.value = false }
}

async function confirmarComprovantes() {
  const itens = selecionadosComp.value.map(r => r.escolhaId === NOVA
    ? {
      criar: true,
      descricao: (r.novaDescricao || r.beneficiarioLido || 'Pagamento (comprovante)').trim(),
      categoria: r.novaCategoria,
      valorPago: r.valorLido ?? 0,
      dataPagamento: r.dataLida ?? null,
      vencimento: r.vencimentoLido ?? r.dataLida ?? null,
      comprovanteUrl: r.comprovanteUrl,
    }
    : {
      lancamentoId: r.escolhaId,
      valorPago: r.valorLido ?? 0,
      dataPagamento: r.dataLida ?? null,
      comprovanteUrl: r.comprovanteUrl,
    })
  if (!itens.length) return
  confirmandoComp.value = true
  try {
    const r = await api.post('/contas-pagar/comprovantes/confirmar', { empresaId: auth.empresaId, itens })
    const { baixados, criados } = r.data
    notif.ok(`${baixados} conta(s) baixada(s)${criados ? ` (${criados} criada(s) do comprovante)` : ''}.`)
    dlgComp.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e?.response?.data ?? 'Falha ao confirmar as baixas.')
  } finally { confirmandoComp.value = false }
}
// ── Baixa em lote (ex.: 1 boleto do Rápido 90 que junta vários CT-e) ──────
const selecionados = ref<string[]>([])
const abertasSelecionadas = computed(() => lancamentosFiltrados.value.filter((l: any) =>
  selecionados.value.includes(l.id) && l.status !== 'Pago' && l.status !== 'Cancelado'
  && (l.saldo ?? l.valorOriginal) > 0))
const qtdSelecionadasAbertas = computed(() => abertasSelecionadas.value.length)
const totalSelecionado = computed(() => abertasSelecionadas.value.reduce((s: number, l: any) => s + (l.saldo ?? l.valorOriginal), 0))

const dlgLote = ref(false)
const salvandoLote = ref(false)
const lote = ref<any>({ data: new Date().toISOString().slice(0, 10), arquivo: null as File | File[] | null })
function abrirBaixaLote() {
  lote.value = { data: new Date().toISOString().slice(0, 10), arquivo: null }
  dlgLote.value = true
}
async function confirmarBaixaLote() {
  const ids = abertasSelecionadas.value.map((l: any) => l.id)
  if (!ids.length) return
  salvandoLote.value = true
  try {
    const fd = new FormData()
    fd.append('ids', ids.join(','))
    fd.append('dataPagamento', lote.value.data)
    const f = Array.isArray(lote.value.arquivo) ? lote.value.arquivo[0] : lote.value.arquivo
    if (f) fd.append('comprovante', f)
    const { data } = await api.post('/contas-pagar/pagar-lote', fd)
    notif.ok(`${data.pagas} conta(s) baixada(s) — R$ ${fmt(data.totalPago)}${data.comprovanteUrl ? ' (comprovante anexado)' : ''}.`)
    dlgLote.value = false
    selecionados.value = []
    await carregar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Falha na baixa em lote.')
  } finally { salvandoLote.value = false }
}

const salvando = ref(false)
const gerandoFolha = ref(false)
const dlgDas = ref(false)
const gerandoDas = ref(false)
const das = ref({ ano: new Date().getFullYear(), mes: new Date().getMonth() + 1, faturamento: null as number | null, aliquota: 8.45 })
const mesesOpcoes = [
  { label: 'Janeiro', value: 1 }, { label: 'Fevereiro', value: 2 }, { label: 'Março', value: 3 },
  { label: 'Abril', value: 4 }, { label: 'Maio', value: 5 }, { label: 'Junho', value: 6 },
  { label: 'Julho', value: 7 }, { label: 'Agosto', value: 8 }, { label: 'Setembro', value: 9 },
  { label: 'Outubro', value: 10 }, { label: 'Novembro', value: 11 }, { label: 'Dezembro', value: 12 },
]
const dasValor = computed(() => ((das.value.faturamento || 0) * (das.value.aliquota || 0)) / 100)
const lancamentos = ref<any[]>([])
const dialogPagamento = ref(false)
const dialogNovo = ref(false)
const dialogEditar = ref(false)
const dialogReneg = ref(false)
const pagamento = ref({ id: '', valor: 0, data: new Date().toISOString().slice(0, 10) })
const fornecedores = ref<any[]>([])
const edicao = ref({
  id: '', descricao: '', categoria: '', valorOriginal: 0,
  dataVencimento: '', observacao: '', fornecedorId: '' as string | null, _buscaForneced: '',
  modo: 'unico' as 'unico' | 'parcelar' | 'repetir', quantas: 2, periodo: 'mensal', prazos: '21/28/35/42/49',
})
const reneg = ref({ id: '', saldo: 0, novoValor: 0, novoVencimento: '', motivo: '' })

const categorias = ['Custo (CMV)', 'Despesas Administrativas', 'Despesas Operacionais', 'Despesas Variáveis', 'Pessoas', 'Impostos']

const periodos = [
  { label: 'Diário',      value: 'diario' },
  { label: 'Semanal',     value: 'semanal' },
  { label: 'Quinzenal',   value: 'quinzenal' },
  { label: 'Mensal',      value: 'mensal' },
  { label: 'Bimestral',   value: 'bimestral' },
  { label: 'Trimestral',  value: 'trimestral' },
  { label: 'Semestral',   value: 'semestral' },
  { label: 'Anual',       value: 'anual' },
  { label: 'Prazos em dias (21/28/35/42/49)', value: 'prazos' },
]
// "prazos" só faz sentido no modo Parcelar; demais selects usam esta lista.
const periodosSemPrazos = periodos.filter(p => p.value !== 'prazos')

// "21/28/35/42/49" (ou vírgula/espaço) → [21,28,35,42,49]
function parsePrazos(s: string): number[] {
  return (s || '').split(/[\/,;\s]+/).map(x => parseInt(x, 10)).filter(n => Number.isFinite(n) && n >= 0)
}
function addDias(base: string, dias: number): string {
  const d = new Date(base + 'T12:00:00'); d.setDate(d.getDate() + dias)
  return d.toISOString().slice(0, 10)
}

const formPadrao = () => ({
  descricao: '', categoria: '', fornecedorId: null as string | null, _buscaForneced: '',
  valorOriginal: 0, dataVencimento: '', observacao: '',
  modo: 'unico' as 'unico' | 'parcelar' | 'repetir',
  quantas: 2,
  periodo: 'mensal',
  prazos: '21/28/35/42/49',
})
const form = ref(formPadrao())

const fmtParcela = computed(() =>
  fmt(Math.round((form.value.valorOriginal || 0) / (form.value.quantas || 1) * 100) / 100)
)
const fmtTotalRepetir = computed(() =>
  fmt(Math.round((form.value.valorOriginal || 0) * (form.value.quantas || 1) * 100) / 100)
)
const prazosPreview = computed(() => parsePrazos(form.value.prazos))
const fmtParcelaN = (n: number) => fmt(Math.round((form.value.valorOriginal || 0) / Math.max(1, n) * 100) / 100)
const prazosVencs = computed(() => form.value.dataVencimento
  ? prazosPreview.value.map(off => fmtData(addDias(form.value.dataVencimento, off))).join(' · ')
  : prazosPreview.value.map(off => `+${off}d`).join(' · '))

function proximaData(base: string, periodo: string, n: number): string {
  const d = new Date(base + 'T12:00:00')
  const map: Record<string, () => void> = {
    diario:     () => d.setDate(d.getDate() + n),
    semanal:    () => d.setDate(d.getDate() + n * 7),
    quinzenal:  () => d.setDate(d.getDate() + n * 15),
    mensal:     () => d.setMonth(d.getMonth() + n),
    bimestral:  () => d.setMonth(d.getMonth() + n * 2),
    trimestral: () => d.setMonth(d.getMonth() + n * 3),
    semestral:  () => d.setMonth(d.getMonth() + n * 6),
    anual:      () => d.setFullYear(d.getFullYear() + n),
  }
  map[periodo]?.()
  return d.toISOString().slice(0, 10)
}

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date(new Date().getFullYear(), new Date().getMonth() + 1, 0).toISOString().slice(0, 10),
  categoria: 'Todas',
  status: 'Todos',
  fornecedor: null as string | null,
  tudo: false,
})

const hoje = () => new Date(new Date().toISOString().slice(0, 10) + 'T12:00:00')

const fornecedoresLista = computed(() =>
  [...new Set(lancamentos.value.map((l: any) => l.fornecedorNome).filter(Boolean))].sort((a: any, b: any) => a.localeCompare(b))
)

const lancamentosFiltrados = computed(() => {
  let lista = lancamentos.value
  if (filtros.value.categoria !== 'Todas')
    lista = lista.filter(l => l.categoria === filtros.value.categoria)
  if (filtros.value.fornecedor)
    lista = lista.filter(l => l.fornecedorNome === filtros.value.fornecedor)
  if (filtros.value.status === 'Todos') {
    // "Todos" não inclui cancelados/estornados (evita parecer duplicado)
    lista = lista.filter(l => l.status !== 'Cancelado' && l.status !== 'Estornado' && l.status !== 'Renegociado')
  } else if (filtros.value.status === 'Vencido') {
    lista = lista.filter(l => l.status === 'EmAberto' && new Date(String(l.dataVencimento).slice(0, 10) + 'T12:00:00') < hoje())
  } else {
    lista = lista.filter(l => l.status === filtros.value.status)
  }
  return lista
})

function somarAberto(cat: string) {
  return lancamentos.value
    .filter(l => l.categoria === cat && l.status === 'EmAberto')
    .reduce((s: number, l: any) => s + l.saldo, 0)
}

const totalAberto = computed(() =>
  lancamentos.value.filter(l => l.status === 'EmAberto').reduce((s: number, l: any) => s + l.saldo, 0)
)
const totalVencidos = computed(() =>
  lancamentos.value
    .filter(l => l.status === 'EmAberto' && new Date(String(l.dataVencimento).slice(0, 10) + 'T12:00:00') < hoje())
    .reduce((s: number, l: any) => s + l.saldo, 0)
)

const totaisCategorias = computed(() => [
  { label: 'Custo (CMV)', valor: somarAberto('Custo (CMV)'), cor: 'brown', icon: 'mdi-package-variant-closed' },
  { label: 'Despesas Administrativas', valor: somarAberto('Despesas Administrativas'),
    cor: 'deep-purple', icon: 'mdi-home-city-outline' },
  { label: 'Despesas Operacionais', valor: somarAberto('Despesas Operacionais'), cor: 'teal',   icon: 'mdi-cog-outline' },
  { label: 'Despesas Variáveis', valor: somarAberto('Despesas Variáveis'), cor: 'orange',      icon: 'mdi-chart-bell-curve-cumulative' },
  { label: 'Pessoas',            valor: somarAberto('Pessoas'),            cor: 'blue',        icon: 'mdi-account-group-outline' },
  { label: 'Impostos',           valor: somarAberto('Impostos'),           cor: 'error',       icon: 'mdi-gavel' },
])

const headersCompletos = [
  { title: 'Descrição',  key: 'descricao',     sortable: true },
  { title: 'Categoria',  key: 'categoria',     width: 170 },
  { title: 'Fornecedor', key: 'fornecedorNome' },
  { title: 'Vencimento', key: 'dataVencimento', sortable: true },
  { title: 'Valor',      key: 'valorOriginal' },
  { title: 'Saldo',      key: 'saldo' },
  { title: 'Status',     key: 'status' },
  { title: '',           key: 'actions', sortable: false },
]
// No celular: só o essencial; as demais colunas ficam no detalhe/ações.
const headersMobile = [
  { title: 'Descrição',  key: 'descricao', sortable: true },
  { title: 'Vence',      key: 'dataVencimento', width: 88 },
  { title: 'Saldo',      key: 'saldo', width: 90 },
  { title: '',           key: 'actions', sortable: false, width: 88 },
]
const headers = computed(() => mobile.value ? headersMobile : headersCompletos)

function corCategoria(cat?: string) {
  const mapa: Record<string, string> = {
    'Despesas Administrativas': 'deep-purple', 'Despesas Operacionais': 'teal',
    'Despesas Variáveis': 'orange', 'Pessoas': 'blue', 'Impostos': 'error',
  }
  return mapa[cat ?? ''] ?? 'grey'
}
function iconCategoria(cat?: string) {
  const mapa: Record<string, string> = {
    'Despesas Administrativas': 'mdi-home-city-outline',
    'Despesas Operacionais': 'mdi-cog-outline',
    'Despesas Variáveis': 'mdi-chart-bell-curve-cumulative',
    'Pessoas': 'mdi-account-group-outline', 'Impostos': 'mdi-gavel',
  }
  return mapa[cat ?? ''] ?? 'mdi-tag-outline'
}
const corStatus = (s: string) =>
  ({ EmAberto: 'info', Pago: 'success', Cancelado: 'error', Renegociado: 'warning' } as any)[s] ?? 'default'
const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 })
// A data pode vir como "2026-07-22" ou ISO completo "2026-07-22T00:00:00" — normaliza para os 10 primeiros.
const fmtData = (d?: string) => d ? new Date(String(d).slice(0, 10) + 'T12:00:00').toLocaleDateString('pt-BR') : '—'

async function carregar() {
  carregando.value = true
  try {
    // "Ver todas" → não envia datas (backend retorna tudo)
    const params: any = { empresaId: auth.empresaId }
    if (!filtros.value.tudo) {
      params.inicio = filtros.value.inicio
      params.fim = filtros.value.fim
    }
    const r = await api.get('/contas-pagar', { params })
    lancamentos.value = r.data
  } finally { carregando.value = false }
}

// Gera a previsão da folha do mês atual: salários (5º dia útil, Pessoas) + FGTS/INSS (dia 20, Impostos).
// Idempotente no backend — se o mês já foi gerado, não duplica.
async function gerarFolha() {
  gerandoFolha.value = true
  try {
    const r = await api.post('/folha/gerar-previsao')
    const qtd = r.data?.contasGeradas ?? 0
    if (qtd > 0) notif.ok(`Previsão de folha gerada: ${qtd} conta(s) para ${r.data.competencia}.`)
    else notif.aviso('A folha desta competência já havia sido gerada (nada duplicado).')
    await carregar()
  } catch { notif.erro('Erro ao gerar previsão de folha.') }
  finally { gerandoFolha.value = false }
}

function abrirDas() {
  const h = new Date()
  das.value = { ano: h.getFullYear(), mes: h.getMonth() + 1, faturamento: null, aliquota: das.value.aliquota || 8.45 }
  dlgDas.value = true
}

// Lança o DAS: faturamento informado × alíquota efetiva → conta a pagar (Impostos, dia 20 do mês seguinte).
async function gerarDas() {
  gerandoDas.value = true
  try {
    const r = await api.post('/das/gerar', {
      empresaId: auth.empresaId, ano: das.value.ano, mes: das.value.mes,
      faturamento: das.value.faturamento, aliquota: das.value.aliquota,
    })
    notif.ok(`DAS de ${r.data.competencia} lançado: R$ ${Number(r.data.valor).toFixed(2)} (vence ${fmtData(r.data.vencimento)}).`)
    dlgDas.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.erro || 'Erro ao gerar DAS.')
  } finally { gerandoDas.value = false }
}

// Filtro rápido "Hoje": mostra todas as contas que vencem hoje (sem esconder por status/categoria).
function filtrarHoje() {
  const hoje = new Date().toISOString().slice(0, 10)
  filtros.value.inicio = hoje
  filtros.value.fim = hoje
  filtros.value.tudo = false
  filtros.value.status = 'Todos'
  filtros.value.categoria = 'Todas'
  carregar()
}

function abrirNovo() {
  form.value = formPadrao()
  dialogNovo.value = true
}

/**
 * Duplica uma conta: reabre "Nova Conta" com descrição, categoria, valor e
 * vencimento preenchidos e o fornecedor EM BRANCO — o caso comum é lançar o
 * mesmo valor para vários beneficiários diferentes.
 */
function duplicarConta(item: any) {
  form.value = {
    ...formPadrao(),
    descricao: item.descricao ?? '',
    categoria: item.categoria ?? '',
    valorOriginal: item.valorOriginal ?? 0,
    dataVencimento: (item.dataVencimento ?? '').slice(0, 10),
    observacao: item.observacao ?? '',
    fornecedorId: null,      // escolher o novo beneficiário
    _buscaForneced: '',
  }
  dialogNovo.value = true
  notif.aviso('Cópia carregada. Escolha o fornecedor/beneficiário e salve.')
}

/**
 * @param continuar mantém o diálogo aberto com os mesmos dados e o
 *   fornecedor em branco, para lançar a próxima conta trocando só o beneficiário.
 */
async function salvarNova(continuar = false) {
  const f = form.value
  if (!f.descricao || !f.categoria || f.valorOriginal <= 0 || !f.dataVencimento) {
    notif.erro('Preencha todos os campos obrigatórios.')
    return
  }
  salvando.value = true
  try {
    const base = {
      empresaId: auth.empresaId,
      descricao: f.descricao,
      categoria: f.categoria,
      ...beneficiarioPayload(f.fornecedorId),
      observacao: f.observacao,
    }

    if (f.modo === 'unico') {
      await api.post('/contas-pagar', {
        ...base, valor: f.valorOriginal,
        primeiroVencimento: f.dataVencimento, totalParcelas: 1,
      })
    } else if (f.modo === 'parcelar' && f.periodo === 'prazos') {
      const offs = parsePrazos(f.prazos)
      if (offs.length === 0) { notif.erro('Informe os prazos em dias (ex.: 21/28/35/42/49).'); return }
      const n = offs.length
      const valorParcela = Math.round(f.valorOriginal / n * 100) / 100
      for (let i = 0; i < n; i++) {
        await api.post('/contas-pagar', {
          ...base,
          descricao: `${f.descricao} ${i + 1}/${n}`,
          valor: i === n - 1
            ? Math.round((f.valorOriginal - valorParcela * (n - 1)) * 100) / 100
            : valorParcela,
          primeiroVencimento: addDias(f.dataVencimento, offs[i]),
          totalParcelas: 1,
        })
      }
    } else if (f.modo === 'parcelar') {
      const n = Math.max(1, f.quantas || 1)
      const valorParcela = Math.round(f.valorOriginal / n * 100) / 100
      for (let i = 0; i < n; i++) {
        await api.post('/contas-pagar', {
          ...base,
          descricao: `${f.descricao} ${i + 1}/${n}`,
          valor: i === n - 1
            ? Math.round((f.valorOriginal - valorParcela * (n - 1)) * 100) / 100
            : valorParcela,
          primeiroVencimento: i === 0 ? f.dataVencimento : proximaData(f.dataVencimento, f.periodo, i),
          totalParcelas: 1,
        })
      }
    } else if (f.modo === 'repetir') {
      const n = Math.max(1, f.quantas || 1)
      for (let i = 0; i < n; i++) {
        await api.post('/contas-pagar', {
          ...base,
          descricao: `${f.descricao} ${i + 1}/${n}`,
          valor: f.valorOriginal,
          primeiroVencimento: i === 0 ? f.dataVencimento : proximaData(f.dataVencimento, f.periodo, i),
          totalParcelas: 1,
        })
      }
    }

    if (continuar) {
      // Mantém valor/descrição/categoria/vencimento e limpa só o beneficiário
      f.fornecedorId = null
      f._buscaForneced = ''
      notif.ok('Conta cadastrada! Escolha o próximo fornecedor/beneficiário.')
    } else {
      notif.ok('Conta(s) a pagar cadastrada(s)!')
      dialogNovo.value = false
    }
    await carregar()
  } catch { notif.erro('Erro ao salvar.') }
  finally { salvando.value = false }
}

function abrirPagamento(item: any) {
  pagamento.value = { id: item.id, valor: item.saldo, data: new Date().toISOString().slice(0, 10) }
  dialogPagamento.value = true
}

async function confirmarPagamento() {
  salvando.value = true
  try {
    await api.post(`/contas-pagar/${pagamento.value.id}/pagar`, {
      valorPago: pagamento.value.valor, dataPagamento: pagamento.value.data,
    })
    notif.ok('Pagamento registrado!')
    dialogPagamento.value = false
    await carregar()
  } finally { salvando.value = false }
}

function abrirEditar(item: any) {
  edicao.value = {
    id: item.id,
    descricao: item.descricao,
    categoria: item.categoria ?? '',
    valorOriginal: item.valorOriginal,
    dataVencimento: item.dataVencimento?.slice(0, 10) ?? '',
    observacao: item.observacao ?? '',
    fornecedorId: item.fornecedorId ?? item.colaboradorId ?? null,
    modo: 'unico',
    quantas: 2,
    periodo: 'mensal',
  }
  dialogEditar.value = true
}

async function confirmarEdicao() {
  const e = edicao.value
  salvando.value = true
  try {
    if (e.modo === 'unico') {
      await api.put(`/contas-pagar/${e.id}`, {
        descricao: e.descricao, categoria: e.categoria,
        valorOriginal: e.valorOriginal, dataVencimento: e.dataVencimento,
        observacao: e.observacao, ...beneficiarioPayload(e.fornecedorId),
      })
    } else {
      // Cancel existing entry, then create N new ones
      await api.post(`/contas-pagar/${e.id}/cancelar`, {})
      const n = Math.max(2, e.quantas || 2)
      const base = { empresaId: auth.empresaId, descricao: e.descricao, categoria: e.categoria, observacao: e.observacao, ...beneficiarioPayload(e.fornecedorId) }
      for (let i = 0; i < n; i++) {
        const valor = e.modo === 'parcelar'
          ? (i === n - 1 ? Math.round((e.valorOriginal - Math.round(e.valorOriginal / n * 100) / 100 * (n - 1)) * 100) / 100 : Math.round(e.valorOriginal / n * 100) / 100)
          : e.valorOriginal
        const descricao = `${e.descricao} ${i + 1}/${n}`
        const vencimento = i === 0 ? e.dataVencimento : proximaData(e.dataVencimento, e.periodo, i)
        await api.post('/contas-pagar', { ...base, descricao, valor, primeiroVencimento: vencimento, totalParcelas: 1 })
      }
    }
    notif.ok('Lançamento atualizado.')
    dialogEditar.value = false
    await carregar()
  } catch { notif.erro('Erro ao editar lançamento.') }
  finally { salvando.value = false }
}

function abrirRenegociar(item: any) {
  reneg.value = {
    id: item.id,
    saldo: item.saldo,
    novoValor: item.saldo,
    novoVencimento: new Date(new Date().setDate(new Date().getDate() + 30)).toISOString().slice(0, 10),
    motivo: '',
  }
  dialogReneg.value = true
}

async function confirmarReneg() {
  salvando.value = true
  try {
    await api.post(`/contas-pagar/${reneg.value.id}/renegociar`, {
      novoValor: reneg.value.novoValor,
      novoVencimento: reneg.value.novoVencimento,
      motivo: reneg.value.motivo,
    })
    notif.ok('Renegociado com sucesso.')
    dialogReneg.value = false
    await carregar()
  } catch { notif.erro('Erro ao renegociar.') }
  finally { salvando.value = false }
}

async function cancelarTitulo(item: any) {
  if (!confirm(`Cancelar o título "${item.descricao}" (R$ ${fmt(item.saldo)})?`)) return
  try {
    await api.post(`/contas-pagar/${item.id}/cancelar`, {})
    notif.ok('Título cancelado.')
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao cancelar título.') }
}

// ── Cadastro rápido de fornecedor/beneficiário (sem sair da conta a pagar) ──
const dlgFornecedor = ref(false)
const salvandoForneced = ref(false)
const contextoForneced = ref<'nova' | 'editar'>('nova')
const formForneced = ref({ tipo: 'Fornecedor', razaoSocial: '', cnpj: '', telefone: '' })

function abrirNovoFornecedor(contexto: 'nova' | 'editar', nome = '') {
  contextoForneced.value = contexto
  formForneced.value = { tipo: 'Fornecedor', razaoSocial: nome, cnpj: '', telefone: '' }
  dlgFornecedor.value = true
}

async function salvarFornecedorRapido() {
  const nome = formForneced.value.razaoSocial.trim()
  if (!nome) return
  const colaborador = formForneced.value.tipo === 'Colaborador'
  // Aceita CPF (11 dígitos) ou CNPJ (14); em branco fica sem documento.
  const doc = formForneced.value.cnpj.replace(/[^\dA-Za-z]/g, '')
  if (doc && doc.length !== 11 && doc.length !== 14) {
    notif.aviso('CPF deve ter 11 dígitos ou CNPJ 14 caracteres.')
    return
  }

  salvandoForneced.value = true
  try {
    const tel = formForneced.value.telefone.trim() || null
    let novoId: string
    if (colaborador) {
      const r = await api.post('/contas-pagar/colaborador', {
        empresaId: auth.empresaId, nome, cpf: doc || null, telefone: tel,
      }, { _quiet: true } as any)
      novoId = r.data.id ?? r.data
    } else {
      const r = await api.post('/fornecedores', {
        empresaId: auth.empresaId, razaoSocial: nome, cnpj: doc || null, telefone: tel,
      }, { _quiet: true } as any)
      novoId = r.data.id ?? r.data
    }

    const novo = { id: novoId, nome, tipo: formForneced.value.tipo, documento: doc || null }
    fornecedores.value = [...fornecedores.value, novo].sort((a, b) => a.nome.localeCompare(b.nome))

    if (contextoForneced.value === 'nova') {
      form.value.fornecedorId = novo.id
      form.value._buscaForneced = ''
    } else {
      edicao.value.fornecedorId = novo.id
      edicao.value._buscaForneced = ''
    }
    dlgFornecedor.value = false
    notif.ok(`${colaborador ? 'Colaborador' : 'Fornecedor'} "${nome}" cadastrado e selecionado.`)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? e?.response?.data?.title ?? 'Erro ao cadastrar.')
  } finally { salvandoForneced.value = false }
}

// Beneficiários = fornecedores + colaboradores (funcionários). Cada item traz o
// tipo, para na hora de salvar mandar fornecedorId OU colaboradorId.
async function carregarBeneficiarios() {
  const r = await api.get('/contas-pagar/beneficiarios', { params: { empresaId: auth.empresaId } })
    .catch(() => ({ data: [] }))
  fornecedores.value = r.data ?? []
}

/** Dado o id selecionado no campo, devolve o par fornecedorId/colaboradorId. */
function beneficiarioPayload(id: string | null) {
  if (!id) return { fornecedorId: null, colaboradorId: null }
  const b = fornecedores.value.find((x: any) => x.id === id)
  return b?.tipo === 'Colaborador'
    ? { fornecedorId: null, colaboradorId: id }
    : { fornecedorId: id, colaboradorId: null }
}

const route = useRoute()

onMounted(async () => {
  // Vindo do Dashboard (calendário) com ?data=YYYY-MM-DD: filtra só aquele dia.
  const data = route.query.data
  if (typeof data === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(data)) {
    filtros.value.inicio = data
    filtros.value.fim = data
    filtros.value.tudo = false
    filtros.value.status = 'Todos'
    filtros.value.categoria = 'Todas'
  }
  // Vindo do sininho (?vencidas=1): mostra todas as vencidas, sem limitar ao mês.
  if (route.query.vencidas) {
    filtros.value.tudo = true
    filtros.value.status = 'Vencido'
    filtros.value.categoria = 'Todas'
  }
  await carregar()
  await carregarBeneficiarios()
})
</script>

<style scoped>
.filtro-acoes { gap: 12px; }
/* Evita o thumb do switch encostar no texto do label */
.filtro-acoes :deep(.v-switch .v-label) { padding-left: 8px; white-space: nowrap; opacity: .9; }
.filtro-acoes :deep(.v-selection-control) { min-height: 40px; }
</style>
