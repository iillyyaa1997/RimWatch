using Xunit;
using RimWatch.Core;
using RimWatch.Utils;
using RimWatch.AI;
using RimWatch.AI.Storytellers;

namespace RimWatch.Tests
{
    /// <summary>
    /// Базовые тесты для проверки компиляции и основных классов
    /// </summary>
    public class BasicTests
    {
        [Fact]
        [Trait("Category", "Unit")]
        public void Logger_ShouldNotThrow()
        {
            // Arrange & Act & Assert
            RimWatchLogger.Info("Test message");
            RimWatchLogger.Warning("Test warning");
            RimWatchLogger.Error("Test error");
            RimWatchLogger.Debug("Test debug");
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BalancedStoryteller_ShouldHaveCorrectProperties()
        {
            // Arrange
            var storyteller = new BalancedStoryteller();

            // Assert
            Assert.NotNull(storyteller.Name);
            Assert.NotNull(storyteller.Icon);
            Assert.NotNull(storyteller.Description);
            Assert.Equal("⚖️", storyteller.Icon);
            Assert.Contains("Сбалансированный", storyteller.Name);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void RimWatchCore_ShouldInitializeWithDefaults()
        {
            // Arrange & Act
            // Core инициализируется статически, просто проверяем состояние

            // Assert
            Assert.NotNull(RimWatchCore.CurrentStoryteller);
            Assert.False(RimWatchCore.AutopilotEnabled); // По умолчанию выключен
            Assert.True(RimWatchCore.WorkEnabled); // Work включен по умолчанию
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void AutopilotStatus_ShouldHaveCorrectValues()
        {
            // Arrange
            var statusDisabled = AutopilotStatus.Disabled;
            var statusGood = AutopilotStatus.ActiveGood;
            var statusWarning = AutopilotStatus.ActiveWarning;
            var statusInactive = AutopilotStatus.Inactive;

            // Assert
            Assert.NotEqual(statusDisabled, statusGood);
            Assert.NotEqual(statusDisabled, statusWarning);
            Assert.NotEqual(statusGood, statusWarning);
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void ActiveAutomationsCount_ShouldCalculateCorrectly()
        {
            // Arrange
            RimWatchCore.WorkEnabled = true;
            RimWatchCore.BuildingEnabled = false;
            RimWatchCore.FarmingEnabled = false;
            RimWatchCore.DefenseEnabled = false;
            RimWatchCore.TradeEnabled = false;
            RimWatchCore.MedicalEnabled = false;
            RimWatchCore.SocialEnabled = false;
            RimWatchCore.ResearchEnabled = false;

            // Act
            int count = RimWatchCore.ActiveAutomationsCount;

            // Assert
            Assert.Equal(1, count); // Только Work включен
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void BalancedStoryteller_GetFullName_ShouldReturnIconAndName()
        {
            // Arrange
            var storyteller = new BalancedStoryteller();

            // Act
            string fullName = storyteller.GetFullName();

            // Assert
            Assert.Contains("⚖️", fullName);
            Assert.Contains("Сбалансированный", fullName);
        }
    }
}

