<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div class="text-h6 font-weight-bold flex-grow-1">Contas a Pagar</div>
      <v-btn color="teal" variant="tonal" rounded="lg" prepend-icon="mdi-account-cash-outline"
        class="mr-2" :loading="gerandoFolha" @click="gerarFolha">Prever folha</v-btn>
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
      </v-row>
      <div class="d-flex align-center justify-end mt-2 gap-3 flex-wrap">
        <v-btn color="warning" variant="tonal" rounded="lg" prepend-icon="mdi-calendar-today"
          :loading="carregando" @click="filtrarHoje">Hoje</v-btn>
        <v-switch v-model="filtros.tudo" color="primary" density="compact" hide-details
          label="Ver todas (ignora as datas)" @update:model-value="carregar" />
        <v-btn color="primary" variant="tonal" rounded="lg" prepend-icon="mdi-magnify"
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
      <v-data-table
        :headers="headers"
        :items="lancamentosFiltrados"
        :loading="carregando"
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
            {{ item.status }}
          </v-chip>
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
                <v-list-item prepend-icon="mdi-cancel" title="Cancelar título"
                  :disabled="item.status === 'Pago' || item.status === 'Cancelado'" @click="cancelarTitulo(item)" />
              </v-list>
            </v-menu>
          </template>
        </template>
      </v-data-table>
    </v-card>

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
              <v-col cols="6">
                <v-text-field v-model.number="form.quantas" label="Nº de parcelas"
                  type="number" min="2" max="360" variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="6">
                <v-select v-model="form.periodo" label="Intervalo"
                  :items="periodos" item-title="label" item-value="value"
                  variant="outlined" density="compact" hide-details />
              </v-col>
              <v-col cols="12">
                <v-alert type="info" variant="tonal" density="compact" class="text-caption">
                  {{ form.quantas || 1 }}x de R$ {{ fmtParcela }} — total R$ {{ fmt(form.valorOriginal || 0) }}
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
                  :items="periodos" item-title="label" item-value="value"
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
                  :items="periodos" item-title="label" item-value="value"
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
                  :items="periodos" item-title="label" item-value="value"
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
  </div>
</template>

<script setup lang="ts">
import FiltroMes from '@/components/FiltroMes.vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import { ref, computed, onMounted } from 'vue'
import { useDisplay } from 'vuetify'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const { mobile } = useDisplay()
const carregando = ref(false)
const salvando = ref(false)
const gerandoFolha = ref(false)
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
  modo: 'unico' as 'unico' | 'parcelar' | 'repetir', quantas: 2, periodo: 'mensal',
})
const reneg = ref({ id: '', saldo: 0, novoValor: 0, novoVencimento: '', motivo: '' })

const categorias = ['Despesas Administrativas', 'Despesas Operacionais', 'Despesas Variáveis', 'Pessoas', 'Impostos']

const periodos = [
  { label: 'Diário',      value: 'diario' },
  { label: 'Semanal',     value: 'semanal' },
  { label: 'Quinzenal',   value: 'quinzenal' },
  { label: 'Mensal',      value: 'mensal' },
  { label: 'Bimestral',   value: 'bimestral' },
  { label: 'Trimestral',  value: 'trimestral' },
  { label: 'Semestral',   value: 'semestral' },
  { label: 'Anual',       value: 'anual' },
]

const formPadrao = () => ({
  descricao: '', categoria: '', fornecedorId: null as string | null, _buscaForneced: '',
  valorOriginal: 0, dataVencimento: '', observacao: '',
  modo: 'unico' as 'unico' | 'parcelar' | 'repetir',
  quantas: 2,
  periodo: 'mensal',
})
const form = ref(formPadrao())

const fmtParcela = computed(() =>
  fmt(Math.round((form.value.valorOriginal || 0) / (form.value.quantas || 1) * 100) / 100)
)
const fmtTotalRepetir = computed(() =>
  fmt(Math.round((form.value.valorOriginal || 0) * (form.value.quantas || 1) * 100) / 100)
)

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
  tudo: false,
})

const hoje = () => new Date(new Date().toISOString().slice(0, 10) + 'T12:00:00')

const lancamentosFiltrados = computed(() => {
  let lista = lancamentos.value
  if (filtros.value.categoria !== 'Todas')
    lista = lista.filter(l => l.categoria === filtros.value.categoria)
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

onMounted(async () => {
  await carregar()
  await carregarBeneficiarios()
})
</script>
