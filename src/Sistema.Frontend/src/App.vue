<template>
  <v-app :theme="tema">
    <template v-if="auth.logado">
      <!-- Drawer lateral: permanente no desktop, temporário (overlay) no celular -->
      <v-navigation-drawer v-model="drawer" :rail="!mobile && rail"
        :permanent="!mobile" :temporary="mobile">
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
          <v-list-item v-if="!ehAtendente && !ehContador" prepend-icon="mdi-view-dashboard-outline" title="Dashboard"
            to="/" value="/" color="primary" rounded="lg" />

          <!-- 2. PDV / Vendas -->
          <v-list-group v-if="!ehContador" value="pdv">
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
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-keyboard-return" title="Devoluções"
              to="/pdv/devolucoes" value="/pdv/devolucoes" color="warning" rounded="lg" class="pl-4" />
          </v-list-group>

          <!-- 3. Cadastros (inclui Produtos) -->
          <v-list-group v-if="!ehContador" value="cadastros">
            <template #activator="{ props }">
              <v-list-item v-bind="props" prepend-icon="mdi-account-group-outline"
                title="Cadastros" color="primary" rounded="lg" />
            </template>
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-tag-multiple-outline" title="Categorias"
              to="/cadastros/categorias" value="/cadastros/categorias" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-account-group-outline" title="Clientes"
              to="/cadastros/clientes" value="/cadastros/clientes" color="primary" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-account-tie-outline" title="Colaboradores"
              to="/cadastros/colaboradores" value="/cadastros/colaboradores" color="primary" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-truck-delivery-outline" title="Fornecedores"
              to="/cadastros/fornecedores" value="/cadastros/fornecedores" color="primary" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-warehouse" title="Locais de Estoque"
              to="/cadastros/locais-estoque" value="/cadastros/locais-estoque" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-package-variant-closed" title="Produtos"
              to="/estoque/produtos" value="/estoque/produtos" color="primary" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-ruler-square" title="Unidades de Medida"
              to="/cadastros/unidades-medida" value="/cadastros/unidades-medida" color="primary" rounded="lg" class="pl-4" />
          </v-list-group>

          <!-- 4. Financeiro -->
          <v-list-group v-if="!ehAtendente && !ehContador" value="financeiro">
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
          <v-list-item v-if="!ehAtendente && !ehContador" prepend-icon="mdi-chart-bar" title="Relatórios"
            to="/relatorios" value="/relatorios" color="primary" rounded="lg" />

          <!-- Restante em ordem alfabética -->
          <v-list-item v-if="!ehAtendente" prepend-icon="mdi-book-open-outline" title="Contabilidade"
            to="/contabilidade" value="/contabilidade" color="primary" rounded="lg" />

          <v-list-item v-if="!ehAtendente && !ehContador" prepend-icon="mdi-truck-delivery-outline" title="Compras"
            to="/compras" value="/compras" color="primary" rounded="lg" />

          <v-list-item v-if="!ehAtendente && !ehContador" prepend-icon="mdi-account-multiple-outline" title="Crediário"
            to="/crediario" value="/crediario" color="primary" rounded="lg" />

          <!-- Estoque (sem Produtos) -->
          <v-list-group v-if="!ehContador" value="estoque">
            <template #activator="{ props }">
              <v-list-item v-bind="props" prepend-icon="mdi-package-variant-closed"
                title="Estoque" color="primary" rounded="lg" />
            </template>
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-view-dashboard" title="Painel Estoque"
              to="/estoque" value="/estoque" color="primary" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-swap-horizontal" title="Movimentações"
              to="/estoque/movimentacoes" value="/estoque/movimentacoes" color="primary" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-list-status" title="Posição de Estoque"
              to="/estoque/posicao" value="/estoque/posicao" color="primary" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-transfer" title="Transferências"
              to="/estoque/transferencias" value="/estoque/transferencias" color="primary" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-tune-vertical" title="Ajuste de Estoque"
              to="/estoque/ajuste" value="/estoque/ajuste" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-tag-outline" title="Etiquetas"
              to="/estoque/etiquetas" value="/estoque/etiquetas" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-calendar-alert" title="Controle de Validade"
              to="/estoque/validade" value="/estoque/validade" color="error" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-scale" title="Exportar para Balança"
              to="/estoque/balanca" value="/estoque/balanca" color="teal" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-package-variant-closed" title="Materiais de Consumo"
              to="/estoque/materiais" value="/estoque/materiais" color="teal" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-desktop-classic" title="Ativo Imobilizado"
              to="/estoque/ativos" value="/estoque/ativos" color="indigo" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-currency-usd" title="Alterar Preços"
              to="/estoque/alterar-precos" value="/estoque/alterar-precos" color="primary" rounded="lg" class="pl-4" />
          </v-list-group>

          <v-list-item v-if="!ehAtendente && !ehContador" prepend-icon="mdi-file-document-outline" title="Fiscal / NF-e"
            to="/fiscal" value="/fiscal" color="primary" rounded="lg" />
          <v-list-item v-if="!ehAtendente && !ehContador" prepend-icon="mdi-truck-outline" title="CT-e recebidos"
            to="/fiscal/cte-recebidos" value="/fiscal/cte-recebidos" color="primary" rounded="lg" />

          <v-list-group v-if="!ehContador" value="marketing">
            <template #activator="{ props }">
              <v-list-item v-bind="props" prepend-icon="mdi-bullhorn-outline"
                title="Marketing" color="primary" rounded="lg" />
            </template>
            <v-list-item prepend-icon="mdi-image-multiple-outline" title="Artes para Redes"
              to="/marketing" value="/marketing" color="primary" rounded="lg" class="pl-4" />
            <v-list-item prepend-icon="mdi-star-circle-outline" title="Clube de Promoções"
              to="/marketing/clube" value="/marketing/clube" color="purple" rounded="lg" class="pl-4" />
            <v-list-item v-if="!ehAtendente" prepend-icon="mdi-tag-multiple-outline" title="Promoções"
              to="/marketing/promocoes" value="/marketing/promocoes" color="error" rounded="lg" class="pl-4" />
          </v-list-group>

          <v-list-item v-if="!ehContador" prepend-icon="mdi-whatsapp" title="WhatsApp"
            to="/whatsapp" value="/whatsapp" color="primary" rounded="lg" />

        </v-list>

        <template #append>
          <v-divider />
          <v-list density="compact" nav class="pb-2">
            <v-list-item v-if="!ehAtendente && !ehContador" prepend-icon="mdi-cog-outline" title="Configurações"
              to="/configuracoes" color="primary" rounded="lg" />
            <v-list-item v-if="!ehAtendente && !ehContador" prepend-icon="mdi-store-outline" title="Filiais / Unidades"
              to="/configuracoes/filiais" color="primary" rounded="lg" />
            <v-list-item prepend-icon="mdi-logout" title="Sair"
              @click="auth.sair()" color="error" rounded="lg" />
          </v-list>
        </template>
      </v-navigation-drawer>

      <!-- Top bar (escondida no PDV mobile, que tem barra própria em tela cheia) -->
      <v-app-bar v-if="!pdvFullscreen" elevation="1" color="surface">
        <template #prepend>
          <v-btn :icon="mobile ? 'mdi-menu' : (rail ? 'mdi-menu' : 'mdi-menu-open')"
            variant="text" @click="mobile ? (drawer = !drawer) : (rail = !rail)" />
        </template>
        <v-app-bar-title>
          <span class="text-body-1 font-weight-medium">{{ tituloPagina }}</span>
        </v-app-bar-title>
        <template #append>
          <!-- Seletor de filial (aparece quando há mais de uma unidade) -->
          <v-menu v-if="auth.temFiliais" offset-y>
            <template #activator="{ props }">
              <v-btn v-bind="props" variant="tonal" color="primary" class="mr-2"
                prepend-icon="mdi-store-outline" append-icon="mdi-chevron-down"
                size="small" style="max-width:240px">
                <span class="text-truncate" style="max-width:160px">
                  {{ auth.empresaAtual?.nomeFantasia ?? 'Selecionar unidade' }}
                </span>
                <v-chip v-if="auth.empresaAtual?.tipoUnidade === 'Filial'"
                  size="x-small" color="warning" variant="tonal" class="ml-1">Filial</v-chip>
              </v-btn>
            </template>
            <v-list min-width="260" density="compact">
              <v-list-subheader>Selecionar unidade</v-list-subheader>
              <v-list-item v-for="f in auth.filiais" :key="f.id"
                :active="f.id === auth.empresaId"
                active-color="primary"
                @click="auth.trocarFilial(f.id)">
                <template #prepend>
                  <v-icon :icon="f.tipoUnidade === 'Matriz' ? 'mdi-home-city-outline' : 'mdi-store-outline'"
                    size="18" class="mr-2" />
                </template>
                <v-list-item-title>{{ f.nomeFantasia }}</v-list-item-title>
                <v-list-item-subtitle class="text-caption">
                  {{ f.tipoUnidade }} · {{ f.cnpj }}
                </v-list-item-subtitle>
                <template #append>
                  <v-icon v-if="f.id === auth.empresaId" icon="mdi-check" color="primary" size="16" />
                </template>
              </v-list-item>
              <v-divider />
              <v-list-item prepend-icon="mdi-plus" title="Cadastrar nova filial"
                to="/configuracoes/filiais" density="compact" color="primary" />
            </v-list>
          </v-menu>

          <!-- Seletor de LOJA/unidade (separa a operação: estoque, etiquetas, vendas) -->
          <v-menu v-if="auth.lojas.length > 1" offset-y>
            <template #activator="{ props }">
              <v-btn v-bind="props" variant="tonal" color="deep-orange" class="mr-2"
                prepend-icon="mdi-storefront-outline" append-icon="mdi-chevron-down"
                size="small" style="max-width:220px">
                <span class="text-truncate" style="max-width:150px">
                  {{ auth.lojaAtual?.nome ?? 'Todas as lojas' }}
                </span>
              </v-btn>
            </template>
            <v-list min-width="240" density="compact">
              <v-list-subheader>Loja / unidade</v-list-subheader>
              <v-list-item :active="!auth.lojaAtualId" active-color="deep-orange"
                @click="auth.setLoja(null)">
                <template #prepend><v-icon icon="mdi-earth" size="18" class="mr-2" /></template>
                <v-list-item-title>Todas as lojas</v-list-item-title>
              </v-list-item>
              <v-list-item v-for="l in auth.lojas" :key="l.id"
                :active="l.id === auth.lojaAtualId" active-color="deep-orange"
                @click="auth.setLoja(l.id)">
                <template #prepend><v-icon icon="mdi-storefront-outline" size="18" class="mr-2" /></template>
                <v-list-item-title>{{ l.nome }}</v-list-item-title>
                <template #append>
                  <v-icon v-if="l.id === auth.lojaAtualId" icon="mdi-check" color="deep-orange" size="16" />
                </template>
              </v-list-item>
            </v-list>
          </v-menu>

          <v-btn :icon="tema === 'clinsoftLight' ? 'mdi-weather-night' : 'mdi-weather-sunny'"
            variant="text" @click="alternarTema" />
          <v-menu offset-y :close-on-content-click="false">
            <template #activator="{ props }">
              <v-btn v-bind="props" variant="text" icon>
                <v-badge :content="totalNotif" :model-value="totalNotif > 0" color="error" max="99">
                  <v-icon>{{ totalNotif > 0 ? 'mdi-bell-ring-outline' : 'mdi-bell-outline' }}</v-icon>
                </v-badge>
              </v-btn>
            </template>
            <v-list min-width="320" max-width="380" density="compact">
              <v-list-subheader>Notificações</v-list-subheader>
              <v-list-item v-if="!notificacoes.length" class="text-medium-emphasis">
                <v-list-item-title class="text-body-2">Tudo em dia 🎉</v-list-item-title>
                <v-list-item-subtitle class="text-caption">Nenhum alerta no momento.</v-list-item-subtitle>
              </v-list-item>
              <v-list-item v-for="n in notificacoes" :key="n.tipo" @click="abrirNotificacao(n)">
                <template #prepend>
                  <v-icon :icon="n.icone" :color="n.cor" class="mr-2" />
                </template>
                <v-list-item-title class="text-body-2 font-weight-medium">
                  {{ n.titulo }}
                  <v-chip size="x-small" :color="n.cor" variant="tonal" class="ml-1">{{ n.quantidade }}</v-chip>
                </v-list-item-title>
                <v-list-item-subtitle class="text-caption">{{ n.texto }}</v-list-item-subtitle>
              </v-list-item>
              <v-divider v-if="notificacoes.length" />
              <v-list-item v-if="notificacoes.length" class="text-caption text-center text-medium-emphasis"
                @click="carregarNotificacoes()">
                <v-list-item-title class="text-caption">Atualizar</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu>
          <v-avatar color="primary" size="36" class="mr-2" style="cursor:pointer">
            <span class="text-caption text-white">{{ auth.iniciais }}</span>
          </v-avatar>
        </template>
      </v-app-bar>

      <v-main>
        <v-container fluid :class="mobile ? 'pa-0' : 'pa-4'">
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
    <v-snackbar v-model="notif.visivel" :color="notif.cor"
      :timeout="notif.cor === 'error' ? -1 : 3500"
      location="bottom right" multi-line>
      <span style="white-space:pre-wrap">{{ notif.mensagem }}</span>
      <template #actions>
        <v-btn variant="text" @click="notif.fechar()">Fechar</v-btn>
      </template>
    </v-snackbar>
  </v-app>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '@/composables/useApi'
import { useDisplay } from 'vuetify'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'
import { useUiStore } from '@/stores/ui'
import { storeToRefs } from 'pinia'

const auth = useAuthStore()
const notif = useNotifStore()
const route = useRoute()
const router = useRouter()
const { mobile } = useDisplay()
const ui = useUiStore()
const { drawer } = storeToRefs(ui)

// No celular o menu começa fechado (overlay); no desktop começa aberto.
drawer.value = !mobile.value
const rail = ref(false)
const tema = ref<'ecoGranelLight' | 'ecoGranelDark'>('ecoGranelLight')

const tituloPagina = computed(() => (route.meta.titulo as string) ?? 'EcoGranel')

// Perfil "Atendente" só enxerga um conjunto reduzido de telas no menu.
const ehAtendente = computed(() => auth.usuario?.role === 'Atendente')
const ehContador = computed(() => auth.usuario?.role === 'Contador')

// ── Sininho de notificações ──────────────────────────────────────
const notificacoes = ref<any[]>([])
const totalNotif = computed(() => notificacoes.value.reduce((s, n) => s + (n.quantidade ?? 0), 0))
async function carregarNotificacoes() {
  if (!auth.logado || !auth.empresaId) return
  try {
    const { data } = await api.get('/notificacoes', { params: { empresaId: auth.empresaId } })
    notificacoes.value = data.itens ?? []
  } catch { notificacoes.value = [] }
}
function abrirNotificacao(n: any) { if (n?.rota) router.push(n.rota) }
onMounted(() => {
  carregarNotificacoes()
  setInterval(carregarNotificacoes, 5 * 60 * 1000)  // atualiza a cada 5 min
  // Mantém a lista de lojas sempre fresca (reflete lojas ativadas/inativadas
  // sem precisar relogar). Sessões antigas também passam a ter o seletor.
  if (auth.logado) auth.carregarLojas()
})
watch(() => route.path, () => { if (route.path === '/estoque/produtos' || route.path === '/financeiro/contas-pagar') carregarNotificacoes() })
// No celular, o PDV ocupa a tela inteira (esconde a app-bar branca redundante).
const pdvFullscreen = computed(() => mobile.value && route.path === '/pdv')

// Ao navegar no celular, fecha o menu overlay; ao alternar mobile/desktop, ajusta.
watch(() => route.path, () => { if (mobile.value) drawer.value = false })
watch(mobile, m => { drawer.value = !m })

function alternarTema() {
  tema.value = tema.value === 'ecoGranelLight' ? 'ecoGranelDark' : 'ecoGranelLight'
}
</script>

<style>
.fade-enter-active, .fade-leave-active { transition: opacity .15s ease; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
