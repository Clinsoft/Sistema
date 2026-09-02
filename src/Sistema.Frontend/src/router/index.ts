import { createRouter, createWebHashHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/setup', component: () => import('@/modules/auth/SetupView.vue'),
      meta: { publica: true } },

    { path: '/login', component: () => import('@/modules/auth/LoginView.vue'),
      meta: { publica: true } },

    { path: '/', component: () => import('@/modules/dashboard/DashboardView.vue'),
      meta: { titulo: 'Dashboard' } },

    // PDV
    { path: '/pdv', component: () => import('@/modules/pdv/PDVView.vue'),
      meta: { titulo: 'PDV — Ponto de Venda' } },
    { path: '/pdv/vendas', component: () => import('@/modules/pdv/VendasView.vue'),
      meta: { titulo: 'Histórico de Vendas' } },
    { path: '/pdv/sessoes', component: () => import('@/modules/pdv/SessoesView.vue'),
      meta: { titulo: 'Sessões de Caixa' } },
    { path: '/pdv/devolucoes', component: () => import('@/modules/pdv/DevolucoesView.vue'),
      meta: { titulo: 'Devoluções de Venda' } },

    // Estoque
    { path: '/estoque', component: () => import('@/modules/estoque/EstoqueView.vue'),
      meta: { titulo: 'Estoque' } },
    { path: '/estoque/produtos', component: () => import('@/modules/estoque/ProdutosView.vue'),
      meta: { titulo: 'Produtos' } },
    { path: '/estoque/movimentacoes', component: () => import('@/modules/estoque/MovimentacoesView.vue'),
      meta: { titulo: 'Movimentações de Estoque' } },
    { path: '/estoque/posicao', component: () => import('@/modules/estoque/PosicaoEstoqueView.vue'),
      meta: { titulo: 'Posição de Estoque' } },
    { path: '/estoque/curva-abc', component: () => import('@/modules/estoque/CurvaAbcProdutosView.vue'),
      meta: { titulo: 'Curva ABC de Produtos' } },
    { path: '/estoque/negativos', component: () => import('@/modules/estoque/EstoqueNegativoView.vue'),
      meta: { titulo: 'Estoque Negativo' } },
    { path: '/estoque/sugestao-compra', component: () => import('@/modules/estoque/SugestaoCompraView.vue'),
      meta: { titulo: 'Sugestão de Compra' } },
    { path: '/estoque/produtos-parados', component: () => import('@/modules/estoque/ProdutosParadosView.vue'),
      meta: { titulo: 'Produtos Parados' } },
    { path: '/estoque/lotes', component: () => import('@/modules/estoque/LotesView.vue'),
      meta: { titulo: 'Lotes e Validades' } },
    { path: '/estoque/transferencias', component: () => import('@/modules/estoque/TransferenciasView.vue'),
      meta: { titulo: 'Transferências de Estoque' } },
    { path: '/estoque/ajuste', component: () => import('@/modules/estoque/AjusteEstoqueView.vue'),
      meta: { titulo: 'Ajuste de Estoque' } },
    { path: '/estoque/etiquetas', component: () => import('@/modules/estoque/EtiquetasView.vue'),
      meta: { titulo: 'Editor de Etiquetas' } },
    { path: '/estoque/balanca', component: () => import('@/modules/estoque/BalancaView.vue'),
      meta: { titulo: 'Exportação para Balança' } },
    { path: '/estoque/validade', component: () => import('@/modules/estoque/ValidadeView.vue'),
      meta: { titulo: 'Controle de Validade' } },
    { path: '/estoque/perdas-validade', component: () => import('@/modules/estoque/PerdasValidadeView.vue'),
      meta: { titulo: 'Perdas por Validade' } },
    { path: '/auditoria', component: () => import('@/modules/auditoria/AuditoriaView.vue'),
      meta: { titulo: 'Auditoria' } },
    { path: '/estoque/materiais', component: () => import('@/modules/estoque/MateriaisConsumoView.vue'),
      meta: { titulo: 'Materiais de Consumo' } },
    { path: '/estoque/ativos', component: () => import('@/modules/estoque/AtivosImobilizadosView.vue'),
      meta: { titulo: 'Ativo Imobilizado' } },
    { path: '/estoque/validade/config', component: () => import('@/modules/estoque/ValidadeConfigView.vue'),
      meta: { titulo: 'Configurações de Validade' } },
    { path: '/estoque/alterar-precos', component: () => import('@/modules/estoque/AlteracaoPrecosView.vue'),
      meta: { titulo: 'Alterar Preços' } },

    // Página pública (sem auth guard)
    { path: '/produto/:id', component: () => import('@/modules/publico/ProdutoPublicoView.vue'),
      meta: { publica: true, titulo: 'Produto' } },
    // Vitrine pública (e-commerce) — cliente monta o pedido; cai na loja escolhida
    { path: '/loja/:empresaId', component: () => import('@/modules/publico/VitrineView.vue'),
      meta: { publica: true, titulo: 'Loja Online' } },

    // Compras
    { path: '/compras', component: () => import('@/modules/compras/ComprasView.vue'),
      meta: { titulo: 'Pedidos de Compra' } },
    { path: '/compras/cotacoes', component: () => import('@/modules/compras/CotacoesView.vue'),
      meta: { titulo: 'Comparador de Cotações' } },

    // Crediário
    { path: '/crediario', component: () => import('@/modules/crediario/CrediarioView.vue'),
      meta: { titulo: 'Crediário' } },

    // Financeiro
    { path: '/financeiro', component: () => import('@/modules/financeiro/FinanceiroView.vue'),
      meta: { titulo: 'Financeiro' } },
    { path: '/financeiro/contas-receber', component: () => import('@/modules/financeiro/ContasReceberView.vue'),
      meta: { titulo: 'Contas a Receber' } },
    { path: '/financeiro/contas-pagar', component: () => import('@/modules/financeiro/ContasPagarView.vue'),
      meta: { titulo: 'Contas a Pagar' } },
    { path: '/financeiro/dre', component: () => import('@/modules/financeiro/DreView.vue'),
      meta: { titulo: 'DRE — Demonstrativo de Resultados' } },
    { path: '/financeiro/dre-mensal', component: () => import('@/modules/financeiro/DreMensalView.vue'),
      meta: { titulo: 'DRE Comparativo (mês a mês)' } },
    { path: '/financeiro/fluxo-caixa', component: () => import('@/modules/financeiro/FluxoCaixaView.vue'),
      meta: { titulo: 'Fluxo de Caixa' } },
    { path: '/financeiro/custos-fixos', component: () => import('@/modules/financeiro/CustosFixosView.vue'),
      meta: { titulo: 'Custos Fixos' } },
    { path: '/financeiro/operadoras-cartao', component: () => import('@/modules/financeiro/OperadorasCartaoView.vue'),
      meta: { titulo: 'Operadoras de Cartão' } },
    { path: '/financeiro/recebiveis-cartao', component: () => import('@/modules/financeiro/ReceiveisCartaoView.vue'),
      meta: { titulo: 'Recebíveis de Cartão' } },
    { path: '/financeiro/financiamentos', component: () => import('@/modules/financeiro/FinanciamentosView.vue'),
      meta: { titulo: 'Financiamentos' } },

    // Fiscal
    { path: '/fiscal', component: () => import('@/modules/fiscal/FiscalView.vue'),
      meta: { titulo: 'Documentos Fiscais' } },
    { path: '/fiscal/entradas/:id', component: () => import('@/modules/fiscal/EntradaNFeView.vue'),
      meta: { titulo: 'Escrituração de Entrada' } },
    { path: '/fiscal/cte-recebidos', component: () => import('@/modules/fiscal/CteRecebidosView.vue'),
      meta: { titulo: 'CT-e recebidos' } },

    // Contabilidade
    { path: '/contabilidade', component: () => import('@/modules/contabilidade/ContabilidadeView.vue'),
      meta: { titulo: 'Contabilidade' } },
    { path: '/contador', component: () => import('@/modules/contabilidade/PainelContadorView.vue'),
      meta: { publica: true, titulo: 'Painel do Contador' } },

    // WhatsApp
    { path: '/whatsapp', component: () => import('@/modules/whatsapp/WhatsAppView.vue'),
      meta: { titulo: 'Catálogo WhatsApp' } },

    // Marketing
    { path: '/marketing', component: () => import('@/modules/marketing/MarketingView.vue'),
      meta: { titulo: 'Marketing' } },
    { path: '/marketing/clube', component: () => import('@/modules/marketing/ClubePromocoesView.vue'),
      meta: { titulo: 'Clube de Promoções' } },
    { path: '/marketing/promocoes', component: () => import('@/modules/marketing/PromocoesView.vue'),
      meta: { titulo: 'Promoções' } },

    // Cadastros
    { path: '/cadastros', component: () => import('@/modules/cadastros/CadastrosView.vue'),
      meta: { titulo: 'Cadastros' } },
    { path: '/cadastros/clientes', component: () => import('@/modules/cadastros/ClientesView.vue'),
      meta: { titulo: 'Clientes' } },
    { path: '/cadastros/fornecedores', component: () => import('@/modules/cadastros/FornecedoresView.vue'),
      meta: { titulo: 'Fornecedores' } },
    { path: '/cadastros/categorias', component: () => import('@/modules/cadastros/CategoriasView.vue'),
      meta: { titulo: 'Categorias de Produtos' } },
    { path: '/cadastros/marcas', component: () => import('@/modules/cadastros/MarcasView.vue'),
      meta: { titulo: 'Marcas de Produtos' } },
    { path: '/cadastros/unidades-medida', component: () => import('@/modules/cadastros/UnidadesMedidaView.vue'),
      meta: { titulo: 'Unidades de Medida' } },
    { path: '/cadastros/locais-estoque', component: () => import('@/modules/cadastros/LocaisEstoqueView.vue'),
      meta: { titulo: 'Locais de Estoque' } },
    { path: '/cadastros/colaboradores', component: () => import('@/modules/cadastros/ColaboradoresView.vue'),
      meta: { titulo: 'Colaboradores' } },

    // Relatórios
    { path: '/relatorios', component: () => import('@/modules/relatorios/RelatoriosView.vue'),
      meta: { titulo: 'Relatórios' } },
    { path: '/relatorios/comparativo-lojas', component: () => import('@/modules/relatorios/ComparativoLojasView.vue'),
      meta: { titulo: 'Comparativo entre Lojas' } },
    { path: '/relatorios/clientes-sumidos', component: () => import('@/modules/relatorios/ClientesSumidosView.vue'),
      meta: { titulo: 'Clientes Sumidos' } },
    { path: '/relatorios/rentabilidade-categoria', component: () => import('@/modules/relatorios/RentabilidadeCategoriaView.vue'),
      meta: { titulo: 'Rentabilidade por Categoria' } },
    { path: '/relatorios/planejamento-anual', component: () => import('@/modules/relatorios/PlanejamentoAnualView.vue'),
      meta: { titulo: 'Planejamento Anual de Vendas' } },
    { path: '/relatorios/materiais', component: () => import('@/modules/relatorios/RelatoriosMateriaisView.vue'),
      meta: { titulo: 'Relatórios de Materiais de Consumo' } },

    // Configurações
    { path: '/configuracoes', component: () => import('@/modules/configuracoes/ConfiguracoesView.vue'),
      meta: { titulo: 'Configurações' } },
    { path: '/configuracoes/filiais', component: () => import('@/modules/configuracoes/FiliaisView.vue'),
      meta: { titulo: 'Unidades / Filiais' } },
    { path: '/configuracoes/saude-jobs', component: () => import('@/modules/configuracoes/SaudeJobsView.vue'),
      meta: { titulo: 'Saúde dos Jobs' } },

    { path: '/:pathMatch(.*)*', redirect: '/' },
  ]
})

// Rotas que o perfil "Atendente" pode acessar (o resto é bloqueado por URL também).
const ROTAS_ATENDENTE = [
  '/pdv', '/pdv/vendas', '/pdv/sessoes',
  '/cadastros/clientes',
  '/estoque/produtos',
  '/estoque/etiquetas', '/estoque/validade', '/estoque/perdas-validade', '/estoque/balanca',
  '/whatsapp',
  '/marketing', '/marketing/clube',
]

// Prefixos que o perfil "Contador" pode acessar (só fiscal e contábil).
const PREFIXOS_CONTADOR = ['/contabilidade', '/contador']

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (!to.meta.publica && !auth.logado) return '/login'
  const role = auth.usuario?.role
  // Atendente: se tentar abrir uma tela fora do permitido, manda para o PDV.
  if (auth.logado && role === 'Atendente'
      && !to.meta.publica && !ROTAS_ATENDENTE.includes(to.path)) {
    return '/pdv'
  }
  // Contador: só telas fiscais/contábeis; o resto redireciona para a Contabilidade.
  if (auth.logado && role === 'Contador' && !to.meta.publica
      && !PREFIXOS_CONTADOR.some(p => to.path === p || to.path.startsWith(p + '/'))) {
    return '/contabilidade'
  }
})

export default router
