using Sistema.Domain.Shared.Primitives;

namespace Sistema.Domain.Ajuda.Entities;

/// <summary>
/// Tutorial/ajuda exibido para o atendente (vídeo + passo a passo). O admin cadastra.
/// </summary>
public class Tutorial : Entity
{
    public Guid EmpresaId { get; private set; }
    public string Titulo { get; private set; } = null!;
    public string? Descricao { get; private set; }   // passo a passo (texto/markdown simples)
    public string? VideoUrl { get; private set; }    // link YouTube/Vimeo/MP4
    public string? Categoria { get; private set; }   // ex.: PDV, Clientes, Compras
    public int Ordem { get; private set; }
    public bool Ativo { get; private set; } = true;

    private Tutorial() { }

    public static Tutorial Criar(Guid empresaId, string titulo, string? descricao,
        string? videoUrl, string? categoria, int ordem)
        => new()
        {
            EmpresaId = empresaId,
            Titulo = titulo,
            Descricao = descricao,
            VideoUrl = videoUrl,
            Categoria = categoria,
            Ordem = ordem,
            Ativo = true,
        };

    public void Editar(string titulo, string? descricao, string? videoUrl, string? categoria, int ordem, bool ativo)
    {
        Titulo = titulo;
        Descricao = descricao;
        VideoUrl = videoUrl;
        Categoria = categoria;
        Ordem = ordem;
        Ativo = ativo;
        AtualizadoEm = DateTime.UtcNow;
    }
}
