using System;
using System.Text.RegularExpressions;

namespace EquipmentFailureAnalysis.Utility
{
    public struct EquipmentParseResult
    {
        public string Title { get; init; }
        public string? InventoryNumber { get; init; }
        public int Uid { get; init; }
    }

    public static class EquipmentNameParser
    {
        public static EquipmentParseResult Parse(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new EquipmentParseResult
                {
                    Title = "Unknown Equipment",
                    InventoryNumber = null,
                    Uid = 0
                };
            }

            var text = rawText.Trim();

            // 1. PRIMARY RULE: If text contains "инв" (case-insensitive) -> Inventory number IS ALWAYS PRESENT!
            if (text.Contains("инв", StringComparison.OrdinalIgnoreCase))
            {
                // Match "инв", "инв.", "инв №", "инв. №", "инв-", "инв:", "инв_", etc., followed by ID token
                // Handles formats like:
                // - "инв.340050"
                // - "инв. 340050"
                // - "инв № 340050"
                // - "инв. № 340050/1"
                // - "инв340050"
                // - "инв-340050"
                // - "(инв. 340050)"
                var matchInv = Regex.Match(
                    text,
                    @"(?<Prefix>[\(\[\s\t\-,:]*)?(?i:инв)[\.\s\t\-\№:_]*(?<ID>[a-zA-Z0-9\-\/]+)(?<Suffix>[\)\]\s\t]*)?",
                    RegexOptions.IgnoreCase);

                if (matchInv.Success)
                {
                    string inventoryNumber = matchInv.Groups["ID"].Value.Trim();
                    int.TryParse(inventoryNumber, out int uid);

                    string titleCandidate = text.Remove(matchInv.Index, matchInv.Length);
                    string title = CleanTitle(titleCandidate);

                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = inventoryNumber;
                    }

                    return new EquipmentParseResult
                    {
                        Title = title,
                        InventoryNumber = inventoryNumber,
                        Uid = uid
                    };
                }
            }

            // 2. Secondary prefix fallback: "зав.", "зав №", "зав", "№"
            var matchWithPrefix = Regex.Match(
                text,
                @"^(?<Name>.*?)[\s\t]+(?:(?:зав\.|№|зав)[\s\t\.\№]*)+(?<ID>[a-zA-Z0-9\-\/]+)$",
                RegexOptions.IgnoreCase);

            if (matchWithPrefix.Success)
            {
                string titleCandidate = matchWithPrefix.Groups["Name"].Value.Trim();
                string inventoryNumber = matchWithPrefix.Groups["ID"].Value.Trim();
                int.TryParse(inventoryNumber, out int uid);

                string title = CleanTitle(titleCandidate);
                if (string.IsNullOrWhiteSpace(title))
                    title = inventoryNumber;

                return new EquipmentParseResult
                {
                    Title = title,
                    InventoryNumber = inventoryNumber,
                    Uid = uid
                };
            }

            // 3. Fallback: Pure numeric suffix without "инв" (e.g., "Установка 129305")
            var matchPureNumeric = Regex.Match(text, @"^(?<Name>.*?)(?:[\s\t]+)(?<ID>\d+)$", RegexOptions.IgnoreCase);
            if (matchPureNumeric.Success)
            {
                var possibleId = matchPureNumeric.Groups["ID"].Value;
                var nameEnd = matchPureNumeric.Groups["Name"].Index + matchPureNumeric.Groups["Name"].Length;
                var idStart = matchPureNumeric.Groups["ID"].Index;
                var separatorSpan = text.Substring(nameEnd, idStart - nameEnd);

                // Treat as inventory number if separated by tab, or if length != 4 (to avoid model numbers like TDS 2002) and length >= 2
                if (separatorSpan.Contains('\t') || (possibleId.Length != 4 && possibleId.Length >= 2))
                {
                    string titleCandidate = matchPureNumeric.Groups["Name"].Value.Trim();
                    string title = CleanTitle(titleCandidate);
                    if (string.IsNullOrWhiteSpace(title))
                        title = possibleId;

                    int.TryParse(possibleId, out int uid);
                    return new EquipmentParseResult
                    {
                        Title = title,
                        InventoryNumber = possibleId,
                        Uid = uid
                    };
                }
            }

            // Default: no inventory number found
            return new EquipmentParseResult
            {
                Title = text,
                InventoryNumber = null,
                Uid = 0
            };
        }

        private static string CleanTitle(string cleanedTitle)
        {
            var title = cleanedTitle;

            // Remove orphaned brackets like "()" or "[]"
            title = Regex.Replace(title, @"\(\s*\)", string.Empty);
            title = Regex.Replace(title, @"\[\s*\]", string.Empty);
            title = title.Trim(' ', '\t', '\r', '\n', ':', '-', ',', ';', '.', '(', ')', '[', ']');

            return title;
        }
    }
}
