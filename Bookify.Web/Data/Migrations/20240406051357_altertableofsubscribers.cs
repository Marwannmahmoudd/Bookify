using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bookify.Web.Data.Migrations
{
    public partial class altertableofsubscribers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "Subscribers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "Subscribers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Subscribers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastUpdatedById",
                table: "Subscribers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUpdatedOn",
                table: "Subscribers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_CreatedById",
                table: "Subscribers",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Subscribers_LastUpdatedById",
                table: "Subscribers",
                column: "LastUpdatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscribers_AspNetUsers_CreatedById",
                table: "Subscribers",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Subscribers_AspNetUsers_LastUpdatedById",
                table: "Subscribers",
                column: "LastUpdatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subscribers_AspNetUsers_CreatedById",
                table: "Subscribers");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscribers_AspNetUsers_LastUpdatedById",
                table: "Subscribers");

            migrationBuilder.DropIndex(
                name: "IX_Subscribers_CreatedById",
                table: "Subscribers");

            migrationBuilder.DropIndex(
                name: "IX_Subscribers_LastUpdatedById",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "LastUpdatedById",
                table: "Subscribers");

            migrationBuilder.DropColumn(
                name: "LastUpdatedOn",
                table: "Subscribers");
        }
    }
}
