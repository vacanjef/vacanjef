using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VacanJef.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class HU009Notificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreadaEn",
                table: "Notificaciones",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Enlace",
                table: "Notificaciones",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeidaEn",
                table: "Notificaciones",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mensaje",
                table: "Notificaciones",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "Notificaciones",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_UsuarioId_LeidaEn_CreadaEn",
                table: "Notificaciones",
                columns: new[] { "UsuarioId", "LeidaEn", "CreadaEn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notificaciones_UsuarioId_LeidaEn_CreadaEn",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "CreadaEn",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "Enlace",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "LeidaEn",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "Mensaje",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "Notificaciones");
        }
    }
}
