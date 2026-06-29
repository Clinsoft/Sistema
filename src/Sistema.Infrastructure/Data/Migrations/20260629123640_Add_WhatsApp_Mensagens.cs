using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Add_WhatsApp_Mensagens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesWhatsAppMensagem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhoneNumberId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AccessToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BusinessAccountId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WebhookVerifyToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    EnviarAniversario = table.Column<bool>(type: "bit", nullable: false),
                    EnviarPromocoes = table.Column<bool>(type: "bit", nullable: false),
                    EnviarNovidades = table.Column<bool>(type: "bit", nullable: false),
                    HoraDisparo = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesWhatsAppMensagem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HistoricosMensagensWhatsApp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Telefone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NomeDestinatario = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoDisparo = table.Column<int>(type: "int", nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WamId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErroDetalhe = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EnviadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntregueEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LidoEm = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricosMensagensWhatsApp", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TemplatesWhatsAppMensagem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmpresaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeMeta = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Idioma = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoDisparo = table.Column<int>(type: "int", nullable: false),
                    VariaveisJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExemploTexto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemplatesWhatsAppMensagem", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesWhatsAppMensagem");

            migrationBuilder.DropTable(
                name: "HistoricosMensagensWhatsApp");

            migrationBuilder.DropTable(
                name: "TemplatesWhatsAppMensagem");
        }
    }
}
