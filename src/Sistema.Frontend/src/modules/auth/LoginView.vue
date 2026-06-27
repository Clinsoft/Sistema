<template>
  <v-app theme="ecoGranelLight">
    <v-main class="bg-background">
      <v-container class="fill-height" fluid>
        <v-row align="center" justify="center">
          <v-col cols="12" sm="8" md="5" lg="4">
            <v-card rounded="xl" elevation="4" class="pa-6">
              <div class="d-flex flex-column align-center justify-center mb-6">
                <img src="/logo-ecogranel.png" alt="EcoGranel" style="height:72px;object-fit:contain;margin-bottom:8px"
                  onerror="this.style.display='none';this.nextElementSibling.style.display='flex'"
                />
                <div style="display:none" class="align-center">
                  <v-icon icon="mdi-sprout" color="success" size="36" class="mr-2" />
                  <div>
                    <div class="text-h5 font-weight-bold text-primary">EcoGranel</div>
                    <div class="text-caption text-medium-emphasis">Produtos Naturais</div>
                  </div>
                </div>
                <div class="text-caption text-medium-emphasis mt-1">Sistema de Gestão</div>
              </div>

              <v-alert v-if="setupOk" type="success" variant="tonal" class="mb-4" density="compact">
                Sistema configurado! Faça login para começar.
              </v-alert>

              <v-form ref="form" @submit.prevent="entrar">
                <v-text-field
                  v-model="email"
                  label="E-mail"
                  type="email"
                  prepend-inner-icon="mdi-email-outline"
                  variant="outlined"
                  density="comfortable"
                  :rules="[r => !!r || 'Obrigatório']"
                  class="mb-3"
                />
                <v-text-field
                  v-model="senha"
                  label="Senha"
                  :type="mostrarSenha ? 'text' : 'password'"
                  prepend-inner-icon="mdi-lock-outline"
                  :append-inner-icon="mostrarSenha ? 'mdi-eye-off' : 'mdi-eye'"
                  @click:append-inner="mostrarSenha = !mostrarSenha"
                  variant="outlined"
                  density="comfortable"
                  :rules="[r => !!r || 'Obrigatório']"
                  class="mb-1"
                />

                <!-- Link esqueci o acesso -->
                <div class="text-right mb-3">
                  <a href="#" class="text-body-2 text-primary text-decoration-none"
                    @click.prevent="dialogRecuperar = true">
                    Esqueci meu acesso
                  </a>
                </div>

                <v-btn
                  type="submit"
                  color="primary"
                  block
                  size="large"
                  rounded="lg"
                  :loading="carregando"
                >
                  Entrar
                </v-btn>
              </v-form>

              <v-divider class="my-5" />

              <div class="text-center text-body-2 text-medium-emphasis">
                Primeira vez aqui?
                <router-link to="/setup" class="text-primary font-weight-medium text-decoration-none">
                  Configurar o sistema
                </router-link>
              </div>
            </v-card>
          </v-col>
        </v-row>
      </v-container>
    </v-main>

    <!-- Dialog: Recuperar Acesso -->
    <v-dialog v-model="dialogRecuperar" max-width="440" persistent>
      <v-card rounded="xl">
        <v-card-title class="d-flex align-center gap-2 pa-5 pb-2">
          <v-icon icon="mdi-lock-reset" color="primary" />
          Recuperar Acesso
        </v-card-title>

        <v-card-text class="pa-5 pt-2">
          <p class="text-body-2 text-medium-emphasis mb-4">
            Informe o <strong>CNPJ</strong> da empresa cadastrada no sistema. Enviaremos
            seu e-mail de acesso e uma nova senha para o endereço registrado.
          </p>

          <v-alert v-if="recuperarSucesso" type="success" variant="tonal" density="compact" class="mb-3">
            {{ recuperarSucesso }}
          </v-alert>

          <v-form v-if="!recuperarSucesso" ref="formRecuperar" @submit.prevent="recuperarAcesso">
            <v-text-field
              v-model="cnpjRecuperar"
              label="CNPJ da empresa"
              placeholder="AB.CDE.FGH/IJKL-00"
              prepend-inner-icon="mdi-domain"
              variant="outlined"
              density="comfortable"
              :rules="cnpjRules"
              maxlength="18"
              autofocus
              @keydown="onCnpjKeydown"
            />
          </v-form>
        </v-card-text>

        <v-card-actions class="pa-5 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="fecharDialogRecuperar">Fechar</v-btn>
          <v-btn v-if="!recuperarSucesso"
            color="primary" variant="flat"
            prepend-icon="mdi-send-outline"
            :loading="recuperandoAcesso"
            @click="recuperarAcesso">
            Enviar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-app>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import api from '@/composables/useApi'

const auth = useAuthStore()
const notif = useNotifStore()
const router = useRouter()
const route = useRoute()
const setupOk = computed(() => route.query.setupOk === '1')

const form = ref()
const email = ref('')
const senha = ref('')
const mostrarSenha = ref(false)
const carregando = ref(false)

async function entrar() {
  const { valid } = await form.value.validate()
  if (!valid) return
  carregando.value = true
  try {
    await auth.login(email.value, senha.value)
    router.push('/')
  } catch {
    notif.erro('E-mail ou senha inválidos.')
  } finally {
    carregando.value = false
  }
}

// ─── Recuperar acesso ──────────────────────────────────────────────
const dialogRecuperar = ref(false)
const formRecuperar = ref()
const cnpjRecuperar = ref('')
const recuperandoAcesso = ref(false)
const recuperarSucesso = ref('')

function limparCnpj(v: string) {
  return v.toUpperCase().replace(/[^A-Z0-9]/g, '')
}

// Regras declaradas como array constante — acessíveis diretamente no template
const cnpjRules = [
  (v: string) => !!v || 'Informe o CNPJ',
  (v: string) => {
    const r = limparCnpj(v)
    if (r.length !== 14) return 'CNPJ deve ter 14 caracteres'
    if (!/^\d{2}$/.test(r.slice(12))) return 'Dígitos verificadores inválidos'
    return true
  },
]

function onCnpjKeydown(e: KeyboardEvent) {
  // Permite: teclas de controle, backspace, delete, setas, tab
  if (['Backspace','Delete','ArrowLeft','ArrowRight','Tab','Enter'].includes(e.key)) return
  const raw = limparCnpj(cnpjRecuperar.value)
  if (raw.length >= 14) { e.preventDefault(); return }
  // Posições 12-13 só aceitam dígito
  if (raw.length >= 12 && !/[0-9]/.test(e.key)) { e.preventDefault(); return }
  if (!/[A-Za-z0-9]/.test(e.key)) e.preventDefault()
}

async function recuperarAcesso() {
  const { valid } = await formRecuperar.value.validate()
  if (!valid) return
  recuperandoAcesso.value = true
  try {
    const r = await api.post('/auth/recuperar-acesso', { cnpj: limparCnpj(cnpjRecuperar.value) })
    recuperarSucesso.value = r.data.mensagem
  } catch {
    notif.erro('Não foi possível processar a solicitação. Tente novamente.')
  } finally {
    recuperandoAcesso.value = false
  }
}

function fecharDialogRecuperar() {
  dialogRecuperar.value = false
  cnpjRecuperar.value = ''
  recuperarSucesso.value = ''
}
</script>
