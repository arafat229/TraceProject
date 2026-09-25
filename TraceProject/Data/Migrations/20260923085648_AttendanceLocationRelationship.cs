using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraceProject.Data.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceLocationRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Locations_LocationId1",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_LocationId1",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "LocationId1",
                table: "Attendances");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "LocationId1",
                table: "Attendances",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_LocationId1",
                table: "Attendances",
                column: "LocationId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Locations_LocationId1",
                table: "Attendances",
                column: "LocationId1",
                principalTable: "Locations",
                principalColumn: "Id");
        }
    }
}
