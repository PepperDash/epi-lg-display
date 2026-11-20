using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Lg.Display.Tests;

/// <summary>
/// Replies are gathered on "x". The line strings below are what a real panel produced: unsolicited
/// "mc" lines (no "x") and stray bytes around power changes land in front of the next reply.
/// </summary>
public class LgResponseParserTests
{
    [Theory]
    [InlineData("a 01 OK01", "a", "01", true, "01")]
    [InlineData("a 01 OK00x", "a", "01", true, "00")]
    [InlineData("b 01 OK90", "b", "01", true, "90")]
    [InlineData("f 01 OK0a", "f", "01", true, "0a")]
    [InlineData("f 01 NG00", "f", "01", false, "00")]
    [InlineData("b 01 NG92", "b", "01", false, "92")]
    [InlineData("a 1 OK1", "a", "1", true, "1")]
    [InlineData("d 0A OK01", "d", "0A", true, "01")]
    public void Parse_CleanReply(string line, string cmd, string id, bool ok, string data)
    {
        var r = LgResponseParser.Parse(line);

        r.Should().NotBeNull();
        r!.Command.Should().Be(cmd);
        r.SetId.Should().Be(id);
        r.IsOk.Should().Be(ok);
        r.Data.Should().Be(data);
        LgResponseParser.HasLeadingNoise(line).Should().BeFalse();
    }

    [Fact]
    public void Parse_PowerOffReplyAfterUnsolicitedMcLines_IsNotLost()
    {
        // Power on, then the held power off at the end of warm-up: the panel's three "mc 01 ea"
        // lines arrived first, and the old parser read the whole line as command "mc"
        const string line = "mc 01 ea\nmc 01 ea\nmc 01 ea\na 01 OK00";

        var r = LgResponseParser.Parse(line);

        r.Should().NotBeNull();
        r!.Command.Should().Be("a");
        r.IsOk.Should().BeTrue();
        r.Data.Should().Be("00");
        LgResponseParser.HasLeadingNoise(line).Should().BeTrue();
    }

    [Fact]
    public void Parse_ReplyAfterStrayBytesAndMcLines_IsNotLost()
    {
        const string line = "mc 01 eb\nmc 01 eb\nýa 01 OK00";

        var r = LgResponseParser.Parse(line);

        r.Should().NotBeNull();
        r!.Command.Should().Be("a");
        r.Data.Should().Be("00");
    }

    [Fact]
    public void Parse_InputReplyAfterMcLines_IsNotLost()
    {
        var r = LgResponseParser.Parse("ùýmc 01 ea\r\nb 01 OK91");

        r.Should().NotBeNull();
        r!.Command.Should().Be("b");
        r.Data.Should().Be("91");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("mc 01 ea\n")]
    [InlineData("ý")]
    [InlineData("a 01")]
    public void Parse_NoReply_ReturnsNull(string? line)
    {
        LgResponseParser.Parse(line!).Should().BeNull();
    }
}
