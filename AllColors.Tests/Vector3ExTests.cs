using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AllColors.Tests;

[TestClass]
public class Vector3ExTests
{
	[TestMethod]
	public void PackAndUnpack()
	{
		const int uniqueColors = 0b1_00000_00000_00000;
		for (var i = 0; i < uniqueColors; i++)
		{
			var expected = (short)i;
			var actual = ((short)i).Unpack().Pack();
			Assert.AreEqual(expected, actual);
		}
	}
}