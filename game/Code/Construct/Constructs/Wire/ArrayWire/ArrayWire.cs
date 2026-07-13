namespace Dxura.RP.Game.Wire;

[Title( "Array" )]
[Category( "Wire" )]
[Icon( "data_array" )]
public class ArrayWire() : BaseWireConstruct( ConstructType.ArrayWire ), IWireEvents
{
	private ArrayWireData _data = new();
	private readonly Dictionary<int, float> _numberValues = new();
	private readonly Dictionary<int, string> _stringValues = new();

	private bool _lastWriteState;
	private bool _lastClearIndexState;
	private bool _lastClearAllState;

	[WireInput( "index" )]
	private float Index { get; set; }

	[WireInput( "value" )]
	private object? Value { get; set; }

	[WireInput( "write" )]
	private bool Write { get; set; }

	[WireInput( "clear_index" )]
	private bool ClearIndex { get; set; }

	[WireInput( "clear_all" )]
	private bool ClearAll { get; set; }

	[WireOutput( "has_value" )]
	private bool HasValue { get; set; }

	[WireOutput( "count" )]
	private float Count { get; set; }

	[WireOutput( "length" )]
	private float Length { get; set; }

	[WireOutput( "index_valid" )]
	private bool IndexValid { get; set; }

	public override string Name => $"Array ({_data.ValueType}) ({GetUsedIndexCount()}/{ArrayWireDefinition.MaxArraySize})";

	protected override void OnStart()
	{
		if ( !IsPreview )
		{
			SetData( DataJson );
			RegisterOutputPort( "value", GetValueWireType() );
		}

		base.OnStart();
		UpdateOutputs();
	}

	protected override void OnDataChanged( IConstructData oldData, IConstructData newData )
	{
		var oldValueType = _data.ValueType;
		_data = newData as ArrayWireData ?? new ArrayWireData();

		if ( oldValueType != _data.ValueType )
		{
			ClearStoredValues();
		}

		UpdateOutputs();
	}

	public override void OnWireInput( string inputId, WireValue value )
	{
		base.OnWireInput( inputId, value );

		if ( inputId == "index" )
		{
			UpdateOutputs();
		}
	}

	public void OnWireTick()
	{
		if ( ClearAll && !_lastClearAllState )
		{
			ClearAllValues();
		}
		else if ( ClearIndex && !_lastClearIndexState )
		{
			ClearCurrentIndex();
		}
		else if ( Write && !_lastWriteState )
		{
			WriteValue();
		}

		_lastWriteState = Write;
		_lastClearIndexState = ClearIndex;
		_lastClearAllState = ClearAll;
	}

	private void WriteValue()
	{
		if ( !TryGetIndex( out var index ) )
		{
			UpdateOutputs();
			return;
		}

		switch ( _data.ValueType )
		{
			case ArrayWireValueType.Number:
				_numberValues[index] = ConvertToFloat( Value );
				break;
			case ArrayWireValueType.String:
				_stringValues[index] = TrimStoredString( Value?.ToString() ?? string.Empty );
				break;
		}

		UpdateOutputs();
	}

	private void ClearCurrentIndex()
	{
		if ( !TryGetIndex( out var index ) )
		{
			UpdateOutputs();
			return;
		}

		switch ( _data.ValueType )
		{
			case ArrayWireValueType.Number:
				_numberValues.Remove( index );
				break;
			case ArrayWireValueType.String:
				_stringValues.Remove( index );
				break;
		}

		UpdateOutputs();
	}

	private void ClearAllValues()
	{
		ClearStoredValues();
		UpdateOutputs();
	}

	private void UpdateOutputs()
	{
		var isValidIndex = TryGetIndex( out var index );
		IndexValid = isValidIndex;
		Length = GetLength();
		Count = GetUsedIndexCount();

		if ( !isValidIndex )
		{
			HasValue = false;
			SetDefaultValueOutput();
			return;
		}

		HasValue = _data.ValueType switch
		{
			ArrayWireValueType.Number => _numberValues.ContainsKey( index ),
			ArrayWireValueType.String => _stringValues.ContainsKey( index ),
			_ => false
		};
		SetValueOutput( index );
	}

	private void SetValueOutput( int index )
	{
		switch ( _data.ValueType )
		{
			case ArrayWireValueType.Number:
				Wire.Current?.SetOutputValue( this, "value", _numberValues.GetValueOrDefault( index ) );
				break;
			case ArrayWireValueType.String:
				Wire.Current?.SetOutputValue( this, "value", _stringValues.GetValueOrDefault( index, string.Empty ) );
				break;
		}
	}

	private void SetDefaultValueOutput()
	{
		switch ( _data.ValueType )
		{
			case ArrayWireValueType.Number:
				Wire.Current?.SetOutputValue( this, "value", 0f );
				break;
			case ArrayWireValueType.String:
				Wire.Current?.SetOutputValue( this, "value", string.Empty );
				break;
		}
	}

	private WireType GetValueWireType()
	{
		return _data.ValueType == ArrayWireValueType.String ? WireType.String : WireType.Number;
	}

	private bool TryGetIndex( out int index )
	{
		index = (int)MathF.Floor( Index );
		return index is >= 0 and < ArrayWireDefinition.MaxArraySize;
	}

	private float GetLength()
	{
		return _data.ValueType switch
		{
			ArrayWireValueType.Number => _numberValues.Count > 0 ? _numberValues.Keys.Max() + 1 : 0f,
			ArrayWireValueType.String => _stringValues.Count > 0 ? _stringValues.Keys.Max() + 1 : 0f,
			_ => 0f
		};
	}

	private int GetUsedIndexCount()
	{
		return _data.ValueType switch
		{
			ArrayWireValueType.Number => _numberValues.Count,
			ArrayWireValueType.String => _stringValues.Count,
			_ => 0
		};
	}

	private void ClearStoredValues()
	{
		_numberValues.Clear();
		_stringValues.Clear();
	}

	private static float ConvertToFloat( object? value )
	{
		return value switch
		{
			float f => f,
			int i => i,
			uint ui => ui,
			bool b => b ? 1f : 0f,
			string s when float.TryParse( s, out var f ) => f,
			_ => 0f
		};
	}

	private static string TrimStoredString( string value )
	{
		return value.Length <= ArrayWireDefinition.MaxStoredStringLength
			? value
			: value[..ArrayWireDefinition.MaxStoredStringLength];
	}
}
