using NUnit.Framework;
using EywaClient.Utilities;

namespace EywaClient.Tests.Utilities;

[TestFixture]
public class PathParserTests
{
    [Test]
    public void Parse_WithFullPath_ReturnsCorrectComponents()
    {
        // Arrange
        var path = "/documents/2024/report.pdf";

        // Act
        var (folder, file) = PathParser.Parse(path);

        // Assert
        Assert.That(folder, Is.EqualTo("/documents/2024/"));
        Assert.That(file, Is.EqualTo("report.pdf"));
    }

    [Test]
    public void Parse_WithRootLevelFile_ReturnsRootFolder()
    {
        // Arrange
        var path = "/file.txt";

        // Act
        var (folder, file) = PathParser.Parse(path);

        // Assert
        Assert.That(folder, Is.EqualTo("/"));
        Assert.That(file, Is.EqualTo("file.txt"));
    }

    [Test]
    public void Parse_WithoutSlash_ReturnsNullFolder()
    {
        // Arrange
        var path = "file.txt";

        // Act
        var (folder, file) = PathParser.Parse(path);

        // Assert
        Assert.That(folder, Is.Null);
        Assert.That(file, Is.EqualTo("file.txt"));
    }

    [Test]
    public void Parse_WithNull_ReturnsNullFolderAndEmptyFile()
    {
        // Act
        var (folder, file) = PathParser.Parse(null);

        // Assert
        Assert.That(folder, Is.Null);
        Assert.That(file, Is.Empty);
    }

    [Test]
    public void GetSegments_WithNestedPath_ReturnsCorrectSegments()
    {
        // Arrange
        var path = "/documents/2024/Q1/";

        // Act
        var segments = PathParser.GetSegments(path);

        // Assert
        Assert.That(segments, Has.Count.EqualTo(3));
        Assert.That(segments[0], Is.EqualTo("documents"));
        Assert.That(segments[1], Is.EqualTo("2024"));
        Assert.That(segments[2], Is.EqualTo("Q1"));
    }

    [Test]
    public void GetSegments_WithRootPath_ReturnsEmptyList()
    {
        // Arrange
        var path = "/";

        // Act
        var segments = PathParser.GetSegments(path);

        // Assert
        Assert.That(segments, Is.Empty);
    }

    [Test]
    public void GetHierarchy_ReturnsAllParentPaths()
    {
        // Arrange
        var path = "/documents/2024/Q1/";

        // Act
        var hierarchy = PathParser.GetHierarchy(path);

        // Assert
        Assert.That(hierarchy, Has.Count.EqualTo(3));
        Assert.That(hierarchy[0], Is.EqualTo("/documents/"));
        Assert.That(hierarchy[1], Is.EqualTo("/documents/2024/"));
        Assert.That(hierarchy[2], Is.EqualTo("/documents/2024/Q1/"));
    }

    [Test]
    public void IsValidFolderPath_WithValidPath_ReturnsTrue()
    {
        Assert.That(PathParser.IsValidFolderPath("/documents/"), Is.True);
        Assert.That(PathParser.IsValidFolderPath("/"), Is.True);
    }

    [Test]
    public void IsValidFolderPath_WithInvalidPath_ReturnsFalse()
    {
        Assert.That(PathParser.IsValidFolderPath("/documents"), Is.False);
        Assert.That(PathParser.IsValidFolderPath(""), Is.False);
        Assert.That(PathParser.IsValidFolderPath(null), Is.False);
    }

    [Test]
    public void NormalizeFolderPath_AddsSlashes()
    {
        Assert.That(PathParser.NormalizeFolderPath("documents/2024"), Is.EqualTo("/documents/2024/"));
        Assert.That(PathParser.NormalizeFolderPath("/documents/2024"), Is.EqualTo("/documents/2024/"));
        Assert.That(PathParser.NormalizeFolderPath("documents/2024/"), Is.EqualTo("/documents/2024/"));
        Assert.That(PathParser.NormalizeFolderPath(""), Is.EqualTo("/"));
    }
}
