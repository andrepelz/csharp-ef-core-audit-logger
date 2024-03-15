using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ListChangeTracking.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OtherEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtherEntity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestEntities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TestEntity_Name = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<int>(type: "integer", nullable: false),
                    Value1 = table.Column<string>(type: "text", nullable: false),
                    Value2 = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InnerEntity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TestEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InnerEntity", x => new { x.TestEntityId, x.Id });
                    table.ForeignKey(
                        name: "FK_InnerEntity_TestEntities_TestEntityId",
                        column: x => x.TestEntityId,
                        principalTable: "TestEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestEntityOtherEntity",
                columns: table => new
                {
                    TestEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OtherEntityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestEntityOtherEntity", x => new { x.TestEntityId, x.OtherEntityId });
                    table.ForeignKey(
                        name: "FK_TestEntityOtherEntity_OtherEntity_OtherEntityId",
                        column: x => x.OtherEntityId,
                        principalTable: "OtherEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestEntityOtherEntity_TestEntities_TestEntityId",
                        column: x => x.TestEntityId,
                        principalTable: "TestEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestEntityOtherEntity_OtherEntityId",
                table: "TestEntityOtherEntity",
                column: "OtherEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InnerEntity");

            migrationBuilder.DropTable(
                name: "TestEntityOtherEntity");

            migrationBuilder.DropTable(
                name: "OtherEntity");

            migrationBuilder.DropTable(
                name: "TestEntities");
        }
    }
}
