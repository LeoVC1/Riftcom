using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiftboundStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCardRarity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Rarity",
                table: "Cards",
                type: "TEXT",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rarity",
                table: "Cards");
        }
    }
}
