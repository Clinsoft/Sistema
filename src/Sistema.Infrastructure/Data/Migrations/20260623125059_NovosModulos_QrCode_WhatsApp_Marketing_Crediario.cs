using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class NovosModulos_QrCode_WhatsApp_Marketing_Crediario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgendamentosPublicacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArteMarketingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rede = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataHoraAgendada = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataHoraPublicado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Legenda = table.Column<string>(type: "nvarchar(2200)", maxLength: 2200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    ErroPublicacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendamentosPublicacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArtesMarketing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Formato = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LayoutJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThumbnailBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UrlExportada = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtesMarketing", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogosWhatsApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    WhatsAppBusinessId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CatalogId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Provedor = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApiToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WebhookUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    UltimaSincronizacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogosWhatsApp", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Crediarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorEntrada = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorFinanciado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NumeroParcelas = table.Column<int>(type: "int", nullable: false),
                    TaxaJurosMensal = table.Column<decimal>(type: "decimal(8,4)", nullable: false),
                    DataContrato = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Crediarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PedidosWhatsApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TelefoneCliente = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NomeCliente = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EnderecoEntrega = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TipoEntrega = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    StatusPagamento = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    PixCopiaCola = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataPagamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosWhatsApp", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QrCodesProduto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UrlPublica = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    QrCodeBase64 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalScans = table.Column<int>(type: "int", nullable: false),
                    UltimoScan = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QrCodesProduto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReceitasProduto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ingredientes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ModoPreparo = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Dicas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TempoPreparoMinutos = table.Column<int>(type: "int", nullable: true),
                    Porcoes = table.Column<int>(type: "int", nullable: true),
                    UrlFoto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceitasProduto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RenegociacoesCrediario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CrediarioOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CrediarioNovoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaldoRenegociado = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Desconto = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Motivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RenegociacoesCrediario", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SugestoesProduto",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ProdutoRelacionadoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SugestoesProduto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TabelasNutricionais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Porcao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Calorias = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    CaloriasGordura = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    GordurasTotais = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    GordurasSaturadas = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    GordurasTrans = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Colesterol = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Sodio = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    CarboidratosTotais = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    FibrasDieteticas = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Acucares = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Proteinas = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    VitaminaA = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    VitaminaC = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    VitaminaD = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    VitaminaE = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    VitaminaK = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    VitaminaB1 = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    VitaminaB2 = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    VitaminaB3 = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    VitaminaB6 = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    VitaminaB12 = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    AcidoFolico = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Calcio = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Ferro = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Magnesio = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Zinco = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    Selenio = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    InformacoesAdicionais = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Ingredientes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Alergenicos = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ModoConservacao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TabelasNutricionais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplatesMarketing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Formato = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LayoutJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThumbnailBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EhTemplate = table.Column<bool>(type: "bit", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplatesMarketing", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensCatalogo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UrlFoto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Disponivel = table.Column<bool>(type: "bit", nullable: false),
                    IdExterno = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensCatalogo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensCatalogo_CatalogosWhatsApp_CatalogoId",
                        column: x => x.CatalogoId,
                        principalTable: "CatalogosWhatsApp",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParcelasCrediario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CrediarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorJuros = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ValorMulta = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DataVencimento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValorPago = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    ContaReceberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PixCopiaCola = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelasCrediario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParcelasCrediario_Crediarios_CrediarioId",
                        column: x => x.CrediarioId,
                        principalTable: "Crediarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItensPedidoWhatsApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PedidoWhatsAppId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProdutoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensPedidoWhatsApp", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensPedidoWhatsApp_PedidosWhatsApp_PedidoWhatsAppId",
                        column: x => x.PedidoWhatsAppId,
                        principalTable: "PedidosWhatsApp",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgendamentosPublicacao_EmpresaId_DataHoraAgendada_Status",
                table: "AgendamentosPublicacao",
                columns: new[] { "EmpresaId", "DataHoraAgendada", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtesMarketing_EmpresaId_CriadoEm",
                table: "ArtesMarketing",
                columns: new[] { "EmpresaId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_Crediarios_EmpresaId_ClienteId",
                table: "Crediarios",
                columns: new[] { "EmpresaId", "ClienteId" });

            migrationBuilder.CreateIndex(
                name: "IX_Crediarios_EmpresaId_Numero",
                table: "Crediarios",
                columns: new[] { "EmpresaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensCatalogo_CatalogoId_ProdutoId",
                table: "ItensCatalogo",
                columns: new[] { "CatalogoId", "ProdutoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedidoWhatsApp_PedidoWhatsAppId",
                table: "ItensPedidoWhatsApp",
                column: "PedidoWhatsAppId");

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasCrediario_CrediarioId_DataVencimento",
                table: "ParcelasCrediario",
                columns: new[] { "CrediarioId", "DataVencimento" });

            migrationBuilder.CreateIndex(
                name: "IX_ParcelasCrediario_CrediarioId_Numero",
                table: "ParcelasCrediario",
                columns: new[] { "CrediarioId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PedidosWhatsApp_EmpresaId_CriadoEm",
                table: "PedidosWhatsApp",
                columns: new[] { "EmpresaId", "CriadoEm" });

            migrationBuilder.CreateIndex(
                name: "IX_PedidosWhatsApp_EmpresaId_Numero",
                table: "PedidosWhatsApp",
                columns: new[] { "EmpresaId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrCodesProduto_ProdutoId",
                table: "QrCodesProduto",
                column: "ProdutoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QrCodesProduto_Slug",
                table: "QrCodesProduto",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceitasProduto_ProdutoId_Ordem",
                table: "ReceitasProduto",
                columns: new[] { "ProdutoId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_SugestoesProduto_ProdutoId_Ordem",
                table: "SugestoesProduto",
                columns: new[] { "ProdutoId", "Ordem" });

            migrationBuilder.CreateIndex(
                name: "IX_TabelasNutricionais_ProdutoId",
                table: "TabelasNutricionais",
                column: "ProdutoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgendamentosPublicacao");

            migrationBuilder.DropTable(
                name: "ArtesMarketing");

            migrationBuilder.DropTable(
                name: "ItensCatalogo");

            migrationBuilder.DropTable(
                name: "ItensPedidoWhatsApp");

            migrationBuilder.DropTable(
                name: "ParcelasCrediario");

            migrationBuilder.DropTable(
                name: "QrCodesProduto");

            migrationBuilder.DropTable(
                name: "ReceitasProduto");

            migrationBuilder.DropTable(
                name: "RenegociacoesCrediario");

            migrationBuilder.DropTable(
                name: "SugestoesProduto");

            migrationBuilder.DropTable(
                name: "TabelasNutricionais");

            migrationBuilder.DropTable(
                name: "TemplatesMarketing");

            migrationBuilder.DropTable(
                name: "CatalogosWhatsApp");

            migrationBuilder.DropTable(
                name: "PedidosWhatsApp");

            migrationBuilder.DropTable(
                name: "Crediarios");
        }
    }
}
