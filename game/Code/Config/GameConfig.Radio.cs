namespace Dxura.RP.Game;

public abstract partial class GameConfig
{
	// Radio
	public virtual string[] RadioStations { get; set; } =
	[
		"Smooth Jazz|https://ais-edge89-dal02.cdnstream.com/2124_128.mp3",
		"Dance FM|https://broadcast.dancefmlive.com/radio/8010/radio.mp3",
		"Fox News|https://tunein.cdnstream1.com/2869_96_nn.mp3",
		"NPR News|https://npr-ice.streamguys1.com/live.mp3",
		"Groove Salad|https://ice1.somafm.com/groovesalad-128-mp3",
		"Seventies|https://ice2.somafm.com/seventies-128-mp3",
		"Underground 80s|https://ice1.somafm.com/u80s-128-mp3",
		"PopTron|https://ice4.somafm.com/poptron-128-mp3",
	];
}
