namespace Sistema.Domain.Shared.ValueObjects;

public sealed class Cnpj
{
    public string Numero { get; }

    private Cnpj(string numero) => Numero = numero;

    public static Result<Cnpj> Criar(string valor)
    {
        var digits = new string(valor.Where(char.IsDigit).ToArray());

        if (digits.Length != 14 || digits.Distinct().Count() == 1)
            return Result<Cnpj>.Failure("CNPJ inválido.");

        if (!ValidarDigitos(digits))
            return Result<Cnpj>.Failure("CNPJ inválido.");

        return Result<Cnpj>.Success(new Cnpj(digits));
    }

    public string Formatado => $"{Numero[..2]}.{Numero[2..5]}.{Numero[5..8]}/{Numero[8..12]}-{Numero[12..]}";

    private static bool ValidarDigitos(string d)
    {
        int[] p1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        int[] p2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

        int Calcular(string n, int[] pesos)
        {
            int soma = pesos.Select((p, i) => (n[i] - '0') * p).Sum();
            int r = soma % 11;
            return r < 2 ? 0 : 11 - r;
        }

        return Calcular(d, p1) == d[12] - '0' && Calcular(d, p2) == d[13] - '0';
    }

    public override string ToString() => Formatado;
}
