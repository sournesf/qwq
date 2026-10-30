using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication2.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnerUserId",
                table: "Teams",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_OwnerUserId",
                table: "Teams",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_AspNetUsers_OwnerUserId",
                table: "Teams",
                column: "OwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Teams_AspNetUsers_OwnerUserId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_OwnerUserId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "Teams");
        }
    }
}
