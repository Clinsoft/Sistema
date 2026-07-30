<template>
  <div>
    <div class="text-h6 font-weight-bold mb-4">Configurações</div>

    <GuiaPassos
      id="configuracoes"
      titulo="Como usar as Configurações"
      :passos="[
        '<b>Empresa</b>: preencha razão social, CNPJ, inscrição estadual e endereço (o CEP preenche o endereço automaticamente). Clique em <b>Salvar</b> — esses dados vão para o XML das notas.',
        '<b>Configuração Fiscal</b>: 1º defina o <b>regime</b>, <b>ambiente</b> (Homologação p/ testes, Produção p/ valor fiscal), séries e próximos números de NF-e/NFC-e; clique em <b>Salvar Configurações Fiscais</b>.',
        'Depois instale o <b>Certificado Digital A1</b> (.pfx/.p12): arraste o arquivo, informe a senha e use <b>Validar</b> para conferir e <b>Instalar</b>. Configure CSC da NFC-e, DANFE, tributação padrão e, se usar, NFS-e e MDF-e.',
        '<b>Usuários</b>: cadastre colaboradores com <b>nível de acesso</b> (Administrador, Vendedor, Financeiro, Contador). No menu ⋮ você pode <b>alterar o perfil</b>, <b>redefinir a senha</b> e <b>ativar/desativar</b> o acesso.',
      ]"
    />

    <v-tabs v-model="aba" bg-color="transparent" class="mb-4">
      <v-tab value="empresa">Empresa</v-tab>
      <v-tab value="fiscal">Configuração Fiscal</v-tab>
      <v-tab value="usuarios">Usuários</v-tab>
    </v-tabs>

    <!-- Empresa -->
    <div v-if="aba==='empresa'">
      <v-card rounded="xl" elevation="1" class="pa-4">
        <v-form ref="formEmpresa" @submit.prevent="salvarEmpresa">
          <v-row dense>
            <v-col cols="12" sm="8">
              <v-text-field v-model="emp.razaoSocial" label="Razão Social *"
                variant="outlined" density="compact" :rules="[r=>!!r||'Obrigatório']" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="emp.nomeFantasia" label="Nome Fantasia"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="emp.cnpj" label="CNPJ *"
                variant="outlined" density="compact" :rules="[r=>!!r||'Obrigatório']" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="emp.inscricaoEstadual" label="Inscrição Estadual"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="emp.telefone" label="Telefone" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="emp.email" label="E-mail" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="3">
              <v-text-field v-model="emp.cep" label="CEP" variant="outlined" density="compact" @blur="buscarCep" :loading="buscandoCep" />
            </v-col>
            <v-col cols="12" sm="7">
              <v-text-field v-model="emp.logradouro" label="Logradouro" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="2">
              <v-text-field v-model="emp.numero" label="Nº" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="emp.bairro" label="Bairro" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-text-field v-model="emp.cidade" label="Cidade" variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="2">
              <v-text-field v-model="emp.uf" label="UF" variant="outlined" density="compact" maxlength="2" />
            </v-col>
          </v-row>
          <div class="d-flex justify-end mt-2">
            <v-btn color="primary" rounded="lg" :loading="salvando" type="submit">Salvar</v-btn>
          </div>
        </v-form>
      </v-card>
    </div>

    <!-- Fiscal -->
    <div v-if="aba==='fiscal'" class="d-flex flex-column gap-4">

      <!-- ── Aviso de ordem ── -->
      <v-alert type="info" variant="tonal" density="compact" icon="mdi-information-outline">
        <strong>Ordem recomendada:</strong>
        1º Preencha a <strong>Configuração Geral</strong> (abaixo) e salve →
        2º Instale o <strong>Certificado Digital A1</strong> (este card).
      </v-alert>

      <!-- ── Certificado Digital ── -->
      <v-card rounded="xl" elevation="1">
        <div class="cfg-secao-header">
          <div class="d-flex align-center gap-2">
            <v-icon icon="mdi-certificate-outline" color="primary" size="20" />
            <span>Certificado Digital A1</span>
          </div>
          <!-- Badge de status do certificado já instalado -->
          <v-chip v-if="certAtual" :color="certAtual.expirado ? 'error' : certAtual.diasRestantes <= 30 ? 'warning' : 'success'"
            size="small" variant="tonal">
            <v-icon start size="14"
              :icon="certAtual.expirado ? 'mdi-alert-circle' : certAtual.diasRestantes <= 30 ? 'mdi-clock-alert-outline' : 'mdi-shield-check'" />
            {{ certAtual.expirado ? 'Expirado' : certAtual.diasRestantes <= 30 ? `Expira em ${certAtual.diasRestantes} dias` : 'Válido' }}
          </v-chip>
        </div>

        <div class="pa-4">
          <!-- Info do certificado atual -->
          <v-expand-transition>
            <div v-if="certAtual && !certAtual.expirado" class="cert-info mb-4">
              <v-row dense>
                <v-col cols="12" sm="6">
                  <div class="text-caption text-medium-emphasis">Titular</div>
                  <div class="text-body-2 font-weight-medium">{{ certAtual.titular }}</div>
                </v-col>
                <v-col cols="12" sm="6">
                  <div class="text-caption text-medium-emphasis">CNPJ no certificado</div>
                  <div class="text-body-2 font-weight-medium">{{ certAtual.cnpj }}</div>
                </v-col>
                <v-col cols="12" sm="6">
                  <div class="text-caption text-medium-emphasis">Autoridade Certificadora</div>
                  <div class="text-body-2">{{ certAtual.emissor }}</div>
                </v-col>
                <v-col cols="6" sm="3">
                  <div class="text-caption text-medium-emphasis">Válido de</div>
                  <div class="text-body-2">{{ fmtData(certAtual.validoDe) }}</div>
                </v-col>
                <v-col cols="6" sm="3">
                  <div class="text-caption text-medium-emphasis">Válido até</div>
                  <div class="text-body-2 font-weight-bold" :class="certAtual.diasRestantes <= 30 ? 'text-warning' : 'text-success'">
                    {{ fmtData(certAtual.validoAte) }}
                  </div>
                </v-col>
              </v-row>
            </div>
          </v-expand-transition>

          <!-- Alert certificado expirado -->
          <v-alert v-if="certAtual?.expirado" type="error" variant="tonal" density="compact" class="mb-4"
            text="O certificado digital está expirado. Faça o upload de um novo certificado para continuar emitindo documentos fiscais." />

          <!-- Alert expirando -->
          <v-alert v-if="certAtual && !certAtual.expirado && certAtual.diasRestantes <= 30"
            type="warning" variant="tonal" density="compact" class="mb-4">
            Atenção: o certificado expira em <strong>{{ certAtual.diasRestantes }} dias</strong> ({{ fmtData(certAtual.validoAte) }}).
            Renove-o com sua Autoridade Certificadora.
          </v-alert>

          <!-- Upload zone -->
          <div
            class="cert-upload-zone"
            :class="{ 'cert-upload-zone--drag': arrastando, 'cert-upload-zone--filled': !!certFile }"
            @dragover.prevent="arrastando = true"
            @dragleave.prevent="arrastando = false"
            @drop.prevent="onDrop"
            @click="$refs.inputCert.click()"
          >
            <input ref="inputCert" type="file" accept=".pfx,.p12" style="display:none" @change="onFilePick" />
            <v-icon :icon="certFile ? 'mdi-file-certificate' : 'mdi-upload'" size="32"
              :color="certFile ? 'primary' : 'medium-emphasis'" class="mb-2" />
            <div v-if="certFile" class="text-body-2 font-weight-bold text-primary">{{ certFile.name }}</div>
            <div v-else class="text-body-2 text-medium-emphasis">
              Arraste o arquivo <strong>.pfx</strong> ou <strong>.p12</strong> aqui, ou clique para selecionar
            </div>
            <div v-if="certFile" class="text-caption text-medium-emphasis mt-1">
              {{ (certFile.size / 1024).toFixed(1) }} KB — clique para trocar
            </div>
          </div>

          <!-- Senha + botões -->
          <v-row dense class="mt-3" align="center">
            <v-col cols="12" sm="5">
              <v-text-field
                v-model="certSenha"
                label="Senha do certificado *"
                :type="mostrarSenhaCert ? 'text' : 'password'"
                :append-inner-icon="mostrarSenhaCert ? 'mdi-eye-off' : 'mdi-eye'"
                @click:append-inner="mostrarSenhaCert = !mostrarSenhaCert"
                variant="outlined" density="compact" hide-details
                :disabled="!certFile"
                @keyup.enter="validarCertificado"
              />
            </v-col>
            <v-col cols="12" sm="auto" class="d-flex gap-2">
              <v-btn
                color="primary"
                prepend-icon="mdi-certificate-outline"
                :loading="salvandoCert"
                :disabled="!certFile || !certSenha"
                @click="salvarCertificado"
              >
                Instalar
              </v-btn>
              <v-btn
                color="secondary" variant="tonal"
                prepend-icon="mdi-shield-search"
                :loading="validandoCert"
                :disabled="!certFile || !certSenha"
                @click="validarCertificado"
              >
                Validar
              </v-btn>
            </v-col>
          </v-row>

          <!-- Resultado da validação -->
          <v-expand-transition>
            <div v-if="certValidacao" class="mt-4">
              <v-alert
                :type="certValidacao.valido ? 'success' : 'error'"
                variant="tonal" rounded="lg"
                :icon="certValidacao.valido ? 'mdi-shield-check' : 'mdi-shield-off'"
              >
                <div v-if="certValidacao.valido">
                  <div class="font-weight-bold mb-2">Certificado válido — pronto para instalar</div>
                  <v-row dense>
                    <v-col cols="12" sm="6">
                      <span class="text-caption text-medium-emphasis">Titular: </span>
                      <span class="text-body-2 font-weight-medium">{{ certValidacao.titular }}</span>
                    </v-col>
                    <v-col cols="12" sm="6">
                      <span class="text-caption text-medium-emphasis">CNPJ: </span>
                      <span class="text-body-2 font-weight-medium">{{ certValidacao.cnpj }}</span>
                    </v-col>
                    <v-col cols="12" sm="6">
                      <span class="text-caption text-medium-emphasis">Emissor: </span>
                      <span class="text-body-2">{{ certValidacao.emissor }}</span>
                    </v-col>
                    <v-col cols="6" sm="3">
                      <span class="text-caption text-medium-emphasis">Válido de: </span>
                      <span class="text-body-2">{{ fmtData(certValidacao.validoDe) }}</span>
                    </v-col>
                    <v-col cols="6" sm="3">
                      <span class="text-caption text-medium-emphasis">Válido até: </span>
                      <span class="text-body-2 font-weight-bold">{{ fmtData(certValidacao.validoAte) }}</span>
                      <span class="text-caption ml-1">({{ certValidacao.diasRestantes }} dias)</span>
                    </v-col>
                    <v-col v-if="certValidacao.cnpjDivergente" cols="12">
                      <v-alert type="warning" density="compact" variant="tonal" class="mt-2"
                        :text="`Atenção: o CNPJ do certificado (${certValidacao.cnpj}) é diferente do cadastrado na empresa. Verifique antes de instalar.`" />
                    </v-col>
                  </v-row>
                </div>
                <div v-else>
                  <div class="font-weight-bold mb-1">Certificado inválido</div>
                  <div class="text-body-2">{{ certValidacao.erro }}</div>
                </div>
              </v-alert>
            </div>
          </v-expand-transition>
        </div>
      </v-card>

      <!-- ── Geral ── -->
      <v-card rounded="xl" elevation="1">
        <div class="cfg-secao-header">
          <div class="d-flex align-center gap-2">
            <v-icon icon="mdi-tune-vertical" color="primary" size="20" />
            <span>Configuração Geral</span>
          </div>
        </div>
        <div class="pa-4">
          <v-row dense>
            <v-col cols="12" sm="4">
              <v-select v-model="fiscal.regime" label="Regime Tributário *"
                :items="[{title:'Simples Nacional',value:'SimplesNacional'},{title:'Lucro Presumido',value:'LucroPresumido'},{title:'Lucro Real',value:'LucroReal'}]"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-select v-model="fiscal.ambiente" label="Ambiente SEFAZ *"
                :items="[{title:'Homologação (Testes)',value:'Homologacao'},{title:'Produção',value:'Producao'}]"
                variant="outlined" density="compact">
                <template #item="{ item, props }">
                  <v-list-item v-bind="props">
                    <template #prepend>
                      <v-icon :icon="item.value === 'Producao' ? 'mdi-check-decagram' : 'mdi-flask-outline'"
                        :color="item.value === 'Producao' ? 'success' : 'warning'" class="mr-2" />
                    </template>
                    <template #subtitle>{{ item.value === 'Producao' ? 'Notas com validade fiscal real' : 'Para testes — sem valor fiscal' }}</template>
                  </v-list-item>
                </template>
              </v-select>
            </v-col>
            <v-col cols="12" sm="4">
              <v-select v-model="fiscal.contingenciaPadrao" label="Modo de Contingência"
                :items="formasEmissao" item-title="title" item-value="value"
                variant="outlined" density="compact"
                hint="Usado automaticamente quando o SEFAZ principal não responde" persistent-hint />
            </v-col>
            <v-col cols="12" sm="6">
              <v-text-field v-model="fiscal.naturezaOperacaoPadrao" label="Natureza da Operação padrão *"
                variant="outlined" density="compact"
                hint="Obrigatório no XML da NF-e e NFC-e — ex: VENDA DE MERCADORIAS" persistent-hint
                placeholder="VENDA DE MERCADORIAS" />
            </v-col>
          </v-row>
          <v-alert v-if="fiscal.ambiente === 'Producao'" type="warning" variant="tonal"
            density="compact" class="mt-3" icon="mdi-alert">
            Ambiente de <strong>Produção</strong> — todas as notas emitidas terão validade fiscal real.
          </v-alert>
          <v-alert v-if="fiscal.contingenciaPadrao === 'Offline'" type="info" variant="tonal"
            density="compact" class="mt-2" icon="mdi-information-outline">
            A contingência <strong>Offline (código 9)</strong> é exclusiva para <strong>NFC-e</strong>.
            Para NF-e, use SVC-AN ou SVC-RS.
          </v-alert>
        </div>
      </v-card>

      <!-- ── NF-e (Modelo 55) ── -->
      <v-card rounded="xl" elevation="1">
        <div class="cfg-secao-header">
          <div class="d-flex align-center gap-2">
            <v-icon icon="mdi-file-document-check-outline" color="indigo" size="20" />
            <span>NF-e — Nota Fiscal Eletrônica (Modelo 55)</span>
          </div>
        </div>
        <div class="pa-4">
          <v-row dense>
            <v-col cols="6" sm="2">
              <v-text-field v-model.number="fiscal.serieNFe" label="Série"
                type="number" variant="outlined" density="compact"
                hint="Padrão: 1" persistent-hint />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field v-model.number="fiscal.proximoNumeroNFe" label="Próximo número"
                type="number" variant="outlined" density="compact"
                hint="Número da próxima NF-e a emitir" persistent-hint />
            </v-col>
            <v-col cols="12" sm="4">
              <v-select v-model="fiscal.formatoDanfe" label="Formato DANFE"
                :items="[{title:'Retrato',value:'Retrato'},{title:'Paisagem',value:'Paisagem'}]"
                variant="outlined" density="compact"
                hint="Orientação da impressão do DANFE" persistent-hint />
            </v-col>
            <v-col cols="12" sm="3">
              <v-switch v-model="fiscal.enviarEmailDestinatario" color="primary"
                label="Enviar DANFE por e-mail" density="compact" hide-details class="mt-1" />
            </v-col>
            <v-col v-if="fiscal.enviarEmailDestinatario" cols="12" sm="6">
              <v-text-field v-model="fiscal.emailCopiaFixa" label="E-mail em cópia fixa (CC)"
                type="email" variant="outlined" density="compact"
                hint="Receberá cópia de toda NF-e emitida (ex: contador)" persistent-hint />
            </v-col>
          </v-row>
        </div>
      </v-card>

      <!-- ── NFC-e (Modelo 65) ── -->
      <v-card rounded="xl" elevation="1">
        <div class="cfg-secao-header">
          <div class="d-flex align-center gap-2">
            <v-icon icon="mdi-receipt-text-outline" color="teal" size="20" />
            <span>NFC-e — Nota Fiscal do Consumidor (Modelo 65)</span>
          </div>
        </div>
        <div class="pa-4">
          <v-row dense>
            <v-col cols="6" sm="2">
              <v-text-field v-model.number="fiscal.serieNFCe" label="Série"
                type="number" variant="outlined" density="compact"
                hint="Padrão: 1" persistent-hint />
            </v-col>
            <v-col cols="6" sm="3">
              <v-text-field v-model.number="fiscal.proximoNumeroNFCe" label="Próximo número"
                type="number" variant="outlined" density="compact"
                hint="Número da próxima NFC-e" persistent-hint />
            </v-col>
            <v-col cols="6" sm="2">
              <v-text-field v-model.number="fiscal.cscIdNFCe" label="CSC ID"
                type="number" min="1" max="999999"
                variant="outlined" density="compact"
                hint="Número inteiro (1–999999)" persistent-hint />
            </v-col>
            <v-col cols="6" sm="5">
              <v-text-field v-model="fiscal.cscTokenNFCe" label="CSC Token (cSC)"
                variant="outlined" density="compact"
                hint="Token gerado no portal da SEFAZ" persistent-hint />
            </v-col>
            <v-col cols="12" sm="4">
              <v-select v-model="fiscal.tipoImpressaoNFCe" label="Impressão do Cupom"
                :items="[{title:'Impressora térmica (80mm)',value:'Termica80'},{title:'Impressora térmica (58mm)',value:'Termica58'},{title:'Impressora comum A4',value:'A4'}]"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-switch v-model="fiscal.imprimirAutomaticamenteNFCe" color="primary"
                label="Imprimir automaticamente ao emitir" density="compact" hide-details class="mt-1" />
            </v-col>
          </v-row>
        </div>
      </v-card>

      <!-- ── Tributação Padrão ── -->
      <v-card rounded="xl" elevation="1">
        <div class="cfg-secao-header">
          <div class="d-flex align-center gap-2">
            <v-icon icon="mdi-percent-box-outline" color="deep-purple" size="20" />
            <span>Tributação Padrão de Produtos</span>
          </div>
          <span class="text-caption text-medium-emphasis font-weight-regular normal-case">
            Aplicado automaticamente a novos produtos cadastrados
          </span>
        </div>
        <div class="pa-4">
          <v-row dense>
            <!-- ICMS -->
            <v-col cols="12">
              <div class="trib-grupo-titulo">ICMS</div>
            </v-col>
            <v-col cols="12" sm="4">
              <v-select v-if="fiscal.regime === 'SimplesNacional'"
                v-model="fiscal.csosnPadrao" label="CSOSN Padrão"
                :items="csosnOpcoes" item-title="title" item-value="value"
                variant="outlined" density="compact" />
              <v-select v-else
                v-model="fiscal.cstIcmsPadrao" label="CST ICMS Padrão"
                :items="cstIcmsOpcoes" item-title="title" item-value="value"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6" sm="2">
              <v-text-field v-model.number="fiscal.aliquotaIcmsPadrao" label="Alíquota ICMS (%)"
                type="number" step="0.01" variant="outlined" density="compact" suffix="%" />
            </v-col>
            <v-col cols="6" sm="2">
              <v-text-field v-model.number="fiscal.aliquotaIcmsInterestadual" label="ICMS Interestadual (%)"
                type="number" step="0.01" variant="outlined" density="compact" suffix="%" />
            </v-col>
            <v-col cols="12" sm="4">
              <v-select v-model="fiscal.origemPadrao" label="Origem da Mercadoria"
                :items="origensOpcoes" item-title="title" item-value="value"
                variant="outlined" density="compact" />
            </v-col>

            <!-- PIS / COFINS -->
            <v-col cols="12" class="mt-2">
              <div class="trib-grupo-titulo">PIS / COFINS</div>
            </v-col>
            <v-col cols="12" sm="3">
              <v-select v-model="fiscal.cstPisPadrao" label="CST PIS Padrão"
                :items="cstPisCofinsOpcoes" item-title="title" item-value="value"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6" sm="2">
              <v-text-field v-model.number="fiscal.aliquotaPisPadrao" label="Alíquota PIS (%)"
                type="number" step="0.001" variant="outlined" density="compact" suffix="%" />
            </v-col>
            <v-col cols="12" sm="3">
              <v-select v-model="fiscal.cstCofinsPadrao" label="CST COFINS Padrão"
                :items="cstPisCofinsOpcoes" item-title="title" item-value="value"
                variant="outlined" density="compact" />
            </v-col>
            <v-col cols="6" sm="2">
              <v-text-field v-model.number="fiscal.aliquotaCofinsPadrao" label="Alíquota COFINS (%)"
                type="number" step="0.001" variant="outlined" density="compact" suffix="%" />
            </v-col>

            <!-- CFOP padrão -->
            <v-col cols="12" class="mt-2">
              <div class="trib-grupo-titulo">CFOP</div>
            </v-col>
            <v-col cols="12" sm="3">
              <v-text-field v-model="fiscal.cfopVendaEstadual" label="CFOP Venda Estadual"
                variant="outlined" density="compact"
                hint="Ex: 5.102 — Simples Nacional" persistent-hint placeholder="5102" />
            </v-col>
            <v-col cols="12" sm="3">
              <v-text-field v-model="fiscal.cfopVendaInterestadual" label="CFOP Venda Interestadual"
                variant="outlined" density="compact"
                hint="Ex: 6.102" persistent-hint placeholder="6102" />
            </v-col>
            <v-col cols="12" sm="3">
              <v-text-field v-model="fiscal.cfopVendaConsumidor" label="CFOP Venda Consumidor (NFC-e)"
                variant="outlined" density="compact"
                hint="Ex: 5.102" persistent-hint placeholder="5102" />
            </v-col>
          </v-row>

          <!-- Botão aplicar em massa -->
          <v-divider class="my-4" />
          <div class="d-flex align-center gap-3 flex-wrap">
            <div class="flex-grow-1">
              <div class="text-body-2 font-weight-medium">Aplicar tributação padrão em todos os produtos</div>
              <div class="text-caption text-medium-emphasis">
                Preenche ICMS, PIS e COFINS de todos os produtos com os valores acima.
                NCM e CEST não são alterados.
              </div>
            </div>
            <v-btn color="deep-purple" variant="tonal" rounded="lg"
              prepend-icon="mdi-lightning-bolt" :loading="carregandoTrib"
              @click="abrirDialogAplicarTrib">
              Aplicar a todos os produtos
            </v-btn>
          </div>
        </div>
      </v-card>

      <!-- Dialog confirmação tributação padrão -->
      <v-dialog v-model="dialogTrib" max-width="520" persistent>
        <v-card rounded="xl">
          <v-card-title class="text-h6 pa-4 pb-2">
            <v-icon icon="mdi-percent-box-outline" color="deep-purple" class="mr-2" />
            Aplicar Tributação Padrão
          </v-card-title>
          <v-card-text>
            <v-alert v-if="tribPreview" type="info" variant="tonal" density="compact" class="mb-4">
              Serão atualizados
              <strong>{{ tribPreview.semTributacao }} produto(s)</strong> sem tributação
              (de {{ tribPreview.totalProdutos }} no total).
            </v-alert>

            <v-table density="compact" v-if="tribPreview">
              <thead><tr>
                <th>Campo</th><th>Valor</th>
              </tr></thead>
              <tbody>
                <tr v-if="fiscal.regime === 'SimplesNacional'">
                  <td>CSOSN</td><td><strong>{{ tribPreview.padrao?.csosnIcms }}</strong> — Não tributado (SN)</td>
                </tr>
                <tr v-else>
                  <td>CST ICMS</td><td><strong>{{ tribPreview.padrao?.cstIcms }}</strong> — Tributado integral</td>
                </tr>
                <tr>
                  <td>Alíquota ICMS</td><td><strong>{{ tribPreview.padrao?.aliquotaIcms }}%</strong></td>
                </tr>
                <tr>
                  <td>CST PIS/COFINS</td><td><strong>{{ tribPreview.padrao?.cstPisCofins }}</strong></td>
                </tr>
                <tr>
                  <td>Alíquota PIS</td><td><strong>{{ tribPreview.padrao?.aliquotaPis }}%</strong></td>
                </tr>
                <tr>
                  <td>Alíquota COFINS</td><td><strong>{{ tribPreview.padrao?.aliquotaCofins }}%</strong></td>
                </tr>
                <tr><td>CFOP</td><td><strong>{{ tribPreview.padrao?.cfop }}</strong></td></tr>
              </tbody>
            </v-table>

            <v-checkbox v-model="tribSobrazerosVazio" class="mt-3" density="compact" hide-details
              label="Aplicar apenas em produtos sem tributação definida" />
          </v-card-text>
          <v-card-actions class="pa-4 pt-0">
            <v-btn variant="text" @click="dialogTrib = false">Cancelar</v-btn>
            <v-spacer />
            <v-btn color="deep-purple" :loading="aplicandoTrib" @click="aplicarTributacao">
              Confirmar e Aplicar
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <!-- ── NFS-e (Serviços) ── -->
      <v-card rounded="xl" elevation="1">
        <div class="cfg-secao-header">
          <div class="d-flex align-center gap-2">
            <v-icon icon="mdi-briefcase-outline" color="orange" size="20" />
            <span>NFS-e — Nota Fiscal de Serviços (Municipal)</span>
          </div>
          <v-switch v-model="fiscal.habilitarNFSe" color="primary" density="compact"
            hide-details label="Habilitar" class="mt-0" />
        </div>
        <v-expand-transition>
          <div v-if="fiscal.habilitarNFSe" class="pa-4">
            <v-row dense>
              <v-col cols="12" sm="4">
                <v-text-field v-model="fiscal.inscricaoMunicipal" label="Inscrição Municipal *"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model="fiscal.codigoMunicipioIbge" label="Código Município IBGE"
                  variant="outlined" density="compact"
                  hint="7 dígitos" persistent-hint />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model.number="fiscal.serieNFSe" label="Série NFS-e"
                  type="number" variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="6">
                <v-select v-model="fiscal.regimeEspecialTributacao" label="Regime Especial de Tributação (ISS)"
                  :items="regimesEspeciaisIss" item-title="title" item-value="value"
                  clearable clear-icon="mdi-close-circle"
                  variant="outlined" density="compact"
                  hint="Opcional — omitido no XML se não selecionado" persistent-hint />
              </v-col>
              <v-col cols="12" sm="3">
                <v-text-field v-model="fiscal.codigoServicoMunicipalPadrao" label="Código de Serviço Padrão"
                  variant="outlined" density="compact"
                  hint="Código LC 116/03 do município" persistent-hint />
              </v-col>
              <v-col cols="12" sm="3">
                <v-text-field v-model.number="fiscal.aliquotaIssPadrao" label="Alíquota ISS (%)"
                  type="number" step="0.01" variant="outlined" density="compact" suffix="%" />
              </v-col>
              <v-col cols="12" sm="4">
                <v-switch v-model="fiscal.issRetidoFonte" color="primary"
                  label="ISS retido na fonte pelo tomador" density="compact" hide-details />
              </v-col>
              <v-col cols="12" sm="4">
                <v-switch v-model="fiscal.incentivadorCultural" color="primary"
                  label="Incentivador cultural" density="compact" hide-details />
              </v-col>
            </v-row>
          </div>
        </v-expand-transition>
      </v-card>

      <!-- ── MDF-e ── -->
      <v-card rounded="xl" elevation="1">
        <div class="cfg-secao-header">
          <div class="d-flex align-center gap-2">
            <v-icon icon="mdi-truck-outline" color="brown" size="20" />
            <span>MDF-e — Manifesto de Documentos Fiscais</span>
          </div>
          <v-switch v-model="fiscal.habilitarMDFe" color="primary" density="compact"
            hide-details label="Habilitar" class="mt-0" />
        </div>
        <v-expand-transition>
          <div v-if="fiscal.habilitarMDFe" class="pa-4">
            <v-row dense>
              <v-col cols="6" sm="2">
                <v-text-field v-model.number="fiscal.serieMDFe" label="Série MDF-e"
                  type="number" variant="outlined" density="compact" />
              </v-col>
              <v-col cols="6" sm="3">
                <v-text-field v-model.number="fiscal.proximoNumeroMDFe" label="Próximo número"
                  type="number" variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="4">
                <v-select v-model="fiscal.tipoEmitenteMDFe" label="Tipo Emitente (tpEmit)"
                  :items="[
                    {title:'1 — PST — Prestador de Serviço de Transporte',value:'1'},
                    {title:'2 — TCP — Transportador de Cargas Próprias',value:'2'},
                  ]"
                  variant="outlined" density="compact"
                  hint="PST = empresa de transporte; TCP = carga própria" persistent-hint />
              </v-col>
              <v-col cols="12" sm="4">
                <v-select v-model="fiscal.modalTransporteMDFe" label="Modal de Transporte (tpTransp)"
                  :items="[
                    {title:'1 — Rodoviário',value:'1'},
                    {title:'2 — Aéreo',value:'2'},
                    {title:'3 — Aquaviário',value:'3'},
                    {title:'4 — Ferroviário',value:'4'},
                  ]"
                  variant="outlined" density="compact" />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field v-model="fiscal.rntrc" label="RNTRC"
                  variant="outlined" density="compact"
                  hint="Registro Nacional de Transportadores" persistent-hint />
              </v-col>
            </v-row>
          </div>
        </v-expand-transition>
      </v-card>

      <!-- Botão salvar global -->
      <div class="d-flex justify-end">
        <v-btn color="primary" size="large" rounded="lg" prepend-icon="mdi-content-save-outline"
          :loading="salvando" @click="salvarFiscal">
          Salvar Configurações Fiscais
        </v-btn>
      </div>
    </div>

    <!-- Colaboradores -->
    <div v-if="aba==='usuarios'">
      <div class="d-flex justify-space-between align-center mb-3">
        <span class="text-body-2 text-medium-emphasis">{{ usuarios.length }} colaborador(es) cadastrado(s)</span>
        <v-btn color="primary" rounded="lg" prepend-icon="mdi-account-plus" @click="abrirNovoColaborador">
          Novo Colaborador
        </v-btn>
      </div>
      <v-card rounded="xl" elevation="1">
        <v-data-table :headers="headersUsr" :items="usuarios" :loading="carregandoUsr" density="comfortable"
          no-data-text="Nenhum colaborador cadastrado">
          <template #item.perfil="{ item }">
            <v-chip size="small" :color="corPerfil(item.perfil)" variant="tonal" class="font-weight-medium">
              <v-icon start size="14">{{ iconePerfil(item.perfil) }}</v-icon>
              {{ item.perfil }}
            </v-chip>
          </template>
          <template #item.ativo="{ item }">
            <v-chip :color="item.ativo ? 'success' : 'error'" size="small" variant="tonal">
              {{ item.ativo ? 'Ativo' : 'Inativo' }}
            </v-chip>
          </template>
          <template #item.ultimoAcesso="{ item }">
            <span class="text-caption text-medium-emphasis">
              {{ item.ultimoAcesso ? new Date(item.ultimoAcesso).toLocaleString('pt-BR') : 'Nunca' }}
            </span>
          </template>
          <template #item.acoes="{ item }">
            <v-menu>
              <template #activator="{ props }">
                <v-btn v-bind="props" icon="mdi-dots-vertical" size="small" variant="text" />
              </template>
              <v-list density="compact" min-width="200">
                <v-list-item prepend-icon="mdi-shield-account" title="Alterar perfil"
                  @click="abrirAlterarPerfil(item)" />
                <v-list-item prepend-icon="mdi-lock-reset" title="Redefinir senha"
                  @click="abrirRedefinirSenha(item)" />
                <v-divider />
                <v-list-item v-if="item.ativo" prepend-icon="mdi-account-off" title="Desativar"
                  @click="toggleAtivo(item)" class="text-error" />
                <v-list-item v-else prepend-icon="mdi-account-check" title="Reativar"
                  @click="toggleAtivo(item)" class="text-success" />
              </v-list>
            </v-menu>
          </template>
        </v-data-table>
      </v-card>
    </div>

    <!-- Dialog: Novo Colaborador -->
    <v-dialog v-model="dlgNovo" max-width="480" persistent>
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="primary">mdi-account-plus</v-icon>
          Novo Colaborador
        </v-card-title>
        <v-card-text>
          <v-form ref="formNovo" @submit.prevent="salvarNovoColaborador">
            <v-row dense>
              <v-col cols="12">
                <v-text-field v-model="novoUsr.nome" label="Nome completo *"
                  variant="outlined" density="compact" :rules="[r=>!!r||'Obrigatório']" />
              </v-col>
              <v-col cols="12">
                <v-text-field v-model="novoUsr.email" label="E-mail (login) *"
                  type="email" variant="outlined" density="compact"
                  :rules="[r=>!!r||'Obrigatório', r=>/.+@.+\..+/.test(r)||'E-mail inválido']" />
              </v-col>
              <v-col cols="12">
                <v-select v-model="novoUsr.perfil" label="Nível de acesso *"
                  :items="perfisDisponiveis" variant="outlined" density="compact"
                  :rules="[r=>!!r||'Obrigatório']">
                  <template #item="{ item, props }">
                    <v-list-item v-bind="props">
                      <template #prepend>
                        <v-icon :color="corPerfil(item.value)" size="18" class="mr-2">
                          {{ iconePerfil(item.value) }}
                        </v-icon>
                      </template>
                      <template #subtitle>{{ descricaoPerfil(item.value) }}</template>
                    </v-list-item>
                  </template>
                </v-select>
              </v-col>
              <v-col cols="12">
                <v-text-field v-model="novoUsr.senha" label="Senha temporária *"
                  :type="mostrarSenhaNovo ? 'text' : 'password'"
                  :append-inner-icon="mostrarSenhaNovo ? 'mdi-eye-off' : 'mdi-eye'"
                  @click:append-inner="mostrarSenhaNovo = !mostrarSenhaNovo"
                  variant="outlined" density="compact"
                  :rules="[r=>!!r||'Obrigatório', r=>r.length>=6||'Mínimo 6 caracteres']"
                  hint="O colaborador poderá alterar após o primeiro acesso" persistent-hint />
              </v-col>
            </v-row>
          </v-form>
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgNovo=false" :disabled="salvandoUsr">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvandoUsr" @click="salvarNovoColaborador">
            Cadastrar
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Alterar Perfil -->
    <v-dialog v-model="dlgPerfil" max-width="400">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="primary">mdi-shield-account</v-icon>
          Alterar Nível de Acesso
        </v-card-title>
        <v-card-text>
          <div class="text-body-2 mb-3">
            Colaborador: <strong>{{ usrSelecionado?.nome }}</strong>
          </div>
          <v-select v-model="novoPerfil" label="Novo nível de acesso"
            :items="perfisDisponiveis" variant="outlined" density="compact" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgPerfil=false">Cancelar</v-btn>
          <v-btn color="primary" rounded="lg" :loading="salvandoUsr" @click="salvarPerfil">Salvar</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Dialog: Redefinir Senha -->
    <v-dialog v-model="dlgSenha" max-width="400">
      <v-card rounded="xl">
        <v-card-title class="pa-4 pb-0">
          <v-icon start color="warning">mdi-lock-reset</v-icon>
          Redefinir Senha
        </v-card-title>
        <v-card-text>
          <div class="text-body-2 mb-3">
            Colaborador: <strong>{{ usrSelecionado?.nome }}</strong>
          </div>
          <v-text-field v-model="novaSenha" label="Nova senha *"
            :type="mostrarSenhaReset ? 'text' : 'password'"
            :append-inner-icon="mostrarSenhaReset ? 'mdi-eye-off' : 'mdi-eye'"
            @click:append-inner="mostrarSenhaReset = !mostrarSenhaReset"
            variant="outlined" density="compact"
            :rules="[r=>!!r||'Obrigatório', r=>r.length>=6||'Mínimo 6 caracteres']" />
        </v-card-text>
        <v-card-actions class="pa-4 pt-0">
          <v-spacer />
          <v-btn variant="text" @click="dlgSenha=false">Cancelar</v-btn>
          <v-btn color="warning" rounded="lg" :loading="salvandoUsr" @click="salvarSenha">Redefinir</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import GuiaPassos from '@/components/GuiaPassos.vue'
import api from '@/composables/useApi'
import { useAuthStore } from '@/stores/auth'
import { useNotifStore } from '@/stores/notif'

const auth = useAuthStore()
const notif = useNotifStore()
const aba = ref('empresa')
const salvando = ref(false)

// ── Certificado digital ──────────────────────────────────────────
const inputCert = ref<HTMLInputElement>()
const certFile = ref<File | null>(null)
const certSenha = ref('')
const mostrarSenhaCert = ref(false)
const arrastando = ref(false)
const validandoCert = ref(false)
const salvandoCert = ref(false)
const certValidacao = ref<any>(null)
const certAtual = ref<any>(null)

const fmtData = (v?: string) =>
  v ? new Date(v).toLocaleDateString('pt-BR') : '—'

function onFilePick(e: Event) {
  const f = (e.target as HTMLInputElement).files?.[0]
  if (f) { certFile.value = f; certValidacao.value = null }
}

function onDrop(e: DragEvent) {
  arrastando.value = false
  const f = e.dataTransfer?.files?.[0]
  if (f && (f.name.endsWith('.pfx') || f.name.endsWith('.p12'))) {
    certFile.value = f
    certValidacao.value = null
  } else {
    notif.aviso('Selecione um arquivo .pfx ou .p12')
  }
}

async function validarCertificado() {
  if (!certFile.value || !certSenha.value) return
  validandoCert.value = true
  certValidacao.value = null
  try {
    const fd = new FormData()
    fd.append('arquivo', certFile.value)
    fd.append('senha', certSenha.value)
    fd.append('empresaId', auth.empresaId ?? '')
    const r = await api.post('/fiscal/certificado/validar', fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    certValidacao.value = r.data
  } catch (e: any) {
    certValidacao.value = {
      valido: false,
      erro: e?.response?.data?.title ?? e?.response?.data?.mensagem ?? 'Senha incorreta ou arquivo corrompido.',
    }
  } finally {
    validandoCert.value = false
  }
}

async function salvarCertificado() {
  if (!certFile.value || !certSenha.value) return
  salvandoCert.value = true
  try {
    const fd = new FormData()
    fd.append('arquivo', certFile.value)
    fd.append('senha', certSenha.value)
    fd.append('empresaId', auth.empresaId ?? '')
    await api.post('/fiscal/certificado', fd, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    notif.ok('Certificado instalado com sucesso!')
    certFile.value = null
    certSenha.value = ''
    certValidacao.value = null
    await carregarCertificado()
  } catch (e: any) {
    notif.erro(e?.response?.data?.title ?? 'Erro ao instalar certificado.')
  } finally {
    salvandoCert.value = false
  }
}

async function carregarCertificado() {
  try {
    const r = await api.get('/fiscal/certificado/status', { params: { empresaId: auth.empresaId } })
    certAtual.value = r.data ?? null
  } catch {
    certAtual.value = null
  }
}
const buscandoCep = ref(false)
const carregandoUsr = ref(false)
const salvandoUsr = ref(false)
const formEmpresa = ref()
const formNovo = ref()

const emp = ref<any>({})
// Defaults de todos os campos — usados para mesclar com o retorno da API
const fiscalDefaults = {
  regime: 'SimplesNacional', ambiente: 'Homologacao', contingenciaPadrao: 'SVC_AN',
  naturezaOperacaoPadrao: 'VENDA DE MERCADORIAS',
  // NF-e
  serieNFe: 1, proximoNumeroNFe: 1, formatoDanfe: 'Retrato',
  enviarEmailDestinatario: true, emailCopiaFixa: '',
  // NFC-e
  serieNFCe: 1, proximoNumeroNFCe: 1, cscIdNFCe: '', cscTokenNFCe: '',
  tipoImpressaoNFCe: 'Termica80', imprimirAutomaticamenteNFCe: true,
  // Tributação padrão
  csosnPadrao: '400', cstIcmsPadrao: '00', aliquotaIcmsPadrao: 0, aliquotaIcmsInterestadual: 0,
  origemPadrao: '0',
  cstPisPadrao: '07', aliquotaPisPadrao: 0,
  cstCofinsPadrao: '07', aliquotaCofinsPadrao: 0,
  cfopVendaEstadual: '5102', cfopVendaInterestadual: '6102', cfopVendaConsumidor: '5102',
  // NFS-e
  habilitarNFSe: false, inscricaoMunicipal: '', codigoMunicipioIbge: '', serieNFSe: 1,
  regimeEspecialTributacao: '' as string, codigoServicoMunicipalPadrao: '', aliquotaIssPadrao: 0,
  issRetidoFonte: false, incentivadorCultural: false,
  // MDF-e
  habilitarMDFe: false, serieMDFe: 1, proximoNumeroMDFe: 1,
  tipoEmitenteMDFe: '2', modalTransporteMDFe: '1', rntrc: '',
}

const fiscal = ref<any>({ ...fiscalDefaults })

// Defaults de PIS/COFINS por regime — só aplicados quando não há dados salvos
const pisCofinsDefaultsPorRegime: Record<string, { cst: string; pis: number; cofins: number }> = {
  SimplesNacional: { cst: '07', pis: 0,    cofins: 0    },  // incluso no DAS
  LucroPresumido:  { cst: '01', pis: 0.65, cofins: 3.00 },  // regime cumulativo
  LucroReal:       { cst: '01', pis: 1.65, cofins: 7.60 },  // regime não-cumulativo
}

let carregandoFiscalDaApi = false

const icmsDefaultsPorRegime: Record<string, { csosn?: string; cst?: string; aliq: number }> = {
  SimplesNacional: { csosn: '400', cst: undefined, aliq: 0    },
  LucroPresumido:  { csosn: undefined, cst: '000', aliq: 12   },
  LucroReal:       { csosn: undefined, cst: '000', aliq: 12   },
}

// Preenche tributação padrão ao trocar regime (apenas se usuário não customizou)
watch(() => fiscal.value.regime, (novoRegime) => {
  if (carregandoFiscalDaApi) return

  // PIS/COFINS
  const d = pisCofinsDefaultsPorRegime[novoRegime]
  if (d) {
    const eraDefault = Object.values(pisCofinsDefaultsPorRegime).some(
      pd => pd.cst === fiscal.value.cstPisPadrao && pd.pis === fiscal.value.aliquotaPisPadrao
    )
    if (eraDefault) {
      fiscal.value.cstPisPadrao         = d.cst
      fiscal.value.aliquotaPisPadrao    = d.pis
      fiscal.value.cstCofinsPadrao      = d.cst
      fiscal.value.aliquotaCofinsPadrao = d.cofins
    }
  }

  // ICMS / CSOSN
  const ic = icmsDefaultsPorRegime[novoRegime]
  if (ic) {
    const eraDefaultIcms = Object.values(icmsDefaultsPorRegime).some(
      pd => pd.aliq === fiscal.value.aliquotaIcmsPadrao
    )
    if (eraDefaultIcms) {
      fiscal.value.csosnPadrao        = ic.csosn ?? null
      fiscal.value.cstIcmsPadrao      = ic.cst   ?? null
      fiscal.value.aliquotaIcmsPadrao = ic.aliq
    }
  }
})

// ── Tabelas fiscais ─────────────────────────────────────────────
const formasEmissao = [
  { value: 'SVC_AN', title: '6 — SVC-AN — SEFAZ Virtual Nacional (recomendado para todos os estados)' },
  { value: 'SVC_RS', title: '7 — SVC-RS — SEFAZ Virtual RS (apenas SP, RS e MG)' },
  { value: 'Offline', title: '9 — Offline — Somente NFC-e, sem conexão, imprime localmente' },
]
const csosnOpcoes = [
  { value: '101', title: '101 — Tributada pelo SN com permissão de crédito' },
  { value: '102', title: '102 — Tributada pelo SN sem permissão de crédito' },
  { value: '103', title: '103 — Isenção do ICMS para faixa de receita bruta' },
  { value: '201', title: '201 — Tributada pelo SN com crédito e com ST' },
  { value: '202', title: '202 — Tributada pelo SN sem crédito e com ST' },
  { value: '203', title: '203 — Isenção do ICMS para faixa de receita bruta e com ST' },
  { value: '300', title: '300 — Imune de ICMS' },
  { value: '400', title: '400 — Não tributada pelo Simples Nacional' },
  { value: '500', title: '500 — ICMS cobrado anteriormente por ST ou antecipação' },
  { value: '900', title: '900 — Outros' },
]
const cstIcmsOpcoes = [
  { value: '00', title: '00 — Tributada integralmente' },
  { value: '10', title: '10 — Tributada com ST' },
  { value: '20', title: '20 — Com redução de BC' },
  { value: '30', title: '30 — Isenta com ST' },
  { value: '40', title: '40 — Isenta' },
  { value: '41', title: '41 — Não tributada' },
  { value: '50', title: '50 — Suspensão' },
  { value: '51', title: '51 — Diferimento' },
  { value: '60', title: '60 — ICMS cobrado por ST anteriormente' },
  { value: '70', title: '70 — Redução de BC e ST' },
  { value: '90', title: '90 — Outros' },
]
const origensOpcoes = [
  { value: '0', title: '0 — Nacional (exceto indicados nos outros códigos)' },
  { value: '1', title: '1 — Estrangeira — Importação direta' },
  { value: '2', title: '2 — Estrangeira — Adquirida no mercado interno' },
  { value: '3', title: '3 — Nacional com conteúdo de importação > 40%' },
  { value: '4', title: '4 — Nacional — Processo produtivo básico' },
  { value: '5', title: '5 — Nacional com conteúdo de importação ≤ 40%' },
  { value: '6', title: '6 — Estrangeira — Importação direta sem similar nacional' },
  { value: '7', title: '7 — Estrangeira — Mercado interno sem similar nacional' },
  { value: '8', title: '8 — Nacional — Conteúdo de importação > 70%' },
]
const cstPisCofinsOpcoes = [
  { value: '01', title: '01 — Operação Tributável — BC = Valor da Operação' },
  { value: '02', title: '02 — Operação Tributável — BC = Valor da Oper. (alíq. diferenciada)' },
  { value: '03', title: '03 — Operação Tributável — BC = Qtd. Vendida × Alíq. por Unidade' },
  { value: '04', title: '04 — Operação Tributável — Monofásica (Alíquota Zero)' },
  { value: '05', title: '05 — Operação Tributável — Substituição Tributária' },
  { value: '06', title: '06 — Operação Tributável — Alíquota Zero' },
  { value: '07', title: '07 — Operação Isenta da Contribuição' },
  { value: '08', title: '08 — Operação sem Incidência da Contribuição' },
  { value: '09', title: '09 — Operação com Suspensão da Contribuição' },
  { value: '49', title: '49 — Outras Operações de Saída' },
  { value: '99', title: '99 — Outras Operações' },
]
const regimesEspeciaisIss = [
  { value: '1', title: '1 — Microempresa Municipal' },
  { value: '2', title: '2 — Estimativa' },
  { value: '3', title: '3 — Sociedade de Profissionais' },
  { value: '4', title: '4 — Cooperativa' },
  { value: '5', title: '5 — Microempresário Individual (MEI)' },
  { value: '6', title: '6 — Microempresário/Empresa de Pequeno Porte (ME/EPP)' },
]
const usuarios = ref<any[]>([])

// Dialogs
const dlgNovo = ref(false)
const dlgPerfil = ref(false)
const dlgSenha = ref(false)
const usrSelecionado = ref<any>(null)
const novoPerfil = ref('')
const novaSenha = ref('')
const mostrarSenhaNovo = ref(false)
const mostrarSenhaReset = ref(false)

const novoUsr = ref({ nome: '', email: '', perfil: 'Vendedor', senha: '' })

const headersUsr = [
  { title: 'Nome', key: 'nome' },
  { title: 'E-mail', key: 'email' },
  { title: 'Nível de Acesso', key: 'perfil' },
  { title: 'Status', key: 'ativo' },
  { title: 'Último Acesso', key: 'ultimoAcesso' },
  { title: '', key: 'acoes', sortable: false, align: 'end' as const },
]

const perfisDisponiveis = [
  { title: 'Administrador', value: 'Administrador' },
  { title: 'Vendedor', value: 'Vendedor' },
  { title: 'Financeiro', value: 'Financeiro' },
  { title: 'Contador', value: 'Contador' },
]

function corPerfil(perfil: string) {
  const cores: Record<string, string> = {
    Administrador: 'error',
    Vendedor: 'primary',
    Financeiro: 'warning',
    Contador: 'info',
  }
  return cores[perfil] ?? 'default'
}

function iconePerfil(perfil: string) {
  const icones: Record<string, string> = {
    Administrador: 'mdi-shield-crown',
    Vendedor: 'mdi-cash-register',
    Financeiro: 'mdi-currency-usd',
    Contador: 'mdi-calculator',
  }
  return icones[perfil] ?? 'mdi-account'
}

function descricaoPerfil(perfil: string) {
  const desc: Record<string, string> = {
    Administrador: 'Acesso total ao sistema',
    Vendedor: 'PDV, estoque e clientes',
    Financeiro: 'Financeiro e relatórios',
    Contador: 'Fiscal, contabilidade e SPED',
  }
  return desc[perfil] ?? ''
}

function abrirNovoColaborador() {
  novoUsr.value = { nome: '', email: '', perfil: 'Vendedor', senha: '' }
  mostrarSenhaNovo.value = false
  dlgNovo.value = true
}

function abrirAlterarPerfil(item: any) {
  usrSelecionado.value = item
  novoPerfil.value = item.perfil
  dlgPerfil.value = true
}

function abrirRedefinirSenha(item: any) {
  usrSelecionado.value = item
  novaSenha.value = ''
  mostrarSenhaReset.value = false
  dlgSenha.value = true
}

async function carregarUsuarios() {
  carregandoUsr.value = true
  try {
    const r = await api.get('/usuarios', { params: { empresaId: auth.empresaId } })
    usuarios.value = r.data
  } catch { /* silencioso */ } finally {
    carregandoUsr.value = false
  }
}

async function salvarNovoColaborador() {
  const { valid } = await formNovo.value.validate()
  if (!valid) return
  salvandoUsr.value = true
  try {
    await api.post('/usuarios', {
      empresaId: auth.empresaId,
      ...novoUsr.value,
    })
    notif.ok(`Colaborador ${novoUsr.value.nome} cadastrado!`)
    dlgNovo.value = false
    await carregarUsuarios()
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? 'Erro ao cadastrar colaborador.')
  } finally {
    salvandoUsr.value = false
  }
}

async function salvarPerfil() {
  salvandoUsr.value = true
  try {
    await api.patch(`/usuarios/${usrSelecionado.value.id}/perfil`, { perfil: novoPerfil.value })
    notif.ok('Nível de acesso atualizado!')
    dlgPerfil.value = false
    await carregarUsuarios()
  } catch {
    notif.erro('Erro ao alterar perfil.')
  } finally {
    salvandoUsr.value = false
  }
}

async function salvarSenha() {
  if (!novaSenha.value || novaSenha.value.length < 6) {
    notif.erro('Senha deve ter no mínimo 6 caracteres.')
    return
  }
  salvandoUsr.value = true
  try {
    await api.patch(`/usuarios/${usrSelecionado.value.id}/senha`, { novaSenha: novaSenha.value })
    notif.ok('Senha redefinida com sucesso!')
    dlgSenha.value = false
  } catch {
    notif.erro('Erro ao redefinir senha.')
  } finally {
    salvandoUsr.value = false
  }
}

async function toggleAtivo(item: any) {
  try {
    if (item.ativo) {
      await api.patch(`/usuarios/${item.id}/desativar`)
      notif.ok(`${item.nome} desativado.`)
    } else {
      await api.patch(`/usuarios/${item.id}/reativar`)
      notif.ok(`${item.nome} reativado.`)
    }
    await carregarUsuarios()
  } catch {
    notif.erro('Erro ao alterar status do colaborador.')
  }
}

async function carregarEmpresa() {
  try { const r = await api.get(`/empresas/${auth.empresaId}`); emp.value = r.data } catch {}
}

async function carregarFiscal() {
  try {
    const r = await api.get('/fiscal/configuracao', { params: { empresaId: auth.empresaId } })
    if (r.data) {
      // Mescla com defaults para cobrir campos novos que a API ainda não retorna
      fiscal.value = { ...fiscalDefaults, ...r.data }
    } else {
      // Sem config fiscal ainda — pré-preenche o regime com o da empresa
      fiscal.value = {
        ...fiscalDefaults,
        regime: emp.value.regimeTributario ?? fiscalDefaults.regime,
      }
    }
  } catch {}
}

async function buscarCep() {
  const cep = emp.value.cep?.replace(/\D/g, '')
  if (cep?.length !== 8) return
  buscandoCep.value = true
  try {
    const r = await fetch(`https://viacep.com.br/ws/${cep}/json/`)
    const d = await r.json()
    if (!d.erro) {
      emp.value.logradouro = d.logradouro
      emp.value.bairro = d.bairro
      emp.value.cidade = d.localidade
      emp.value.uf = d.uf
    }
  } finally { buscandoCep.value = false }
}

async function salvarEmpresa() {
  salvando.value = true
  try { await api.put(`/empresas/${auth.empresaId}`, emp.value); notif.ok('Empresa atualizada!') }
  finally { salvando.value = false }
}

async function salvarFiscal() {
  salvando.value = true
  try {
    if (fiscal.value.id) {
      await api.put(`/fiscal/configuracao/${fiscal.value.id}`, fiscal.value)
    } else {
      const r = await api.post('/fiscal/configuracao', { ...fiscal.value, empresaId: auth.empresaId })
      // Guarda o id retornado para que próximos salvamentos usem PUT (evita erro "já existe")
      if (r.data?.id) fiscal.value.id = r.data.id
    }
    notif.ok('Configuração fiscal salva!')
  } catch (e: any) {
    notif.erro(e.response?.data?.mensagem ?? e.response?.data?.detalhe ?? 'Erro ao salvar configuração fiscal.')
  } finally { salvando.value = false }
}

// ── Tributação padrão em massa ───────────────────────────────────
const dialogTrib     = ref(false)
const carregandoTrib = ref(false)
const aplicandoTrib  = ref(false)
const tribPreview    = ref<any>(null)
const tribSobrazerosVazio = ref(true)

async function abrirDialogAplicarTrib() {
  if (!fiscal.value.id) {
    notif.aviso('Salve a configuração fiscal antes de aplicar tributação.')
    return
  }
  carregandoTrib.value = true
  try {
    const r = await api.get(`/fiscal/configuracao/${fiscal.value.id}/tributacao-padrao`)
    tribPreview.value = r.data
    dialogTrib.value  = true
  } catch { /* silencioso */ } finally { carregandoTrib.value = false }
}

async function aplicarTributacao() {
  aplicandoTrib.value = true
  try {
    const r = await api.post(
      `/fiscal/configuracao/${fiscal.value.id}/aplicar-tributacao-padrao`,
      null,
      { params: { apenasSeVazio: tribSobrazerosVazio.value } }
    )
    notif.ok(`Tributação aplicada em ${r.data.atualizados} produto(s).`)
    dialogTrib.value = false
  } catch { notif.erro('Erro ao aplicar tributação.') }
  finally { aplicandoTrib.value = false }
}

watch(aba, async (v) => {
  if (v === 'fiscal') {
    // Garante que os dados da empresa estejam carregados antes de pré-preencher o regime
    if (!emp.value.razaoSocial) await carregarEmpresa()
    await Promise.all([carregarFiscal(), carregarCertificado()])
  } else if (v === 'usuarios') {
    carregarUsuarios()
  }
})
onMounted(carregarEmpresa)
</script>

<style scoped>
.cfg-secao-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: #f8f9fb;
  border-bottom: 1px solid #e8edf3;
  border-radius: 12px 12px 0 0;
  font-size: 0.82rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.07em;
  color: rgb(var(--v-theme-primary));
}
.cert-upload-zone {
  border: 2px dashed #c8d4e0;
  border-radius: 12px;
  padding: 28px 16px;
  text-align: center;
  cursor: pointer;
  transition: border-color .2s, background .2s;
  background: #f8f9fb;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  user-select: none;
}
.cert-upload-zone:hover,
.cert-upload-zone--drag {
  border-color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.04);
}
.cert-upload-zone--filled {
  border-color: rgb(var(--v-theme-primary));
  border-style: solid;
  background: rgba(var(--v-theme-primary), 0.04);
}
.cert-info {
  background: #f0f7f0;
  border: 1px solid #c8e6c9;
  border-radius: 10px;
  padding: 14px;
}
.gap-4 { gap: 16px; }
.trib-grupo-titulo {
  font-size: 0.72rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: rgb(var(--v-theme-deep-purple));
  padding: 4px 0 2px;
  border-bottom: 1px solid #ede7f6;
  margin-bottom: 8px;
}
.normal-case { text-transform: none; letter-spacing: normal; font-size: 0.78rem; }
</style>
