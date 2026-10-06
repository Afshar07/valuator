using ProjectOperations.Core.Agents;
using Xunit;

namespace ProjectOperations.Tests;

public sealed class TaskProposalParserTests
{
    [Fact]
    public void OrdinaryTextDoesNotCreateTasks() => Assert.Empty(TaskProposalParser.Parse("Request a forecast."));

    [Theory]
    [InlineData("null")]
    [InlineData("{\"tasks\":false}")]
    [InlineData("{bad json}")]
    [InlineData("{\"tasks\":[null,{},42,{\"title\":\"  \"}]}")]
    public void MalformedProposalsAreIgnored(string json)
        => Assert.Empty(TaskProposalParser.Parse($"```task-proposals\n{json}\n```"));

    [Fact]
    public void ValidProposalRemainsPendingUntilApproval()
    {
        var proposal = Assert.Single(TaskProposalParser.Parse("""
            Here are the suggested actions.
            ```task-proposals
            {"tasks":[{"title":" Request forecast ","description":"Financial data is missing","dueAt":"2026-10-14T12:00:00Z"}]}
            ```
            """));
        Assert.Equal("Request forecast", proposal.Title);
        Assert.Equal(ProposalReviewStatus.Pending, proposal.ReviewStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-10-14T12:00:00Z"), proposal.DueAt);
    }

    [Fact]
    public void DeadlineWithoutTimezoneIsNotAssumed()
    {
        var proposal = Assert.Single(TaskProposalParser.Parse("""
            ```task-proposals
            {"tasks":[{"title":"Review","dueAt":"2026-10-14T12:00:00"}]}
            ```
            """));
        Assert.Null(proposal.DueAt);
    }

    [Fact]
    public void ProposalCountIsBounded()
    {
        var json = "{\"tasks\":[" + string.Join(',', Enumerable.Repeat("{\"title\":\"Review\"}", 35)) + "]}";
        Assert.Equal(30, TaskProposalParser.Parse($"```task-proposals\n{json}\n```").Count);
    }
}
