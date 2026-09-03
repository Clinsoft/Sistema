<template>
  <div>
    <div class="d-flex align-center mb-4 gap-2 flex-wrap">
      <div class="flex-grow-1">
        <div class="text-h6 font-weight-bold">Tutoriais</div>
        <div class="text-caption text-medium-emphasis">Vídeos e passo a passo para tirar dúvidas do dia a dia</div>
      </div>
      <template v-if="ehGestor">
        <v-btn v-if="!tutoriais.length" variant="tonal" color="secondary" prepend-icon="mdi-playlist-plus"
          :loading="semeando" @click="semear">Adicionar tópicos sugeridos</v-btn>
        <v-btn color="primary" prepend-icon="mdi-plus" rounded="lg" @click="abrirNovo">Novo Tutorial</v-btn>
      </template>
    </div>

    <div v-if="!carregando && !tutoriais.length" class="text-center text-medium-emphasis pa-8">
      <v-icon size="48" color="grey-lighten-1">mdi-school-outline</v-icon>
      <div class="mt-2">Nenhum tutorial cadastrado ainda.</div>
      <div v-if="ehGestor" class="text-caption">Use “Adicionar tópicos sugeridos” para começar com o passo a passo pronto.</div>
    </div>

    <v-row>
      <v-col v-for="t in tutoriais" :key="t.id" cols="12" md="6">
        <v-card rounded="xl" elevation="1" class="h-100 d-flex flex-column">
          <div v-if="embedUrl(t.videoUrl)" class="video-wrap">
            <iframe :src="embedUrl(t.videoUrl)" allowfullscreen frameborder="0"
              allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" />
          </div>
          <div v-else-if="t.videoUrl" class="pa-3">
            <v-btn :href="t.videoUrl" target="_blank" color="primary" variant="tonal" block
              prepend-icon="mdi-open-in-new">Abrir vídeo</v-btn>
          </div>
          <div v-else class="video-placeholder">
            <v-icon size="40" color="grey-lighten-1">mdi-video-off-outline</v-icon>
            <span class="text-caption text-medium-emphasis">Sem vídeo{{ ehGestor ? ' — edite para adicionar o link' : '' }}</span>
          </div>

          <v-card-title class="text-body-1 font-weight-bold d-flex align-center">
            <v-chip v-if="t.categoria" size="x-small" color="primary" variant="tonal" class="mr-2">{{ t.categoria }}</v-chip>
            {{ t.titulo }}
            <v-spacer />
            <template v-if="ehGestor">
              <v-btn icon="mdi-pencil-outline" size="x-small" variant="text" @click="abrirEdicao(t)" />
              <v-btn icon="mdi-delete-outline" size="x-small" variant="text" color="error" @click="excluir(t)" />
            </template>
          </v-card-title>
          <v-card-text v-if="t.descricao" class="text-body-2" style="white-space:pre-line">{{ t.descricao }}</v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Dialog novo/editar (gestor) -->
    <v-dialog v-model="dialog" max-width="620" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 d-flex align-center">
          {{ editandoId ? 'Editar Tutorial' : 'Novo Tutorial' }}
          <v-spacer /><v-btn icon="mdi-close" variant="text" @click="dialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field v-model="form.titulo" label="Título *" variant="outlined" density="compact" class="mb-3" />
          <v-row dense class="mb-1">
            <v-col cols="8">
              <v-text-field v-model="form.categoria" label="Categoria (ex.: PDV, Compras)"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="4">
              <v-text-field v-model.number="form.ordem" label="Ordem" type="number"
                variant="outlined" density="compact" />
            </v-col>
          </v-row>
          <v-text-field v-model="form.videoUrl" label="Link do vídeo (YouTube/Vimeo/MP4)"
            placeholder="https://youtu.be/..." variant="outlined" density="compact" class="mb-3"
            prepend-inner-icon="mdi-video-outline" clearable />
          <v-textarea v-model="form.descricao" label="Passo a passo" rows="7" auto-grow
            variant="outlined" density="compact" hint="Uma etapa por linha" persistent-hint />
          <v-switch v-model="form.ativo" color="primary" density="compact" hide-details
            :label="form.ativo ? 'Visível para o atendente' : 'Oculto'" class="mt-2" />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3 justify-end">
          <v-btn variant="text" @click="dialog = false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvando" :disabled="!form.titulo.trim()" @click="salvar">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const ehGestor = computed(() => ['Administrador', 'Gerente'].includes(auth.usuario?.role ?? ''))

const carregando = ref(false)
const tutoriais = ref<any[]>([])
const dialog = ref(false)
const salvando = ref(false)
const semeando = ref(false)
const editandoId = ref<string | null>(null)
const formPadrao = () => ({ titulo: '', categoria: '', videoUrl: '', descricao: '', ordem: 0, ativo: true })
const form = ref(formPadrao())

// Converte link do YouTube/Vimeo em URL de embed. Outros links não embedam (mostra botão).
function embedUrl(url?: string | null): string | null {
  if (!url) return null
  const u = url.trim()
  let m = u.match(/(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/embed\/|youtube\.com\/shorts\/)([\w-]{11})/)
  if (m) return `https://www.youtube.com/embed/${m[1]}`
  m = u.match(/vimeo\.com\/(?:video\/)?(\d+)/)
  if (m) return `https://player.vimeo.com/video/${m[1]}`
  if (/\.(mp4|webm|ogg)(\?|$)/i.test(u)) return u // vídeo direto tocaria em <video>, mas mantemos simples
  // Simulação animada hospedada no próprio sistema (mesma origem, arquivo .html).
  if (/^\/tutoriais\/.+\.html?($|\?)/i.test(u)) return u
  if (/\.html?($|\?)/i.test(u) && u.startsWith(location.origin)) return u
  return null
}

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get('/tutoriais', { params: { empresaId: auth.empresaId } })
    tutoriais.value = r.data ?? []
  } catch { tutoriais.value = [] } finally { carregando.value = false }
}

function abrirNovo() { editandoId.value = null; form.value = formPadrao(); form.value.ordem = tutoriais.value.length; dialog.value = true }
function abrirEdicao(t: any) {
  editandoId.value = t.id
  form.value = { titulo: t.titulo, categoria: t.categoria ?? '', videoUrl: t.videoUrl ?? '', descricao: t.descricao ?? '', ordem: t.ordem ?? 0, ativo: t.ativo }
  dialog.value = true
}
async function salvar() {
  salvando.value = true
  try {
    const dados = { ...form.value, empresaId: auth.empresaId }
    if (editandoId.value) await api.put(`/tutoriais/${editandoId.value}`, dados)
    else await api.post('/tutoriais', dados)
    notif.ok('Tutorial salvo!')
    dialog.value = false
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao salvar.') }
  finally { salvando.value = false }
}
async function excluir(t: any) {
  if (!confirm(`Excluir o tutorial "${t.titulo}"?`)) return
  try { await api.delete(`/tutoriais/${t.id}`); notif.ok('Tutorial excluído.'); await carregar() }
  catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao excluir.') }
}
async function semear() {
  semeando.value = true
  try {
    const r = await api.post('/tutoriais/seed', null, { params: { empresaId: auth.empresaId } })
    notif.ok(`${r.data?.criados ?? 0} tópico(s) adicionado(s). Agora edite cada um para incluir o link do vídeo.`)
    await carregar()
  } catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Erro ao adicionar tópicos.') }
  finally { semeando.value = false }
}

onMounted(carregar)
</script>

<style scoped>
.video-wrap { position: relative; width: 100%; padding-top: 56.25%; background: #000; border-radius: 12px 12px 0 0; overflow: hidden; }
.video-wrap iframe { position: absolute; inset: 0; width: 100%; height: 100%; }
.video-placeholder { height: 140px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 6px; background: rgba(0,0,0,.03); border-radius: 12px 12px 0 0; }
</style>
