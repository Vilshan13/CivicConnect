using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CivicConnect.Api.Migrations;

/// <inheritdoc />
public partial class _20261122144451_AddReportStatusHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReportStatusHistories",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ReportId = table.Column<int>(type: "integer", nullable: false),
                PreviousStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                NewStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                ChangedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReportStatusHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_ReportStatusHistories_Reports_ReportId",
                    column: x => x.ReportId,
                    principalTable: "Reports",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ReportStatusHistories_ReportId",
            table: "ReportStatusHistories",
            column: "ReportId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ReportStatusHistories");
    }
}
