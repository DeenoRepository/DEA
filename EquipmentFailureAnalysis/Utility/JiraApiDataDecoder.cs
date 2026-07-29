using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EquipmentFailureAnalysis.Utility
{
    public sealed class JiraApiDataDecoder
    {
        public int LastTotalPositionsFromApi { get; private set; }
        public int LastLoadedPositionsFromApi { get; private set; }

        private static readonly string[] DefaultEquipmentFieldIds =
        {
            "customfield_10500",
            "customfield_10519",
            "customfield_10524",
            "customfield_10541"
        };

        public async Task<ObservableCollection<EquipmentInfo>> DecodeEquipmentAsync(
            string jiraApiUrl,
            string username,
            string token,
            string? jql = null,
            int maxResults = 1000,
            int? totalResultsLimit = null,
            IEnumerable<string>? equipmentFieldIds = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(jiraApiUrl))
                throw new ArgumentException("Не указан URL Jira API.", nameof(jiraApiUrl));

            LastTotalPositionsFromApi = 0;
            LastLoadedPositionsFromApi = 0;

            var effectiveEquipmentFieldIds = NormalizeEquipmentFieldIds(equipmentFieldIds);
            using var client = new HttpClient();

            var equipmentCollection = new ObservableCollection<EquipmentInfo>();
            var startAt = 0;
            var pageSize = Math.Max(1, maxResults);
            var remainingLimit = totalResultsLimit.HasValue
                ? Math.Max(0, totalResultsLimit.Value)
                : int.MaxValue;

            while (true)
            {
                if (remainingLimit <= 0)
                    break;

                var requestedPageSize = Math.Min(pageSize, remainingLimit);
                var requestUrl = BuildSearchUrl(jiraApiUrl, jql, requestedPageSize, effectiveEquipmentFieldIds, startAt);
                using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(token))
                {
                    var authBytes = Encoding.UTF8.GetBytes($"{username}:{token}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
                }

                using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    break;

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("total", out var totalElement) && totalElement.ValueKind == JsonValueKind.Number)
                    LastTotalPositionsFromApi = totalElement.GetInt32();

                if (!root.TryGetProperty("issues", out var issuesElement) || issuesElement.ValueKind != JsonValueKind.Array)
                    break;

                var fetchedCount = issuesElement.GetArrayLength();
                LastLoadedPositionsFromApi += fetchedCount;
                if (fetchedCount == 0)
                    break;

                var processedInBatch = 0;
                foreach (var issueElement in issuesElement.EnumerateArray())
                {
                    if (processedInBatch >= remainingLimit)
                        break;

                    if (!issueElement.TryGetProperty("fields", out var fields))
                        continue;

                    var isInProgress = IsInProgressIssueStatus(fields);
                    if (!IsIncludedIssueStatus(fields))
                        continue;

                    var subdivision = GetSubdivision(fields);
                    var equipmentText = GetEquipmentText(fields, effectiveEquipmentFieldIds);

                    var title = "Unknown Equipment";
                    var uid = 0;
                    string? inventoryNumber = null;

                    if (!string.IsNullOrWhiteSpace(equipmentText))
                    {
                        // 1. Поиск с явным префиксом (инв., зав., №, инв, зав) с поддержкой комбинаций (например, "зав. №")
                        var matchWithPrefix = Regex.Match(equipmentText, @"^(?<Name>.*?)[\s\t]+(?:(?:инв\.|зав\.|№|инв|зав)[\s\t\.\№]*)+(?<ID>[a-zA-Z0-9\-\/]+)$", RegexOptions.IgnoreCase);
                        if (matchWithPrefix.Success)
                        {
                            title = matchWithPrefix.Groups["Name"].Value.Trim();
                            inventoryNumber = matchWithPrefix.Groups["ID"].Value.Trim();
                            int.TryParse(inventoryNumber, out uid);
                        }
                        else
                        {
                            // 2. Поиск без префикса (числовой суффикс)
                            var matchPureNumeric = Regex.Match(equipmentText, @"^(?<Name>.*?)[\s\t]+(?<ID>\d+)$", RegexOptions.IgnoreCase);
                            if (matchPureNumeric.Success)
                            {
                                var possibleId = matchPureNumeric.Groups["ID"].Value;
                                var nameEnd = matchPureNumeric.Groups["Name"].Index + matchPureNumeric.Groups["Name"].Length;
                                var idStart = matchPureNumeric.Groups["ID"].Index;
                                var separatorSpan = equipmentText.Substring(nameEnd, idStart - nameEnd);

                                // Считаем инвентарным номером, если разделено табуляцией, либо если длина не равна 4 (чтобы не путать с моделями типа TDS 2002) и длина >= 2
                                if (separatorSpan.Contains('\t') || (possibleId.Length != 4 && possibleId.Length >= 2))
                                {
                                    title = matchPureNumeric.Groups["Name"].Value.Trim();
                                    inventoryNumber = possibleId;
                                    int.TryParse(inventoryNumber, out uid);
                                }
                                else
                                {
                                    title = equipmentText;
                                }
                            }
                            else
                            {
                                title = equipmentText;
                            }
                        }
                    }
                    else
                    {
                        var summary = TryGetString(fields, "summary");
                        title = string.IsNullOrWhiteSpace(summary) ? "Без названия" : summary.Trim();
                    }

                    DateTime.TryParse(TryGetString(fields, "created"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var start);
                    DateTime.TryParse(TryGetString(fields, "resolutiondate"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var end);
                    if (end == default)
                        end = isInProgress ? DateTime.Now : start;

                    if (end < start)
                        end = start;

                    var description = NormalizeDescription(GetDescriptionText(fields));
                    var typeText = GetCustomFieldText(fields, "customfield_10501");
                    var issueType = DetectIssueType(typeText, title, description);
                    var responsible = ResolveResponsible(fields);
                    var reporter = ResolveReporter(fields);
                    var comments = GetCommentsText(fields);

                    if (IsExcludedEmployee(TryGetString(fields, "assignee", "name")) || IsExcludedEmployee(responsible))
                        continue;

                    var issue = new Issue
                    {
                        Start = start,
                        End = end,
                        Description = description,
                        Type = issueType,
                        Responsible = string.IsNullOrWhiteSpace(responsible) ? "Не назначен" : responsible,
                        IsInProgress = isInProgress,
                        JiraIssueKey = TryGetString(issueElement, "key"),
                        Reporter = reporter,
                        Comments = comments
                    };

                    var equipment = equipmentCollection.FirstOrDefault(e =>
                        ((uid != 0 && e.Uid == uid) || e.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
                        && string.Equals(e.Subdivision ?? string.Empty, subdivision ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                    if (equipment == null)
                    {
                        equipment = new EquipmentInfo
                        {
                            Title = title,
                            Uid = uid,
                            InventoryNumber = inventoryNumber,
                            Subdivision = subdivision
                        };
                        equipmentCollection.Add(equipment);
                    }

                    equipment.Issues.Add(issue);
                    processedInBatch++;
                }

                startAt += fetchedCount;
                remainingLimit = Math.Max(0, remainingLimit - processedInBatch);
                if (LastTotalPositionsFromApi > 0 && startAt >= LastTotalPositionsFromApi)
                    break;
            }

            return equipmentCollection;
        }

        private static string BuildSearchUrl(string jiraApiUrl, string? jql, int maxResults, IReadOnlyCollection<string> equipmentFieldIds, int startAt)
        {
            var source = jiraApiUrl.Trim();
            var searchUrl = source.Contains("/rest/api/", StringComparison.OrdinalIgnoreCase)
                ? source
                : source.TrimEnd('/') + "/rest/api/2/search";

            var fieldsForRequest = new List<string>
            {
                "summary",
                "description",
                "created",
                "resolutiondate",
                "status",
                "assignee",
                "reporter",
                "comment",
                "project",
                "customfield_10501",
                "customfield_10502"
            };
            fieldsForRequest.AddRange(equipmentFieldIds);
            var fields = string.Join(",", fieldsForRequest.Distinct(StringComparer.OrdinalIgnoreCase));

            var effectiveJql = string.IsNullOrWhiteSpace(jql)
                ? "(statusCategory = Done OR statusCategory = 'In Progress') ORDER BY updated DESC"
                : jql.Trim();

            var separator = searchUrl.Contains('?') ? "&" : "?";
            return searchUrl
                + separator + "jql=" + Uri.EscapeDataString(effectiveJql)
                + "&maxResults=" + Math.Max(1, maxResults).ToString(CultureInfo.InvariantCulture)
                + "&startAt=" + Math.Max(0, startAt).ToString(CultureInfo.InvariantCulture)
                + "&fields=" + Uri.EscapeDataString(fields);
        }

        private static bool IsIncludedIssueStatus(JsonElement fields)
        {
            if (fields.TryGetProperty("status", out var status))
            {
                var categoryKey = TryGetString(status, "statusCategory", "key");
                if (string.Equals(categoryKey, "done", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(categoryKey, "indeterminate", StringComparison.OrdinalIgnoreCase))
                    return true;

                var statusName = TryGetString(status, "name");
                if (!string.IsNullOrWhiteSpace(statusName)
                    && (statusName.Equals("Решен", StringComparison.OrdinalIgnoreCase)
                        || statusName.Equals("В процессе", StringComparison.OrdinalIgnoreCase)
                        || statusName.Equals("Resolved", StringComparison.OrdinalIgnoreCase)
                        || statusName.Equals("In Progress", StringComparison.OrdinalIgnoreCase)
                        || statusName.Equals("Done", StringComparison.OrdinalIgnoreCase)
                        || statusName.Equals("Closed", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInProgressIssueStatus(JsonElement fields)
        {
            if (!fields.TryGetProperty("status", out var status))
                return false;

            var categoryKey = TryGetString(status, "statusCategory", "key");
            if (string.Equals(categoryKey, "indeterminate", StringComparison.OrdinalIgnoreCase))
                return true;

            var statusName = TryGetString(status, "name");
            if (string.IsNullOrWhiteSpace(statusName))
                return false;

            return statusName.Equals("В процессе", StringComparison.OrdinalIgnoreCase)
                   || statusName.Equals("В работе", StringComparison.OrdinalIgnoreCase)
                   || statusName.Equals("In Progress", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetEquipmentText(JsonElement fields, IReadOnlyCollection<string> equipmentFieldIds)
        {
            foreach (var fieldId in equipmentFieldIds)
            {
                var text = GetCustomFieldText(fields, fieldId);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return null;
        }

        private static IReadOnlyCollection<string> NormalizeEquipmentFieldIds(IEnumerable<string>? equipmentFieldIds)
        {
            var normalized = (equipmentFieldIds ?? DefaultEquipmentFieldIds)
                .Select(v => v?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalized.Count == 0)
                normalized.AddRange(DefaultEquipmentFieldIds);

            return normalized;
        }

        private static string? GetCustomFieldText(JsonElement fields, string fieldName)
        {
            if (!fields.TryGetProperty(fieldName, out var value))
                return null;

            return ReadJsonValueAsText(value);
        }

        private static string GetSubdivision(JsonElement fields)
        {
            var subdivision = TryGetString(fields, "project", "name");
            if (!string.IsNullOrWhiteSpace(subdivision))
                return subdivision.Trim();

            var key = TryGetString(fields, "project", "key");
            return key?.Trim() ?? string.Empty;
        }

        private static string GetDescriptionText(JsonElement fields)
        {
            if (!fields.TryGetProperty("description", out var description))
                return string.Empty;

            return ReadJsonValueAsText(description) ?? string.Empty;
        }

        private static string NormalizeDescription(string description)
        {
            var text = description ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = Regex.Replace(text, "</?p\\s*>", string.Empty, RegexOptions.IgnoreCase).Trim();
            if (text.Length > 0)
                text = char.ToUpper(text[0]) + (text.Length > 1 ? text.Substring(1) : string.Empty);
            return text;
        }

        private static string ResolveResponsible(JsonElement fields)
        {
            var fullName = TryGetString(fields, "assignee", "displayName")?.Trim();
            if (!IsMissingResponsible(fullName))
                return fullName!;

            var userName = TryGetString(fields, "assignee", "name")?.Trim();
            if (!IsMissingResponsible(userName))
                return userName!;

            return "Не назначен";
        }

        private static string ResolveReporter(JsonElement fields)
        {
            // Primary source provided by business: "ФИО Автора"
            var authorFullName = GetCustomFieldText(fields, "customfield_10502")?.Trim();
            if (!string.IsNullOrWhiteSpace(authorFullName))
                return authorFullName;

            var fullName = TryGetString(fields, "reporter", "displayName")?.Trim();
            if (!string.IsNullOrWhiteSpace(fullName))
                return fullName;

            var userName = TryGetString(fields, "reporter", "name")?.Trim();
            if (!string.IsNullOrWhiteSpace(userName))
                return userName;

            return string.Empty;
        }

        private static string GetCommentsText(JsonElement fields)
        {
            if (!fields.TryGetProperty("comment", out var commentElement))
                return string.Empty;

            if (!commentElement.TryGetProperty("comments", out var commentsArray) || commentsArray.ValueKind != JsonValueKind.Array)
                return string.Empty;

            var comments = new List<string>();
            foreach (var item in commentsArray.EnumerateArray())
            {
                var author = TryGetString(item, "author", "displayName")?.Trim();
                if (string.IsNullOrWhiteSpace(author))
                    author = TryGetString(item, "author", "name")?.Trim();
                var createdRaw = TryGetString(item, "created")?.Trim();
                if (string.IsNullOrWhiteSpace(createdRaw))
                    createdRaw = TryGetString(item, "updated")?.Trim();
                var createdLabel = string.Empty;
                if (!string.IsNullOrWhiteSpace(createdRaw)
                    && DateTime.TryParse(createdRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var createdAt))
                {
                    createdLabel = createdAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture);
                }
                else if (!string.IsNullOrWhiteSpace(createdRaw))
                {
                    createdLabel = createdRaw;
                }

                var body = item.TryGetProperty("body", out var bodyElement)
                    ? ReadJsonValueAsText(bodyElement)
                    : null;
                body = NormalizeDescription(body ?? string.Empty);
                if (string.IsNullOrWhiteSpace(body))
                    continue;

                var prefix = string.IsNullOrWhiteSpace(author) ? string.Empty : $"{author}: ";
                comments.Add(string.IsNullOrWhiteSpace(createdLabel) ? $"{prefix}{body}" : $"[{createdLabel}] {prefix}{body}");
            }

            return comments.Count == 0 ? string.Empty : string.Join(Environment.NewLine + Environment.NewLine, comments);
        }

        private static bool IsMissingResponsible(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                || value == "-1"
                || value.Equals("unassigned", StringComparison.OrdinalIgnoreCase)
                || value.Equals("null", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExcludedEmployee(string? value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Trim().Equals("tv_kpims", StringComparison.OrdinalIgnoreCase);
        }

        private static IssueType DetectIssueType(string? typeText, string title, string description)
        {
            var normType = (typeText ?? string.Empty).ToLowerInvariant();
            var normTitle = NormalizeForMatching(title);
            var normDescription = NormalizeForMatching(description);

            var repairKeywords = new[]
            {
                "ремонт", "поломк", "слом", "неисправн", "поврежд", "авар", "протеч", "течь", "трещ", "корроз", "замык",
                "разборк", "демонтаж", "снят", "очистк", "дефектовк", "восстанов", "шлифов", "замен", "монтаж", "сборк", "сверлен", "резк", "покраск", "подвар"
            };

            var setupKeywords = new[]
            {
                "настрой", "налад", "калибр", "конфиг", "параметр", "пусконал", "установк", "регул", "включ", "выключ", "подключ", "отключ"
            };

            var repairScore = 0;
            var setupScore = 0;

            foreach (var keyword in repairKeywords)
            {
                repairScore += CountMatches(normDescription, keyword);
                repairScore += CountMatches(normTitle, keyword) * 2;
            }

            foreach (var keyword in setupKeywords)
            {
                setupScore += CountMatches(normDescription, keyword);
                setupScore += CountMatches(normTitle, keyword) * 2;
            }

            if (normType.Contains("ремонт"))
                repairScore += 4;
            if (normType.Contains("настрой") || normType.Contains("налад"))
                setupScore += 4;

            if (repairScore == 0)
                setupScore += 1;

            return setupScore >= repairScore ? IssueType.Настройка : IssueType.Ремонт;
        }

        private static int CountMatches(string text, string keyword)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
                return 0;

            return Regex.Matches(text, Regex.Escape(keyword), RegexOptions.IgnoreCase).Count;
        }

        private static string NormalizeForMatching(string source)
        {
            return Regex.Replace((source ?? string.Empty).ToLowerInvariant(), "[\\p{P}\\t\\n\\r]+", " ").Trim();
        }

        private static string? ReadJsonValueAsText(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return element.ToString();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean().ToString();
                case JsonValueKind.Array:
                    {
                        var values = new List<string>();
                        foreach (var child in element.EnumerateArray())
                        {
                            var childText = ReadJsonValueAsText(child);
                            if (!string.IsNullOrWhiteSpace(childText))
                                values.Add(childText.Trim());
                        }

                        return values.Count > 0 ? string.Join(", ", values) : null;
                    }
                case JsonValueKind.Object:
                    {
                        var knownProps = new[] { "value", "name", "displayName", "text" };
                        foreach (var prop in knownProps)
                        {
                            if (element.TryGetProperty(prop, out var value))
                            {
                                var text = ReadJsonValueAsText(value);
                                if (!string.IsNullOrWhiteSpace(text))
                                    return text;
                            }
                        }

                        if (element.TryGetProperty("content", out var content))
                        {
                            var contentText = ReadJsonValueAsText(content);
                            if (!string.IsNullOrWhiteSpace(contentText))
                                return contentText;
                        }

                        var allTexts = new List<string>();
                        foreach (var property in element.EnumerateObject())
                        {
                            var propertyText = ReadJsonValueAsText(property.Value);
                            if (!string.IsNullOrWhiteSpace(propertyText))
                                allTexts.Add(propertyText.Trim());
                        }

                        return allTexts.Count > 0 ? string.Join(" ", allTexts.Distinct()) : null;
                    }
                default:
                    return null;
            }
        }

        private static string? TryGetString(JsonElement element, params string[] path)
        {
            var current = element;
            for (var i = 0; i < path.Length; i++)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(path[i], out current))
                    return null;
            }

            return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
        }
    }
}
