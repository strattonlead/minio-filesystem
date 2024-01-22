﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Minio.FileSystem.Backend;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Minio.FileSystem.Backend.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20240122150213_ThumbnailSupport")]
    partial class ThumbnailSupport
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.24")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Minio.FileSystem.Backend.FileSystemEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.Property<long?>("TenantId")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("TenantId");

                    b.ToTable("FileSystems");
                });

            modelBuilder.Entity("Minio.FileSystem.Backend.FileSystemItemEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("ContentType")
                        .HasColumnType("text");

                    b.Property<string>("ExternalUrl")
                        .HasColumnType("text");

                    b.Property<Guid>("FileSystemId")
                        .HasColumnType("uuid");

                    b.Property<int>("FileSystemItemType")
                        .HasColumnType("integer");

                    b.Property<string>("MetaProperties")
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.Property<Guid?>("ParentId")
                        .HasColumnType("uuid");

                    b.Property<long?>("SizeInBytes")
                        .HasColumnType("bigint");

                    b.Property<long?>("TenantId")
                        .HasColumnType("bigint");

                    b.Property<bool>("ThumbnailsProcessed")
                        .HasColumnType("boolean");

                    b.Property<string>("VirtualPath")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("FileSystemId");

                    b.HasIndex("ParentId");

                    b.HasIndex("TenantId");

                    b.HasIndex("VirtualPath");

                    b.ToTable("FileSystemItems");
                });

            modelBuilder.Entity("Minio.FileSystem.Backend.ThumbnailEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<string>("ContentType")
                        .HasColumnType("text");

                    b.Property<Guid>("FileSystemItemId")
                        .HasColumnType("uuid");

                    b.Property<int>("Height")
                        .HasColumnType("integer");

                    b.Property<long?>("SizeInBytes")
                        .HasColumnType("bigint");

                    b.Property<long?>("TenantId")
                        .HasColumnType("bigint");

                    b.Property<int>("ThumbnailType")
                        .HasColumnType("integer");

                    b.Property<int>("Width")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("FileSystemItemId");

                    b.HasIndex("TenantId");

                    b.ToTable("Thumbnails");
                });

            modelBuilder.Entity("Minio.FileSystem.Backend.FileSystemItemEntity", b =>
                {
                    b.HasOne("Minio.FileSystem.Backend.FileSystemEntity", "FileSystem")
                        .WithMany("FileSystemItems")
                        .HasForeignKey("FileSystemId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Minio.FileSystem.Backend.FileSystemItemEntity", "Parent")
                        .WithMany("Children")
                        .HasForeignKey("ParentId")
                        .OnDelete(DeleteBehavior.ClientCascade);

                    b.Navigation("FileSystem");

                    b.Navigation("Parent");
                });

            modelBuilder.Entity("Minio.FileSystem.Backend.ThumbnailEntity", b =>
                {
                    b.HasOne("Minio.FileSystem.Backend.FileSystemItemEntity", "FileSystemItem")
                        .WithMany("Thumbnails")
                        .HasForeignKey("FileSystemItemId")
                        .OnDelete(DeleteBehavior.ClientCascade)
                        .IsRequired();

                    b.Navigation("FileSystemItem");
                });

            modelBuilder.Entity("Minio.FileSystem.Backend.FileSystemEntity", b =>
                {
                    b.Navigation("FileSystemItems");
                });

            modelBuilder.Entity("Minio.FileSystem.Backend.FileSystemItemEntity", b =>
                {
                    b.Navigation("Children");

                    b.Navigation("Thumbnails");
                });
#pragma warning restore 612, 618
        }
    }
}