using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sandbox;

[TestClass]
public sealed class KeypadEntryStateTests
{
	[TestMethod]
	public void AppendsDigitsInOrder()
	{
		var state = CreateState( "1234" );

		state.AppendDigit( '1' );
		state.AppendDigit( '2' );

		Assert.AreEqual( "12", state.EnteredCode );
	}

	[TestMethod]
	public void RejectsNonDigitsAndInputPastPinLength()
	{
		var state = CreateState( "12" );

		Assert.IsFalse( state.AppendDigit( 'x' ) );
		Assert.IsTrue( state.AppendDigit( '1' ) );
		Assert.IsTrue( state.AppendDigit( '2' ) );
		Assert.IsFalse( state.AppendDigit( '3' ) );
		Assert.AreEqual( "12", state.EnteredCode );
	}

	[TestMethod]
	public void BackspaceRemovesLastDigit()
	{
		var state = CreateState( "1234" );
		state.AppendDigit( '1' );
		state.AppendDigit( '2' );

		Assert.IsTrue( state.Backspace() );
		Assert.AreEqual( "1", state.EnteredCode );
	}

	[TestMethod]
	public void ClearRemovesAllEnteredDigits()
	{
		var state = CreateState( "1234" );
		state.AppendDigit( '1' );
		state.AppendDigit( '2' );

		state.Clear();

		Assert.AreEqual( string.Empty, state.EnteredCode );
	}

	[TestMethod]
	public void SubmitAcceptsOnlyExactPin()
	{
		var state = CreateState( "0451" );
		Enter( state, "0451" );

		Assert.IsTrue( state.Submit() );
	}

	[TestMethod]
	public void IncorrectSubmitClearsEntryForRetry()
	{
		var state = CreateState( "1234" );
		Enter( state, "1233" );

		Assert.IsFalse( state.Submit() );
		Assert.AreEqual( string.Empty, state.EnteredCode );
	}

	[TestMethod]
	public void SupportsVariablePinLengths()
	{
		var state = CreateState( "987654" );
		Enter( state, "987654" );

		Assert.AreEqual( 6, state.PinLength );
		Assert.IsTrue( state.Submit() );
	}

	private static KeypadEntryState CreateState( string pin )
	{
		var state = new KeypadEntryState();
		state.Configure( pin );
		return state;
	}

	private static void Enter( KeypadEntryState state, string digits )
	{
		foreach ( var digit in digits )
			state.AppendDigit( digit );
	}
}
