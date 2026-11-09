using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RedWineStock.Data;

#nullable disable

namespace RedWineStock.Migrations
{
    /// <summary>
    /// Combos (paquetes de productos con precio fijo): tablas Combos y ComboItems, columna IdCombo en DetallesVenta
    /// y categorías sugeridas para vender otras bebidas además del vino.
    /// (Escrita a mano: no lleva archivo .Designer; el snapshot sí está actualizado.)
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261107233000_Combos")]
    public partial class Combos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Combos",
                columns: table => new
                {
                    IdCombo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Precio = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Combos", x => x.IdCombo);
                });

            migrationBuilder.CreateTable(
                name: "ComboItems",
                columns: table => new
                {
                    IdComboItem = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdCombo = table.Column<int>(type: "int", nullable: false),
                    IdProducto = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComboItems", x => x.IdComboItem);
                    table.ForeignKey(
                        name: "FK_ComboItems_Combos_IdCombo",
                        column: x => x.IdCombo,
                        principalTable: "Combos",
                        principalColumn: "IdCombo",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ComboItems_Productos_IdProducto",
                        column: x => x.IdProducto,
                        principalTable: "Productos",
                        principalColumn: "IdProducto",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddColumn<int>(
                name: "IdCombo",
                table: "DetallesVenta",
                type: "int",
                nullable: true);

            // El índice (venta, producto) deja de ser único: un producto puede ir suelto y también dentro de un combo
            migrationBuilder.DropIndex(
                name: "IX_DetallesVenta_IdVenta_IdProducto",
                table: "DetallesVenta");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_IdVenta_IdProducto",
                table: "DetallesVenta",
                columns: new[] { "IdVenta", "IdProducto" });

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_IdCombo",
                table: "DetallesVenta",
                column: "IdCombo");

            migrationBuilder.CreateIndex(
                name: "IX_ComboItems_IdCombo_IdProducto",
                table: "ComboItems",
                columns: new[] { "IdCombo", "IdProducto" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComboItems_IdProducto",
                table: "ComboItems",
                column: "IdProducto");

            migrationBuilder.AddForeignKey(
                name: "FK_DetallesVenta_Combos_IdCombo",
                table: "DetallesVenta",
                column: "IdCombo",
                principalTable: "Combos",
                principalColumn: "IdCombo",
                onDelete: ReferentialAction.Restrict);

            // Categorías sugeridas (solo se agregan las que no existan todavía; después se editan desde la app)
            var categorias = new[]
            {
                ("Tinto", "Vinos tintos"),
                ("Blanco", "Vinos blancos"),
                ("Rosado", "Vinos rosados"),
                ("Espumantes", "Espumantes y champagne"),
                ("Destilados", "Destilados en general: aguardientes, ron, vodka, tequila"),
                ("Whisky", "Whisky y bourbon"),
                ("Gin", "Gin"),
                ("Vermouth", "Vermouth"),
                ("Licores", "Licores y cremas"),
                ("Aperitivos", "Fernet, bitter y aperitivos"),
                ("Cervezas", "Cervezas en lata y botella"),
                ("Gaseosas y sin alcohol", "Gaseosas, aguas, jugos y otros sin alcohol")
            };
            foreach (var (nombre, descripcion) in categorias)
            {
                migrationBuilder.Sql(
                    "INSERT INTO [Categorias] ([Nombre], [Descripcion], [Activo]) " +
                    $"SELECT N'{nombre}', N'{descripcion}', 1 " +
                    $"WHERE NOT EXISTS (SELECT 1 FROM [Categorias] WHERE [Nombre] = N'{nombre}');");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetallesVenta_Combos_IdCombo",
                table: "DetallesVenta");

            migrationBuilder.DropTable(
                name: "ComboItems");

            migrationBuilder.DropTable(
                name: "Combos");

            migrationBuilder.DropIndex(
                name: "IX_DetallesVenta_IdCombo",
                table: "DetallesVenta");

            migrationBuilder.DropIndex(
                name: "IX_DetallesVenta_IdVenta_IdProducto",
                table: "DetallesVenta");

            migrationBuilder.DropColumn(
                name: "IdCombo",
                table: "DetallesVenta");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesVenta_IdVenta_IdProducto",
                table: "DetallesVenta",
                columns: new[] { "IdVenta", "IdProducto" },
                unique: true);
        }
    }
}
