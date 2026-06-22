namespace Sistema.Domain.Shared.ValueObjects;

public sealed class Cpf
{
    public string Numero { get; }

    private Cpf(string numero) => Numero = numero;

    public static Result<Cpf> Criar(string valor)
    {
        var digits = new string(valor.Where(char.IsDigit).ToArray());

        if (digits.Length != 11 || digits.Distinct().Count() == 1)
            return Result<Cpf>.Failure("CPF inválido.");

        if (!ValidarDigitos(digits))
            return Result<Cpf>.Failure("CPF inválido.");

        return Result<Cpf>.Success(new Cpf(digits));
    }

    public string Formatado => $"{Numero[..3]}.{Numero[3..6]}.{Numero[6..9]}-{Numero[9..]}";

    private static bool ValidarDigitos(string d)
    {
        int soma = 0;
        for (int i = 0; i < 9; i++) soma += (d[i] - '0') * (10 - i);
        int r1 = soma % 11 < 2 ? 0 : 11 - soma % 11;
        if (r1 != d[9] - '0') return false;

        soma = 0;
        for (int i = 0; i < 10; i++) soma += (d[i] - '0') * (11 - i);
        int r2 = soma % 11 < 2 ? 0 : 11 - soma % 11;
        return r2 == d[10] - '0';
    }

    public override string ToString() => Formatado;
}
