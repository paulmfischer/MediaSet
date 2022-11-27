using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Genre_Movies_MovieId",
                table: "Genre");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaItems_Format_FormatId",
                table: "MediaItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Movies_Studio_StudioId",
                table: "Movies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Studio",
                table: "Studio");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Genre",
                table: "Genre");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Format",
                table: "Format");

            migrationBuilder.RenameTable(
                name: "Studio",
                newName: "Studios");

            migrationBuilder.RenameTable(
                name: "Genre",
                newName: "Genres");

            migrationBuilder.RenameTable(
                name: "Format",
                newName: "Formats");

            migrationBuilder.RenameIndex(
                name: "IX_Genre_MovieId",
                table: "Genres",
                newName: "IX_Genres_MovieId");

            migrationBuilder.AddColumn<int>(
                name: "MediaType",
                table: "Studios",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MediaType",
                table: "Genres",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MediaType",
                table: "Formats",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Studios",
                table: "Studios",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Genres",
                table: "Genres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Formats",
                table: "Formats",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Genres_Movies_MovieId",
                table: "Genres",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_Formats_FormatId",
                table: "MediaItems",
                column: "FormatId",
                principalTable: "Formats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_Studios_StudioId",
                table: "Movies",
                column: "StudioId",
                principalTable: "Studios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Genres_Movies_MovieId",
                table: "Genres");

            migrationBuilder.DropForeignKey(
                name: "FK_MediaItems_Formats_FormatId",
                table: "MediaItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Movies_Studios_StudioId",
                table: "Movies");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Studios",
                table: "Studios");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Genres",
                table: "Genres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Formats",
                table: "Formats");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Genres");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Formats");

            migrationBuilder.RenameTable(
                name: "Studios",
                newName: "Studio");

            migrationBuilder.RenameTable(
                name: "Genres",
                newName: "Genre");

            migrationBuilder.RenameTable(
                name: "Formats",
                newName: "Format");

            migrationBuilder.RenameIndex(
                name: "IX_Genres_MovieId",
                table: "Genre",
                newName: "IX_Genre_MovieId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Studio",
                table: "Studio",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Genre",
                table: "Genre",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Format",
                table: "Format",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Genre_Movies_MovieId",
                table: "Genre",
                column: "MovieId",
                principalTable: "Movies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_Format_FormatId",
                table: "MediaItems",
                column: "FormatId",
                principalTable: "Format",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Movies_Studio_StudioId",
                table: "Movies",
                column: "StudioId",
                principalTable: "Studio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
