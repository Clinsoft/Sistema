namespace Sistema.Infrastructure.Branding;

/// <summary>
/// Identidade da marca da INSTÂNCIA (não da empresa/tenant). O mesmo código roda em
/// várias instâncias (ex.: EcoGranel e Natural Sistemas); cada uma lê a sua marca da
/// seção "Branding" do appsettings/variáveis de ambiente. Os PADRÕES abaixo são os da
/// EcoGranel — assim, se nada for configurado, a instância continua idêntica à EcoGranel.
/// </summary>
public sealed class BrandingOptions
{
    public const string Section = "Branding";

    /// <summary>Nome de exibição do produto/sistema (aparece em telas, e-mails, catálogo).</summary>
    public string Nome { get; set; } = "EcoGranel";

    /// <summary>Descrição curta sob o nome (ex.: "Produtos Naturais").</summary>
    public string Slogan { get; set; } = "Produtos Naturais";

    /// <summary>Razão social usada como fallback em documentos (regulamento de premiação).</summary>
    public string RazaoSocialPadrao { get; set; } = "ECOGRANEL COMERCIO DE PRODUTOS NATURAIS LTDA";

    /// <summary>URL/caminho público da logo usado pelo frontend (servido de wwwroot ou /public).</summary>
    public string LogoUrl { get; set; } = "/logo-ecogranel.png";

    /// <summary>Nome do arquivo de logo dentro de wwwroot (DANFE, artes de marketing).</summary>
    public string LogoArquivo { get; set; } = "logo-ecogranel.png";

    /// <summary>Base pública da própria aplicação (links de imagem, callbacks).</summary>
    public string PublicBaseUrl { get; set; } = "https://sistema.ecogranel.com.br";

    /// <summary>Site institucional público (catálogo, links de WhatsApp).</summary>
    public string SiteUrl { get; set; } = "https://ecogranel.com.br";

    /// <summary>Base do link de produto no site (usado no QR da etiqueta e no catálogo). Termina pronto para concatenar o slug quando contém "?p=".</summary>
    public string CatalogoProdutoUrl { get; set; } = "https://ecogranel.com.br/produtos/produto.php?p=";

    /// <summary>Link do catálogo geral (WhatsApp).</summary>
    public string CatalogoUrl { get; set; } = "https://ecogranel.com.br/produtos";

    /// <summary>Remetente padrão de e-mail quando "Email:Remetente" não estiver configurado.</summary>
    public string EmailRemetente { get; set; } = "noreply@ecogranel.com";

    /// <summary>E-mail de contato exibido em textos institucionais.</summary>
    public string EmailContato { get; set; } = "contato@ecogranel.com.br";

    /// <summary>Cores da marca (tema). Usadas pelo frontend e no prompt de IA de marketing.</summary>
    public string CorPrimaria { get; set; } = "#5C2D0C";
    public string CorSecundaria { get; set; } = "#8B4513";
    public string CorAccent { get; set; } = "#6AAF2E";
    public string CorFundo { get; set; } = "#FAF7F4";

    /// <summary>Identificador do software no XML da NF-e (tag verProc).</summary>
    public string VerProc { get; set; } = "EcoGranel-1.0";

    /// <summary>Descrição livre da paleta para o prompt de IA (marketing). Vazio = monta a partir das cores.</summary>
    public string DescricaoVisualIa { get; set; } = "";
}

/// <summary>
/// Acesso estático à marca da instância, preenchido no startup a partir da config.
/// Existe para os pontos que não usam injeção de dependência (builders/serviços estáticos).
/// Serviços com DI podem injetar <see cref="BrandingOptions"/> diretamente.
/// </summary>
public static class BrandingRuntime
{
    public static BrandingOptions Atual { get; set; } = new();
}
