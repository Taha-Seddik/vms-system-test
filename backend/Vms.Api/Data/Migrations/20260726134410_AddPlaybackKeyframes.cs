using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackKeyframes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecordingKeyframes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimestampSeconds = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordingKeyframes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecordingKeyframes_Recordings_RecordingId",
                        column: x => x.RecordingId,
                        principalTable: "Recordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecordingKeyframes_RecordingId_TimestampSeconds",
                table: "RecordingKeyframes",
                columns: new[] { "RecordingId", "TimestampSeconds" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecordingKeyframes");
        }
    }
}
