using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authra.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionTokenLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ownership_transfers",
                columns: table => new
                {
                    transfer_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    from_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    initiated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "text", nullable: true),
                    initiated_by_ip = table.Column<IPAddress>(type: "inet", nullable: true),
                    completed_by_ip = table.Column<IPAddress>(type: "inet", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ownership_transfers", x => x.transfer_id);
                    table.CheckConstraint("ck_ownership_transfers_status", "status IN ('pending', 'completed', 'cancelled', 'expired')");
                    table.ForeignKey(
                        name: "FK_ownership_transfers_tenant_members_completed_by_member_id",
                        column: x => x.completed_by_member_id,
                        principalTable: "tenant_members",
                        principalColumn: "tenant_member_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_tenant_members_from_member_id",
                        column: x => x.from_member_id,
                        principalTable: "tenant_members",
                        principalColumn: "tenant_member_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_tenant_members_to_member_id",
                        column: x => x.to_member_id,
                        principalTable: "tenant_members",
                        principalColumn: "tenant_member_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ownership_transfers_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    refresh_token_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    generation = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    device_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    absolute_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.refresh_token_id);
                    table.CheckConstraint("ck_refresh_tokens_revoked_reason", "revoked_reason IS NULL OR revoked_reason IN ('logout', 'rotation', 'reuse_detected', 'admin', 'password_change')");
                    table.ForeignKey(
                        name: "FK_refresh_tokens_tenant_members_tenant_member_id",
                        column: x => x.tenant_member_id,
                        principalTable: "tenant_members",
                        principalColumn: "tenant_member_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "signing_keys",
                columns: table => new
                {
                    key_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    algorithm = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ES256"),
                    public_key_pem = table.Column<string>(type: "text", nullable: false),
                    private_key_encrypted = table.Column<byte[]>(type: "bytea", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rotated_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signing_keys", x => x.key_id);
                    table.CheckConstraint("ck_signing_keys_algorithm", "algorithm IN ('ES256', 'ES384', 'RS256')");
                    table.CheckConstraint("ck_signing_keys_status", "status IN ('pending', 'active', 'rotate_out', 'expired')");
                });

            migrationBuilder.CreateTable(
                name: "token_blacklist",
                columns: table => new
                {
                    blacklist_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    jti = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reason = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_token_blacklist", x => x.blacklist_id);
                    table.CheckConstraint("ck_token_blacklist_reason", "reason IN ('logout', 'password_change', 'admin', 'security')");
                    table.ForeignKey(
                        name: "FK_token_blacklist_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_token_blacklist_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_completed_by_member_id",
                table: "ownership_transfers",
                column: "completed_by_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_from_member_id",
                table: "ownership_transfers",
                column: "from_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_tenant_id",
                table: "ownership_transfers",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_ownership_transfers_to_member_id",
                table: "ownership_transfers",
                column: "to_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_active",
                table: "refresh_tokens",
                columns: new[] { "user_id", "tenant_id" },
                filter: "revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family_id",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_tenant_id",
                table: "refresh_tokens",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_tenant_member_id",
                table: "refresh_tokens",
                column: "tenant_member_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_signing_keys_status",
                table: "signing_keys",
                column: "status",
                filter: "status IN ('active', 'rotate_out')");

            migrationBuilder.CreateIndex(
                name: "ix_token_blacklist_expiry",
                table: "token_blacklist",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_token_blacklist_jti",
                table: "token_blacklist",
                column: "jti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_token_blacklist_tenant_id",
                table: "token_blacklist",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_token_blacklist_user_id",
                table: "token_blacklist",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ownership_transfers");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "signing_keys");

            migrationBuilder.DropTable(
                name: "token_blacklist");
        }
    }
}
