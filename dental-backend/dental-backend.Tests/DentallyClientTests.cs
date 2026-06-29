using System.Net;
using System.Text;
using System.Web;
using dental_backend.Dentally;
using dental_backend.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace dental_backend.Tests;

public class DentallyClientTests
{
    private static readonly DateRange Range = new(new DateOnly(2026, 6, 24), new DateOnly(2026, 6, 24));

    private static DentallyClient Create(StubHandler handler, int pageSize = 2)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.dentally.co/v1/") };
        var options = Options.Create(new DentallyOptions { PageSize = pageSize, MaxConcurrency = 4 });
        return new DentallyClient(http, options, NullLogger<DentallyClient>.Instance);
    }

    [Fact]
    public async Task Retrieves_all_pages()
    {
        // per_page = 2, total = 5  ->  3 pages.
        var handler = new StubHandler((page, _) => page switch
        {
            1 => Ok(Page(total: 5, ids: new[] { 1, 2 })),
            2 => Ok(Page(total: 5, ids: new[] { 3, 4 })),
            3 => Ok(Page(total: 5, ids: new[] { 5 })),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var result = await Create(handler).GetAppointmentsAsync("tok", Range, CancellationToken.None);

        Assert.Equal(5, result.Count);
        Assert.Equal(new[] { "1", "2", "3", "4", "5" }, result.Select(a => a.Id));
    }

    [Fact]
    public async Task Deduplicates_overlapping_ids_across_pages()
    {
        // per_page = 2, total = 3  ->  2 pages.
        var handler = new StubHandler((page, _) => page switch
        {
            1 => Ok(Page(total: 3, ids: new[] { 1, 2 })),
            2 => Ok(Page(total: 3, ids: new[] { 2, 3 })), // 2 repeats
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var result = await Create(handler).GetAppointmentsAsync("tok", Range, CancellationToken.None);

        Assert.Equal(new[] { "1", "2", "3" }, result.Select(a => a.Id));
    }

    [Fact]
    public async Task Surfaces_error_when_a_page_fails()
    {
        // per_page = 2, total = 3  ->  2 pages; the second one fails.
        var handler = new StubHandler((page, _) => page switch
        {
            1 => Ok(Page(total: 3, ids: new[] { 1 })),
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("nope") }
        });

        var ex = await Assert.ThrowsAsync<DentallyApiException>(() =>
            Create(handler).GetAppointmentsAsync("tok", Range, CancellationToken.None));
        Assert.Equal(500, ex.StatusCode);
    }

    [Fact]
    public async Task Sends_bearer_token()
    {
        string? auth = null;
        var handler = new StubHandler((page, req) =>
        {
            auth = req.Headers.Authorization?.ToString();
            return Ok(Page(total: 1, ids: new[] { 1 }));
        });

        await Create(handler).GetAppointmentsAsync("secret", Range, CancellationToken.None);

        Assert.Equal("Bearer secret", auth);
    }

    [Fact]
    public async Task Builds_after_before_and_pagination_query()
    {
        string? query = null;
        var handler = new StubHandler((page, req) =>
        {
            query ??= req.RequestUri!.Query;
            return Ok(Page(total: 1, ids: new[] { 1 }));
        });

        await Create(handler).GetAppointmentsAsync("tok", Range, CancellationToken.None);

        Assert.Contains("after=", query);
        Assert.Contains("before=", query);
        Assert.Contains("page=1", query);
        Assert.Contains("per_page=2", query);
    }

    [Fact]
    public async Task Aborts_when_count_never_reconciles()
    {
        // meta.total = 5 but only 4 unique ids are ever returned -> mismatch on every attempt.
        var handler = new StubHandler((page, _) => page switch
        {
            1 => Ok(Page(total: 5, ids: new[] { 1, 2 })),
            2 => Ok(Page(total: 5, ids: new[] { 3, 4 })),
            3 => Ok(Page(total: 5, ids: Array.Empty<int>())),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });

        var ex = await Assert.ThrowsAsync<DentallyApiException>(() =>
            Create(handler).GetAppointmentsAsync("tok", Range, CancellationToken.None));
        Assert.Contains("count mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Recovers_when_count_reconciles_on_retry()
    {
        // First full pass is short (mismatch); the retry returns the complete set.
        var attempt = 0;
        var handler = new StubHandler((page, _) =>
        {
            if (page == 1) attempt++; // a new full-range pass always starts at page 1
            bool complete = attempt >= 2;
            return page switch
            {
                1 => Ok(Page(total: 3, ids: new[] { 1, 2 })),
                2 => Ok(Page(total: 3, ids: complete ? new[] { 3 } : Array.Empty<int>())),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        });

        var result = await Create(handler).GetAppointmentsAsync("tok", Range, CancellationToken.None);

        Assert.Equal(new[] { "1", "2", "3" }, result.Select(a => a.Id));
    }

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string Page(int total, int[] ids)
    {
        var items = string.Join(",", ids.Select(id =>
            "{\"id\":" + id + ",\"start_time\":\"2026-06-24T09:00:00Z\",\"duration\":30,\"state\":\"Booked\"}"));
        return "{\"appointments\":[" + items + "],\"meta\":{\"total\":" + total + ",\"page\":1}}";
    }

    private sealed class StubHandler(Func<int, HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var query = HttpUtility.ParseQueryString(request.RequestUri!.Query);
            int page = int.Parse(query["page"] ?? "1");
            return Task.FromResult(responder(page, request));
        }
    }
}
