using Dapper;
using TemperatureApi.Applications.Interfaces;
using TemperatureApi.Models;
using System.Data;
using Polly;
using Polly.Retry;
using TemperatureApi.Infrastructure.Policies;

namespace TemperatureApi.Infrastructure.Repositories;

public class TemperatureRepository : ITemperatureRepository
{
    private readonly IDbConnection _db;

    private readonly AsyncRetryPolicy _retryPolicy;

    public TemperatureRepository(IDbConnection db, AsyncRetryPolicy retryPolicy)
    {
        _db = db;
        _retryPolicy = retryPolicy;
    }

    public async Task<IEnumerable<TemperatureDto>> GetPagedAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var offset = (page - 1) * pageSize;
        var sql = @"
                SELECT id,
                    value AS Temperature,
                    recorded_at AS RecordedAt
                FROM temperatures
                ORDER BY recorded_at DESC
                LIMIT @PageSize OFFSET @Offset";

        return await _retryPolicy.ExecuteAsync(async () =>
            await _db.QueryAsync<TemperatureDto>(
                sql,
                new { PageSize = pageSize, Offset = offset })
        );
    }

}
