using Npgsql;
using ProcessingWorker.Domain;

namespace ProcessingWorker.Infrastructure.Persistence;

public class TemperatureRepository
{
    private readonly string _connectionString;

    public TemperatureRepository(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("Postgres");
    }

    public async Task SaveAsync(TemperatureRecord record)
    {
        try{
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            Console.WriteLine("Connected successfully!");
            
            var cmd = new NpgsqlCommand(
                "INSERT INTO temperatures(value, recorded_at) VALUES(@value, @time)",
                conn);


            cmd.Parameters.AddWithValue("value", record.Value);
            cmd.Parameters.AddWithValue("time", record.RecordedAt);

            await cmd.ExecuteNonQueryAsync();

        }catch (Exception ex){
            Console.WriteLine(ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
}