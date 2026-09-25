<template>
  <v-app theme="ecoGranelLight">
    <v-main class="bg-background">
      <v-container class="fill-height" fluid>
        <v-row align="center" justify="center">
          <v-col cols="12" sm="9" md="6" lg="5">
            <v-card rounded="xl" elevation="4" class="pa-6">
              <div class="d-flex flex-column align-center mb-4">
                <img :src="branding.logoUrl" :alt="branding.nome" style="height:64px;object-fit:contain;margin-bottom:6px"
                  onerror="this.style.display='none'" />
                <div class="text-h6 font-weight-bold text-primary">Crie sua conta grátis</div>
                <div class="text-body-2 text-medium-emphasis">14 dias grátis · sem cartão de crédito</div>
              </div>

              <v-alert v-if="erro" type="error" variant="tonal" density="compact" class="mb-3">{{ erro }}</v-alert>

              <v-form ref="form" @submit.prevent="cadastrar">
                <div class="text-overline text-medium-emphasis">Sua loja</div>
                <v-text-field v-model="f.nomeLoja" label="Nome da loja" prepend-inner-icon="mdi-store"
                  variant="outlined" density="comfortable" :rules="[obrig]" class="mb-2" />
                <v-row dense>
                  <v-col cols="12" sm="7">
                    <v-text-field v-model="f.cnpj" label="CNPJ" prepend-inner-icon="mdi-card-account-details-outline"
                      variant="outlined" density="comfortable" :rules="[obrig]" />
                  </v-col>
                  <v-col cols="12" sm="5">
                    <v-text-field v-model="f.telefone" label="WhatsApp / Telefone" prepend-inner-icon="mdi-whatsapp"
                      variant="outlined" density="comfortable" />
                  </v-col>
                </v-row>

                <div class="text-overline text-medium-emphasis mt-2">Seu acesso</div>
                <v-text-field v-model="f.nomeAdmin" label="Seu nome" prepend-inner-icon="mdi-account"
                  variant="outlined" density="comfortable" :rules="[obrig]" class="mb-2" />
                <v-text-field v-model="f.emailAdmin" label="E-mail (login)" type="email" prepend-inner-icon="mdi-email-outline"
                  variant="outlined" density="comfortable" :rules="[obrig]" class="mb-2" />
                <v-text-field v-model="f.senhaAdmin" label="Senha (mín. 6)" :type="ver ? 'text' : 'password'"
                  prepend-inner-icon="mdi-lock-outline" :append-inner-icon="ver ? 'mdi-eye-off' : 'mdi-eye'"
                  @click:append-inner="ver = !ver" variant="outlined" density="comfortable" :rules="[obrig, min6]" />

                <v-btn type="submit" color="accent" block size="large" rounded="lg" :loading="salvando"
                  class="text-none font-weight-bold mt-2" prepend-icon="mdi-rocket-launch-outline">
                  Começar meu teste grátis
                </v-btn>
              </v-form>

              <v-divider class="my-4" />
              <div class="text-center text-body-2 text-medium-emphasis">
                Já tem conta?
                <router-link to="/login" class="text-primary font-weight-medium text-decoration-none">Entrar</router-link>
              </div>
            </v-card>
            <div class="text-center text-caption text-medium-emphasis mt-3">
              Ao criar a conta você concorda com os termos de uso do {{ branding.nome }}.
            </div>
          </v-col>
        </v-row>
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { branding } from '@/branding'

const router = useRouter()
const auth = useAuthStore()
const form = ref()
const salvando = ref(false)
const ver = ref(false)
const erro = ref('')

const f = reactive({ nomeLoja: '', cnpj: '', telefone: '', nomeAdmin: '', emailAdmin: '', senhaAdmin: '' })

const obrig = (v: string) => !!v || 'Obrigatório'
const min6 = (v: string) => (v?.length ?? 0) >= 6 || 'Mínimo 6 caracteres'

async function cadastrar() {
  erro.value = ''
  const { valid } = await form.value.validate()
  if (!valid) return
  salvando.value = true
  try {
    await api.post('/cadastro', { ...f }, { _quiet: true } as any)
    // Auto-login: já entra no sistema em trial.
    await auth.login(f.emailAdmin, f.senhaAdmin)
    router.push('/')
  } catch (e: any) {
    erro.value = e?.response?.data?.mensagem ?? 'Não foi possível criar a conta. Tente novamente.'
  } finally {
    salvando.value = false
  }
}
</script>
