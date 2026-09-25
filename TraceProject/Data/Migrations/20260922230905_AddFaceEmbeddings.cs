using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraceProject.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceEmbeddings : Migration
    {
        /// <inheritdoc />
        /// NOTE: This migration includes:
        /// 1. New FaceEmbeddings table for Phase 4 face recognition (core change)
        /// 2. Unrelated schema additions for Locations, Cameras, and Attendances tables
        ///    (These appear due to prior uncommitted model changes. Review and apply carefully.)
        /// Consider splitting this into two migrations if you want to apply face recognition
        /// without affecting other entities.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Locations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Locations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Locations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LocationId",
                table: "Employees",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LocationId",
                table: "Cameras",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RtspUrl",
                table: "Cameras",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Transport",
                table: "Cameras",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Cameras",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CameraId",
                table: "Attendances",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Attendances",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<long>(
                name: "LocationId1",
                table: "Attendances",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Attendances",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FaceEmbeddings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EmployeeId = table.Column<long>(type: "bigint", nullable: false),
                    EmbeddingData = table.Column<byte[]>(type: "bytea", nullable: false),
                    ModelName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EmbeddingDimension = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaceEmbeddings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaceEmbeddings_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_LocationId",
                table: "Employees",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_LocationId",
                table: "Cameras",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_CameraId",
                table: "Attendances",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_LocationId1",
                table: "Attendances",
                column: "LocationId1");

            migrationBuilder.CreateIndex(
                name: "IX_FaceEmbeddings_EmployeeId_IsActive",
                table: "FaceEmbeddings",
                columns: new[] { "EmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FaceEmbeddings_IsActive",
                table: "FaceEmbeddings",
                column: "IsActive");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Cameras_CameraId",
                table: "Attendances",
                column: "CameraId",
                principalTable: "Cameras",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Attendances_Locations_LocationId1",
                table: "Attendances",
                column: "LocationId1",
                principalTable: "Locations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Cameras_Locations_LocationId",
                table: "Cameras",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Locations_LocationId",
                table: "Employees",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Cameras_CameraId",
                table: "Attendances");

            migrationBuilder.DropForeignKey(
                name: "FK_Attendances_Locations_LocationId1",
                table: "Attendances");

            migrationBuilder.DropForeignKey(
                name: "FK_Cameras_Locations_LocationId",
                table: "Cameras");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Locations_LocationId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "FaceEmbeddings");

            migrationBuilder.DropIndex(
                name: "IX_Employees_LocationId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Cameras_LocationId",
                table: "Cameras");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_CameraId",
                table: "Attendances");

            migrationBuilder.DropIndex(
                name: "IX_Attendances_LocationId1",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "RtspUrl",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Transport",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "CameraId",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "LocationId1",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Attendances");
        }
    }
}
