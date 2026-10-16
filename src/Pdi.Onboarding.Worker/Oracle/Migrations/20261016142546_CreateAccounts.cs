using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pdi.Onboarding.Worker.Oracle.Migrations
{
    /// <inheritdoc />
    public partial class CreateAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ACCOUNTS",
                columns: table => new
                {
                    ID = table.Column<long>(type: "NUMBER(19)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    PROPOSAL_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    PRODUCT_CODE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ACCOUNT_NUMBER = table.Column<string>(type: "NVARCHAR2(13)", maxLength: 13, nullable: false),
                    HOLDER_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    HOLDER_TAX_ID = table.Column<string>(type: "NVARCHAR2(11)", maxLength: 11, nullable: false),
                    SOURCE_EVENT_ID = table.Column<Guid>(type: "RAW(16)", nullable: false),
                    CREATED_AT = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ACCOUNTS", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ACCOUNTS_NUMBER",
                table: "ACCOUNTS",
                column: "ACCOUNT_NUMBER",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ACCOUNTS_PROPOSAL_PRODUCT",
                table: "ACCOUNTS",
                columns: new[] { "PROPOSAL_ID", "PRODUCT_CODE" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACCOUNTS");
        }
    }
}
