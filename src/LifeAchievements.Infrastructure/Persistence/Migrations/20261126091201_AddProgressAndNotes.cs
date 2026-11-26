using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeAchievements.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProgressAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "user_achievements",
                type: "character varying(280)",
                maxLength: 280,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressUnit",
                table: "achievements",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProgressWindow",
                table: "achievements",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "TargetCount",
                table: "achievements",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "achievement_progress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AchievementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Current = table.Column<int>(type: "integer", nullable: false),
                    WindowStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_achievement_progress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_achievement_progress_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_achievement_progress_achievements_AchievementId",
                        column: x => x.AchievementId,
                        principalTable: "achievements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_achievement_progress_AchievementId",
                table: "achievement_progress",
                column: "AchievementId");

            migrationBuilder.CreateIndex(
                name: "IX_achievement_progress_UserId_AchievementId",
                table: "achievement_progress",
                columns: new[] { "UserId", "AchievementId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "achievement_progress");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "user_achievements");

            migrationBuilder.DropColumn(
                name: "ProgressUnit",
                table: "achievements");

            migrationBuilder.DropColumn(
                name: "ProgressWindow",
                table: "achievements");

            migrationBuilder.DropColumn(
                name: "TargetCount",
                table: "achievements");
        }
    }
}
