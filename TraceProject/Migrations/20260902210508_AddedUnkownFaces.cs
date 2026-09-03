using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraceProject.Migrations
{
    /// <inheritdoc />
    public partial class AddedUnkownFaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnkownFaces",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    face = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnkownFaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnknownFaceLocation",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UnknownFaceId = table.Column<long>(type: "INTEGER", nullable: false),
                    LocationId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnknownFaceLocation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnknownFaceLocation_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UnknownFaceLocation_UnkownFaces_UnknownFaceId",
                        column: x => x.UnknownFaceId,
                        principalTable: "UnkownFaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnknownFaceLocation_LocationId",
                table: "UnknownFaceLocation",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_UnknownFaceLocation_UnknownFaceId",
                table: "UnknownFaceLocation",
                column: "UnknownFaceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnknownFaceLocation");

            migrationBuilder.DropTable(
                name: "UnkownFaces");
        }
    }
}
