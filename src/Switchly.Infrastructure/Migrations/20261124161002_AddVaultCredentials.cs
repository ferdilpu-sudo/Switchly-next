using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Switchly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVaultCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VaultCredentials",
                columns: table => new
                {
                    CredentialId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    SecretPayloadCiphertext = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CryptoVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    Nonce = table.Column<byte[]>(type: "BLOB", nullable: false),
                    AuthenticationTag = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaultCredentials", x => x.CredentialId);
                    table.ForeignKey(
                        name: "FK_VaultCredentials_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "ProfileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VaultCredentials_ProfileId_Origin",
                table: "VaultCredentials",
                columns: new[] { "ProfileId", "Origin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VaultCredentials");
        }
    }
}
