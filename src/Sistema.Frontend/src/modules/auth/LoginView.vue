<template>
  <v-app theme="ecoGranelLight">
    <v-main class="bg-background">
      <v-container class="fill-height" fluid>
        <v-row align="center" justify="center">
          <v-col cols="12" sm="8" md="5" lg="4">
            <v-card rounded="xl" elevation="4" class="pa-6">
              <div class="d-flex flex-column align-center justify-center mb-6">
                <img :src="branding.logoUrl" :alt="branding.nome" style="height:72px;object-fit:contain;margin-bottom:8px"
                  onerror="this.style.display='none';this.nextElementSibling.style.display='flex'"
                />
                <div style="display:none" class="align-center">
                  <v-icon icon="mdi-sprout" color="success" size="36" class="mr-2" />
                  <div>
                    <div class="text-h5 font-weight-bold text-primary">{{ branding.nome }}</div>
                    <div class="text-caption text-medium-emphasis">{{ branding.slogan }}</div>
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

              <div class="text-center my-5 d-flex align-center">
                <v-divider />
                <span class="px-3 text-caption text-medium-emphasis">ou</span>
                <v-divider />
              </div>

              <!-- Chamada para novos clientes: precisa ficar evidente para quem
                   acabou de contratar e ainda não tem configurado o sistema. -->
              <div class="primeira-vez pa-4 rounded-lg text-center">
                <div class="text-subtitle-2 font-weight-bold mb-1">
                  <v-icon icon="mdi-hand-wave" color="accent" size="20" class="mr-1" />
                  É a sua primeira vez aqui?
                </div>
                <div class="text-body-2 text-medium-emphasis mb-3">
                  Configure sua loja em poucos minutos e comece a usar.
                </div>
                <v-btn
                  :to="branding.autoCadastro ? '/cadastro' : '/setup'"
                  color="accent"
                  block
                  size="large"
                  rounded="lg"
                  variant="flat"
                  prepend-icon="mdi-rocket-launch-outline"
                  class="text-none font-weight-bold"
                >
                  Começar agora
                </v-btn>
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
            Informe o <strong>e-mail</strong> cadastrado. Vamos gerar uma nova senha
            e enviar para esse endereço.
          </p>

          <v-alert v-if="recuperarSucesso" type="success" variant="tonal" density="compact" class="mb-3">
            {{ recuperarSucesso }}
          </v-alert>

          <v-form v-if="!recuperarSucesso" ref="formRecuperar" @submit.prevent="recuperarAcesso">
            <v-text-field
              v-model="emailRecuperar"
              label="E-mail cadastrado"
              type="email"
              placeholder="voce@sualoja.com.br"
              prepend-inner-icon="mdi-email-outline"
              variant="outlined"
              density="comfortable"
              :rules="[r => !!r || 'Informe o e-mail']"
              autofocus
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
import { branding } from '@/branding'

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
    // Atendente cai direto no "Meu Desempenho"; demais perfis vão para a home.
    router.push(auth.usuario?.role === 'Atendente' ? '/desempenho/meu' : '/')
  } catch {
    notif.erro('E-mail ou senha inválidos.')
  } finally {
    carregando.value = false
  }
}

// ─── Recuperar acesso ──────────────────────────────────────────────
const dialogRecuperar = ref(false)
const formRecuperar = ref()
const emailRecuperar = ref('')
const recuperandoAcesso = ref(false)
const recuperarSucesso = ref('')

async function recuperarAcesso() {
  const { valid } = await formRecuperar.value.validate()
  if (!valid) return
  recuperandoAcesso.value = true
  try {
    const r = await api.post('/auth/recuperar-acesso', { email: emailRecuperar.value.trim() })
    recuperarSucesso.value = r.data.mensagem
  } catch {
    notif.erro('Não foi possível processar a solicitação. Tente novamente.')
  } finally {
    recuperandoAcesso.value = false
  }
}

function fecharDialogRecuperar() {
  dialogRecuperar.value = false
  emailRecuperar.value = ''
  recuperarSucesso.value = ''
}
</script>

<style scoped>
/* Destaque suave para a chamada de primeiro acesso (novos clientes) */
.primeira-vez {
  background: rgba(var(--v-theme-accent), 0.10);
  border: 1px solid rgba(var(--v-theme-accent), 0.35);
}
</style>
