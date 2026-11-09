using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedWineStock.Migrations
{
    /// <inheritdoc />
    public partial class VencimientoYCajas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnidadesPorCaja",
                table: "Productos",
                type: "int",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaVencimiento",
                table: "Ingresos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Cajas",
                table: "DetallesIngreso",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnidadesPorCaja",
                table: "Productos");

            migrationBuilder.DropColumn(
                name: "FechaVencimiento",
                table: "Ingresos");

            migrationBuilder.DropColumn(
                name: "Cajas",
                table: "DetallesIngreso");
        }
    }
}
