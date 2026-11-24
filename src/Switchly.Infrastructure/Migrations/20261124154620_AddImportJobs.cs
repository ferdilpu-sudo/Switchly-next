using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Switchly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportJobs",
                columns: table => new
                {
                    ImportJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceBrowser = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SourceProfileLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SelectedCategoriesJson = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    TargetProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ResultSummaryJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportJobs", x => x.ImportJobId);
                    table.ForeignKey(
                        name: "FK_ImportJobs_Profiles_TargetProfileId",
                        column: x => x.TargetProfileId,
                        principalTable: "Profiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_CreatedAtUtc",
                table: "ImportJobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_TargetProfileId",
                table: "ImportJobs",
                column: "TargetProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportJobs");
        }
    }
}
