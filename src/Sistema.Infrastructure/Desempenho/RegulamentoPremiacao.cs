using System.Globalization;
using System.Text;

namespace Sistema.Infrastructure.Desempenho;

/// <summary>
/// Fonte única do texto do Regulamento de Premiação por Desempenho (Art. 457, §2º, CLT).
/// Preenche os valores (metas e valor base) com as metas estabelecidas por loja/competência.
/// O mesmo texto é exibido na tela de aceite, tem o hash calculado e é reproduzido no comprovante.
/// </summary>
public static class RegulamentoPremiacao
{
    public const string Versao = "2.0";

    private static readonly CultureInfo PtBr = new("pt-BR");
    private static string M(decimal v) => "R$ " + v.ToString("N2", PtBr);

    /// <param name="valorBaseReduzido">Valor do prêmio na faixa de 90% a 99% (redutor aplicado sobre o valor base).</param>
    public static string Gerar(
        string empresaRazao, string lojaNome, int ano, int mes,
        decimal metaLoja, decimal metaIndividual, decimal valorBase, decimal valorBaseReduzido,
        decimal minPresenca, decimal thresholdLoja, decimal thresholdIndividual, decimal redutorPercent)
    {
        var razao = string.IsNullOrWhiteSpace(empresaRazao) ? "ECOGRANEL COMERCIO DE PRODUTOS NATURAIS LTDA" : empresaRazao.ToUpperInvariant();
        var comp = new DateTime(ano, mes, 1).ToString("MMMM 'de' yyyy", PtBr);
        var tLoja = thresholdLoja.ToString("0.##", PtBr);
        var tInd = thresholdIndividual.ToString("0.##", PtBr);
        var red = redutorPercent.ToString("0.##", PtBr);
        var pres = minPresenca.ToString("0.##", PtBr);

        var sb = new StringBuilder();
        void P(string s) => sb.AppendLine(s).AppendLine();

        P("TERMO DE REGULAMENTO DE PREMIAÇÃO POR DESEMPENHO");

        P($"Com fulcro no Artigo 457, §2º, da Consolidação das Leis do Trabalho (CLT), conforme a redação dada pela Lei nº 13.467/2017 (Reforma Trabalhista), a {razao} estabelece o presente Regulamento de Premiação, com o objetivo de incentivar e reconhecer o desempenho superior de seus colaboradores.");

        P($"Loja: {lojaNome}  ·  Competência de referência dos valores: {comp}.");

        P("CLÁUSULA PRIMEIRA: DA NATUREZA E BASE LEGAL");
        P("1.1. O pagamento efetuado sob a égide deste Regulamento configura-se como Prêmio, nos termos do Artigo 457, §2º, da CLT, sendo uma liberalidade concedida em decorrência de desempenho superior ao ordinariamente esperado.");
        P("1.2. O valor da premiação não integrará a remuneração do colaborador, não se incorporando ao contrato de trabalho e não constituindo base de incidência de qualquer encargo trabalhista ou previdenciário.");
        P("1.3. A premiação será paga, exclusivamente, mediante o cumprimento cumulativo dos critérios de elegibilidade, ativação coletiva (Loja) e ativação individual (Resultado de Venda), seguido da apuração da Performance Comercial, conforme detalhado nas cláusulas seguintes.");
        P("1.4. Período de Apuração e Pagamento: O desempenho será apurado mensalmente, do primeiro ao último dia do mês. O pagamento do Prêmio, quando devido, será efetuado até o 28º dia útil do mês subsequente ao da apuração.");
        P($"1.5. Desempenho Ordinariamente Esperado: Para fins deste Regulamento, considera-se desempenho ordinariamente esperado o cumprimento integral da jornada de trabalho e de todas as rotinas operacionais básicas, bem como o atingimento de metas de vendas inferiores a {tInd}% do estipulado na Cláusula Quarta. O Prêmio visa reconhecer apenas o desempenho superior, caracterizado pelo atingimento da ativação coletiva e individual de {tLoja}% ou mais, somado à Performance Comercial de alta qualidade.");

        P("CLÁUSULA SEGUNDA: DOS CRITÉRIOS DE ELEGIBILIDADE");
        P("A elegibilidade à premiação é a condição inicial obrigatória, devendo o colaborador atender integralmente aos critérios abaixo, no período de apuração (mensal):");
        P($"• Presença: Cumprir no mínimo {pres}% da jornada de trabalho no mês. Consideram-se dias de presença efetiva apenas os dias trabalhados conforme a escala.");
        P("• Faltas: Não possuir faltas injustificadas no período. Qualquer falta sem justificativa válida elimina a elegibilidade.");
        P("• Conduta: Não possuir advertência formal registrada no período de apuração, incluindo advertências por comportamento, descumprimento de processos ou postura inadequada.");
        P("• Execução Mínima Obrigatória: Cumprir de forma consistente as rotinas básicas da operação, sendo critérios mínimos esperados:");
        P("   – Abastecimento: Reposição dos principais produtos realizada conforme rotina, não deixando produtos críticos em ruptura por negligência.");
        P("   – Limpeza: Manter a área de trabalho, granel e utensílios em condições adequadas, sem acúmulo de resíduos ou desorganização evidente.");
        P("   – Validade: Realizar conferência de validade conforme rotina definida, não mantendo produto vencido exposto em hipótese alguma.");
        P("Regra Geral: Caso qualquer um dos critérios de elegibilidade acima não seja cumprido, o colaborador não participará do cálculo do prêmio, independentemente dos resultados de venda ou performance.");

        P("CLÁUSULA TERCEIRA: DA ATIVAÇÃO COLETIVA (LOJA) – CRITÉRIO CHAVE DE ENTRADA E VALOR BASE");
        P($"3.1. O Prêmio somente será ativado se a Loja atingir o desempenho mínimo esperado de faturamento coletivo no período de apuração.");
        P($"3.2. Meta da Loja: {M(metaLoja)} por mês.");
        P($"3.3. Valor Base do Prêmio: O valor integral do prêmio individual, antes de qualquer apuração de performance, é fixado em {M(valorBase)} por colaborador.");
        P("3.4. Regras de Ativação:");
        P($"• Abaixo de {tLoja}% da meta: Não haverá pagamento de prêmio para nenhum colaborador.");
        P($"• Entre {tLoja}% e 99% da meta: O prêmio será ativado com redutor sobre o valor base. O valor máximo a ser apurado individualmente será de {M(valorBaseReduzido)} ({red}% de {M(valorBase)}).");
        P($"• A partir de 100% da meta: O prêmio será ativado integralmente no valor base de {M(valorBase)}.");

        P("CLÁUSULA QUARTA: DA ATIVAÇÃO INDIVIDUAL (RESULTADO DE VENDA) – CRITÉRIO DE ENTRADA");
        P("4.1. Após a ativação da meta coletiva da Loja (Cláusula Terceira), cada colaborador será avaliado pela sua meta individual de faturamento.");
        P("4.2. O Resultado de Venda individual é o critério de entrada para o cálculo do prêmio. Se o colaborador não atingir o mínimo de desempenho em venda, as demais dimensões de performance não serão consideradas para pagamento.");
        P($"4.3. Meta Individual: {M(metaIndividual)}.");
        P("4.4. Regras de Participação Individual:");
        P($"• Abaixo de {tInd}% da meta individual: Não participa do cálculo do prêmio.");
        P($"• Entre {tInd}% e 99% da meta individual: Participa de forma proporcional ao percentual de atingimento da meta individual, ajustando o valor base do prêmio ({M(valorBase)} ou {M(valorBaseReduzido)}, conforme Cláusula Terceira).");
        P($"• A partir de 100% da meta individual: Participa integralmente do valor base do prêmio ({M(valorBase)} ou {M(valorBaseReduzido)}, conforme Cláusula Terceira).");

        P("CLÁUSULA QUINTA: DO CÁLCULO DO PRÊMIO E DA PERFORMANCE COMERCIAL");
        P("5.1. O prêmio é calculado com base no indicador Performance Comercial, que consolida o nível de execução operacional e qualidade de cada colaborador.");
        P("5.2. O indicador é avaliado semanalmente por colaborador e possui uma pontuação total de 100 pontos.");
        P("5.3. A pontuação final obtida na Performance Comercial (em percentual) será aplicada sobre o valor do prêmio individual que foi ativado (conforme Cláusulas Terceira e Quarta).");
        P("5.4. Estrutura de Avaliação (100 Pontos):");
        P("• 1. PROCESSO DE VENDA — 40 pontos: Abordagem (8 pts), Diagnóstico (8 pts), Conexão com Produto (8 pts), Sugestão Complementar (8 pts), Fechamento (8 pts).");
        P("• 2. EXECUÇÃO OPERACIONAL — 30 pontos: Abastecimento (10 pts), Organização (10 pts), Rotina (10 pts).");
        P("• 3. QUALIDADE DA OPERAÇÃO — 30 pontos: Validade (10 pts), Perdas (10 pts), Armazenamento e Integridade (10 pts).");
        P("5.5. Critérios de Pontuação por Item:");
        P("• Realizado com consistência: 100% da pontuação do item.");
        P("• Realizado parcialmente: 50% da pontuação do item.");
        P("• Não realizado: 0% da pontuação do item.");

        P("CLÁUSULA SEXTA: REGRAS DE CORTE (ZERO AUTOMÁTICO)");
        P("A ocorrência de qualquer uma das situações abaixo resultará no zeramento automático do prêmio do colaborador no período de apuração, independentemente de sua pontuação na Performance Comercial e do atingimento da meta de vendas:");
        P("• Produto vencido exposto.");
        P("• Falta injustificada.");
        P("• Falha grave de higiene.");
        P("• Não execução de rotina mínima.");
        P("• Reclamação relevante de cliente.");

        P("CLÁUSULA SÉTIMA: DISPOSIÇÕES FINAIS");
        P($"7.1. A interpretação e aplicação deste Regulamento são de responsabilidade exclusiva da {razao}.");
        P("7.2. A Loja reserva-se o direito de alterar, suspender ou revogar o presente Regulamento a qualquer momento, mediante comunicação prévia aos colaboradores. As metas por loja são dinâmicas e podem variar mês a mês conforme a base financeira de apuração.");
        P("7.3. O recebimento da premiação em um período não gera direito adquirido para recebimentos futuros, caracterizando-se como pagamento por desempenho pontual e excepcional.");
        P("7.4. Termo de Ciência e Aceite: O colaborador declara, por meio de documento à parte com sua assinatura (Termo de Aceite), ter recebido uma cópia deste Regulamento, estando ciente de todos os critérios de elegibilidade, ativação e cálculo, e concordando que o pagamento de Prêmio não possui natureza salarial, não se incorpora ao seu contrato de trabalho e está condicionado ao desempenho superior ao ordinariamente esperado.");
        P("7.5. Condição de Recebimento: O pagamento do Prêmio está condicionado a que o colaborador esteja com seu contrato de trabalho ativo e se encontre efetivamente trabalhando na data do pagamento (28º dia útil do mês subsequente à apuração), sendo vedado o pagamento a colaboradores que tenham seu contrato suspenso, rescindido ou estejam em aviso prévio (trabalhado ou indenizado) na data de efetivação do crédito.");

        P("CLÁUSULA OITAVA: DA PROTEÇÃO DE DADOS (LGPD – LEI Nº 13.709/2018)");
        P("8.1. Para comprovar a identidade e a autoria do aceite deste Regulamento, a empresa coleta e trata os seguintes dados pessoais do colaborador: foto de rosto (dado pessoal sensível de natureza biométrica), assinatura eletrônica, geolocalização, endereço IP e data/hora do aceite.");
        P("8.2. Base legal e finalidade: o tratamento tem por base o consentimento do titular (art. 7º, I, e, quanto ao dado biométrico, art. 11, I, da Lei nº 13.709/2018) e destina-se exclusivamente a registrar, autenticar e comprovar a adesão do colaborador à premiação por desempenho, não sendo os dados utilizados para qualquer outra finalidade nem compartilhados com terceiros, salvo por obrigação legal ou determinação judicial.");
        P("8.3. Retenção e eliminação: a foto de rosto (dado biométrico) é mantida pelo prazo de 24 (vinte e quatro) meses, contados do aceite, e após esse prazo é eliminada automaticamente. As demais evidências (assinatura, geolocalização, IP, data/hora e hash do documento) são retidas enquanto perdurar a finalidade probatória e pelos prazos legais aplicáveis.");
        P("8.4. Direitos do titular: o colaborador pode, a qualquer momento, solicitar à empresa a confirmação do tratamento, o acesso, a correção, a portabilidade, a eliminação dos dados tratados com base no consentimento e a revogação do consentimento, nos termos dos arts. 9º e 18 da LGPD, ciente de que a revogação pode inviabilizar a comprovação do aceite e, por consequência, a participação na premiação.");
        P("8.5. Ao assinar o Termo de Aceite, o colaborador declara ter lido e compreendido esta cláusula e consente livre, informada e inequivocamente com a coleta e o tratamento dos dados aqui descritos para a finalidade indicada.");

        P("Colaborador: ______________________________________________");

        return sb.ToString().TrimEnd() + "\n";
    }
}
