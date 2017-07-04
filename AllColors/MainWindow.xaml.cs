using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Permissions;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
		}

		private void image1_Generate(object sender, InputEventArgs e)
		{
			cancel = true;
			if (!syncObj.Wait(0))
				return;

			var title = window1.Title;
			cancel = false;
			(byte x, byte y) startCoord;
			switch (e)
			{
				case MouseEventArgs me:
					var mPos = me.GetPosition(image1);
					startCoord = (x: (byte)mPos.X, y: (byte)mPos.Y);
					break;
				case TouchEventArgs te:
					var tPos = te.GetTouchPoint(image1);
					startCoord = (x: (byte)tPos.Position.X, y: (byte)tPos.Position.Y);
					break;
				default:
					startCoord = (x: 128, y: 64);
					break;
			}

			try
			{
				window1.Title += " (generating...)";

				//generate all unique colors, then shuffle them randomly
				const int uniqueColors = 0b1_00000_00000_00000;
				var randomColors = new short[uniqueColors];
				for (int i = 0; i < uniqueColors; i++)
					randomColors[i] = (short)i;
				Shuffle(randomColors);

				//optionally sort everything
				randomColors = randomColors.Select(c => (diff: LinearDifference(randomColors[0], c), c: c)).OrderBy(t => t.diff).Select(t => t.c).ToArray();

				//next we put the pixels one by one where they fit best (by cortesian coordinates in the color space)
				var result = new short[128][];
				for (var y = 0; y < 128; y++)
				{
					result[y] = new short[256];
					for (var x = 0; x < 256; x++)
						result[y][x] = 0b0_11111_11111_11111;
				}
				var filled = new HashSet<(byte x, byte y)>();
				var front = new HashSet<(byte x, byte y)>();

				void PutPixel((byte x, byte y) coord, short color)
				{
					var (x, y) = coord;
					result[y][x] = color;
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

				//this is fast and works fine
				(byte x, byte y) FindBestFitness(short color)
				{
					if (front.Count == 0)
						throw new InvalidOperationException("Front is empty");

					if (front.Count == 1)
						return front.First();

					var distList = new List<(double fitness, byte x, byte y)>(front.Count * 4);

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

				//this is slower and gives much better results
				(byte x, byte y) FindBestFitnessWeighted(short color)
				{
					if (front.Count == 0)
						throw new InvalidOperationException("Front is empty");

					if (front.Count == 1)
						return front.First();

					var distList = new List<(double fitness, byte x, byte y)>(front.Count);

					(int n, double dist) GetFitness((byte x, byte y) checkCoord, (int n, double dist) stat)
					{
						if (filled.Contains(checkCoord))
							return (n: stat.n + 1, dist: Math.Min(stat.dist, LinearDifference(result[checkCoord.y][checkCoord.x], color)));
						return stat;
					}

					foreach (var coord in front)
					{
						var (x, y) = coord;
						var stat = (n: 0, dist: double.PositiveInfinity);
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

						distList.Add((fitness: stat.dist / stat.n, x, y));
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
				PutPixel(startCoord, randomColors[0]);
				for (var idx = 1; idx < randomColors.Length; idx++)
				{
					//PutPixel(FindBestFitness(randomColors[idx]), randomColors[idx]);
					PutPixel(FindBestFitnessWeighted(randomColors[idx]), randomColors[idx]);
					if (/*idx % 1000 == 0 &&*/ timer.Elapsed.TotalMilliseconds > 16.66)
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

		private static readonly SemaphoreSlim syncObj = new SemaphoreSlim(1, 1);
		private static volatile bool cancel = false;

		private void window1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			cancel = true;
		}
	}
}
