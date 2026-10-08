using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

using Npgsql;

using QuickPatch.Catalog.Infrastructure.Persistence;
using QuickPatch.Catalog.UnitTests.Support;

using Testcontainers.Kafka;
using Testcontainers.PostgreSql;

namespace QuickPatch.Catalog.IntegrationTests;

/// <summary>PostgreSQL 16 y Kafka reales, con migraciones, roles del DD 10.2 y el servicio como <c>catalog_app</c>.</summary>
public sealed class CatalogEnvironment : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine").WithDatabase("db_catalog").Build();
    private readonly KafkaContainer kafka = new KafkaBuilder("confluentinc/cp-kafka:7.7.1").Build();

    public string AdminConnectionString => postgres.GetConnectionString();

    public string AppConnectionString => new NpgsqlConnectionStringBuilder(AdminConnectionString)
    {
        Username = "catalog_app",
        Password = "app-pruebas",
    }.ConnectionString;

    public string KafkaServers => kafka.GetBootstrapAddress();

    public WebApplicationFactory<Program> App { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(postgres.StartAsync(), kafka.StartAsync());
        await using (var db = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseNpgsql(AdminConnectionString).Options))
        {
            await db.Database.MigrateAsync();
        }

        await ExecuteAdminAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "db", "roles.sql")));
        await ExecuteAdminAsync("ALTER ROLE catalog_app LOGIN PASSWORD 'app-pruebas';");
        using (var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = KafkaServers }).Build())
        {
            await admin.CreateTopicsAsync([new TopicSpecification { Name = "catalog.category-changed", NumPartitions = 1, ReplicationFactor = 1 }]);
        }

        App = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Catalog", AppConnectionString);
            b.UseSetting("Kafka:BootstrapServers", KafkaServers);
            b.UseSetting("Outbox:PublisherRole", "catalog_outbox");
            b.UseSetting("Outbox:PollInterval", "00:00:00.200");
            b.UseSetting("Jwt:PublicKeyPem", TestTokens.PublicKeyPem);
        });
        _ = App.Server;
    }

    public async Task DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }

        await Task.WhenAll(postgres.DisposeAsync().AsTask(), kafka.DisposeAsync().AsTask());
    }

    public async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public HttpClient Client(string role, Guid tenant)
    {
        var client = App.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Create(Guid.NewGuid(), tenant, role));
        return client;
    }

    /// <summary>Lee de Kafka hasta encontrar <paramref name="count"/> mensajes con la clave dada.</summary>
    public List<JsonElement> Consume(string key, int count)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = KafkaServers,
            GroupId = $"pruebas-{Guid.NewGuid()}",
            AutoOffsetReset = AutoOffsetReset.Earliest,
        }).Build();
        consumer.Subscribe("catalog.category-changed");
        var found = new List<JsonElement>();
        var limit = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < limit && found.Count < count)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(1));
            if (result?.Message.Key == key)
            {
                using var doc = JsonDocument.Parse(result.Message.Value);
                found.Add(doc.RootElement.Clone());
            }
        }

        consumer.Close();
        return found;
    }
}

[CollectionDefinition(Name)]
public sealed class CatalogDefinition : ICollectionFixture<CatalogEnvironment>
{
    public const string Name = "catalog-real";
}

[Collection(CatalogDefinition.Name)]
public class CatalogFlowTests(CatalogEnvironment env)
{
    private static readonly Uri Admin = new("/v1/catalog/admin/categories", UriKind.Relative);

    [Fact]
    public async Task CrearYDesactivar_PublicaElEstadoCompletoEnKafka()
    {
        var tenant = Guid.NewGuid();
        var correlation = Guid.NewGuid();
        using var admin = env.Client("admin_tenant", tenant);
        admin.DefaultRequestHeaders.Add("X-Correlation-Id", correlation.ToString());

        using var created = await admin.PostAsJsonAsync(Admin, new { name = "Plomería" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var patched = await admin.PatchAsJsonAsync(new Uri($"/v1/catalog/admin/categories/{id}", UriKind.Relative), new { active = false });

        var events = env.Consume(id.ToString(), 2);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        Assert.Equal(2, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal("catalog.category-changed", e.GetProperty("eventType").GetString());
            Assert.Equal("catalog-service", e.GetProperty("producer").GetString());
            Assert.Equal(tenant, e.GetProperty("tenantId").GetGuid());
            Assert.Equal(correlation, e.GetProperty("correlationId").GetGuid());
            Assert.Equal(id, e.GetProperty("data").GetProperty("categoryId").GetGuid());
        });
        Assert.True(events[0].GetProperty("data").GetProperty("active").GetBoolean());
        Assert.False(events[1].GetProperty("data").GetProperty("active").GetBoolean());
        Assert.True(events[1].GetProperty("data").GetProperty("updatedAt").GetDateTimeOffset()
            >= events[0].GetProperty("data").GetProperty("updatedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task ListadoAisladoPorTenant_YNombreUnicoEnLaBase()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using var adminA = env.Client("admin_tenant", tenantA);
        using var adminB = env.Client("admin_tenant", tenantB);
        using (await adminA.PostAsJsonAsync(Admin, new { name = "Cerrajería" }))
        {
        }

        using var listB = await env.Client("cliente", tenantB).GetAsync(new Uri("/v1/catalog/categories", UriKind.Relative));
        using var mismoNombreEnB = await adminB.PostAsJsonAsync(Admin, new { name = "Cerrajería" });
        using var repetidoEnA = await adminA.PostAsJsonAsync(Admin, new { name = "cerrajería" });

        Assert.Empty((await listB.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        Assert.Equal(HttpStatusCode.Created, mismoNombreEnB.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, repetidoEnA.StatusCode);
    }

    [Fact]
    public async Task Rls_ElRolDelServicioNoVeNiEscribeEnOtroTenant()
    {
        var tenant = Guid.NewGuid();
        using (await env.Client("admin_tenant", tenant).PostAsJsonAsync(Admin, new { name = "Electricidad" }))
        {
        }

        await using var connection = new NpgsqlConnection(env.AppConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant', @t, true)", connection, transaction))
        {
            set.Parameters.AddWithValue("t", Guid.NewGuid().ToString());
            await set.ExecuteNonQueryAsync();
        }

        await using var count = new NpgsqlCommand("SELECT count(*) FROM service_categories", connection, transaction);
        Assert.Equal(0L, (long)(await count.ExecuteScalarAsync())!);

        await using var insert = new NpgsqlCommand(
            "INSERT INTO service_categories (id, tenant_id, name, active, created_at, updated_at) VALUES (@id, @t, 'X', true, now(), now())",
            connection,
            transaction);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("t", tenant);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal("42501", ex.SqlState);
    }

    [Fact]
    public async Task HealthReady_ConPostgreSql_RespondeOk()
    {
        using var response = await env.App.CreateClient().GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}