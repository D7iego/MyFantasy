using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFantasy.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    RefreshTokenEnc = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthStates", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthStates");
        }
    }
}
