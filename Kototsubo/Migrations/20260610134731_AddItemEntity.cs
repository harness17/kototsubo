using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Site.Migrations
{
    /// <inheritdoc />
    public partial class AddItemEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ItemHistories",
                columns: table => new
                {
                    HistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DelFlag = table.Column<bool>(type: "bit", nullable: false),
                    UpdateApplicationUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateApplicationUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MediaType = table.Column<int>(type: "int", nullable: false),
                    OwnershipStatus = table.Column<int>(type: "int", nullable: false),
                    IsDigital = table.Column<bool>(type: "bit", nullable: false),
                    AcquisitionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    Memo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ISBN = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    JANCode = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    ASIN = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Creator = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Format = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    DiscCount = table.Column<int>(type: "int", nullable: true),
                    Runtime = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemHistories", x => x.HistoryId);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DelFlag = table.Column<bool>(type: "bit", nullable: false),
                    UpdateApplicationUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateApplicationUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MediaType = table.Column<int>(type: "int", nullable: false),
                    OwnershipStatus = table.Column<int>(type: "int", nullable: false),
                    IsDigital = table.Column<bool>(type: "bit", nullable: false),
                    AcquisitionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: true),
                    Memo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ISBN = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    JANCode = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    ASIN = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Creator = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Publisher = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Format = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    DiscCount = table.Column<int>(type: "int", nullable: true),
                    Runtime = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItemHistories");

            migrationBuilder.DropTable(
                name: "Items");
        }
    }
}
