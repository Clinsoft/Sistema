using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Vendas.Entities;

public class DevolucaoVenda : Entity
{
    public Guid EmpresaId { get; private set; }
    public Guid VendaId { get; private set; }
    public string NumeroVenda { get; private set; } = null!;
    public Guid? ClienteId { get; private set; }
    public DateTime DataHora { get; private set; }
    public string Motivo { get; private set; } = null!;
    public decimal TotalDevolvido { get; private set; }
    public bool ReporEstoque { get; private set; }

    private readonly List<ItemDevolucao> _itens = [];
    public IReadOnlyList<ItemDevolucao> Itens => _itens.AsReadOnly();

    private DevolucaoVenda() { }

    public static DevolucaoVenda Criar(Guid empresaId, Guid vendaId, string numeroVenda,
        Guid? clienteId, string motivo, IEnumerable<(Guid produtoId, string descricao, decimal quantidade, decimal valorUnitario)> itens,
        bool reporEstoque = true)
    {
        var devolucao = new DevolucaoVenda
        {
            EmpresaId = empresaId,
            VendaId = vendaId,
            NumeroVenda = numeroVenda,
            ClienteId = clienteId,
            Motivo = motivo,
            DataHora = DateTime.Now,
            ReporEstoque = reporEstoque,
        };

        foreach (var (produtoId, descricao, quantidade, valorUnitario) in itens)
            devolucao._itens.Add(new ItemDevolucao(devolucao.Id, produtoId, descricao, quantidade, valorUnitario));

        devolucao.TotalDevolvido = devolucao._itens.Sum(i => i.Total);
        return devolucao;
    }
}

public class ItemDevolucao : Entity
{
    public Guid DevolucaoId { get; private set; }
    public Guid ProdutoId { get; private set; }
    public string Descricao { get; private set; } = null!;
    public decimal Quantidade { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public decimal Total => Quantidade * ValorUnitario;

    private ItemDevolucao() { }

    internal ItemDevolucao(Guid devolucaoId, Guid produtoId, string descricao, decimal quantidade, decimal valorUnitario)
    {
        DevolucaoId = devolucaoId;
        ProdutoId = produtoId;
        Descricao = descricao;
        Quantidade = quantidade;
        ValorUnitario = valorUnitario;
    }
}
