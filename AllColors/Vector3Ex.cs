using System.Numerics;

namespace AllColors
{
	public static class Vector3Ex
	{
		public static Vector3 Unpack(this short rgb555)
		{
			return new Vector3(rgb555 & 0b11111, (rgb555 >> 5) & 0b11111, (rgb555 >> 10) & 0b11111);
		}

		public static short Pack(this Vector3 vector)
		{
			vector = Vector3.Abs(vector);
			return (short)(((int)vector.X & 0b11111) | (((int)vector.Y & 0b11111) << 5) | (((int)vector.Z & 0b11111) << 10));
		}
	}
}