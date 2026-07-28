using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFantasy.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLeagueTeamId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeamId",
                table: "Leagues",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Leagues");
        }
    }
}
