using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Globalization;
using System.IO;

namespace EquipmentFailureAnalysis.Utility
{
    public class XmlDataDecoder
    {
        private XmlDocument xmlDocument;
        private ObservableCollection<EquipmentInfo>? equipmentCollection;
        private const string SubdivisionAttributeName = "dea-subdivision";

        // ID полей оборудования из разных проектов (Сектор сборки, Сектор измерений и др.)
        private readonly string[] equipmentFieldIds = {
            "customfield_10500", // Сектор сборки (2.xml)
            "customfield_10519", // Сектор сборки (1.xml)
            "customfield_10524", // Сектор измерений (3.xml)
            "customfield_10541"  // Кристальное производство (SearchRequest-10445.xml)
        };

        public XmlDataDecoder()
        {
            xmlDocument = new XmlDocument();
            // try load bundled data file if available; otherwise leave empty document
            try
            {
                var defaultPath = Path.Combine("Data", "EquipmentData.xml");
                if (File.Exists(defaultPath))
                {
                    xmlDocument.Load(defaultPath);
                    AnnotateItemsWithSubdivision(xmlDocument, ExtractSubdivisionFromDocument(xmlDocument));
                }
            }
            catch
            {
                // ignore - caller can later load via other constructor or import
            }
        }

        public XmlDataDecoder(string filePath)
        {
            xmlDocument = new XmlDocument();
            xmlDocument.Load(filePath);
            AnnotateItemsWithSubdivision(xmlDocument, ExtractSubdivisionFromDocument(xmlDocument));
        }

        // Load and merge multiple XML files: items from subsequent files are appended
        // into the first document's channel (or document element) so DecodeEquipment
        // will see a combined list of <item> nodes.
        public XmlDataDecoder(System.Collections.Generic.IEnumerable<string> filePaths)
        {
            xmlDocument = new XmlDocument();
            var paths = (filePaths ?? System.Linq.Enumerable.Empty<string>()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (paths.Count == 0)
                return;

            // Load the first document as the base
            xmlDocument.Load(paths[0]);
            AnnotateItemsWithSubdivision(xmlDocument, ExtractSubdivisionFromDocument(xmlDocument));

            // Try to find a <channel> node to append items to; fall back to document element
            XmlNode? appendTarget = xmlDocument.SelectSingleNode("//channel") ?? xmlDocument.DocumentElement;

            // For each additional file, import its <item> nodes into the base document
            for (int i = 1; i < paths.Count; i++)
            {
                try
                {
                    var temp = new XmlDocument();
                    temp.Load(paths[i]);
                    var tempSubdivision = ExtractSubdivisionFromDocument(temp);
                    var items = temp.GetElementsByTagName("item");
                    foreach (XmlNode item in items)
                    {
                        var imported = xmlDocument.ImportNode(item, true);
                        TrySetItemSubdivision(imported, tempSubdivision);
                        if (appendTarget != null)
                            appendTarget.AppendChild(imported);
                        else if (xmlDocument.DocumentElement != null)
                            xmlDocument.DocumentElement.AppendChild(imported);
                    }
                }
                catch
                {
                    // ignore errors for individual files and continue merging others
                }
            }
        }

        private static string ExtractSubdivisionFromDocument(XmlDocument document)
        {
            var channelTitle = document.SelectSingleNode("//channel/title")?.InnerText;
            return ParseSubdivisionFromChannelTitle(channelTitle);
        }

        private static string ParseSubdivisionFromChannelTitle(string? channelTitle)
        {
            if (string.IsNullOrWhiteSpace(channelTitle))
                return string.Empty;

            const string portalSuffix = " (Портал поддержки АО \"НЗПП Восток\")";
            var cleaned = channelTitle.Trim();
            if (cleaned.EndsWith(portalSuffix, StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(0, cleaned.Length - portalSuffix.Length).TrimEnd();

            return cleaned;
        }

        private static void AnnotateItemsWithSubdivision(XmlDocument document, string subdivision)
        {
            if (string.IsNullOrWhiteSpace(subdivision))
                return;

            var items = document.GetElementsByTagName("item");
            foreach (XmlNode item in items)
            {
                TrySetItemSubdivision(item, subdivision);
            }
        }

        private static void TrySetItemSubdivision(XmlNode itemNode, string subdivision)
        {
            if (string.IsNullOrWhiteSpace(subdivision))
                return;

            if (itemNode is XmlElement itemElement)
                itemElement.SetAttribute(SubdivisionAttributeName, subdivision);
        }

        private static string GetItemSubdivision(XmlNode itemNode, string fallbackSubdivision)
        {
            if (itemNode is XmlElement itemElement)
            {
                var value = itemElement.GetAttribute(SubdivisionAttributeName);
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return fallbackSubdivision;
        }

        public ObservableCollection<EquipmentInfo> DecodeEquipment()
        {
            XmlNodeList xmlNodeList = xmlDocument.GetElementsByTagName("item");
            equipmentCollection = new ObservableCollection<EquipmentInfo>();
            var fallbackSubdivision = ExtractSubdivisionFromDocument(xmlDocument);

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                var subdivision = GetItemSubdivision(xmlNode, fallbackSubdivision);
                // Фильтр по статусу: оставляем завершенные и "в работе"
                string status = xmlNode["status"]?.InnerText?.Trim() ?? string.Empty;
                bool isResolvedStatus =
                    status.Equals("Решен", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("Resolved", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("Done", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("Closed", StringComparison.OrdinalIgnoreCase);
                bool isInProgressStatus =
                    status.Equals("В процессе", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("В работе", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("In Progress", StringComparison.OrdinalIgnoreCase);
                if (!isResolvedStatus && !isInProgressStatus)
                    continue;

                // 1. Поиск данных об оборудовании в кастомных полях
                XmlNode? equipmentNode = null;
                foreach (var id in equipmentFieldIds)
                {
                    equipmentNode = xmlNode.SelectSingleNode($"customfields/customfield[@id='{id}']/customfieldvalues/customfieldvalue");
                    if (equipmentNode != null) break;
                }

                string? equipmentText = equipmentNode?.InnerText?.Trim();
                string title = "Unknown Equipment";
                int uid = 0;
                string? inventoryNumber = null;

                if (!string.IsNullOrEmpty(equipmentText))
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
                    // Если спец. поле пустое, берем название из Summary
                    title = xmlNode["summary"]?.InnerText ?? "Без названия";
                }

                // 2. Парсинг дат (с учетом формата JIRA RSS)
                DateTime.TryParse(xmlNode["created"]?.InnerText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime start);
                DateTime.TryParse(xmlNode["resolved"]?.InnerText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime end);
                DateTime.TryParse(xmlNode["updated"]?.InnerText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime updated);
                if (end == default)
                    end = updated != default ? updated : start;
                if (end < start)
                    end = start;

                // 3. Обработка описания (чистка HTML)
                string description = xmlNode["description"]?.InnerText ?? string.Empty;
                if (!string.IsNullOrEmpty(description))
                {
                    description = Regex.Replace(description, "</?p\\s*>", string.Empty, RegexOptions.IgnoreCase).Trim();
                    if (description.Length > 0)
                        description = char.ToUpper(description[0]) + (description.Length > 1 ? description.Substring(1) : "");
                }

                // 4. Тип работ (кастомное поле 10501)
                var typeNode = xmlNode.SelectSingleNode("customfields/customfield[@id='customfield_10501']/customfieldvalues/customfieldvalue");
                string? typeText = typeNode?.InnerText?.Trim();

                IssueType issueType = IssueType.Ремонт; // По умолчанию
                if (!string.IsNullOrEmpty(typeText) && typeText.Contains("Настрой", StringComparison.OrdinalIgnoreCase))
                {
                    issueType = IssueType.Настройка;
                }

                // 5. Ответственный: приоритет у ФИО из assignee.InnerText,
                // затем username
                static bool IsMissingResponsible(string? value) =>
                    string.IsNullOrWhiteSpace(value)
                    || value == "-1"
                    || value.Equals("unassigned", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("null", StringComparison.OrdinalIgnoreCase);

                var assigneeNode = xmlNode["assignee"];
                string? assigneeFullName = assigneeNode?.InnerText?.Trim();
                string? assigneeUsername = assigneeNode?.Attributes?["username"]?.Value?.Trim();

                string? responsible = !IsMissingResponsible(assigneeFullName)
                    ? assigneeFullName
                    : (!IsMissingResponsible(assigneeUsername) ? assigneeUsername : null);

                static bool IsExcludedEmployee(string? value) =>
                    !string.IsNullOrWhiteSpace(value)
                    && value.Trim().Equals("tv_kpims", StringComparison.OrdinalIgnoreCase);

                if (IsExcludedEmployee(assigneeUsername) || IsExcludedEmployee(responsible))
                    continue;

                // ФИО автора заявки (Jira custom field 10502), fallback на <reporter>
                var reporterNode = xmlNode.SelectSingleNode("customfields/customfield[@id='customfield_10502']/customfieldvalues/customfieldvalue")
                    ?? xmlNode.SelectSingleNode("customfields/customfield[customfieldname='ФИО Автора']/customfieldvalues/customfieldvalue");
                var reporter = reporterNode?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(reporter))
                    reporter = xmlNode["reporter"]?.InnerText?.Trim();

                // Комментарии из Jira RSS: <comments><comment ...>...</comment></comments>
                string comments = string.Empty;
                var commentNodes = xmlNode.SelectNodes("comments/comment");
                if (commentNodes != null && commentNodes.Count > 0)
                {
                    var parts = new List<string>();
                    foreach (XmlNode commentNode in commentNodes)
                    {
                        var author = commentNode.Attributes?["author"]?.Value?.Trim();
                        var createdRaw = commentNode.Attributes?["created"]?.Value?.Trim();
                        if (string.IsNullOrWhiteSpace(createdRaw))
                            createdRaw = commentNode.Attributes?["updated"]?.Value?.Trim();
                        var text = commentNode.InnerText ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            // Jira XML часто хранит комментарии как HTML (<p>...</p>) внутри текста.
                            // Снимаем теги, чтобы стабильно выводить плейн-текст в карточке задачи.
                            text = Regex.Replace(text, "<[^>]+>", string.Empty, RegexOptions.IgnoreCase).Trim();
                        }

                        if (string.IsNullOrWhiteSpace(text))
                            continue;

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

                        var prefix = string.IsNullOrWhiteSpace(author) ? string.Empty : $"{author}: ";
                        parts.Add(string.IsNullOrWhiteSpace(createdLabel) ? $"{prefix}{text}" : $"[{createdLabel}] {prefix}{text}");
                    }

                    if (parts.Count > 0)
                        comments = string.Join(Environment.NewLine + Environment.NewLine, parts);
                }

                var issue = new Issue
                {
                    Start = start,
                    End = end,
                    Description = description,
                    Type = issueType,
                    Responsible = string.IsNullOrEmpty(responsible) ? "Не назначен" : responsible,
                    JiraIssueKey = xmlNode["key"]?.InnerText?.Trim(),
                    IsInProgress = isInProgressStatus,
                    Reporter = string.IsNullOrWhiteSpace(reporter) ? null : reporter,
                    Comments = string.IsNullOrWhiteSpace(comments) ? null : comments
                };

                // NLP-like heuristic: token scoring with keyword lists and simple normalization
                try
                {
                    // Build normalized text sources separately to weight title higher than description
                    string normDesc = Regex.Replace((description ?? string.Empty).ToLowerInvariant(), "[\\p{P}\t\\n\\r]+", " ").Trim();
                    string normTitle = Regex.Replace((title ?? string.Empty).ToLowerInvariant(), "[\\p{P}\t\\n\\r]+", " ").Trim();
                    string normType = (typeText ?? string.Empty).ToLowerInvariant();

                    // Expanded keyword sets with common stems
                    var repairKeywords = new[] { "ремонт", "поломк", "слом", "замен", "неисправн", "поврежд", "не работает", "не запуска", "не включ", "авар", "протеч", "течь", "трещ", "корроз", "замык" };
                    var setupKeywords = new[] { "настрой", "налад", "калибр", "конфиг", "параметр", "пусконал", "установк", "тонк", "регул", "калибров", "калиброван", "конфигурац" };

                    int repairScore = 0;
                    int setupScore = 0;

                    // helper: count keyword occurrences in a text (simple substring match)
                    int CountMatches(string text, string keyword)
                    {
                        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword)) return 0;
                        return Regex.Matches(text, Regex.Escape(keyword)).Count;
                    }

                    // Count occurrences in description and title with different weights
                    foreach (var k in repairKeywords)
                    {
                        repairScore += CountMatches(normDesc, k) * 1;   // description weight
                        repairScore += CountMatches(normTitle, k) * 2;  // title weight
                    }
                    foreach (var k in setupKeywords)
                    {
                        setupScore += CountMatches(normDesc, k) * 1;
                        setupScore += CountMatches(normTitle, k) * 2;
                    }

                    // simple negation: if description contains patterns like "не настрой" reduce setup score
                    var negations = new[] { "не", "нет", "без", "исключая", "отмена" };
                    foreach (var neg in negations)
                    {
                        foreach (var k in repairKeywords)
                            if (normDesc.Contains(neg + " " + k)) repairScore = Math.Max(0, repairScore - 1);
                        foreach (var k in setupKeywords)
                            if (normDesc.Contains(neg + " " + k)) setupScore = Math.Max(0, setupScore - 1);
                    }

                    // Boost by explicit type field strongly
                    if (!string.IsNullOrEmpty(normType))
                    {
                        if (normType.Contains("настрой")) setupScore += 5;
                        if (normType.Contains("ремонт")) repairScore += 5;
                    }

                    // If description explicitly contains words like "настройка завершена" or "ремонт выполнен" weight further
                    if (Regex.IsMatch(normDesc, "(настройка|наладка)\\s+заверш|(настроен|наладчик)", RegexOptions.IgnoreCase)) setupScore += 2;
                    if (Regex.IsMatch(normDesc, "(ремонт|замена)\\s+(выполн|заверш|проведен)|поломка", RegexOptions.IgnoreCase)) repairScore += 2;

                    // Decide detected type; require clear margin
                    EquipmentFailureAnalysis.Models.IssueType? detected = null;
                    int diff = repairScore - setupScore;
                    if (repairScore + setupScore == 0)
                    {
                        detected = null;
                    }
                    else if (diff >= 2)
                    {
                        detected = EquipmentFailureAnalysis.Models.IssueType.Ремонт;
                    }
                    else if (diff <= -2)
                    {
                        detected = EquipmentFailureAnalysis.Models.IssueType.Настройка;
                    }
                    else
                    {
                        // close scores -> prefer explicit field if exists
                        if (!string.IsNullOrEmpty(normType))
                        {
                            if (normType.Contains("настрой")) detected = EquipmentFailureAnalysis.Models.IssueType.Настройка;
                            else if (normType.Contains("ремонт")) detected = EquipmentFailureAnalysis.Models.IssueType.Ремонт;
                        }
                    }

                    // If we detected a type confidently, update issue.Type to improve grouping
                    var originalType = issue.Type;
                    if (detected.HasValue)
                    {
                        issue.Type = detected.Value;
                    }

                    issue.DetectedType = detected;
                    issue.TypeSuspicious = detected.HasValue && detected.Value != originalType;
                }
                catch
                {
                    // don't fail parsing on analysis errors
                }

                // 6. Группировка: ищем, нет ли уже такого оборудования в коллекции
                EquipmentInfo? equipment = equipmentCollection.FirstOrDefault(e =>
                    ((uid != 0 && e.Uid == uid) || e.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
                    && string.Equals(e.Subdivision ?? string.Empty, subdivision ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                if (equipment == null)
                {
                    equipment = new EquipmentInfo
                    {
                        Title = title ?? string.Empty,
                        Uid = uid,
                        InventoryNumber = inventoryNumber,
                        Subdivision = subdivision
                    };
                    equipmentCollection.Add(equipment);
                }

                equipment.Issues.Add(issue);
            }

            return equipmentCollection;
        }
    }
}
