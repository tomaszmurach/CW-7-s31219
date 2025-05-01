using Microsoft.Data.SqlClient;
using WebApplication1.Exceptions;
using WebApplication1.Models;
using WebApplication1.Models.DTOs;

namespace WebApplication1.Services;


public interface IDbService
{
    Task<IEnumerable<TripGetDTO>> GetTripsAsync(); //Zwraca liste wszystkich wycieczek z podstawowymi informacjami
    Task<IEnumerable<ClientTripsGetDTO>> GetTripsByClientIdAsync(int id); //Zwraca wycieczki na ktore zapisany jest klient
    Task<Client> CreateClientAsync(ClientCreateDTO dto); //Tworzy nowego klienta w bazie danych
    Task RegisterClientToTripAsync(int clientId, int tripId); //Rejestruje klienta na konkretna wycieczke, w razie bledu zwraca odpowiedni status i komunikat
    Task RemoveClientFromTripAsync(int clientId, int tripId); //Usuwa rejestracje klienta z wycieczki w bazie danych




}



public class DbService(IConfiguration config) : IDbService
{
    private readonly string? _connectionString = config.GetConnectionString("DefaultConnection");
    
    public async Task<IEnumerable<TripGetDTO>> GetTripsAsync()
    {
        var result = new List<TripGetDTO>();
    
        // Pobranie informacji o wycieczce, wraz z krajami
        await using var connection = new SqlConnection(_connectionString);
        const string sql = @"
        SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
               STRING_AGG(c.Name, ', ') AS Countries
        FROM Trip t
        LEFT JOIN Country_Trip ct ON ct.IdTrip = t.IdTrip
        LEFT JOIN Country c ON c.IdCountry = ct.IdCountry
        GROUP BY t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople";
    
        await using var command = new SqlCommand(sql, connection);
        await connection.OpenAsync();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new TripGetDTO
            {
                IdTrip = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                DateFrom = reader.GetDateTime(3),
                DateTo = reader.GetDateTime(4),
                MaxPeople = reader.GetInt32(5),
                Countries = reader.IsDBNull(6) ? "" : reader.GetString(6)
            });
        }

        return result;
    }
    
    public async Task<IEnumerable<ClientTripsGetDTO>> GetTripsByClientIdAsync(int clientId)
    {
        var result = new List<ClientTripsGetDTO>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        
        //Sprawdzenie czy klient istnieje
        const string checkSql = "SELECT 1 FROM Client WHERE IdClient = @id";
        await using (var checkCmd = new SqlCommand(checkSql, connection))
        {
            checkCmd.Parameters.AddWithValue("@id", clientId);
            var exists = await checkCmd.ExecuteScalarAsync();
            if (exists is null)
            {
                throw new NotFoundException($"Client with ID {clientId} does not exist");
            }
        }

        
        //Pobranie informacji o wycieczkach klienta o wybranym id wraz z danymi o rejestracji i platnosci
        const string sql = @"
    SELECT t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
           STRING_AGG(c.Name, ', ') AS Countries,
           ct.RegisteredAt,
           ct.PaymentDate
    FROM Trip t
    JOIN Client_Trip ct ON ct.IdTrip = t.IdTrip
    LEFT JOIN Country_Trip ct2 ON ct2.IdTrip = t.IdTrip
    LEFT JOIN Country c ON c.IdCountry = ct2.IdCountry
    WHERE ct.IdClient = @id
    GROUP BY t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople, ct.RegisteredAt, ct.PaymentDate";


        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", clientId);
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(new ClientTripsGetDTO
            {
                IdTrip = reader.GetInt32(0),
                Name = reader.GetString(1),
                Description = reader.GetString(2),
                DateFrom = reader.GetDateTime(3),
                DateTo = reader.GetDateTime(4),
                MaxPeople = reader.GetInt32(5),
                Countries = reader.IsDBNull(6) ? "" : reader.GetString(6),
                RegisteredAt = reader.GetInt32(7),
                PaymentDate = reader.IsDBNull(8) ? null : reader.GetInt32(8)
            });




        }

        return result;
    }
    
    
    public async Task<Client> CreateClientAsync(ClientCreateDTO dto)
    {
        
        //Dodanie nowego klienta do bazy danych i utworzenie mu unikalnego ID
        await using var connection = new SqlConnection(_connectionString);
        const string sql = @"
        INSERT INTO Client (FirstName, LastName, Email, Pesel, Telephone)
        VALUES (@FirstName, @LastName, @Email, @Pesel, @Telephone);
        SELECT SCOPE_IDENTITY();";

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@FirstName", dto.FirstName);
        command.Parameters.AddWithValue("@LastName", dto.LastName);
        command.Parameters.AddWithValue("@Email", dto.Email);
        command.Parameters.AddWithValue("@Pesel", dto.Pesel);
        command.Parameters.AddWithValue("@Telephone", dto.Telephone);

        await connection.OpenAsync();
        var newId = Convert.ToInt32(await command.ExecuteScalarAsync());

        return new Client
        {
            IdClient = newId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            Pesel = dto.Pesel,
            Telephone = dto.Telephone
        };
    }

    
    public async Task RegisterClientToTripAsync(int clientId, int tripId)
{
    await using var connection = new SqlConnection(_connectionString);
    await connection.OpenAsync();


    //Sprawdzenie czy klient o podanym id istnieje
    const string checkClientSql = "SELECT 1 FROM Client WHERE IdClient = @id";
    await using (var cmd = new SqlCommand(checkClientSql, connection))
    {
        cmd.Parameters.AddWithValue("@id", clientId);
        var exists = await cmd.ExecuteScalarAsync();
        if (exists is null)
            throw new NotFoundException($"Client with ID {clientId} does not exist");
    }

    //Sprawdzenie czy wycieczka o podanym id istnieje
    const string checkTripSql = "SELECT MaxPeople FROM Trip WHERE IdTrip = @id";
    int maxPeople;
    await using (var cmd = new SqlCommand(checkTripSql, connection))
    {
        cmd.Parameters.AddWithValue("@id", tripId);
        var result = await cmd.ExecuteScalarAsync();
        if (result is null)
            throw new NotFoundException($"Trip with ID {tripId} does not exist");

        maxPeople = (int)result;
    }

    //Sprawdzenie czy klient jest juz zarejestrowany na ta wycieczke
    const string checkDupSql = "SELECT 1 FROM Client_Trip WHERE IdClient = @clientId AND IdTrip = @tripId";
    await using (var cmd = new SqlCommand(checkDupSql, connection))
    {
        cmd.Parameters.AddWithValue("@clientId", clientId);
        cmd.Parameters.AddWithValue("@tripId", tripId);
        var exists = await cmd.ExecuteScalarAsync();
        if (exists is not null)
            throw new InvalidOperationException("Client is already registered for this trip");
    }
    
    //Sprawdzenie czy liczba aktualnych uczestnikow nie jest juz pelna
    const string countSql = "SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @tripId";
    int currentCount;
    await using (var cmd = new SqlCommand(countSql, connection))
    {
        cmd.Parameters.AddWithValue("@tripId", tripId);
        currentCount = (int)await cmd.ExecuteScalarAsync();
    }

    if (currentCount >= maxPeople)
        throw new InvalidOperationException("Trip has reached maximum number of participants");

    //Zapisanie klienta na wycieczke i dodanie danych do bazy
    const string insertSql = @"
        INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt, PaymentDate)
        VALUES (@clientId, @tripId, @registeredAt, NULL)";
    await using (var cmd = new SqlCommand(insertSql, connection))
    {
        cmd.Parameters.AddWithValue("@clientId", clientId);
        cmd.Parameters.AddWithValue("@tripId", tripId);
        cmd.Parameters.AddWithValue("@registeredAt", DateTime.Now.ToString("yyyyMMdd"));
        await cmd.ExecuteNonQueryAsync();
    }
}
    
    
    public async Task RemoveClientFromTripAsync(int clientId, int tripId)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        
        //Sprawdzenie czy klient jest zarejestrowany na dana wycieczke
        const string checkSql = "SELECT 1 FROM Client_Trip WHERE IdClient = @clientId AND IdTrip = @tripId";
        await using (var cmd = new SqlCommand(checkSql, connection))
        {
            cmd.Parameters.AddWithValue("@clientId", clientId);
            cmd.Parameters.AddWithValue("@tripId", tripId);

            var exists = await cmd.ExecuteScalarAsync();
            if (exists is null)
                throw new NotFoundException("This registration does not exist.");
        }
        
        //Usuniecie klienta z wycieczki
        const string deleteSql = "DELETE FROM Client_Trip WHERE IdClient = @clientId AND IdTrip = @tripId";
        await using (var cmd = new SqlCommand(deleteSql, connection))
        {
            cmd.Parameters.AddWithValue("@clientId", clientId);
            cmd.Parameters.AddWithValue("@tripId", tripId);
            await cmd.ExecuteNonQueryAsync();
        }
    }


    

}