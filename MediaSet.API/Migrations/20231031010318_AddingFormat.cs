using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaSet.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddingFormat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FormatId",
                table: "Books",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Formats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Formats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Books_FormatId",
                table: "Books",
                column: "FormatId");

            migrationBuilder.AddForeignKey(
                name: "FK_Books_Formats_FormatId",
                table: "Books",
                column: "FormatId",
                principalTable: "Formats",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Books_Formats_FormatId",
                table: "Books");

            migrationBuilder.DropTable(
                name: "Formats");

            migrationBuilder.DropIndex(
                name: "IX_Books_FormatId",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "FormatId",
                table: "Books");
        }
    }
}
