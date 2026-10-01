<template>
  <v-container class="py-6" fluid style="max-width:1200px">
    <div class="d-flex align-center mb-4">
      <v-icon size="26" color="deep-purple-darken-1" class="mr-2">mdi-shield-crown-outline</v-icon>
      <div>
        <div class="text-h6 font-weight-bold">Administração do SaaS</div>
        <div class="text-caption text-medium-emphasis">Assinaturas das lojas-cliente · {{ lista.length }} conta(s)</div>
      </div>
      <v-spacer />
      <v-btn variant="text" prepend-icon="mdi-cog-sync-outline" class="text-none mr-1" @click="rodarBloqueio" title="Bloqueia trial expirado/inadimplência e envia os lembretes de vencimento">Rodar manutenção</v-btn>
      <v-btn variant="text" prepend-icon="mdi-refresh" :loading="carregando" @click="carregar" class="text-none">Atualizar</v-btn>
    </div>

    <v-alert v-if="nfseErro" type="warning" variant="tonal" density="compact" class="mb-3" :text="`Emissor NFS-e: ${nfseErro}`" />

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
            <th class="text-center">Lojas</th><th v-if="nfseAtivo" class="text-center">NFS-e</th><th>Vencimento / Trial</th><th class="text-right">Ações</th>
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
            <td v-if="nfseAtivo" class="text-center">
              <v-chip v-if="nfseDe(e)" :color="corNfse(nfseDe(e).assinatura)" size="x-small" label
                :title="`${nfseDe(e).notas_emitidas} nota(s) · ${nfseDe(e).ambiente}`">{{ labelNfse(nfseDe(e).assinatura) }}</v-chip>
              <span v-else class="text-medium-emphasis">—</span>
            </td>
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
            <td :colspan="nfseAtivo ? 8 : 7" class="text-center text-medium-emphasis py-6">Nenhuma loja cadastrada ainda.</td>
          </tr>
        </tbody>
      </v-table>
    </v-card>

    <!-- Clientes que existem SÓ no Emissor NFS-e (não são lojas da Natural) -->
    <v-card v-if="nfseAtivo && nfseSoEmissor.length" rounded="xl" elevation="1" class="mt-4">
      <v-card-title class="text-subtitle-1 d-flex align-center">
        <v-icon size="20" color="deep-purple-darken-1" class="mr-2">mdi-file-document-outline</v-icon>
        Só no Emissor NFS-e
        <span class="text-caption text-medium-emphasis ml-2">{{ nfseSoEmissor.length }} — não são lojas da Natural</span>
      </v-card-title>
      <v-table density="comfortable">
        <thead>
          <tr><th>Empresa</th><th>Município</th><th>Situação</th><th class="text-center">Notas</th><th>Último acesso</th></tr>
        </thead>
        <tbody>
          <tr v-for="c in nfseSoEmissor" :key="c.id">
            <td>
              <div class="font-weight-medium">{{ c.razao_social }}</div>
              <div class="text-caption text-medium-emphasis">{{ fmtCnpj(c.cnpj) }}</div>
            </td>
            <td>{{ c.municipio }}<span v-if="c.uf">/{{ c.uf }}</span></td>
            <td><v-chip :color="corNfse(c.assinatura)" size="small" label>{{ labelNfse(c.assinatura) }}</v-chip></td>
            <td class="text-center">{{ c.notas_emitidas }}</td>
            <td class="text-caption">{{ fmtData(c.ultimo_acesso) }}</td>
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
const nfse = ref<any[]>([])
const nfseErro = ref<string | null>(null)
// so aparece na instancia que INTEGRA o NFS-e (Nfse:BaseUrl/Token configurados).
// No EcoGranel fica false -> a coluna/seção NFS-e nem renderiza (tela identica à original).
const nfseAtivo = ref(false)

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
  // clientes do Emissor NFS-e — falha aqui NAO derruba o painel da Natural
  try {
    const { data } = await api.get('/admin/nfse-clientes', { _quiet: true } as any)
    nfseAtivo.value = !!data.configurado   // EcoGranel = false -> nada de NFS-e na tela
    nfse.value = data.clientes ?? []
    // so avisa se ESTA configurado mas falhou; "nao configurado" fica silencioso
    nfseErro.value = (data.configurado && !data.ok) ? (data.erro ?? 'Falha ao ler o emissor.') : null
  } catch { nfse.value = []; nfseErro.value = 'Não consegui consultar o Emissor NFS-e.' }
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
async function rodarBloqueio() {
  try { await api.post('/admin/rodar-bloqueio'); notif.ok('Verificação de bloqueios enfileirada.'); setTimeout(carregar, 3000) }
  catch { notif.erro('Falha ao rodar a verificação.') }
}

const labelSit = (s: string) => ({ TrialAtivo: 'Em teste', TrialExpirado: 'Teste expirado', Ativa: 'Ativa', EmTolerancia: 'Em tolerância', Bloqueada: 'Bloqueada', Cancelada: 'Cancelada' } as any)[s] ?? s
const corSit = (s: string) => ({ TrialAtivo: 'info', Ativa: 'success', EmTolerancia: 'warning', TrialExpirado: 'error', Bloqueada: 'error', Cancelada: 'grey' } as any)[s] ?? 'grey'
const fmtData = (v: string | null) => v ? new Date(v).toLocaleDateString('pt-BR') : '—'
const fmtCnpj = (c: string) => (c || '').length === 14 ? c.replace(/^(\w{2})(\w{3})(\w{3})(\w{4})(\w{2})$/, '$1.$2.$3/$4-$5') : c

// --- junção com o Emissor NFS-e (por CNPJ) ---
const soDigitos = (s: string) => (s || '').replace(/\D/g, '')
const nfsePorCnpj = computed(() => {
  const m = new Map<string, any>()
  for (const c of nfse.value) m.set(soDigitos(c.cnpj), c)
  return m
})
const nfseDe = (e: any) => nfsePorCnpj.value.get(soDigitos(e.cnpj))
const cnpjsLojas = computed(() => new Set(lista.value.map((e: any) => soDigitos(e.cnpj))))
const nfseSoEmissor = computed(() => nfse.value.filter((c: any) => !cnpjsLojas.value.has(soDigitos(c.cnpj))))
const labelNfse = (s: string) => ({ trial: 'Teste', ativa: 'Ativa', inadimplente: 'Em atraso', cancelada: 'Cancelada' } as any)[s] ?? s
const corNfse = (s: string) => ({ trial: 'info', ativa: 'success', inadimplente: 'warning', cancelada: 'grey' } as any)[s] ?? 'grey'

onMounted(carregar)
</script>
