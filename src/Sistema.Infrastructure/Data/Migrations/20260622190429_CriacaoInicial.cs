using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CriacaoInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CategoriaPaiId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CpfCnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: true),
                    TipoPessoa = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Telefone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Celular = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DataNascimento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Logradouro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Complemento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Bairro = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Cidade = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Uf = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Cep = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Classificacao = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LimiteCredito = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    PontosFidelidade = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Empresas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RazaoSocial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NomeFantasia = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    InscricaoEstadual = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InscricaoMunicipal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RegimeTributario = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Logradouro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Complemento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Bairro = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Cidade = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Uf = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    Cep = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Telefone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empresas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fornecedores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RazaoSocial = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NomeFantasia = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Cnpj = table.Column<string>(type: "nvarchar(14)", maxLength: 14, nullable: false),
                    InscricaoEstadual = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Telefone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Contato = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Logradouro = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Complemento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Bairro = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Cidade = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Uf = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Cep = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    PrazoPagamentoDias = table.Column<int>(type: "int", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fornecedores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocaisEstoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Principal = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocaisEstoque", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroLote = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DataFabricacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataValidade = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Marcas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Marcas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimentacoesEstoque",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DocumentoOrigem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentacoesEstoque", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PDVSessoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Abertura = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fechamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SaldoAbertura = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SaldoFechamento = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalVendas = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalSuprimentos = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalSangrias = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ObservacaoFechamento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PDVSessoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PedidosCompra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FornecedorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    DataPedido = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataPrevisaoEntrega = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataRecebimento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosCompra", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Produtos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CodigoBarras = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DescricaoComplementar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CategoriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarcaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnidadeMedidaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FornecedorPrincipalId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ncm = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Cest = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    CstIcms = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CsosnIcms = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CstPisCofins = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    AliquotaIcms = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    AliquotaPis = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    AliquotaCofins = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    Cfop = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    Origem = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    CustoUnitario = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PrecoVenda = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrecoAtacado = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Markup = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MargemLucro = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstoqueAtual = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    EstoqueMinimo = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    EstoqueMaximo = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    ControlarLote = table.Column<bool>(type: "bit", nullable: false),
                    ControlarValidade = table.Column<bool>(type: "bit", nullable: false),
                    ProdutoBalanca = table.Column<bool>(type: "bit", nullable: false),
                    CodigoPlu = table.Column<int>(type: "int", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    VendidoFracionado = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Produtos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnidadesMedida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sigla = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnidadesMedida", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SenhaHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Perfil = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    UltimoAcesso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalEstoqueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataHoraFechamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDesconto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAcrescimo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPago = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Troco = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensPedidoCompra",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PedidoCompraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantidadeRecebida = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensPedidoCompra", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensPedidoCompra_PedidosCompra_PedidoCompraId",
                        column: x => x.PedidoCompraId,
                        principalTable: "PedidosCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItensVenda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PercentualDesconto = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    TotalDesconto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensVenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensVenda_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PagamentosVenda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Forma = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Parcelas = table.Column<int>(type: "int", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagamentosVenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagamentosVenda_Vendas_VendaId",
                        column: x => x.VendaId,
                        principalTable: "Vendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_EmpresaId_CpfCnpj",
                table: "Clientes",
                columns: new[] { "EmpresaId", "CpfCnpj" },
                filter: "[CpfCnpj] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Empresas_Cnpj",
                table: "Empresas",
                column: "Cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Fornecedores_EmpresaId_Cnpj",
                table: "Fornecedores",
                columns: new[] { "EmpresaId", "Cnpj" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedidoCompra_PedidoCompraId",
                table: "ItensPedidoCompra",
                column: "PedidoCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensVenda_VendaId",
                table: "ItensVenda",
                column: "VendaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_ProdutoId_LocalEstoqueId_NumeroLote",
                table: "Lotes",
                columns: new[] { "ProdutoId", "LocalEstoqueId", "NumeroLote" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoque_EmpresaId_CriadoEm",
                table: "MovimentacoesEstoque",
                columns: new[] { "EmpresaId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentacoesEstoque_ProdutoId_CriadoEm",
                table: "MovimentacoesEstoque",
                columns: new[] { "ProdutoId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosVenda_VendaId",
                table: "PagamentosVenda",
                column: "VendaId");

            migrationBuilder.CreateIndex(
                name: "IX_PDVSessoes_EmpresaId_Abertura",
                table: "PDVSessoes",
                columns: new[] { "EmpresaId", "Abertura" });

            migrationBuilder.CreateIndex(
                name: "IX_PedidosCompra_EmpresaId_Numero",
                table: "PedidosCompra",
                columns: new[] { "EmpresaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_EmpresaId_Codigo",
                table: "Produtos",
                columns: new[] { "EmpresaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_EmpresaId_CodigoBarras",
                table: "Produtos",
                columns: new[] { "EmpresaId", "CodigoBarras" },
                filter: "[CodigoBarras] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UnidadesMedida_EmpresaId_Sigla",
                table: "UnidadesMedida",
                columns: new[] { "EmpresaId", "Sigla" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_EmpresaId_Email",
                table: "Usuarios",
                columns: new[] { "EmpresaId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vendas_EmpresaId_ClienteId",
                table: "Vendas",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendas_EmpresaId_DataHora",
                table: "Vendas",
                columns: new[] { "EmpresaId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Vendas_EmpresaId_Numero",
                table: "Vendas",
                columns: new[] { "EmpresaId", "Numero" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Categorias");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "Empresas");

            migrationBuilder.DropTable(
                name: "Fornecedores");

            migrationBuilder.DropTable(
                name: "ItensPedidoCompra");

            migrationBuilder.DropTable(
                name: "ItensVenda");

            migrationBuilder.DropTable(
                name: "LocaisEstoque");

            migrationBuilder.DropTable(
                name: "Lotes");

            migrationBuilder.DropTable(
                name: "Marcas");

            migrationBuilder.DropTable(
                name: "MovimentacoesEstoque");

            migrationBuilder.DropTable(
                name: "PagamentosVenda");

            migrationBuilder.DropTable(
                name: "PDVSessoes");

            migrationBuilder.DropTable(
                name: "Produtos");

            migrationBuilder.DropTable(
                name: "UnidadesMedida");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "PedidosCompra");

            migrationBuilder.DropTable(
                name: "Vendas");
        }
    }
}
