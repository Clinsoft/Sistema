<template>
  <div>
    <v-row align="center" class="mb-1">
      <v-col>
        <h2 class="text-h5 font-weight-bold d-flex align-center">
          <v-icon icon="mdi-map-marker-radius" class="mr-2" color="deep-purple" />
          Concorrência
        </h2>
        <div class="text-body-2 text-medium-emphasis">
          Lojas de <b>produtos naturais</b> (e suplementos/ervanário) num raio em volta de cada loja —
          fonte OpenStreetMap. Os preços entram nas próximas fases.
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
          <div ref="mapEl" class="mapa" />
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
            <span v-if="temCoord" class="text-caption text-medium-emphasis">raio {{ raioKm }} km</span>
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
    const m = L.circleMarker([c.latitude, c.longitude], {
      radius: 6, color: '#5e35b1', fillColor: '#7e57c2', fillOpacity: 0.9, weight: 1.5,
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
.lista-conc { max-height: 520px; overflow-y: auto; }
</style>
