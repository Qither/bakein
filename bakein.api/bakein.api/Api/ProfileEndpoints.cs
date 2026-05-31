using Bakein.Api.Infrastructure;
using Bakein.Api.Security;
using Npgsql;

namespace Bakein.Api.Api;

public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder routes)
    {
        var group = routes.MapGroup("/users/me").WithTags("Profile");

        group.MapGet("/addresses", GetAddressesAsync);
        group.MapPost("/addresses", CreateAddressAsync);

        return routes;
    }

    private static async Task<IResult> GetAddressesAsync(HttpContext httpContext, NpgsqlDataSource db, CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var addresses = await db.QueryAsync(
            """
            select id, contact_name, phone, province, city, district, detail, is_default
            from profile_addresses
            where account_id = @account_id
            order by is_default desc, created_at desc
            """,
            MapAddress,
            [Pg.Param("account_id", user.Id)],
            cancellationToken);

        return Results.Ok(addresses);
    }

    private static async Task<IResult> CreateAddressAsync(
        ProfileAddressRequest request,
        HttpContext httpContext,
        NpgsqlDataSource db,
        CancellationToken cancellationToken)
    {
        var user = httpContext.GetAuthenticatedUser();
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ContactName) ||
            string.IsNullOrWhiteSpace(request.Phone) ||
            string.IsNullOrWhiteSpace(request.Detail))
        {
            return Results.BadRequest(new ApiError("invalid_address", "Contact name, phone, and detail are required."));
        }

        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (request.IsDefault)
        {
            await using var resetCommand = new NpgsqlCommand(
                "update profile_addresses set is_default = false where account_id = @account_id",
                connection,
                transaction);
            resetCommand.Parameters.Add(Pg.Param("account_id", user.Id));
            await resetCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        ProfileAddressDto? address;
        await using (var command = new NpgsqlCommand(
            """
            insert into profile_addresses (
              account_id, contact_name, phone, province, city, district, detail, is_default
            ) values (
              @account_id, @contact_name, @phone, @province, @city, @district, @detail, @is_default
            )
            returning id, contact_name, phone, province, city, district, detail, is_default
            """,
            connection,
            transaction))
        {
            command.Parameters.Add(Pg.Param("account_id", user.Id));
            command.Parameters.Add(Pg.Param("contact_name", request.ContactName.Trim()));
            command.Parameters.Add(Pg.Param("phone", request.Phone.Trim()));
            command.Parameters.Add(Pg.Param("province", request.Province.Trim()));
            command.Parameters.Add(Pg.Param("city", request.City.Trim()));
            command.Parameters.Add(Pg.Param("district", request.District.Trim()));
            command.Parameters.Add(Pg.Param("detail", request.Detail.Trim()));
            command.Parameters.Add(Pg.Param("is_default", request.IsDefault));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            address = await reader.ReadAsync(cancellationToken) ? MapAddress(reader) : null;
        }

        await transaction.CommitAsync(cancellationToken);
        return address is null ? Results.Problem("Failed to create address.") : Results.Created($"/api/users/me/addresses/{address.Id}", address);
    }

    private static ProfileAddressDto MapAddress(NpgsqlDataReader reader) =>
        new(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("contact_name")),
            reader.GetString(reader.GetOrdinal("phone")),
            reader.GetString(reader.GetOrdinal("province")),
            reader.GetString(reader.GetOrdinal("city")),
            reader.GetString(reader.GetOrdinal("district")),
            reader.GetString(reader.GetOrdinal("detail")),
            reader.GetBoolean(reader.GetOrdinal("is_default")));
}
