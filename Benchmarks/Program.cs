using System;
using System.Numerics;
using AllColors;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Benchmarks;

//	[DisassemblyDiagnoser(printAsm: true, printSource: true)]
[LegacyJitX86Job, LegacyJitX64Job, RyuJitX64Job]
public class Program
{
	private const int ColorCount = 0b1_00000_00000_00000;
	private short[] colors = new short[ColorCount];
	private Vector3[] colorsVec = new Vector3[ColorCount];

	public Program()
	{
		var rng = new Random();
		for (var i = 0; i < ColorCount; i++)
		{
			var color = (short)rng.Next(ColorCount);
			colors[i] = color;
			colorsVec[i] = color.Unpack();
		}
	}

	static void Main(string[] args)
	{
		var summary = BenchmarkRunner.Run<Program>();

		Console.WriteLine(summary);
	}


	[Benchmark(Baseline = true)]
	public double CalcLinearDifferenceNrml()
	{
		var min = double.PositiveInfinity;
		var i = 0;
		//for (var i=0; i<colors.Length-1; i++)
		for (var j = i + 1; j < colors.Length; j++)
		{
			var diff = LinearDifference(colors[i], colors[j]);
			if (diff < min)
				min = diff;
		}
		return min;
	}

	[Benchmark]
	public float CalcLinearDifferenceSimd()
	{
		var min = float.PositiveInfinity;
		var i = 0;
		//for (var i = 0; i < colorsVec.Length - 1; i++)
		for (var j = i + 1; j < colorsVec.Length; j++)
		{
			var diff = LinearDifference(colorsVec[i], colorsVec[j]);
			if (diff < min)
				min = diff;
		}
		return min;
	}

	private static float LinearDifference(Vector3 vec1, Vector3 vec2)
	{
		return Vector3.Distance(vec1, vec2);
	}

	private static double LinearDifference(short rgb555_1, short rgb555_2)
	{
		var sum = 0.0;
		for (var i = 0; i < 3; i++)
		{
			sum += Math.Pow((rgb555_1 & 0b11111) - (rgb555_2 & 0b11111), 2.0);
			rgb555_1 >>= 5;
			rgb555_2 >>= 5;
		}
		return Math.Sqrt(sum);
	}
}