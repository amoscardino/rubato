using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rubato.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectWorkItemId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkItemId",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkItemId",
                table: "Projects");
        }
    }
}
