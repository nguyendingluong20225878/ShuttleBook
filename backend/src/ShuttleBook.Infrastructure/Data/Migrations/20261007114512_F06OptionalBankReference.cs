using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShuttleBook.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class F06OptionalBankReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_decision_valid",
                table: "payment_decisions");

            migrationBuilder.AlterColumn<string>(
                name: "bank_reference",
                table: "payment_evidence",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddCheckConstraint(
                name: "ck_decision_valid",
                table: "payment_decisions",
                sql: "(resolution='CONFIRMED' AND confirmed_amount IS NOT NULL AND confirmed_amount >= 0) OR (resolution IN ('NEEDS_REVIEW','FINAL_REJECTION') AND reason_code IS NOT NULL AND reason IS NOT NULL AND length(btrim(reason))>0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Do not invent bank references or overwrite evidence when reverting the old contract.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM payment_evidence WHERE bank_reference IS NULL)
                    OR EXISTS (SELECT 1 FROM payment_decisions WHERE resolution='CONFIRMED' AND bank_reference IS NULL) THEN
                    RAISE EXCEPTION 'Cannot downgrade optional bank references while newer evidence or confirmations exist';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_decision_valid",
                table: "payment_decisions");

            migrationBuilder.AlterColumn<string>(
                name: "bank_reference",
                table: "payment_evidence",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_decision_valid",
                table: "payment_decisions",
                sql: "(resolution='CONFIRMED' AND confirmed_amount IS NOT NULL AND confirmed_amount >= 0 AND bank_reference IS NOT NULL) OR (resolution IN ('NEEDS_REVIEW','FINAL_REJECTION') AND reason_code IS NOT NULL AND reason IS NOT NULL AND length(btrim(reason))>0)");
        }
    }
}
