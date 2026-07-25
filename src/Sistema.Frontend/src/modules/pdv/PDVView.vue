<template>
  <div class="pdv-shell">

    <!-- ══ BARRA SUPERIOR ══════════════════════════════════════════ -->
    <div class="pdv-topbar">
      <div class="pdv-topbar-left">
        <v-btn v-if="mobile" icon="mdi-menu" variant="text" size="small" color="white"
          class="pdv-menu-btn" title="Menu" @click="ui.alternarMenu()" />
        <div class="pdv-brand">
          <v-icon icon="mdi-point-of-sale" size="20" color="rgba(255,255,255,.7)" />
          <span class="pdv-brand-name">PDV</span>
        </div>
        <v-chip v-if="sessaoAtual" size="x-small" color="success" variant="flat">
          <v-icon start size="10">mdi-circle</v-icon>
          Caixa #{{ sessaoAtual.numero }}
        </v-chip>
        <v-chip v-else-if="verificandoCaixa" size="x-small" color="warning" variant="flat">
          Verificando...
        </v-chip>
        <v-chip v-else size="x-small" color="error" variant="flat"
          style="cursor:pointer" @click="dialogAbrirCaixa = true">
          Sem caixa aberto
        </v-chip>
      </div>
      <div class="pdv-topbar-center">
        <v-text-field
          ref="inputCodigo"
          v-model="codigoDigitado"
          placeholder="Código de barras, SKU ou nome do produto"
          prepend-inner-icon="mdi-barcode-scan"
          variant="outlined"
          density="compact"
          hide-details
          autofocus
          rounded="lg"
          class="pdv-search"
          enterkeyhint="search"
          inputmode="search"
          @keyup.enter="buscarProduto"
        >
          <template #append-inner>
            <v-btn icon size="small" variant="text" color="rgba(255,255,255,.75)"
              title="Buscar produto" @click="buscarProduto">
              <v-icon size="22">mdi-magnify</v-icon>
            </v-btn>
            <v-btn icon size="small" variant="text" color="rgba(255,255,255,.75)"
              title="Escanear com câmera" @click="abrirScanner">
              <v-icon size="22">mdi-camera</v-icon>
            </v-btn>
          </template>
        </v-text-field>
      </div>
      <div class="pdv-topbar-right">
        <div class="pdv-info-pill">
          <v-icon size="13">mdi-account-outline</v-icon>
          {{ nomeOperador }}
        </div>
        <div class="pdv-info-pill">
          <v-icon size="13">mdi-clock-outline</v-icon>
          {{ horaAtual }}
        </div>
        <v-btn v-if="!mobile" :icon="emTelaCheia ? 'mdi-fullscreen-exit' : 'mdi-fullscreen'"
          size="small" variant="text" color="rgba(255,255,255,.6)"
          :title="emTelaCheia ? 'Sair da tela cheia' : 'Tela cheia'" @click="alternarTelaCheia" />
        <v-btn icon="mdi-keyboard-outline" size="small" variant="text"
          color="rgba(255,255,255,.5)" @click="modalAtalhos = true" title="Atalhos (F1)" />
      </div>
    </div>

    <!-- ══ CORPO PRINCIPAL ══════════════════════════════════════════ -->
    <div class="pdv-body">

      <!-- ── COLUNA ESQUERDA: Itens ─────────────────────────── -->
      <div class="pdv-col-itens">

        <!-- Faixa de promoções ativas (banner rotativo automático) -->
        <transition name="pdv-promo-slide">
          <div v-if="promoAtiva && !promoDismissed" class="pdv-promo-bar">
            <v-icon size="20" class="pdv-promo-icon">mdi-bullhorn-variant</v-icon>
            <div class="pdv-promo-viewport">
              <transition name="pdv-promo-fade" mode="out-in">
                <span class="pdv-promo-texto" :key="promoIdx">
                  <strong>Informe ao cliente:</strong> {{ promoAtiva }}
                </span>
              </transition>
            </div>
            <div v-if="todasPromocoes.length > 1" class="pdv-promo-dots">
              <span v-for="(_, i) in todasPromocoes" :key="i"
                class="pdv-promo-dot" :class="{ ativo: i === promoIdx }" />
            </div>
            <button class="pdv-promo-dismiss" title="Dispensar" @click="fecharPromo">
              <v-icon size="14">mdi-close</v-icon>
            </button>
          </div>
        </transition>

        <!-- Cabeçalho das colunas -->
        <div class="pdv-col-header">
          <span class="pdv-ch-seq">#</span>
          <span class="pdv-ch-info">Produto</span>
          <span class="pdv-ch-qtd">Quantidade</span>
          <span class="pdv-ch-tot">Total</span>
          <span class="pdv-ch-del"></span>
        </div>

        <!-- Lista de itens -->
        <div class="pdv-itens-wrap">
          <!-- Estado vazio -->
          <transition name="fade">
            <div v-if="!itens.length" class="pdv-vazio">
              <div class="pdv-vazio-icon">
                <v-icon icon="mdi-barcode-scan" size="44" color="#94a3b8" />
              </div>
              <div class="pdv-vazio-titulo">Aguardando produto</div>
              <div class="pdv-vazio-sub">Digite o código ou escaneie o código de barras</div>
              <div class="pdv-hint-chips">
                <div class="pdv-hint-chip"><kbd>F1</kbd> Ajuda</div>
                <div class="pdv-hint-chip"><kbd>F10</kbd> Finalizar</div>
                <div class="pdv-hint-chip"><kbd>ESC</kbd> Cancelar</div>
              </div>
            </div>
          </transition>

          <!-- Itens da venda -->
          <div v-if="itens.length" class="pdv-itens-lista">
            <transition-group name="slide-item">
              <div
                v-for="(item, i) in itens"
                :key="item.produtoId + i"
                class="pdv-item"
                :class="{ 'pdv-item--selected': itemSelecionado === i }"
                @click="itemSelecionado = i"
              >
                <!-- Número sequencial -->
                <div class="pdv-item-seq">{{ i + 1 }}</div>

                <!-- Info do produto -->
                <div class="pdv-item-info">
                  <div class="pdv-item-nome">{{ item.descricao }}</div>
                  <div class="pdv-item-detalhe">
                    <span>{{ item.codigo }}</span>
                    <span class="sep">·</span>
                    <span>{{ item.unidade }}</span>
                    <span class="sep">·</span>
                    <span>R$ {{ fmt(item.precoUnitario) }}</span>
                    <span v-if="item.desconto > 0" style="color:#fbbf24;margin-left:4px">
                      − R$ {{ fmt(item.desconto) }}
                    </span>
                  </div>
                </div>

                <!-- Qtd inline -->
                <div class="pdv-item-qtd">
                  <button class="pdv-qtd-btn" @click.stop="ajustarQtd(i, -1)">−</button>
                  <input
                    v-model.number="item.quantidade"
                    class="pdv-qtd-input"
                    type="number"
                    :min="item.porPeso ? 0.001 : 1"
                    :step="item.porPeso ? 0.1 : 1"
                    :title="item.porPeso ? 'Quantidade em KG' : ''"
                    @change="recalcular(i)"
                    @click.stop
                  />
                  <button class="pdv-qtd-btn" @click.stop="ajustarQtd(i, 1)">+</button>
                </div>

                <!-- Total do item -->
                <div class="pdv-item-total">R$ {{ fmt(item.total) }}</div>

                <!-- Desconto no item -->
                <button
                  class="pdv-item-desc"
                  :class="{ ativo: item.desconto > 0 }"
                  title="Desconto neste item"
                  @click.stop="abrirDescontoItem(i)"
                >
                  <v-icon size="16">mdi-tag-minus-outline</v-icon>
                </button>

                <!-- Remover -->
                <button class="pdv-item-del" title="Remover (Delete)" @click.stop="removerItem(i)">
                  <v-icon size="16">mdi-close</v-icon>
                </button>
              </div>
            </transition-group>
          </div>
        </div>

        <!-- Rodapé com totais -->
        <div class="pdv-totais">
          <div class="pdv-totais-linha">
            <span>{{ totalItens }} {{ totalItens === 1 ? 'item' : 'itens' }}</span>
            <span>Subtotal: R$ {{ fmt(subtotal) }}</span>
          </div>
          <div v-if="desconto > 0" class="pdv-totais-linha" style="color:#f59e0b">
            <span>Desconto aplicado</span>
            <span>− R$ {{ fmt(desconto) }}</span>
          </div>
          <div class="pdv-totais-total">
            <span class="pdv-total-label">Total a pagar</span>
            <span class="pdv-total-valor">R$ {{ fmt(total) }}</span>
          </div>
        </div>
      </div>

      <!-- ── COLUNA DIREITA: Pagamento ──────────────────────── -->
      <div class="pdv-col-pag">

        <!-- Cliente / CPF -->
        <div class="pdv-pag-section">
          <div class="pdv-pag-label">
            <v-icon size="14" class="mr-1">mdi-account-outline</v-icon>
            Cliente <kbd class="pdv-kbd">F3</kbd>
          </div>
          <v-autocomplete
            ref="inputCliente"
            v-model="clienteId"
            v-model:search="buscaCliente"
            :items="clientes"
            item-title="nome"
            item-value="id"
            variant="outlined"
            density="compact"
            hide-details
            clearable
            placeholder="Buscar cliente..."
            class="mb-2"
            :loading="buscandoCliente"
            @update:search="pesquisarCliente"
          >
            <template #no-data>
              <v-list-item v-if="buscaCliente && buscaCliente.trim().length >= 2"
                :title="'Cadastrar cliente: ' + buscaCliente.trim()"
                prepend-icon="mdi-account-plus-outline"
                @click="cadastrarClienteRapido(buscaCliente.trim())" />
              <v-list-item v-else title="Digite o nome para buscar ou cadastrar" disabled />
            </template>
          </v-autocomplete>
          <div class="d-flex align-center ga-2">
            <v-btn-toggle v-model="tipoDocConsumidor" density="compact" mandatory variant="outlined" rounded="lg">
              <v-btn value="cpf" size="small">CPF</v-btn>
              <v-btn value="cnpj" size="small">CNPJ</v-btn>
            </v-btn-toggle>
            <v-text-field
              ref="inputCpf"
              v-model="cpfConsumidor"
              :placeholder="tipoDocConsumidor === 'cpf' ? '000.000.000-00' : '00.000.000/0000-00'"
              variant="outlined"
              density="compact"
              hide-details
              clearable
              :maxlength="tipoDocConsumidor === 'cpf' ? 14 : 18"
              class="flex-grow-1"
              @input="onInputDoc"
            />
          </div>
        </div>

        <!-- Desconto -->
        <div class="pdv-pag-section">
          <div class="pdv-pag-label">
            <v-icon size="14" class="mr-1">mdi-tag-outline</v-icon>
            Desconto <kbd class="pdv-kbd">F2</kbd>
          </div>
          <div class="d-flex ga-2">
            <v-text-field
              ref="inputDesconto"
              v-model.number="descontoReais"
              prefix="R$"
              type="number"
              variant="outlined"
              density="compact"
              hide-details
              placeholder="0,00"
              @update:model-value="sincrDesconto('reais')"
            />
            <v-text-field
              v-model.number="descontoPct"
              suffix="%"
              type="number"
              variant="outlined"
              density="compact"
              hide-details
              placeholder="0"
              style="max-width:100px"
              @update:model-value="sincrDesconto('pct')"
            />
          </div>
        </div>

        <!-- Formas de pagamento -->
        <div class="pdv-pag-section">
          <div class="pdv-pag-label">
            <v-icon size="14" class="mr-1">mdi-cash-multiple</v-icon>
            Pagamento
          </div>
          <div class="pdv-fps-grid">
            <button
              v-for="fp in formasPagamento"
              :key="fp.tipo"
              class="pdv-fp-btn"
              :class="{ 'pdv-fp-btn--ativo': pagamentos.some(p => p.tipo === fp.tipo) }"
              :style="{ '--fp-cor': fp.cor }"
              @click="selecionarFormaPagamento(fp.tipo)"
            >
              <v-icon :icon="fp.icon" size="20" :color="fp.cor" />
              <span>{{ fp.label }}</span>
              <kbd v-if="fp.key" class="pdv-kbd-fp">{{ fp.key }}</kbd>
            </button>
          </div>

          <!-- Valores por forma -->
          <div v-if="pagamentos.length" class="mt-3">
            <div v-for="(pag, i) in pagamentos" :key="i" class="d-flex align-center ga-2 mb-2">
              <div class="pdv-pag-forma-label">
                <v-icon :icon="formasPagamento.find(f => f.tipo === pag.tipo)?.icon" size="16" class="mr-1" />
                {{ pag.tipo }}
                <span v-if="pag.operadoraNome" class="text-caption text-medium-emphasis">
                  · {{ pag.operadoraNome }}<span v-if="(pag.parcelas ?? 1) > 1"> {{ pag.parcelas }}x</span>
                </span>
                <span v-else-if="pag.crediarioParcelas" class="text-caption text-medium-emphasis">
                  · {{ pag.crediarioParcelas }}x<span v-if="(pag.crediarioEntrada ?? 0) > 0"> (entrada R$ {{ fmt(pag.crediarioEntrada) }})</span>
                </span>
              </div>
              <v-text-field
                v-model.number="pag.valor"
                prefix="R$"
                type="number"
                variant="outlined"
                density="compact"
                hide-details
                class="flex-grow-1"
              />
              <button class="pdv-del-pag" @click="pagamentos.splice(i, 1)">
                <v-icon size="16">mdi-close</v-icon>
              </button>
            </div>

            <!-- Botão dinheiro exato -->
            <v-btn v-if="pagamentos.some(p => p.tipo === 'Dinheiro')"
              size="small" variant="tonal" color="success" block class="mt-1"
              prepend-icon="mdi-cash-check"
              @click="dinheiroExato">
              Dinheiro Exato <kbd class="ml-2 pdv-kbd">F5</kbd>
            </v-btn>
          </div>
        </div>

        <!-- Status do pagamento -->
        <div class="pdv-pag-status" :class="statusPagamentoClass">
          <div v-if="!pagamentos.length" class="pdv-status-hint">
            Selecione a forma de pagamento
          </div>
          <template v-else>
            <div v-if="troco > 0" class="pdv-status-troco">
              <v-icon icon="mdi-cash-refund" size="18" class="mr-1" />
              Troco: <strong>R$ {{ fmt(troco) }}</strong>
            </div>
            <div v-else-if="totalPago >= total && total > 0" class="pdv-status-ok">
              <v-icon icon="mdi-check-circle-outline" size="18" class="mr-1" />
              Pagamento confirmado
            </div>
            <div v-else-if="totalPago > 0 && totalPago < total" class="pdv-status-falta">
              <v-icon icon="mdi-alert-circle-outline" size="18" class="mr-1" />
              Faltam: <strong>R$ {{ fmt(total - totalPago) }}</strong>
            </div>
          </template>
        </div>

        <!-- Separador -->
        <div class="pdv-sep"></div>

        <!-- Colaborador -->
        <div class="pdv-pag-section">
          <div class="pdv-pag-label">
            <v-icon size="14" class="mr-1">mdi-account-tie-outline</v-icon>
            Colaborador
          </div>
          <v-select
            v-model="colaboradorId"
            :items="colaboradores"
            item-title="nome"
            item-value="id"
            variant="outlined"
            density="compact"
            hide-details
          />
        </div>

        <!-- Botões de ação -->
        <div class="pdv-acoes">
          <button
            class="pdv-btn-finalizar"
            :class="{ 'pdv-btn-finalizar--ok': podeFinalizarVenda }"
            :disabled="!podeFinalizarVenda || finalizando"
            @click="finalizar"
          >
            <v-icon icon="mdi-check-circle" size="24" class="mr-2" />
            <span>{{ finalizando ? 'Finalizando...' : 'Finalizar Venda' }}</span>
            <kbd class="pdv-kbd-big">F10</kbd>
          </button>

          <button class="pdv-btn-cancelar" :disabled="!itens.length" @click="cancelar">
            <v-icon icon="mdi-close-circle-outline" size="18" class="mr-1" />
            Cancelar Venda
            <kbd class="pdv-kbd">ESC</kbd>
          </button>
        </div>

      </div>
    </div>

    <!-- ══ DIALOG SCANNER CÂMERA ══════════════════════════════════ -->
    <!-- Dialog: pagamento com cartão (operadora + parcelas + taxa) -->
    <v-dialog v-model="dialogCartao" max-width="440">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="primary">mdi-credit-card-outline</v-icon>
          Cartão de {{ cartaoTipo }}
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <div class="text-body-2 font-weight-medium mb-2">Bandeira do cartão</div>
          <div v-if="opcoesCartao.length" class="d-flex flex-wrap gap-2 mb-2">
            <v-chip v-for="op in opcoesCartao" :key="op.label" size="large" label
              :color="cartaoBandeiraSel?.label === op.label ? 'primary' : undefined"
              :variant="cartaoBandeiraSel?.label === op.label ? 'flat' : 'outlined'"
              @click="cartaoBandeiraSel = op">
              {{ op.label }}
            </v-chip>
          </div>
          <v-alert v-else type="warning" variant="tonal" density="compact" class="mb-2">
            Nenhuma bandeira/maquininha cadastrada em Financeiro → Operadoras de Cartão.
          </v-alert>

          <v-select v-if="cartaoTipo === 'Crédito'" v-model.number="cartaoParcelas"
            :items="[1,2,3,4,5,6,7,8,9,10,11,12]" label="Parcelas"
            variant="outlined" density="compact" hide-details class="mt-1">
            <template #selection="{ item }">{{ item.value }}x</template>
            <template #item="{ item, props }">
              <v-list-item v-bind="props" :title="`${item.value}x`" />
            </template>
          </v-select>

          <v-alert type="info" variant="tonal" density="compact" class="mt-3">
            <div class="d-flex justify-space-between">
              <span>Valor a cobrar</span><b>R$ {{ fmt(cartaoValor) }}</b>
            </div>
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogCartao = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :disabled="!cartaoBandeiraSel" @click="confirmarCartao">
            Adicionar pagamento
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: crediário (parcelas + entrada; exige cliente) -->
    <v-dialog v-model="dialogCrediario" max-width="440">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="deep-purple">mdi-account-credit-card-outline</v-icon>
          Crediário
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <v-alert v-if="clienteSelecionadoNome" type="info" variant="tonal" density="compact" class="mb-3">
            Cliente: <b>{{ clienteSelecionadoNome }}</b>
          </v-alert>
          <v-row dense>
            <v-col cols="6">
              <v-text-field v-model.number="crediarioParcelas" label="Nº de parcelas *"
                type="number" min="1" max="60" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="6">
              <v-text-field v-model.number="crediarioEntrada" label="Entrada (R$)"
                type="number" min="0" prefix="R$" variant="outlined" density="compact" hide-details />
            </v-col>
          </v-row>
          <v-alert type="info" variant="tonal" density="compact" class="mt-3">
            <div class="d-flex justify-space-between"><span>Valor financiado</span><b>R$ {{ fmt(crediarioValor) }}</b></div>
            <div class="d-flex justify-space-between">
              <span>{{ crediarioParcelas }}x de</span>
              <b>R$ {{ fmt(crediarioValorParcela) }}</b>
            </div>
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogCrediario = false">Cancelar</v-btn>
          <v-btn color="deep-purple" rounded="lg"
            :disabled="!(crediarioParcelas >= 1) || crediarioEntrada >= crediarioValorBase"
            @click="confirmarCrediario">
            Adicionar pagamento
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: venda por peso (gramas) -->
    <v-dialog v-model="dialogPeso" max-width="420">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center gap-2 text-body-1 font-weight-bold">
          <v-icon color="teal">mdi-scale</v-icon>Produto por peso
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <div class="text-body-2 font-weight-medium mb-1">{{ produtoPeso?.descricao }}</div>
          <div class="text-caption text-medium-emphasis mb-3">
            R$ {{ fmt(produtoPeso?.precoVenda ?? 0) }} / KG
            · R$ {{ fmt(precoPor100g) }} / 100g
          </div>
          <v-text-field ref="inputGramas" v-model.number="gramas" label="Peso (gramas) *"
            type="number" min="0" suffix="g" variant="outlined" density="compact"
            autofocus hide-details @keyup.enter="confirmarPeso" />
          <div class="d-flex flex-wrap gap-1 mt-2">
            <v-chip v-for="g in [100, 200, 250, 500, 1000]" :key="g" size="small"
              variant="tonal" color="teal" @click="gramas = g">{{ g }}g</v-chip>
          </div>
          <v-alert v-if="gramas > 0" type="info" variant="tonal" density="compact" class="mt-3">
            {{ (gramas / 1000).toLocaleString('pt-BR', { maximumFractionDigits: 3 }) }} KG ·
            Total: <b>R$ {{ fmt(totalPeso) }}</b>
          </v-alert>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogPeso = false">Cancelar</v-btn>
          <v-btn color="teal" rounded="lg" :disabled="!(gramas > 0)" @click="confirmarPeso">
            Adicionar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="dialogScanner" max-width="420" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center">
          <v-icon start color="primary">mdi-barcode-scan</v-icon>
          Escanear código de barras
          <v-spacer />
          <v-btn icon size="small" variant="text" @click="fecharScanner">
            <v-icon>mdi-close</v-icon>
          </v-btn>
        </v-card-title>
        <v-card-text class="pa-0">
          <!-- Viewfinder da câmera -->
          <div class="pdv-scanner-wrap">
            <video ref="videoScanner" class="pdv-scanner-video" autoplay playsinline muted />
            <div class="pdv-scanner-overlay">
              <div class="pdv-scanner-frame" />
            </div>
            <div v-if="scannerStatus" class="pdv-scanner-status">{{ scannerStatus }}</div>
          </div>

          <!-- Fallback: captura por foto (iOS Safari) -->
          <div v-if="usarFotoFallback" class="pa-4 text-center">
            <v-icon size="40" color="primary" class="mb-2">mdi-camera</v-icon>
            <div class="text-body-2 text-medium-emphasis mb-4">
              Tire uma foto do código de barras
            </div>
            <input
              ref="inputFoto"
              type="file"
              accept="image/*"
              capture="environment"
              style="display:none"
              @change="lerFoto"
            />
            <v-btn color="primary" prepend-icon="mdi-camera" @click="inputFoto?.click()">
              Abrir Câmera
            </v-btn>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4 pt-2">
          <v-btn variant="text" prepend-icon="mdi-keyboard" @click="fecharScanner">
            Digitar manualmente
          </v-btn>
          <v-spacer />
          <v-select
            v-if="cameras.length > 1"
            v-model="cameraAtual"
            :items="cameras"
            item-title="label"
            item-value="deviceId"
            label="Câmera"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width:160px"
            @update:model-value="trocarCamera"
          />
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ══ MODAL ATALHOS ══════════════════════════════════════════ -->
    <v-dialog v-model="modalAtalhos" max-width="500">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2 d-flex align-center">
          <v-icon icon="mdi-keyboard-outline" class="mr-2" />
          Ajuda do PDV
        </v-card-title>
        <v-card-text class="pa-4 pt-2">
          <div class="text-subtitle-2 font-weight-bold mb-2">
            <v-icon size="16" color="primary" class="mr-1">mdi-map-marker-path</v-icon>
            Como operar uma venda
          </div>
          <ol class="pdv-passos mb-4">
            <li>Abra o caixa informando o <b>local de estoque</b> e o fundo de troco.</li>
            <li>Escaneie ou digite o <b>código/nome</b> do produto e tecle <kbd>Enter</kbd> para adicionar.</li>
            <li>Ajuste as <b>quantidades</b> pelos botões +/− ou digitando; aplique <b>desconto</b> (<kbd>F2</kbd>) se necessário.</li>
            <li>Opcional: informe o <b>cliente</b> (<kbd>F3</kbd>) e o <b>CPF/CNPJ</b> na nota (<kbd>F9</kbd>).</li>
            <li>Escolha a <b>forma de pagamento</b> e confira o troco; use <kbd>F5</kbd> para dinheiro exato.</li>
            <li>Clique em <b>Finalizar Venda</b> (<kbd>F10</kbd>). O comprovante e o QR Code da NFC-e aparecem ao final.</li>
          </ol>
          <div class="text-subtitle-2 font-weight-bold mb-2">Atalhos de teclado</div>
          <div class="atalhos-grid">
            <div v-for="a in atalhos" :key="a.key" class="atalho-linha">
              <kbd class="atalho-key">{{ a.key }}</kbd>
              <span class="text-body-2">{{ a.descricao }}</span>
            </div>
          </div>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn color="primary" variant="tonal" @click="modalAtalhos = false">Fechar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- ══ MODAL BUSCA DE PRODUTO ════════════════════════════════ -->
    <v-dialog v-model="dialogProdutos" max-width="580">
      <v-card rounded="xl">
        <v-card-title class="pa-4">
          <v-icon icon="mdi-magnify" class="mr-2" />
          Selecionar Produto
        </v-card-title>
        <v-card-text class="pa-0">
          <v-list density="compact">
            <v-list-item
              v-for="(p, i) in produtosBusca"
              :key="p.id"
              class="px-4 py-2"
              rounded
              @click="adicionarProduto(p)"
            >
              <template #prepend>
                <div class="pdv-busca-num mr-3">{{ i + 1 }}</div>
              </template>
              <template #title>
                <span class="text-body-2 font-weight-medium">{{ p.descricao }}</span>
              </template>
              <template #subtitle>
                <span>{{ p.codigo }}</span>
                <span class="mx-1 opacity-40">·</span>
                <span>{{ p.unidadeSigla }}</span>
              </template>
              <template #append>
                <span class="text-body-2 font-weight-bold text-success">
                  R$ {{ fmt(p.precoVenda) }}
                </span>
              </template>
            </v-list-item>
          </v-list>
        </v-card-text>
      </v-card>
    </v-dialog>

    <!-- ══ DIALOG DESCONTO POR ITEM ══════════════════════════════ -->
    <v-dialog v-model="dialogDescontoItem" max-width="400">
      <v-card rounded="xl" class="pa-4">
        <div class="text-subtitle-1 font-weight-bold mb-1">Desconto no Item</div>
        <div class="text-body-2 text-medium-emphasis">
          {{ itemSelecionado !== null ? itens[itemSelecionado]?.descricao : '' }}
        </div>
        <div class="text-caption text-medium-emphasis mb-3">
          Subtotal do item: R$ {{ fmt(baseDescontoItem) }}
        </div>
        <div class="d-flex ga-2">
          <v-text-field v-model.number="descontoItemTemp" label="Desconto R$" prefix="R$"
            type="number" variant="outlined" density="compact" autofocus
            @update:model-value="sincrDescontoItem('reais')" />
          <v-text-field v-model.number="descontoItemPctTemp" label="Desconto %" suffix="%"
            type="number" variant="outlined" density="compact"
            @update:model-value="sincrDescontoItem('pct')" />
        </div>
        <div class="d-flex justify-end ga-2 mt-2">
          <v-btn variant="text" @click="dialogDescontoItem = false">Cancelar</v-btn>
          <v-btn color="primary" @click="aplicarDescontoItem">Aplicar</v-btn>
        </div>
      </v-card>
    </v-dialog>

    <!-- ══ DIALOG COMPROVANTE ════════════════════════════════════ -->
    <v-dialog v-model="dialogComprovante" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-text class="pa-5">
          <div class="text-center mb-4">
            <div class="pdv-check-circle mb-2">
              <v-icon icon="mdi-check" size="36" color="white" />
            </div>
            <div class="text-h5 font-weight-bold mt-3">Venda Finalizada!</div>
            <div class="text-body-2 text-medium-emphasis mt-1">Cupom Nº {{ ultimaVendaNumero }}</div>
          </div>

          <!-- Valores -->
          <div class="pdv-comprovante-valores mb-4">
            <div class="pdv-cv-linha">
              <span>Total da venda</span>
              <strong class="text-success">R$ {{ fmt(ultimaVendaTotal) }}</strong>
            </div>
            <div v-if="ultimaVendaTroco > 0" class="pdv-cv-linha">
              <span>Troco</span>
              <strong>R$ {{ fmt(ultimaVendaTroco) }}</strong>
            </div>
          </div>

          <!-- QR Code NFC-e -->
          <div v-if="ultimaVendaQrCode" class="text-center mb-4">
            <div class="text-caption text-medium-emphasis mb-2">
              <v-icon size="14" class="mr-1">mdi-qrcode</v-icon>
              NFC-e — consulte sua nota fiscal
            </div>
            <div class="d-inline-block pa-2 rounded-xl" style="background:#fff">
              <canvas ref="canvasQr" width="160" height="160" />
            </div>
            <div class="text-caption text-medium-emphasis mt-1 font-mono">
              {{ ultimaVendaChave?.substring(0, 22) }}…
            </div>
          </div>

          <v-alert v-else type="info" variant="tonal" density="compact" class="mb-4" rounded="lg">
            NFC-e não emitida — certificado A1 não configurado.
            <router-link to="/configuracoes" class="ml-1">Configurar</router-link>
          </v-alert>

          <v-row dense>
            <v-col cols="6">
              <v-btn block color="primary" variant="outlined" rounded="lg"
                prepend-icon="mdi-printer" @click="imprimirComprovante">
                Imprimir
              </v-btn>
            </v-col>
            <v-col cols="6">
              <v-btn block color="success" rounded="lg"
                prepend-icon="mdi-plus" @click="novaVenda">
                Nova Venda
              </v-btn>
            </v-col>
          </v-row>
        </v-card-text>
      </v-card>
    </v-dialog>

    <!-- ══ DIALOG: ABRIR CAIXA ══════════════════════════════════════ -->
    <v-dialog v-model="dialogAbrirCaixa" max-width="420" persistent :scrim="true">
      <v-card rounded="xl">
        <v-card-text class="pa-6 text-center">
          <v-avatar color="warning" size="64" class="mb-4">
            <v-icon icon="mdi-cash-register" size="36" color="white" />
          </v-avatar>
          <div class="text-h6 font-weight-bold mb-1">Nenhum caixa aberto</div>
          <div class="text-body-2 text-medium-emphasis mb-6">
            Informe o saldo inicial em dinheiro para abrir o caixa de hoje.
          </div>

          <v-select v-if="locaisCaixa.length"
            v-model="localEstoqueIdCaixa" :items="locaisCaixa"
            item-title="nome" item-value="id"
            label="Local de Estoque *"
            variant="outlined" density="comfortable" class="mb-3"
          />
          <v-text-field
            v-model.number="saldoInicial"
            label="Fundo de troco inicial (R$)"
            type="number"
            prefix="R$"
            variant="outlined"
            density="comfortable"
            autofocus
            class="mb-2"
            @keyup.enter="abrirCaixa"
          />

          <div class="text-caption text-medium-emphasis mb-4">
            <v-icon size="14" class="mr-1">mdi-information-outline</v-icon>
            Operador: <strong>{{ nomeOperador }}</strong> ·
            {{ new Date().toLocaleDateString('pt-BR', { weekday:'long', day:'numeric', month:'long' }) }}
          </div>

          <v-btn block color="primary" size="large" rounded="lg"
            :loading="abrindoCaixa" @click="abrirCaixa">
            <v-icon start>mdi-lock-open-variant-outline</v-icon>
            Abrir Caixa
          </v-btn>
          <v-btn block variant="text" class="mt-2" to="/pdv/sessoes">
            Ver histórico de sessões
          </v-btn>
        </v-card-text>
      </v-card>
    </v-dialog>

  </div>
</template>

<script setup lang="ts">
// PDV — layout profissional v2
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useDisplay } from 'vuetify'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import { useUiStore } from '@/stores/ui'
import QRCode from 'qrcode'
import { formatarCpf, maskCnpj, cpfRaw, cnpjRaw } from '@/utils/documento'
import { BrowserMultiFormatReader } from '@zxing/browser'

const auth = useAuthStore()
const notif = useNotifStore()
const ui = useUiStore()
const { mobile } = useDisplay()

// ── Tela cheia (desktop) ──────────────────────────────────────────
const emTelaCheia = ref(false)
function alternarTelaCheia() {
  if (!document.fullscreenElement) document.documentElement.requestFullscreen?.()
  else document.exitFullscreen?.()
}
function onFullscreenChange() { emTelaCheia.value = !!document.fullscreenElement }

// ── Tipos ────────────────────────────────────────────────────────
interface ItemVenda {
  produtoId: string; descricao: string; codigo: string
  precoUnitario: number; quantidade: number; total: number
  unidade: string; desconto: number; porPeso?: boolean
}
interface Pagamento {
  tipo: string; valor: number
  operadoraId?: string | null; parcelas?: number
  taxa?: number; liquido?: number; operadoraNome?: string
  crediarioParcelas?: number; crediarioEntrada?: number
}
interface Operadora {
  id: string; nome: string; cor?: string; bandeiras?: string[]
  taxaDebito: number
  taxaCreditoVista: number; taxaCreditoParcelado: number
  prazoDebito: number; prazoCreditoVista: number; prazoCreditoParcelado: number
}
interface OpcaoCartao { label: string; operadoraId: string; cor?: string }
interface Produto {
  id: string; descricao: string; codigo: string; precoVenda: number
  unidadeSigla: string; vendidoFracionado?: boolean
}
interface Cliente { id: string; nome: string }
interface Colaborador { id: string; nome: string; perfil: string }

// ── Refs ─────────────────────────────────────────────────────────
const inputCodigo = ref()
const inputCliente = ref()
const inputGramas = ref()
const inputDesconto = ref()
const inputCpf = ref()
const canvasQr = ref<HTMLCanvasElement | null>(null)

const codigoDigitado = ref('')
const itens = ref<ItemVenda[]>([])
const itemSelecionado = ref<number | null>(null)
const desconto = ref(0)
const descontoReais = ref(0)
const descontoPct = ref(0)
const pagamentos = ref<Pagamento[]>([])
const clienteId = ref<string | null>(null)

// ── Cartão: operadoras + diálogo de seleção ───────────────────────
const operadoras = ref<Operadora[]>([])
const dialogCartao = ref(false)
const cartaoTipo = ref<'Crédito' | 'Débito'>('Crédito')
const cartaoBandeiraSel = ref<OpcaoCartao | null>(null)
const cartaoParcelas = ref(1)

// Bandeiras que o caixa escolhe. Cada bandeira já pertence a uma maquininha
// (operadora) cadastrada — usamos a taxa dela por trás, sem exibir ao caixa.
// Se a mesma bandeira estiver em duas maquininhas, vale a primeira (ordem por nome).
const opcoesCartao = computed<OpcaoCartao[]>(() => {
  const comBandeira = operadoras.value.flatMap(o =>
    (o.bandeiras ?? []).map(b => ({ label: b, operadoraId: o.id, cor: o.cor })))
  if (comBandeira.length) {
    const vistos = new Set<string>()
    return comBandeira.filter(x => (vistos.has(x.label) ? false : vistos.add(x.label)))
  }
  // Sem bandeiras cadastradas: cai para as próprias maquininhas.
  return operadoras.value.map(o => ({ label: o.nome, operadoraId: o.id, cor: o.cor }))
})
const buscaCliente = ref('')
const clientes = ref<Cliente[]>([])
const buscandoCliente = ref(false)
const dialogProdutos = ref(false)
const dialogDescontoItem = ref(false)
const descontoItemTemp = ref(0)
const descontoItemPctTemp = ref(0)
const produtosBusca = ref<Produto[]>([])
const dialogComprovante = ref(false)
const modalAtalhos = ref(false)
const finalizando = ref(false)
const colaboradores = ref<Colaborador[]>([])
const colaboradorId = ref('')
const cpfConsumidor = ref('')
const tipoDocConsumidor = ref<'cpf' | 'cnpj'>('cpf')

// Comprovante
const ultimaVendaNumero = ref('')
const ultimaVendaId = ref('')
const ultimaVendaTotal = ref(0)
const ultimaVendaTroco = ref(0)
const ultimaVendaQrCode = ref<string | null>(null)
const ultimaVendaChave = ref<string | null>(null)

// ── Sessão de caixa ──────────────────────────────────────────────
const sessaoAtual = ref<{ id: string; numero: number; abertoEm: string; localEstoqueId?: string | null } | null>(null)
const dialogAbrirCaixa = ref(false)
const abrindoCaixa = ref(false)
const saldoInicial = ref<number>(0)
const verificandoCaixa = ref(true)

const locaisCaixa = ref<any[]>([])
const localEstoqueIdCaixa = ref<string | null>(null)

async function verificarSessaoCaixa() {
  verificandoCaixa.value = true
  try {
    const r = await api.get('/pdv/sessoes/aberta', { params: { empresaId: auth.empresaId, usuarioId: auth.usuario?.id } })
    if (r.data?.id) {
      sessaoAtual.value = r.data
    } else {
      // Carrega locais de estoque e abre o dialog
      const loc = await api.get('/locais-estoque', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] }))
      locaisCaixa.value = loc.data ?? []
      dialogAbrirCaixa.value = true
    }
  } catch {
    sessaoAtual.value = { id: 'dev', numero: 1, abertoEm: new Date().toISOString() }
  } finally {
    verificandoCaixa.value = false
  }
}

async function abrirCaixa() {
  if (!localEstoqueIdCaixa.value && locaisCaixa.value.length) {
    notif.aviso('Selecione o local de estoque.')
    return
  }
  abrindoCaixa.value = true
  try {
    const r = await api.post('/pdv/sessoes/abrir', {
      empresaId: auth.empresaId,
      usuarioId: auth.usuario?.id,
      localEstoqueId: localEstoqueIdCaixa.value,
      saldoAbertura: saldoInicial.value,
    })
    // O endpoint 'abrir' retorna apenas { id }; completa os dados da sessão localmente.
    sessaoAtual.value = {
      id: r.data.id,
      numero: r.data.numero ?? 1,
      abertoEm: new Date().toISOString(),
      localEstoqueId: localEstoqueIdCaixa.value,
    }
    dialogAbrirCaixa.value = false
    notif.ok('Caixa aberto!')
  } catch {
    sessaoAtual.value = { id: 'dev', numero: 1, abertoEm: new Date().toISOString() }
    dialogAbrirCaixa.value = false
    notif.ok('Caixa aberto!')
  } finally {
    abrindoCaixa.value = false
  }
}

// Hora
const horaAtual = ref('')
let clockTimer: ReturnType<typeof setInterval>
function atualizarHora() {
  horaAtual.value = new Date().toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
}

const nomeOperador = computed(() => auth.usuario?.nome?.split(' ')[0] ?? 'Operador')

// ── Computed ──────────────────────────────────────────────────────
const totalItens = computed(() => itens.value.reduce((s, i) => s + i.quantidade, 0))
const subtotal = computed(() => itens.value.reduce((s, i) => s + i.total, 0))
const total = computed(() => Math.max(0, subtotal.value - desconto.value))
const totalPago = computed(() => pagamentos.value.reduce((s, p) => s + (p.valor ?? 0), 0))
const troco = computed(() => Math.max(0, totalPago.value - total.value))
const podeFinalizarVenda = computed(() =>
  itens.value.length > 0 && totalPago.value >= total.value && total.value > 0)

const statusPagamentoClass = computed(() => {
  if (!pagamentos.value.length) return 'pdv-status--neutro'
  if (troco.value > 0) return 'pdv-status--troco'
  if (totalPago.value >= total.value && total.value > 0) return 'pdv-status--ok'
  return 'pdv-status--falta'
})

// ── Formas de pagamento ───────────────────────────────────────────
const formasPagamento = [
  { tipo: 'Dinheiro',  label: 'Dinheiro',  icon: 'mdi-cash',                     key: 'F4', cor: '#2e7d32' },
  { tipo: 'Pix',       label: 'Pix',        icon: 'mdi-qrcode',                   key: 'F6', cor: '#00897b' },
  { tipo: 'Crédito',   label: 'Crédito',    icon: 'mdi-credit-card-outline',      key: 'F7', cor: '#3949ab' },
  { tipo: 'Débito',    label: 'Débito',     icon: 'mdi-credit-card-chip-outline', key: 'F8', cor: '#1e88e5' },
  { tipo: 'Crediário', label: 'Crediário / Recebido', icon: 'mdi-account-credit-card-outline', key: '', cor: '#8e24aa' },
]

// De-para: rótulo exibido no PDV → valor do enum FormaPagamento no backend
const mapaFormaPagamento: Record<string, string> = {
  'Dinheiro': 'Dinheiro',
  'Pix': 'Pix',
  'Crédito': 'CartaoCredito',
  'Débito': 'CartaoDebito',
  'Crediário': 'Crediario',
}

// ── Atalhos ───────────────────────────────────────────────────────
const atalhos = [
  { key: 'Enter',  descricao: 'Buscar produto pelo código' },
  { key: 'F1',     descricao: 'Abrir/fechar lista de atalhos' },
  { key: 'F2',     descricao: 'Focar no campo de desconto' },
  { key: 'F3',     descricao: 'Focar na busca de cliente' },
  { key: 'F4',     descricao: 'Adicionar/remover pagamento Dinheiro' },
  { key: 'F5',     descricao: 'Dinheiro exato (preenche o total)' },
  { key: 'F6',     descricao: 'Adicionar/remover pagamento Pix' },
  { key: 'F7',     descricao: 'Adicionar/remover pagamento Crédito' },
  { key: 'F8',     descricao: 'Adicionar/remover pagamento Débito' },
  { key: 'F9',     descricao: 'Focar no campo CPF/CNPJ do consumidor' },
  { key: 'F10',    descricao: 'Finalizar venda' },
  { key: '↑ / ↓',  descricao: 'Navegar entre itens da venda' },
  { key: 'Delete', descricao: 'Remover item selecionado' },
  { key: '+ / −',  descricao: 'Aumentar / diminuir quantidade do item selecionado' },
  { key: 'ESC',    descricao: 'Cancelar venda / fechar diálogo' },
]

// ── Utilitários ───────────────────────────────────────────────────
function fmt(v: number) {
  return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function recalcular(i: number) {
  const item = itens.value[i]
  // Por peso: quantidade em KG (aceita decimais). Unidade: mínimo 1.
  const min = item.porPeso ? 0.001 : 1
  item.quantidade = Math.max(min, Number(item.quantidade) || min)
  item.total = Math.max(0, item.quantidade * item.precoUnitario - item.desconto)
}

function ajustarQtd(idx: number, delta: number) {
  const item = itens.value[idx]
  // Item por peso anda de 100g (0,1 KG); unitário anda de 1 em 1.
  const passo = item.porPeso ? delta * 0.1 : delta
  const min = item.porPeso ? 0.001 : 1
  item.quantidade = Math.max(min, Math.round((item.quantidade + passo) * 1000) / 1000)
  recalcular(idx)
}

function removerItem(idx: number) {
  itens.value.splice(idx, 1)
  if (itemSelecionado.value !== null && itemSelecionado.value >= itens.value.length)
    itemSelecionado.value = itens.value.length - 1
}

function sincrDesconto(origem: 'reais' | 'pct') {
  if (origem === 'reais') {
    desconto.value = descontoReais.value
    descontoPct.value = subtotal.value > 0
      ? parseFloat(((descontoReais.value / subtotal.value) * 100).toFixed(1))
      : 0
  } else {
    descontoReais.value = parseFloat(((descontoPct.value / 100) * subtotal.value).toFixed(2))
    desconto.value = descontoReais.value
  }
}

function selecionarFormaPagamento(tipo: string) {
  const idx = pagamentos.value.findIndex(p => p.tipo === tipo)
  if (idx >= 0) { pagamentos.value.splice(idx, 1); return }

  // Cartão: abre o diálogo para o caixa escolher a bandeira (a taxa fica por trás)
  if ((tipo === 'Crédito' || tipo === 'Débito') && operadoras.value.length) {
    cartaoTipo.value = tipo
    cartaoBandeiraSel.value = opcoesCartao.value[0] ?? null
    cartaoParcelas.value = 1
    dialogCartao.value = true
    return
  }

  // Crediário: exige cliente e abre o diálogo de parcelas/entrada.
  if (tipo === 'Crediário') {
    if (!clienteId.value) {
      notif.aviso('Selecione um cliente para vender no crediário.')
      return
    }
    crediarioParcelas.value = 2
    crediarioEntrada.value = 0
    dialogCrediario.value = true
    return
  }

  const restante = Math.max(0, total.value - totalPago.value)
  pagamentos.value.push({ tipo, valor: restante || total.value })
}

// ── Crediário ─────────────────────────────────────────────────────
const dialogCrediario = ref(false)
const crediarioParcelas = ref(2)
const crediarioEntrada = ref(0)
// Base do crediário = valor restante da venda (o que ainda falta pagar).
const crediarioValorBase = computed(() => Math.max(0, total.value - totalPago.value) || total.value)
const crediarioValor = computed(() => crediarioValorBase.value)
const crediarioValorParcela = computed(() => {
  const financiado = Math.max(0, crediarioValorBase.value - (crediarioEntrada.value || 0))
  const n = Math.max(1, crediarioParcelas.value || 1)
  return Math.round(financiado / n * 100) / 100
})
const clienteSelecionadoNome = computed(() =>
  clientes.value.find(c => c.id === clienteId.value)?.nome ?? '')

// Ao selecionar um cliente, preenche automaticamente o CPF/CNPJ na nota
// com o documento cadastrado do cliente. Busca o detalhe por ID para não
// depender do item da lista (que o autocomplete pode substituir na seleção).
watch(clienteId, async (id) => {
  if (!id) return
  let doc = String((clientes.value.find(x => x.id === id) as any)?.cpfCnpj ?? '').replace(/\D/g, '')
  if (!doc) {
    try {
      const r = await api.get(`/clientes/${id}`)
      doc = String(r.data?.cpfCnpj ?? '').replace(/\D/g, '')
    } catch { /* ignora */ }
  }
  if (!doc) return
  if (doc.length > 11) {
    tipoDocConsumidor.value = 'cnpj'
    cpfConsumidor.value = maskCnpj(doc)
  } else {
    tipoDocConsumidor.value = 'cpf'
    cpfConsumidor.value = formatarCpf(doc)
  }
})

function confirmarCrediario() {
  if (!(crediarioParcelas.value >= 1)) { notif.aviso('Informe o número de parcelas.'); return }
  if (crediarioEntrada.value >= crediarioValorBase.value) {
    notif.aviso('A entrada não pode ser maior ou igual ao valor.'); return
  }
  pagamentos.value.push({
    tipo: 'Crediário', valor: crediarioValorBase.value,
    crediarioParcelas: crediarioParcelas.value,
    crediarioEntrada: crediarioEntrada.value || 0,
  })
  dialogCrediario.value = false
}

// Maquininha por trás da bandeira escolhida (usada só para o cálculo interno).
const cartaoOperadora = computed(() =>
  operadoras.value.find(o => o.id === cartaoBandeiraSel.value?.operadoraId) ?? null)
const cartaoValor = computed(() => Math.max(0, total.value - totalPago.value) || total.value)
const cartaoTaxa = computed(() => {
  const o = cartaoOperadora.value
  if (!o) return 0
  if (cartaoTipo.value === 'Débito') return o.taxaDebito
  return cartaoParcelas.value >= 2 ? o.taxaCreditoParcelado : o.taxaCreditoVista
})
const cartaoLiquido = computed(() =>
  Math.round(cartaoValor.value * (1 - cartaoTaxa.value / 100) * 100) / 100)

function confirmarCartao() {
  const sel = cartaoBandeiraSel.value
  const o = cartaoOperadora.value
  if (!sel || !o) { notif.aviso('Escolha a bandeira do cartão.'); return }
  pagamentos.value.push({
    tipo: cartaoTipo.value, valor: cartaoValor.value,
    // operadoraId vai por trás (taxa/recebível); o caixa vê apenas a bandeira.
    operadoraId: o.id, operadoraNome: sel.label,
    parcelas: cartaoTipo.value === 'Crédito' ? cartaoParcelas.value : 1,
    taxa: cartaoTaxa.value, liquido: cartaoLiquido.value,
  })
  dialogCartao.value = false
}

function dinheiroExato() {
  const din = pagamentos.value.find(p => p.tipo === 'Dinheiro')
  if (din) din.valor = total.value
}

function onInputDoc(e: Event) {
  const raw = (e.target as HTMLInputElement).value
  cpfConsumidor.value = tipoDocConsumidor.value === 'cpf' ? formatarCpf(raw) : maskCnpj(raw)
}

const baseDescontoItem = computed(() => {
  if (itemSelecionado.value === null) return 0
  const it = itens.value[itemSelecionado.value]
  return it ? it.precoUnitario * it.quantidade : 0
})

function sincrDescontoItem(origem: 'reais' | 'pct') {
  const base = baseDescontoItem.value
  if (origem === 'reais') {
    descontoItemPctTemp.value = base > 0
      ? parseFloat(((descontoItemTemp.value / base) * 100).toFixed(1)) : 0
  } else {
    descontoItemTemp.value = parseFloat(((descontoItemPctTemp.value / 100) * base).toFixed(2))
  }
}

function abrirDescontoItem(i: number) {
  itemSelecionado.value = i
  const it = itens.value[i]
  descontoItemTemp.value = it?.desconto || 0
  const base = it ? it.precoUnitario * it.quantidade : 0
  descontoItemPctTemp.value = base > 0
    ? parseFloat(((descontoItemTemp.value / base) * 100).toFixed(1)) : 0
  dialogDescontoItem.value = true
}

function aplicarDescontoItem() {
  if (itemSelecionado.value !== null) {
    const item = itens.value[itemSelecionado.value]
    item.desconto = Math.min(descontoItemTemp.value, item.precoUnitario * item.quantidade)
    recalcular(itemSelecionado.value)
  }
  dialogDescontoItem.value = false
}

// ── Busca de produto ──────────────────────────────────────────────
async function buscarProduto() {
  const codigo = codigoDigitado.value.trim()
  if (!codigo) { inputCodigo.value?.focus(); return }
  try {
    const res = await api.get<Produto[]>('/produtos/buscar', {
      params: { q: codigo, empresaId: auth.empresaId }
    })
    const lista = res.data
    if (lista.length === 1) {
      adicionarProduto(lista[0])
    } else if (lista.length > 1) {
      produtosBusca.value = lista
      dialogProdutos.value = true
    } else {
      notif.aviso('Produto não encontrado.')
    }
  } catch { /* interceptor exibiu */ }
  finally { codigoDigitado.value = '' }
}

/** Produto vendido por peso (preço por KG): a quantidade é informada em gramas. */
function ehPorPeso(p: { vendidoFracionado?: boolean; unidadeSigla?: string }) {
  return p.vendidoFracionado === true || (p.unidadeSigla || '').toUpperCase() === 'KG'
}

function adicionarProduto(p: Produto) {
  dialogProdutos.value = false
  // Produto por peso → abre o diálogo de gramas em vez de somar 1 unidade
  if (ehPorPeso(p)) { abrirPeso(p); return }

  const existe = itens.value.find(i => i.produtoId === p.id)
  if (existe) {
    existe.quantidade++
    existe.total = existe.quantidade * existe.precoUnitario - existe.desconto
  } else {
    itens.value.push({
      produtoId: p.id, descricao: p.descricao, codigo: p.codigo,
      precoUnitario: p.precoVenda, quantidade: 1,
      total: p.precoVenda, unidade: p.unidadeSigla, desconto: 0
    })
    itemSelecionado.value = itens.value.length - 1
  }
  nextTick(() => inputCodigo.value?.focus())
}

// ── Venda por peso (gramas) ───────────────────────────────────────
const dialogPeso = ref(false)
const produtoPeso = ref<Produto | null>(null)
const gramas = ref<number>(0)
const precoPor100g = computed(() =>
  produtoPeso.value ? Math.round(produtoPeso.value.precoVenda / 10 * 100) / 100 : 0)
const totalPeso = computed(() =>
  produtoPeso.value ? Math.round((gramas.value / 1000) * produtoPeso.value.precoVenda * 100) / 100 : 0)

function abrirPeso(p: Produto) {
  produtoPeso.value = p
  gramas.value = 0
  dialogPeso.value = true
  nextTick(() => inputGramas.value?.focus?.())
}

function confirmarPeso() {
  const p = produtoPeso.value
  const g = Number(gramas.value)
  if (!p || !(g > 0)) { notif.aviso('Informe o peso em gramas.'); return }
  const qtdKg = Math.round((g / 1000) * 1000) / 1000   // KG com 3 casas
  itens.value.push({
    produtoId: p.id, descricao: p.descricao, codigo: p.codigo,
    precoUnitario: p.precoVenda, quantidade: qtdKg,
    total: Math.round(qtdKg * p.precoVenda * 100) / 100,
    unidade: p.unidadeSigla || 'KG', desconto: 0, porPeso: true,
  })
  itemSelecionado.value = itens.value.length - 1
  dialogPeso.value = false
  nextTick(() => inputCodigo.value?.focus())
}

// ── Cliente ───────────────────────────────────────────────────────
async function pesquisarCliente(q: string) {
  if (!q || q.length < 2) return
  buscandoCliente.value = true
  try {
    const res = await api.get<Cliente[]>('/clientes/buscar', {
      params: { q, empresaId: auth.empresaId }
    })
    clientes.value = res.data
  } finally { buscandoCliente.value = false }
}

/** Cadastra um cliente só com o nome e já seleciona na venda. */
async function cadastrarClienteRapido(nome: string) {
  try {
    const r = await api.post('/clientes', { empresaId: auth.empresaId, nome, tipoPessoa: 'Fisica' })
    const novo: Cliente = { id: r.data.id ?? r.data, nome }
    clientes.value = [novo, ...clientes.value]
    clienteId.value = novo.id
    buscaCliente.value = ''
    notif.ok(`Cliente "${nome}" cadastrado e selecionado.`)
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? e?.response?.data?.title ?? 'Erro ao cadastrar cliente.')
  }
}

// ── Finalizar ─────────────────────────────────────────────────────
async function finalizar() {
  if (finalizando.value || !podeFinalizarVenda.value) return
  finalizando.value = true
  try {
    const localEstoqueId = sessaoAtual.value?.localEstoqueId || localEstoqueIdCaixa.value
    if (!localEstoqueId) {
      notif.erro('Nenhum local de estoque vinculado ao caixa. Reabra o caixa selecionando o local.')
      finalizando.value = false
      return
    }
    const inicioRes = await api.post<{ id: string }>('/vendas/iniciar', {
      empresaId: auth.empresaId,
      usuarioId: colaboradorId.value || auth.usuario?.id,
      localEstoqueId,
      clienteId: clienteId.value ?? null,
    })
    const vendaId = inicioRes.data.id

    for (const item of itens.value) {
      await api.post(`/vendas/${vendaId}/itens`, {
        produtoId: item.produtoId,
        quantidade: item.quantidade,
        precoUnitario: item.precoUnitario,
        percentualDesconto: item.desconto > 0
          ? (item.desconto / (item.precoUnitario * item.quantidade)) * 100
          : 0,
      })
    }

    const docRaw = tipoDocConsumidor.value === 'cpf' ? cpfRaw(cpfConsumidor.value) : cnpjRaw(cpfConsumidor.value)
    const docValido = tipoDocConsumidor.value === 'cpf' ? docRaw.length === 11 : docRaw.length === 14

    const res = await api.post<{
      numero: string; total: number; troco: number
      notaFiscalId?: string; qrCode?: string; chaveAcesso?: string
    }>(`/vendas/${vendaId}/finalizar`, {
      pagamentos: pagamentos.value.map(p => ({
        forma: mapaFormaPagamento[p.tipo] ?? p.tipo,
        valor: p.valor,
        parcelas: p.parcelas ?? 1,
        operadoraCartaoId: p.operadoraId ?? null,
      })),
      cpfCnpjConsumidor: docValido ? docRaw : null,
    })

    // Crediário: abre o contrato (gera parcelas → contas a receber) vinculado à venda.
    const pagCrediario = pagamentos.value.find(p => p.tipo === 'Crediário')
    if (pagCrediario && clienteId.value) {
      try {
        await api.post('/crediario', {
          empresaId: auth.empresaId,
          clienteId: clienteId.value,
          usuarioId: colaboradorId.value || auth.usuario?.id,
          valorTotal: pagCrediario.valor,
          valorEntrada: pagCrediario.crediarioEntrada ?? 0,
          numeroParcelas: pagCrediario.crediarioParcelas ?? 1,
          taxaJurosMensal: 0,
          vendaId,
        })
      } catch {
        notif.erro('Venda finalizada, mas houve erro ao abrir o crediário. Registre-o manualmente na tela Crediário.')
      }
    }

    ultimaVendaId.value = vendaId
    ultimaVendaNumero.value = res.data.numero
    ultimaVendaTotal.value = res.data.total
    ultimaVendaTroco.value = res.data.troco
    ultimaVendaQrCode.value = res.data.qrCode ?? null
    ultimaVendaChave.value = res.data.chaveAcesso ?? null
    dialogComprovante.value = true
    notif.ok('Venda finalizada!')

    if (res.data.qrCode) {
      await nextTick()
      if (canvasQr.value)
        await QRCode.toCanvas(canvasQr.value, res.data.qrCode, { width: 160, margin: 1 })
    }
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao finalizar venda.')
  } finally {
    finalizando.value = false
  }
}

function novaVenda() {
  itens.value = []; pagamentos.value = []
  desconto.value = 0; descontoReais.value = 0; descontoPct.value = 0
  clienteId.value = null; cpfConsumidor.value = ''
  tipoDocConsumidor.value = 'cpf'; itemSelecionado.value = null
  ultimaVendaQrCode.value = null; ultimaVendaChave.value = null
  dialogComprovante.value = false
  nextTick(() => inputCodigo.value?.focus())
}

function cancelar() {
  if (!itens.value.length) return
  if (confirm('Cancelar a venda atual?')) {
    itens.value = []; pagamentos.value = []
    desconto.value = 0; descontoReais.value = 0; descontoPct.value = 0
    itemSelecionado.value = null
    nextTick(() => inputCodigo.value?.focus())
  }
}

async function imprimirComprovante() {
  if (!ultimaVendaId.value) return
  const res = await api.get(`/fiscal/recibo/venda/${ultimaVendaId.value}`, { responseType: 'blob' })
  const url = URL.createObjectURL(res.data)
  window.open(url, '_blank')
}

// ── Scanner de câmera ─────────────────────────────────────────────
const dialogScanner = ref(false)
const videoScanner = ref<HTMLVideoElement | null>(null)
const inputFoto = ref<HTMLInputElement | null>(null)
const scannerStatus = ref('')
const usarFotoFallback = ref(false)
const cameras = ref<{ deviceId: string; label: string }[]>([])
const cameraAtual = ref('')
let zxingReader: BrowserMultiFormatReader | null = null
let scannerStream: MediaStream | null = null

async function abrirScanner() {
  dialogScanner.value = true
  usarFotoFallback.value = false
  scannerStatus.value = 'Iniciando câmera...'

  await nextTick()

  try {
    // Lista câmeras disponíveis
    const dispositivosCam = await BrowserMultiFormatReader.listVideoInputDevices()
    cameras.value = dispositivosCam.map(d => ({
      deviceId: d.deviceId,
      label: d.label || `Câmera ${d.deviceId.slice(0, 6)}`
    }))

    // Prefere câmera traseira no mobile
    const traseira = dispositivosCam.find(d =>
      /back|rear|traseira|environment/i.test(d.label)
    )
    cameraAtual.value = traseira?.deviceId ?? dispositivosCam[0]?.deviceId ?? ''

    await iniciarLeitura()
  } catch (e: any) {
    if (e?.name === 'NotAllowedError') {
      scannerStatus.value = 'Permissão de câmera negada.'
      usarFotoFallback.value = true
    } else {
      // Sem câmera acessível — usa fallback por foto
      usarFotoFallback.value = true
      scannerStatus.value = ''
    }
  }
}

async function iniciarLeitura() {
  if (!videoScanner.value) return
  scannerStatus.value = 'Aponte para o código de barras...'

  zxingReader = new BrowserMultiFormatReader()

  try {
    await zxingReader.decodeFromVideoDevice(
      cameraAtual.value || undefined,
      videoScanner.value,
      (result, err) => {
        if (result) {
          const codigo = result.getText()
          fecharScanner()
          codigoDigitado.value = codigo
          buscarProduto()
        }
        // NotFoundException é esperado entre frames — ignora silenciosamente
      }
    )
  } catch (e: any) {
    scannerStatus.value = 'Erro ao acessar câmera.'
    usarFotoFallback.value = true
  }
}

async function trocarCamera() {
  if (zxingReader) {
    BrowserMultiFormatReader.releaseAllStreams()
    zxingReader = null
  }
  await nextTick()
  await iniciarLeitura()
}

function fecharScanner() {
  dialogScanner.value = false
  if (zxingReader) {
    BrowserMultiFormatReader.releaseAllStreams()
    zxingReader = null
  }
  scannerStatus.value = ''
  nextTick(() => inputCodigo.value?.focus())
}

async function lerFoto(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return

  // Usa BarcodeDetector nativo se disponível (Chrome Android)
  if ('BarcodeDetector' in window) {
    try {
      const bd = new (window as any).BarcodeDetector({
        formats: ['ean_13', 'ean_8', 'code_128', 'code_39', 'qr_code', 'upc_a']
      })
      const img = await createImageBitmap(file)
      const barcodes = await bd.detect(img)
      if (barcodes.length) {
        fecharScanner()
        codigoDigitado.value = barcodes[0].rawValue
        buscarProduto()
        return
      }
      notif.aviso('Código não detectado. Tente outra foto.')
    } catch { /* fallback para ZXing abaixo */ }
  }

  // Fallback: ZXing a partir de blob URL
  try {
    const url = URL.createObjectURL(file)
    const reader = new BrowserMultiFormatReader()
    const result = await reader.decodeFromImageUrl(url)
    URL.revokeObjectURL(url)
    fecharScanner()
    codigoDigitado.value = result.getText()
    buscarProduto()
  } catch {
    notif.aviso('Código não detectado. Tente outra foto mais nítida.')
  }
}

// ── Promoções ativas ───────────────────────────────────────────────
const todasPromocoes = ref<string[]>([])
const promoIdx = ref(0)
const promoDismissed = ref(false)
const promoAtiva = computed(() => todasPromocoes.value[promoIdx.value] ?? null)

let promoTimer: any = null
function iniciarRotacaoPromo() {
  clearInterval(promoTimer)
  promoIdx.value = 0
  if (todasPromocoes.value.length > 1) {
    promoTimer = setInterval(() => {
      promoIdx.value = (promoIdx.value + 1) % todasPromocoes.value.length
    }, 5000)
  }
}
function fecharPromo() {
  promoDismissed.value = true
  clearInterval(promoTimer)
}

async function carregarPromocoes() {
  try {
    // Lê as promoções ATIVAS do módulo Promoções (inclui as geradas pela regra
    // de validade). Não usa mais as artes da galeria de Marketing.
    const r = await api.get('/promocoes', {
      params: { empresaId: auth.empresaId, status: 'Ativa' }
    })
    const promos: any[] = r.data ?? []
    const nomes = promos.map((p: any) => {
      const desc = p.tipoDesconto === 'ValorFixo'
        ? `R$ ${Number(p.desconto).toFixed(2)} OFF`
        : `${Number(p.desconto).toFixed(0)}% OFF`
      return `${p.nome} — ${desc}`
    }).filter(Boolean)
    todasPromocoes.value = nomes
    iniciarRotacaoPromo()
  } catch { /* silencioso */ }
}

// ── Colaboradores ──────────────────────────────────────────────────
async function carregarColaboradores() {
  if (!auth.empresaId) return
  try {
    const res = await api.get<Colaborador[]>('/usuarios', { params: { empresaId: auth.empresaId } })
    colaboradores.value = res.data.filter(u => u.perfil !== 'Contador')
    colaboradorId.value = auth.usuario?.id ?? colaboradores.value[0]?.id ?? ''
  } catch { /* silencioso */ }
}

// ── Atalhos globais ────────────────────────────────────────────────
function onKeydown(e: KeyboardEvent) {
  const tag = (e.target as HTMLElement).tagName
  const emInput = ['INPUT', 'TEXTAREA'].includes(tag)

  switch (e.key) {
    case 'F1':
      e.preventDefault()
      modalAtalhos.value = !modalAtalhos.value
      break
    case 'F2':
      e.preventDefault()
      inputDesconto.value?.focus()
      break
    case 'F3':
      e.preventDefault()
      inputCliente.value?.focus()
      break
    case 'F4':
      e.preventDefault()
      selecionarFormaPagamento('Dinheiro')
      break
    case 'F5':
      e.preventDefault()
      dinheiroExato()
      break
    case 'F6':
      e.preventDefault()
      selecionarFormaPagamento('Pix')
      break
    case 'F7':
      e.preventDefault()
      selecionarFormaPagamento('Crédito')
      break
    case 'F8':
      e.preventDefault()
      selecionarFormaPagamento('Débito')
      break
    case 'F9':
      e.preventDefault()
      inputCpf.value?.focus()
      break
    case 'F10':
      e.preventDefault()
      finalizar()
      break
    case 'Escape':
      if (modalAtalhos.value) { modalAtalhos.value = false; break }
      if (dialogProdutos.value) { dialogProdutos.value = false; break }
      if (!emInput) { e.preventDefault(); cancelar() }
      break
    case 'ArrowUp':
      if (!emInput && itens.value.length) {
        e.preventDefault()
        itemSelecionado.value = Math.max(0, (itemSelecionado.value ?? 0) - 1)
      }
      break
    case 'ArrowDown':
      if (!emInput && itens.value.length) {
        e.preventDefault()
        itemSelecionado.value = Math.min(itens.value.length - 1, (itemSelecionado.value ?? -1) + 1)
      }
      break
    case 'Delete':
      if (!emInput && itemSelecionado.value !== null) {
        e.preventDefault()
        removerItem(itemSelecionado.value)
      }
      break
    case '+':
      if (!emInput && itemSelecionado.value !== null) {
        e.preventDefault()
        ajustarQtd(itemSelecionado.value, 1)
      }
      break
    case '-':
      if (!emInput && itemSelecionado.value !== null) {
        e.preventDefault()
        ajustarQtd(itemSelecionado.value, -1)
      }
      break
  }
}

onMounted(async () => {
  atualizarHora()
  clockTimer = setInterval(atualizarHora, 30000)
  document.addEventListener('keydown', onKeydown, true)
  document.addEventListener('fullscreenchange', onFullscreenChange)
  await Promise.all([carregarColaboradores(), verificarSessaoCaixa(), carregarPromocoes(), carregarOperadoras()])
})

async function carregarOperadoras() {
  try {
    const r = await api.get('/financeiro/operadoras-cartao', { params: { empresaId: auth.empresaId } })
    operadoras.value = r.data ?? []
  } catch { operadoras.value = [] }
}
onUnmounted(() => {
  clearInterval(clockTimer)
  clearInterval(promoTimer)
  document.removeEventListener('keydown', onKeydown, true)
  document.removeEventListener('fullscreenchange', onFullscreenChange)
  if (zxingReader) BrowserMultiFormatReader.releaseAllStreams()
})
</script>

<style>
/* ══ SHELL ══════════════════════════════════════════════════════ */
.pdv-shell {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 64px);
  background: #f1f5f9;
  margin: -16px;
  font-family: 'Inter', 'Roboto', sans-serif;
}

/* ══ TOPBAR ═════════════════════════════════════════════════════ */
.pdv-topbar {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 0 24px;
  height: 54px;
  background: linear-gradient(90deg, #0f172a 0%, #1e293b 100%);
  color: white;
  flex-shrink: 0;
  box-shadow: 0 2px 12px rgba(0,0,0,.25);
}
.pdv-topbar-left  { display: flex; align-items: center; flex-shrink: 0; gap: 10px; }
.pdv-topbar-center { flex: 1; max-width: 560px; margin: 0 auto; }
.pdv-topbar-right { display: flex; align-items: center; flex-shrink: 0; margin-left: auto; gap: 4px; }

.pdv-brand {
  display: flex; align-items: center; gap: 8px;
  padding-right: 16px;
  border-right: 1px solid rgba(255,255,255,.12);
}
.pdv-brand-name { font-weight: 700; font-size: 13px; letter-spacing: .04em; color: white; }

.pdv-search .v-field { background: rgba(255,255,255,.1) !important; border-radius: 8px !important; }
.pdv-search .v-field__outline { --v-field-border-color: rgba(255,255,255,.2) !important; }
.pdv-search input { color: white !important; font-size: 13px; }
.pdv-search input::placeholder { color: rgba(255,255,255,.4) !important; }
.pdv-search .v-field__prepend-inner .v-icon { color: rgba(255,255,255,.5) !important; }

.pdv-info-pill {
  display: flex; align-items: center; gap: 5px;
  padding: 4px 10px;
  border-radius: 20px;
  background: rgba(255,255,255,.07);
  font-size: 12px;
  color: rgba(255,255,255,.65);
  white-space: nowrap;
}

/* ══ BODY ═══════════════════════════════════════════════════════ */
.pdv-body {
  display: flex;
  flex: 1;
  overflow: hidden;
}

/* ══ COLUNA ITENS ═══════════════════════════════════════════════ */
.pdv-col-itens {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: white;
  overflow: hidden;
}

/* Cabeçalho das colunas */
.pdv-col-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 0 20px;
  height: 36px;
  background: #f8fafc;
  border-bottom: 1px solid #e2e8f0;
  flex-shrink: 0;
}
.pdv-col-header span {
  font-size: 10px;
  font-weight: 700;
  letter-spacing: .06em;
  text-transform: uppercase;
  color: #94a3b8;
}
.pdv-ch-seq  { width: 28px; flex-shrink: 0; }
.pdv-ch-info { flex: 1; }
.pdv-ch-qtd  { width: 108px; text-align: center; flex-shrink: 0; }
.pdv-ch-tot  { min-width: 90px; text-align: right; flex-shrink: 0; }
.pdv-ch-del  { width: 28px; flex-shrink: 0; }

.pdv-itens-wrap {
  flex: 1;
  overflow-y: auto;
  position: relative;
}
.pdv-itens-wrap::-webkit-scrollbar { width: 5px; }
.pdv-itens-wrap::-webkit-scrollbar-track { background: transparent; }
.pdv-itens-wrap::-webkit-scrollbar-thumb { background: #e2e8f0; border-radius: 10px; }

.pdv-vazio {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  min-height: 300px;
}

.pdv-vazio-icon {
  width: 96px; height: 96px;
  border-radius: 24px;
  background: linear-gradient(135deg, #f1f5f9, #e2e8f0);
  display: flex; align-items: center; justify-content: center;
  margin-bottom: 20px;
}

.pdv-vazio-titulo {
  font-size: 16px; font-weight: 700; color: #64748b; margin-bottom: 6px;
}
.pdv-vazio-sub {
  font-size: 13px; color: #94a3b8; margin-bottom: 24px;
}

.pdv-hint-chips {
  display: flex; gap: 8px; flex-wrap: wrap; justify-content: center;
}
.pdv-hint-chip {
  display: flex; align-items: center; gap: 5px;
  padding: 4px 10px;
  border-radius: 8px;
  border: 1px solid #e2e8f0;
  font-size: 11px; color: #94a3b8;
}
.pdv-hint-chip kbd {
  font-size: 10px; background: #f1f5f9; border-radius: 4px;
  padding: 1px 5px; font-family: monospace; color: #64748b;
}

.pdv-itens-lista { padding: 0; }

/* ── Item individual ── */
.pdv-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 11px 20px;
  border-bottom: 1px solid #f1f5f9;
  cursor: pointer;
  transition: background .1s;
}
.pdv-item:hover { background: #f8fafc; }
.pdv-item--selected {
  background: #eff6ff !important;
  border-left: 3px solid #3b82f6;
  padding-left: 17px;
}

.pdv-item-seq {
  width: 28px; height: 28px;
  border-radius: 8px;
  background: #f1f5f9;
  display: flex; align-items: center; justify-content: center;
  font-size: 11px; font-weight: 700; color: #64748b;
  flex-shrink: 0;
}
.pdv-item--selected .pdv-item-seq { background: #3b82f6; color: white; border-radius: 8px; }

.pdv-item-info { flex: 1; min-width: 0; }
.pdv-item-nome {
  font-size: 13.5px; font-weight: 600; color: #0f172a;
  white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
  line-height: 1.3;
}
.pdv-item-detalhe {
  font-size: 11px; color: #94a3b8; margin-top: 2px;
  display: flex; align-items: center; gap: 4px;
}
.pdv-item-detalhe .sep { color: #cbd5e1; }

/* Qtd inline */
.pdv-item-qtd {
  display: flex;
  align-items: center;
  border: 1.5px solid #e2e8f0;
  border-radius: 8px;
  overflow: hidden;
  width: 108px;
  flex-shrink: 0;
}
.pdv-qtd-btn {
  width: 30px; height: 30px;
  background: #f8fafc;
  border: none; cursor: pointer;
  font-size: 16px; font-weight: 600;
  color: #64748b;
  transition: all .1s;
  display: flex; align-items: center; justify-content: center;
}
.pdv-qtd-btn:hover { background: #e2e8f0; color: #0f172a; }
.pdv-qtd-input {
  flex: 1;
  text-align: center;
  border: none;
  border-left: 1.5px solid #e2e8f0;
  border-right: 1.5px solid #e2e8f0;
  font-size: 13px; font-weight: 700; color: #0f172a;
  height: 30px;
  outline: none;
  background: white;
  min-width: 0;
}
.pdv-qtd-input::-webkit-inner-spin-button { display: none; }

.pdv-item-total {
  font-size: 14px; font-weight: 700; color: #0f172a;
  min-width: 90px; text-align: right; flex-shrink: 0;
}

.pdv-item-del {
  width: 28px; height: 28px;
  border: none; background: none;
  cursor: pointer; border-radius: 6px;
  color: #cbd5e1;
  display: flex; align-items: center; justify-content: center;
  transition: all .12s; flex-shrink: 0;
}
.pdv-item-del:hover { background: #fee2e2; color: #dc2626; }

.pdv-item-desc {
  width: 28px; height: 28px;
  border: none; background: none;
  cursor: pointer; border-radius: 6px;
  color: #cbd5e1;
  display: flex; align-items: center; justify-content: center;
  transition: all .12s; flex-shrink: 0;
}
.pdv-item-desc:hover { background: #fef3c7; color: #d97706; }
.pdv-item-desc.ativo { color: #d97706; background: #fef3c7; }

/* ── Totais ── */
.pdv-totais {
  border-top: 1px solid #e2e8f0;
  padding: 14px 20px;
  background: white;
  flex-shrink: 0;
}
.pdv-totais-linha {
  display: flex; justify-content: space-between;
  font-size: 12.5px; color: #64748b; margin-bottom: 4px;
}
.pdv-totais-total {
  display: flex; justify-content: space-between; align-items: center;
  margin-top: 8px; padding-top: 10px;
  border-top: 1.5px solid #e2e8f0;
}
.pdv-total-label {
  font-size: 11px; font-weight: 700; letter-spacing: .08em;
  text-transform: uppercase; color: #94a3b8;
}
.pdv-total-valor {
  font-size: 28px; font-weight: 800; color: #0f172a;
  letter-spacing: -.5px;
}

/* ══ COLUNA PAGAMENTO ════════════════════════════════════════════ */
.pdv-col-pag {
  width: 380px;
  flex-shrink: 0;
  background: #ffffff;
  border-left: 1px solid #e2e8f0;
  display: flex;
  flex-direction: column;
  overflow-y: auto;
  overflow-x: hidden;
}
.pdv-col-pag::-webkit-scrollbar { width: 4px; }
.pdv-col-pag::-webkit-scrollbar-track { background: transparent; }
.pdv-col-pag::-webkit-scrollbar-thumb { background: #e2e8f0; border-radius: 10px; }

.pdv-pag-section {
  padding: 16px 20px 0;
}

.pdv-pag-label {
  font-size: 10px; font-weight: 700; letter-spacing: .1em;
  text-transform: uppercase; color: #94a3b8;
  display: flex; align-items: center; margin-bottom: 8px;
}

/* Formas de pagamento em grid */
.pdv-fps-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}
.pdv-fp-btn {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 5px;
  padding: 14px 6px;
  border-radius: 12px;
  border: 1.5px solid color-mix(in srgb, var(--fp-cor) 25%, #e2e8f0);
  border-top: 3px solid var(--fp-cor, #94a3b8);
  background: color-mix(in srgb, var(--fp-cor) 12%, white);
  color: color-mix(in srgb, var(--fp-cor) 55%, #334155);
  cursor: pointer;
  font-size: 11px; font-weight: 600;
  transition: all .15s;
  position: relative;
}
.pdv-fp-btn:hover {
  border-color: var(--fp-cor, #3b82f6);
  background: color-mix(in srgb, var(--fp-cor) 22%, white);
}
.pdv-fp-btn--ativo {
  border-color: var(--fp-cor, #2563eb);
  color: var(--fp-cor, #2563eb);
  background: #eef2ff;
  background: color-mix(in srgb, var(--fp-cor) 14%, white);
  box-shadow: inset 0 0 0 2px var(--fp-cor, #2563eb);
}
.pdv-kbd-fp {
  position: absolute; top: 5px; right: 7px;
  font-size: 9px; background: rgba(0,0,0,.07);
  border-radius: 3px; padding: 1px 4px;
  font-family: monospace; color: #94a3b8;
}
.pdv-fp-btn--ativo .pdv-kbd-fp {
  background: rgba(255,255,255,.25);
  color: rgba(255,255,255,.8);
}

.pdv-pag-forma-label {
  font-size: 12px; color: #64748b;
  min-width: 80px; display: flex; align-items: center;
}
.pdv-del-pag {
  background: none; border: none;
  color: #cbd5e1;
  cursor: pointer; border-radius: 6px;
  width: 28px; height: 28px;
  display: flex; align-items: center; justify-content: center;
  transition: all .12s;
}
.pdv-del-pag:hover { background: #fee2e2; color: #dc2626; }

/* Status do pagamento */
.pdv-pag-status {
  margin: 14px 20px 0;
  border-radius: 10px;
  padding: 12px 16px;
  font-size: 13px; font-weight: 600;
  display: flex; align-items: center;
  min-height: 44px;
}
.pdv-status--neutro {
  background: #f8fafc;
  color: #94a3b8; font-weight: 400; font-size: 12px;
  border: 1px dashed #e2e8f0;
}
.pdv-status--ok     { background: #f0fdf4; color: #16a34a; border: 1px solid #bbf7d0; }
.pdv-status--troco  { background: #eff6ff; color: #2563eb; border: 1px solid #bfdbfe; }
.pdv-status--falta  { background: #fffbeb; color: #d97706; border: 1px solid #fde68a; }

.pdv-status-hint, .pdv-status-ok, .pdv-status-troco, .pdv-status-falta {
  display: flex; align-items: center; width: 100%;
}

/* Separador */
.pdv-sep {
  height: 1px;
  background: #e2e8f0;
  margin: 16px 20px 0;
}

/* Botões de ação */
.pdv-acoes {
  margin-top: auto;
  padding: 16px 20px 20px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.pdv-btn-finalizar {
  display: flex; align-items: center; justify-content: center; gap: 8px;
  height: 58px;
  border-radius: 12px;
  border: 1.5px dashed #e2e8f0;
  background: #f8fafc;
  color: #94a3b8;
  font-size: 16px; font-weight: 700;
  cursor: not-allowed;
  transition: all .2s;
  position: relative;
  letter-spacing: .01em;
}
.pdv-btn-finalizar--ok {
  border: none;
  background: linear-gradient(135deg, #10b981 0%, #059669 100%);
  color: white;
  cursor: pointer;
  box-shadow: 0 4px 16px rgba(16,185,129,.3);
}
.pdv-btn-finalizar--ok:hover {
  transform: translateY(-1px);
  box-shadow: 0 8px 24px rgba(16,185,129,.4);
}
.pdv-btn-finalizar--ok:active { transform: translateY(0); }
.pdv-btn-finalizar:disabled { cursor: not-allowed; }

.pdv-btn-cancelar {
  display: flex; align-items: center; justify-content: center; gap: 6px;
  height: 38px;
  border-radius: 8px;
  border: 1px solid #e2e8f0;
  background: transparent;
  color: #94a3b8;
  font-size: 12px; font-weight: 600;
  cursor: pointer;
  transition: all .15s;
}
.pdv-btn-cancelar:not(:disabled):hover {
  border-color: #fca5a5;
  color: #dc2626;
  background: #fff1f2;
}
.pdv-btn-cancelar:disabled { opacity: .35; cursor: not-allowed; }

/* ── Kbd inline ── */
.pdv-kbd {
  display: inline-flex; align-items: center;
  background: rgba(0,0,0,.06);
  color: #94a3b8;
  border-radius: 4px;
  padding: 1px 6px;
  font-family: monospace; font-size: 10px;
  margin-left: 6px;
}
.pdv-btn-finalizar--ok .pdv-kbd-big {
  background: rgba(0,0,0,.15);
  color: rgba(255,255,255,.8);
}
.pdv-kbd-big {
  margin-left: auto;
  background: rgba(0,0,0,.07);
  color: #94a3b8;
  border-radius: 6px;
  padding: 3px 10px;
  font-family: monospace; font-size: 12px;
}

/* Busca produto */
.pdv-busca-num {
  width: 26px; height: 26px;
  border-radius: 6px;
  background: #e8f0fe;
  color: #3b82f6;
  font-size: 12px; font-weight: 700;
  display: flex; align-items: center; justify-content: center;
}

/* Comprovante */
.pdv-check-circle {
  width: 72px; height: 72px;
  border-radius: 50%;
  background: linear-gradient(135deg, #10b981, #059669);
  display: flex; align-items: center; justify-content: center;
  margin: 0 auto;
  box-shadow: 0 4px 20px rgba(16,185,129,.4);
}
.pdv-comprovante-valores {
  background: #f8fafc;
  border-radius: 12px;
  padding: 12px 16px;
  border: 1px solid #e2e8f0;
}
.pdv-cv-linha {
  display: flex; justify-content: space-between; align-items: center;
  font-size: 14px; padding: 4px 0;
}

/* Atalhos modal */
.atalhos-grid { display: flex; flex-direction: column; gap: 6px; }
.atalho-linha { display: flex; align-items: center; gap: 12px; }
.pdv-passos { padding-left: 1.3rem; margin: 0; font-size: 13px; line-height: 1.5; color: #334155; }
.pdv-passos li { margin-bottom: 4px; }
.pdv-passos li::marker { font-weight: 700; color: rgb(var(--v-theme-primary)); }
.pdv-passos kbd {
  background: #f1f5f9; border: 1px solid #e2e8f0; border-radius: 4px;
  padding: 0 5px; font-family: monospace; font-size: 11px; font-weight: 700;
}
.atalho-key {
  min-width: 60px; text-align: center;
  background: #f8fafc; border: 1px solid #e2e8f0;
  border-radius: 6px; padding: 3px 8px;
  font-family: monospace; font-size: 11px; font-weight: 700; color: #334155;
  flex-shrink: 0;
}

/* ── Faixa de promoções ── */
.pdv-promo-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 0 18px;
  height: 46px;
  background: linear-gradient(90deg, #f59e0b 0%, #f97316 50%, #ea580c 100%);
  background-size: 200% 100%;
  animation: pdv-promo-shimmer 6s linear infinite;
  border-bottom: 2px solid #c2410c;
  box-shadow: 0 2px 6px rgba(234,88,12,.35);
  flex-shrink: 0;
  overflow: hidden;
}
@keyframes pdv-promo-shimmer {
  0% { background-position: 0% 0; }
  100% { background-position: 200% 0; }
}
.pdv-promo-icon {
  color: #fff;
  flex-shrink: 0;
  animation: pdv-promo-pulse 1.6s ease-in-out infinite;
}
@keyframes pdv-promo-pulse {
  0%, 100% { transform: scale(1); opacity: 1; }
  50% { transform: scale(1.25); opacity: .6; }
}
.pdv-promo-viewport {
  flex: 1;
  overflow: hidden;
}
.pdv-promo-texto {
  display: block;
  font-size: 16px;
  font-weight: 600;
  color: #fff;
  text-shadow: 0 1px 2px rgba(0,0,0,.25);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  letter-spacing: .2px;
}
.pdv-promo-texto strong {
  color: #fff;
  font-weight: 900;
  text-transform: uppercase;
  margin-right: 6px;
}
.pdv-promo-dots {
  display: flex;
  align-items: center;
  gap: 5px;
  flex-shrink: 0;
}
.pdv-promo-dot {
  width: 7px; height: 7px;
  border-radius: 50%;
  background: rgba(255,255,255,.45);
  transition: all .3s ease;
}
.pdv-promo-dot.ativo {
  background: #fff;
  width: 18px;
  border-radius: 4px;
}
.pdv-promo-dismiss {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border: none;
  background: rgba(255,255,255,.18);
  color: #fff;
  cursor: pointer;
  border-radius: 5px;
  flex-shrink: 0;
  opacity: .85;
  transition: all .12s;
}
.pdv-promo-dismiss:hover { opacity: 1; background: rgba(255,255,255,.35); }

/* Fade entre mensagens do banner */
.pdv-promo-fade-enter-active { transition: opacity .4s ease, transform .4s ease; }
.pdv-promo-fade-leave-active { transition: opacity .3s ease, transform .3s ease; }
.pdv-promo-fade-enter-from { opacity: 0; transform: translateY(60%); }
.pdv-promo-fade-leave-to { opacity: 0; transform: translateY(-60%); }

.pdv-promo-slide-enter-active { transition: all .2s ease; }
.pdv-promo-slide-leave-active { transition: all .15s ease; }
.pdv-promo-slide-enter-from, .pdv-promo-slide-leave-to {
  height: 0; opacity: 0; padding-top: 0; padding-bottom: 0;
}

/* ── Scanner ── */
.pdv-scanner-wrap {
  position: relative;
  background: #000;
  aspect-ratio: 4/3;
  overflow: hidden;
}
.pdv-scanner-video {
  width: 100%; height: 100%;
  object-fit: cover;
  display: block;
}
.pdv-scanner-overlay {
  position: absolute; inset: 0;
  display: flex; align-items: center; justify-content: center;
}
.pdv-scanner-frame {
  width: 220px; height: 140px;
  border: 2.5px solid rgba(255,255,255,.85);
  border-radius: 10px;
  box-shadow: 0 0 0 9999px rgba(0,0,0,.45);
  position: relative;
}
.pdv-scanner-frame::before, .pdv-scanner-frame::after {
  content: '';
  position: absolute;
  width: 28px; height: 28px;
  border-color: #3b82f6;
  border-style: solid;
}
.pdv-scanner-frame::before { top: -2px; left: -2px; border-width: 3px 0 0 3px; border-radius: 6px 0 0 0; }
.pdv-scanner-frame::after  { bottom: -2px; right: -2px; border-width: 0 3px 3px 0; border-radius: 0 0 6px 0; }
.pdv-scanner-status {
  position: absolute; bottom: 12px; left: 0; right: 0;
  text-align: center; font-size: 12px; color: rgba(255,255,255,.8);
  text-shadow: 0 1px 3px rgba(0,0,0,.8);
}

/* ══ MOBILE RESPONSIVO ══════════════════════════════════════════ */
@media (max-width: 640px) {
  /* No celular o PDV ocupa a tela inteira (a app-bar global fica escondida) */
  .pdv-shell { height: 100dvh; min-height: 0; }

  /* Barra do PDV quebra em duas linhas: caixa/menu em cima, busca em baixo */
  .pdv-topbar { flex-wrap: wrap; height: auto; padding: 8px 10px; gap: 8px; }
  .pdv-brand { display: none; }              /* espaço para a busca */
  .pdv-topbar-right .pdv-info-pill { display: none; }

  /* Busca em linha própria, ocupando toda a largura e com toque confortável */
  .pdv-topbar-center { order: 3; flex: 1 0 100%; max-width: none; margin: 2px 0 0; }
  .pdv-search .v-field { min-height: 48px; border-radius: 10px !important; }
  .pdv-search input { font-size: 16px !important; }        /* evita zoom do iOS */
  .pdv-search .v-field__prepend-inner .v-icon { color: rgba(255,255,255,.7) !important; }

  /* Empilha colunas verticalmente */
  .pdv-body { flex-direction: column; overflow-y: auto; }

  .pdv-col-itens {
    flex: none;
    min-height: 0;
    overflow: visible;
  }
  .pdv-itens-wrap { max-height: 45dvh; overflow-y: auto; }

  .pdv-col-pag {
    width: 100%;
    border-left: none;
    border-top: 1px solid #e2e8f0;
    overflow: visible;
    flex: none;
  }

  /* Itens com touch mais fácil */
  .pdv-item { padding: 13px 14px; gap: 10px; }
  .pdv-qtd-btn { width: 36px; height: 36px; font-size: 18px; }
  .pdv-qtd-input { height: 36px; font-size: 14px; }
  .pdv-item-qtd { width: 112px; }

  /* Formas de pagamento em grid 2x2 mais generoso */
  .pdv-fp-btn { padding: 16px 6px; font-size: 12px; }

  /* Botão Finalizar maior para toque, fixo no rodapé para sempre poder finalizar */
  .pdv-btn-finalizar { height: 60px; font-size: 17px; }
  .pdv-acoes {
    position: sticky;
    bottom: 0;
    background: #ffffff;
    padding: 12px 16px calc(12px + env(safe-area-inset-bottom));
    box-shadow: 0 -6px 16px rgba(15, 23, 42, .1);
    z-index: 6;
  }
  .pdv-btn-cancelar { margin-top: 8px; }

  /* Totais compactos */
  .pdv-total-valor { font-size: 24px; }

  /* Ocultar atalhos de teclado no mobile */
  .pdv-kbd, .pdv-kbd-fp, .pdv-kbd-big { display: none; }

  /* Hint chips menores */
  .pdv-vazio-titulo { font-size: 14px; }
  .pdv-vazio-sub { font-size: 12px; }
}

/* Transições */
.fade-enter-active, .fade-leave-active { transition: opacity .3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
.slide-item-enter-active { transition: all .18s ease; }
.slide-item-leave-active { transition: all .14s ease; }
.slide-item-enter-from { opacity: 0; transform: translateX(-8px); }
.slide-item-leave-to   { opacity: 0; transform: translateX(8px); }
</style>
 