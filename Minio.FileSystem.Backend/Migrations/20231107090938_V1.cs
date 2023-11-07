using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Minio.Filesystem.Backend.Migrations
{
    public partial class V1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileSystems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileSystemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VirtualPath = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    FileSystemItemType = table.Column<int>(type: "int", nullable: false),
                    MetaProperties = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileSystemItems", x => x.Id);
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
