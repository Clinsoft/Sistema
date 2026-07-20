<!--
  Scanner de código de barras por câmera (reutilizável).
  Uso:
    <BarcodeScanner v-model="mostrar" @detected="onCodigo" />
  Emite `detected(codigo: string)` ao ler um código e fecha sozinho.
  Tem fallback por foto (iOS Safari / sem permissão de vídeo).
-->
<template>
  <v-dialog :model-value="modelValue" max-width="420" persistent
    @update:model-value="v => !v && fechar()">
    <v-card rounded="xl">
      <v-card-title class="pa-4 pb-2 d-flex align-center">
        <v-icon start color="primary">mdi-barcode-scan</v-icon>
        {{ titulo }}
        <v-spacer />
        <v-btn icon size="small" variant="text" @click="fechar">
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </v-card-title>
      <v-card-text class="pa-0">
        <div class="bcs-wrap">
          <video ref="video" class="bcs-video" autoplay playsinline muted />
          <div class="bcs-overlay"><div class="bcs-frame" /></div>
          <div v-if="status" class="bcs-status">{{ status }}</div>
        </div>

        <div v-if="usarFotoFallback" class="pa-4 text-center">
          <v-icon size="40" color="primary" class="mb-2">mdi-camera</v-icon>
          <div class="text-body-2 text-medium-emphasis mb-4">
            Tire uma foto do código de barras
          </div>
          <input ref="inputFoto" type="file" accept="image/*" capture="environment"
            style="display:none" @change="lerFoto" />
          <v-btn color="primary" prepend-icon="mdi-camera" @click="inputFoto?.click()">
            Abrir Câmera
          </v-btn>
        </div>
      </v-card-text>
      <v-card-actions class="pa-4 pt-2">
        <v-btn variant="text" prepend-icon="mdi-keyboard" @click="fechar">Digitar manualmente</v-btn>
        <v-spacer />
        <v-select v-if="cameras.length > 1" v-model="cameraAtual" :items="cameras"
          item-title="label" item-value="deviceId" label="Câmera"
          variant="outlined" density="compact" hide-details style="max-width:160px"
          @update:model-value="trocarCamera" />
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, nextTick, watch, onUnmounted } from 'vue'
import { BrowserMultiFormatReader } from '@zxing/browser'

const props = withDefaults(defineProps<{
  modelValue: boolean
  titulo?: string
}>(), { titulo: 'Escanear código de barras' })

const emit = defineEmits<{
  'update:modelValue': [boolean]
  detected: [string]
}>()

const video = ref<HTMLVideoElement | null>(null)
const inputFoto = ref<HTMLInputElement | null>(null)
const status = ref('')
const usarFotoFallback = ref(false)
const cameras = ref<{ deviceId: string; label: string }[]>([])
const cameraAtual = ref('')
let reader: BrowserMultiFormatReader | null = null

watch(() => props.modelValue, v => { v ? abrir() : parar() })

async function abrir() {
  usarFotoFallback.value = false
  status.value = 'Iniciando câmera...'
  await nextTick()
  try {
    const devs = await BrowserMultiFormatReader.listVideoInputDevices()
    cameras.value = devs.map(d => ({ deviceId: d.deviceId, label: d.label || `Câmera ${d.deviceId.slice(0, 6)}` }))
    const traseira = devs.find(d => /back|rear|traseira|environment/i.test(d.label))
    cameraAtual.value = traseira?.deviceId ?? devs[0]?.deviceId ?? ''
    await iniciar()
  } catch (e: any) {
    if (e?.name === 'NotAllowedError') status.value = 'Permissão de câmera negada.'
    else status.value = ''
    usarFotoFallback.value = true
  }
}

async function iniciar() {
  if (!video.value) return
  status.value = 'Aponte para o código de barras...'
  reader = new BrowserMultiFormatReader()
  try {
    await reader.decodeFromVideoDevice(cameraAtual.value || undefined, video.value, (result) => {
      if (result) achou(result.getText())
    })
  } catch {
    status.value = 'Erro ao acessar câmera.'
    usarFotoFallback.value = true
  }
}

async function trocarCamera() {
  BrowserMultiFormatReader.releaseAllStreams()
  reader = null
  await nextTick()
  await iniciar()
}

function achou(codigo: string) {
  emit('detected', codigo)
  fechar()
}

function parar() {
  BrowserMultiFormatReader.releaseAllStreams()
  reader = null
  status.value = ''
}

function fechar() {
  parar()
  emit('update:modelValue', false)
}

async function lerFoto(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  if ('BarcodeDetector' in window) {
    try {
      const bd = new (window as any).BarcodeDetector({
        formats: ['ean_13', 'ean_8', 'code_128', 'code_39', 'qr_code', 'upc_a'],
      })
      const img = await createImageBitmap(file)
      const codes = await bd.detect(img)
      if (codes.length) { achou(codes[0].rawValue); return }
    } catch { /* cai no ZXing */ }
  }
  try {
    const url = URL.createObjectURL(file)
    const r = new BrowserMultiFormatReader()
    const result = await r.decodeFromImageUrl(url)
    URL.revokeObjectURL(url)
    achou(result.getText())
  } catch {
    status.value = 'Código não detectado. Tente outra foto mais nítida.'
  }
}

onUnmounted(parar)
</script>

<style scoped>
.bcs-wrap { position: relative; background: #000; aspect-ratio: 4/3; overflow: hidden; }
.bcs-video { width: 100%; height: 100%; object-fit: cover; display: block; }
.bcs-overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; }
.bcs-frame {
  width: 220px; height: 140px; border: 2.5px solid rgba(255,255,255,.85);
  border-radius: 10px; box-shadow: 0 0 0 9999px rgba(0,0,0,.45); position: relative;
}
.bcs-frame::before, .bcs-frame::after {
  content: ''; position: absolute; width: 28px; height: 28px; border-color: #3b82f6; border-style: solid;
}
.bcs-frame::before { top: -2px; left: -2px; border-width: 3px 0 0 3px; border-radius: 6px 0 0 0; }
.bcs-frame::after  { bottom: -2px; right: -2px; border-width: 0 3px 3px 0; border-radius: 0 0 6px 0; }
.bcs-status {
  position: absolute; bottom: 12px; left: 0; right: 0; text-align: center;
  font-size: 12px; color: rgba(255,255,255,.8); text-shadow: 0 1px 3px rgba(0,0,0,.8);
}
</style>
