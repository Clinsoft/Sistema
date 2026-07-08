<template>
  <div>
    <div class="d-flex align-center mb-4">
      <div>
        <div class="text-h6 font-weight-bold">Unidades / Filiais</div>
        <div class="text-caption text-medium-emphasis">
          Gerencie a matriz e as filiais do grupo. Cada unidade tem CNPJ próprio e dados completamente separados.
        </div>
      </div>
      <v-spacer />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="abrirNova">
        Nova Filial
      </v-btn>
    </div>

    <!-- Cards do grupo -->
    <v-row v-if="!carregando && grupo.length">
      <v-col v-for="emp in grupo" :key="emp.id" cols="12" sm="6" md="4">
        <v-card rounded="xl" elevation="2"
          :color="emp.id === auth.empresaId ? 'primary' : undefined"
          :variant="emp.id === auth.empresaId ? 'tonal' : 'elevated'">
          <v-card-text>
            <div class="d-flex align-center mb-2">
              <v-icon :icon="emp.tipoUnidade === 'Matriz' ? 'mdi-home-city-outline' : 'mdi-store-outline'"
                class="mr-2" :color="emp.id === auth.empresaId ? 'primary' : 'default'" />
              <v-chip size="x-small" :color="emp.tipoUnidade === 'Matriz' ? 'primary' : 'warning'"
                variant="tonal">{{ emp.tipoUnidade }}</v-chip>
              <v-spacer />
              <v-chip v-if="emp.id === auth.empresaId" size="x-small" color="success" variant="tonal">
                Ativa
              </v-chip>
            </div>
            <div class="text-body-1 font-weight-bold">{{ emp.nomeFantasia }}</div>
            <div class="text-body-2 text-medium-emphasis mb-1">{{ emp.razaoSocial }}</div>
            <div class="text-caption">
              <v-icon icon="mdi-identifier" size="14" class="mr-1" />{{ emp.cnpj }}<br>
              <v-icon icon="mdi-map-marker-outline" size="14" class="mr-1" />
              {{ emp.cidade }}/{{ emp.uf }}
            </div>
          </v-card-text>
          <v-card-actions class="pt-0">
            <v-btn v-if="emp.id !== auth.empresaId" size="small" variant="text"
              prepend-icon="mdi-swap-horizontal" @click="auth.trocarFilial(emp.id)">
              Acessar
            </v-btn>
            <v-btn size="small" variant="text" prepend-icon="mdi-pencil-outline"
              @click="abrirEditar(emp)">
              Editar
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-col>

      <!-- Card para adicionar -->
      <v-col cols="12" sm="6" md="4">
        <v-card rounded="xl" elevation="0" variant="outlined"
          class="d-flex align-center justify-center" style="min-height:160px;cursor:pointer;border-style:dashed"
          @click="abrirNova">
          <v-card-text class="text-center text-medium-emphasis">
            <v-icon icon="mdi-plus-circle-outline" size="36" class="mb-2" /><br>
            Adicionar filial
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <div v-else-if="carregando" class="text-center pa-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <!-- Informativo sobre isolamento de dados -->
    <v-alert type="info" variant="tonal" class="mt-4" icon="mdi-information-outline">
      <strong>Isolamento de dados por unidade:</strong>
      Estoque, vendas, financeiro, compras e fiscal são completamente separados por CNPJ.
      Produtos e cadastros (clientes, fornecedores) também são por unidade.
      Para acessar outra unidade, use o seletor no topo da tela.
    </v-alert>

    <!-- Dialog Nova/Editar Filial -->
    <v-dialog v-model="dialog" max-width="700" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 d-flex align-center">
          <v-icon :icon="editando ? 'mdi-pencil-outline' : 'mdi-plus-circle-outline'" class="mr-2" />
          {{ editando ? 'Editar Unidade' : 'Nova Filial' }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="dialog = false" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4">
          <v-form ref="formRef" @submit.prevent="salvar">
            <v-row dense>
              <v-col cols="12" sm="8">
                <v-text-field v-model="form.razaoSocial" label="Razão Social *"
                  variant="outlined" density="compact" :rules="[v => !!v || 'Obrigatório']" />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model="form.cnpj" label="CNPJ *"
                  variant="outlined" density="compact"
                  placeholder="00.000.000/0001-00"
                  :rules="[v => !!v || 'Obrigatório']" />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field v-model="form.nomeFantasia" label="Nome Fantasia *"
                  variant="outlined" density="compact" :rules="[v => !!v || 'Obrigatório']" />
              </v-col>
              <v-col cols="12" sm="6">
                <v-select v-model="form.regimeTributario" label="Regime Tributário *"
                  :items="['SN','LP','LR']" :item-title="(v: string) => ({ SN:'Simples Nacional', LP:'Lucro Presumido', LR:'Lucro Real' }[v] ?? v)"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field v-model="form.inscricaoEstadual" label="Inscrição Estadual"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field v-model="form.inscricaoMunicipal" label="Inscrição Municipal"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model="form.cep" label="CEP *" variant="outlined" density="compact"
                  @blur="buscarCep" />
              </v-col>
              <v-col cols="12" sm="8">
                <v-text-field v-model="form.logradouro" label="Logradouro *"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="4" sm="2">
                <v-text-field v-model="form.numero" label="Nº *" variant="outlined" density="compact" />
              </v-col>
              <v-col cols="8" sm="4">
                <v-text-field v-model="form.complemento" label="Complemento"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field v-model="form.bairro" label="Bairro *" variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="5">
                <v-text-field v-model="form.cidade" label="Cidade *" variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="2">
                <v-text-field v-model="form.uf" label="UF *" variant="outlined" density="compact" maxlength="2" />
              </v-col>
              <v-col cols="12" sm="5">
                <v-text-field v-model="form.telefone" label="Telefone *" variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="7">
                <v-text-field v-model="form.email" label="E-mail *" type="email"
                  variant="outlined" density="compact" />
              </v-col>
            </v-row>
          </v-form>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="dialog = false">Cancelar</v-btn>
          <v-btn color="primary" variant="flat" :loading="salvando" @click="salvar">
            {{ editando ? 'Salvar alterações' : 'Criar filial' }}
          </v-btn>
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

const carregando = ref(false)
const salvando = ref(false)
const dialog = ref(false)
const editando = ref<any>(null)
const grupo = ref<any[]>([])
const formRef = ref<any>(null)

const formVazio = () => ({
  razaoSocial: '', nomeFantasia: '', cnpj: '', regimeTributario: 'SN',
  inscricaoEstadual: '', inscricaoMunicipal: '',
  cep: '', logradouro: '', numero: '', complemento: '',
  bairro: '', cidade: '', uf: '', telefone: '', email: '',
})
const form = ref(formVazio())

async function carregar() {
  carregando.value = true
  try {
    const r = await api.get(`/empresas/${auth.empresaId}/grupo`)
    grupo.value = r.data
  } catch { /* silencioso */ } finally {
    carregando.value = false
  }
}

function abrirNova() {
  editando.value = null
  form.value = formVazio()
  dialog.value = true
}

function abrirEditar(emp: any) {
  editando.value = emp
  form.value = {
    razaoSocial: emp.razaoSocial, nomeFantasia: emp.nomeFantasia, cnpj: emp.cnpj,
    regimeTributario: emp.regimeTributario, inscricaoEstadual: emp.inscricaoEstadual ?? '',
    inscricaoMunicipal: emp.inscricaoMunicipal ?? '', cep: emp.cep,
    logradouro: emp.logradouro, numero: emp.numero, complemento: emp.complemento ?? '',
    bairro: emp.bairro, cidade: emp.cidade, uf: emp.uf,
    telefone: emp.telefone, email: emp.email,
  }
  dialog.value = true
}

async function buscarCep() {
  const cep = form.value.cep.replace(/\D/g, '')
  if (cep.length !== 8) return
  try {
    const r = await fetch(`https://viacep.com.br/ws/${cep}/json/`)
    const d = await r.json()
    if (!d.erro) {
      form.value.logradouro = d.logradouro
      form.value.bairro = d.bairro
      form.value.cidade = d.localidade
      form.value.uf = d.uf
    }
  } catch { /* silencioso */ }
}

async function salvar() {
  const { valid } = await formRef.value?.validate()
  if (!valid) return
  salvando.value = true
  try {
    if (editando.value) {
      await api.put(`/empresas/${editando.value.id}`, form.value)
      notif.ok('Unidade atualizada!')
    } else {
      // Cria filial vinculada à matriz da empresa atual
      const matrizId = grupo.value.find(e => e.tipoUnidade === 'Matriz')?.id ?? auth.empresaId
      await api.post(`/empresas/${matrizId}/filiais`, form.value)
      notif.ok('Filial criada! Atualize a página para vê-la no seletor.')
    }
    dialog.value = false
    await carregar()
    await auth.carregarFiliais()
  } catch {
    notif.erro('Erro ao salvar. Verifique os dados.')
  } finally {
    salvando.value = false
  }
}

onMounted(carregar)
</script>
