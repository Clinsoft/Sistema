<template>
  <div>
    <div class="text-h6 font-weight-bold mb-1">
      <v-icon class="mr-1" color="amber-darken-2">mdi-file-sign</v-icon>Aceites da Premiação
    </div>
    <p class="text-body-2 text-medium-emphasis mb-4">
      Trilha de auditoria das assinaturas do Termo de Premiação por Desempenho (foto, assinatura, localização, IP e data/hora).
    </p>

    <v-card rounded="xl" elevation="1">
      <v-table density="comfortable">
        <thead>
          <tr>
            <th>Colaborador(a)</th>
            <th>Data/hora</th>
            <th>Versão</th>
            <th>Localização</th>
            <th>IP</th>
            <th class="text-center">Evidências</th>
            <th class="text-right">Comprovante</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="a in aceites" :key="a.id">
            <td class="font-weight-medium">{{ a.colaboradorNome }}</td>
            <td>{{ fmtData(a.dataAceite) }}</td>
            <td>{{ a.termoVersao }}</td>
            <td>
              <a v-if="a.latitude" :href="`https://maps.google.com/?q=${a.latitude},${a.longitude}`" target="_blank" class="text-primary">
                {{ a.latitude.toFixed(4) }}, {{ a.longitude.toFixed(4) }}
              </a>
              <span v-else class="text-medium-emphasis">—</span>
            </td>
            <td class="text-medium-emphasis">{{ a.ip || '—' }}</td>
            <td class="text-center">
              <v-btn size="small" variant="text" icon="mdi-eye-outline" @click="abrir(a)" title="Ver evidências" />
            </td>
            <td class="text-right">
              <v-btn size="small" color="red-darken-1" variant="tonal" prepend-icon="mdi-file-pdf-box"
                :loading="baixando === a.id" @click="baixarPdf(a)">PDF</v-btn>
            </td>
          </tr>
          <tr v-if="!aceites.length">
            <td colspan="7" class="text-center text-medium-emphasis py-6">Nenhum aceite registrado ainda.</td>
          </tr>
        </tbody>
      </v-table>
    </v-card>

    <!-- Dialog de evidências -->
    <v-dialog v-model="dialog" max-width="640">
      <v-card v-if="sel" rounded="xl">
        <v-card-title class="d-flex align-center">
          <v-icon color="amber-darken-2" class="mr-2">mdi-shield-check-outline</v-icon>
          {{ sel.colaboradorNome }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="dialog = false" />
        </v-card-title>
        <v-card-text>
          <div class="text-caption text-medium-emphasis mb-2">Assinado em {{ fmtData(sel.dataAceite) }} · IP {{ sel.ip || '—' }}</div>
          <v-row dense>
            <v-col cols="6">
              <div class="campo">Foto</div>
              <img v-if="sel.fotoBase64" :src="sel.fotoBase64" class="ev-img" alt="Foto" />
              <div v-else class="ev-vazio">—</div>
            </v-col>
            <v-col cols="6">
              <div class="campo">Assinatura</div>
              <img v-if="sel.assinaturaBase64" :src="sel.assinaturaBase64" class="ev-img ev-assin" alt="Assinatura" />
              <div v-else class="ev-vazio">—</div>
            </v-col>
          </v-row>
          <div class="mt-3 text-body-2">
            <div><b>Localização:</b>
              <a v-if="sel.latitude" :href="`https://maps.google.com/?q=${sel.latitude},${sel.longitude}`" target="_blank">
                {{ sel.latitude.toFixed(5) }}, {{ sel.longitude.toFixed(5) }} (±{{ Math.round(sel.precisaoMetros || 0) }}m)
              </a>
              <span v-else>—</span>
            </div>
            <div class="text-caption text-medium-emphasis mt-1 text-break"><b>Hash:</b> {{ sel.termoHash || '—' }}</div>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn color="red-darken-1" variant="tonal" prepend-icon="mdi-file-pdf-box" :loading="baixando === sel.id" @click="baixarPdf(sel)">Baixar comprovante</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const aceites = ref<any[]>([])
const dialog = ref(false)
const sel = ref<any>(null)
const baixando = ref<string | null>(null)

const fmtData = (s: string) => s ? new Date(s).toLocaleString('pt-BR') : '—'

function abrir(a: any) { sel.value = a; dialog.value = true }

async function baixarPdf(a: any) {
  baixando.value = a.id
  try {
    const r = await api.get(`/premiacao/aceites/${a.id}/comprovante-pdf`, { responseType: 'blob' })
    const url = URL.createObjectURL(r.data as Blob)
    const link = document.createElement('a')
    link.href = url; link.download = `aceite-${a.colaboradorNome}.pdf`; link.click()
    setTimeout(() => URL.revokeObjectURL(url), 60000)
  } catch { notif.erro('Erro ao gerar o comprovante.') }
  finally { baixando.value = null }
}

onMounted(async () => {
  const r = await api.get('/premiacao/aceites', { params: { empresaId: auth.empresaId } }).catch(() => ({ data: [] }))
  aceites.value = r.data
})
</script>

<style scoped>
.campo { font-size: 11px; font-weight: 700; letter-spacing: .06em; text-transform: uppercase; color: #94a3b8; margin-bottom: 4px; }
.ev-img { width: 100%; height: 200px; object-fit: cover; border-radius: 10px; border: 1px solid rgba(128,128,128,.25); }
.ev-assin { object-fit: contain; background: #fff; }
.ev-vazio { height: 200px; display: grid; place-items: center; color: #94a3b8; border: 1px dashed rgba(128,128,128,.3); border-radius: 10px; }
.text-break { word-break: break-all; }
</style>
