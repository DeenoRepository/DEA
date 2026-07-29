using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EquipmentFailureAnalysis.Services
{
    public sealed class JiraImportRequest
    {
        public string Url { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Jql { get; set; } = string.Empty;
        public IReadOnlyCollection<string> FilterIds { get; set; } = Array.Empty<string>();
        public int PageSize { get; set; } = 1000;
        public int? TotalResultsLimit { get; set; }
        public bool EnsureLatestOrdering { get; set; }
    }

    public sealed class JiraImportResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public ObservableCollection<EquipmentInfo> Items { get; set; } = new ObservableCollection<EquipmentInfo>();
        public int TotalPositions { get; set; }
        public int LoadedPositions { get; set; }
        public int IssuesCount { get; set; }
        public IReadOnlyList<string> TopEquipmentRows { get; set; } = Array.Empty<string>();

        public string BuildSummaryMessage()
        {
            var sampleText = TopEquipmentRows.Count == 0
                ? "Нет данных."
                : string.Join(Environment.NewLine, TopEquipmentRows);

            return $"Импорт завершен. Всего позиций: {TotalPositions}, загружено: {LoadedPositions}. Найдено оборудования: {Items.Count}, событий: {IssuesCount}.{Environment.NewLine}{Environment.NewLine}{sampleText}";
        }
    }

    public sealed class JiraImportService
    {
        public async Task<JiraImportResult> ImportAsync(JiraImportRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                return new JiraImportResult
                {
                    Success = false,
                    ErrorMessage = "Не задан запрос на импорт."
                };
            }

            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return new JiraImportResult
                {
                    Success = false,
                    ErrorMessage = "Укажите URL Jira API."
                };
            }

            try
            {
                var decoder = new JiraApiDataDecoder();
                var effectiveJql = BuildEffectiveJql(request.Jql, request.FilterIds, request.EnsureLatestOrdering);
                var items = await decoder.DecodeEquipmentAsync(
                    request.Url,
                    request.Username,
                    request.Token,
                    effectiveJql,
                    maxResults: request.PageSize,
                    totalResultsLimit: request.TotalResultsLimit,
                    cancellationToken: cancellationToken);

                try
                {
                    PersistedDataStore.SaveJiraImportedEquipment(items);
                }
                catch
                {
                    // ignore cache persistence errors
                }

                var issuesCount = items.Sum(e => e.Issues?.Count ?? 0);
                var sample = items
                    .OrderByDescending(e => e.Issues?.Count ?? 0)
                    .Take(5)
                    .Select(e => $"- {e} — {e.Issues.Count} событий")
                    .ToList();

                return new JiraImportResult
                {
                    Success = true,
                    Items = items,
                    TotalPositions = decoder.LastTotalPositionsFromApi,
                    LoadedPositions = decoder.LastLoadedPositionsFromApi,
                    IssuesCount = issuesCount,
                    TopEquipmentRows = sample
                };
            }
            catch (Exception ex)
            {
                return new JiraImportResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        private static string BuildEffectiveJql(string jql, IReadOnlyCollection<string> filterIds, bool ensureLatestOrdering)
        {
            string effectiveJql;
            if (filterIds == null || filterIds.Count == 0)
            {
                effectiveJql = jql?.Trim() ?? string.Empty;
            }
            else
            {
                var filterClause = string.Join(" OR ", filterIds.Select(id => $"filter = {id}"));
                var fromFilters = $"({filterClause}) AND status in ('Решен', 'В процессе', 'Resolved', 'In Progress')";
                effectiveJql = string.IsNullOrWhiteSpace(jql)
                    ? fromFilters
                    : $"({fromFilters}) AND ({jql.Trim()})";
            }

            if (!ensureLatestOrdering)
                return effectiveJql;

            if (string.IsNullOrWhiteSpace(effectiveJql))
                return "ORDER BY updated DESC";

            return effectiveJql.IndexOf("order by", StringComparison.OrdinalIgnoreCase) >= 0
                ? effectiveJql
                : $"{effectiveJql} ORDER BY updated DESC";
        }
    }
}
