using Microsoft.EntityFrameworkCore.Migrations;

namespace MediaSet.Data.Migrations
{
    public partial class MakeISBNString : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ISBN",
                table: "Books",
                nullable: true,
                oldClrType: typeof(int));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ISBN",
                table: "Books",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);
        }
    }
}
