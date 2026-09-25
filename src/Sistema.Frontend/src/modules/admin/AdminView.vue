<template>
  <v-container class="py-6" fluid style="max-width:1200px">
    <div class="d-flex align-center mb-4">
      <v-icon size="26" color="deep-purple-darken-1" class="mr-2">mdi-shield-crown-outline</v-icon>
      <div>
        <div class="text-h6 font-weight-bold">Administração do SaaS</div>
        <div class="text-caption text-medium-emphasis">Assinaturas das lojas-cliente · {{ lista.length }} conta(s)</div>
      </div>
      <v-spacer />
      <v-btn variant="text" prepend-icon="mdi-refresh" :loading="carregando" @click="carregar" class="text-none">Atualizar</v-btn>
    </div>

    <!-- Resumo -->
    <v-row dense class="mb-2">
      <v-col v-for="c in resumo" :key="c.label" cols="6" sm="3">
        <v-card rounded="lg" variant="tonal" :color="c.cor" class="pa-3 text-center">
          <div class="text-h5 font-weight-bold">{{ c.n }}</div>
          <div class="text-caption">{{ c.label }}</div>
        </v-card>
      </v-col>
    </v-row>

    <v-card rounded="xl" elevation="1">
      <v-table density="comfortable">
        <thead>
          <tr>
            <th>Loja</th><th>Plano</th><th>Situação</th><th class="text-center">Usuários</th>
            <th class="text-center">Lojas</th><th>Vencimento / Trial</th><th class="text-right">Ações</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="e in lista" :key="e.empresaId">
            <td>
              <div class="font-weight-medium">{{ e.nome }}</div>
              <div class="text-caption text-medium-emphasis">{{ fmtCnpj(e.cnpj) }} · {{ e.email }}</div>
            </td>
            <td style="min-width:150px">
              <v-select :model-value="e.plano" :items="planos" density="compact" variant="outlined" hide-details
                @update:model-value="v => trocarPlano(e, v)" />
            </td>
            <td><v-chip :color="corSit(e.situacao)" size="small" label>{{ labelSit(e.situacao) }}</v-chip></td>
            <td class="text-center">{{ e.usuarios }}</td>
            <td class="text-center">{{ e.lojas }}</td>
            <td class="text-caption">
              <span v-if="e.situacao.startsWith('Trial')">Trial até {{ fmtData(e.trialAte) }}</span>
              <span v-else-if="e.proximoVencimento">Vence {{ fmtData(e.proximoVencimento) }}</span>
              <span v-else class="text-medium-emphasis">—</span>
            </td>
            <td class="text-right" style="white-space:nowrap">
              <v-btn size="small" color="success" variant="tonal" class="text-none mr-1" @click="pagar(e)">Marcar pago</v-btn>
              <v-btn size="small" variant="tonal" class="text-none mr-1" @click="estender(e)">+7 trial</v-btn>
              <v-btn v-if="e.status !== 'Bloqueada'" size="small" color="error" variant="tonal" class="text-none" @click="bloquear(e)">Bloquear</v-btn>
              <v-btn v-else size="small" color="primary" variant="tonal" class="text-none" @click="reativar(e)">Reativar</v-btn>
            </td>
          </tr>
          <tr v-if="!lista.length && !carregando">
            <td colspan="7" class="text-center text-medium-emphasis py-6">Nenhuma loja cadastrada ainda.</td>
          </tr>
        </tbody>
      </v-table>
    </v-card>
  </v-container>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import api from '@/composables/useApi'
import { useNotifStore } from '@/stores/notif'

const notif = useNotifStore()
const lista = ref<any[]>([])
const carregando = ref(false)
const planos = ['Micro', 'Essencial', 'Profissional', 'Rede']

const resumo = computed(() => [
  { label: 'Total', n: lista.value.length, cor: 'primary' },
  { label: 'Em teste', n: lista.value.filter(e => e.situacao.startsWith('Trial')).length, cor: 'info' },
  { label: 'Ativas', n: lista.value.filter(e => e.situacao === 'Ativa' || e.situacao === 'EmTolerancia').length, cor: 'success' },
  { label: 'Bloqueadas', n: lista.value.filter(e => e.situacao === 'Bloqueada' || e.situacao === 'TrialExpirado').length, cor: 'error' },
])

async function carregar() {
  carregando.value = true
  try {
    const { data } = await api.get('/admin/empresas')
    lista.value = data.empresas ?? []
  } catch { notif.erro('Sem permissão ou falha ao carregar.') }
  finally { carregando.value = false }
}

async function acao(fn: Promise<any>, msg: string) {
  try { await fn; notif.ok(msg); await carregar() }
  catch (e: any) { notif.erro(e?.response?.data?.mensagem ?? 'Falha na ação.') }
}
const pagar = (e: any) => acao(api.post(`/admin/${e.empresaId}/pagar`, {}), 'Pagamento registrado.')
const estender = (e: any) => acao(api.post(`/admin/${e.empresaId}/trial`, { dias: 7 }), 'Trial estendido em 7 dias.')
const bloquear = (e: any) => acao(api.post(`/admin/${e.empresaId}/bloquear`, {}), 'Assinatura bloqueada.')
const reativar = (e: any) => acao(api.post(`/admin/${e.empresaId}/reativar`, {}), 'Assinatura reativada.')
const trocarPlano = (e: any, plano: string) => acao(api.post(`/admin/${e.empresaId}/plano`, { plano }), `Plano alterado para ${plano}.`)

const labelSit = (s: string) => ({ TrialAtivo: 'Em teste', TrialExpirado: 'Teste expirado', Ativa: 'Ativa', EmTolerancia: 'Em tolerância', Bloqueada: 'Bloqueada', Cancelada: 'Cancelada' } as any)[s] ?? s
const corSit = (s: string) => ({ TrialAtivo: 'info', Ativa: 'success', EmTolerancia: 'warning', TrialExpirado: 'error', Bloqueada: 'error', Cancelada: 'grey' } as any)[s] ?? 'grey'
const fmtData = (v: string | null) => v ? new Date(v).toLocaleDateString('pt-BR') : '—'
const fmtCnpj = (c: string) => (c || '').length === 14 ? c.replace(/^(\w{2})(\w{3})(\w{3})(\w{4})(\w{2})$/, '$1.$2.$3/$4-$5') : c

onMounted(carregar)
</script>
