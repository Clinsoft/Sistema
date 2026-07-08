using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sistema.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfiguracaoFiscal_CamposCompletos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaCofinsPadrao",
                table: "ConfiguracoesFiscais",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaIcmsInterestadual",
                table: "ConfiguracoesFiscais",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaIcmsPadrao",
                table: "ConfiguracoesFiscais",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaIssPadrao",
                table: "ConfiguracoesFiscais",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AliquotaPisPadrao",
                table: "ConfiguracoesFiscais",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "CfopVendaConsumidor",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CfopVendaEstadual",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CfopVendaInterestadual",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoMunicipioIbge",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoServicoMunicipalPadrao",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContingenciaPadrao",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CsosnPadrao",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CstCofinsPadrao",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CstIcmsPadrao",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CstPisPadrao",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FormatoDanfe",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HabilitarMDFe",
                table: "ConfiguracoesFiscais",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HabilitarNFSe",
                table: "ConfiguracoesFiscais",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ImprimirAutomaticamenteNFCe",
                table: "ConfiguracoesFiscais",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IncentivadorCultural",
                table: "ConfiguracoesFiscais",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "InscricaoMunicipal",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IssRetidoFonte",
                table: "ConfiguracoesFiscais",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModalTransporteMDFe",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NaturezaOperacaoPadrao",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrigemPadrao",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProximoNumeroMDFe",
                table: "ConfiguracoesFiscais",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "RegimeEspecialTributacao",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rntrc",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SerieMDFe",
                table: "ConfiguracoesFiscais",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SerieNFSe",
                table: "ConfiguracoesFiscais",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TipoEmitenteMDFe",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoImpressaoNFCe",
                table: "ConfiguracoesFiscais",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AliquotaCofinsPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "AliquotaIcmsInterestadual",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "AliquotaIcmsPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "AliquotaIssPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "AliquotaPisPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CfopVendaConsumidor",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CfopVendaEstadual",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CfopVendaInterestadual",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CodigoMunicipioIbge",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CodigoServicoMunicipalPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "ContingenciaPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CsosnPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CstCofinsPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CstIcmsPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "CstPisPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "FormatoDanfe",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "HabilitarMDFe",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "HabilitarNFSe",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "ImprimirAutomaticamenteNFCe",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "IncentivadorCultural",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "InscricaoMunicipal",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "IssRetidoFonte",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "ModalTransporteMDFe",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "NaturezaOperacaoPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "OrigemPadrao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "ProximoNumeroMDFe",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "RegimeEspecialTributacao",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "Rntrc",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "SerieMDFe",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "SerieNFSe",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "TipoEmitenteMDFe",
                table: "ConfiguracoesFiscais");

            migrationBuilder.DropColumn(
                name: "TipoImpressaoNFCe",
                table: "ConfiguracoesFiscais");
        }
    }
}
