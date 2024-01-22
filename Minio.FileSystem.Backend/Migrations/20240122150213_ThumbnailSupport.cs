using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Minio.FileSystem.Backend.Migrations
{
    public partial class ThumbnailSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ThumbnailsProcessed",
                table: "FileSystemItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Thumbnails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileSystemItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThumbnailType = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: true),
                    ContentType = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Thumbnails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Thumbnails_FileSystemItems_FileSystemItemId",
                        column: x => x.FileSystemItemId,
                        principalTable: "FileSystemItems",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_FileSystemItemId",
                table: "Thumbnails",
                column: "FileSystemItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_TenantId",
                table: "Thumbnails",
                column: "TenantId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Thumbnails");

            migrationBuilder.DropColumn(
                name: "ThumbnailsProcessed",
                table: "FileSystemItems");
        }
    }
}
