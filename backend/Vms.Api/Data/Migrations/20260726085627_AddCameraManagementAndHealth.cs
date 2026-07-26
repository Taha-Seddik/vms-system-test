using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vms.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraManagementAndHealth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CameraGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CameraGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cameras",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Location = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    RtspUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    HlsPath = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConnectionStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RecordingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResolutionWidth = table.Column<int>(type: "integer", nullable: true),
                    ResolutionHeight = table.Column<int>(type: "integer", nullable: true),
                    FramesPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastConnectionError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cameras", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cameras_CameraGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CameraGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            var seededAt = new DateTimeOffset(
                2026,
                7,
                26,
                0,
                0,
                0,
                TimeSpan.Zero);

            migrationBuilder.InsertData(
                table: "CameraGroups",
                columns: new[] { "Id", "Name", "Description", "CreatedAt", "UpdatedAt" },
                values: new object[,]
                {
                    {
                        new Guid("20000000-0000-0000-0000-000000000001"),
                        "Perimeter",
                        "Public entrances and parking areas",
                        seededAt,
                        seededAt
                    },
                    {
                        new Guid("20000000-0000-0000-0000-000000000002"),
                        "Operations",
                        "Loading and warehouse operations",
                        seededAt,
                        seededAt
                    }
                });

            migrationBuilder.InsertData(
                table: "Cameras",
                columns: new[]
                {
                    "Id",
                    "Name",
                    "Location",
                    "RtspUrl",
                    "HlsPath",
                    "GroupId",
                    "IsEnabled",
                    "ConnectionStatus",
                    "RecordingStatus",
                    "CreatedAt",
                    "UpdatedAt"
                },
                values: new object[,]
                {
                    {
                        "camera-1",
                        "Entrance",
                        "Main entrance",
                        "rtsp://mediamtx:8554/camera-1",
                        "/camera-1/index.m3u8",
                        new Guid("20000000-0000-0000-0000-000000000001"),
                        true,
                        "Unknown",
                        "NotRecording",
                        seededAt,
                        seededAt
                    },
                    {
                        "camera-2",
                        "Loading Bay",
                        "Logistics area",
                        "rtsp://mediamtx:8554/camera-2",
                        "/camera-2/index.m3u8",
                        new Guid("20000000-0000-0000-0000-000000000002"),
                        true,
                        "Unknown",
                        "NotRecording",
                        seededAt,
                        seededAt
                    },
                    {
                        "camera-3",
                        "Parking",
                        "Visitor parking",
                        "rtsp://mediamtx:8554/camera-3",
                        "/camera-3/index.m3u8",
                        new Guid("20000000-0000-0000-0000-000000000001"),
                        true,
                        "Unknown",
                        "NotRecording",
                        seededAt,
                        seededAt
                    },
                    {
                        "camera-4",
                        "Warehouse",
                        "Storage floor",
                        "rtsp://mediamtx:8554/camera-4",
                        "/camera-4/index.m3u8",
                        new Guid("20000000-0000-0000-0000-000000000002"),
                        true,
                        "Unknown",
                        "NotRecording",
                        seededAt,
                        seededAt
                    }
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCameraAssignments_CameraId",
                table: "UserCameraAssignments",
                column: "CameraId");

            migrationBuilder.CreateIndex(
                name: "IX_CameraGroups_Name",
                table: "CameraGroups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_GroupId",
                table: "Cameras",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_IsEnabled_ConnectionStatus",
                table: "Cameras",
                columns: new[] { "IsEnabled", "ConnectionStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_Name",
                table: "Cameras",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_UserCameraAssignments_Cameras_CameraId",
                table: "UserCameraAssignments",
                column: "CameraId",
                principalTable: "Cameras",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCameraAssignments_Cameras_CameraId",
                table: "UserCameraAssignments");

            migrationBuilder.DropTable(
                name: "Cameras");

            migrationBuilder.DropTable(
                name: "CameraGroups");

            migrationBuilder.DropIndex(
                name: "IX_UserCameraAssignments_CameraId",
                table: "UserCameraAssignments");
        }
    }
}
