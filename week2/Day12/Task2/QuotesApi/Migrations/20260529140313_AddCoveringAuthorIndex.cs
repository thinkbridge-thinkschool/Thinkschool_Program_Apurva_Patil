using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuotesApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCoveringAuthorIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quotes_Author",
                table: "Quotes");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Author",
                table: "Quotes",
                column: "Author")
                .Annotation("SqlServer:Include", new[] { "Text", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Quotes_Author",
                table: "Quotes");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Author",
                table: "Quotes",
                column: "Author");
        }
    }
}
