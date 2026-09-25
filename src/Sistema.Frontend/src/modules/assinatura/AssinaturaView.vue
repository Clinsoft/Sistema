<template>
  <v-container class="py-6" style="max-width:760px">
    <!-- Bloqueada / expirada -->
    <v-card v-if="ass.bloqueado" rounded="xl" elevation="2" class="pa-6 mb-4" border="start" color="error" variant="tonal">
      <div class="d-flex align-center mb-2">
        <v-icon size="28" color="error" class="mr-2">mdi-lock-alert</v-icon>
        <span class="text-h6 font-weight-bold">Assinatura {{ ass.situacao === 'TrialExpirado' ? 'de teste expirada' : 'bloqueada' }}</span>
      </div>
      <p class="text-body-1 mb-4">
        {{ ass.situacao === 'TrialExpirado'
          ? 'Seu período de teste gratuito terminou. Para continuar usando o sistema, ative um plano.'
          : 'Sua assinatura está bloqueada. Regularize o pagamento para reativar o acesso. Seus dados estão salvos.' }}
      </p>
      <div class="d-flex gap-3 flex-wrap">
        <v-btn color="success" prepend-icon="mdi-whatsapp" :href="whatsappUrl" target="_blank" class="text-none">Falar com o suporte</v-btn>
        <v-btn variant="outlined" prepend-icon="mdi-open-in-new" href="https://naturalsistemas.com.br/#planos" target="_blank" class="text-none">Ver planos</v-btn>
      </div>
    </v-card>

    <!-- Upsell de recurso -->
    <v-card v-else-if="recurso" rounded="xl" elevation="2" class="pa-6 mb-4" border="start" color="primary" variant="tonal">
      <div class="d-flex align-center mb-2">
        <v-icon size="28" color="primary" class="mr-2">mdi-lock-open-outline</v-icon>
        <span class="text-h6 font-weight-bold">Recurso do plano {{ planoMinimo }}</span>
      </div>
      <p class="text-body-1 mb-1">
        <b>{{ recursoLabel }}</b> não está incluído no seu plano atual (<b>{{ ass.plano }}</b>).
      </p>
      <p class="text-body-2 text-medium-emphasis mb-4">
        Faça upgrade para o plano <b>{{ planoMinimo }}</b> e desbloqueie este e outros recursos.
      </p>
      <div class="d-flex gap-3 flex-wrap">
        <v-btn color="success" prepend-icon="mdi-whatsapp" :href="whatsappUrl" target="_blank" class="text-none">Fazer upgrade</v-btn>
        <v-btn variant="outlined" prepend-icon="mdi-open-in-new" href="https://naturalsistemas.com.br/#planos" target="_blank" class="text-none">Comparar planos</v-btn>
      </div>
    </v-card>

    <!-- Assinar / pagar -->
    <v-card rounded="xl" elevation="1" class="pa-6 mb-4">
      <div class="text-overline text-medium-emphasis">Assinar / regularizar</div>
      <div class="text-body-2 text-medium-emphasis mb-3">Pague por Pix, boleto ou cartão. No plano anual você paga 10 meses e ganha 2.</div>
      <v-row dense align="center">
        <v-col cols="12" sm="5">
          <v-select v-model="planoSel" :items="planos" label="Plano" density="comfortable" variant="outlined" hide-details />
        </v-col>
        <v-col cols="12" sm="4">
          <v-btn-toggle v-model="cicloSel" mandatory density="comfortable" color="primary" rounded="lg" class="w-100">
            <v-btn value="Mensal" class="text-none flex-grow-1">Mensal</v-btn>
            <v-btn value="Anual" class="text-none flex-grow-1">Anual</v-btn>
          </v-btn-toggle>
        </v-col>
        <v-col cols="12" sm="3" class="text-right">
          <div class="text-h6 font-weight-bold text-primary">R$ {{ preco }}</div>
          <div class="text-caption text-medium-emphasis">{{ cicloSel === 'Anual' ? 'por ano' : 'por mês' }}</div>
        </v-col>
      </v-row>
      <v-btn color="success" size="large" rounded="lg" block class="text-none font-weight-bold mt-3"
        prepend-icon="mdi-credit-card-outline" :loading="gerando" @click="assinar">
        Gerar pagamento
      </v-btn>
    </v-card>

    <!-- Status atual -->
    <v-card rounded="xl" elevation="1" class="pa-6">
      <div class="text-overline text-medium-emphasis">Minha assinatura</div>
      <div class="d-flex align-center mb-3">
        <span class="text-h5 font-weight-bold text-primary">Plano {{ ass.plano }}</span>
        <v-chip class="ml-3" :color="corSituacao" size="small" label>{{ labelSituacao }}</v-chip>
      </div>

      <v-alert v-if="ass.emTrial" type="info" variant="tonal" density="comfortable" class="mb-4">
        Você está no <b>teste grátis</b> — faltam <b>{{ ass.diasTrial }}</b> dia(s). Durante o teste, todos os recursos do plano <b>Profissional</b> estão liberados.
      </v-alert>

      <v-table density="comfortable">
        <tbody>
          <tr><td class="text-medium-emphasis">Plano contratado</td><td class="text-right font-weight-medium">{{ ass.plano }}</td></tr>
          <tr><td class="text-medium-emphasis">Situação</td><td class="text-right font-weight-medium">{{ labelSituacao }}</td></tr>
          <tr v-if="ass.info.proximoVencimento"><td class="text-medium-emphasis">Próximo vencimento</td><td class="text-right font-weight-medium">{{ fmtData(ass.info.proximoVencimento) }}</td></tr>
          <tr v-if="ass.info.trialAte"><td class="text-medium-emphasis">Teste até</td><td class="text-right font-weight-medium">{{ fmtData(ass.info.trialAte) }}</td></tr>
        </tbody>
      </v-table>

      <div class="mt-4 d-flex gap-3 flex-wrap">
        <v-btn color="primary" variant="flat" prepend-icon="mdi-arrow-left" to="/" class="text-none">Voltar ao sistema</v-btn>
        <v-btn variant="text" prepend-icon="mdi-whatsapp" :href="whatsappUrl" target="_blank" class="text-none">Suporte / vendas</v-btn>
      </div>
    </v-card>
  </v-container>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useAssinaturaStore } from '@/stores/assinatura'
import api from '@/composables/useApi'
import { useNotifStore } from '@/stores/notif'

const ass = useAssinaturaStore()
const route = useRoute()
const notif = useNotifStore()

const planos = ['Micro', 'Essencial', 'Profissional', 'Rede']
const PRECO_MENSAL: Record<string, number> = { Micro: 79, Essencial: 119, Profissional: 229, Rede: 379 }
const planoSel = ref(['Micro', 'Essencial', 'Profissional', 'Rede'].includes(ass.plano) ? ass.plano : 'Profissional')
const cicloSel = ref<'Mensal' | 'Anual'>('Mensal')
const gerando = ref(false)
const preco = computed(() => {
  const m = PRECO_MENSAL[planoSel.value] ?? 0
  return (cicloSel.value === 'Anual' ? m * 10 : m).toLocaleString('pt-BR')
})

async function assinar() {
  gerando.value = true
  try {
    const { data } = await api.post('/minha-assinatura/checkout', { plano: planoSel.value, ciclo: cicloSel.value }, { _quiet: true } as any)
    if (data?.link) window.open(data.link, '_blank')
    else notif.aviso('Cobrança gerada, mas sem link de fatura. Fale com o suporte.')
  } catch (e: any) {
    notif.erro(e?.response?.data?.mensagem ?? 'Não foi possível gerar o pagamento.')
  } finally {
    gerando.value = false
  }
}

const whatsappUrl = 'https://wa.me/5518981952545?text=' +
  encodeURIComponent('Olá! Quero ativar/regularizar minha assinatura do Natural Sistemas.')

const recurso = computed(() => (route.query.recurso as string) || '')

const LABELS: Record<string, { label: string; plano: string }> = {
  Estoque: { label: 'Controle de estoque', plano: 'Essencial' },
  Validade: { label: 'Controle de validade', plano: 'Essencial' },
  Etiquetas: { label: 'Etiquetas', plano: 'Essencial' },
  Financeiro: { label: 'Financeiro e DRE', plano: 'Profissional' },
  Whatsapp: { label: 'WhatsApp integrado', plano: 'Profissional' },
  MarketingIa: { label: 'Marketing com IA', plano: 'Profissional' },
  Fidelidade: { label: 'Fidelidade e cashback', plano: 'Profissional' },
  Recebiveis: { label: 'Recebíveis de cartão', plano: 'Profissional' },
  MultiLoja: { label: 'Multi-loja', plano: 'Rede' },
  DrePorLoja: { label: 'DRE por loja', plano: 'Rede' },
  Premiacao: { label: 'Metas e premiação', plano: 'Rede' },
  Cotacoes: { label: 'Comparador de cotações', plano: 'Rede' },
  Concorrencia: { label: 'Mapa de concorrência', plano: 'Rede' },
}
const recursoLabel = computed(() => LABELS[recurso.value]?.label ?? recurso.value)
const planoMinimo = computed(() => LABELS[recurso.value]?.plano ?? 'superior')

const labelSituacao = computed(() => ({
  TrialAtivo: 'Em teste grátis', TrialExpirado: 'Teste expirado', Ativa: 'Ativa',
  EmTolerancia: 'Vencida (em tolerância)', Bloqueada: 'Bloqueada', Cancelada: 'Cancelada',
} as Record<string, string>)[ass.situacao] ?? ass.situacao)

const corSituacao = computed(() => ({
  TrialAtivo: 'info', Ativa: 'success', EmTolerancia: 'warning',
  TrialExpirado: 'error', Bloqueada: 'error', Cancelada: 'grey',
} as Record<string, string>)[ass.situacao] ?? 'grey')

const fmtData = (v: string | null) => v ? new Date(v).toLocaleDateString('pt-BR') : '—'
</script>
