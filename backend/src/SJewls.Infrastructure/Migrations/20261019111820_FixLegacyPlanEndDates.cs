using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SJewls.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixLegacyPlanEndDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""ChituPlans"" 
                SET ""EndDate"" = (""StartDate"" + (((""DurationMonths"" - 1) || ' month')::interval))::date 
                WHERE ""EndDate"" <= ""StartDate"" OR ""EndDate"" = '-infinity'::date;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
