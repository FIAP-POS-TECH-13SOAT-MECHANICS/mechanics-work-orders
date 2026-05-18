using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mechanics.Infra.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaidAtToWorkOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAt",
                schema: "Mechanics",
                table: "WorkOrders",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAt",
                schema: "Mechanics",
                table: "WorkOrders");
        }
    }
}
