using FluentAssertions;
using Sistema.Domain.Cadastros.Entities;
using Xunit;

namespace Sistema.IntegrationTests.Cadastros;

public class ClienteNomeTests
{
    [Theory]
    [InlineData("ANA TEIXEIRA RAMALHO", "Ana Teixeira Ramalho")]
    [InlineData("sandra Regina pereira gomes", "Sandra Regina Pereira Gomes")]
    [InlineData("maria DE souza", "Maria de Souza")]
    [InlineData("joão da silva e santos", "João da Silva e Santos")]
    [InlineData("JOSÉ DOS SANTOS", "José dos Santos")]
    [InlineData("  ana   paula  ", "Ana Paula")]
    [InlineData("maria-clara d'avila", "Maria-Clara D'Avila")]
    [InlineData("da vinci", "Da Vinci")] // conector como 1ª palavra continua maiúsculo
    public void NormalizarNome_AplicaRegraDeNome(string entrada, string esperado)
        => Cliente.NormalizarNome(entrada).Should().Be(esperado);

    [Fact]
    public void Criar_NormalizaONome()
        => Cliente.Criar(System.Guid.NewGuid(), "PEDRO da SILVA", TipoPessoa.Fisica)
            .Nome.Should().Be("Pedro da Silva");
}
