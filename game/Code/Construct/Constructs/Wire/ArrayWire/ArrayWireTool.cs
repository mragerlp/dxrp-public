using Dxura.RP.Game.Wire;
using Dxura.RP.Shared;

namespace Dxura.RP.Game.Tools;

[Tool( "#tool.wire.array.name", "#tool.wire.array.description", "#tool.group.logic", Category = ToolCategory.Wire, MinimumLevel = 1 )]
public class ArrayWireTool() : BaseConstructTool<ArrayWireData>( ConstructType.ArrayWire )
{
	[Property]
	[Title( "Type" )]
	[Description( "Whether this array stores numbers or strings" )]
	public ArrayWireValueType ValueType
	{
		get => Data.ValueType;
		set => Data = Data with
		{
			ValueType = value
		};
	}
}
