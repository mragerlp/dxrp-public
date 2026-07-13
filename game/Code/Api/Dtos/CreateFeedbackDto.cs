namespace Dxura.RP.Shared;

public class CreateFeedbackDto
{
	public required FeedbackType Type { get; init; }
	public required string Title { get; init; }
	public required string Description { get; init; }
	public string? Screenshot { get; init; }
	public string? Version { get; init; }
}
