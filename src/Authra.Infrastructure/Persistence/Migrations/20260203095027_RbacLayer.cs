using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RbacLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.permission_id);
                    table.ForeignKey(
                        name: "FK_permissions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_restricted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.role_id);
                    table.ForeignKey(
                        name: "FK_roles_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_organizations",
                columns: table => new
                {
                    role_organization_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_organizations", x => x.role_organization_id);
                    table.ForeignKey(
                        name: "FK_role_organizations_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "organization_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_organizations_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_organizations_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_permission_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => x.role_permission_id);
                    table.ForeignKey(
                        name: "FK_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "permission_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permissions_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_member_roles",
                columns: table => new
                {
                    tenant_member_role_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    tenant_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_member_roles", x => x.tenant_member_role_id);
                    table.ForeignKey(
                        name: "FK_tenant_member_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_member_roles_tenant_members_assigned_by",
                        column: x => x.assigned_by,
                        principalTable: "tenant_members",
                        principalColumn: "tenant_member_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tenant_member_roles_tenant_members_tenant_member_id",
                        column: x => x.tenant_member_id,
                        principalTable: "tenant_members",
                        principalColumn: "tenant_member_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tenant_member_roles_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_permissions_system_code",
                table: "permissions",
                column: "code",
                filter: "tenant_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "uk_permissions_tenant_code",
                table: "permissions",
                columns: new[] { "tenant_id", "code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "IX_role_organizations_organization_id",
                table: "role_organizations",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_organizations_tenant_id",
                table: "role_organizations",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uk_role_organizations_role_org",
                table: "role_organizations",
                columns: new[] { "role_id", "organization_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_role_id",
                table: "role_permissions",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_tenant_id",
                table: "role_permissions",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uk_role_permissions_role_permission",
                table: "role_permissions",
                columns: new[] { "role_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roles_tenant_default",
                table: "roles",
                column: "tenant_id",
                filter: "is_default = true");

            migrationBuilder.CreateIndex(
                name: "uk_roles_tenant_code",
                table: "roles",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenant_member_roles_assigned_by",
                table: "tenant_member_roles",
                column: "assigned_by");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_member_roles_member_id",
                table: "tenant_member_roles",
                column: "tenant_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_member_roles_role_id",
                table: "tenant_member_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_member_roles_tenant_id",
                table: "tenant_member_roles",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "uk_tenant_member_roles_member_role",
                table: "tenant_member_roles",
                columns: new[] { "tenant_member_id", "role_id" },
                unique: true);

            // Enable Row-Level Security on tenant-scoped RBAC tables
            migrationBuilder.Sql(@"
                -- Enable RLS on roles table
                ALTER TABLE roles ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation_roles ON roles
                    FOR ALL
                    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

                -- Enable RLS on role_permissions table
                ALTER TABLE role_permissions ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation_role_permissions ON role_permissions
                    FOR ALL
                    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

                -- Enable RLS on role_organizations table
                ALTER TABLE role_organizations ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation_role_organizations ON role_organizations
                    FOR ALL
                    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);

                -- Enable RLS on tenant_member_roles table
                ALTER TABLE tenant_member_roles ENABLE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation_tenant_member_roles ON tenant_member_roles
                    FOR ALL
                    USING (tenant_id = current_setting('app.current_tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.current_tenant_id', true)::uuid);
            ");

            // Seed 19 MVP system permissions
            // These are global permissions (tenant_id = NULL) that cannot be modified
            migrationBuilder.Sql(@"
                INSERT INTO permissions (permission_id, tenant_id, code, name, description, category, is_system, created_at, updated_at)
                VALUES
                    -- Tenant permissions
                    (uuidv7(), NULL, 'tenant:read', 'View Tenant Settings', 'Allows viewing tenant settings and configuration', 'Tenant', true, now(), now()),
                    (uuidv7(), NULL, 'tenant:update', 'Update Tenant Settings', 'Allows modifying tenant settings and configuration', 'Tenant', true, now(), now()),
                    (uuidv7(), NULL, 'tenant:transfer', 'Transfer Ownership', 'Allows transferring tenant ownership to another member', 'Tenant', true, now(), now()),

                    -- Account permissions
                    (uuidv7(), NULL, 'accounts:read', 'View Accounts', 'Allows viewing tenant member accounts', 'Accounts', true, now(), now()),
                    (uuidv7(), NULL, 'accounts:invite', 'Invite Accounts', 'Allows inviting new members to the tenant', 'Accounts', true, now(), now()),
                    (uuidv7(), NULL, 'accounts:update', 'Update Accounts', 'Allows modifying tenant member accounts', 'Accounts', true, now(), now()),
                    (uuidv7(), NULL, 'accounts:suspend', 'Suspend Accounts', 'Allows suspending tenant member accounts', 'Accounts', true, now(), now()),
                    (uuidv7(), NULL, 'accounts:remove', 'Remove Accounts', 'Allows removing members from the tenant', 'Accounts', true, now(), now()),

                    -- Organization permissions
                    (uuidv7(), NULL, 'organizations:read', 'View Organizations', 'Allows viewing organizations within the tenant', 'Organizations', true, now(), now()),
                    (uuidv7(), NULL, 'organizations:create', 'Create Organizations', 'Allows creating new organizations within the tenant', 'Organizations', true, now(), now()),
                    (uuidv7(), NULL, 'organizations:update', 'Update Organizations', 'Allows modifying organization settings', 'Organizations', true, now(), now()),
                    (uuidv7(), NULL, 'organizations:delete', 'Delete Organizations', 'Allows deleting organizations from the tenant', 'Organizations', true, now(), now()),
                    (uuidv7(), NULL, 'organizations:members.read', 'View Org Members', 'Allows viewing organization member lists', 'Organizations', true, now(), now()),
                    (uuidv7(), NULL, 'organizations:members.write', 'Manage Org Members', 'Allows adding and removing organization members', 'Organizations', true, now(), now()),

                    -- Role permissions
                    (uuidv7(), NULL, 'roles:read', 'View Roles', 'Allows viewing roles and their permissions', 'Roles', true, now(), now()),
                    (uuidv7(), NULL, 'roles:create', 'Create Roles', 'Allows creating new roles within the tenant', 'Roles', true, now(), now()),
                    (uuidv7(), NULL, 'roles:update', 'Update Roles', 'Allows modifying role settings and permissions', 'Roles', true, now(), now()),
                    (uuidv7(), NULL, 'roles:delete', 'Delete Roles', 'Allows deleting roles from the tenant', 'Roles', true, now(), now()),
                    (uuidv7(), NULL, 'roles:assign', 'Assign Roles', 'Allows assigning and unassigning roles to members', 'Roles', true, now(), now())
                ON CONFLICT DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop RLS policies before dropping tables
            migrationBuilder.Sql(@"
                DROP POLICY IF EXISTS tenant_isolation_tenant_member_roles ON tenant_member_roles;
                DROP POLICY IF EXISTS tenant_isolation_role_organizations ON role_organizations;
                DROP POLICY IF EXISTS tenant_isolation_role_permissions ON role_permissions;
                DROP POLICY IF EXISTS tenant_isolation_roles ON roles;
            ");

            migrationBuilder.DropTable(
                name: "role_organizations");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "tenant_member_roles");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
