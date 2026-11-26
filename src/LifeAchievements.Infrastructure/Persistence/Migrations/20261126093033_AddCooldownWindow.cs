using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeAchievements.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCooldownWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CooldownWindow",
                table: "achievements",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CooldownWindow",
                table: "achievements");
        }
    }
}
