<template>
  <div>
    <div class="d-flex align-center mb-4">
      <v-btn icon="mdi-arrow-left" variant="text" to="/estoque/validade" class="mr-2" />
      <div>
        <div class="text-h6 font-weight-bold">Configurações de Validade</div>
        <div class="text-caption text-medium-emphasis">
          Regras para alertas automáticos, promoções e bloqueios.
        </div>
      </div>
    </div>

    <v-row>
      <v-col cols="12" md="7">

        <!-- Limiares de alerta -->
        <v-card rounded="xl" elevation="1" class="mb-4">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold">
            <v-icon icon="mdi-clock-alert-outline" class="mr-2" />Limiares de Alerta (dias)
          </v-card-title>
          <v-card-text>
            <v-row dense>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="form.diasAlertaAmarelo"
                  label="🟡 Alerta Amarelo" type="number" :min="1" :max="365"
                  variant="outlined" density="compact" hint="Exibir em amarelo no painel"
                  persistent-hint />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="form.diasAlertaVermelho"
                  label="🔴 Alerta Vermelho" type="number" :min="1" :max="365"
                  variant="outlined" density="compact" hint="Vermelho + criar promoção automática"
                  persistent-hint />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="form.diasAlertaUrgente"
                  label="⚠️ Alerta Urgente" type="number" :min="1" :max="365"
                  variant="outlined" density="compact" hint="Notificação urgente de vencimento"
                  persistent-hint />
              </v-col>
            </v-row>

            <!-- Timeline visual -->
            <div class="mt-4 pa-3 rounded-lg bg-surface-variant">
              <div class="text-caption font-weight-medium mb-2">Como os dias são interpretados:</div>
              <div class="d-flex align-center gap-2 text-caption flex-wrap">
                <v-chip size="x-small" color="success" variant="flat">OK</v-chip>
                <span class="text-medium-emphasis">{{ form.diasAlertaAmarelo + 1 }}+ dias</span>
                <v-icon icon="mdi-arrow-right" size="12" />
                <v-chip size="x-small" color="amber" variant="flat">🟡 Amarelo</v-chip>
                <span class="text-medium-emphasis">{{ form.diasAlertaVermelho + 1 }}–{{ form.diasAlertaAmarelo }} dias</span>
                <v-icon icon="mdi-arrow-right" size="12" />
                <v-chip size="x-small" color="warning" variant="flat">🔴 Vermelho</v-chip>
                <span class="text-medium-emphasis">{{ form.diasAlertaUrgente + 1 }}–{{ form.diasAlertaVermelho }} dias</span>
                <v-icon icon="mdi-arrow-right" size="12" />
                <v-chip size="x-small" color="error" variant="flat">⚠️ Urgente</v-chip>
                <span class="text-medium-emphasis">0–{{ form.diasAlertaUrgente }} dias</span>
                <v-icon icon="mdi-arrow-right" size="12" />
                <v-chip size="x-small" color="error" variant="flat">✖ Vencido</v-chip>
              </div>
            </div>
          </v-card-text>
        </v-card>

        <!-- Promoção automática -->
        <v-card rounded="xl" elevation="1" class="mb-4">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold">
            <v-icon icon="mdi-tag-multiple-outline" class="mr-2" />Promoção Automática
          </v-card-title>
          <v-card-text>
            <v-switch v-model="form.promoAutomatica" color="primary"
              label="Criar promoção automaticamente ao atingir alerta Vermelho"
              hide-details class="mb-3" />

            <template v-if="form.promoAutomatica">
              <v-row dense>
                <v-col cols="12" sm="6">
                  <v-text-field v-model.number="form.descontoAutoPercent"
                    label="Desconto automático (%)" type="number" :min="1" :max="99"
                    variant="outlined" density="compact"
                    :hint="`Preço de R$ 100,00 → R$ ${(100 * (1 - form.descontoAutoPercent/100)).toFixed(2)}`"
                    persistent-hint />
                </v-col>
                <v-col cols="12" sm="6" class="d-flex align-center">
                  <v-switch v-model="form.exigeAprovacao" color="warning"
                    label="Exigir aprovação antes de publicar"
                    hide-details density="compact" />
                </v-col>
              </v-row>

              <v-alert v-if="!form.exigeAprovacao" type="info" variant="tonal"
                density="compact" class="mt-3" icon="mdi-robot-outline">
                Promoção e artes (Feed, Story, Banner) serão geradas automaticamente às 8h
                sem intervenção manual.
              </v-alert>
              <v-alert v-else type="warning" variant="tonal"
                density="compact" class="mt-3" icon="mdi-account-check-outline">
                As promoções criadas precisarão de aprovação manual em Marketing → Promoções.
              </v-alert>
            </template>
          </v-card-text>
        </v-card>

        <!-- Bloqueio de venda -->
        <v-card rounded="xl" elevation="1" class="mb-4">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold">
            <v-icon icon="mdi-shield-alert-outline" class="mr-2" />Comportamento para Vencidos
          </v-card-title>
          <v-card-text>
            <v-switch v-model="form.bloqueioVendaVencido" color="error"
              label="Bloquear venda de produtos com lote vencido no PDV"
              hide-details />
            <div class="text-caption text-medium-emphasis mt-1">
              Quando ativado, o operador de caixa não conseguirá finalizar uma venda
              com produto de lote vencido (apenas Administrador pode sobrescrever).
            </div>
          </v-card-text>
        </v-card>

      </v-col>

      <!-- Resumo lateral -->
      <v-col cols="12" md="5">
        <v-card rounded="xl" elevation="1" variant="tonal" color="primary" class="mb-4">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold">
            <v-icon icon="mdi-information-outline" class="mr-2" />Fluxo automático
          </v-card-title>
          <v-card-text class="text-body-2">
            <v-timeline density="compact" side="end" class="ml-n2">
              <v-timeline-item dot-color="amber" size="small">
                <strong>🟡 {{ form.diasAlertaAmarelo }} dias</strong> antes<br>
                <span class="text-medium-emphasis">Produto aparece amarelo no painel</span>
              </v-timeline-item>
              <v-timeline-item dot-color="warning" size="small">
                <strong>🔴 {{ form.diasAlertaVermelho }} dias</strong> antes<br>
                <span class="text-medium-emphasis">
                  Fica vermelho
                  <span v-if="form.promoAutomatica">
                    + promoção de {{ form.descontoAutoPercent }}% criada automaticamente
                    + 3 artes geradas (Feed, Story, Banner)
                  </span>
                </span>
              </v-timeline-item>
              <v-timeline-item dot-color="error" size="small">
                <strong>⚠️ {{ form.diasAlertaUrgente }} dias</strong> antes<br>
                <span class="text-medium-emphasis">Notificação urgente diária</span>
              </v-timeline-item>
              <v-timeline-item dot-color="error" size="small">
                <strong>✖ Vencido</strong><br>
                <span class="text-medium-emphasis">
                  <span v-if="form.bloqueioVendaVencido">Venda bloqueada no PDV</span>
                  <span v-else>Aparece como vencido (venda ainda permitida)</span>
                </span>
              </v-timeline-item>
            </v-timeline>
          </v-card-text>
        </v-card>

        <v-card rounded="xl" elevation="1">
          <v-card-title class="pa-4 pb-2 text-body-1 font-weight-bold">
            <v-icon icon="mdi-bell-outline" class="mr-2" />Notificação diária (8h)
          </v-card-title>
          <v-card-text class="text-body-2">
            O sistema envia automaticamente às <strong>8h</strong> um resumo com:
            <ul class="mt-2 ml-4">
              <li>Quantidade de produtos próximos do vencimento</li>
              <li>Produtos que entraram em promoção hoje</li>
              <li>Valor total do estoque em risco</li>
            </ul>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- Ações -->
    <div class="d-flex justify-end gap-3 mt-4">
      <v-btn variant="text" to="/estoque/validade">Cancelar</v-btn>
      <v-btn color="primary" variant="flat" :loading="salvando" @click="salvar">
        <v-icon icon="mdi-content-save-outline" class="mr-1" />Salvar configurações
      </v-btn>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const salvando = ref(false)

const form = ref({
  diasAlertaAmarelo: 60,
  diasAlertaVermelho: 30,
  diasAlertaUrgente: 15,
  promoAutomatica: true,
  exigeAprovacao: false,
  descontoAutoPercent: 30,
  bloqueioVendaVencido: false,
})

async function carregar() {
  try {
    const r = await api.get('/validade/configuracoes', { params: { empresaId: auth.empresaId } })
    const d = r.data
    form.value = {
      diasAlertaAmarelo: d.diasAlertaAmarelo,
      diasAlertaVermelho: d.diasAlertaVermelho,
      diasAlertaUrgente: d.diasAlertaUrgente,
      promoAutomatica: d.promoAutomatica,
      exigeAprovacao: d.exigeAprovacao,
      descontoAutoPercent: d.descontoAutoPercent,
      bloqueioVendaVencido: d.bloqueioVendaVencido,
    }
  } catch { /* usa padrões */ }
}

async function salvar() {
  salvando.value = true
  try {
    await api.put('/validade/configuracoes', form.value, {
      params: { empresaId: auth.empresaId }
    })
    notif.ok('Configurações salvas!')
  } catch {
    notif.erro('Erro ao salvar configurações.')
  } finally {
    salvando.value = false
  }
}

onMounted(carregar)
</script>
