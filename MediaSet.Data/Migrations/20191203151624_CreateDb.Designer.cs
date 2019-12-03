﻿// <auto-generated />
using System;
using MediaSet.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MediaSet.Data.Migrations
{
    [DbContext(typeof(MediaSetContext))]
    [Migration("20191203151624_CreateDb")]
    partial class CreateDb
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.0.1");

            modelBuilder.Entity("MediaSet.Data.Format", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("MediaTypeId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Formats");
                });

            modelBuilder.Entity("MediaSet.Data.Genre", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("MediaTypeId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("MediaTypeId");

                    b.ToTable("Genres");
                });

            modelBuilder.Entity("MediaSet.Data.Media", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Barcode")
                        .HasColumnType("TEXT");

                    b.Property<int>("FormatId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ISBN")
                        .HasColumnType("TEXT");

                    b.Property<int>("MediaTypeId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Title")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("FormatId")
                        .IsUnique();

                    b.ToTable("Media");
                });

            modelBuilder.Entity("MediaSet.Data.MediaGenre", b =>
                {
                    b.Property<int>("GenreId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MediaId")
                        .HasColumnType("INTEGER");

                    b.HasKey("GenreId", "MediaId");

                    b.HasIndex("MediaId");

                    b.ToTable("MediaGenre");
                });

            modelBuilder.Entity("MediaSet.Data.MediaType", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("MediaTypes");
                });

            modelBuilder.Entity("MediaSet.Data.MovieData.Movie", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("IMDBLink")
                        .HasColumnType("TEXT");

                    b.Property<int>("MediaId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Plot")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("ReleaseDate")
                        .HasColumnType("TEXT");

                    b.Property<int>("Runtime")
                        .HasColumnType("INTEGER");

                    b.Property<string>("SortTitle")
                        .HasColumnType("TEXT");

                    b.Property<string>("SubTitle")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("MediaId");

                    b.ToTable("Movies");
                });

            modelBuilder.Entity("MediaSet.Data.MovieData.MovieStudio", b =>
                {
                    b.Property<int>("MovieId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("StudioId")
                        .HasColumnType("INTEGER");

                    b.HasKey("MovieId", "StudioId");

                    b.HasIndex("StudioId");

                    b.ToTable("MovieStudio");
                });

            modelBuilder.Entity("MediaSet.Data.MovieData.Studio", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Studios");
                });

            modelBuilder.Entity("MediaSet.Data.Genre", b =>
                {
                    b.HasOne("MediaSet.Data.MediaType", null)
                        .WithMany("Genres")
                        .HasForeignKey("MediaTypeId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MediaSet.Data.Media", b =>
                {
                    b.HasOne("MediaSet.Data.Format", null)
                        .WithOne("Media")
                        .HasForeignKey("MediaSet.Data.Media", "FormatId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MediaSet.Data.MediaGenre", b =>
                {
                    b.HasOne("MediaSet.Data.Genre", "Genre")
                        .WithMany("MediaGenres")
                        .HasForeignKey("GenreId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MediaSet.Data.Media", "Media")
                        .WithMany("MediaGenres")
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MediaSet.Data.MovieData.Movie", b =>
                {
                    b.HasOne("MediaSet.Data.Media", "Media")
                        .WithMany()
                        .HasForeignKey("MediaId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MediaSet.Data.MovieData.MovieStudio", b =>
                {
                    b.HasOne("MediaSet.Data.MovieData.Movie", "Movie")
                        .WithMany("MovieStudios")
                        .HasForeignKey("MovieId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MediaSet.Data.MovieData.Studio", "Studio")
                        .WithMany("MovieStudios")
                        .HasForeignKey("StudioId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
