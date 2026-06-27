<template>
  <v-app>
    <v-main style="background:#f0f4f0; min-height:100vh;">
      <v-container class="d-flex align-center justify-center py-8" style="min-height:100vh;">
        <v-card width="580" rounded="xl" elevation="3">
          <v-card-text class="pa-8">

            <!-- Logo -->
            <div class="d-flex flex-column align-center mb-6">
              <img src="/logo-ecogranel.png" alt="EcoGranel" style="height:72px;object-fit:contain;margin-bottom:8px"
                onerror="this.style.display='none';this.nextElementSibling.style.display='flex'"
              />
              <div style="display:none" class="align-center mb-2">
                <v-icon size="40" color="success" class="mr-2">mdi-sprout</v-icon>
                <span class="text-h5 font-weight-bold text-primary">EcoGranel</span>
              </div>
              <div class="text-h6 font-weight-bold text-primary">Bem-vindo ao EcoGranel!</div>
              <div class="text-body-2 text-medium-emphasis">Configure o sistema em 2 passos simples</div>
            </div>

            <!-- Indicador de passo -->
            <div class="d-flex align-center mb-6 gap-2">
              <v-chip :color="passo >= 1 ? 'success' : 'default'" size="small" class="font-weight-bold">1</v-chip>
              <span class="text-body-2" :class="passo === 1 ? 'font-weight-bold' : 'text-medium-emphasis'">Dados da Empresa</span>
              <v-divider class="flex-grow-1" />
              <v-chip :color="passo >= 2 ? 'success' : 'default'" size="small" class="font-weight-bold">2</v-chip>
              <span class="text-body-2" :class="passo === 2 ? 'font-weight-bold' : 'text-medium-emphasis'">Administrador</span>
            </div>

            <!-- Passo 1: Empresa -->
            <v-form v-if="passo === 1" ref="formEmpresa" @submit.prevent="irParaPasso2">
              <v-row dense>
                <v-col cols="12">
                  <v-text-field v-model="empresa.razaoSocial" label="Razão Social *"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="12">
                  <v-text-field v-model="empresa.nomeFantasia" label="Nome Fantasia *"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="6">
                  <v-text-field v-model="empresa.cnpj" label="CNPJ *" placeholder="00.000.000/0001-00"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="6">
                  <v-select v-model="empresa.regimeTributario" label="Regime Tributário *"
                    :items="regimes" variant="outlined" density="compact"
                    :rules="[r => !!r || 'Obrigatório']" />
                </v-col>
                <v-col cols="5">
                  <v-text-field v-model="empresa.cep" label="CEP *" placeholder="00000-000"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact"
                    @blur="buscarCep" />
                </v-col>
                <v-col cols="7">
                  <v-text-field v-model="empresa.logradouro" label="Logradouro *"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="3">
                  <v-text-field v-model="empresa.numero" label="Nº *"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="9">
                  <v-text-field v-model="empresa.bairro" label="Bairro *"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="8">
                  <v-text-field v-model="empresa.cidade" label="Cidade *"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="4">
                  <v-text-field v-model="empresa.uf" label="UF *" maxlength="2"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="6">
                  <v-text-field v-model="empresa.telefone" label="Telefone *"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="6">
                  <v-text-field v-model="empresa.email" label="E-mail da empresa *"
                    type="email" :rules="[r => !!r || 'Obrigatório']"
                    variant="outlined" density="compact" />
                </v-col>
              </v-row>
              <v-btn type="submit" color="success" block class="mt-4" size="large">
                Continuar <v-icon end>mdi-arrow-right</v-icon>
              </v-btn>
            </v-form>

            <!-- Passo 2: Admin -->
            <v-form v-if="passo === 2" ref="formAdmin" @submit.prevent="concluir">
              <v-row dense>
                <v-col cols="12">
                  <v-text-field v-model="admin.nome" label="Nome completo do administrador *"
                    :rules="[r => !!r || 'Obrigatório']" variant="outlined" density="compact" />
                </v-col>
                <v-col cols="12">
                  <v-text-field v-model="admin.email" label="E-mail (será usado para login) *"
                    type="email"
                    :rules="[r => !!r || 'Obrigatório', r => /.+@.+\..+/.test(r) || 'E-mail inválido']"
                    variant="outlined" density="compact" />
                </v-col>
                <v-col cols="12">
                  <v-text-field v-model="admin.senha" label="Senha *"
                    :type="mostrarSenha ? 'text' : 'password'"
                    :append-inner-icon="mostrarSenha ? 'mdi-eye-off' : 'mdi-eye'"
                    @click:append-inner="mostrarSenha = !mostrarSenha"
                    :rules="[r => !!r || 'Obrigatório', r => r.length >= 6 || 'Mínimo 6 caracteres']"
                    variant="outlined" density="compact" />
                </v-col>
                <v-col cols="12">
                  <v-text-field v-model="admin.confirmarSenha" label="Confirmar senha *"
                    :type="mostrarSenha ? 'text' : 'password'"
                    :rules="[r => !!r || 'Obrigatório', r => r === admin.senha || 'Senhas não conferem']"
                    variant="outlined" density="compact" />
                </v-col>
              </v-row>

              <v-alert v-if="erro" type="error" variant="tonal" class="mt-2 mb-3" density="compact">
                {{ erro }}
              </v-alert>

              <div class="d-flex gap-2 mt-4">
                <v-btn variant="outlined" @click="passo = 1" :disabled="salvando">
                  <v-icon start>mdi-arrow-left</v-icon> Voltar
                </v-btn>
                <v-btn type="submit" color="success" class="flex-grow-1" size="large" :loading="salvando">
                  <v-icon start>mdi-check-circle</v-icon> Concluir configuração
                </v-btn>
              </div>
            </v-form>

          </v-card-text>
        </v-card>
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import axios from 'axios'

const router = useRouter()
const passo = ref(1)
const salvando = ref(false)
const erro = ref('')
const mostrarSenha = ref(false)
const formEmpresa = ref()
const formAdmin = ref()

const regimes = [
  { title: 'Simples Nacional', value: 'SimplesNacional' },
  { title: 'Lucro Presumido', value: 'LucroPresumido' },
  { title: 'Lucro Real', value: 'LucroReal' },
]

const empresa = ref({
  razaoSocial: '', nomeFantasia: '', cnpj: '', regimeTributario: 'SimplesNacional',
  cep: '', logradouro: '', numero: '', bairro: '', cidade: '', uf: '',
  telefone: '', email: '', complemento: '',
})

const admin = ref({ nome: '', email: '', senha: '', confirmarSenha: '' })

async function buscarCep() {
  const cep = empresa.value.cep.replace(/\D/g, '')
  if (cep.length !== 8) return
  try {
    const res = await axios.get(`https://viacep.com.br/ws/${cep}/json/`)
    if (!res.data.erro) {
      empresa.value.logradouro = res.data.logradouro
      empresa.value.bairro = res.data.bairro
      empresa.value.cidade = res.data.localidade
      empresa.value.uf = res.data.uf
    }
  } catch { /* silencioso */ }
}

async function irParaPasso2() {
  const { valid } = await formEmpresa.value.validate()
  if (valid) passo.value = 2
}

async function concluir() {
  const { valid } = await formAdmin.value.validate()
  if (!valid) return
  salvando.value = true
  erro.value = ''
  try {
    const baseUrl = import.meta.env.VITE_API_URL ?? ''
    await axios.post(`${baseUrl}/api/setup`, {
      ...empresa.value,
      nomeAdmin: admin.value.nome,
      emailAdmin: admin.value.email,
      senhaAdmin: admin.value.senha,
    })
    router.push({ path: '/login', query: { setupOk: '1' } })
  } catch (e: any) {
    erro.value = e.response?.data?.mensagem ?? 'Erro ao configurar. Verifique a conexão com o servidor.'
  } finally {
    salvando.value = false
  }
}
</script>
