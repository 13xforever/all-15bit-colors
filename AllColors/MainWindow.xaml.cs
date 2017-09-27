using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Security.Permissions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Vector = System.Windows.Vector;

namespace AllColors
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void window1_Loaded(object sender, RoutedEventArgs e)
		{
			var borderWidth = window1.ActualWidth - grid1.ActualWidth;
			var borderHeight = window1.ActualHeight - grid1.ActualHeight;
			var widthRatio = window1.ActualWidth / window1.Width;
			var heightRatio = window1.ActualHeight / window1.Height;
			window1.Width = (image1.Width + borderWidth) / widthRatio;
			window1.Height = (image1.Height + borderHeight) /heightRatio;

			var widthScaleFactor = (int)((SystemParameters.FullPrimaryScreenWidth - borderWidth) / image1.Width);
			var heightScaleFactor = (int)((SystemParameters.FullPrimaryScreenHeight - borderHeight) / image1.Height);
			var scaleFactor = Math.Min(widthScaleFactor, heightScaleFactor);
			window1.Width = (image1.Width * scaleFactor + borderWidth) / widthRatio;
			window1.Height = (image1.Height * scaleFactor + borderHeight) / heightRatio;
			window1.Left = (SystemParameters.FullPrimaryScreenWidth - window1.Width) / 2;
			window1.Top = (SystemParameters.FullPrimaryScreenHeight - window1.Height) / 2;
			image1.RenderTransform = new ScaleTransform(scaleFactor, scaleFactor);

			while (!cancel)
			{
				image1_Generate(null, null);
				for (var t = 0; t < 60; t++)
				{
					Thread.Sleep(16);
					DoEvents();
				}
			}
		}

		private void image1_Generate(object sender, InputEventArgs e)
		{
			cancel = true;
			if (!syncObj.Wait(0))
				return;

			var title = window1.Title;
			cancel = false;
			List<(byte x, byte y)> startCoords = new List<(byte x, byte y)>();
			switch (e)
			{
				case MouseEventArgs me:
					var mPos = me.GetPosition(image1);
					startCoords.Add((x: (byte)mPos.X, y: (byte)mPos.Y));
					break;
				case TouchEventArgs te:
					var tPos = te.GetTouchPoint(image1);
					startCoords.Add((x: (byte)tPos.Position.X, y: (byte)tPos.Position.Y));
					break;
				default:
					//for (var n = rng.Next(4) + 1; n > 0; n--)
						startCoords.Add((x: (byte)rng.Next(256), y: (byte)rng.Next(128)));
					break;
			}

			try
			{
				window1.Title += " (generating...)";

				//generate all unique colors, then shuffle them randomly
				const int uniqueColors = 0b1_00000_00000_00000;
				var randomColors = new Vector3[uniqueColors];
				for (int i = 0; i < uniqueColors; i++)
					randomColors[i] = ((short)i).Unpack();
				Shuffle(randomColors);

				//optionally sort everything
				if (startCoords.Count == 1)
					randomColors = randomColors
						.Select(c => (diff: LinearDifference(randomColors[0], c), c: c))
						.OrderBy(t => t.diff)
						.Select(t => t.c)
						.ToArray();
				else
				{
					var n = startCoords.Count;
					var buckets = new List<Vector3>[n];
					for (var i = 0; i < n; i++)
						buckets[i] = new List<Vector3>(uniqueColors / n + 1) {randomColors[i]};
					for (var i = n; i < uniqueColors; i++)
					{
						var minBucketRank = buckets.Min(l => l.Count);
						var qualifiedBuckets = buckets.Where(l => l.Count == minBucketRank).ToList();
						var selectedBucket = qualifiedBuckets.Select(b => (diff: LinearDifference(randomColors[i], b[0]), b: b)).OrderBy(t => t.diff).Select(t => t.b).First();
						selectedBucket.Add(randomColors[i]);
					}
					var maxBucketRank = buckets.Max(l => l.Count);
					var zip = new List<Vector3>();
					for (var i = 0; i < maxBucketRank; i++)
					for (var j = 0; j < n; j++)
						if (i < buckets[j].Count)
							zip.Add(buckets[j][i]);
					if (zip.Count != randomColors.Length)
						throw new InvalidOperationException("Looks like you screwed up the zipping");

					randomColors = zip.ToArray();
				}

				//next we put the pixels one by one where they fit best (by cortesian coordinates in the color space)
				var result = new short[128][];
				var resultVec = new Vector3[128][];
				for (var y = 0; y < 128; y++)
				{
					result[y] = new short[256];
					resultVec[y] = new Vector3[256];
					for (var x = 0; x < 256; x++)
					{
						result[y][x] = 0b11111_11111_11111;
						resultVec[y][x] = Black;
}
				}
				var filled = new HashSet<(byte x, byte y)>();
				var front = new HashSet<(byte x, byte y)>();

				void PutPixel((byte x, byte y) coord, Vector3 color)
				{
					var (x, y) = coord;
					result[y][x] = color.Pack();
					resultVec[y][x] = color;
					filled.Add(coord);
					front.Remove(coord);
					if (x > 0)
					{
						var newCoord = ((byte)(x - 1), y);
						if (!filled.Contains(newCoord))
							front.Add(newCoord);
					}
					if (x < 255)
					{
						var newCoord = ((byte)(x + 1), y);
						if (!filled.Contains(newCoord))
							front.Add(newCoord);
					}
					if (y > 0)
					{
						var newCoord = (x, (byte)(y - 1));
						if (!filled.Contains(newCoord))
							front.Add(newCoord);
					}
					if (y < 127)
					{
						var newCoord = (x, (byte)(y + 1));
						if (!filled.Contains(newCoord))
							front.Add(newCoord);
					}
				}
/*

				//this is fast and works fine
				(byte x, byte y) FindBestFitness(short color)
				{
					if (front.Count == 0)
						throw new InvalidOperationException("Front is empty");

					if (front.Count == 1)
						return front.First();

					var distList = new List<(float fitness, byte x, byte y)>(front.Count * 4);

					void CheckFitness((byte x, byte y) checkCoord, (byte x, byte y) frontCoord)
					{
						if (filled.Contains(checkCoord))
							distList.Add((LinearDifference(result[checkCoord.y][checkCoord.x], color), frontCoord.x, frontCoord.y));
					}

					foreach (var coord in front)
					{
						var (x, y) = coord;
						if (x > 0)
								CheckFitness((x: (byte)(x - 1), y: y), coord);
							if (x < 255)
								CheckFitness((x: (byte)(x + 1), y: y), coord);
							if (y > 0)
								CheckFitness((x: x, y: (byte)(y - 1)), coord);
							if (y < 127)
								CheckFitness((x: x, y: (byte)(y + 1)), coord);
					}
					var min = distList.OrderBy(f => f.fitness).First();
					return (x: min.x, y: min.y);
				}
*/

				//this is slower and gives much better results
				(byte x, byte y) FindBestFitnessWeighted(Vector3 color)
				{
					if (front.Count == 0)
						throw new InvalidOperationException("Front is empty");

					if (front.Count == 1)
						return front.First();

					var distList = new List<(float fitness, byte x, byte y)>(front.Count);

					(int n, float dist) GetFitness((byte x, byte y) checkCoord, (int n, float dist) stat)
					{
						if (filled.Contains(checkCoord))
							return (n: stat.n + 1, dist: Math.Min(stat.dist, LinearDifference(resultVec[checkCoord.y][checkCoord.x], color)));
						return stat;
					}

					foreach (var coord in front)
					{
						var (x, y) = coord;
						var stat = (n: 0, dist: float.PositiveInfinity);
						if (x > 0)
							stat = GetFitness((x: (byte)(x - 1), y: y), stat);
						if (x < 255)
							stat = GetFitness((x: (byte)(x + 1), y: y), stat);
						if (y > 0)
							stat = GetFitness((x: x, y: (byte)(y - 1)), stat);
						if (y < 127)
							stat = GetFitness((x: x, y: (byte)(y + 1)), stat);
						if (stat.n == 0)
							throw new InvalidOperationException("Front is not connected");

						distList.Add((fitness: stat.dist / coeffs[stat.n], x, y));
					}
					var min = distList.OrderBy(f => f.fitness).First();
					return (x: min.x, y: min.y);
				}

				//create the bitmap with results
				var bitmap = new WriteableBitmap(256, 128, 96, 96, PixelFormats.Bgr555, null);
				image1.Source = bitmap;

				void Update()
				{
					bitmap.Lock();
					unsafe
					{
						for (var y = 0; y < 128; y++)
							fixed (void* pRow = &result[y][0])
							{
								var backBufferRow = (byte*)bitmap.BackBuffer + y * bitmap.BackBufferStride;
								Buffer.MemoryCopy(pRow, backBufferRow, (ulong)bitmap.BackBufferStride, (ulong)(result[y].Length * sizeof(short)));
							}
					}
					bitmap.AddDirtyRect(new Int32Rect(0, 0, 256, 128));
					bitmap.Unlock();
				}

				//put the first pixel in the center, then fit everything else accordingly
				var timer = new Stopwatch();
				timer.Start();
				for (var i = 0; i < startCoords.Count; i++)
					PutPixel(startCoords[i], randomColors[i]);
				Update();
				DoEvents();

				for (var idx = startCoords.Count; idx < randomColors.Length; idx++)
				{
					//PutPixel(FindBestFitness(randomColors[idx]), randomColors[idx]);
					PutPixel(FindBestFitnessWeighted(randomColors[idx]), randomColors[idx]);
					if (/*idx % 1000 == 0 &&*/ timer.Elapsed.TotalMilliseconds >= 1000.0/60.0)
					{
						Update();
						window1.Title = $"{title} ({100.0 * idx / randomColors.Length:0.00}%)";
						DoEvents();
						if (cancel)
							break;
						timer.Restart();
					}
				}
				timer.Reset();

				Update();
			}
			finally
			{
				window1.Title = title;
				syncObj.Release();
			}
		}

		[SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
		public void DoEvents()
		{
			DispatcherFrame frame = new DispatcherFrame();
			Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
				new DispatcherOperationCallback(ExitFrame), frame);
			Dispatcher.PushFrame(frame);
		}

		public object ExitFrame(object f)
		{
			((DispatcherFrame)f).Continue = false;

			return null;
		}

		private static void Shuffle<T>(T[] array)
		{
			var rng = new Random();
			for (var i = array.Length - 1; i > -1; i--)
			{
				var idx = rng.Next(i + 1);
				if (idx == i)
					continue;

				var tmp = array[i];
				array[i] = array[idx];
				array[idx] = tmp;
			}
		}

		private static float LinearDifference(Vector3 vec1, Vector3 vec2)
		{
			return Vector3.Distance(vec1, vec2);
		}

		private static readonly SemaphoreSlim syncObj = new SemaphoreSlim(1, 1);
		private static volatile bool cancel;
		private static readonly float[] coeffs = {float.NaN, 1.0f, (float)Math.Sqrt(2), (float)Math.Sqrt(3), 2.0f};
		private static readonly Random rng = new Random();
		private static readonly Vector3 Black = Vector3Ex.Unpack(0b0_11111_11111_11111);

		private void window1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			cancel = true;
		}
	}
}
