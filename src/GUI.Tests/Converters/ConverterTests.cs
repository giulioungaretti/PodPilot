using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GUI.Converters;

namespace GUI.Tests.Converters;

[TestClass]
public class ConverterTests
{
    [TestMethod]
    public void TimeAgoConverter_JustNow_ReturnsCorrectString()
    {
        // Arrange
        var converter = new TimeAgoConverter();
        var time = DateTime.Now.AddSeconds(-30);

        // Act
        var result = converter.Convert(time, typeof(string), null, "en-US");

        // Assert
        Assert.AreEqual("just now", result);
    }

    [TestMethod]
    public void TimeAgoConverter_Minutes_ReturnsCorrectString()
    {
        // Arrange
        var converter = new TimeAgoConverter();
        var time = DateTime.Now.AddMinutes(-5);

        // Act
        var result = converter.Convert(time, typeof(string), null, "en-US");

        // Assert
        Assert.AreEqual("5m ago", result);
    }

    [TestMethod]
    public void TimeAgoConverter_Hours_ReturnsCorrectString()
    {
        // Arrange
        var converter = new TimeAgoConverter();
        var time = DateTime.Now.AddHours(-2);

        // Act
        var result = converter.Convert(time, typeof(string), null, "en-US");

        // Assert
        Assert.AreEqual("2h ago", result);
    }

    [TestMethod]
    public void TimeAgoConverter_Days_ReturnsCorrectString()
    {
        // Arrange
        var converter = new TimeAgoConverter();
        var time = DateTime.Now.AddDays(-3);

        // Act
        var result = converter.Convert(time, typeof(string), null, "en-US");

        // Assert
        Assert.AreEqual("3d ago", result);
    }

    [TestMethod]
    public void TimeAgoConverter_InvalidType_ReturnsEmptyString()
    {
        // Arrange
        var converter = new TimeAgoConverter();

        // Act
        var result = converter.Convert("not a date", typeof(string), null, "en-US");

        // Assert
        Assert.AreEqual("", result);
    }
}
