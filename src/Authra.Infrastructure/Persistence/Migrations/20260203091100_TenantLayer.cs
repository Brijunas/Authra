using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_tenant_members_user_id",
                table: "tenant_members",
                newName: "ix_tenant_members_user_id");

            // Create tenants table WITHOUT the owner_member_id FK initially
            // The FK will be added later as DEFERRABLE to handle circular reference
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    owner_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    allow_custom_permissions = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allow_org_restrictions = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.tenant_id);
                    table.CheckConstraint("ck_tenants_status", "status IN ('active', 'suspended', 'deleted')");
                });

            migrationBuilder.CreateTable(
                name: "invites",
                columns: table => new
                {
                    invite_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    invited_by_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false, defaultValueSql: "'{}'"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invites", x => x.invite_id);
                    table.CheckConstraint("ck_invites_status", "status IN ('pending', 'accepted', 'expired', 'cancelled')");
                    table.ForeignKey(
                        name: "FK_invites_tenant_members_invited_by_member_id",
                        column: x => x.invited_by_member_id,
                        principalTable: "tenant_members",
                        principalColumn: "tenant_member_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_invites_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.organization_id);
                    table.CheckConstraint("ck_organizations_status", "status IN ('active', 'archived', 'deleted')");
                    table.ForeignKey(
                        name: "FK_organizations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_members",
                columns: table => new
                {
                    organization_member_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_members", x => x.organization_member_id);
                    table.ForeignKey(
                        name: "FK_organization_members_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "organization_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_members_tenant_members_tenant_member_id",
                        column: x => x.tenant_member_id,
                        principalTable: "tenant_members",
                        principalColumn: "tenant_member_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_members_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_members_status",
                table: "tenant_members",
                columns: new[] { "tenant_id", "status" },
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_invites_invited_by_member_id",
                table: "invites",
                column: "invited_by_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_invites_tenant_id_email",
                table: "invites",
                columns: new[] { "tenant_id", "email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invites_tenant_pending",
                table: "invites",
                column: "tenant_id",
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_invites_token",
                table: "invites",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_members_organization_id_tenant_member_id",
                table: "organization_members",
                columns: new[] { "organization_id", "tenant_member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organization_members_tenant_id",
                table: "organization_members",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_organization_members_tenant_member_id",
                table: "organization_members",
                column: "tenant_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_organizations_tenant_id_slug",
                table: "organizations",
                columns: new[] { "tenant_id", "slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_owner_member_id",
                table: "tenants",
                column: "owner_member_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenants_slug",
                table: "tenants",
                column: "slug",
                unique: true);

            // Add FK from tenant_members to tenants (standard constraint)
            migrationBuilder.AddForeignKey(
                name: "FK_tenant_members_tenants_tenant_id",
                table: "tenant_members",
                column: "tenant_id",
                principalTable: "tenants",
                principalColumn: "tenant_id",
                onDelete: ReferentialAction.Cascade);

            // Add DEFERRABLE FK from tenants.owner_member_id to tenant_members
            // This handles the circular reference: Tenant <-> TenantMember
            // DEFERRABLE INITIALLY DEFERRED allows both records to be inserted
            // within a transaction before FK validation occurs
            migrationBuilder.Sql(@"
                ALTER TABLE tenants
                ADD CONSTRAINT ""FK_tenants_tenant_members_owner_member_id""
                FOREIGN KEY (owner_member_id) REFERENCES tenant_members(tenant_member_id)
                ON DELETE SET NULL
                DEFERRABLE INITIALLY DEFERRED;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tenant_members_tenants_tenant_id",
                table: "tenant_members");

            // Drop the deferred FK constraint
            migrationBuilder.Sql(@"
                ALTER TABLE tenants
                DROP CONSTRAINT IF EXISTS ""FK_tenants_tenant_members_owner_member_id"";
            ");

            migrationBuilder.DropTable(
                name: "invites");

            migrationBuilder.DropTable(
                name: "organization_members");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_tenant_members_status",
                table: "tenant_members");

            migrationBuilder.RenameIndex(
                name: "ix_tenant_members_user_id",
                table: "tenant_members",
                newName: "IX_tenant_members_user_id");
        }
    }
}
