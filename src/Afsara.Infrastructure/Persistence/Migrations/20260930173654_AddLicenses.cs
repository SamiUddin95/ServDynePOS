using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Afsara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLicenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Licenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LicenseKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Licensee = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    MachineId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastSeenUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licenses", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Licenses");
        }
    }
}
