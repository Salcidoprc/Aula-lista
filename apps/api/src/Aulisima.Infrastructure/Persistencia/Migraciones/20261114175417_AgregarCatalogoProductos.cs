using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aulisima.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class AgregarCatalogoProductos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "precio_id",
                table: "suscripcion",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "precio_id",
                table: "pago",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "producto",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    alcance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    funciones = table.Column<List<string>>(type: "text[]", nullable: false),
                    meses_prepago = table.Column<List<int>>(type: "integer[]", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    id_externo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cambios_pendientes = table.Column<bool>(type: "boolean", nullable: false),
                    archivo_pendiente = table.Column<bool>(type: "boolean", nullable: false),
                    error_sincronizacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sincronizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_producto", x => x.id);
                    table.CheckConstraint("ck_producto_alcance", "alcance IN ('grupo')");
                    table.CheckConstraint("ck_producto_estado", "estado IN ('borrador', 'activo', 'archivado')");
                    table.CheckConstraint("ck_producto_orden", "orden BETWEEN 1 AND 999");
                });

            migrationBuilder.CreateTable(
                name: "precio_producto",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    producto_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    precio_primera = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    precio_adicional = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    id_externo_primera = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    id_externo_adicional = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    vigente_desde = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archivado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archivo_pendiente = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_precio_producto", x => x.id);
                    table.CheckConstraint("ck_precio_producto_estado", "estado IN ('pendiente', 'vigente', 'archivado')");
                    table.CheckConstraint("ck_precio_producto_montos", "precio_primera >= 1 AND precio_adicional >= 0");
                    table.ForeignKey(
                        name: "fk_precio_producto_productos_producto_id",
                        column: x => x.producto_id,
                        principalTable: "producto",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_suscripcion_precio_id",
                table: "suscripcion",
                column: "precio_id");

            migrationBuilder.CreateIndex(
                name: "ix_pago_precio_id",
                table: "pago",
                column: "precio_id");

            migrationBuilder.CreateIndex(
                name: "ix_precio_producto_producto_id_version",
                table: "precio_producto",
                columns: new[] { "producto_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_precio_producto_vigente",
                table: "precio_producto",
                column: "producto_id",
                unique: true,
                filter: "estado = 'vigente'");

            migrationBuilder.CreateIndex(
                name: "ix_producto_id_externo",
                table: "producto",
                column: "id_externo",
                unique: true,
                filter: "id_externo IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_pago_precios_producto_precio_id",
                table: "pago",
                column: "precio_id",
                principalTable: "precio_producto",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_suscripcion_precio_producto_precio_id",
                table: "suscripcion",
                column: "precio_id",
                principalTable: "precio_producto",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // ADR-0017: el catálogo lo lee cualquiera (la pantalla Plan, los candados); solo el administrador lo escribe
            // (RLS con app.admin_id). Sin FORCE, para que la migración siembre el producto inicial. Un producto nunca se
            // borra; una versión de precio solo se borra mientras es un borrador sin Stripe.
            migrationBuilder.Sql("""
                REVOKE DELETE ON producto FROM app_runtime;
                ALTER TABLE producto ENABLE ROW LEVEL SECURITY;
                CREATE POLICY producto_lectura ON producto FOR SELECT USING (true);
                CREATE POLICY producto_alta_admin ON producto FOR INSERT
                    WITH CHECK (NULLIF(current_setting('app.admin_id', true), '') IS NOT NULL);
                CREATE POLICY producto_cambio_admin ON producto FOR UPDATE
                    USING (NULLIF(current_setting('app.admin_id', true), '') IS NOT NULL)
                    WITH CHECK (NULLIF(current_setting('app.admin_id', true), '') IS NOT NULL);

                ALTER TABLE precio_producto ENABLE ROW LEVEL SECURITY;
                CREATE POLICY precio_producto_lectura ON precio_producto FOR SELECT USING (true);
                CREATE POLICY precio_producto_alta_admin ON precio_producto FOR INSERT
                    WITH CHECK (NULLIF(current_setting('app.admin_id', true), '') IS NOT NULL);
                CREATE POLICY precio_producto_cambio_admin ON precio_producto FOR UPDATE
                    USING (NULLIF(current_setting('app.admin_id', true), '') IS NOT NULL)
                    WITH CHECK (NULLIF(current_setting('app.admin_id', true), '') IS NOT NULL);
                CREATE POLICY precio_producto_baja_admin ON precio_producto FOR DELETE
                    USING (NULLIF(current_setting('app.admin_id', true), '') IS NOT NULL AND estado = 'pendiente'
                        AND id_externo_primera IS NULL AND id_externo_adicional IS NULL);
                """);

            // Los precios del piloto (ADR-0006) pasan a ser el primer producto del catálogo. Sus ids de Stripe se adoptan
            // de Pagos__PrecioPrimerGrupoId y Pagos__PrecioGrupoAdicionalId (ADR-0017). Toda suscripción y todo pago
            // existentes quedan en esa versión: nadie cambia de precio.
            migrationBuilder.Sql("""
                INSERT INTO producto (id, nombre, descripcion, alcance, funciones, meses_prepago, orden, estado, cambios_pendientes,
                    archivo_pendiente, created_at, updated_at)
                VALUES ('0199a3c0-0000-7000-8000-000000000001', 'Pro',
                    'Cierre de periodo con observaciones redactadas por IA, boletas en PDF para los tutores y avisos por WhatsApp.',
                    'grupo', ARRAY['cierre_ia', 'boletas', 'whatsapp_boletas', 'avisos_urgentes'], ARRAY[1, 3, 6], 1, 'activo',
                    false, false, now(), now());
                INSERT INTO precio_producto (id, producto_id, version, precio_primera, precio_adicional, moneda, estado, vigente_desde,
                    archivo_pendiente, created_at, updated_at)
                VALUES ('0199a3c0-0000-7000-8000-000000000002', '0199a3c0-0000-7000-8000-000000000001', 1, 79, 29, 'MXN', 'vigente',
                    now(), false, now(), now());
                """);
            migrationBuilder.Sql("""
                ALTER TABLE suscripcion DISABLE ROW LEVEL SECURITY;
                UPDATE suscripcion SET precio_id = '0199a3c0-0000-7000-8000-000000000002';
                ALTER TABLE suscripcion ENABLE ROW LEVEL SECURITY;
                ALTER TABLE pago DISABLE ROW LEVEL SECURITY;
                UPDATE pago SET precio_id = '0199a3c0-0000-7000-8000-000000000002';
                ALTER TABLE pago ENABLE ROW LEVEL SECURITY;
                """);

            // RF-095: cuántos docentes paga cada versión, y con qué versión se calcula el ingreso del panel. Solo ids.
            migrationBuilder.Sql("""
                CREATE FUNCTION admin_precios_suscripcion()
                RETURNS TABLE (docente_id uuid, precio_id uuid, estado text)
                LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
                #variable_conflict use_column
                BEGIN
                    PERFORM exigir_admin();
                    RETURN QUERY SELECT s.docente_id, s.precio_id, s.estado::text FROM suscripcion s;
                END $$;
                REVOKE ALL ON FUNCTION admin_precios_suscripcion() FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION admin_precios_suscripcion() TO app_runtime;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS admin_precios_suscripcion();");

            migrationBuilder.DropForeignKey(
                name: "fk_pago_precios_producto_precio_id",
                table: "pago");

            migrationBuilder.DropForeignKey(
                name: "fk_suscripcion_precio_producto_precio_id",
                table: "suscripcion");

            migrationBuilder.DropTable(
                name: "precio_producto");

            migrationBuilder.DropTable(
                name: "producto");

            migrationBuilder.DropIndex(
                name: "ix_suscripcion_precio_id",
                table: "suscripcion");

            migrationBuilder.DropIndex(
                name: "ix_pago_precio_id",
                table: "pago");

            migrationBuilder.DropColumn(
                name: "precio_id",
                table: "suscripcion");

            migrationBuilder.DropColumn(
                name: "precio_id",
                table: "pago");
        }
    }
}
