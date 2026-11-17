using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aulisima.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarParticipaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "valor_participacion",
                table: "grupo",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 0.10m);

            migrationBuilder.CreateTable(
                name: "participacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumno_id = table.Column<Guid>(type: "uuid", nullable: false),
                    docente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    periodo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_participacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_participacion_alumno_alumno_id",
                        column: x => x.alumno_id,
                        principalTable: "alumno",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_participacion_grupo_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_participacion_periodos_periodo_id",
                        column: x => x.periodo_id,
                        principalTable: "periodo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_grupo_valor_participacion",
                table: "grupo",
                sql: "valor_participacion BETWEEN 0.01 AND 1");

            migrationBuilder.CreateIndex(
                name: "ix_participacion_alumno_id",
                table: "participacion",
                column: "alumno_id");

            migrationBuilder.CreateIndex(
                name: "ix_participacion_docente_id",
                table: "participacion",
                column: "docente_id");

            migrationBuilder.CreateIndex(
                name: "ix_participacion_grupo_id_fecha",
                table: "participacion",
                columns: new[] { "grupo_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_participacion_periodo_id_alumno_id",
                table: "participacion",
                columns: new[] { "periodo_id", "alumno_id" });

            // ADR-0019: cada docente solo ve sus participaciones.
            migrationBuilder.HabilitarRlsPorDocente("participacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "participacion");

            migrationBuilder.DropCheckConstraint(
                name: "ck_grupo_valor_participacion",
                table: "grupo");

            migrationBuilder.DropColumn(
                name: "valor_participacion",
                table: "grupo");
        }
    }
}
