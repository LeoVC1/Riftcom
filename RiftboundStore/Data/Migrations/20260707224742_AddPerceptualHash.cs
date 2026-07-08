using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RiftboundStore.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPerceptualHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PerceptualHash",
                table: "Cards",
                type: "TEXT",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PerceptualHash",
                table: "Cards");
        }
    }
}
