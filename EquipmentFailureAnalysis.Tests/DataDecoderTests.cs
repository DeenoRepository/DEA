using System;
using System.IO;
using System.Linq;
using System.Xml;
using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Utility;
using Xunit;

namespace EquipmentFailureAnalysis.Tests
{
    public class DataDecoderTests
    {
        private string CreateTempXml(string equipmentText, string summary, string description, string typeText = "", string status = "Решен")
        {
            var tempFile = Path.GetTempFileName();
            var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<rss version=""0.92"">
  <channel>
    <title>Сектор сборки (Портал поддержки АО ""НЗПП Восток"")</title>
    <item>
      <key>DEA-101</key>
      <summary>{summary}</summary>
      <description>{description}</description>
      <status>{status}</status>
      <created>Mon, 01 Jun 2026 10:00:00 +0300</created>
      <resolved>Mon, 01 Jun 2026 11:00:00 +0300</resolved>
      <assignee username=""ivanov"">Иван Иванов</assignee>
      <reporter>Петр Петров</reporter>
      <customfields>
        <customfield id=""customfield_10500"">
          <customfieldvalues>
            <customfieldvalue>{equipmentText}</customfieldvalue>
          </customfieldvalues>
        </customfield>
        <customfield id=""customfield_10501"">
          <customfieldvalues>
            <customfieldvalue>{typeText}</customfieldvalue>
          </customfieldvalues>
        </customfield>
      </customfields>
    </item>
  </channel>
</rss>";
            File.WriteAllText(tempFile, xml);
            return tempFile;
        }

        [Theory]
        [InlineData("ПКВ-2 58", "ПКВ-2", "58", 58)]
        [InlineData("Станок инв.340050", "Станок", "340050", 340050)]
        [InlineData("Прибор зав. № XY-890/A", "Прибор", "XY-890/A", 0)]
        [InlineData("Осциллограф TDS 2002", "Осциллограф TDS 2002", null, 0)]
        public void XmlDataDecoder_ShouldParseEquipmentAndInventoryNumberCorrectly(
            string rawText, string expectedTitle, string? expectedInvNumber, int expectedUid)
        {
            // Arrange
            var tempFile = CreateTempXml(rawText, "Тестовая задача", "Проблема с питанием");
            try
            {
                var decoder = new XmlDataDecoder(tempFile);

                // Act
                var result = decoder.DecodeEquipment();

                // Assert
                Assert.Single(result);
                var eq = result[0];
                Assert.Equal(expectedTitle, eq.Title);
                Assert.Equal(expectedInvNumber, eq.InventoryNumber);
                Assert.Equal(expectedUid, eq.Uid);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Theory]
        [InlineData("выполнен ремонт платы и замена конденсатора", "", IssueType.Ремонт)]
        [InlineData("требуется настройка и калибровка датчика температуры", "", IssueType.Настройка)]
        [InlineData("не работает станок, не включается питание", "Ремонт", IssueType.Ремонт)]
        [InlineData("проверка конфигурации и пусконаладка прибора", "Настройка", IssueType.Настройка)]
        [InlineData("не настраивается, требуется ремонт", "", IssueType.Ремонт)]
        public void XmlDataDecoder_NLPHeuristics_ShouldClassifyCorrectly(
            string description, string typeText, IssueType expectedType)
        {
            // Arrange
            var tempFile = CreateTempXml("Станок инв.100", "Сбой оборудования", description, typeText);
            try
            {
                var decoder = new XmlDataDecoder(tempFile);

                // Act
                var result = decoder.DecodeEquipment();

                // Assert
                Assert.Single(result);
                var issue = result[0].Issues.Single();
                Assert.Equal(expectedType, issue.Type);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
    }
}
