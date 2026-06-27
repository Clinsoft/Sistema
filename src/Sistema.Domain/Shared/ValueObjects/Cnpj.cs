namespace Sistema.Domain.Shared.ValueObjects;

public sealed class Cnpj
{
    public string Numero { get; }

    private Cnpj(string numero) => Numero = numero;

    /// <summary>
    /// Aceita o formato alfanumérico da RF (vigente a partir de julho/2026):
    /// 12 caracteres alfanuméricos + 2 dígitos verificadores numéricos.
    /// CNPJs antigos (apenas dígitos) continuam válidos.
    /// </summary>
    public static Result<Cnpj> Criar(string valor)
    {
        // Mantém letras maiúsculas e dígitos; descarta pontuação
        var limpo = new string(valor.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        if (limpo.Length != 14)
            return Result<Cnpj>.Failure("CNPJ inválido — deve ter 14 caracteres alfanuméricos.");

        // Todos os caracteres iguais são inválidos
        if (limpo.Distinct().Count() == 1)
            return Result<Cnpj>.Failure("CNPJ inválido.");

        // Dígitos verificadores (posições 12-13) devem ser numéricos
        if (!char.IsDigit(limpo[12]) || !char.IsDigit(limpo[13]))
            return Result<Cnpj>.Failure("CNPJ inválido — dígitos verificadores devem ser numéricos.");

        if (!ValidarDigitos(limpo))
            return Result<Cnpj>.Failure("CNPJ inválido.");

        return Result<Cnpj>.Success(new Cnpj(limpo));
    }

    public string Formatado =>
        $"{Numero[..2]}.{Numero[2..5]}.{Numero[5..8]}/{Numero[8..12]}-{Numero[12..]}";

    // Algoritmo RF para CNPJ alfanumérico: converte cada caractere em valor numérico
    // Dígitos: valor = dígito. Letras: A=17, B=18, ..., Z=42 (RF 2026)
    private static int ValorCaractere(char c) =>
        char.IsDigit(c) ? c - '0' : c - 'A' + 17;

    private static bool ValidarDigitos(string d)
    {
        // Pesos para o 1º dígito verificador: posições 0–11
        int[] p1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        // Pesos para o 2º dígito verificador: posições 0–12
        int[] p2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        int Calcular(int[] pesos, int len)
        {
            int soma = pesos.Take(len).Select((p, i) => ValorCaractere(d[i]) * p).Sum();
            int r = soma % 11;
            return r < 2 ? 0 : 11 - r;
        }

        return Calcular(p1, 12) == d[12] - '0'
            && Calcular(p2, 13) == d[13] - '0';
    }

    public override string ToString() => Formatado;
}
