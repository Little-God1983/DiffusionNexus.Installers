using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiffusionNexus.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class SageAttention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstallationConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Repository_Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Repository_RepositoryUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Repository_Branch = table.Column<string>(type: "TEXT", nullable: false),
                    Repository_CommitHash = table.Column<string>(type: "TEXT", nullable: false),
                    Python_PythonVersion = table.Column<string>(type: "TEXT", nullable: false),
                    Python_InterpreterPathOverride = table.Column<string>(type: "TEXT", nullable: false),
                    Python_CreateVirtualEnvironment = table.Column<bool>(type: "INTEGER", nullable: false),
                    Python_CreateVramSettings = table.Column<bool>(type: "INTEGER", nullable: false),
                    Python_VirtualEnvironmentName = table.Column<string>(type: "TEXT", nullable: false),
                    Python_InstallTriton = table.Column<bool>(type: "INTEGER", nullable: false),
                    Python_InstallSageAttention = table.Column<bool>(type: "INTEGER", nullable: false),
                    Torch_TorchVersion = table.Column<string>(type: "TEXT", nullable: false),
                    Torch_CudaVersion = table.Column<string>(type: "TEXT", nullable: false),
                    Torch_IndexUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Paths_RootDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    Paths_DefaultModelDownloadDirectory = table.Column<string>(type: "TEXT", nullable: true),
                    Paths_LogFileName = table.Column<string>(type: "TEXT", nullable: false),
                    Vram_VramProfiles = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstallationConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitRepository",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    InstallRequirements = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstallationConfigurationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitRepository", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GitRepository_InstallationConfigurations_InstallationConfigurationId",
                        column: x => x.InstallationConfigurationId,
                        principalTable: "InstallationConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelDownload",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    Destination = table.Column<string>(type: "TEXT", nullable: false),
                    VramProfile = table.Column<int>(type: "INTEGER", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    InstallationConfigurationId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelDownload", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelDownload_InstallationConfigurations_InstallationConfigurationId",
                        column: x => x.InstallationConfigurationId,
                        principalTable: "InstallationConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelDownloadLink",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    VramProfile = table.Column<int>(type: "INTEGER", nullable: true),
                    Destination = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModelDownloadId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelDownloadLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelDownloadLink_ModelDownload_ModelDownloadId",
                        column: x => x.ModelDownloadId,
                        principalTable: "ModelDownload",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitRepository_InstallationConfigurationId",
                table: "GitRepository",
                column: "InstallationConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_InstallationConfigurations_Name",
                table: "InstallationConfigurations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelDownload_InstallationConfigurationId",
                table: "ModelDownload",
                column: "InstallationConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelDownloadLink_ModelDownloadId",
                table: "ModelDownloadLink",
                column: "ModelDownloadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitRepository");

            migrationBuilder.DropTable(
                name: "ModelDownloadLink");

            migrationBuilder.DropTable(
                name: "ModelDownload");

            migrationBuilder.DropTable(
                name: "InstallationConfigurations");
        }
    }
}
