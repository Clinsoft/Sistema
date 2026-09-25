using Sistema.Domain.Assinaturas;

namespace Sistema.API.Auth;

/// <summary>
/// Exige que o plano da empresa inclua o recurso indicado. Aplicável no controller ou na action.
/// O <see cref="AssinaturaGateFilter"/> devolve 402 quando o plano não cobre.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequerRecursoAttribute(Recurso recurso) : Attribute
{
    public Recurso Recurso { get; } = recurso;
}

/// <summary>
/// Marca endpoints que continuam acessíveis mesmo com a assinatura BLOQUEADA/expirada
/// (ex.: consultar a própria assinatura para ver a tela de regularização).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PermitirBloqueadoAttribute : Attribute;
