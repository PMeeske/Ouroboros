using Xunit;
using FluentAssertions;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Domain.Environment;
using Ouroboros.Pipeline.Memory;
using LangChain.DocumentLoaders;

namespace Ouroboros.Tests.Pipeline.Branches;

[Trait("Category", "Unit")]
[Trait("Area", "EpisodeDagIntegration")]
public class EpisodeDagIntegrationTests
{
    [Fact]
    public void WithEpisode_ShouldAddEpisodeEventToBranch()
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(".");
        var branch = new PipelineBranch("test-branch", store, source);
        
        var episode = CreateTestEpisode();
        
        var newBranch = branch.WithEpisode(episode);
        
        newBranch.Events.Count.Should().Be(1);
        newBranch.Events[0].Should().BeOfType<Ouroboros.Domain.Events.EpisodeEvent>();
    }
    
    [Fact]
    public void RecordEpisode_ShouldAddEpisodeToBranch()
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(".");
        var branch = new PipelineBranch("test-branch", store, source);
        
        var episode = CreateTestEpisode();
        
        var newBranch = branch.RecordEpisode(episode);
        
        newBranch.Events.Count.Should().Be(1);
    }
    
    [Fact]
    public void RecordEpisodes_ShouldAddMultipleEpisodes()
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(".");
        var branch = new PipelineBranch("test-branch", store, source);
        
        var episodes = new[] 
        {
            CreateTestEpisode(reward: 10.0),
            CreateTestEpisode(reward: 20.0),
            CreateTestEpisode(reward: 15.0)
        };
        
        var newBranch = branch.RecordEpisodes(episodes);
        
        newBranch.Events.Count.Should().Be(3);
    }
    
    [Fact]
    public void GetEpisodes_ShouldReturnRecordedEpisodes()
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(".");
        var branch = new PipelineBranch("test-branch", store, source);
        
        var episode1 = CreateTestEpisode(reward: 10.0);
        var episode2 = CreateTestEpisode(reward: 20.0);
        
        branch = branch.RecordEpisode(episode1).RecordEpisode(episode2);
        
        var episodes = branch.GetEpisodes().ToList();
        
        episodes.Count.Should().Be(2);
        episodes.Should().Contain(e => e.TotalReward == 10.0);
        episodes.Should().Contain(e => e.TotalReward == 20.0);
    }
    
    [Fact]
    public void GetEpisodeStatistics_ShouldCalculateCorrectStats()
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(".");
        var branch = new PipelineBranch("test-branch", store, source);
        
        var episodes = new[]
        {
            CreateTestEpisode(reward: 10.0, success: true),
            CreateTestEpisode(reward: 20.0, success: true),
            CreateTestEpisode(reward: 5.0, success: false)
        };
        
        branch = branch.RecordEpisodes(episodes);
        var stats = branch.GetEpisodeStatistics();
        
        stats.TotalEpisodes.Should().Be(3);
        stats.SuccessfulEpisodes.Should().Be(2);
        stats.SuccessRate.Should().BeApproximately(0.666, 0.01);
        stats.AverageReward.Should().BeApproximately(11.666, 0.01);
        stats.TotalReward.Should().Be(35.0);
    }
    
    [Fact]
    public void GetBestEpisode_ShouldReturnHighestRewardEpisode()
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(".");
        var branch = new PipelineBranch("test-branch", store, source);
        
        var episodes = new[]
        {
            CreateTestEpisode(reward: 10.0),
            CreateTestEpisode(reward: 25.0),
            CreateTestEpisode(reward: 15.0)
        };
        
        branch = branch.RecordEpisodes(episodes);
        var best = branch.GetBestEpisode();
        
        best.Should().NotBeNull();
        best!.TotalReward.Should().Be(25.0);
    }
    
    [Fact]
    public void GetEpisodeStatistics_WithNoEpisodes_ShouldReturnZeroStats()
    {
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath(".");
        var branch = new PipelineBranch("test-branch", store, source);
        
        var stats = branch.GetEpisodeStatistics();
        
        stats.TotalEpisodes.Should().Be(0);
        stats.SuccessfulEpisodes.Should().Be(0);
        stats.SuccessRate.Should().Be(0.0);
        stats.AverageReward.Should().Be(0.0);
    }
    
    private Ouroboros.Domain.Environment.Episode CreateTestEpisode(double reward = 10.0, bool success = true)
    {
        return new Ouroboros.Domain.Environment.Episode(
            Id: Guid.NewGuid(),
            EnvironmentName: "TestEnvironment",
            Steps: new List<EnvironmentStep>().AsReadOnly(),
            TotalReward: reward,
            StartTime: DateTime.UtcNow.AddMinutes(-1),
            EndTime: DateTime.UtcNow,
            Success: success,
            Metadata: null
        );
    }
}
