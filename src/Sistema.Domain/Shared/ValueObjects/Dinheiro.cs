namespace Sistema.Domain.Shared.ValueObjects;

public sealed class Dinheiro
{
    public decimal Valor { get; }

    private Dinheiro(decimal valor) => Valor = valor;

    public static Result<Dinheiro> Criar(decimal valor)
    {
        if (valor < 0)
            return Result<Dinheiro>.Failure("Valor monetário não pode ser negativo.");

        return Result<Dinheiro>.Success(new Dinheiro(Math.Round(valor, 2)));
    }

    public static Dinheiro Zero => new(0m);

    public Dinheiro Somar(Dinheiro outro) => new(Valor + outro.Valor);
    public Dinheiro Subtrair(Dinheiro outro) => new(Math.Max(0, Valor - outro.Valor));
    public Dinheiro Multiplicar(decimal fator) => new(Math.Round(Valor * fator, 2));
    public Dinheiro Porcentagem(decimal percentual) => new(Math.Round(Valor * percentual / 100m, 2));

    public static Dinheiro operator +(Dinheiro a, Dinheiro b) => a.Somar(b);
    public static Dinheiro operator -(Dinheiro a, Dinheiro b) => a.Subtrair(b);
    public static Dinheiro operator *(Dinheiro a, decimal f) => a.Multiplicar(f);
    public static bool operator >(Dinheiro a, Dinheiro b) => a.Valor > b.Valor;
    public static bool operator <(Dinheiro a, Dinheiro b) => a.Valor < b.Valor;
    public static bool operator >=(Dinheiro a, Dinheiro b) => a.Valor >= b.Valor;
    public static bool operator <=(Dinheiro a, Dinheiro b) => a.Valor <= b.Valor;

    public override string ToString() => Valor.ToString("C2", new System.Globalization.CultureInfo("pt-BR"));
}
