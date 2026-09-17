using System;
using System.Collections.Generic;
using LifePunch.DXRP.Addons.Morpheus;

internal static class CivicLengthProbe
{
	private static int Main()
	{
		var law = new string( 'A', 40 );
		var laws = new List<string>();
		for ( var i = 0; i < 12; i++ )
			laws.Add( law );

		var result = LpMorpheusSpeech.BuildCivicBriefing( Array.Empty<string>(), laws );
		var hasOmission = result.IndexOf( "omitted", StringComparison.OrdinalIgnoreCase ) >= 0;
		var fits = result.Length <= LpMorpheusSpeech.MaxLength;
		var ok = fits && hasOmission;

		Console.WriteLine(
			$"RESULT civic_length length={result.Length} max={LpMorpheusSpeech.MaxLength} " +
			$"omitted={hasOmission} ok={ok}" );
		return ok ? 0 : 1;
	}
}
