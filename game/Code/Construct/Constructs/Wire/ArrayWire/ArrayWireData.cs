namespace Dxura.RP.Game.Wire;

public record ArrayWireData : IConstructData
{
	public uint SchemaVersion => 1;
	public ArrayWireValueType ValueType { get; set; } = ArrayWireValueType.Number;
}
