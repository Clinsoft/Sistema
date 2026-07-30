<template>
  <div>
    <v-row class="mb-2">
      <v-col>
        <div class="text-h6 font-weight-bold">Bom dia, {{ auth.usuario?.nome?.split(' ')?.[0] }}!</div>
        <div class="text-body-2 text-medium-emphasis">{{ dataHoje }}</div>
      </v-col>
    </v-row>

    <!-- Cards de resumo -->
    <v-row>
      <v-col v-for="card in cards" :key="card.titulo" cols="12" sm="6" md="3">
        <v-card rounded="xl" elevation="1" :to="card.to">
          <v-card-text class="d-flex align-center pa-4">
            <v-avatar :color="card.cor" size="52" class="mr-4">
              <v-icon :icon="card.icon" color="white" size="28" />
            </v-avatar>
            <div>
              <div class="text-h5 font-weight-bold">{{ card.valor }}</div>
              <div class="text-caption text-medium-emphasis">{{ card.titulo }}</div>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Indicadores: Margem de Contribuição + CMV + Curva ABC -->
    <v-row class="mt-3">
      <v-col cols="12" md="4">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="d-flex align-center pa-4">
            <v-avatar color="deep-purple" size="48" class="mr-3">
              <v-icon icon="mdi-percent-outline" color="white" size="26" />
            </v-avatar>
            <div class="flex-grow-1">
              <div class="text-caption text-medium-emphasis">Índice de Margem de Contribuição — {{ calMesLabel }}</div>
              <div class="text-h5 font-weight-bold text-deep-purple">
                {{ pe ? pe.percentualMargemContribuicao + '%' : '--' }}
              </div>
              <div class="text-caption text-medium-emphasis">
                {{ pe ? fmt(pe.margemContribuicao) + ' de contribuição' : 'sem vendas no mês' }}
              </div>
            </div>
            <v-progress-circular v-if="pe" :model-value="Math.min(pe.percentualMargemContribuicao, 100)"
              :size="52" :width="6" color="deep-purple">
              <span class="text-caption font-weight-bold">{{ Math.round(pe.percentualMargemContribuicao) }}%</span>
            </v-progress-circular>
          </v-card-text>
        </v-card>
      </v-col>

      <v-col cols="12" md="4">
        <v-card rounded="xl" elevation="1">
          <v-card-text class="d-flex align-center pa-4">
            <v-avatar color="orange-darken-2" size="48" class="mr-3">
              <v-icon icon="mdi-cart-arrow-down" color="white" size="26" />
            </v-avatar>
            <div class="flex-grow-1">
              <div class="text-caption text-medium-emphasis">Custo da Mercadoria Vendida (CMV) — {{ calMesLabel }}</div>
              <div class="text-h5 font-weight-bold text-orange-darken-2">
                {{ dre ? fmt(dre.cmv) : 'R$ --' }}
              </div>
              <div class="text-caption text-medium-emphasis">
                {{ dre && dre.receitaLiquida > 0
                  ? Math.round(dre.cmv / dre.receitaLiquida * 100) + '% da receita líquida'
                  : 'sem receita no mês' }}
              </div>
            </div>
            <v-progress-circular v-if="dre && dre.receitaLiquida > 0"
              :model-value="Math.min(dre.cmv / dre.receitaLiquida * 100, 100)"
              :size="52" :width="6" color="orange-darken-2">
              <span class="text-caption font-weight-bold">{{ Math.round(dre.cmv / dre.receitaLiquida * 100) }}%</span>
            </v-progress-circular>
          </v-card-text>
        </v-card>
      </v-col>

      <!-- Curva ABC — card pequeno -->
      <v-col cols="12" md="4">
        <v-card rounded="xl" elevation="1" to="/estoque/posicao">
          <v-card-text class="d-flex align-center pa-4">
            <v-avatar color="deep-orange" size="48" class="mr-3">
              <v-icon icon="mdi-chart-bar-stacked" color="white" size="26" />
            </v-avatar>
            <div class="flex-grow-1">
              <div class="text-caption text-medium-emphasis">Curva ABC de Produtos</div>
              <div class="text-body-2 font-weight-medium mt-1">
                {{ curvaAbc.length ? curvaAbc.length + ' produtos vendidos' : 'sem vendas no período' }}
              </div>
              <div v-if="curvaAbc.length" class="d-flex ga-1 mt-1">
                <v-chip size="x-small" color="success" variant="tonal" label>A: {{ abcResumo.A }}</v-chip>
                <v-chip size="x-small" color="warning" variant="tonal" label>B: {{ abcResumo.B }}</v-chip>
                <v-chip size="x-small" color="error" variant="tonal" label>C: {{ abcResumo.C }}</v-chip>
              </div>
            </div>
            <v-icon icon="mdi-chevron-right" color="grey" />
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Vendas + Contas a Pagar (agendas do mês, lado a lado) -->
    <v-row class="mt-3">
      <v-col cols="12" md="6">
        <v-card rounded="xl" elevation="1" height="100%">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-calendar-month-outline" class="mr-2" color="success" />
            Vendas — {{ calMesLabel }}
            <v-btn icon="mdi-chevron-left" size="x-small" variant="text" density="comfortable" class="ml-1" @click="mudarMes(-1)" />
            <v-btn icon="mdi-chevron-right" size="x-small" variant="text" density="comfortable" @click="mudarMes(1)" />
            <v-spacer />
            <v-chip v-if="totalMesVendas > 0" color="success" size="small" label>{{ fmt(totalMesVendas) }}</v-chip>
          </v-card-title>
          <v-card-text class="pt-1">
            <div v-if="carregando" class="d-flex justify-center pa-6">
              <v-progress-circular indeterminate color="success" />
            </div>
            <template v-else>
              <div class="dash-cal-head">
                <span v-for="(d, i) in ['D','S','T','Q','Q','S','S']" :key="i">{{ d }}</span>
              </div>
              <div class="dash-cal-grid">
                <div v-for="(cel, i) in celasVendas" :key="i" class="dash-cal-cell"
                  :class="{
                    'dash-cal-empty': !cel.dia,
                    'dash-cal-hoje': cel.dia === diaHoje && mesOffset === 0,
                    'dash-cal-vend': cel.total > 0,
                    'dash-cal-sel-v': cel.dia === diaSelecionadoV && cel.total > 0,
                  }"
                  @click="cel.total > 0 && (diaSelecionadoV = cel.dia)">
                  <template v-if="cel.dia">
                    <div class="dash-cal-num">{{ cel.dia }}</div>
                    <div v-if="cel.total > 0" class="dash-cal-val dash-cal-val-v">{{ fmtCel(cel.total) }}</div>
                  </template>
                </div>
              </div>

              <div v-if="vendasDoDia" class="mt-2 d-flex align-center dash-cal-resumo">
                <div class="text-caption font-weight-bold">
                  {{ String(diaSelecionadoV).padStart(2, '0') }}/{{ String(mesNum).padStart(2, '0') }}
                </div>
                <v-spacer />
                <v-chip size="small" color="success" variant="tonal" label class="mr-1">
                  {{ vendasDoDia.qtd }} venda{{ vendasDoDia.qtd !== 1 ? 's' : '' }}
                </v-chip>
                <span class="text-body-2 font-weight-bold text-success">{{ fmt(vendasDoDia.total) }}</span>
                <span class="text-caption text-medium-emphasis ml-2">TM {{ fmt(vendasDoDia.ticket) }}</span>
              </div>
              <div v-else class="text-caption text-medium-emphasis text-center mt-2">
                Toque num dia com venda. Sem vendas em {{ String(diaSelecionadoV).padStart(2,'0') }}/{{ String(mesNum).padStart(2,'0') }}.
              </div>
            </template>
          </v-card-text>

          <v-card-actions class="pa-2 pt-0">
            <v-btn variant="text" size="small" color="success" append-icon="mdi-arrow-right"
              :to="vendasDoDia
                ? { path: '/pdv/vendas', query: { data: dataSelecionadaISOV } }
                : '/pdv/vendas'">
              {{ vendasDoDia
                ? `Ver dia ${String(diaSelecionadoV).padStart(2,'0')}/${String(mesNum).padStart(2,'0')}`
                : 'Ver todas' }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-col>

      <!-- Contas a Pagar — Agenda do mês -->
      <v-col cols="12" md="6">
        <v-card rounded="xl" elevation="1" height="100%">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-calendar-month-outline" class="mr-2" color="error" />
            Contas a Pagar — {{ calMesLabel }}
            <v-btn icon="mdi-chevron-left" size="x-small" variant="text" density="comfortable" class="ml-1" @click="mudarMes(-1)" />
            <v-btn icon="mdi-chevron-right" size="x-small" variant="text" density="comfortable" @click="mudarMes(1)" />
            <v-spacer />
            <v-chip v-if="totalMesPagar > 0" color="error" size="small" label>{{ fmt(totalMesPagar) }}</v-chip>
          </v-card-title>

          <v-card-text class="pt-1">
            <div v-if="carregandoCP" class="d-flex justify-center pa-6">
              <v-progress-circular indeterminate color="error" />
            </div>
            <template v-else>
              <div class="dash-cal-head">
                <span v-for="(d, i) in ['D','S','T','Q','Q','S','S']" :key="i">{{ d }}</span>
              </div>
              <div class="dash-cal-grid">
                <div v-for="(cel, i) in celasCalendario" :key="i" class="dash-cal-cell"
                  :class="{
                    'dash-cal-empty': !cel.dia,
                    'dash-cal-hoje': cel.dia === diaHoje && mesOffset === 0,
                    'dash-cal-tem': cel.total > 0,
                    'dash-cal-venc': cel.vencido,
                    'dash-cal-sel': cel.dia === diaSelecionado && cel.total > 0,
                  }"
                  @click="cel.total > 0 && (diaSelecionado = cel.dia)">
                  <template v-if="cel.dia">
                    <div class="dash-cal-num">{{ cel.dia }}</div>
                    <div v-if="cel.total > 0" class="dash-cal-val">{{ fmtCel(cel.total) }}</div>
                  </template>
                </div>
              </div>

              <div v-if="contasDoDia.length" class="mt-2">
                <div class="text-caption font-weight-bold mb-1">
                  {{ String(diaSelecionado).padStart(2, '0') }}/{{ String(mesNum).padStart(2, '0') }} —
                  {{ contasDoDia.length }} conta(s)
                </div>
                <v-list density="compact" class="pa-0" style="max-height: 120px; overflow-y: auto">
                  <v-list-item v-for="c in contasDoDia" :key="c.id" class="px-2" min-height="34">
                    <template #prepend>
                      <v-icon :icon="c.vencido ? 'mdi-alert-circle' : 'mdi-clock-outline'"
                        :color="c.vencido ? 'error' : 'warning'" size="16" class="mr-1" />
                    </template>
                    <v-list-item-title class="text-caption">{{ c.descricao }}</v-list-item-title>
                    <template #append>
                      <span class="text-caption font-weight-bold text-error">{{ fmt(c.saldo) }}</span>
                    </template>
                  </v-list-item>
                </v-list>
              </div>
              <div v-else class="text-caption text-medium-emphasis text-center mt-2">
                Toque num dia com valor para ver as contas. Nada a pagar em {{ String(diaSelecionado).padStart(2,'0') }}/{{ String(mesNum).padStart(2,'0') }}.
              </div>
            </template>
          </v-card-text>

          <v-card-actions class="pa-2 pt-0">
            <v-btn variant="text" size="small" color="error" append-icon="mdi-arrow-right"
              :to="contasDoDia.length
                ? { path: '/financeiro/contas-pagar', query: { data: dataSelecionadaISO } }
                : '/financeiro/contas-pagar'">
              {{ contasDoDia.length
                ? `Ver dia ${String(diaSelecionado).padStart(2,'0')}/${String(mesNum).padStart(2,'0')}`
                : 'Ver todas' }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-col>

      <!-- Contas a Receber — Agenda do mês -->
      <v-col cols="12" md="6">
        <v-card rounded="xl" elevation="1" height="100%">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-calendar-month-outline" class="mr-2" color="success" />
            Contas a Receber — {{ calMesLabel }}
            <v-btn icon="mdi-chevron-left" size="x-small" variant="text" density="comfortable" class="ml-1" @click="mudarMes(-1)" />
            <v-btn icon="mdi-chevron-right" size="x-small" variant="text" density="comfortable" @click="mudarMes(1)" />
            <v-spacer />
            <v-chip v-if="totalMesReceber > 0" color="success" size="small" label>{{ fmt(totalMesReceber) }}</v-chip>
          </v-card-title>

          <v-card-text class="pt-1">
            <div v-if="carregandoCR" class="d-flex justify-center pa-6">
              <v-progress-circular indeterminate color="success" />
            </div>
            <template v-else>
              <div class="dash-cal-head">
                <span v-for="(d, i) in ['D','S','T','Q','Q','S','S']" :key="i">{{ d }}</span>
              </div>
              <div class="dash-cal-grid">
                <div v-for="(cel, i) in celasReceber" :key="i" class="dash-cal-cell"
                  :class="{
                    'dash-cal-empty': !cel.dia,
                    'dash-cal-hoje': cel.dia === diaHoje && mesOffset === 0,
                    'dash-cal-vend': cel.total > 0,
                    'dash-cal-sel-v': cel.dia === diaSelecionadoR && cel.total > 0,
                  }"
                  @click="cel.total > 0 && (diaSelecionadoR = cel.dia)">
                  <template v-if="cel.dia">
                    <div class="dash-cal-num">{{ cel.dia }}</div>
                    <div v-if="cel.total > 0" class="dash-cal-val dash-cal-val-v">{{ fmtCel(cel.total) }}</div>
                  </template>
                </div>
              </div>

              <div v-if="receberDoDia.length" class="mt-2">
                <div class="text-caption font-weight-bold mb-1">
                  {{ String(diaSelecionadoR).padStart(2, '0') }}/{{ String(mesNum).padStart(2, '0') }} —
                  {{ receberDoDia.length }} conta(s)
                </div>
                <v-list density="compact" class="pa-0" style="max-height: 120px; overflow-y: auto">
                  <v-list-item v-for="c in receberDoDia" :key="c.id" class="px-2" min-height="34">
                    <template #prepend>
                      <v-icon :icon="c.vencido ? 'mdi-alert-circle' : 'mdi-clock-outline'"
                        :color="c.vencido ? 'error' : 'success'" size="16" class="mr-1" />
                    </template>
                    <v-list-item-title class="text-caption">{{ c.descricao }}</v-list-item-title>
                    <template #append>
                      <span class="text-caption font-weight-bold text-success">{{ fmt(c.saldo) }}</span>
                    </template>
                  </v-list-item>
                </v-list>
              </div>
              <div v-else class="text-caption text-medium-emphasis text-center mt-2">
                Toque num dia com valor para ver as contas. Nada a receber em {{ String(diaSelecionadoR).padStart(2,'0') }}/{{ String(mesNum).padStart(2,'0') }}.
              </div>
            </template>
          </v-card-text>

          <v-card-actions class="pa-2 pt-0">
            <v-btn variant="text" size="small" color="success" append-icon="mdi-arrow-right"
              :to="receberDoDia.length
                ? { path: '/financeiro/contas-receber', query: { data: dataSelecionadaISOR } }
                : '/financeiro/contas-receber'">
              {{ receberDoDia.length
                ? `Ver dia ${String(diaSelecionadoR).padStart(2,'0')}/${String(mesNum).padStart(2,'0')}`
                : 'Ver todas' }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-col>

      <!-- Ponto de Equilíbrio + DRE (empilhados, ao lado do Contas a Receber) -->
      <v-col cols="12" md="6">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-chart-donut" class="mr-2" color="deep-purple" />
            Ponto de Equilíbrio — {{ calMesLabel }}
            <v-btn icon="mdi-chevron-left" size="x-small" variant="text" density="comfortable" class="ml-1" @click="mudarMes(-1)" />
            <v-btn icon="mdi-chevron-right" size="x-small" variant="text" density="comfortable" @click="mudarMes(1)" />
            <v-spacer />
            <v-chip v-if="pe" :color="pe.peAtingido ? 'success' : 'warning'" size="small" label>
              {{ pe.peAtingido ? 'Atingido ✓' : pe.percentualAtingido + '% do PE' }}
            </v-chip>
          </v-card-title>

          <v-card-text v-if="carregandoPe" class="d-flex justify-center pa-8">
            <v-progress-circular indeterminate color="deep-purple" />
          </v-card-text>

          <v-card-text v-else-if="!pe || pe.pontoEquilibrio === 0" class="text-center text-medium-emphasis pa-6">
            <v-icon icon="mdi-information-outline" class="mb-2" size="32" />
            <div v-if="!pe || pe.totalCustosFixos === 0" class="text-body-2">
              Cadastre <strong>contas a pagar</strong> do mês para calcular o ponto de equilíbrio.
            </div>
            <div v-else class="text-body-2">
              Sem <strong>vendas</strong> para calcular a margem de contribuição. Registre vendas
              (ou tenha histórico dos últimos 90 dias) para projetar o ponto de equilíbrio.
            </div>
            <v-btn class="mt-3" size="small" variant="tonal" color="deep-purple"
              :to="!pe || pe.totalCustosFixos === 0 ? '/financeiro/contas-pagar' : '/pdv'">
              {{ !pe || pe.totalCustosFixos === 0 ? 'Ver contas a pagar' : 'Ir para o PDV' }}
            </v-btn>
          </v-card-text>

          <v-card-text v-else class="pb-2">
            <div class="d-flex justify-space-between text-caption text-medium-emphasis mb-1">
              <span>Faturamento acumulado</span>
              <span class="font-weight-bold">{{ fmt(pe.faturamentoMes) }} / {{ fmt(pe.pontoEquilibrio) }}</span>
            </div>
            <v-progress-linear
              :model-value="Math.min(pe.percentualAtingido, 100)"
              :color="pe.peAtingido ? 'success' : pe.percentualAtingido >= 75 ? 'warning' : 'deep-purple'"
              height="22" rounded class="mb-4"
            >
              <template #default>
                <span class="text-caption font-weight-bold" style="color:white">{{ pe.percentualAtingido }}%</span>
              </template>
            </v-progress-linear>

            <!-- Indicadores em grade 2x2 -->
            <v-row dense class="mb-3">
              <v-col cols="6">
                <div class="pe-stat">
                  <div class="pe-stat-lbl">Contas a pagar (mês)</div>
                  <div class="pe-stat-val text-error">{{ fmt(pe.totalCustosFixos) }}</div>
                </div>
              </v-col>
              <v-col cols="6">
                <div class="pe-stat">
                  <div class="pe-stat-lbl">
                    Margem de contribuição
                    <span v-if="pe.margemEstimada" class="text-warning" title="Estimada pelos últimos 90 dias (mês sem vendas)">*</span>
                  </div>
                  <div class="pe-stat-val text-deep-purple">{{ pe.percentualMargemContribuicao }}%</div>
                  <div v-if="pe.margemEstimada" class="text-caption text-warning" style="font-size:.6rem;line-height:1">
                    estimada (90 dias)
                  </div>
                </div>
              </v-col>
              <v-col cols="6">
                <div class="pe-stat">
                  <div class="pe-stat-lbl">PE calculado</div>
                  <div class="pe-stat-val">{{ fmt(pe.pontoEquilibrio) }}</div>
                </div>
              </v-col>
              <v-col cols="6">
                <div class="pe-stat" :class="pe.peAtingido ? 'pe-stat--ok' : 'pe-stat--warn'">
                  <div class="pe-stat-lbl">{{ pe.peAtingido ? 'Lucro acima do PE' : 'Falta atingir' }}</div>
                  <div class="pe-stat-val" :class="pe.peAtingido ? 'text-success' : 'text-warning'">
                    {{ pe.peAtingido ? '+' + fmt(pe.lucroAcimaPE) : fmt(pe.pontoEquilibrio - pe.faturamentoMes) }}
                  </div>
                </div>
              </v-col>
            </v-row>

            <canvas ref="peCanvas" height="90" style="width:100%" />
          </v-card-text>
        </v-card>

        <!-- DRE — abaixo do Ponto de Equilíbrio, mesmo tratamento visual -->
        <v-card rounded="xl" elevation="1" class="mt-3">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-finance" class="mr-2" color="indigo" />
            DRE — {{ calMesLabel }}
            <v-btn icon="mdi-chevron-left" size="x-small" variant="text" density="comfortable" class="ml-1" @click="mudarMes(-1)" />
            <v-btn icon="mdi-chevron-right" size="x-small" variant="text" density="comfortable" @click="mudarMes(1)" />
            <v-spacer />
            <v-chip v-if="dre" :color="dre.resultadoOperacional >= 0 ? 'success' : 'error'" size="small" label>
              {{ dre.resultadoOperacional >= 0 ? '+' : '' }}{{ fmt(dre.resultadoOperacional) }}
            </v-chip>
          </v-card-title>

          <v-card-text v-if="carregandoDre" class="d-flex justify-center pa-8">
            <v-progress-circular indeterminate color="indigo" />
          </v-card-text>

          <v-card-text v-else-if="!dre" class="text-center text-medium-emphasis pa-6">
            <v-icon icon="mdi-chart-line-variant" size="36" class="mb-2" />
            <div class="text-body-2">Sem dados para o período.</div>
          </v-card-text>

          <v-card-text v-else class="pb-2">
            <!-- Indicadores em grade 2x2 -->
            <v-row dense class="mb-3">
              <v-col cols="6">
                <div class="pe-stat">
                  <div class="pe-stat-lbl">Receita líquida</div>
                  <div class="pe-stat-val text-indigo">{{ fmt(dre.receitaLiquida) }}</div>
                </div>
              </v-col>
              <v-col cols="6">
                <div class="pe-stat">
                  <div class="pe-stat-lbl">CMV</div>
                  <div class="pe-stat-val text-warning">{{ fmt(dre.cmv) }}</div>
                </div>
              </v-col>
              <v-col cols="6">
                <div class="pe-stat">
                  <div class="pe-stat-lbl">Margem bruta</div>
                  <div class="pe-stat-val text-deep-purple">{{ dre.margemBruta }}%</div>
                </div>
              </v-col>
              <v-col cols="6">
                <div class="pe-stat" :class="dre.resultadoOperacional >= 0 ? 'pe-stat--ok' : 'pe-stat--warn'">
                  <div class="pe-stat-lbl">Resultado operacional</div>
                  <div class="pe-stat-val" :class="dre.resultadoOperacional >= 0 ? 'text-success' : 'text-error'">
                    {{ dre.resultadoOperacional >= 0 ? '+' : '' }}{{ fmt(dre.resultadoOperacional) }}
                  </div>
                </div>
              </v-col>
            </v-row>

            <canvas ref="dreCanvas" height="80" style="width:100%" />
          </v-card-text>

          <v-card-actions class="pa-2 pt-0">
            <v-btn variant="text" size="small" color="indigo" to="/financeiro/dre" append-icon="mdi-arrow-right">
              Ver DRE completo
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>

    <!-- Planejamento + Vendas por Colaborador -->
    <v-row class="mt-2">
      <v-col cols="12" md="6">
        <v-card rounded="xl" elevation="1" style="display:flex;flex-direction:column">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-calendar-month-outline" class="mr-2" color="blue-darken-2" />
            Planejamento — {{ anoAtual }}
            <v-spacer />
            <v-btn size="x-small" variant="tonal" color="blue-darken-2" to="/relatorios/planejamento-anual">
              Ver
            </v-btn>
          </v-card-title>

          <v-card-text v-if="!planejamento" class="text-center text-medium-emphasis flex-grow-1 d-flex flex-column justify-center pa-6">
            <v-icon icon="mdi-chart-timeline-variant-shimmer" size="36" class="mb-2" color="blue-lighten-3" />
            <div class="text-body-2">Nenhum planejamento para {{ anoAtual }}.</div>
            <v-btn class="mt-3" size="small" variant="tonal" color="blue-darken-2" to="/relatorios/planejamento-anual">
              Criar Planejamento
            </v-btn>
          </v-card-text>

          <div v-else class="pa-3 pt-0 flex-grow-1">
            <div v-for="item in planejamento.meses" :key="item.mes" class="mb-2">
              <div class="d-flex align-center mb-1">
                <span class="text-caption text-medium-emphasis flex-grow-1">{{ item.nomeMes }}</span>
                <span class="text-caption font-weight-bold"
                  :class="item.realizado >= item.meta ? 'text-success' : item.mes <= mesAtualNum ? 'text-warning' : ''">
                  {{ item.realizado > 0 ? fmt(item.realizado) : '—' }}
                </span>
              </div>
              <v-progress-linear
                :model-value="item.meta > 0 ? Math.min((item.realizado / item.meta) * 100, 100) : 0"
                :color="item.realizado >= item.meta ? 'success' : item.mes <= mesAtualNum ? 'warning' : 'blue-lighten-3'"
                height="5" rounded
              />
            </div>
            <v-divider class="my-2" />
            <div class="d-flex justify-space-between text-caption">
              <span class="text-medium-emphasis">Realizado YTD</span>
              <span class="font-weight-bold text-blue-darken-2">{{ fmt(planejamento.totalRealizado) }}</span>
            </div>
          </div>
        </v-card>
      </v-col>

      <!-- Vendas por Colaborador -->
      <v-col cols="12" md="6">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-account-group-outline" class="mr-2" color="teal" />
            Vendas por Colaborador — {{ calMesLabel }}
            <v-btn icon="mdi-chevron-left" size="x-small" variant="text" density="comfortable" class="ml-1" @click="mudarMes(-1)" />
            <v-btn icon="mdi-chevron-right" size="x-small" variant="text" density="comfortable" @click="mudarMes(1)" />
            <v-spacer />
            <v-btn icon size="x-small" variant="text" @click="periodoColaborador = periodoColaborador === 'mes' ? 'hoje' : 'mes'">
              <v-icon>{{ periodoColaborador === 'mes' ? 'mdi-calendar-month' : 'mdi-calendar-today' }}</v-icon>
              <v-tooltip activator="parent">{{ periodoColaborador === 'mes' ? 'Ver hoje' : 'Ver mês' }}</v-tooltip>
            </v-btn>
          </v-card-title>

          <v-card-text v-if="carregandoColab" class="d-flex justify-center pa-8">
            <v-progress-circular indeterminate color="teal" />
          </v-card-text>

          <div v-else-if="vendasColaborador.length === 0" class="text-center text-medium-emphasis pa-6">
            <v-icon icon="mdi-account-off-outline" size="36" class="mb-2" />
            <div class="text-body-2">Nenhuma venda registrada no período.</div>
          </div>

          <div v-else class="pa-3 pt-0">
            <div
              v-for="(v, i) in vendasColaborador"
              :key="v.usuarioId"
              class="mb-3"
            >
              <div class="d-flex align-center mb-1">
                <v-avatar :color="coresColab[i % coresColab.length]" size="28" class="mr-2">
                  <span class="text-caption font-weight-bold text-white">
                    {{ iniciais(v.nome) }}
                  </span>
                </v-avatar>
                <span class="text-body-2 font-weight-medium flex-grow-1">{{ v.nome }}</span>
                <span class="text-body-2 font-weight-bold ml-2">{{ fmt(v.totalVendido) }}</span>
              </div>
              <div class="d-flex align-center gap-2">
                <v-progress-linear
                  :model-value="(v.totalVendido / vendasColaborador[0].totalVendido) * 100"
                  :color="coresColab[i % coresColab.length]"
                  height="6"
                  rounded
                  class="flex-grow-1"
                />
                <span class="text-caption text-medium-emphasis" style="min-width:44px;text-align:right">
                  {{ v.qtdVendas }} vda{{ v.qtdVendas !== 1 ? 's' : '' }}
                </span>
              </div>
            </div>

            <!-- Ticket médio total -->
            <v-divider class="my-2" />
            <div class="d-flex justify-space-between text-caption text-medium-emphasis">
              <span>Ticket médio geral</span>
              <span class="font-weight-bold text-teal">{{ fmt(ticketMedioGeral) }}</span>
            </div>
          </div>
        </v-card>
      </v-col>

    </v-row>

    <!-- Alertas -->
    <v-row v-if="alertas.length" class="mt-2">
      <v-col cols="12">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-0 text-body-1 font-weight-bold">Alertas</v-card-title>
          <v-list density="compact" class="pa-2">
            <v-list-item
              v-for="(a, i) in alertas"
              :key="i"
              :prepend-icon="a.icon"
              :title="a.texto"
              :subtitle="a.detalhe"
              :base-color="a.cor"
            />
          </v-list>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, nextTick } from 'vue'
import { useAuthStore } from '@/stores/auth'
import api from '@/composables/useApi'

const auth = useAuthStore()
const peCanvas = ref<HTMLCanvasElement>()
const dreCanvas = ref<HTMLCanvasElement>()
const carregando = ref(true)

const dataHoje = new Date().toLocaleDateString('pt-BR', {
  weekday: 'long', year: 'numeric', month: 'long', day: 'numeric'
})
const mesAtual = new Date().toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' })
const mesAtualNum = new Date().getMonth() + 1
const anoAtual = new Date().getFullYear()

// ── Planejamento Anual ───────────────────────────────────────────────────────
const MESES_PT = ['Jan','Fev','Mar','Abr','Mai','Jun','Jul','Ago','Set','Out','Nov','Dez']
interface PlanejamentoData {
  totalMeta: number; totalRealizado: number
  meses: { mes: number; nomeMes: string; meta: number; realizado: number }[]
}
const planejamento = ref<PlanejamentoData | null>(null)

async function carregarPlanejamento() {
  if (!auth.empresaId) return
  try {
    const res = await api.get<PlanejamentoData>('/relatorios/planejamento-anual', {
      params: { empresaId: auth.empresaId, ano: anoAtual }
    })
    planejamento.value = res.data
  } catch { planejamento.value = null }
}

const fmt = (v: number) => 'R$ ' + (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const fmtData = (v: string) => v ? new Date(v).toLocaleDateString('pt-BR') : '-'
const iniciais = (nome: string) => nome.split(' ').slice(0, 2).map(p => p[0]).join('').toUpperCase()

const coresColab = ['teal', 'indigo', 'deep-purple', 'blue', 'cyan', 'green', 'orange']

interface ResumoData {
  vendasHoje: number
  pedidosAbertos: number
  aReceberVencido: number
  produtosSemEstoque: number
}
const resumo = ref<ResumoData | null>(null)

const cards = computed(() => [
  { titulo: 'Vendas hoje', valor: resumo.value ? fmt(resumo.value.vendasHoje) : 'R$ --', icon: 'mdi-cash-multiple', cor: 'success', to: '/pdv/vendas' },
  { titulo: 'Pedidos abertos', valor: resumo.value ? String(resumo.value.pedidosAbertos) : '--', icon: 'mdi-truck-delivery-outline', cor: 'info', to: '/compras' },
  { titulo: 'A receber (venc.)', valor: resumo.value ? fmt(resumo.value.aReceberVencido) : 'R$ --', icon: 'mdi-calendar-clock', cor: 'warning', to: '/financeiro/contas-receber' },
  { titulo: 'Produtos s/ estoque', valor: resumo.value ? String(resumo.value.produtosSemEstoque) : '--', icon: 'mdi-package-variant-remove', cor: 'error', to: '/estoque' },
])

async function carregarResumo() {
  if (!auth.empresaId) return
  try {
    const res = await api.get<ResumoData>('/dashboard/resumo', { params: { empresaId: auth.empresaId } })
    resumo.value = res.data
    montarAlertas()
  } catch { resumo.value = null }
}

function montarAlertas() {
  const r = resumo.value
  if (!r) { alertas.value = []; return }
  const lista: { icon: string; texto: string; detalhe: string; cor: string }[] = []
  if (r.produtosSemEstoque > 0)
    lista.push({ icon: 'mdi-package-variant-remove', cor: 'error',
      texto: `${r.produtosSemEstoque} produto(s) sem estoque`,
      detalhe: 'Verifique reposição em Estoque → Produtos.' })
  if (r.aReceberVencido > 0)
    lista.push({ icon: 'mdi-calendar-alert', cor: 'warning',
      texto: `${fmt(r.aReceberVencido)} a receber vencido`,
      detalhe: 'Cobre os títulos em atraso em Financeiro → Contas a Receber.' })
  if (r.pedidosAbertos > 0)
    lista.push({ icon: 'mdi-truck-delivery-outline', cor: 'info',
      texto: `${r.pedidosAbertos} pedido(s) de compra em aberto`,
      detalhe: 'Aguardando recebimento em Compras.' })
  alertas.value = lista
}


const alertas = ref<{ icon: string; texto: string; detalhe: string; cor: string }[]>([])

// ── Ponto de Equilíbrio ───────────────────────────────────────────────────────
interface PeData {
  totalCustosFixos: number
  receitaTotal: number
  margemContribuicao: number
  percentualMargemContribuicao: number
  margemEstimada?: boolean
  pontoEquilibrio: number
  faturamentoMes: number
  percentualAtingido: number
  peAtingido: boolean
  lucroAcimaPE: number
  detalhesCustosFixos: { categoria: string; total: number }[]
}

const pe = ref<PeData | null>(null)
const carregandoPe = ref(true)

async function carregarPe() {
  if (!auth.empresaId) { carregandoPe.value = false; return }
  try {
    const res = await api.get<PeData>('/financeiro/ponto-equilibrio', {
      params: { empresaId: auth.empresaId, ano: anoRef.value, mes: mesNum.value }
    })
    pe.value = res.data
    await nextTick()
    renderizarGraficoPe()
  } catch { /* estado vazio */ } finally {
    carregandoPe.value = false
  }
}

let _peTry = 0
function renderizarGraficoPe() {
  const canvas = peCanvas.value
  if (!canvas || !pe.value) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  // No load inicial o layout ainda não fluiu e offsetWidth=0; re-tenta no próximo frame.
  if (canvas.offsetWidth === 0) {
    if (_peTry++ < 30) requestAnimationFrame(renderizarGraficoPe)
    return
  }
  _peTry = 0
  const d = pe.value
  const cats = d.detalhesCustosFixos.length ? d.detalhesCustosFixos : [{ categoria: 'Custos Fixos', total: d.totalCustosFixos }]
  const labels = ['Faturamento', 'PE', ...cats.map(c => c.categoria)]
  const valores = [d.faturamentoMes, d.pontoEquilibrio, ...cats.map(c => c.total)]
  const cores = [d.peAtingido ? '#4caf50' : '#ab47bc', '#ef5350', ...cats.map(() => '#78909c')]
  const W = canvas.offsetWidth || 260; const H = 90
  canvas.width = W; canvas.height = H
  const barW = Math.floor((W - 20) / labels.length) - 4
  const maxV = Math.max(...valores, 1); const maxBarH = H - 28
  ctx.clearRect(0, 0, W, H)
  labels.forEach((label, i) => {
    const x = 10 + i * (barW + 4)
    const barH = Math.round((valores[i] / maxV) * maxBarH)
    ctx.fillStyle = cores[i]
    ctx.beginPath(); ctx.roundRect(x, H - 20 - barH, barW, barH, [3, 3, 0, 0]); ctx.fill()
    ctx.fillStyle = '#9e9e9e'; ctx.font = '8px sans-serif'; ctx.textAlign = 'center'
    ctx.fillText(label.length > 8 ? label.slice(0, 7) + '…' : label, x + barW / 2, H - 6)
  })
}

// ── Contas a Pagar — Agenda do mês ───────────────────────────────────────────
interface ContaPagar {
  id: string; descricao: string; saldo: number; dataVencimento: string
  status: string; parcela: number; totalParcelas: number; vencido: boolean
}
const contasMes = ref<ContaPagar[]>([])
const carregandoCP = ref(true)

// Mês exibido nos calendários (navegável com as setas). Os demais cards do
// dashboard continuam no mês atual fixo (mesAtual).
const _hoje = new Date()
const diaHoje = _hoje.getDate()
const mesOffset = ref(0)
const _refDate = computed(() => new Date(_hoje.getFullYear(), _hoje.getMonth() + mesOffset.value, 1))
const anoRef = computed(() => _refDate.value.getFullYear())
const mesRef = computed(() => _refDate.value.getMonth())
const mesNum = computed(() => mesRef.value + 1)
const calMesLabel = computed(() =>
  _refDate.value.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' }))
async function mudarMes(delta: number) {
  mesOffset.value += delta
  await Promise.all([
    carregarVendasMes(), carregarContasMes(), carregarReceberMes(),
    carregarPe(), carregarDre(), carregarVendasColaborador(),
  ])
}
const diaSelecionado = ref(diaHoje)
const dataSelecionadaISO = computed(() =>
  `${anoRef.value}-${String(mesNum.value).padStart(2, '0')}-${String(diaSelecionado.value).padStart(2, '0')}`)

const fmtMil = (v: number) =>
  v >= 1000 ? (v / 1000).toFixed(1).replace('.', ',') + 'k' : Math.round(v).toString()

// Valor exato (com centavos, sem "R$") para as células dos calendários — sem arredondar.
const fmtCel = (v: number) =>
  (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

const totalMesPagar = computed(() => contasMes.value.reduce((s, c) => s + c.saldo, 0))

// Agrupa por dia do mês { dia: { total, vencido, itens } }
const contasPorDia = computed(() => {
  const map: Record<number, { total: number; vencido: boolean; itens: ContaPagar[] }> = {}
  for (const c of contasMes.value) {
    const d = new Date(String(c.dataVencimento).slice(0, 10) + 'T12:00:00')
    if (d.getMonth() !== mesRef.value || d.getFullYear() !== anoRef.value) continue
    const dia = d.getDate()
    if (!map[dia]) map[dia] = { total: 0, vencido: false, itens: [] }
    map[dia].total += c.saldo
    map[dia].itens.push(c)
    if (c.vencido) map[dia].vencido = true
  }
  return map
})

// Células do calendário (com espaços em branco antes do 1º dia)
const celasCalendario = computed(() => {
  const primeiroDiaSemana = new Date(anoRef.value, mesRef.value, 1).getDay()   // 0=Dom
  const diasNoMes = new Date(anoRef.value, mesRef.value + 1, 0).getDate()
  const cells: { dia: number | null; total: number; vencido: boolean }[] = []
  for (let i = 0; i < primeiroDiaSemana; i++) cells.push({ dia: null, total: 0, vencido: false })
  for (let d = 1; d <= diasNoMes; d++) {
    const info = contasPorDia.value[d]
    cells.push({ dia: d, total: info?.total ?? 0, vencido: info?.vencido ?? false })
  }
  return cells
})

const contasDoDia = computed(() => contasPorDia.value[diaSelecionado.value]?.itens ?? [])

async function carregarContasMes() {
  if (!auth.empresaId) { carregandoCP.value = false; return }
  carregandoCP.value = true
  try {
    const inicio = new Date(anoRef.value, mesRef.value, 1).toISOString().slice(0, 10)
    const fim = new Date(anoRef.value, mesRef.value + 1, 0).toISOString().slice(0, 10)
    const res = await api.get<ContaPagar[]>('/contas-pagar', {
      params: { empresaId: auth.empresaId, inicio, fim }
    })
    // Só o que ainda falta pagar (em aberto / parcial), não cancelado nem quitado.
    contasMes.value = (res.data ?? []).filter(c => c.status !== 'Pago' && c.status !== 'Cancelado')
  } catch { contasMes.value = [] } finally { carregandoCP.value = false }
}

// ── Contas a Receber — Agenda do mês ─────────────────────────────────────────
interface ContaReceber {
  id: string; descricao: string; saldo: number; dataVencimento: string; status: string
}
const receberMes = ref<ContaReceber[]>([])
const carregandoCR = ref(true)
const diaSelecionadoR = ref(diaHoje)
const dataSelecionadaISOR = computed(() =>
  `${anoRef.value}-${String(mesNum.value).padStart(2, '0')}-${String(diaSelecionadoR.value).padStart(2, '0')}`)

const totalMesReceber = computed(() => receberMes.value.reduce((s, c) => s + c.saldo, 0))

const receberPorDia = computed(() => {
  const hojeStr = new Date().toISOString().slice(0, 10)
  const map: Record<number, { total: number; vencido: boolean; itens: (ContaReceber & { vencido: boolean })[] }> = {}
  for (const c of receberMes.value) {
    const d = new Date(String(c.dataVencimento).slice(0, 10) + 'T12:00:00')
    if (d.getMonth() !== mesRef.value || d.getFullYear() !== anoRef.value) continue
    const dia = d.getDate()
    const vencido = String(c.dataVencimento).slice(0, 10) < hojeStr && c.status !== 'Pago'
    if (!map[dia]) map[dia] = { total: 0, vencido: false, itens: [] }
    map[dia].total += c.saldo
    map[dia].itens.push({ ...c, vencido })
    if (vencido) map[dia].vencido = true
  }
  return map
})

const celasReceber = computed(() => {
  const primeiroDiaSemana = new Date(anoRef.value, mesRef.value, 1).getDay()
  const diasNoMes = new Date(anoRef.value, mesRef.value + 1, 0).getDate()
  const cells: { dia: number | null; total: number; vencido: boolean }[] = []
  for (let i = 0; i < primeiroDiaSemana; i++) cells.push({ dia: null, total: 0, vencido: false })
  for (let d = 1; d <= diasNoMes; d++) {
    const info = receberPorDia.value[d]
    cells.push({ dia: d, total: info?.total ?? 0, vencido: info?.vencido ?? false })
  }
  return cells
})

const receberDoDia = computed(() => receberPorDia.value[diaSelecionadoR.value]?.itens ?? [])

async function carregarReceberMes() {
  if (!auth.empresaId) { carregandoCR.value = false; return }
  carregandoCR.value = true
  try {
    const inicio = new Date(anoRef.value, mesRef.value, 1).toISOString().slice(0, 10)
    const fim = new Date(anoRef.value, mesRef.value + 1, 0).toISOString().slice(0, 10)
    const res = await api.get<ContaReceber[]>('/contas-receber', {
      params: { empresaId: auth.empresaId, inicio, fim }
    })
    receberMes.value = (res.data ?? []).filter(c => c.status !== 'Pago' && c.status !== 'Cancelado')
  } catch { receberMes.value = [] } finally { carregandoCR.value = false }
}

// ── Vendas por Colaborador ───────────────────────────────────────────────────
interface VendaColab {
  usuarioId: string; nome: string; qtdVendas: number
  totalVendido: number; ticketMedio: number
}
const vendasColaborador = ref<VendaColab[]>([])
const carregandoColab = ref(true)
const periodoColaborador = ref<'mes' | 'hoje'>('mes')

const ticketMedioGeral = computed(() => {
  const total = vendasColaborador.value.reduce((s, v) => s + v.totalVendido, 0)
  const qtd = vendasColaborador.value.reduce((s, v) => s + v.qtdVendas, 0)
  return qtd > 0 ? total / qtd : 0
})

async function carregarVendasColaborador() {
  if (!auth.empresaId) { carregandoColab.value = false; return }
  carregandoColab.value = true
  try {
    const hojeStr = new Date().toISOString().slice(0, 10)
    // "Hoje" só faz sentido no mês corrente; navegando para outro mês, usa o mês todo.
    const usarDia = periodoColaborador.value === 'hoje' && mesOffset.value === 0
    const inicio = usarDia
      ? hojeStr
      : new Date(anoRef.value, mesRef.value, 1).toISOString().slice(0, 10)
    const fim = mesOffset.value === 0
      ? hojeStr
      : new Date(anoRef.value, mesRef.value + 1, 0).toISOString().slice(0, 10)

    const [rankingRes, usuariosRes] = await Promise.all([
      api.get<{ usuarioId: string; qtdVendas: number; totalVendido: number; ticketMedio: number }[]>(
        '/relatorios/vendas/por-vendedor',
        { params: { empresaId: auth.empresaId, inicio, fim } }
      ),
      api.get<{ id: string; nome: string }[]>('/usuarios', { params: { empresaId: auth.empresaId } })
    ])

    const nomeMap = Object.fromEntries(usuariosRes.data.map(u => [u.id, u.nome]))
    vendasColaborador.value = rankingRes.data.map(r => ({
      ...r,
      nome: nomeMap[r.usuarioId] ?? 'Usuário #' + r.usuarioId.slice(0, 6)
    }))
  } catch { vendasColaborador.value = [] } finally { carregandoColab.value = false }
}

watch(periodoColaborador, carregarVendasColaborador)

// ── DRE do Mês ───────────────────────────────────────────────────────────────
interface DreData {
  receitaBruta: number; descontos: number; receitaLiquida: number
  cmv: number; lucroBruto: number; margemBruta: number
  despesasOperacionais: number; despesasPorCategoria: { categoria: string; total: number }[]
  resultadoOperacional: number; margemOperacional: number
}
const dre = ref<DreData | null>(null)
const carregandoDre = ref(true)

const linhasDre = computed(() => {
  if (!dre.value) return []
  const d = dre.value
  return [
    { label: 'Receita Bruta', valor: d.receitaBruta, cor: 'text-success', destaque: false },
    { label: '(-) Descontos', valor: -d.descontos, cor: 'text-error', destaque: false, indent: true },
    { label: 'Receita Líquida', valor: d.receitaLiquida, destaque: true, separador: true },
    { label: '(-) CMV', valor: -d.cmv, cor: 'text-warning', destaque: false, indent: true },
    { label: 'Lucro Bruto', valor: d.lucroBruto, destaque: true, separador: true },
    { label: '(-) Despesas Operac.', valor: -d.despesasOperacionais, cor: 'text-error', destaque: false, indent: true },
    { label: 'Resultado Operacional', valor: d.resultadoOperacional, cor: d.resultadoOperacional >= 0 ? 'text-success' : 'text-error', destaque: true, separador: true },
  ] as { label: string; valor: number; cor?: string; destaque?: boolean; separador?: boolean; indent?: boolean; prefixo?: string }[]
})

async function carregarDre() {
  if (!auth.empresaId) { carregandoDre.value = false; return }
  try {
    const res = await api.get<DreData>('/financeiro/dre', {
      params: { empresaId: auth.empresaId, ano: anoRef.value, mes: mesNum.value }
    })
    dre.value = res.data
    await nextTick()
    renderizarGraficoDre()
  } catch { /* vazio */ } finally { carregandoDre.value = false }
}

let _dreTry = 0
function renderizarGraficoDre() {
  const canvas = dreCanvas.value
  if (!canvas || !dre.value) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return
  if (canvas.offsetWidth === 0) {
    if (_dreTry++ < 30) requestAnimationFrame(renderizarGraficoDre)
    return
  }
  _dreTry = 0
  const d = dre.value
  const W = canvas.offsetWidth || 400; const H = 80
  canvas.width = W; canvas.height = H

  // Barras horizontais empilhadas: CMV + Lucro bruto = Receita líquida
  const receita = d.receitaLiquida || 1
  const cmvPct = Math.min(d.cmv / receita, 1)
  const lbPct = Math.max(0, Math.min(d.lucroBruto / receita, 1))
  const despPct = Math.min(d.despesasOperacionais / receita, 1)
  const resPos = d.resultadoOperacional >= 0

  const barH = 22; const labelW = 96; const barW = W - labelW - 8

  const drawBar = (y: number, label: string, segments: { pct: number; cor: string }[]) => {
    ctx.fillStyle = '#9e9e9e'; ctx.font = '10px sans-serif'; ctx.textAlign = 'right'
    ctx.fillText(label, labelW - 4, y + barH / 2 + 4)
    let x = labelW
    for (const seg of segments) {
      const w = Math.round(seg.pct * barW)
      if (w <= 0) continue
      ctx.fillStyle = seg.cor
      ctx.beginPath(); ctx.roundRect(x, y, w, barH, 4); ctx.fill()
      x += w
    }
  }

  drawBar(2, 'Receita líq.', [
    { pct: cmvPct, cor: '#ffa726' },
    { pct: lbPct, cor: '#66bb6a' },
  ])
  drawBar(30, 'Lucro bruto', [
    { pct: despPct, cor: '#ef5350' },
    { pct: Math.max(0, (d.lucroBruto - d.despesasOperacionais) / receita), cor: resPos ? '#26a69a' : '#ef9a9a' },
  ])

  // Legenda
  const legenda = [
    { cor: '#ffa726', label: 'CMV' }, { cor: '#66bb6a', label: 'Lucro bruto' },
    { cor: '#ef5350', label: 'Despesas' }, { cor: '#26a69a', label: 'Resultado' }
  ]
  ctx.font = '9px sans-serif'; ctx.textAlign = 'left'
  legenda.forEach((l, i) => {
    const lx = labelW + i * Math.floor(barW / 4)
    ctx.fillStyle = l.cor
    ctx.fillRect(lx, 58, 8, 8)
    ctx.fillStyle = '#9e9e9e'
    ctx.fillText(l.label, lx + 10, 67)
  })
}

// ── Vendas — Agenda do mês ───────────────────────────────────────────────────
interface VendaDia { data: string; qtdVendas: number; totalVendido: number; ticketMedio: number }
const vendasMes = ref<VendaDia[]>([])

const totalMesVendas = computed(() => vendasMes.value.reduce((s, v) => s + v.totalVendido, 0))

const vendasPorDia = computed(() => {
  const map: Record<number, number> = {}
  for (const v of vendasMes.value) {
    const d = new Date(String(v.data).slice(0, 10) + 'T12:00:00')
    if (d.getMonth() !== mesRef.value || d.getFullYear() !== anoRef.value) continue
    map[d.getDate()] = (map[d.getDate()] ?? 0) + v.totalVendido
  }
  return map
})

const celasVendas = computed(() => {
  const primeiroDiaSemana = new Date(anoRef.value, mesRef.value, 1).getDay()
  const diasNoMes = new Date(anoRef.value, mesRef.value + 1, 0).getDate()
  const cells: { dia: number | null; total: number }[] = []
  for (let i = 0; i < primeiroDiaSemana; i++) cells.push({ dia: null, total: 0 })
  for (let d = 1; d <= diasNoMes; d++) cells.push({ dia: d, total: vendasPorDia.value[d] ?? 0 })
  return cells
})

const diaSelecionadoV = ref(diaHoje)
const dataSelecionadaISOV = computed(() =>
  `${anoRef.value}-${String(mesNum.value).padStart(2, '0')}-${String(diaSelecionadoV.value).padStart(2, '0')}`)
const vendasDoDia = computed(() => {
  const entry = vendasMes.value.find(v => {
    const d = new Date(String(v.data).slice(0, 10) + 'T12:00:00')
    return d.getDate() === diaSelecionadoV.value && d.getMonth() === mesRef.value && d.getFullYear() === anoRef.value
  })
  return entry ? { qtd: entry.qtdVendas, total: entry.totalVendido, ticket: entry.ticketMedio } : null
})

async function carregarVendasMes() {
  carregando.value = true
  if (!auth.empresaId) { carregando.value = false; return }
  try {
    const inicio = new Date(anoRef.value, mesRef.value, 1).toISOString().slice(0, 10)
    const fim = new Date(anoRef.value, mesRef.value + 1, 0).toISOString().slice(0, 10)
    const res = await api.get<VendaDia[]>('/relatorios/vendas/diarias', {
      params: { empresaId: auth.empresaId, inicio, fim }
    })
    vendasMes.value = res.data ?? []
  } catch { vendasMes.value = [] } finally { carregando.value = false }
}

// ── Curva ABC de Produtos ────────────────────────────────────────────────────
interface AbcItem { descricao: string; totalVendido: number; participacao: number; participacaoAcumulada: number; curva: string }
const abcCanvas = ref<HTMLCanvasElement>()
const curvaAbc = ref<AbcItem[]>([])
const carregandoAbc = ref(true)
const abcResumo = computed(() => ({
  A: curvaAbc.value.filter(i => i.curva === 'A').length,
  B: curvaAbc.value.filter(i => i.curva === 'B').length,
  C: curvaAbc.value.filter(i => i.curva === 'C').length,
}))

async function carregarCurvaAbc() {
  if (!auth.empresaId) { carregandoAbc.value = false; return }
  try {
    const inicio = `${anoAtual}-01-01`
    const fim = new Date().toISOString().slice(0, 10)
    const res = await api.get<{ itens: AbcItem[] }>('/estoque/curva-abc', {
      params: { empresaId: auth.empresaId, inicio, fim }
    })
    curvaAbc.value = res.data.itens ?? []
    await nextTick()
    renderizarCurvaAbc()
  } catch { curvaAbc.value = [] } finally { carregandoAbc.value = false }
}

function renderizarCurvaAbc() {
  const canvas = abcCanvas.value
  if (!canvas || !curvaAbc.value.length) return
  const ctx = canvas.getContext('2d'); if (!ctx) return
  const dados = curvaAbc.value.slice(0, 15)
  const W = canvas.offsetWidth || 800, H = 175
  canvas.width = W; canvas.height = H
  ctx.clearRect(0, 0, W, H)
  const padL = 8, padR = 8, padT = 18, padB = 34
  const areaW = W - padL - padR, areaH = H - padT - padB
  const maxPart = Math.max(...dados.map(d => d.participacao), 1)
  const corCurva = (c: string) => c === 'A' ? '#4caf50' : c === 'B' ? '#ff9800' : '#ef5350'

  // Largura da barra limitada (não estica quando há poucos produtos); o conjunto
  // é centralizado para não ficar tudo amontoado num canto.
  const slot = Math.min(areaW / dados.length, 130)
  const barW = Math.min(slot - 16, 64)
  const offsetX = padL + (areaW - slot * dados.length) / 2

  const cx = (i: number) => offsetX + slot * (i + 0.5)
  const py = (v: number) => padT + areaH - (v / 100) * areaH

  dados.forEach((d, i) => {
    const x = cx(i)
    const bh = Math.max(3, Math.round((d.participacao / maxPart) * areaH))
    ctx.fillStyle = corCurva(d.curva)
    ctx.beginPath(); ctx.roundRect(x - barW / 2, padT + areaH - bh, barW, bh, [4, 4, 0, 0]); ctx.fill()
    // % de participação em cima da barra
    ctx.fillStyle = '#616161'; ctx.font = 'bold 10px sans-serif'; ctx.textAlign = 'center'
    ctx.fillText(Math.round(d.participacao) + '%', x, padT + areaH - bh - 6)
    // nome do produto embaixo
    ctx.fillStyle = '#9e9e9e'; ctx.font = '10px sans-serif'
    const nome = d.descricao.length > 16 ? d.descricao.slice(0, 15) + '…' : d.descricao
    ctx.fillText(nome, x, H - 8)
  })

  // Linha de % acumulado (Pareto) — só desenha com 2+ produtos
  if (dados.length >= 2) {
    ctx.strokeStyle = '#5c6bc0'; ctx.lineWidth = 2; ctx.beginPath()
    dados.forEach((d, i) => i === 0 ? ctx.moveTo(cx(i), py(d.participacaoAcumulada)) : ctx.lineTo(cx(i), py(d.participacaoAcumulada)))
    ctx.stroke()
    dados.forEach((d, i) => { ctx.fillStyle = '#5c6bc0'; ctx.beginPath(); ctx.arc(cx(i), py(d.participacaoAcumulada), 2.5, 0, Math.PI * 2); ctx.fill() })
  }
}

onMounted(async () => {
  await Promise.all([
    carregarResumo(),
    carregarVendasMes(),
    carregarPe(),
    carregarContasMes(),
    carregarReceberMes(),
    carregarVendasColaborador(),
    carregarDre(),
    carregarPlanejamento(),
    carregarCurvaAbc(),
  ])
})
</script>

<style scoped>
.dash-cal-head {
  display: grid; grid-template-columns: repeat(7, 1fr);
  font-size: 10px; font-weight: 700; color: #9e9e9e; text-align: center; margin-bottom: 4px;
}
.dash-cal-grid { display: grid; grid-template-columns: repeat(7, 1fr); gap: 4px; }
.dash-cal-cell {
  aspect-ratio: 1 / 0.82; border-radius: 8px; border: 1px solid #eef0f3;
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  padding: 2px; position: relative;
}
.dash-cal-empty { border: none; }
.dash-cal-num { font-size: 11px; color: #616161; line-height: 1; }
.dash-cal-val {
  font-size: 8.5px; font-weight: 700; line-height: 1.1; margin-top: 2px; color: #f57c00;
  font-variant-numeric: tabular-nums; white-space: nowrap; letter-spacing: -0.2px;
  max-width: 100%; overflow: hidden; text-overflow: ellipsis;
}
.dash-cal-tem { background: #fff8e1; border-color: #ffe0b2; cursor: pointer; }
.dash-cal-vend { background: #e8f5e9; border-color: #c8e6c9; cursor: pointer; }
.dash-cal-val-v { color: #2e7d32; }
.dash-cal-sel-v { box-shadow: 0 0 0 2px #2e7d32 inset; }
.dash-cal-resumo { min-height: 28px; }
.dash-cal-venc { background: #ffebee; border-color: #ffcdd2; }
.dash-cal-venc .dash-cal-val { color: #e53935; }
.dash-cal-hoje { outline: 2px solid rgb(var(--v-theme-primary)); outline-offset: -2px; }
.dash-cal-hoje .dash-cal-num { font-weight: 800; color: rgb(var(--v-theme-primary)); }
.dash-cal-sel { box-shadow: 0 0 0 2px #e53935 inset; }

/* Indicadores do Ponto de Equilíbrio (grade 2x2) */
.pe-stat {
  background: #f6f4fb;
  border: 1px solid #ece7f5;
  border-radius: 10px;
  padding: 8px 10px;
  height: 100%;
}
.pe-stat-lbl { font-size: 0.7rem; color: #78909c; line-height: 1.1; }
.pe-stat-val { font-size: 1rem; font-weight: 700; margin-top: 2px; }
.pe-stat--ok { background: #edf7ee; border-color: #d7ecd9; }
.pe-stat--warn { background: #fff8e9; border-color: #f7e6c2; }
</style>
