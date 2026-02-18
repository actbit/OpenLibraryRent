using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenLibraryRent.Migrations
{
    /// <inheritdoc />
    public partial class AddUserApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalFormFields",
                table: "TenantDetails",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalInstructions",
                table: "TenantDetails",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultApprovedRoles",
                table: "TenantDetails",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireApproval",
                table: "TenantDetails",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserApprovalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Sub = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApplicationData = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AssignedRoles = table.Column<string>(type: "text", nullable: true),
                    UserMetadata = table.Column<string>(type: "text", nullable: true),
                    CreatedUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserApprovalRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserApprovalRequests_TenantId_Email",
                table: "UserApprovalRequests",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_UserApprovalRequests_TenantId_Status",
                table: "UserApprovalRequests",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_UserApprovalRequests_TenantId_Sub",
                table: "UserApprovalRequests",
                columns: new[] { "TenantId", "Sub" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserApprovalRequests");

            migrationBuilder.DropColumn(
                name: "ApprovalFormFields",
                table: "TenantDetails");

            migrationBuilder.DropColumn(
                name: "ApprovalInstructions",
                table: "TenantDetails");

            migrationBuilder.DropColumn(
                name: "DefaultApprovedRoles",
                table: "TenantDetails");

            migrationBuilder.DropColumn(
                name: "RequireApproval",
                table: "TenantDetails");
        }
    }
}
