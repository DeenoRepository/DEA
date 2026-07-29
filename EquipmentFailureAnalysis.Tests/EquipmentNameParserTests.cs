using EquipmentFailureAnalysis.Utility;
using Xunit;

namespace EquipmentFailureAnalysis.Tests
{
    public class EquipmentNameParserTests
    {
        [Theory]
        [InlineData("Установка герметизации в ленте инв.340050", "Установка герметизации в ленте", "340050", 340050)]
        [InlineData("Установка герметизации ПГРС-1 инв. 334864", "Установка герметизации ПГРС-1", "334864", 334864)]
        [InlineData("Установка герметизации ПГРС-1 инв.12930", "Установка герметизации ПГРС-1", "12930", 12930)]
        [InlineData("Установка герметизации металлостеклянная (инв. № 5020)", "Установка герметизации металлостеклянная", "5020", 5020)]
        [InlineData("Маркер лазерный инв №340050/1", "Маркер лазерный", "340050/1", 0)]
        [InlineData("Станок инв. № 11491", "Станок", "11491", 11491)]
        [InlineData("Печь терм. инв-334865", "Печь терм", "334865", 334865)]
        public void Parse_WhenContainsInv_ExtractsInventoryNumberAndCleansTitle(
            string input, string expectedTitle, string expectedInv, int expectedUid)
        {
            var result = EquipmentNameParser.Parse(input);

            Assert.Equal(expectedTitle, result.Title);
            Assert.Equal(expectedInv, result.InventoryNumber);
            Assert.Equal(expectedUid, result.Uid);
        }

        [Fact]
        public void Parse_WhenOnlyInvString_ExtractsInventoryNumberAndKeepsTextAsTitle()
        {
            var result = EquipmentNameParser.Parse("инв. 12930");

            Assert.Equal("12930", result.InventoryNumber);
            Assert.Equal("12930", result.Title);
            Assert.Equal(12930, result.Uid);
        }

        [Fact]
        public void Parse_WhenPureNumericSuffix_ExtractsInventoryNumber()
        {
            var result = EquipmentNameParser.Parse("Установка герметизации 129305");

            Assert.Equal("Установка герметизации", result.Title);
            Assert.Equal("129305", result.InventoryNumber);
            Assert.Equal(129305, result.Uid);
        }

        [Fact]
        public void Parse_WhenModelNumberWithoutInv_DoesNotExtractAsInventoryNumber()
        {
            var result = EquipmentNameParser.Parse("Осциллограф TDS 2002");

            Assert.Equal("Осциллограф TDS 2002", result.Title);
            Assert.Null(result.InventoryNumber);
            Assert.Equal(0, result.Uid);
        }

        [Fact]
        public void Parse_WhenNoInventoryNumber_ReturnsNullInventoryNumber()
        {
            var result = EquipmentNameParser.Parse("Оборудование тестовое общее");

            Assert.Equal("Оборудование тестовое общее", result.Title);
            Assert.Null(result.InventoryNumber);
            Assert.Equal(0, result.Uid);
        }
    }
}
