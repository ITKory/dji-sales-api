using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace sales_performance_api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.CheckConstraint("ck_categories_name", "btrim(name) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    company = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                    table.CheckConstraint("ck_customers_company", "btrim(company) <> ''");
                    table.CheckConstraint("ck_customers_name", "btrim(name) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "managers",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    initials = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_managers", x => x.id);
                    table.CheckConstraint("ck_managers_initials", "btrim(initials) <> ''");
                    table.CheckConstraint("ck_managers_name", "btrim(name) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "seed_history",
                schema: "public",
                columns: table => new
                {
                    version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seed_history", x => x.version);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.CheckConstraint("ck_products_name", "btrim(name) <> ''");
                    table.ForeignKey(
                        name: "fk_products_categories",
                        column: x => x.category_id,
                        principalSchema: "public",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    currency = table.Column<string>(type: "character(3)", nullable: false, defaultValue: "USD")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales", x => x.id);
                    table.CheckConstraint("ck_sales_currency", "currency = 'USD'");
                    table.CheckConstraint("ck_sales_sale_date", "isfinite(sale_date)");
                    table.CheckConstraint("ck_sales_status", "status IN ('Paid', 'Cancelled', 'Refunded')");
                    table.ForeignKey(
                        name: "fk_sales_customers",
                        column: x => x.customer_id,
                        principalSchema: "public",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_managers",
                        column: x => x.manager_id,
                        principalSchema: "public",
                        principalTable: "managers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sale_items",
                schema: "public",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sale_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id_at_sale = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name_at_sale = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_sale_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    unit_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sale_items", x => x.id);
                    table.CheckConstraint("ck_sale_items_line_number", "line_number > 0");
                    table.CheckConstraint("ck_sale_items_product_name", "btrim(product_name_at_sale) <> ''");
                    table.CheckConstraint("ck_sale_items_quantity", "quantity > 0");
                    table.CheckConstraint("ck_sale_items_unit_cost", "unit_cost >= 0");
                    table.CheckConstraint("ck_sale_items_unit_sale_price", "unit_sale_price >= 0");
                    table.ForeignKey(
                        name: "fk_sale_items_categories",
                        column: x => x.category_id_at_sale,
                        principalSchema: "public",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_items_products",
                        column: x => x.product_id,
                        principalSchema: "public",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sale_items_sales",
                        column: x => x.sale_id,
                        principalSchema: "public",
                        principalTable: "sales",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_categories_name",
                schema: "public",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_category_id",
                schema: "public",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_sale_items_category_id_at_sale",
                schema: "public",
                table: "sale_items",
                column: "category_id_at_sale");

            migrationBuilder.CreateIndex(
                name: "ix_sale_items_product_id",
                schema: "public",
                table: "sale_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_sale_items_sale_id_line_number",
                schema: "public",
                table: "sale_items",
                columns: new[] { "sale_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sales_customer_id",
                schema: "public",
                table: "sales",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_manager_id_sale_date",
                schema: "public",
                table: "sales",
                columns: new[] { "manager_id", "sale_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sales_sale_date_id",
                schema: "public",
                table: "sales",
                columns: new[] { "sale_date", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_sales_status_sale_date",
                schema: "public",
                table: "sales",
                columns: new[] { "status", "sale_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sale_items",
                schema: "public");

            migrationBuilder.DropTable(
                name: "seed_history",
                schema: "public");

            migrationBuilder.DropTable(
                name: "products",
                schema: "public");

            migrationBuilder.DropTable(
                name: "sales",
                schema: "public");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "public");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "public");

            migrationBuilder.DropTable(
                name: "managers",
                schema: "public");
        }
    }
}
