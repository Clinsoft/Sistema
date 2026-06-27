<template>
  <v-app :theme="tema">
    <template v-if="auth.logado">
      <!-- Drawer lateral -->
      <v-navigation-drawer v-model="drawer" :rail="rail" permanent>
        <v-list-item nav class="py-3">
          <template #prepend>
            <img src="/logo-ecogranel.png" alt="EcoGranel"
              :style="rail ? 'height:32px;width:32px;object-fit:contain' : 'height:36px;object-fit:contain'"
              onerror="this.style.display='none';this.nextElementSibling.style.display='inline-flex'"
            />
            <v-icon icon="mdi-sprout" color="success"
              style="display:none" size="28" />
          </template>
          <template #title>
            <span v-if="!rail" class="font-weight-bold text-primary" style="font-size:15px;line-height:1.2">
              EcoGranel<br>
              <span class="text-caption text-medium-emphasis font-weight-regular">Produtos Naturais</span>
            </span>
          </template>
          <template #append>
            <v-btn :icon="rail ? 'mdi-chevron-right' : 'mdi-chevron-left'"
              variant="text" @click="rail = !rail" />
          </template>
        </v-list-item>

        <v-divider />

        <v-list density="compact" nav open-strategy="multiple">

          <!-- 1. Dashboard -->
          <v-list-item prepend-icon="mdi-view-dashboard-outline" title="Dashboard"
            to="/" value="/" color="primary" rounded="lg" />

          <!-- 2. PDV / Vendas -->
          <v-list-group value="pdv">
            <template #activator="{ props }">
              <v-list-item v-bind="props" prepend-icon="mdi-cash-register"
                title="PDV / Vendas" color="primary" rounded="lg" />
            </template>
            <v-list-item prepend-icon="mdi-point-of-sale" title="Caixa (PDV)"
              to="/pdv" value="/pdv" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-history" title="Histórico de Vendas"
              to="/pdv/vendas" value="/pdv/vendas" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-cash-lock-open" title="Sessões de Caixa"
              to="/pdv/sessoes" value="/pdv/sessoes" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-keyboard-return" title="Devoluções"
              to="/pdv/devolucoes" value="/pdv/devolucoes" color="warning" rounded="lg" class="pl-4" />
          </v-list-group>

          <!-- 3. Cadastros (inclui Produtos) -->
          <v-list-group value="cadastros">
            <template #activator="{ props }">
              <v-list-item v-bind="props" prepend-icon="mdi-account-group-outline"
                title="Cadastros" color="primary" rounded="lg" />
            </template>
            <v-list-item prepend-icon="mdi-tag-multiple-outline" title="Categorias"
              to="/cadastros/categorias" value="/cadastros/categorias" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-account-group-outline" title="Clientes"
              to="/cadastros/clientes" value="/cadastros/clientes" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-account-tie-outline" title="Colaboradores"
              to="/cadastros/colaboradores" value="/cadastros/colaboradores" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-truck-delivery-outline" title="Fornecedores"
              to="/cadastros/fornecedores" value="/cadastros/fornecedores" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-warehouse" title="Locais de Estoque"
              to="/cadastros/locais-estoque" value="/cadastros/locais-estoque" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-package-variant-closed" title="Produtos"
              to="/estoque/produtos" value="/estoque/produtos" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-ruler-square" title="Unidades de Medida"
              to="/cadastros/unidades-medida" value="/cadastros/unidades-medida" color="primary" rounded="lg" class="pl-4" />
          </v-list-group>

          <!-- 4. Financeiro -->
          <v-list-group value="financeiro">
            <template #activator="{ props }">
              <v-list-item v-bind="props" prepend-icon="mdi-currency-usd"
                title="Financeiro" color="primary" rounded="lg" />
            </template>
            <v-list-item prepend-icon="mdi-view-dashboard" title="Painel Financeiro"
              to="/financeiro" value="/financeiro" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-cash-plus" title="Contas a Receber"
              to="/financeiro/contas-receber" value="/financeiro/contas-receber" color="success" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-cash-minus" title="Contas a Pagar"
              to="/financeiro/contas-pagar" value="/financeiro/contas-pagar" color="error" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-chart-line" title="DRE"
              to="/financeiro/dre" value="/financeiro/dre" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-cash-flow" title="Fluxo de Caixa"
              to="/financeiro/fluxo-caixa" value="/financeiro/fluxo-caixa" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-calendar-month-outline" title="Planejamento Anual"
              to="/relatorios/planejamento-anual" value="/relatorios/planejamento-anual" color="blue-darken-2" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-credit-card-clock-outline" title="Recebíveis de Cartão"
              to="/financeiro/recebiveis-cartao" value="/financeiro/recebiveis-cartao" color="indigo" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-credit-card-settings-outline" title="Operadoras de Cartão"
              to="/financeiro/operadoras-cartao" value="/financeiro/operadoras-cartao" color="grey-darken-1" rounded="lg" class="pl-4" />
          </v-list-group>

          <!-- 5. Relatórios -->
          <v-list-item prepend-icon="mdi-chart-bar" title="Relatórios"
            to="/relatorios" value="/relatorios" color="primary" rounded="lg" />

          <!-- Restante em ordem alfabética -->
          <v-list-item prepend-icon="mdi-book-open-outline" title="Contabilidade"
            to="/contabilidade" value="/contabilidade" color="primary" rounded="lg" />

          <v-list-item prepend-icon="mdi-truck-delivery-outline" title="Compras"
            to="/compras" value="/compras" color="primary" rounded="lg" />

          <v-list-item prepend-icon="mdi-account-multiple-outline" title="Crediário"
            to="/crediario" value="/crediario" color="primary" rounded="lg" />

          <!-- Estoque (sem Produtos) -->
          <v-list-group value="estoque">
            <template #activator="{ props }">
              <v-list-item v-bind="props" prepend-icon="mdi-package-variant-closed"
                title="Estoque" color="primary" rounded="lg" />
            </template>
            <v-list-item prepend-icon="mdi-view-dashboard" title="Painel Estoque"
              to="/estoque" value="/estoque" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-swap-horizontal" title="Movimentações"
              to="/estoque/movimentacoes" value="/estoque/movimentacoes" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-list-status" title="Posição de Estoque"
              to="/estoque/posicao" value="/estoque/posicao" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-package-variant" title="Lotes e Validades"
              to="/estoque/lotes" value="/estoque/lotes" color="warning" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-transfer" title="Transferências"
              to="/estoque/transferencias" value="/estoque/transferencias" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-tune-vertical" title="Ajuste de Estoque"
              to="/estoque/ajuste" value="/estoque/ajuste" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-tag-outline" title="Etiquetas"
              to="/estoque/etiquetas" value="/estoque/etiquetas" color="primary" rounded="lg" class="pl-4" />
          </v-list-group>

          <v-list-item prepend-icon="mdi-file-document-outline" title="Fiscal / NF-e"
            to="/fiscal" value="/fiscal" color="primary" rounded="lg" />

          <v-list-group value="marketing">
            <template #activator="{ props }">
              <v-list-item v-bind="props" prepend-icon="mdi-bullhorn-outline"
                title="Marketing" color="primary" rounded="lg" />
            </template>
            <v-list-item prepend-icon="mdi-image-multiple-outline" title="Artes para Redes"
              to="/marketing" value="/marketing" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-star-circle-outline" title="Clube de Promoções"
              to="/marketing/clube" value="/marketing/clube" color="purple" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-tag-multiple-outline" title="Promoções"
              to="/marketing/promocoes" value="/marketing/promocoes" color="error" rounded="lg" class="pl-4" />
          </v-list-group>

          <v-list-item prepend-icon="mdi-whatsapp" title="WhatsApp"
            to="/whatsapp" value="/whatsapp" color="primary" rounded="lg" />

        </v-list>

        <template #append>
          <v-divider />
          <v-list density="compact" nav class="pb-2">
            <v-list-item prepend-icon="mdi-cog-outline" title="Configurações"
              to="/configuracoes" color="primary" rounded="lg" />
            <v-list-item prepend-icon="mdi-logout" title="Sair"
              @click="auth.sair()" color="error" rounded="lg" />
          </v-list>
        </template>
      </v-navigation-drawer>

      <!-- Top bar -->
      <v-app-bar elevation="1" color="surface">
        <template #prepend>
          <v-btn :icon="rail ? 'mdi-menu' : 'mdi-menu-open'"
            variant="text" @click="rail = !rail" />
        </template>
        <v-app-bar-title>
          <span class="text-body-1 font-weight-medium">{{ tituloPagina }}</span>
        </v-app-bar-title>
        <template #append>
          <v-btn :icon="tema === 'clinsoftLight' ? 'mdi-weather-night' : 'mdi-weather-sunny'"
            variant="text" @click="alternarTema" />
          <v-btn icon="mdi-bell-outline" variant="text" />
          <v-avatar color="primary" size="36" class="mr-2" style="cursor:pointer">
            <span class="text-caption text-white">{{ auth.iniciais }}</span>
          </v-avatar>
        </template>
      </v-app-bar>

      <v-main>
        <v-container fluid class="pa-4">
          <router-view v-slot="{ Component }">
            <transition name="fade" mode="out-in">
              <component :is="Component" />
            </transition>
          </router-view>
        </v-container>
      </v-main>
    </template>

    <!-- Login -->
    <router-view v-else />

    <!-- Snackbar global -->
    <v-snackbar v-model="notif.visivel" :color="notif.cor" :timeout="3500"
      location="bottom right">
      {{ notif.mensagem }}
      <template #actions>
        <v-btn variant="text" @click="notif.fechar()">Fechar</v-btn>
      </template>
    </v-snackbar>
  </v-app>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const route = useRoute()

const drawer = ref(true)
const rail = ref(false)
const tema = ref<'ecoGranelLight' | 'ecoGranelDark'>('ecoGranelLight')

const tituloPagina = computed(() => (route.meta.titulo as string) ?? 'EcoGranel')

function alternarTema() {
  tema.value = tema.value === 'ecoGranelLight' ? 'ecoGranelDark' : 'ecoGranelLight'
}
</script>

<style>
.fade-enter-active, .fade-leave-active { transition: opacity .15s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
