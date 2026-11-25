using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Switchly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupDefaultUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultUrl",
                table: "Groups",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultUrl",
                table: "Groups");
        }
    }
}
