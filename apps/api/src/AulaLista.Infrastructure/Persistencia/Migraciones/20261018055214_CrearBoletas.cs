using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaLista.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class CrearBoletas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cantidad",
                table: "auditoria",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "boleta",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    datos = table.Column<string>(type: "jsonb", nullable: false),
                    codigo = table.Column<string>(type: "character(8)", fixedLength: true, maxLength: 8, nullable: false),
                    emitida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pdf_ruta = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    fallo_pdf = table.Column<bool>(type: "boolean", nullable: false),
                    publicada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reemplazada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boleta", x => x.id);
                    table.CheckConstraint("ck_boleta_reemplazada", "reemplazada_por IS NULL OR publicada_en IS NOT NULL");
                    table.CheckConstraint("ck_boleta_version", "version >= 1");
                    table.ForeignKey(
                        name: "fk_boleta_alumno_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_boleta_boleta_reemplazada_por",
                        column: x => x.reemplazada_por,
                        principalTable: "boleta",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boleta_periodos_periodo_id",
                        column: x => x.periodo_id,
                        principalTable: "periodo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_boleta_alumno_id_periodo_id_version",
                table: "boleta",
                columns: new[] { "alumno_id", "periodo_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_boleta_codigo",
                table: "boleta",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_boleta_periodo_id",
                table: "boleta",
                column: "periodo_id");

            migrationBuilder.CreateIndex(
                name: "ix_boleta_reemplazada_por",
                table: "boleta",
                column: "reemplazada_por");

            // reglas/03: toda tabla del docente llega con su política RLS en la misma migración.
            migrationBuilder.HabilitarRlsPorDocente("boleta");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boleta");

            migrationBuilder.DropColumn(
                name: "cantidad",
                table: "auditoria");
        }
    }
}
