using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Migrations
{
    /// <inheritdoc />
    public partial class FixingMediaTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Studios");

            migrationBuilder.DropColumn(
                name: "MediaType",
                table: "Genres");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
