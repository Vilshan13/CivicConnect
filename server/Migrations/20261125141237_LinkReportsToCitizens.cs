using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CivicConnect.Api.Migrations;

/// <inheritdoc />
public partial class _20261125141237_LinkReportsToCitizens : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CitizenId",
            table: "Reports",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Reports_CitizenId",
            table: "Reports",
            column: "CitizenId");

        migrationBuilder.AddForeignKey(
            name: "FK_Reports_AspNetUsers_CitizenId",
            table: "Reports",
            column: "CitizenId",
            principalTable: "AspNetUsers",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Reports_AspNetUsers_CitizenId",
            table: "Reports");

        migrationBuilder.DropIndex(
            name: "IX_Reports_CitizenId",
            table: "Reports");

        migrationBuilder.DropColumn(
            name: "CitizenId",
            table: "Reports");
    }
}
