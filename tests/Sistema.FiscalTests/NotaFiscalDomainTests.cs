using FluentAssertions;
using Sistema.Domain.Fiscal.Entities;

namespace Sistema.FiscalTests;

/// <summary>
/// Testes de domínio da NF-e — validam ciclo de vida, cálculo de totais e impostos.
/// Não requerem banco ou integração — apenas Domain puro.
/// </summary>
public class NotaFiscalDomainTests
{
    private static NotaFiscal CriarNfe(long numero = 1)
        => NotaFiscal.Criar(Guid.NewGuid(), ModeloNF.NFe, 1, numero,
            NaturezaOperacao.VendaProduto);

    private static ItemNotaFiscal CriarItem(Guid nfId, int numeroItem = 1,
        decimal qtd = 2, decimal preco = 100m)
        => ItemNotaFiscal.Criar(nfId, numeroItem,
            "VIT-C-001", "Vitamina C 1g 60 Cápsulas",
            cfop: "5102", unidade: "UN",
            quantidade: qtd, valorUnitario: preco);

    // ─── Ciclo de vida ───────────────────────────────────────────────────────

    [Fact]
    public void Criar_DeveInicializarComStatusEmDigitacao()
    {
        var nf = CriarNfe();
        nf.Status.Should().Be(StatusNF.EmDigitacao);
        nf.Modelo.Should().Be(ModeloNF.NFe);
        nf.Serie.Should().Be(1);
        nf.Numero.Should().Be(1);
    }

    [Fact]
    public void RegistrarTransmissao_DeveAlterarStatusParaTransmitindo()
    {
        var nf = CriarNfe();
        nf.RegistrarTransmissao("chave44digitos".PadRight(44, '0'), "<xml/>");
        nf.Status.Should().Be(StatusNF.Transmitindo);
        nf.ChaveAcesso.Should().NotBeNullOrEmpty();
        nf.XmlEnvio.Should().Be("<xml/>");
    }

    [Fact]
    public void RegistrarAutorizacao_DeveAlterarStatusParaAutorizada()
    {
        var nf = CriarNfe();
        nf.RegistrarTransmissao("chave".PadRight(44, '0'), "<envio/>");
        nf.RegistrarAutorizacao("protocolo123", "<retorno/>");
        nf.Status.Should().Be(StatusNF.Autorizada);
        nf.Protocolo.Should().Be("protocolo123");
        nf.XmlRetorno.Should().Be("<retorno/>");
    }

    [Fact]
    public void RegistrarRejeicao_DeveAlterarStatusParaRejeitada()
    {
        var nf = CriarNfe();
        nf.RegistrarTransmissao("chave".PadRight(44, '0'), "<envio/>");
        nf.RegistrarRejeicao("562 - Rejeição: Código NCM inválido", "<retorno/>");
        nf.Status.Should().Be(StatusNF.Rejeitada);
        nf.MotivoRejeicao.Should().Contain("NCM");
    }

    [Fact]
    public void Cancelar_NFAutorizada_DeveAlterarStatusParaCancelada()
    {
        var nf = CriarNfe();
        nf.RegistrarTransmissao("chave".PadRight(44, '0'), "<envio/>");
        nf.RegistrarAutorizacao("prot1", "<ret/>");
        nf.Cancelar("prot-cancel-001");
        nf.Status.Should().Be(StatusNF.Cancelada);
    }

    // ─── Itens e totais ──────────────────────────────────────────────────────

    [Fact]
    public void AdicionarItem_DeveRecalcularTotalDaNota()
    {
        var nf = CriarNfe();
        nf.AdicionarItem(CriarItem(nf.Id, 1, qtd: 2, preco: 100m));
        nf.TotalProdutos.Should().Be(200m);
        nf.TotalNota.Should().Be(200m);
    }

    [Fact]
    public void AdicionarMultiplosItens_DeveSomarTotais()
    {
        var nf = CriarNfe();
        nf.AdicionarItem(CriarItem(nf.Id, 1, qtd: 1, preco: 50m));
        nf.AdicionarItem(CriarItem(nf.Id, 2, qtd: 3, preco: 20m));
        nf.TotalProdutos.Should().Be(110m); // 50 + 60
        nf.Itens.Should().HaveCount(2);
    }

    [Fact]
    public void AdicionarItem_ComDesconto_DeveDescontarDoTotal()
    {
        var nf = CriarNfe();
        var item = ItemNotaFiscal.Criar(nf.Id, 1,
            "PROD", "Produto", "5102", "UN",
            quantidade: 1, valorUnitario: 100m, valorDesconto: 10m);
        nf.AdicionarItem(item);
        nf.TotalDesconto.Should().Be(10m);
        nf.TotalNota.Should().Be(90m);
    }

    [Fact]
    public void DefinirDestinatario_DevePreencherDados()
    {
        var nf = CriarNfe();
        nf.DefinirDestinatario("123.456.789-09", "João da Silva", "joao@email.com");
        nf.CpfCnpjDestinatario.Should().Be("123.456.789-09");
        nf.NomeDestinatario.Should().Be("João da Silva");
        nf.EmailDestinatario.Should().Be("joao@email.com");
    }

    // ─── Cálculo de impostos ─────────────────────────────────────────────────

    [Fact]
    public void CalcularImpostos_ICMS12_DeveCalcularValorCorreto()
    {
        var nf = CriarNfe();
        var item = CriarItem(nf.Id, qtd: 1, preco: 1000m);
        item.CalcularImpostos("00", null, aliqIcms: 12m, "07", aliqPis: 0.65m, aliqCofins: 3m);

        item.ValorIcms.Should().Be(120m);           // 1000 * 12%
        item.ValorPis.Should().Be(6.50m);           // 1000 * 0,65%
        item.ValorCofins.Should().Be(30m);          // 1000 * 3%
    }

    [Fact]
    public void CalcularImpostos_Simples_AliquotaZero_NaoDeveGerarTributo()
    {
        var nf = CriarNfe();
        var item = CriarItem(nf.Id, qtd: 5, preco: 50m); // total = 250
        item.CalcularImpostos(null, "400", aliqIcms: 0m, null, aliqPis: 0m, aliqCofins: 0m);

        item.ValorIcms.Should().Be(0m);
        item.ValorPis.Should().Be(0m);
        item.ValorCofins.Should().Be(0m);
        item.CsosnIcms.Should().Be("400"); // simples nacional isento
    }

    [Fact]
    public void CalcularImpostos_ICMS18_BaseEValor_DeveEstarCorretos()
    {
        var nf = CriarNfe();
        var item = CriarItem(nf.Id, qtd: 2, preco: 500m); // total = 1000
        item.CalcularImpostos("00", null, aliqIcms: 18m, "07", aliqPis: 0.65m, aliqCofins: 3m);

        item.BaseIcms.Should().Be(1000m);
        item.ValorIcms.Should().Be(180m);           // 1000 * 18%
        item.AliquotaIcms.Should().Be(18m);
    }

    [Fact]
    public void Item_ValorTotal_DeveSerQuantidadeVezesPrecoMenosDesconto()
    {
        var nf = CriarNfe();
        var item = ItemNotaFiscal.Criar(nf.Id, 1,
            "SKU", "Produto", "5102", "CX",
            quantidade: 3, valorUnitario: 40m, valorDesconto: 5m);

        item.ValorTotal.Should().Be(115m); // 3*40 - 5
    }

    // ─── NFC-e (modelo 65) ───────────────────────────────────────────────────

    [Fact]
    public void NfCe_Criar_DeveUsarModelo65()
    {
        var nf = NotaFiscal.Criar(Guid.NewGuid(), ModeloNF.NFCe, 1, 100,
            NaturezaOperacao.VendaConsumidor);
        nf.Modelo.Should().Be(ModeloNF.NFCe);
        nf.NaturezaOperacao.Should().Be(NaturezaOperacao.VendaConsumidor);
    }
}
