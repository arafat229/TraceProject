using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraceProject.Data.Migrations
{
    /// <inheritdoc />
    public partial class FaceImagesUnknownFaceDetailsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CameraId",
                table: "UnknownFaces",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DetectedAtUtc",
                table: "UnknownFaces",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<float>(
                name: "DetectionScore",
                table: "UnknownFaces",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<byte[]>(
                name: "FaceImage",
                table: "FaceEmbeddings",
                type: "bytea",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecognitionThreshold = table.Column<float>(type: "real", nullable: false),
                    WorkStartTime = table.Column<string>(type: "text", nullable: false),
                    WorkEndTime = table.Column<string>(type: "text", nullable: false),
                    LateGraceMinutes = table.Column<int>(type: "integer", nullable: false),
                    AttendanceUpdateIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "CameraId",
                table: "UnknownFaces");

            migrationBuilder.DropColumn(
                name: "DetectedAtUtc",
                table: "UnknownFaces");

            migrationBuilder.DropColumn(
                name: "DetectionScore",
                table: "UnknownFaces");

            migrationBuilder.DropColumn(
                name: "FaceImage",
                table: "FaceEmbeddings");
        }
    }
}
