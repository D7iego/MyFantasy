using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyFantasy.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Season",
                table: "Sales",
                type: "varchar(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Season",
                table: "Holdings",
                type: "varchar(9)",
                maxLength: 9,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PlayerSeasonStats",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Season = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Team = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TeamId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Position = table.Column<int>(type: "int", nullable: false),
                    TotalPoints = table.Column<double>(type: "double", nullable: true),
                    Goals = table.Column<int>(type: "int", nullable: true),
                    Assists = table.Column<int>(type: "int", nullable: true),
                    Minutes = table.Column<int>(type: "int", nullable: true),
                    StartValue = table.Column<long>(type: "bigint", nullable: true),
                    EndValue = table.Column<long>(type: "bigint", nullable: true),
                    PeakValue = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerSeasonStats", x => new { x.PlayerId, x.Season });
                    table.ForeignKey(
                        name: "FK_PlayerSeasonStats_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Seasons",
                columns: table => new
                {
                    Label = table.Column<string>(type: "varchar(9)", maxLength: 9, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Seasons", x => x.Label);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // Back-fill: los datos existentes son de la temporada 2026/27 (la app
            // arrancó esta temporada). Etiquetamos operaciones y sembramos la fila
            // de temporada actual para que el arranque sea consistente.
            migrationBuilder.Sql("UPDATE `Holdings` SET `Season` = '2026/27' WHERE `Season` = '';");
            migrationBuilder.Sql("UPDATE `Sales` SET `Season` = '2026/27' WHERE `Season` = '';");
            migrationBuilder.Sql("INSERT IGNORE INTO `Seasons` (`Label`, `StartsOn`, `IsCurrent`) VALUES ('2026/27', '2026-07-01', 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerSeasonStats");

            migrationBuilder.DropTable(
                name: "Seasons");

            migrationBuilder.DropColumn(
                name: "Season",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "Season",
                table: "Holdings");
        }
    }
}
