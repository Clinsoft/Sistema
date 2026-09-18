<template>
  <div>
    <v-row align="center" class="mb-1">
      <v-col>
        <h2 class="text-h5 font-weight-bold d-flex align-center">
          <v-icon icon="mdi-map-marker-radius" class="mr-2" color="deep-purple" />
          Concorrência
        </h2>
        <div class="text-body-2 text-medium-emphasis">
          Lojas de <b>produtos naturais</b> (e suplementos) num raio em volta de cada loja —
          busca automática (Google Places) + cadastro manual. Os preços entram nas próximas fases.
        </div>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1" class="mb-3 pa-3">
      <v-row dense align="center">
        <v-col cols="12" md="5">
          <v-select v-model="lojaId" :items="lojas" item-title="nome" item-value="id"
            label="Loja" variant="outlined" density="compact" hide-details
            :loading="carregandoLojas" @update:model-value="selecionarLoja">
            <template #item="{ props, item }">
              <v-list-item v-bind="props" :subtitle="item.raw.endereco || 'sem endereço cadastrado'">
                <template #append>
                  <v-chip v-if="item.raw.geocodificada" size="x-small" color="success" variant="tonal">
                    {{ item.raw.concorrentes }} mapeados
                  </v-chip>
                  <v-chip v-else size="x-small" color="warning" variant="tonal">sem local</v-chip>
                </template>
              </v-list-item>
            </template>
          </v-select>
        </v-col>
        <v-col cols="6" md="2">
          <v-select v-model="raioKm" :items="[3, 5, 10]" label="Raio (km)" variant="outlined"
            density="compact" hide-details suffix="km" />
        </v-col>
        <v-col cols="6" md="5" class="d-flex ga-2">
          <v-btn color="deep-purple" variant="flat" rounded="lg" :loading="buscando"
            :disabled="!lojaId" prepend-icon="mdi-magnify" @click="buscar">
            Buscar concorrentes
          </v-btn>
          <v-btn v-if="loja && !temCoord" color="deep-purple" variant="tonal" rounded="lg"
            :loading="geocodificando" prepend-icon="mdi-crosshairs-gps" @click="geocodificar">
            Localizar loja
          </v-btn>
        </v-col>
      </v-row>
      <div v-if="loja && !temCoord" class="text-caption text-warning mt-2">
        Esta loja ainda não foi localizada no mapa. "Buscar concorrentes" já geocodifica pelo endereço;
        se falhar, revise o endereço em Cadastros → Locais de Estoque.
      </div>
    </v-card>

    <v-row>
      <v-col cols="12" md="7">
        <v-card rounded="xl" elevation="1">
          <v-alert v-if="adicionando" type="info" density="compact" variant="tonal" class="ma-2 mb-0">
            Clique no mapa no ponto onde fica o concorrente.
            <template #append>
              <v-btn size="x-small" variant="text" @click="cancelarAdd">Cancelar</v-btn>
            </template>
          </v-alert>
          <div ref="mapEl" class="mapa" :class="{ 'modo-add': adicionando }" />
          <div v-if="temCoord" class="text-caption text-medium-emphasis pa-2 d-flex align-center">
            <v-icon icon="mdi-cursor-move" size="16" class="mr-1" />
            Local errado? Arraste o marcador <b class="mx-1" style="color:#43a047">verde</b> para o ponto exato da loja e clique em “Buscar concorrentes” de novo.
          </div>
          <div v-if="!temCoord" class="pa-6 text-center text-medium-emphasis">
            <v-icon icon="mdi-map-search-outline" size="40" class="mb-2" />
            <div class="text-body-2">Selecione uma loja e clique em “Buscar concorrentes”.</div>
          </div>
        </v-card>
      </v-col>

      <v-col cols="12" md="5">
        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold d-flex align-center">
            <v-icon icon="mdi-store-search-outline" class="mr-2" color="deep-purple" />
            {{ concorrentes.length }} concorrente(s)
            <v-spacer />
            <v-btn v-if="temCoord" size="small" variant="tonal" color="indigo" class="mr-1"
              prepend-icon="mdi-scale-balance" @click="abrirComparativo">Comparar</v-btn>
            <v-btn v-if="temCoord" size="small" variant="tonal" color="teal"
              prepend-icon="mdi-map-marker-plus" @click="iniciarAdd">Adicionar</v-btn>
          </v-card-title>
          <v-divider />
          <div v-if="concorrentes.length === 0" class="pa-6 text-center text-medium-emphasis text-body-2">
            Nenhum concorrente mapeado ainda para esta loja.
          </div>
          <v-list v-else density="compact" class="pa-0 lista-conc">
            <template v-for="(c, i) in concorrentes" :key="c.id">
              <v-list-item @click="focar(c)">
                <template #prepend>
                  <v-avatar size="30" color="deep-purple-lighten-4">
                    <span class="text-caption font-weight-bold text-deep-purple">{{ c.distanciaKm }}</span>
                  </v-avatar>
                </template>
                <v-list-item-title class="text-body-2 font-weight-medium">{{ c.nome }}</v-list-item-title>
                <v-list-item-subtitle class="text-caption">
                  {{ c.categoria || 'Estabelecimento' }}<span v-if="c.endereco"> · {{ c.endereco }}</span>
                </v-list-item-subtitle>
                <template #append>
                  <v-btn icon="mdi-tag-text-outline" size="x-small" variant="text" color="teal"
                    @click.stop="abrirPrecos(c)">
                    <v-icon>mdi-tag-text-outline</v-icon>
                    <v-tooltip activator="parent" location="top">Preços</v-tooltip>
                  </v-btn>
                  <span class="text-caption text-medium-emphasis mr-1">{{ c.distanciaKm }} km</span>
                  <v-btn icon="mdi-close" size="x-small" variant="text" color="grey"
                    @click.stop="remover(c)" />
                </template>
              </v-list-item>
              <v-divider v-if="i < concorrentes.length - 1" />
            </template>
          </v-list>
        </v-card>
      </v-col>
    </v-row>

    <!-- Diálogo: preços de um concorrente -->
    <v-dialog v-model="dialogPrecos" max-width="640" scrollable>
      <v-card rounded="xl">
        <v-card-title class="d-flex align-center pa-4 pb-2 text-body-1 font-weight-bold">
          <v-icon icon="mdi-tag-multiple-outline" color="teal" class="mr-2" />
          Preços — {{ concAtual?.nome }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="dialogPrecos = false" />
        </v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12">
              <v-autocomplete v-model="formPreco.produto" :items="prodItens" item-title="descricao"
                item-value="id" return-object label="Produto do nosso catálogo" variant="outlined"
                density="compact" hide-details :loading="buscandoProd" no-filter clearable
                @update:search="buscarProdutos" placeholder="Digite 2+ letras para buscar" />
              <div class="text-caption text-medium-emphasis mt-1">
                <template v-if="formPreco.produto">Nosso preço: <b>R$ {{ fmtNum(formPreco.produto.precoVenda) }}</b></template>
                <template v-else>Sem produto no catálogo? Use a descrição livre abaixo.</template>
              </div>
            </v-col>
            <v-col cols="12" v-if="!formPreco.produto">
              <v-text-field v-model="formPreco.descricaoLivre" label="Descrição livre"
                variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="6" sm="4">
              <v-text-field v-model.number="formPreco.preco" label="Preço no concorrente" type="number"
                prefix="R$" variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="6" sm="3">
              <v-select v-model="formPreco.unidade" :items="unidades" label="Unid."
                variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12" sm="5">
              <v-text-field v-model="formPreco.observacao" label="Obs. (opcional)"
                variant="outlined" density="compact" hide-details />
            </v-col>
            <v-col cols="12">
              <v-btn color="teal" variant="flat" block :loading="salvandoPreco"
                :disabled="(!formPreco.produto && !formPreco.descricaoLivre.trim()) || !formPreco.preco"
                @click="adicionarPreco">Adicionar preço</v-btn>
            </v-col>
          </v-row>

          <v-divider class="my-3" />
          <div v-if="carregandoPrecos" class="text-center pa-4"><v-progress-circular indeterminate color="teal" /></div>
          <div v-else-if="!precos.length" class="text-center text-medium-emphasis pa-4 text-body-2">
            Nenhum preço coletado ainda neste concorrente.
          </div>
          <v-list v-else density="compact" class="pa-0">
            <v-list-item v-for="p in precos" :key="p.id" class="px-1">
              <v-list-item-title class="text-body-2">{{ p.descricao }}</v-list-item-title>
              <v-list-item-subtitle class="text-caption">
                Concorrente R$ {{ fmtNum(p.preco) }}/{{ p.unidade }}
                <template v-if="p.nossoPreco != null">
                  · nosso R$ {{ fmtNum(p.nossoPreco) }}/{{ p.porPeso ? 'kg' : 'un' }}
                  <span :class="inlineCmp(p).cls"> · {{ inlineCmp(p).texto }}</span>
                </template>
                <span v-if="p.observacao" class="text-medium-emphasis"> · {{ p.observacao }}</span>
              </v-list-item-subtitle>
              <template #append>
                <v-btn icon="mdi-close" size="x-small" variant="text" color="grey" @click="removerPreco(p.id)" />
              </template>
            </v-list-item>
          </v-list>
        </v-card-text>
      </v-card>
    </v-dialog>

    <!-- Diálogo: comparativo nosso × concorrentes -->
    <v-dialog v-model="dialogComp" max-width="860" scrollable>
      <v-card rounded="xl">
        <v-card-title class="d-flex align-center pa-4 pb-2 text-body-1 font-weight-bold">
          <v-icon icon="mdi-scale-balance" color="indigo" class="mr-2" />
          Comparativo de preços
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="dialogComp = false" />
        </v-card-title>
        <v-card-text>
          <v-text-field v-model="compNomeQ" clearable variant="outlined" density="compact" hide-details
            prepend-inner-icon="mdi-magnify" class="mb-3"
            label="Comparar por nome (ex.: aveia em flocos, castanha do pará, quinoa)" />

          <template v-if="compNomeQ && compNomeQ.trim().length >= 2">
            <div v-if="carregandoNome" class="text-center pa-6"><v-progress-circular indeterminate color="indigo" /></div>
            <div v-else-if="!compNomeItens.length" class="text-center text-medium-emphasis pa-6 text-body-2">
              Nenhum preço (nosso ou de concorrente) com “{{ compNomeQ }}”.
            </div>
            <v-table v-else density="compact">
              <thead>
                <tr>
                  <th>Fonte</th><th>Descrição</th>
                  <th class="text-right">Preço</th><th class="text-center">Base</th><th class="text-right">Normalizado</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(it, i) in compNomeItens" :key="i" :class="it.ehNosso ? 'bg-teal-lighten-5' : ''">
                  <td :class="it.ehNosso ? 'font-weight-bold text-teal-darken-3' : ''">{{ it.ehNosso ? 'Nós' : it.fonte }}</td>
                  <td>{{ it.descricao }}</td>
                  <td class="text-right">R$ {{ fmtNum(it.preco) }}/{{ it.unidade }}</td>
                  <td class="text-center text-caption">/{{ it.unidadeBase }}</td>
                  <td class="text-right font-weight-bold">R$ {{ fmtNum(it.precoBase) }}</td>
                </tr>
              </tbody>
            </v-table>
          </template>

          <template v-else>
          <div v-if="carregandoComp" class="text-center pa-6"><v-progress-circular indeterminate color="indigo" /></div>
          <div v-else-if="!comparativo.length" class="text-center text-medium-emphasis pa-6 text-body-2">
            Ainda não há preços ligados a produtos do nosso catálogo.<br>
            Colete preços (botão de etiqueta em cada concorrente) escolhendo o produto do catálogo — aí eles aparecem aqui.
          </div>
          <div v-else>
          <div class="text-caption text-medium-emphasis mb-2">
            Preços normalizados por unidade: granel em <b>R$/kg</b>, demais em <b>R$/un</b>
            (100g×10, dúzia÷12). Unidade incompatível não entra no cálculo.
          </div>
          <v-table density="compact">
            <thead>
              <tr>
                <th>Produto</th>
                <th class="text-center">Base</th>
                <th class="text-right">Nosso</th>
                <th class="text-right">Menor conc.</th>
                <th class="text-right">Médio</th>
                <th class="text-right">Maior</th>
                <th class="text-center">Situação</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="it in comparativo" :key="it.produtoId">
                <td>
                  {{ it.produto }}
                  <span v-if="it.incompativeis" class="text-caption text-warning">
                    ({{ it.incompativeis }} c/ unidade incompatível)
                  </span>
                </td>
                <td class="text-center text-caption">/{{ it.baseUnidade }}</td>
                <td class="text-right font-weight-bold">R$ {{ fmtNum(it.nossoPreco) }}</td>
                <td class="text-right">{{ it.comparaveis ? 'R$ ' + fmtNum(it.min) : '—' }}</td>
                <td class="text-right">{{ it.comparaveis ? 'R$ ' + fmtNum(it.media) : '—' }}</td>
                <td class="text-right">{{ it.comparaveis ? 'R$ ' + fmtNum(it.max) : '—' }}</td>
                <td class="text-center">
                  <v-chip size="x-small" label :color="situacaoCor(it.situacao)">{{ situacaoLabel(it.situacao) }}</v-chip>
                </td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </template>
        </v-card-text>
      </v-card>
    </v-dialog>

    <!-- Diálogo: novo concorrente manual -->
    <v-dialog v-model="dialogAdd" max-width="420">
      <v-card rounded="xl">
        <v-card-title class="text-body-1 font-weight-bold d-flex align-center">
          <v-icon icon="mdi-map-marker-plus" color="teal" class="mr-2" />Novo concorrente
        </v-card-title>
        <v-card-text>
          <v-text-field v-model="novoConc.nome" label="Nome da loja" variant="outlined"
            density="compact" autofocus class="mb-2" @keyup.enter="salvarManual" />
          <v-text-field v-model="novoConc.categoria" label="Categoria" variant="outlined"
            density="compact" hide-details />
          <div class="text-caption text-medium-emphasis mt-2">
            Ponto no mapa: {{ novoConc.lat.toFixed(5) }}, {{ novoConc.lng.toFixed(5) }}
          </div>
        </v-card-text>
        <v-card-actions class="px-4 pb-3">
          <v-spacer />
          <v-btn variant="text" @click="dialogAdd = false">Cancelar</v-btn>
          <v-btn color="teal" variant="flat" :loading="salvandoManual"
            :disabled="!novoConc.nome.trim()" @click="salvarManual">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from 'vue'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

interface Loja { id: string; nome: string; latitude: number | null; longitude: number | null; endereco: string | null; geocodificada: boolean; concorrentes: number }
interface Concorrente { id: string; nome: string; categoria: string | null; latitude: number; longitude: number; distanciaKm: number; endereco: string | null; telefone: string | null; website: string | null; fonte: string }

const lojas = ref<Loja[]>([])
const lojaId = ref<string | null>(null)
const loja = ref<{ id: string; nome: string; latitude: number | null; longitude: number | null; endereco: string | null } | null>(null)
const concorrentes = ref<Concorrente[]>([])
const raioKm = ref(5)
const carregandoLojas = ref(true)
const buscando = ref(false)
const geocodificando = ref(false)

const temCoord = computed(() => !!loja.value?.latitude && !!loja.value?.longitude)
const fmtNum = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

let map: L.Map | null = null
let camada: L.LayerGroup | null = null
const marcadores = new Map<string, L.CircleMarker>()
const mapEl = ref<HTMLElement>()

function garantirMapa() {
  if (map || !mapEl.value) return
  map = L.map(mapEl.value, { zoomControl: true, scrollWheelZoom: true }).setView([-22.0, -47.9], 13)
  L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19, attribution: '© OpenStreetMap',
  }).addTo(map)
  camada = L.layerGroup().addTo(map)
  map.on('click', (e: any) => {
    if (!adicionando.value) return
    novoConc.value = { nome: '', categoria: 'Produtos naturais', lat: e.latlng.lat, lng: e.latlng.lng }
    dialogAdd.value = true
  })
}

function desenhar(ajustarZoom = true) {
  if (!temCoord.value) return
  garantirMapa()
  if (!map || !camada) return
  camada.clearLayers()
  marcadores.clear()

  const lat = loja.value!.latitude!, lng = loja.value!.longitude!
  // Loja: marcador ARRASTÁVEL (verde) para corrigir o local + círculo do raio.
  const pin = L.divIcon({ className: '', iconSize: [18, 18], iconAnchor: [9, 9],
    html: '<div style="width:16px;height:16px;border-radius:50%;background:#43a047;border:2px solid #1b5e20;box-shadow:0 0 0 4px rgba(67,160,71,.35)"></div>' })
  const mLoja = L.marker([lat, lng], { draggable: true, icon: pin, zIndexOffset: 1000,
    title: 'Arraste para corrigir o local da loja' })
    .bindPopup(`<b>${loja.value!.nome}</b><br>Sua loja — arraste para ajustar o local`)
  mLoja.on('dragend', (e: any) => { const p = e.target.getLatLng(); salvarCoordenada(p.lat, p.lng) })
  mLoja.addTo(camada)
  L.circle([lat, lng], { radius: raioKm.value * 1000, color: '#7e57c2', weight: 1, fillColor: '#7e57c2', fillOpacity: 0.06 }).addTo(camada)

  // Concorrentes (roxo)
  for (const c of concorrentes.value) {
    const manual = c.fonte === 'Manual'
    const m = L.circleMarker([c.latitude, c.longitude], {
      radius: 6, weight: 1.5, fillOpacity: 0.9,
      color: manual ? '#00695c' : '#5e35b1', fillColor: manual ? '#26a69a' : '#7e57c2',
    })
    const linhas = [
      `<b>${c.nome}</b>`,
      c.categoria ? c.categoria : null,
      c.endereco ? c.endereco : null,
      c.telefone ? `📞 ${c.telefone}` : null,
      c.website ? `<a href="${c.website}" target="_blank" rel="noopener">site</a>` : null,
      `${c.distanciaKm} km da loja`,
    ].filter(Boolean).join('<br>')
    m.bindPopup(linhas)
    m.addTo(camada)
    marcadores.set(c.id, m)
  }

  if (ajustarZoom) {
    const grupo = L.featureGroup([...camada.getLayers()] as L.Layer[])
    try { map.fitBounds(grupo.getBounds().pad(0.1)) } catch { map.setView([lat, lng], 14) }
  }
  nextTick(() => map?.invalidateSize())
}

// Correção manual do local da loja (arrastar o marcador). Salva e mantém o zoom.
async function salvarCoordenada(lat: number, lng: number) {
  if (!lojaId.value || !loja.value) return
  loja.value.latitude = lat
  loja.value.longitude = lng
  desenhar(false)
  try {
    await api.post(`/concorrentes/loja/${lojaId.value}/coordenada`, { latitude: lat, longitude: lng })
  } catch { /* mantém o ponto na tela mesmo se a gravação falhar */ }
}

function focar(c: Concorrente) {
  if (!map) return
  map.setView([c.latitude, c.longitude], 16)
  marcadores.get(c.id)?.openPopup()
}

// Cadastro MANUAL de concorrente (o OSM não tem as lojas de produtos naturais;
// no Google elas existem — o usuário clica no mapa onde ficam e dá o nome).
const adicionando = ref(false)
const dialogAdd = ref(false)
const salvandoManual = ref(false)
const novoConc = ref<{ nome: string; categoria: string; lat: number; lng: number }>(
  { nome: '', categoria: 'Produtos naturais', lat: 0, lng: 0 })
function iniciarAdd() { adicionando.value = true }
function cancelarAdd() { dialogAdd.value = false; adicionando.value = false }
async function salvarManual() {
  if (!lojaId.value || !novoConc.value.nome.trim()) return
  salvandoManual.value = true
  try {
    const res = await api.post<Concorrente>(`/concorrentes/loja/${lojaId.value}/manual`, {
      nome: novoConc.value.nome.trim(),
      categoria: novoConc.value.categoria?.trim() || null,
      latitude: novoConc.value.lat, longitude: novoConc.value.lng, endereco: null,
    })
    concorrentes.value = [...concorrentes.value, res.data].sort((a, b) => a.distanciaKm - b.distanciaKm)
    dialogAdd.value = false
    adicionando.value = false
    desenhar(false)
  } catch (e: any) {
    alert(e?.response?.data?.mensagem || 'Não foi possível adicionar o concorrente.')
  } finally { salvandoManual.value = false }
}

// ── Fase 2: preços (nosso × concorrente) ─────────────────────────────────────
interface PrecoItem {
  id: string; produtoId: string | null; descricao: string; ean: string | null
  preco: number; unidade: string; dataColeta: string; observacao: string | null
  nossoPreco: number | null; porPeso: boolean | null
}
const unidades = ['un', 'kg', '100g', 'g', 'L', 'ml', 'dz', 'pct']
// Normaliza o preço do concorrente para a base do produto (granel = R$/kg, senão R$/un).
function normPreco(preco: number, unidade: string, porPeso: boolean | null): number | null {
  const u = (unidade || '').trim().toLowerCase()
  if (porPeso) return u === 'kg' ? preco : u === '100g' ? preco * 10 : u === 'g' ? preco * 1000 : null
  return (u === 'un' || u === '') ? preco : u === 'dz' ? preco / 12 : u === 'pct' ? preco : null
}
function inlineCmp(p: PrecoItem): { texto: string; cls: string } {
  if (p.nossoPreco == null) return { texto: '', cls: '' }
  const cb = normPreco(p.preco, p.unidade, p.porPeso)
  if (cb == null) return { texto: 'unidade não comparável', cls: 'text-warning' }
  if (cb < p.nossoPreco) return { texto: 'eles mais baratos', cls: 'text-error font-weight-bold' }
  if (cb > p.nossoPreco) return { texto: 'somos mais baratos', cls: 'text-success font-weight-bold' }
  return { texto: 'empatados', cls: 'text-medium-emphasis' }
}
const dialogPrecos = ref(false)
const concAtual = ref<Concorrente | null>(null)
const precos = ref<PrecoItem[]>([])
const carregandoPrecos = ref(false)
const salvandoPreco = ref(false)
const formPreco = ref<{ produto: any; descricaoLivre: string; preco: number | null; unidade: string; observacao: string }>(
  { produto: null, descricaoLivre: '', preco: null, unidade: 'un', observacao: '' })
const prodOpcoes = ref<any[]>([])
const buscandoProd = ref(false)
let _tProd: any
// Mantém o produto selecionado sempre na lista (senão a v-autocomplete o perde
// quando a busca substitui as opções).
const prodItens = computed(() => {
  const sel = formPreco.value.produto
  if (sel && !prodOpcoes.value.some((x: any) => x.id === sel.id)) return [sel, ...prodOpcoes.value]
  return prodOpcoes.value
})

async function abrirPrecos(c: Concorrente) {
  concAtual.value = c
  formPreco.value = { produto: null, descricaoLivre: '', preco: null, unidade: 'un', observacao: '' }
  dialogPrecos.value = true
  await carregarPrecos()
}
async function carregarPrecos() {
  if (!concAtual.value) return
  carregandoPrecos.value = true
  try {
    const res = await api.get<PrecoItem[]>(`/concorrentes/concorrente/${concAtual.value.id}/precos`)
    precos.value = res.data ?? []
  } finally { carregandoPrecos.value = false }
}
function buscarProdutos(q: string) {
  clearTimeout(_tProd)
  if (!q || q.trim().length < 2) { prodOpcoes.value = []; return }
  _tProd = setTimeout(async () => {
    buscandoProd.value = true
    try {
      const res = await api.get<any[]>('/produtos/buscar', { params: { empresaId: auth.empresaId, q } })
      prodOpcoes.value = res.data ?? []
    } catch { prodOpcoes.value = [] } finally { buscandoProd.value = false }
  }, 300)
}
async function adicionarPreco() {
  if (!concAtual.value) return
  const prod = formPreco.value.produto
  const descricao = prod ? prod.descricao : formPreco.value.descricaoLivre.trim()
  if (!descricao || !formPreco.value.preco || formPreco.value.preco <= 0) return
  salvandoPreco.value = true
  try {
    await api.post(`/concorrentes/concorrente/${concAtual.value.id}/precos`, {
      produtoId: prod?.id ?? null, descricao, ean: prod?.codigoBarras ?? null,
      preco: formPreco.value.preco, unidade: formPreco.value.unidade,
      observacao: formPreco.value.observacao?.trim() || null,
    })
    formPreco.value = { produto: null, descricaoLivre: '', preco: null, unidade: formPreco.value.unidade, observacao: '' }
    await carregarPrecos()
  } catch (e: any) {
    alert(e?.response?.data?.mensagem || 'Não foi possível salvar o preço.')
  } finally { salvandoPreco.value = false }
}
async function removerPreco(id: string) {
  try { await api.delete(`/concorrentes/precos/${id}`); precos.value = precos.value.filter(p => p.id !== id) } catch { /* ignora */ }
}

// Comparativo nosso × concorrentes (por produto)
const dialogComp = ref(false)
const carregandoComp = ref(false)
const comparativo = ref<any[]>([])
function situacaoLabel(s: string) {
  return s === 'mais-caro' ? 'Mais caro' : s === 'mais-barato' ? 'Mais barato'
    : s === 'no-meio' ? 'No meio' : s === 'sem-preco' ? 'Sem nosso preço' : 'Sem base comum'
}
function situacaoCor(s: string) {
  return s === 'mais-caro' ? 'error' : s === 'mais-barato' ? 'success'
    : s === 'no-meio' ? 'warning' : 'grey'
}
async function abrirComparativo() {
  if (!lojaId.value) return
  compNomeQ.value = ''
  compNomeItens.value = []
  dialogComp.value = true
  carregandoComp.value = true
  try {
    const res = await api.get<{ itens: any[] }>(`/concorrentes/comparativo/${lojaId.value}`)
    comparativo.value = res.data.itens ?? []
  } finally { carregandoComp.value = false }
}

// Comparativo por NOME (aveia em flocos, castanha do pará, quinoa…): nosso × concorrentes
const compNomeQ = ref('')
const compNomeItens = ref<any[]>([])
const carregandoNome = ref(false)
let _tNome: any
watch(compNomeQ, (v) => {
  clearTimeout(_tNome)
  if (!v || v.trim().length < 2) { compNomeItens.value = []; return }
  _tNome = setTimeout(async () => {
    carregandoNome.value = true
    try {
      const res = await api.get<{ itens: any[] }>('/concorrentes/comparativo-nome',
        { params: { empresaId: auth.empresaId, q: v.trim() } })
      compNomeItens.value = res.data.itens ?? []
    } catch { compNomeItens.value = [] } finally { carregandoNome.value = false }
  }, 350)
})

async function carregarLojas() {
  if (!auth.empresaId) { carregandoLojas.value = false; return }
  carregandoLojas.value = true
  try {
    const res = await api.get<Loja[]>('/concorrentes/lojas', { params: { empresaId: auth.empresaId } })
    lojas.value = res.data ?? []
    const inicial = lojas.value.find(l => l.id === auth.lojaAtualId) ?? lojas.value[0]
    if (inicial) { lojaId.value = inicial.id; await selecionarLoja(inicial.id) }
  } finally { carregandoLojas.value = false }
}

async function selecionarLoja(id: string | null) {
  if (!id) return
  const res = await api.get<{ loja: any; concorrentes: Concorrente[] }>(`/concorrentes/loja/${id}`)
  loja.value = res.data.loja
  concorrentes.value = res.data.concorrentes ?? []
  await nextTick()
  desenhar()
}

async function geocodificar() {
  if (!lojaId.value) return
  geocodificando.value = true
  try {
    await api.post(`/concorrentes/loja/${lojaId.value}/geocodificar`)
    await selecionarLoja(lojaId.value)
  } catch (e: any) {
    alert(e?.response?.data?.mensagem || 'Não foi possível localizar a loja no mapa.')
  } finally { geocodificando.value = false }
}

async function buscar() {
  if (!lojaId.value) return
  buscando.value = true
  try {
    await api.post(`/concorrentes/loja/${lojaId.value}/buscar`, null, { params: { raioKm: raioKm.value } })
    await selecionarLoja(lojaId.value)
  } catch (e: any) {
    alert(e?.response?.data?.mensagem || 'Falha ao buscar concorrentes no mapa.')
  } finally { buscando.value = false }
}

async function remover(c: Concorrente) {
  try {
    await api.delete(`/concorrentes/${c.id}`)
    concorrentes.value = concorrentes.value.filter(x => x.id !== c.id)
    desenhar()
  } catch { /* ignora */ }
}

onMounted(carregarLojas)
onBeforeUnmount(() => { map?.remove(); map = null })
</script>

<style scoped>
.mapa { height: 520px; width: 100%; border-radius: 16px; z-index: 0; }
.mapa.modo-add { cursor: crosshair; }
.lista-conc { max-height: 520px; overflow-y: auto; }
</style>
