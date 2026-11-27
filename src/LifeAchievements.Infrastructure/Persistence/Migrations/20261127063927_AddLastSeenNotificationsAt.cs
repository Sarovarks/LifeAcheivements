using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LifeAchievements.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSeenNotificationsAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSeenNotificationsAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSeenNotificationsAt",
                table: "AspNetUsers");
        }
    }
}
