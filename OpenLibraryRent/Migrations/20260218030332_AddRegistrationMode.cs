using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenLibraryRent.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RegistrationMode",
                table: "TenantDetails",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegistrationMode",
                table: "TenantDetails");
        }
    }
}
