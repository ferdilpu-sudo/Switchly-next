using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Switchly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileFavoritesGroupHiddenAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Groups_Kind_DomainKey",
                table: "Groups");

            migrationBuilder.AlterColumn<bool>(
                name: "RestoreLastSession",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "PersistSessionCookies",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<bool>(
                name: "IsArchived",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER");

            migrationBuilder.AddColumn<bool>(
                name: "IsFavorite",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Groups",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_StorageRelativePath",
                table: "Profiles",
                column: "StorageRelativePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Kind_DomainKey",
                table: "Groups",
                columns: new[] { "Kind", "DomainKey" },
                unique: true,
                filter: "\"Kind\" = 'WebsiteDomain'");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_GroupId",
                table: "GroupMemberships",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMemberships_Groups_GroupId",
                table: "GroupMemberships",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "GroupId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupMemberships_Profiles_ProfileId",
                table: "GroupMemberships",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "ProfileId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupMemberships_Groups_GroupId",
                table: "GroupMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupMemberships_Profiles_ProfileId",
                table: "GroupMemberships");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_StorageRelativePath",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Groups_Kind_DomainKey",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_GroupMemberships_GroupId",
                table: "GroupMemberships");

            migrationBuilder.DropColumn(
                name: "IsFavorite",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Groups");

            migrationBuilder.AlterColumn<bool>(
                name: "RestoreLastSession",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "PersistSessionCookies",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsArchived",
                table: "Profiles",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "INTEGER",
                oldDefaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Kind_DomainKey",
                table: "Groups",
                columns: new[] { "Kind", "DomainKey" });
        }
    }
}
