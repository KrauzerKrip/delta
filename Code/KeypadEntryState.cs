namespace Sandbox;

/// <summary>Engine-independent PIN entry and comparison state.</summary>
public sealed class KeypadEntryState
{
	public string PinCode { get; private set; } = string.Empty;
	public string EnteredCode { get; private set; } = string.Empty;
	public int PinLength => PinCode.Length;

	public void Configure( string pinCode )
	{
		PinCode = pinCode ?? string.Empty;
		EnteredCode = string.Empty;
	}

	public bool AppendDigit( char digit )
	{
		if ( digit < '0' || digit > '9' || EnteredCode.Length >= PinLength )
			return false;

		EnteredCode += digit;
		return true;
	}

	public bool Backspace()
	{
		if ( EnteredCode.Length == 0 )
			return false;

		EnteredCode = EnteredCode[..^1];
		return true;
	}

	public void Clear()
	{
		EnteredCode = string.Empty;
	}

	public bool Submit()
	{
		var accepted = EnteredCode == PinCode;
		if ( !accepted )
			Clear();

		return accepted;
	}
}
