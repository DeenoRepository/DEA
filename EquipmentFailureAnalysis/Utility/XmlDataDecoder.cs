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
using System.Net;

namespace EquipmentFailureAnalysis.Utility
{
    public class XmlDataDecoder
    {
        private XmlDocument xmlDocument;
        private ObservableCollection<EquipmentInfo>? equipmentCollection;

        // ID полей оборудования из разных проектов (Сектор сборки, Сектор измерений и др.)
        private readonly string[] equipmentFieldIds = {
            "customfield_10500", // Сектор сборки (2.xml)
            "customfield_10519", // Сектор сборки (1.xml)
            "customfield_10524"  // Сектор измерений (3.xml)
        };

        // Стемы ремонтных операций (включая производные формы слов)
        private static readonly string[] repairWordStems =
        {
            "разбор", "демонтаж", "снят", "выем", "очист", "обмыв", "промыв", "обезжир", "слив", "откач",
            "расконсервац", "дефект", "осмотр", "вскрыт", "замер", "обмер", "простукив", "восстанов", "реставрац",
            "ремонт", "капитал", "переборк", "шлифов", "полиров", "притир", "проточ", "расточ", "правк", "рихтов",
            "заделк", "замен", "смен", "подмен", "установк", "монтаж", "сборк", "сочлен", "запрессовк", "выпрессовк",
            "креплен", "фиксац", "заправк", "доливк", "уплотнен", "сверлен", "зенкер", "резк", "обрезк", "вырубк",
            "срубан", "клепк", "склеив", "покрас", "изоляц", "обмотк", "латк", "заплатк", "вытяжк", "центровк",
            "шплинтовк", "протяжк", "набивк", "прихватк", "подварк"
        };

        public XmlDataDecoder()
        {
            xmlDocument = new XmlDocument();
            // try load bundled data file if available; otherwise leave empty document
            try
            {
                var defaultPath = Path.Combine("Data", "EquipmentData.xml");
                if (File.Exists(defaultPath))
                    xmlDocument.Load(defaultPath);
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

            // Try to find a <channel> node to append items to; fall back to document element
            XmlNode? appendTarget = xmlDocument.SelectSingleNode("//channel") ?? xmlDocument.DocumentElement;

            // For each additional file, import its <item> nodes into the base document
            for (int i = 1; i < paths.Count; i++)
            {
                try
                {
                    var temp = new XmlDocument();
                    temp.Load(paths[i]);
                    var items = temp.GetElementsByTagName("item");
                    foreach (XmlNode item in items)
                    {
                        var imported = xmlDocument.ImportNode(item, true);
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

        public ObservableCollection<EquipmentInfo> DecodeEquipment()
        {
            XmlNodeList xmlNodeList = xmlDocument.GetElementsByTagName("item");
            equipmentCollection = new ObservableCollection<EquipmentInfo>();

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                // Фильтр по статусу (пропускаем нерешенные задачи)
                string status = xmlNode["status"]?.InnerText ?? string.Empty;
                string resolution = xmlNode["resolution"]?.InnerText ?? string.Empty;
                if (!IsResolved(status, resolution))
                    continue;

                // 1. Поиск данных об оборудовании в кастомных полях
                var equipmentText = FindEquipmentFieldValue(xmlNode);
                string title = "Unknown Equipment";
                int uid = 0;
                string? inventoryNumber = null;

                if (!string.IsNullOrEmpty(equipmentText))
                {
                    ParseEquipmentText(equipmentText, out title, out inventoryNumber, out uid);
                }
                else
                {
                    // Если спец. поле пустое, берем название из Summary
                    title = xmlNode["summary"]?.InnerText ?? "Без названия";
                }

                // 2. Парсинг дат (с учетом формата JIRA RSS)
                DateTime start = ParseJiraDate(xmlNode["created"]?.InnerText);
                DateTime end = ParseJiraDate(xmlNode["resolved"]?.InnerText);
                if (end == default)
                    end = ParseJiraDate(xmlNode["updated"]?.InnerText);
                if (start == default)
                    start = end == default ? DateTime.Now : end;
                if (end == default || end < start)
                    end = start;

                // 3. Обработка описания (чистка HTML)
                string description = NormalizeText(xmlNode["description"]?.InnerText ?? string.Empty, true);
                string issueSummary = NormalizeText(xmlNode["summary"]?.InnerText ?? string.Empty, true);

                // 4. Тип работ (кастомное поле 10501)
                var typeText = FindCustomFieldValue(xmlNode, "customfield_10501")
                    ?? FindCustomFieldValueByNameContains(xmlNode, "тип проводимых работ");

                var requestTypeText = FindCustomFieldValue(xmlNode, "customfield_10001")
                    ?? FindCustomFieldValueByNameContains(xmlNode, "тип запроса клиента")
                    ?? string.Empty;

                var explicitType = ParseExplicitType(typeText);
                var nlpEvaluation = EvaluateWorkTypeNlp(issueSummary, description, requestTypeText, typeText);
                var detectedType = DetectTypeFromText(issueSummary, description, requestTypeText, typeText, nlpEvaluation);
                var finalType = ResolveFinalType(explicitType, detectedType, issueSummary, description, requestTypeText, nlpEvaluation);

                // 5. Ответственный: приоритет у ФИО из assignee.InnerText,
                // затем username, затем резервное поле customfield_10502
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

                if (IsMissingResponsible(responsible))
                {
                    var fallbackResponsible = FindCustomFieldValue(xmlNode, "customfield_10502")
                        ?? FindCustomFieldValueByNameContains(xmlNode, "фио автора");
                    if (!IsMissingResponsible(fallbackResponsible))
                        responsible = fallbackResponsible;
                }

                var issue = new Issue
                {
                    Start = start,
                    End = end,
                    Description = description,
                    Type = finalType,
                    Responsible = string.IsNullOrEmpty(responsible) ? "Не назначен" : responsible,
                    RepairProbability = nlpEvaluation.RepairProbability,
                    SetupProbability = nlpEvaluation.SetupProbability
                };
                issue.DetectedType = detectedType;
                issue.TypeSuspicious = explicitType.HasValue && detectedType.HasValue && explicitType.Value != detectedType.Value;

                // 6. Группировка: ищем, нет ли уже такого оборудования в коллекции
                EquipmentInfo? equipment = equipmentCollection.FirstOrDefault(e =>
                    (uid != 0 && e.Uid == uid) || e.Title.Equals(title, StringComparison.OrdinalIgnoreCase));

                if (equipment == null)
                {
                    equipment = new EquipmentInfo
                    {
                        Title = title,
                        Uid = uid,
                        InventoryNumber = inventoryNumber
                    };
                    equipmentCollection.Add(equipment);
                }

                equipment.Issues.Add(issue);
            }

            return equipmentCollection;
        }

        private static bool IsResolved(string status, string resolution)
        {
            static bool ContainsAny(string source, params string[] values) =>
                values.Any(v => source.Contains(v, StringComparison.OrdinalIgnoreCase));

            var st = (status ?? string.Empty).Trim();
            var rs = (resolution ?? string.Empty).Trim();
            return ContainsAny(st, "Решен", "Закрыт", "Выполн") || ContainsAny(rs, "Готово", "Выполн", "Решено");
        }

        private static string? FindCustomFieldValue(XmlNode issueNode, string fieldId)
        {
            var node = issueNode.SelectSingleNode($"customfields/customfield[@id='{fieldId}']/customfieldvalues/customfieldvalue");
            return node?.InnerText?.Trim();
        }

        private static string? FindCustomFieldValueByNameContains(XmlNode issueNode, string namePart)
        {
            var fields = issueNode.SelectNodes("customfields/customfield");
            if (fields == null)
                return null;

            foreach (XmlNode field in fields)
            {
                var name = field.SelectSingleNode("customfieldname")?.InnerText?.Trim();
                if (string.IsNullOrWhiteSpace(name) || !name.Contains(namePart, StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = field.SelectSingleNode("customfieldvalues/customfieldvalue")?.InnerText?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

        private string? FindEquipmentFieldValue(XmlNode issueNode)
        {
            foreach (var id in equipmentFieldIds)
            {
                var value = FindCustomFieldValue(issueNode, id);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return FindCustomFieldValueByNameContains(issueNode, "оборудован");
        }

        private static void ParseEquipmentText(string equipmentText, out string title, out string? inventoryNumber, out int uid)
        {
            title = equipmentText;
            inventoryNumber = null;
            uid = 0;

            if (string.IsNullOrWhiteSpace(equipmentText))
                return;

            var text = Regex.Replace(equipmentText, "\\s+", " ").Trim();

            if (text.Equals("иное", StringComparison.OrdinalIgnoreCase))
            {
                title = "Не указано";
                return;
            }

            var labeledId = Regex.Match(text, @"(?:инв\.?|зав\.?)\s*(?<id>\d{2,})\s*$", RegexOptions.IgnoreCase);
            if (labeledId.Success)
            {
                inventoryNumber = labeledId.Groups["id"].Value;
                title = Regex.Replace(text, @"(?:инв\.?|зав\.?)\s*\d{2,}\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
                int.TryParse(inventoryNumber, out uid);
                return;
            }

            var trailingId = Regex.Match(text, @"^(?<name>.+?)\s+(?<id>\d{2,})\s*$", RegexOptions.IgnoreCase);
            if (trailingId.Success)
            {
                title = trailingId.Groups["name"].Value.Trim();
                inventoryNumber = trailingId.Groups["id"].Value;
                int.TryParse(inventoryNumber, out uid);
                return;
            }

            title = text;
        }

        private static DateTime ParseJiraDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return default;

            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dto))
                return dto.LocalDateTime;

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var dt))
                return dt;

            return default;
        }

        private static string NormalizeText(string value, bool capitalizeFirst)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var decoded = WebUtility.HtmlDecode(value);
            decoded = Regex.Replace(decoded, "<\\s*br\\s*/?\\s*>", " ", RegexOptions.IgnoreCase);
            decoded = Regex.Replace(decoded, "</?p\\s*>", " ", RegexOptions.IgnoreCase);
            decoded = Regex.Replace(decoded, "<[^>]+>", " ");
            decoded = Regex.Replace(decoded, "\\s+", " ").Trim();

            if (!capitalizeFirst || decoded.Length == 0)
                return decoded;

            return char.ToUpper(decoded[0]) + (decoded.Length > 1 ? decoded[1..] : string.Empty);
        }

        private static IssueType? ParseExplicitType(string? typeText)
        {
            if (string.IsNullOrWhiteSpace(typeText))
                return null;

            if (typeText.Contains("Настрой", StringComparison.OrdinalIgnoreCase) ||
                typeText.Contains("Включ", StringComparison.OrdinalIgnoreCase) ||
                typeText.Contains("Отключ", StringComparison.OrdinalIgnoreCase) ||
                typeText.Contains("Подключ", StringComparison.OrdinalIgnoreCase) ||
                typeText.Contains("Налад", StringComparison.OrdinalIgnoreCase) ||
                typeText.Contains("Калибр", StringComparison.OrdinalIgnoreCase))
                return IssueType.Настройка;

            if (typeText.Contains("Ремонт", StringComparison.OrdinalIgnoreCase) ||
                typeText.Contains("Неисправ", StringComparison.OrdinalIgnoreCase) ||
                typeText.Contains("Полом", StringComparison.OrdinalIgnoreCase) ||
                repairWordStems.Any(s => typeText.Contains(s, StringComparison.OrdinalIgnoreCase)))
                return IssueType.Ремонт;

            return null;
        }

        private static IssueType? DetectTypeFromText(string summary, string description, string requestTypeText, string? explicitTypeText, (double RepairScore, double SetupScore, double RepairProbability, double SetupProbability) nlp)
        {
            var text = (summary + " " + description + " " + requestTypeText).ToLowerInvariant();

            if (text.Contains("не включ"))
                return IssueType.Ремонт;

            if (text.Contains("настройка") || text.Contains("настройить") || text.Contains("включение") || text.Contains("отключение") || text.Contains("подключение"))
                return IssueType.Настройка;

            if (nlp.RepairScore == 0 && nlp.SetupScore == 0)
                return null;

            // Для снижения ложных "ремонт" при работах по настройке:
            // если признаки настройки не слабее ремонта — считаем это настройкой.
            if (nlp.SetupScore > 0 && nlp.SetupScore >= nlp.RepairScore)
                return IssueType.Настройка;

            if (nlp.RepairProbability >= 0.60)
                return IssueType.Ремонт;

            if (nlp.SetupProbability >= 0.55)
                return IssueType.Настройка;

            var diff = nlp.RepairScore - nlp.SetupScore;
            if (diff >= 1.8)
                return IssueType.Ремонт;
            if (diff <= -1.8)
                return IssueType.Настройка;

            return null;
        }

        private static IssueType InferFallbackType(string summary, string description, string requestTypeText, (double RepairScore, double SetupScore, double RepairProbability, double SetupProbability)? nlp = null)
        {
            var text = (summary + " " + description + " " + requestTypeText).ToLowerInvariant();

            if (text.Contains("не включ"))
                return IssueType.Ремонт;

            if (text.Contains("настройка") || text.Contains("настройить") || text.Contains("включение") || text.Contains("отключение") || text.Contains("подключение"))
                return IssueType.Настройка;

            var repairKeywords = new[]
            {
                "ремонт", "неисправ", "полом", "слом", "замен", "авар", "не работает", "не горит", "замык", "течь", "протеч", "вышел из строя"
            }.Concat(repairWordStems).ToArray();

            if (repairKeywords.Any(k => text.Contains(k)))
                return IssueType.Ремонт;

            var eval = nlp ?? EvaluateWorkTypeNlp(summary, description, requestTypeText, null);
            if (eval.RepairProbability > eval.SetupProbability && eval.RepairProbability >= 0.58)
                return IssueType.Ремонт;

            if (text.Contains("настрой") || text.Contains("налад") || text.Contains("калибр") || text.Contains("регулир") || text.Contains("параметр")
                || text.Contains("включ") || text.Contains("выключ") || text.Contains("подключ") || text.Contains("отключ"))
                return IssueType.Настройка;

            // Если явных признаков ремонта нет, по умолчанию считаем это настройкой.
            return IssueType.Настройка;
        }

        private static IssueType ResolveFinalType(
            IssueType? explicitType,
            IssueType? detectedType,
            string summary,
            string description,
            string requestTypeText,
            (double RepairScore, double SetupScore, double RepairProbability, double SetupProbability) nlp)
        {
            if (explicitType == IssueType.Ремонт)
            {
                // Если в тексте нет признаков ремонта, даже при явном "Ремонт"
                // считаем это настройкой (согласно правилам проекта).
                if (!HasStrongRepairMarkers(summary, description, requestTypeText))
                    return detectedType ?? InferFallbackType(summary, description, requestTypeText, nlp);
            }

            if (explicitType.HasValue)
                return explicitType.Value;

            return detectedType ?? InferFallbackType(summary, description, requestTypeText, nlp);
        }

        private static bool HasStrongRepairMarkers(string summary, string description, string requestTypeText)
        {
            var text = (summary + " " + description + " " + requestTypeText).ToLowerInvariant();

            if (text.Contains("не включ"))
                return true;

            var repairKeywords = new[]
            {
                "ремонт", "неисправ", "полом", "слом", "замен", "авар", "не работает", "не горит", "замык", "течь", "протеч", "вышел из строя"
            }.Concat(repairWordStems).ToArray();

            return repairKeywords.Any(k => text.Contains(k));
        }

        private static (double RepairScore, double SetupScore, double RepairProbability, double SetupProbability) EvaluateWorkTypeNlp(string summary, string description, string requestTypeText, string? explicitTypeText)
        {
            var text = ((summary ?? string.Empty) + " " + (description ?? string.Empty) + " " + (requestTypeText ?? string.Empty)).ToLowerInvariant();
            var explicitText = (explicitTypeText ?? string.Empty).ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(text))
                return (0, 0, 0.5, 0.5);

            static int Count(string source, string pattern) => Regex.Matches(source, Regex.Escape(pattern), RegexOptions.IgnoreCase).Count;

            // Простая NLP-оценка на основе n-грамм и ключевых стемов с весами
            var repairPhrases = new (string Pattern, double Weight)[]
            {
                ("не включ", 3.6),
                ("не работает", 2.8),
                ("не горит", 2.4),
                ("вышел из строя", 2.7),
                ("требует ремонта", 2.5),
                ("замена", 1.8),
                ("авар", 1.7),
                ("полом", 1.8),
                ("неисправ", 2.2),
                ("ремонт", 2.0),
                ("течь", 1.6),
                ("протеч", 1.6)
            };

            var setupPhrases = new (string Pattern, double Weight)[]
            {
                ("настрой", 2.2),
                ("настройка", 2.8),
                ("налад", 2.0),
                ("калибр", 2.0),
                ("регулир", 1.8),
                ("параметр", 1.6),
                ("пусконалад", 2.1),
                ("корректиров", 1.7),
                ("включ", 1.4),
                ("включение", 1.8),
                ("выключ", 1.4),
                ("отключ", 1.8),
                ("отключение", 1.9),
                ("подключ", 1.5),
                ("подключение", 1.9)
            };

            double repairScore = 0;
            double setupScore = 0;

            foreach (var (pattern, weight) in repairPhrases)
                repairScore += Count(text, pattern) * weight;

            // Производные ремонтных слов
            foreach (var stem in repairWordStems)
                repairScore += Count(text, stem) * 1.35;

            foreach (var (pattern, weight) in setupPhrases)
                setupScore += Count(text, pattern) * weight;

            if (requestTypeText.Contains("Проблем", StringComparison.OrdinalIgnoreCase) || requestTypeText.Contains("Инцидент", StringComparison.OrdinalIgnoreCase))
                repairScore += 0.8;

            if (requestTypeText.Contains("Запрос", StringComparison.OrdinalIgnoreCase) || requestTypeText.Contains("Обслужив", StringComparison.OrdinalIgnoreCase))
                setupScore += 0.6;

            if (explicitText.Contains("ремонт"))
                repairScore += 2.5;
            if (explicitText.Contains("настрой"))
                setupScore += 2.5;

            // Сглаживание, чтобы всегда получить вероятности
            var repairSmoothed = Math.Max(0, repairScore) + 1.0;
            var setupSmoothed = Math.Max(0, setupScore) + 1.0;
            var total = repairSmoothed + setupSmoothed;

            var repairProb = repairSmoothed / total;
            var setupProb = setupSmoothed / total;

            return (repairScore, setupScore, repairProb, setupProb);
        }
    }
}