using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiftboundStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Domains",
                table: "Cards",
                type: "TEXT",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Domains",
                table: "Cards");
        }
    }
}
