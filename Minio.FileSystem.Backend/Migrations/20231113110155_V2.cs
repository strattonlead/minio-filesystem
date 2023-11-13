using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Minio.FileSystem.Backend.Migrations
{
    public partial class V2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "FileSystemItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileSystemItems_ParentId",
                table: "FileSystemItems",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_FileSystemItems_FileSystemItems_ParentId",
                table: "FileSystemItems",
                column: "ParentId",
                principalTable: "FileSystemItems",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileSystemItems_FileSystemItems_ParentId",
                table: "FileSystemItems");

            migrationBuilder.DropIndex(
                name: "IX_FileSystemItems_ParentId",
                table: "FileSystemItems");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "FileSystemItems");
        }
    }
}
