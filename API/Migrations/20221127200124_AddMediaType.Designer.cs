﻿// <auto-generated />
using System;
using API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace API.Migrations
{
    [DbContext(typeof(MediaSetContext))]
    [Migration("20221127200124_AddMediaType")]
    partial class AddMediaType
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.0");

            modelBuilder.Entity("API.Data.Format", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("MediaType")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Formats");
                });

            modelBuilder.Entity("API.Data.Genre", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("MediaType")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("MovieId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("MovieId");

                    b.ToTable("Genres");
                });

            modelBuilder.Entity("API.Data.MediaItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Barcode")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("FormatId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("FormatId");

                    b.ToTable("MediaItems");
                });

            modelBuilder.Entity("API.Data.Movie", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsTvSeries")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MediaItemId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Plot")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ReleaseDate")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Runtime")
                        .HasColumnType("INTEGER");

                    b.Property<int>("StudioId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("MediaItemId");

                    b.HasIndex("StudioId");

                    b.ToTable("Movies");
                });

            modelBuilder.Entity("API.Data.Studio", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("MediaType")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Studios");
                });

            modelBuilder.Entity("API.Data.Genre", b =>
                {
                    b.HasOne("API.Data.Movie", null)
                        .WithMany("Genres")
                        .HasForeignKey("MovieId");
                });

            modelBuilder.Entity("API.Data.MediaItem", b =>
                {
                    b.HasOne("API.Data.Format", "Format")
                        .WithMany()
                        .HasForeignKey("FormatId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Format");
                });

            modelBuilder.Entity("API.Data.Movie", b =>
                {
                    b.HasOne("API.Data.MediaItem", "MediaItem")
                        .WithMany()
                        .HasForeignKey("MediaItemId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("API.Data.Studio", "Studio")
                        .WithMany()
                        .HasForeignKey("StudioId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("MediaItem");

                    b.Navigation("Studio");
                });

            modelBuilder.Entity("API.Data.Movie", b =>
                {
                    b.Navigation("Genres");
                });
#pragma warning restore 612, 618
        }
    }
}