<template>
  <v-app theme="ecoGranelLight">
    <!-- Barra superior -->
    <v-app-bar :elevation="scrolled ? 3 : 0" color="primary" height="64" class="vitrine-appbar">
      <div class="d-flex align-center px-3" style="width: 100%; max-width: 1200px; margin: 0 auto;">
        <img src="/logo-ecogranel.png" alt="logo" class="vitrine-logo" />
        <div class="ml-3">
          <div class="text-h6 font-weight-bold" style="line-height: 1.1">{{ nomeLoja || 'Loja Online' }}</div>
          <div class="text-caption" style="opacity: .8; line-height: 1">Produtos naturais a granel</div>
        </div>
        <v-spacer />
        <v-btn icon size="large" @click="carrinhoAberto = true">
          <v-badge :content="qtdItens" :model-value="qtdItens > 0" color="accent">
            <v-icon size="26">mdi-basket</v-icon>
          </v-badge>
        </v-btn>
      </div>
    </v-app-bar>

    <v-main>
      <!-- HERO -->
      <section class="hero">
        <div class="hero-inner">
          <h1 class="hero-titulo">Sua feira natural,<br class="d-sm-none" /> do seu jeito 🌿</h1>
          <p class="hero-sub">Grãos, farinhas, temperos e suplementos a granel — escolha a quantidade e receba em casa ou retire na loja.</p>
          <v-text-field v-model="busca" placeholder="Buscar produto (ex.: aveia, castanha, chia...)"
            variant="solo" flat rounded="pill" density="comfortable" hide-details clearable
            prepend-inner-icon="mdi-magnify" class="hero-busca" bg-color="surface" />
          <div v-if="lojas.length" class="hero-lojas">
            <v-icon size="16" class="mr-1">mdi-map-marker</v-icon>
            <span>{{ lojas.map(l => l.nome).join(' · ') }}</span>
          </div>
        </div>
      </section>

      <v-container style="max-width: 1200px" class="pb-16">
        <!-- Categorias (fixa ao rolar) -->
        <div class="cat-bar">
          <div class="d-flex flex-wrap py-2 gap-2">
            <button class="cat-chip" :class="{ ativo: categoria === null }" @click="categoria = null">
              <v-icon size="18">mdi-view-grid</v-icon> Todos
            </button>
            <button v-for="c in categorias" :key="c" class="cat-chip"
              :class="{ ativo: categoria === c }" @click="categoria = c">
              {{ c }}
            </button>
          </div>
        </div>

        <!-- Carregando -->
        <div v-if="carregando" class="py-16 text-center">
          <v-progress-circular indeterminate color="accent" size="56" />
          <div class="text-medium-emphasis mt-3">Montando a vitrine...</div>
        </div>

        <template v-else>
          <div class="d-flex align-center mb-3 mt-1">
            <div class="text-body-2 text-medium-emphasis">
              {{ produtosFiltrados.length }} produto(s)
              <span v-if="categoria"> em <b>{{ categoria }}</b></span>
            </div>
          </div>

          <!-- Grade -->
          <v-row dense>
            <v-col v-for="p in produtosFiltrados" :key="p.id" cols="6" sm="4" md="3" lg="3">
              <div class="produto-card">
                <div class="produto-img">
                  <v-img v-if="p.imagemUrl" :src="p.imagemUrl" aspect-ratio="1" cover />
                  <div v-else class="sem-img">
                    <v-icon size="40" color="accent">mdi-leaf</v-icon>
                  </div>
                  <span v-if="p.porPeso" class="tag-granel">a granel</span>
                </div>
                <div class="produto-corpo">
                  <div class="produto-nome">{{ p.descricao }}</div>
                  <div class="produto-preco">
                    R$ {{ fmt(p.porPeso ? p.precoVenda / 10 : p.precoVenda) }}
                    <span class="produto-unidade">{{ p.porPeso ? '/100g' : '/un' }}</span>
                  </div>

                  <!-- Stepper quando já no carrinho, senão botão Adicionar -->
                  <div v-if="noCarrinho(p.id)" class="stepper-card">
                    <v-btn icon="mdi-minus" size="small" variant="flat" color="primary"
                      @click="menos(noCarrinho(p.id)!)" />
                    <span class="stepper-qtd">{{ rotuloQtd(noCarrinho(p.id)!) }}</span>
                    <v-btn icon="mdi-plus" size="small" variant="flat" color="primary"
                      @click="mais(noCarrinho(p.id)!)" />
                  </div>
                  <v-btn v-else color="accent" variant="flat" rounded="pill" block size="small"
                    class="mt-1 text-none font-weight-bold" @click="adicionar(p)">
                    <v-icon start size="18">mdi-plus</v-icon>Adicionar
                  </v-btn>
                </div>
              </div>
            </v-col>
          </v-row>

          <div v-if="!produtosFiltrados.length" class="text-center py-12 text-medium-emphasis">
            <v-icon icon="mdi-magnify-close" size="56" color="grey-lighten-1" class="mb-2" />
            <div class="text-body-1">Nada encontrado para sua busca.</div>
            <v-btn variant="text" color="primary" class="mt-2" @click="busca = ''; categoria = null">
              Limpar filtros
            </v-btn>
          </div>
        </template>
      </v-container>

      <!-- Rodapé -->
      <footer class="vitrine-footer">
        <img src="/logo-ecogranel.png" alt="logo" class="footer-logo" />
        <div class="text-body-2 font-weight-bold">{{ nomeLoja }}</div>
        <div class="text-caption" style="opacity:.7">Produtos naturais a granel · pagamento na retirada ou entrega</div>
      </footer>
    </v-main>

    <!-- Botão flutuante do carrinho -->
    <transition name="fade-up">
      <button v-if="qtdItens > 0 && !carrinhoAberto" class="fab-carrinho" @click="carrinhoAberto = true">
        <v-icon>mdi-basket</v-icon>
        <span class="fab-qtd">{{ qtdItens }}</span>
        <span>Ver pedido</span>
        <span class="fab-total">R$ {{ fmt(total) }}</span>
      </button>
    </transition>

    <!-- Carrinho -->
    <v-navigation-drawer v-model="carrinhoAberto" location="right" temporary
      :width="drawerWidth" class="carrinho-drawer">
      <div class="d-flex align-center pa-4 pb-3">
        <v-icon icon="mdi-basket" color="primary" class="mr-2" />
        <span class="text-h6 font-weight-bold">Seu pedido</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" @click="carrinhoAberto = false" />
      </div>
      <v-divider />

      <div v-if="!carrinho.length" class="text-center py-16 px-6 text-medium-emphasis">
        <v-icon icon="mdi-basket-outline" size="64" color="grey-lighten-1" class="mb-3" />
        <div class="text-body-1">Seu carrinho está vazio</div>
        <v-btn variant="tonal" color="primary" class="mt-4" @click="carrinhoAberto = false">
          Ver produtos
        </v-btn>
      </div>

      <div v-else class="carrinho-itens">
        <div v-for="it in carrinho" :key="it.produtoId" class="carrinho-item">
          <v-avatar rounded="lg" size="52" color="grey-lighten-4">
            <v-img v-if="it.imagemUrl" :src="it.imagemUrl" cover />
            <v-icon v-else icon="mdi-leaf" color="accent" />
          </v-avatar>
          <div class="flex-grow-1 mx-3" style="min-width:0">
            <div class="text-body-2 font-weight-medium text-truncate">{{ it.descricao }}</div>
            <div class="text-caption text-medium-emphasis">
              R$ {{ fmt(it.porPeso ? it.precoVenda / 10 : it.precoVenda) }}{{ it.porPeso ? '/100g' : '/un' }}
            </div>
            <div class="text-body-2 font-weight-bold text-primary">R$ {{ fmt(subtotal(it)) }}</div>
          </div>
          <div class="stepper-mini">
            <v-btn icon="mdi-minus" size="x-small" variant="tonal" color="primary" @click="menos(it)" />
            <span class="stepper-qtd-mini">{{ rotuloQtd(it) }}</span>
            <v-btn icon="mdi-plus" size="x-small" variant="tonal" color="primary" @click="mais(it)" />
          </div>
        </div>
      </div>

      <template #append>
        <div v-if="carrinho.length" class="carrinho-rodape">
          <div class="d-flex justify-space-between align-center mb-3">
            <span class="text-body-1 text-medium-emphasis">Total</span>
            <span class="text-h5 font-weight-bold text-primary">R$ {{ fmt(total) }}</span>
          </div>
          <v-btn color="accent" size="large" block rounded="pill"
            class="text-none font-weight-bold" @click="abrirCheckout">
            Finalizar pedido<v-icon end>mdi-arrow-right</v-icon>
          </v-btn>
        </div>
      </template>
    </v-navigation-drawer>

    <!-- Checkout -->
    <v-dialog v-model="checkoutAberto" :max-width="520" :fullscreen="$vuetify.display.xs" scrollable>
      <v-card :rounded="$vuetify.display.xs ? 0 : 'xl'">
        <v-toolbar color="primary" density="comfortable">
          <v-btn icon="mdi-arrow-left" @click="checkoutAberto = false" />
          <v-toolbar-title class="text-body-1 font-weight-bold">Finalizar pedido</v-toolbar-title>
        </v-toolbar>
        <v-card-text class="pa-4">
          <div class="text-overline text-medium-emphasis mb-1">Seus dados</div>
          <v-text-field v-model="ped.nome" label="Seu nome *" variant="outlined"
            density="comfortable" prepend-inner-icon="mdi-account" class="mb-2" hide-details />
          <v-text-field v-model="ped.telefone" label="WhatsApp / Telefone *" variant="outlined"
            density="comfortable" type="tel" prepend-inner-icon="mdi-whatsapp"
            placeholder="(00) 00000-0000" class="mb-4" hide-details />

          <div class="text-overline text-medium-emphasis mb-1">Entrega</div>
          <v-select v-if="lojas.length > 1" v-model="ped.lojaId" :items="lojas"
            item-title="nome" item-value="id" label="Loja *" variant="outlined"
            density="comfortable" prepend-inner-icon="mdi-storefront" class="mb-2" hide-details />

          <div class="entrega-toggle mb-3">
            <button class="entrega-op" :class="{ ativo: ped.tipoEntrega === 'Retirada' }"
              @click="ped.tipoEntrega = 'Retirada'">
              <v-icon>mdi-storefront-outline</v-icon><span>Retirar na loja</span>
            </button>
            <button class="entrega-op" :class="{ ativo: ped.tipoEntrega === 'Entrega' }"
              @click="ped.tipoEntrega = 'Entrega'">
              <v-icon>mdi-moped-outline</v-icon><span>Entrega</span>
            </button>
          </div>

          <v-textarea v-if="ped.tipoEntrega === 'Entrega'" v-model="ped.endereco"
            label="Endereço de entrega *" variant="outlined" rows="2" auto-grow
            density="comfortable" prepend-inner-icon="mdi-map-marker" class="mb-2" hide-details />
          <v-textarea v-model="ped.observacao" label="Observação (opcional)"
            variant="outlined" rows="2" auto-grow density="comfortable"
            prepend-inner-icon="mdi-note-text-outline" hide-details />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <div>
            <div class="text-caption text-medium-emphasis">{{ qtdItens }} item(ns)</div>
            <div class="text-h6 font-weight-bold text-primary">R$ {{ fmt(total) }}</div>
          </div>
          <v-spacer />
          <v-btn color="accent" variant="flat" size="large" rounded="pill"
            class="text-none font-weight-bold px-6" :loading="enviando" @click="enviarPedido">
            Enviar pedido
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Sucesso -->
    <v-dialog v-model="sucessoAberto" max-width="440" persistent>
      <v-card rounded="xl" class="text-center pa-6">
        <div class="sucesso-check"><v-icon icon="mdi-check" size="44" color="white" /></div>
        <div class="text-h5 font-weight-bold mt-4 mb-1">Pedido enviado! 🎉</div>
        <div class="text-body-2 text-medium-emphasis mb-1">
          Pedido <b>{{ pedidoFeito.numero }}</b> · <b>R$ {{ fmt(pedidoFeito.total) }}</b>
        </div>
        <div class="text-body-2 mb-5">
          A loja vai confirmar e combinar o pagamento com você. Obrigado! 🌿
        </div>
        <v-btn v-if="whatsappLoja" color="success" size="large" block rounded="pill"
          class="mb-2 text-none font-weight-bold" @click="abrirWhatsApp">
          <v-icon start>mdi-whatsapp</v-icon>Confirmar no WhatsApp
        </v-btn>
        <v-btn variant="text" block class="text-none" @click="novoPedido">Fazer novo pedido</v-btn>
      </v-card>
    </v-dialog>
  </v-app>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useDisplay } from 'vuetify'
import api from '@/composables/useApi'

interface Produto {
  id: string; descricao: string; precoVenda: number
  imagemUrl?: string | null; categoria?: string | null; porPeso: boolean
}
interface ItemCarrinho extends Produto { produtoId: string; quantidade: number }

const route = useRoute()
const display = useDisplay()
const empresaId = String(route.params.empresaId)

const carregando = ref(true)
const nomeLoja = ref('')
const lojas = ref<{ id: string; nome: string; whatsapp?: string | null }[]>([])
const categorias = ref<string[]>([])
const produtos = ref<Produto[]>([])

const busca = ref('')
const categoria = ref<string | null>(null)
const carrinho = ref<ItemCarrinho[]>([])
const carrinhoAberto = ref(false)
const checkoutAberto = ref(false)
const sucessoAberto = ref(false)
const enviando = ref(false)
const scrolled = ref(false)

const ped = ref({ nome: '', telefone: '', lojaId: '', tipoEntrega: 'Retirada', endereco: '', observacao: '' })
const pedidoFeito = ref<{ numero: string; total: number }>({ numero: '', total: 0 })
const whatsappLoja = ref('')

const drawerWidth = computed(() => (display.xs.value ? 340 : 400))
const fmt = (v: number) => (v ?? 0).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const chaveCarrinho = `vitrine_cart_${empresaId}`

function normalizar(s: string) {
  return s.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase()
}

const produtosFiltrados = computed(() => {
  const q = normalizar(busca.value.trim())
  return produtos.value.filter(p => {
    if (categoria.value && p.categoria !== categoria.value) return false
    if (q && !normalizar(p.descricao).includes(q)) return false
    return true
  })
})

const qtdItens = computed(() => carrinho.value.length)
function subtotal(it: ItemCarrinho) { return it.precoVenda * it.quantidade }
const total = computed(() => carrinho.value.reduce((s, it) => s + subtotal(it), 0))
function noCarrinho(id: string) { return carrinho.value.find(it => it.produtoId === id) }
function rotuloQtd(it: ItemCarrinho) {
  return it.porPeso ? Math.round(it.quantidade * 1000) + 'g' : it.quantidade + 'un'
}

function passo(it: { porPeso: boolean }) { return it.porPeso ? 0.1 : 1 }
function adicionar(p: Produto) {
  const existe = noCarrinho(p.id)
  if (existe) mais(existe)
  else carrinho.value.push({ ...p, produtoId: p.id, quantidade: passo(p) })
}
function mais(it: ItemCarrinho) { it.quantidade = +(it.quantidade + passo(it)).toFixed(3) }
function menos(it: ItemCarrinho) {
  it.quantidade = +(it.quantidade - passo(it)).toFixed(3)
  if (it.quantidade <= 0) carrinho.value = carrinho.value.filter(x => x.produtoId !== it.produtoId)
}

watch(carrinho, () => localStorage.setItem(chaveCarrinho, JSON.stringify(carrinho.value)), { deep: true })

function abrirCheckout() {
  if (!ped.value.lojaId && lojas.value.length) ped.value.lojaId = lojas.value[0].id
  checkoutAberto.value = true
}

function onScroll() { scrolled.value = window.scrollY > 8 }

async function carregar() {
  carregando.value = true
  try {
    const [cfg, prods] = await Promise.all([
      api.get(`/publico/vitrine/${empresaId}`),
      api.get(`/publico/vitrine/${empresaId}/produtos`),
    ])
    nomeLoja.value = cfg.data.empresa ?? ''
    lojas.value = cfg.data.lojas ?? []
    categorias.value = cfg.data.categorias ?? []
    produtos.value = prods.data ?? []
    if (lojas.value.length) ped.value.lojaId = lojas.value[0].id
    document.title = `${nomeLoja.value} — Loja Online`

    const salvo = localStorage.getItem(chaveCarrinho)
    if (salvo) {
      try {
        const arr = JSON.parse(salvo) as ItemCarrinho[]
        carrinho.value = arr.map(it => {
          const p = produtos.value.find(x => x.id === it.produtoId)
          return p ? { ...p, produtoId: p.id, quantidade: it.quantidade } : null
        }).filter(Boolean) as ItemCarrinho[]
      } catch { /* ignora carrinho corrompido */ }
    }
  } finally {
    carregando.value = false
  }
}

async function enviarPedido() {
  if (!ped.value.nome.trim() || !ped.value.telefone.trim()) {
    alert('Preencha nome e telefone.'); return
  }
  if (ped.value.tipoEntrega === 'Entrega' && !ped.value.endereco.trim()) {
    alert('Informe o endereço de entrega.'); return
  }
  enviando.value = true
  try {
    const { data } = await api.post(`/publico/vitrine/${empresaId}/pedido`, {
      nomeCliente: ped.value.nome,
      telefone: ped.value.telefone,
      localEstoqueId: ped.value.lojaId || null,
      tipoEntrega: ped.value.tipoEntrega,
      enderecoEntrega: ped.value.endereco || null,
      observacao: ped.value.observacao || null,
      itens: carrinho.value.map(it => ({ produtoId: it.produtoId, quantidade: it.quantidade })),
    }, { _quiet: true } as any)

    pedidoFeito.value = { numero: data.numero, total: data.total }
    whatsappLoja.value = lojas.value.find(l => l.id === ped.value.lojaId)?.whatsapp ?? ''
    carrinho.value = []
    localStorage.removeItem(chaveCarrinho)
    checkoutAberto.value = false
    carrinhoAberto.value = false
    sucessoAberto.value = true
  } catch (e: any) {
    alert(e?.response?.data?.mensagem ?? 'Não foi possível enviar o pedido. Tente novamente.')
  } finally {
    enviando.value = false
  }
}

function abrirWhatsApp() {
  const linhas = [`Olá! Fiz o pedido *${pedidoFeito.value.numero}* pela loja online.`,
    `Nome: ${ped.value.nome}`,
    ped.value.tipoEntrega === 'Entrega' ? `Entrega: ${ped.value.endereco}` : 'Retirada na loja',
    `Total: R$ ${fmt(pedidoFeito.value.total)}`]
  const texto = encodeURIComponent(linhas.join('\n'))
  window.open(`https://wa.me/${whatsappLoja.value.replace(/\D/g, '')}?text=${texto}`, '_blank')
}

function novoPedido() {
  sucessoAberto.value = false
  ped.value = { nome: '', telefone: '', lojaId: lojas.value[0]?.id ?? '', tipoEntrega: 'Retirada', endereco: '', observacao: '' }
}

onMounted(() => { carregar(); window.addEventListener('scroll', onScroll, { passive: true }) })
onUnmounted(() => window.removeEventListener('scroll', onScroll))
</script>

<style scoped>
.vitrine-appbar { transition: box-shadow .2s; }
.vitrine-logo { height: 40px; width: 40px; object-fit: contain; background: #fff; border-radius: 10px; padding: 3px; }

/* HERO */
.hero {
  background:
    linear-gradient(135deg, rgba(40,20,7,.86) 0%, rgba(92,45,12,.74) 45%, rgba(61,122,30,.72) 135%),
    url('/hero-vitrine.jpg') center 40% / cover no-repeat;
  color: #fff;
  padding: 52px 16px 64px;
  position: relative;
}
.hero-inner { max-width: 720px; margin: 0 auto; text-align: center; }
.hero-titulo { font-size: clamp(1.7rem, 5vw, 2.6rem); font-weight: 800; line-height: 1.12; letter-spacing: -.5px; text-shadow: 0 2px 12px rgba(0,0,0,.45); }
.hero-sub { opacity: .95; margin: 12px auto 22px; max-width: 560px; font-size: .98rem; text-shadow: 0 1px 6px rgba(0,0,0,.4); }
.hero-busca { max-width: 560px; margin: 0 auto; box-shadow: 0 10px 30px rgba(0,0,0,.18); border-radius: 999px; }
.hero-lojas { margin-top: 16px; font-size: .85rem; opacity: .9; display: flex; align-items: center; justify-content: center; }

/* CATEGORIAS */
.cat-bar {
  position: sticky; top: 64px; z-index: 5;
  background: rgb(var(--v-theme-background)); margin: 0 -12px 6px; padding: 0 12px;
  border-bottom: 1px solid rgba(0,0,0,.06);
}
.gap-2 { gap: 8px; }
.cat-chip {
  flex: 0 0 auto; display: inline-flex; align-items: center; gap: 4px;
  padding: 7px 16px; border-radius: 999px; font-size: .85rem; font-weight: 600;
  background: #fff; border: 1.5px solid rgba(0,0,0,.1); color: #5C2D0C;
  cursor: pointer; transition: all .15s; white-space: nowrap;
}
.cat-chip:hover { border-color: #6AAF2E; }
.cat-chip.ativo { background: #5C2D0C; border-color: #5C2D0C; color: #fff; }

/* CARDS */
.produto-card {
  height: 100%; display: flex; flex-direction: column; background: #fff;
  border: 1px solid rgba(0,0,0,.07); border-radius: 16px; overflow: hidden;
  transition: transform .18s, box-shadow .18s;
}
.produto-card:hover { transform: translateY(-4px); box-shadow: 0 12px 26px rgba(92,45,12,.14); }
.produto-img { position: relative; aspect-ratio: 1; background: #f3f1ec; }
.sem-img { height: 100%; display: flex; align-items: center; justify-content: center;
  background: linear-gradient(135deg, #eef3e6, #f3f1ec); }
.tag-granel {
  position: absolute; top: 8px; left: 8px; background: rgba(61,122,30,.95); color: #fff;
  font-size: .68rem; font-weight: 700; padding: 3px 8px; border-radius: 999px; letter-spacing: .3px;
}
.produto-corpo { padding: 10px 10px 12px; display: flex; flex-direction: column; flex-grow: 1; }
.produto-nome {
  font-size: .86rem; font-weight: 600; line-height: 1.25; color: #2c1a0e; margin-bottom: 6px;
  display: -webkit-box; -webkit-line-clamp: 2; line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden;
  min-height: 2.2em;
}
.produto-preco { margin-top: auto; font-size: 1.15rem; font-weight: 800; color: #3D7A1E; }
.produto-unidade { font-size: .72rem; font-weight: 600; color: #8a8a8a; }
.stepper-card {
  display: flex; align-items: center; justify-content: space-between; margin-top: 6px;
  background: #f5f2ec; border-radius: 999px; padding: 3px;
}
.stepper-qtd { font-weight: 700; font-size: .9rem; color: #5C2D0C; }

/* FAB */
.fab-carrinho {
  position: fixed; left: 50%; bottom: 18px; transform: translateX(-50%); z-index: 1000;
  display: flex; align-items: center; gap: 10px; padding: 12px 20px;
  background: #3D7A1E; color: #fff; border-radius: 999px; font-weight: 700; font-size: .95rem;
  box-shadow: 0 8px 24px rgba(61,122,30,.45); cursor: pointer; border: none;
}
.fab-qtd { background: rgba(255,255,255,.25); border-radius: 999px; padding: 1px 9px; font-size: .85rem; }
.fab-total { border-left: 1px solid rgba(255,255,255,.35); padding-left: 10px; }
.fade-up-enter-active, .fade-up-leave-active { transition: all .25s; }
.fade-up-enter-from, .fade-up-leave-to { opacity: 0; transform: translate(-50%, 20px); }

/* CARRINHO */
.carrinho-itens { padding: 4px 0; }
.carrinho-item { display: flex; align-items: center; padding: 12px 16px; border-bottom: 1px solid rgba(0,0,0,.05); }
.stepper-mini { display: flex; flex-direction: column; align-items: center; gap: 4px; }
.stepper-qtd-mini { font-size: .78rem; font-weight: 700; color: #5C2D0C; min-width: 44px; text-align: center; }
.carrinho-rodape { padding: 16px; border-top: 1px solid rgba(0,0,0,.08); background: #fff; }

/* ENTREGA toggle */
.entrega-toggle { display: grid; grid-template-columns: 1fr 1fr; gap: 10px; }
.entrega-op {
  display: flex; flex-direction: column; align-items: center; gap: 4px; padding: 14px 8px;
  border: 1.5px solid rgba(0,0,0,.12); border-radius: 14px; background: #fff; cursor: pointer;
  font-size: .85rem; font-weight: 600; color: #5C2D0C; transition: all .15s;
}
.entrega-op.ativo { border-color: #3D7A1E; background: #eef5e7; color: #3D7A1E; }

/* SUCESSO */
.sucesso-check {
  width: 84px; height: 84px; border-radius: 50%; background: #3D7A1E; margin: 0 auto;
  display: flex; align-items: center; justify-content: center; box-shadow: 0 8px 22px rgba(61,122,30,.4);
}

/* RODAPÉ */
.vitrine-footer { text-align: center; padding: 32px 16px 48px; background: rgba(92,45,12,.04); }
.footer-logo { height: 46px; margin-bottom: 8px; opacity: .9; }

.overflow-x-auto { overflow-x: auto; scrollbar-width: none; }
.overflow-x-auto::-webkit-scrollbar { display: none; }
</style>
