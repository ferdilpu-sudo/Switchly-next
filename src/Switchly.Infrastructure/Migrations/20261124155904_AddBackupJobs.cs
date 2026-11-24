using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Switchly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackupJobs",
                columns: table => new
                {
                    BackupJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    DestinationPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IncludedProfilesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ArchiveVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ResultSummaryJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupJobs", x => x.BackupJobId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupJobs_StartedAtUtc",
                table: "BackupJobs",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupJobs");
        }
    }
}
