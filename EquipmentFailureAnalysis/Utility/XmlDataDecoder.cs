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

        // ID полей оборудования из разных проектов (Сектор сборки, Сектор измерений и др.)
        private readonly string[] equipmentFieldIds = {
            "customfield_10500", // Сектор сборки (2.xml)
            "customfield_10519", // Сектор сборки (1.xml)
            "customfield_10524"  // Сектор измерений (3.xml)
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

        public ObservableCollection<EquipmentInfo> DecodeEquipment()
        {
            XmlNodeList xmlNodeList = xmlDocument.GetElementsByTagName("item");
            equipmentCollection = new ObservableCollection<EquipmentInfo>();

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                // Фильтр по статусу (пропускаем нерешенные задачи)
                string status = xmlNode["status"]?.InnerText ?? string.Empty;
                if (!status.Equals("Решен", StringComparison.OrdinalIgnoreCase))
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
                    // Регулярное выражение для парсинга названий типа:
                    // "ПКВ-2 58" или "Станок инв.340050" или "Прибор зав.1445"
                    var m = Regex.Match(equipmentText, @"^(?<Name>.*?)[\s\t]+(?:инв\.|зав\.)?[\s\t]*(?<ID>\d+)$", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        title = m.Groups["Name"].Value.Trim();
                        inventoryNumber = m.Groups["ID"].Value;
                        int.TryParse(inventoryNumber, out uid);
                    }
                    else
                    {
                        title = equipmentText;
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

                // 5. Ответственный (Assignee или ФИО Автора 10502)
                string? responsible = xmlNode["assignee"]?.Attributes?["username"]?.Value
                                     ?? xmlNode["assignee"]?.InnerText;

                if (string.IsNullOrEmpty(responsible) || responsible == "-1")
                {
                    var respNode = xmlNode.SelectSingleNode("customfields/customfield[@id='customfield_10502']/customfieldvalues/customfieldvalue");
                    responsible = respNode?.InnerText?.Trim();
                }

                var issue = new Issue
                {
                    Start = start,
                    End = end,
                    Description = description,
                    Type = issueType,
                    Responsible = string.IsNullOrEmpty(responsible) ? "Не назначен" : responsible
                };

                // NLP-like heuristic: token scoring with keyword lists and simple normalization
                try
                {
                    var raw = ((description ?? string.Empty) + " " + (title ?? string.Empty) + " " + (typeText ?? string.Empty)).ToLowerInvariant();
                    // normalize common punctuation and collapse spaces
                    raw = Regex.Replace(raw, "[\\p{P}\t\\n\\r]+", " ").Trim();

                    var repairKeywords = new string[] { "ремонт", "поломк", "слом", "замен", "неисправн", "поврежд", "не работает", "не запуска", "не включ", "авар", "протеч", "течь" };
                    var setupKeywords = new string[] { "настрой", "налад", "калибр", "конфиг", "параметр", "пусконал", "установк", "тонк", "регул" };

                    int repairScore = 0;
                    int setupScore = 0;

                    // tokenize into words for context window analysis
                    var tokens = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    // simple negation handling words
                    var negations = new string[] { "не", "нет", "без", "исключая" };

                    // scan tokens and score by proximity: if keyword appears near a negation, reduce its weight
                    int window = 3; // tokens left/right considered
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        var t = tokens[i];
                        foreach (var k in repairKeywords)
                        {
                            if (t.Contains(k))
                            {
                                // check negation in the previous window
                                bool neg = false;
                                for (int j = Math.Max(0, i - window); j < i; j++)
                                    if (negations.Any(n => tokens[j] == n)) { neg = true; break; }
                                repairScore += neg ? 0 : 1;
                            }
                        }
                        foreach (var k in setupKeywords)
                        {
                            if (t.Contains(k))
                            {
                                bool neg = false;
                                for (int j = Math.Max(0, i - window); j < i; j++)
                                    if (negations.Any(n => tokens[j] == n)) { neg = true; break; }
                                setupScore += neg ? 0 : 1;
                            }
                        }
                    }

                    // boost by explicit type field
                    if (!string.IsNullOrEmpty(typeText))
                    {
                        if (typeText.IndexOf("настрой", StringComparison.OrdinalIgnoreCase) >= 0) setupScore += 4;
                        if (typeText.IndexOf("ремонт", StringComparison.OrdinalIgnoreCase) >= 0) repairScore += 4;
                    }

                    // final decision with simple thresholds
                    EquipmentFailureAnalysis.Models.IssueType? detected = null;
                    if (repairScore + setupScore == 0)
                        detected = null;
                    else if (repairScore >= setupScore + 2)
                        detected = EquipmentFailureAnalysis.Models.IssueType.Ремонт;
                    else if (setupScore >= repairScore + 2)
                        detected = EquipmentFailureAnalysis.Models.IssueType.Настройка;
                    else
                    {
                        // close scores -> fallback to explicit field
                        if (!string.IsNullOrEmpty(typeText))
                        {
                            if (typeText.IndexOf("настрой", StringComparison.OrdinalIgnoreCase) >= 0)
                                detected = EquipmentFailureAnalysis.Models.IssueType.Настройка;
                            else if (typeText.IndexOf("ремонт", StringComparison.OrdinalIgnoreCase) >= 0)
                                detected = EquipmentFailureAnalysis.Models.IssueType.Ремонт;
                        }
                    }

                    issue.DetectedType = detected;
                    issue.TypeSuspicious = detected.HasValue && detected.Value != issue.Type;
                }
                catch
                {
                    // don't fail parsing on analysis errors
                }

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
    }
}