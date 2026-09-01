using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraceProject.Migrations
{
    /// <inheritdoc />
    public partial class removeFaceProfilFromUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_FaceProfil_FaceProfilId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_FaceProfilId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "FaceProfilId",
                table: "AspNetUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FaceProfilId",
                table: "AspNetUsers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_FaceProfilId",
                table: "AspNetUsers",
                column: "FaceProfilId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_FaceProfil_FaceProfilId",
                table: "AspNetUsers",
                column: "FaceProfilId",
                principalTable: "FaceProfil",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
