<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col><h2 class="text-h5 font-weight-bold">Histórico de Vendas</h2></v-col>
      <v-col cols="auto" class="d-flex gap-2">
        <v-btn v-if="ehAdmin" color="error" variant="tonal" prepend-icon="mdi-content-duplicate"
          @click="abrirDuplicatas">Duplicatas</v-btn>
        <v-btn color="primary" prepend-icon="mdi-cash-register" to="/pdv">Ir ao PDV</v-btn>
      </v-col>
    </v-row>

    <!-- Dialog: Cancelar vendas duplicadas -->
    <v-dialog v-model="dupDlg" max-width="1000" scrollable>
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center">
          <v-icon icon="mdi-content-duplicate" color="error" class="mr-2" />Vendas duplicadas
        </v-card-title>
        <v-divider />
        <v-card-text>
          <v-alert type="warning" variant="tonal" density="compact" class="mb-3">
            Cestas idênticas re-lançadas (bug de emissão). Cancelar <b>reverte estoque, recebível de cartão,
            cupom fiscal e pontos</b>. A 1ª de cada grupo é mantida. <b>Comece testando 1 venda</b> e confira antes de cancelar o resto.
          </v-alert>

          <div class="d-flex flex-wrap align-center gap-2 mb-3">
            <v-text-field v-model="dupInicio" type="date" label="De" variant="outlined" density="compact" hide-details style="max-width:170px" />
            <v-text-field v-model="dupFim" type="date" label="Até" variant="outlined" density="compact" hide-details style="max-width:170px" />
            <v-btn color="primary" :loading="dupCarregando" @click="buscarDuplicatas">Buscar</v-btn>
            <v-spacer />
            <v-chip v-if="dupResumo" size="small" color="error" variant="tonal">
              {{ dupResumo.aCancelar }} a cancelar · R$ {{ (dupResumo.valorACancelar ?? 0).toFixed(2) }}
              <span v-if="dupResumo.comNotaAutorizada"> · {{ dupResumo.comNotaAutorizada }} c/ cupom</span>
            </v-chip>
          </div>

          <div v-if="dupItens.length" class="d-flex gap-2 mb-2">
            <v-btn size="x-small" variant="text" @click="selecionar('alta')">Marcar só ALTA confiança</v-btn>
            <v-btn size="x-small" variant="text" @click="selecionar('todas')">Marcar todas</v-btn>
            <v-btn size="x-small" variant="text" @click="selecionar('nenhuma')">Limpar</v-btn>
          </div>

          <v-table v-if="dupItens.length" density="compact" class="border rounded" style="max-height:420px;overflow-y:auto">
            <thead><tr>
              <th></th><th>Venda</th><th>Data/Hora</th><th class="text-right">Total</th><th>Ação</th><th>Confiança</th><th>Cupom</th><th>Cesta</th>
            </tr></thead>
            <tbody>
              <template v-for="it in dupItens" :key="it.vendaId">
                <tr :class="{ 'bg-grey-lighten-4': it.acao === 'MANTER' }">
                  <td>
                    <v-checkbox v-if="it.acao === 'CANCELAR'" :model-value="sel.has(it.vendaId)"
                      @update:model-value="toggle(it.vendaId)" density="compact" hide-details />
                  </td>
                  <td>{{ it.numero }}</td>
                  <td class="text-caption">{{ it.dataHora }}</td>
                  <td class="text-right">R$ {{ it.total.toFixed(2) }}</td>
                  <td>
                    <v-chip size="x-small" :color="it.acao === 'MANTER' ? 'success' : 'error'" variant="flat">{{ it.acao }}</v-chip>
                  </td>
                  <td>
                    <v-chip v-if="it.acao === 'CANCELAR'" size="x-small"
                      :color="it.confianca === 'ALTA' ? 'green' : 'orange'" variant="tonal">{{ it.confianca }}</v-chip>
                  </td>
                  <td><v-icon v-if="it.notaAutorizada" size="16" color="warning" title="Cupom autorizado — contador">mdi-receipt-text</v-icon></td>
                  <td class="text-caption" style="max-width:320px">{{ it.cesta }}</td>
                </tr>
              </template>
            </tbody>
          </v-table>
          <div v-else-if="dupBuscou" class="text-center py-8 text-medium-emphasis">Nenhuma duplicata no período. 🎉</div>

          <v-alert v-if="dupResultado" :type="dupResultado.cancelados > 0 ? 'success' : 'info'" variant="tonal" density="compact" class="mt-3">
            <b>{{ dupResultado.cancelados }}</b> venda(s) cancelada(s).
            <span v-if="dupResultado.pulados?.length"> {{ dupResultado.pulados.length }} pulada(s).</span>
            <span v-if="dupResultado.erros?.length"> {{ dupResultado.erros.length }} com erro.</span>
            <div v-if="dupResultado.notasParaContador?.length" class="mt-1 text-caption">
              ⚠️ {{ dupResultado.notasParaContador.length }} tinham cupom autorizado — leve ao contador para cancelar na SEFAZ.
            </div>
          </v-alert>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <span class="text-caption text-medium-emphasis">{{ sel.size }} selecionada(s)</span>
          <v-spacer />
          <v-btn v-if="dupItens.length" variant="text" color="blue-grey" :loading="dupCancelando"
            @click="dispensarDuplicatas" title="Some da lista; só reaparece se surgir duplicata nova">
            Não são duplicatas — limpar lista
          </v-btn>
          <v-btn variant="text" @click="dupDlg = false">Fechar</v-btn>
          <v-btn color="error" variant="flat" :loading="dupCancelando" :disabled="sel.size === 0"
            @click="confirmarCancelarDuplicatas">Cancelar {{ sel.size }} venda(s)</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <GuiaPassos
      id="historico-vendas"
      titulo="Como usar o Histórico de Vendas"
      :passos="[
        'Escolha o <b>período</b> (mês ou datas) e, se quiser, filtre por <b>Status</b> (Finalizada, Cancelada, Em Aberto). Clique em <b>Buscar</b>.',
        'Os cards mostram o resumo: total de vendas, faturamento, ticket médio e canceladas do período.',
        'Clique em uma linha (ou no ícone 👁) para abrir o <b>detalhe</b> da venda: itens, pagamentos e totais.',
        'Em vendas <b>Finalizadas</b>, use <b>↩ Registrar Devolução</b> para devolver itens e repor o estoque automaticamente.',
      ]"
    />

    <!-- Filtros -->
    <v-card rounded="xl" elevation="1" class="mb-4 pa-3">
      <v-row dense align="center">
        <v-col cols="12" sm="3">
          <FiltroMes @selecionar="(i, f) => { filtros.inicio = i; filtros.fim = f; carregar() }" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.inicio" label="De" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="filtros.fim" label="Até" type="date"
            variant="outlined" density="compact" hide-details />
        </v-col>
        <v-col cols="12" sm="3">
          <v-select v-model="filtros.status" :items="statusOptions" :item-title="rotuloStatus" label="Status"
            variant="outlined" density="compact" hide-details clearable />
        </v-col>
        <v-col cols="12" sm="3">
          <v-select v-model="filtros.loja" :items="lojasDisponiveis" label="Loja"
            variant="outlined" density="compact" hide-details clearable
            prepend-inner-icon="mdi-store-outline" />
        </v-col>
        <v-col cols="auto">
          <v-btn color="primary" variant="tonal" @click="carregar" :loading="carregando">Buscar</v-btn>
        </v-col>
      </v-row>
    </v-card>

    <!-- Totalizadores -->
    <v-row v-if="vendas.length" class="mb-4">
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-primary">{{ vendasFiltradas.length }}</div>
          <div class="text-caption text-medium-emphasis">Vendas no Período</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-success">R$ {{ fmt(totalVendas) }}</div>
          <div class="text-caption text-medium-emphasis">Total Faturado</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold">R$ {{ fmt(ticketMedio) }}</div>
          <div class="text-caption text-medium-emphasis">Ticket Médio</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card rounded="xl" elevation="1" class="pa-3 text-center">
          <div class="text-h6 font-weight-bold text-error">{{ canceladas }}</div>
          <div class="text-caption text-medium-emphasis">Canceladas</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Tabela -->
    <v-card rounded="xl" elevation="1">
      <v-data-table :headers="headers" :items="vendasFiltradas" :loading="carregando"
        density="compact" hover items-per-page="20"
        @click:row="(_: any, row: any) => abrirDetalhe(row.item)">
        <template #item.loja="{ item }">
          <v-chip size="x-small" color="deep-orange" variant="tonal" label>
            <v-icon start size="12">mdi-store-outline</v-icon>{{ item.loja ?? '—' }}
          </v-chip>
        </template>
        <template #item.total="{ item }">
          <span class="font-weight-medium">R$ {{ fmt(item.total) }}</span>
        </template>
        <template #item.dataHora="{ item }">
          {{ new Date(item.dataHora ?? item.criadoEm).toLocaleString('pt-BR') }}
        </template>
        <template #item.status="{ item }">
          <v-chip size="small" :color="corStatus(item.status)" variant="tonal">
            {{ rotuloStatus(item.status) }}
          </v-chip>
        </template>
        <template #item.actions="{ item }">
          <v-btn icon="mdi-eye-outline" size="x-small" variant="text" color="primary"
            title="Ver detalhe" @click.stop="abrirDetalhe(item)" />
          <v-menu>
            <template #activator="{ props }">
              <v-btn icon="mdi-printer-outline" size="x-small" variant="text" color="success"
                title="Reimprimir cupom" :loading="imprimindoId === item.id"
                v-bind="props" @click.stop />
            </template>
            <v-list density="compact">
              <v-list-item prepend-icon="mdi-receipt-text-outline" title="Cupom simples"
                @click="reimprimirCupom(item)" />
              <v-list-item prepend-icon="mdi-file-document-check-outline" title="Cupom fiscal (NFC-e)"
                @click="reimprimirCupomFiscal(item)" />
            </v-list>
          </v-menu>
          <v-btn v-if="item.status === 'Finalizada'" icon="mdi-keyboard-return"
            size="x-small" variant="text" color="warning" title="Registrar Devolução"
            @click.stop="abrirDevolucao(item)" />
        </template>
      </v-data-table>
    </v-card>

    <!-- Drawer detalhe -->
    <v-navigation-drawer v-model="drawerDetalhe" location="right" width="420" temporary>
      <v-toolbar flat>
        <v-toolbar-title class="text-body-1 font-weight-bold">
          Venda {{ vendaSel?.numero }}
        </v-toolbar-title>
        <v-btn icon="mdi-close" @click="drawerDetalhe = false" />
      </v-toolbar>
      <v-divider />

      <div v-if="vendaSel" class="pa-4">
        <div class="mb-3">
          <v-chip :color="corStatus(vendaSel.status)" size="small" class="mr-2">{{ vendaSel.status }}</v-chip>
          <span class="text-caption text-medium-emphasis">
            {{ new Date(vendaSel.dataHora ?? vendaSel.criadoEm).toLocaleString('pt-BR') }}
          </span>
        </div>

        <div class="text-overline mb-1">Itens</div>
        <v-table density="compact" class="mb-3">
          <thead>
            <tr><th>Produto</th><th class="text-right">Qtd</th><th class="text-right">Total</th></tr>
          </thead>
          <tbody>
            <tr v-for="item in (vendaSel.itens ?? [])" :key="item.id">
              <td class="text-body-2">{{ item.descricao }}</td>
              <td class="text-right text-body-2">{{ item.quantidade }}</td>
              <td class="text-right text-body-2">R$ {{ fmt(item.total) }}</td>
            </tr>
            <tr v-if="!(vendaSel.itens?.length)">
              <td colspan="3" class="text-center text-caption text-medium-emphasis py-2">
                Carregando itens…
              </td>
            </tr>
          </tbody>
        </v-table>

        <div class="text-overline mb-1">Pagamentos</div>
        <div v-for="pag in (vendaSel.pagamentos ?? [])" :key="pag.id"
          class="d-flex justify-space-between mb-1">
          <span class="text-body-2">{{ pag.forma }}</span>
          <span class="text-body-2 font-weight-medium">R$ {{ fmt(pag.valor) }}</span>
        </div>

        <v-divider class="my-3" />
        <div class="d-flex justify-space-between mb-1">
          <span class="text-body-2 text-medium-emphasis">Subtotal</span>
          <span class="text-body-2">R$ {{ fmt(vendaSel.subTotal) }}</span>
        </div>
        <div v-if="(vendaSel.totalDesconto ?? 0) > 0" class="d-flex justify-space-between mb-1">
          <span class="text-body-2 text-medium-emphasis">Desconto</span>
          <span class="text-body-2 text-error">- R$ {{ fmt(vendaSel.totalDesconto) }}</span>
        </div>
        <div class="d-flex justify-space-between">
          <span class="text-body-1 font-weight-bold">Total</span>
          <span class="text-h6 font-weight-bold text-success">R$ {{ fmt(vendaSel.total) }}</span>
        </div>
        <div v-if="(vendaSel.troco ?? 0) > 0" class="d-flex justify-space-between mt-1">
          <span class="text-body-2 text-medium-emphasis">Troco</span>
          <span class="text-body-2">R$ {{ fmt(vendaSel.troco) }}</span>
        </div>

        <div v-if="vendaSel.observacao" class="mt-3">
          <div class="text-overline mb-1">Observação</div>
          <div class="text-body-2 text-medium-emphasis">{{ vendaSel.observacao }}</div>
        </div>

        <div class="mt-4 d-flex flex-column gap-2">
          <v-btn block color="success" variant="tonal" prepend-icon="mdi-receipt-text-outline"
            :loading="imprimindoId === vendaSel.id" @click="reimprimirCupom(vendaSel)">
            Reimprimir cupom simples
          </v-btn>
          <v-btn block color="teal" variant="tonal" prepend-icon="mdi-file-document-check-outline"
            :loading="imprimindoFiscalId === vendaSel.id" @click="reimprimirCupomFiscal(vendaSel)">
            Reimprimir cupom fiscal (NFC-e)
          </v-btn>
        </div>

        <div v-if="vendaSel.status === 'Finalizada'" class="mt-3">
          <v-btn block color="warning" variant="tonal" prepend-icon="mdi-keyboard-return"
            @click="abrirDevolucao(vendaSel)">
            Registrar Devolução
          </v-btn>
        </div>
      </div>
    </v-navigation-drawer>

    <!-- Dialog devolução -->
    <v-dialog v-model="dialogDevolucao" max-width="600" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-2">
          Devolução — Venda {{ vendaDevolucao?.numero }}
        </v-card-title>
        <v-card-text>
          <v-text-field v-model="motivoDevolucao" label="Motivo da Devolução *"
            variant="outlined" density="compact" class="mb-3"
            :rules="[r => !!r || 'Obrigatório']" />

          <div class="text-body-2 font-weight-medium mb-2">Selecione os itens a devolver:</div>
          <div v-for="item in itensParaDevolucao" :key="item.produtoId" class="mb-2">
            <div class="d-flex align-center gap-2">
              <v-checkbox v-model="item.selecionado" hide-details density="compact" class="flex-shrink-0" />
              <div class="flex-grow-1">
                <div class="text-body-2">{{ item.descricao }}</div>
                <div class="text-caption text-medium-emphasis">
                  Qtd vendida: {{ item.quantidade }} | R$ {{ fmt(item.precoUnitario) }} un.
                </div>
              </div>
              <v-text-field v-if="item.selecionado" v-model.number="item.qtdDevolver"
                type="number" variant="outlined" density="compact" hide-details
                style="width:90px" :max="item.quantidade" :min="0.01" label="Qtd" />
            </div>
          </div>

          <v-divider class="my-3" />
          <div class="d-flex justify-space-between">
            <span class="text-body-2 text-medium-emphasis">Total a devolver:</span>
            <span class="text-body-1 font-weight-bold text-warning">R$ {{ fmt(totalDevolucao) }}</span>
          </div>

          <v-checkbox v-model="reporEstoque" label="Repor estoque automaticamente"
            density="compact" class="mt-2" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dialogDevolucao = false">Cancelar</v-btn>
          <v-btn color="warning" :loading="salvandoDevolucao"
            :disabled="!motivoDevolucao || totalDevolucao === 0"
            @click="confirmarDevolucao">
            Confirmar Devolução
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
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()

// ─── Cancelar vendas duplicadas (admin) ─────────────────────────────────────
const ehAdmin = computed(() => auth.usuario?.role === 'Administrador')
const dupDlg = ref(false)
const dupInicio = ref('2026-07-25')
const dupFim = ref('2026-08-06')
const dupCarregando = ref(false)
const dupBuscou = ref(false)
const dupItens = ref<any[]>([])
const dupResumo = ref<any>(null)
const dupResultado = ref<any>(null)
const dupCancelando = ref(false)
const sel = ref(new Set<string>())

function abrirDuplicatas() {
  dupResultado.value = null
  dupDlg.value = true
  if (!dupItens.value.length) buscarDuplicatas()
}

function selecionar(modo: 'alta' | 'todas' | 'nenhuma') {
  const s = new Set<string>()
  if (modo !== 'nenhuma') {
    for (const it of dupItens.value)
      if (it.acao === 'CANCELAR' && (modo === 'todas' || it.confianca === 'ALTA')) s.add(it.vendaId)
  }
  sel.value = s
}
function toggle(id: string) {
  const s = new Set(sel.value)
  if (s.has(id)) s.delete(id); else s.add(id)
  sel.value = s
}

async function buscarDuplicatas() {
  dupCarregando.value = true; dupResultado.value = null
  try {
    const { data } = await api.get('/vendas/duplicatas', {
      params: { empresaId: auth.empresaId, inicio: dupInicio.value, fim: dupFim.value },
    })
    dupItens.value = data.itens ?? []
    dupResumo.value = data
    selecionar('alta')   // pré-seleciona as de ALTA confiança
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem || 'Falha ao buscar duplicatas.')
  } finally {
    dupCarregando.value = false; dupBuscou.value = true
  }
}

async function confirmarCancelarDuplicatas() {
  if (sel.value.size === 0) return
  if (!confirm(`Cancelar ${sel.value.size} venda(s)? Isso reverte estoque, recebíveis e cupom. Não dá pra desfazer em lote.`)) return
  dupCancelando.value = true
  try {
    const { data } = await api.post('/vendas/cancelar-lote', {
      vendaIds: [...sel.value],
      motivo: 'Venda duplicada (bug de emissão)',
    })
    dupResultado.value = data
    notif.ok(`${data.cancelados} venda(s) cancelada(s).`)
    sel.value = new Set()
    await buscarDuplicatas()   // atualiza a lista (canceladas somem)
    await carregar()           // atualiza o histórico
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem || 'Falha ao cancelar.')
  } finally {
    dupCancelando.value = false
  }
}

async function dispensarDuplicatas() {
  if (!dupItens.value.length) return
  if (!confirm('Marcar as duplicatas listadas como revisadas? Elas somem da lista e só voltam a aparecer se surgir duplicata NOVA.')) return
  dupCancelando.value = true
  try {
    await api.post('/vendas/duplicatas/ignorar', { vendaIds: dupItens.value.map(i => i.vendaId) })
    notif.ok('Lista limpa. Só vai mostrar duplicatas novas a partir de agora.')
    await buscarDuplicatas()
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem || 'Falha ao marcar como revisadas.')
  } finally {
    dupCancelando.value = false
  }
}
const carregando = ref(false)
const vendas = ref<any[]>([])
const drawerDetalhe = ref(false)
const vendaSel = ref<any>(null)
const imprimindoId = ref<string | null>(null)
const imprimindoFiscalId = ref<string | null>(null)

// Reimprime o cupom (comprovante simples) da venda — abre o PDF em nova aba.
async function reimprimirCupom(venda: any) {
  imprimindoId.value = venda.id
  try {
    const r = await api.get(`/fiscal/recibo/venda/${venda.id}`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data)
    window.open(url, '_blank')
    setTimeout(() => URL.revokeObjectURL(url), 60000)
  } catch {
    notif.erro('Não foi possível gerar o cupom desta venda.')
  } finally {
    imprimindoId.value = null
  }
}

// Reimprime o cupom fiscal (DANFE da NFC-e). Se a venda não teve NFC-e, avisa.
async function reimprimirCupomFiscal(venda: any) {
  imprimindoFiscalId.value = venda.id
  try {
    const r = await api.get(`/fiscal/recibo/venda/${venda.id}/nfce`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data)
    window.open(url, '_blank')
    setTimeout(() => URL.revokeObjectURL(url), 60000)
  } catch (e: any) {
    // Erro vem como blob (responseType) → extrai a mensagem do JSON.
    let msg = 'Esta venda não tem cupom fiscal (NFC-e) para reimprimir.'
    try { msg = JSON.parse(await e?.response?.data?.text())?.mensagem || msg } catch { /* mantém padrão */ }
    notif.aviso(msg)
  } finally {
    imprimindoFiscalId.value = null
  }
}

const dialogDevolucao = ref(false)
const vendaDevolucao = ref<any>(null)
const motivoDevolucao = ref('')
const reporEstoque = ref(true)
const salvandoDevolucao = ref(false)
const itensParaDevolucao = ref<any[]>([])

const totalDevolucao = computed(() =>
  itensParaDevolucao.value
    .filter(i => i.selecionado)
    .reduce((s, i) => s + i.qtdDevolver * i.precoUnitario, 0)
)

const statusOptions = ['EmAberto', 'Finalizada', 'Cancelada']

const filtros = ref({
  inicio: new Date(new Date().getFullYear(), new Date().getMonth(), 1).toISOString().slice(0, 10),
  fim: new Date().toISOString().slice(0, 10),
  status: null as string | null,
  loja: (auth.lojaAtual?.nome ?? null) as string | null,  // já vem na loja selecionada
})

const headers = [
  { title: 'Nº', key: 'numero', width: 90 },
  { title: 'Data/Hora', key: 'dataHora', sortable: true, width: 160 },
  { title: 'Cliente', key: 'clienteNome' },
  { title: 'Loja', key: 'loja', width: 150 },
  { title: 'Total', key: 'total', sortable: true, width: 120 },
  { title: 'Status', key: 'status', width: 110 },
  { title: '', key: 'actions', sortable: false, width: 50 },
]

// Lojas presentes nas vendas carregadas (para o filtro).
const lojasDisponiveis = computed(() =>
  [...new Set(vendas.value.map(v => v.loja).filter(Boolean))].sort() as string[]
)
const vendasFiltradas = computed(() =>
  filtros.value.loja ? vendas.value.filter(v => v.loja === filtros.value.loja) : vendas.value
)

const totalVendas = computed(() =>
  vendasFiltradas.value.filter(v => v.status === 'Finalizada').reduce((s, v) => s + (v.total ?? 0), 0)
)
const ticketMedio = computed(() => {
  const fins = vendasFiltradas.value.filter(v => v.status === 'Finalizada')
  return fins.length ? totalVendas.value / fins.length : 0
})
const canceladas = computed(() => vendasFiltradas.value.filter(v => v.status === 'Cancelada').length)

function corStatus(s: string) {
  return s === 'Finalizada' ? 'success' : s === 'Cancelada' ? 'error' : 'warning'
}
function fmt(v: number) { return (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2 }) }

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/vendas', {
      params: {
        empresaId: auth.empresaId,
        inicio: filtros.value.inicio,
        fim: filtros.value.fim,
        status: filtros.value.status || undefined,
      },
    })
    vendas.value = r.data?.itens ?? r.data ?? []
  } finally { carregando.value = false }
}

async function abrirDetalhe(venda: any) {
  vendaSel.value = venda
  drawerDetalhe.value = true
  if (!venda.itens) {
    try {
      const r = await api.get(`/vendas/${venda.id}`, { params: { empresaId: auth.empresaId } })
      vendaSel.value = { ...vendaSel.value, ...r.data }
    } catch { /* usa dados da listagem */ }
  }
}

async function abrirDevolucao(venda: any) {
  vendaDevolucao.value = venda
  motivoDevolucao.value = ''
  reporEstoque.value = true
  itensParaDevolucao.value = []
  dialogDevolucao.value = true
  try {
    const r = await api.get(`/devolucoes/venda/${venda.id}/itens`, { params: { empresaId: auth.empresaId } })
    itensParaDevolucao.value = (r.data ?? []).map((i: any) => ({
      ...i, selecionado: true, qtdDevolver: i.quantidade,
    }))
  } catch { /* usa lista vazia */ }
}

async function confirmarDevolucao() {
  const itensSel = itensParaDevolucao.value
    .filter(i => i.selecionado && i.qtdDevolver > 0)
    .map(i => ({
      produtoId: i.produtoId,
      descricao: i.descricao,
      quantidade: i.qtdDevolver,
      valorUnitario: i.precoUnitario,
    }))

  if (!itensSel.length || !motivoDevolucao.value) return
  salvandoDevolucao.value = true
  try {
    await api.post('/devolucoes', {
      empresaId: auth.empresaId,
      vendaId: vendaDevolucao.value.id,
      motivo: motivoDevolucao.value,
      itens: itensSel,
      reporEstoque: reporEstoque.value,
    })
    notif.ok('Devolução registrada com sucesso!')
    dialogDevolucao.value = false
    drawerDetalhe.value = false
    await carregar()
  } catch (e: any) {
    notif.erro(e?.response?.data?.title ?? e?.response?.data ?? 'Erro ao registrar devolução.')
  } finally { salvandoDevolucao.value = false }
}

const route = useRoute()

onMounted(() => {
  // Vindo do Dashboard (calendário de Vendas) com ?data=YYYY-MM-DD: filtra só o dia.
  const data = route.query.data
  if (typeof data === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(data)) {
    filtros.value.inicio = data
    filtros.value.fim = data
    filtros.value.status = null
  }
  carregar()
})
</script>
