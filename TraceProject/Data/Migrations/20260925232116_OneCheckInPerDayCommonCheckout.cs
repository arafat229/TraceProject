using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraceProject.Data.Migrations
{
    /// <inheritdoc />
    public partial class OneCheckInPerDayCommonCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttendanceUpdateIntervalMinutes",
                table: "SystemSettings");

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkDate",
                table: "Attendances",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            // Existing rows: the work day is the server-local date of the check-in
            var offset = TimeZoneInfo.Local.BaseUtcOffset;
            migrationBuilder.Sql(
                $"UPDATE \"Attendances\" SET \"WorkDate\" = (\"CheckIn\" AT TIME ZONE 'UTC' + INTERVAL '{(int)offset.TotalMinutes} minutes')::date;");

            // Check-out is now the shared work end time: clear the old camera "last seen" check-outs
            // so DailyCheckoutService closes those days with the common check-out time.
            migrationBuilder.Sql("UPDATE \"Attendances\" SET \"CheckOut\" = NULL WHERE \"CameraId\" IS NOT NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_EmployeeId_WorkDate",
                table: "Attendances",
                columns: new[] { "EmployeeId", "WorkDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendances_EmployeeId_WorkDate",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "WorkDate",
                table: "Attendances");

            migrationBuilder.AddColumn<int>(
                name: "AttendanceUpdateIntervalMinutes",
                table: "SystemSettings",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
