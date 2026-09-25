using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraceProject.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase5_AttendancePersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendances_CameraId",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_EmployeeId",
                table: "Attendances");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_CameraId_CheckIn",
                table: "Attendances",
                columns: new[] { "CameraId", "CheckIn" });

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_CheckIn",
                table: "Attendances",
                column: "CheckIn");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId_CheckIn",
                table: "Attendances",
                columns: new[] { "EmployeeId", "CheckIn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendances_CameraId_CheckIn",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_CheckIn",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_EmployeeId_CheckIn",
                table: "Attendances");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_CameraId",
                table: "Attendances",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId",
                table: "Attendances",
                column: "EmployeeId");
        }
    }
}
