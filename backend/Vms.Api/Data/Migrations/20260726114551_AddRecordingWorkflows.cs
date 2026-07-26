using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordingWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Recordings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CameraId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<double>(type: "double precision", nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerEventId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recordings_Cameras_CameraId",
                        column: x => x.CameraId,
                        principalTable: "Cameras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_CameraId_StartedAt",
                table: "Recordings",
                columns: new[] { "CameraId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Recordings_State_StartedAt",
                table: "Recordings",
                columns: new[] { "State", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recordings");
        }
    }
}
