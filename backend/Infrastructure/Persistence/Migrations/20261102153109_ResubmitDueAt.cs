using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Backend.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResubmitDueAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<DateTimeOffset>>(
                name: "ResubmitDueAt",
                table: "ContentLinks",
                type: "timestamp with time zone[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResubmitDueAt",
                table: "ContentLinks");
        }
    }
}
