using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Minio.FileSystem.Backend.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileSystems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileSystems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileSystemItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileSystemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: true),
                    ContentType = table.Column<string>(type: "text", nullable: true),
                    ExternalUrl = table.Column<string>(type: "text", nullable: true),
                    VirtualPath = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    FileSystemItemType = table.Column<int>(type: "integer", nullable: false),
                    MetaProperties = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileSystemItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileSystemItems_FileSystemItems_ParentId",
                        column: x => x.ParentId,
                        principalTable: "FileSystemItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FileSystemItems_FileSystems_FileSystemId",
                        column: x => x.FileSystemId,
                        principalTable: "FileSystems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileSystemItems_FileSystemId",
                table: "FileSystemItems",
                column: "FileSystemId");

            migrationBuilder.CreateIndex(
                name: "IX_FileSystemItems_ParentId",
                table: "FileSystemItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_FileSystemItems_TenantId",
                table: "FileSystemItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FileSystemItems_VirtualPath",
                table: "FileSystemItems",
                column: "VirtualPath");

            migrationBuilder.CreateIndex(
                name: "IX_FileSystems_TenantId",
                table: "FileSystems",
                column: "TenantId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileSystemItems");

            migrationBuilder.DropTable(
                name: "FileSystems");
        }
    }
}
