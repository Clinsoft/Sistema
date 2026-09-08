<template>
  <div class="termo-wrap">
    <v-card rounded="xl" elevation="2" class="pa-5">
      <div class="d-flex align-center mb-3">
        <v-icon color="amber-darken-2" class="mr-2">mdi-file-sign</v-icon>
        <div class="text-h6 font-weight-bold">Termo de Aceite — Premiação por Desempenho</div>
      </div>
      <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
        Para acessar o <b>Meu Desempenho</b> você precisa <b>ler e aceitar</b> o regulamento da premiação,
        assinando digitalmente (com foto, assinatura e localização). Isso confirma sua participação.
      </v-alert>

      <!-- Texto do regulamento (buscado do servidor, com as metas da loja) -->
      <div v-if="carregandoRegulamento" class="d-flex justify-center pa-6">
        <v-progress-circular indeterminate color="amber-darken-2" />
      </div>
      <pre v-else ref="termoEl" class="termo-texto mb-4">{{ regulamentoTexto }}</pre>

      <v-divider class="mb-4" />

      <v-row dense>
        <!-- Foto -->
        <v-col cols="12" sm="6">
          <div class="campo-label">1. Foto (confirmação de identidade)</div>
          <div class="captura">
            <video v-show="!foto" ref="video" autoplay playsinline class="midia"></video>
            <img v-if="foto" :src="foto" class="midia" alt="Foto capturada" />
          </div>
          <div class="d-flex ga-2 mt-2">
            <v-btn v-if="!foto" size="small" color="primary" variant="tonal" prepend-icon="mdi-camera" :loading="iniciandoCam" @click="capturarFoto">Tirar foto</v-btn>
            <v-btn v-else size="small" variant="text" prepend-icon="mdi-camera-retake" @click="refazerFoto">Refazer</v-btn>
          </div>
          <div v-if="erroCam" class="text-caption text-error mt-1">{{ erroCam }}</div>
        </v-col>

        <!-- Assinatura -->
        <v-col cols="12" sm="6">
          <div class="campo-label">2. Assinatura</div>
          <div class="assinatura-box">
            <canvas ref="canvas" class="assinatura" @pointerdown="inicio" @pointermove="mover" @pointerup="fim" @pointerleave="fim"></canvas>
          </div>
          <div class="d-flex ga-2 mt-2">
            <v-btn size="small" variant="text" prepend-icon="mdi-eraser" @click="limparAssinatura">Limpar</v-btn>
            <span class="text-caption text-medium-emphasis align-self-center">Assine com o dedo ou mouse</span>
          </div>
        </v-col>
      </v-row>

      <!-- Localização -->
      <div class="mt-3">
        <div class="campo-label">3. Localização</div>
        <div class="d-flex align-center ga-2">
          <v-chip size="small" :color="geo ? 'success' : 'grey'" variant="tonal">
            <v-icon start size="14">mdi-map-marker</v-icon>
            {{ geo ? `${geo.lat.toFixed(5)}, ${geo.lng.toFixed(5)} (±${Math.round(geo.acc)}m)` : 'Não capturada' }}
          </v-chip>
          <v-btn size="small" variant="text" prepend-icon="mdi-crosshairs-gps" :loading="buscandoGeo" @click="capturarGeo">
            {{ geo ? 'Atualizar' : 'Capturar localização' }}
          </v-btn>
        </div>
      </div>

      <v-divider class="my-4" />

      <v-checkbox v-model="concordo" hide-details density="compact"
        label="Li e concordo com o regulamento e confirmo minha participação na premiação por desempenho." />

      <div class="d-flex justify-end mt-3">
        <v-btn color="amber-darken-2" size="large" prepend-icon="mdi-check-decagram"
          :disabled="!podeAssinar" :loading="enviando" @click="assinar">Assinar e confirmar</v-btn>
      </div>
      <div class="text-caption text-medium-emphasis mt-2">
        Registramos data/hora, IP e as evidências acima para fins de comprovação. Ao assinar, esses dados ficam guardados com segurança.
      </div>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const emit = defineEmits<{ (e: 'assinado'): void }>()
const auth = useAuthStore()
const notif = useNotifStore()

const regulamentoTexto = ref('')
const regulamentoVersao = ref('2.0')
const carregandoRegulamento = ref(true)

const termoEl = ref<HTMLElement>()
const video = ref<HTMLVideoElement>()
const canvas = ref<HTMLCanvasElement>()
const foto = ref<string | null>(null)
const geo = ref<{ lat: number; lng: number; acc: number } | null>(null)
const concordo = ref(false)
const enviando = ref(false)
const iniciandoCam = ref(false)
const buscandoGeo = ref(false)
const erroCam = ref('')
let stream: MediaStream | null = null
let assinou = ref(false)

const podeAssinar = computed(() => concordo.value && !!foto.value && assinou.value && !!regulamentoTexto.value)

// ── Webcam ──
async function iniciarCam() {
  try {
    iniciandoCam.value = true
    stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' }, audio: false })
    if (video.value) { video.value.srcObject = stream; await video.value.play() }
  } catch { erroCam.value = 'Não foi possível acessar a câmera. Verifique a permissão.' }
  finally { iniciandoCam.value = false }
}
function pararCam() { stream?.getTracks().forEach(t => t.stop()); stream = null }
function capturarFoto() {
  const v = video.value; if (!v || !v.videoWidth) { iniciarCam(); return }
  const c = document.createElement('canvas')
  c.width = 320; c.height = Math.round(320 * v.videoHeight / v.videoWidth)
  c.getContext('2d')!.drawImage(v, 0, 0, c.width, c.height)
  foto.value = c.toDataURL('image/jpeg', 0.7)
  pararCam()
}
function refazerFoto() { foto.value = null; iniciarCam() }

// ── Assinatura ──
let desenhando = false
function ctx() { return canvas.value!.getContext('2d')! }
function ajustarCanvas() {
  const cv = canvas.value; if (!cv) return
  const r = cv.getBoundingClientRect()
  cv.width = r.width; cv.height = r.height
  const g = ctx(); g.strokeStyle = '#111'; g.lineWidth = 2; g.lineCap = 'round'; g.lineJoin = 'round'
}
function pos(e: PointerEvent) { const r = canvas.value!.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top } }
function inicio(e: PointerEvent) { desenhando = true; const p = pos(e); const g = ctx(); g.beginPath(); g.moveTo(p.x, p.y) }
function mover(e: PointerEvent) { if (!desenhando) return; const p = pos(e); const g = ctx(); g.lineTo(p.x, p.y); g.stroke(); assinou.value = true }
function fim() { desenhando = false }
function limparAssinatura() { const cv = canvas.value!; ctx().clearRect(0, 0, cv.width, cv.height); assinou.value = false }

// ── Geolocalização ──
function capturarGeo() {
  if (!navigator.geolocation) { notif.aviso('Geolocalização não suportada.'); return }
  buscandoGeo.value = true
  navigator.geolocation.getCurrentPosition(
    p => { geo.value = { lat: p.coords.latitude, lng: p.coords.longitude, acc: p.coords.accuracy }; buscandoGeo.value = false },
    () => { buscandoGeo.value = false; notif.aviso('Não foi possível obter a localização (permissão negada).') },
    { enableHighAccuracy: true, timeout: 10000 })
}

async function sha256(txt: string) {
  const buf = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(txt))
  return Array.from(new Uint8Array(buf)).map(b => b.toString(16).padStart(2, '0')).join('')
}

async function carregarRegulamento() {
  carregandoRegulamento.value = true
  try {
    const r = await api.get('/premiacao/regulamento', { params: { empresaId: auth.empresaId } })
    regulamentoTexto.value = r.data.texto ?? ''
    regulamentoVersao.value = r.data.versao ?? '2.0'
  } catch {
    notif.erro('Não foi possível carregar o regulamento. Tente novamente.')
  } finally { carregandoRegulamento.value = false }
}

async function assinar() {
  if (!podeAssinar.value) return
  enviando.value = true
  try {
    const hash = await sha256(regulamentoTexto.value)
    await api.post('/premiacao/aceitar', {
      empresaId: auth.empresaId, termoVersao: regulamentoVersao.value, termoHash: hash,
      textoRegulamento: regulamentoTexto.value,
      fotoBase64: foto.value, assinaturaBase64: canvas.value?.toDataURL('image/png'),
      latitude: geo.value?.lat ?? null, longitude: geo.value?.lng ?? null, precisaoMetros: geo.value?.acc ?? null,
    })
    notif.ok('Termo assinado! Bem-vinda ao seu painel de desempenho.')
    emit('assinado')
  } catch { notif.erro('Erro ao registrar o aceite. Tente novamente.') }
  finally { enviando.value = false }
}

onMounted(async () => { ajustarCanvas(); window.addEventListener('resize', ajustarCanvas); carregarRegulamento(); await iniciarCam(); capturarGeo() })
onBeforeUnmount(() => { pararCam(); window.removeEventListener('resize', ajustarCanvas) })
</script>

<style scoped>
.termo-wrap { max-width: 780px; margin: 0 auto; }
.termo-texto { max-height: 340px; overflow-y: auto; font-size: 12.5px; line-height: 1.55;
  white-space: pre-wrap; word-break: break-word; font-family: inherit; margin: 0;
  background: rgba(128,128,128,.06); border: 1px solid rgba(128,128,128,.18); border-radius: 12px; padding: 14px 16px; }
.termo-texto p { margin: 0 0 10px; }
.campo-label { font-size: 11px; font-weight: 700; letter-spacing: .06em; text-transform: uppercase; color: #94a3b8; margin-bottom: 6px; }
.captura, .assinatura-box { border: 1px solid rgba(128,128,128,.3); border-radius: 12px; overflow: hidden; background: rgba(128,128,128,.06); }
.midia { width: 100%; height: 180px; object-fit: cover; display: block; background: #000; }
.assinatura { width: 100%; height: 180px; display: block; touch-action: none; cursor: crosshair; background: #fff; }
</style>
