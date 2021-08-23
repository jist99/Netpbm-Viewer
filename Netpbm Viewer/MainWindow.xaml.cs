using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Drawing;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing.Imaging;
using System.Diagnostics;

namespace Netpbm_Viewer
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

		public string fp = string.Empty;
		
		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dialog = new OpenFileDialog();

			dialog.InitialDirectory = "c:\\";
			dialog.Filter = "Netpbm files|*.pbm;*.pgm;*.ppm|(*.pbm)|*.pbm|(*.pgm)|*.pgm|(*.ppm)|*.ppm|All files (*.*)|*.*";
			dialog.RestoreDirectory = true;

			if(dialog.ShowDialog() == true)
			{
				fp = dialog.FileName;
				var fs = dialog.OpenFile();

				Parser parser = new Parser(fs);
				OutputBmp.Source = parser.parse();
			}
		}

		private double zoomLevel = 1;
		private const double zoomSpeed = 0.001;
		private const double minZoom = 1;

		private void OutputBmp_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			zoomLevel += e.Delta * zoomSpeed;
			var relativeMouseLocation = e.GetPosition(OutputBmp);

			if (zoomLevel >= minZoom)
			{
				if (zoomLevel > 1)
				{
					OutputBmp.RenderTransform = new ScaleTransform(zoomLevel, zoomLevel, relativeMouseLocation.X, relativeMouseLocation.Y);
				}
				else
				{
					OutputBmp.RenderTransform = new ScaleTransform(zoomLevel, zoomLevel);
				}
			}
			else zoomLevel = minZoom;

			e.Handled = true;
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			if (fp != string.Empty)
			{
				loadFile();
			}
		}


		//Helpers
		public void loadFile()
		{
			var refreshedFile = File.Open(fp, FileMode.Open);
			Parser parser = new Parser(refreshedFile);
			OutputBmp.Source = parser.parse();

			//Close the file so other apps can update it
			refreshedFile.Dispose();
		}

		//Keep the image inside the correct size
		/*private void ScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			OutputBmp.Width = Scroller.ViewportWidth;
			OutputBmp.Height = Scroller.ViewportHeight;
		}*/

	}

	public class Parser
	{
		private enum formats
		{
			P1,
			P2,
			P3,
			P4,
			P5,
			P6,
			Err
		}
		private bool bin = false;

		private BinaryReader reader;

		private Int32 width;
		private Int32 height;

		public Parser(Stream content)
		{
			reader = new BinaryReader(content);
		}

		public BitmapImage parse()
		{
			switch (determineFormat())
			{
				case formats.P1:
					parseDimentions();
					var bmp = parseP1Image();
					var bmpimg = ToBitmapImage(bmp);
					return bmpimg;

				case formats.P2:
					parseDimentions();
					var numColours = parseNumber();
					if (numColours == null) return null;

					var bmpp2 = parseP2Image(numColours.Value);
					var bmpimgp2 = ToBitmapImage(bmpp2);
					return bmpimgp2;

				case formats.P3:
					parseDimentions();
					var maxComponentColourValue = parseNumber();
					if (maxComponentColourValue == null) return null;

					var bmpp3 = parseP3Image(maxComponentColourValue.Value);
					var bmpimgp3 = ToBitmapImage(bmpp3);
					return bmpimgp3;

				case formats.Err:
					MessageBox.Show("Unrecognised format!");
					return null;

				default:
					return null;
			}
		}

		private formats determineFormat()
		{
			formats returnValue;

			string frmtStr = new string(reader.ReadChars(2));

			if (frmtStr.StartsWith("P1")) returnValue = formats.P1;
			else if (frmtStr.StartsWith("P2")) returnValue = formats.P2;
			else if (frmtStr.StartsWith("P3")) returnValue = formats.P3;
			else if (frmtStr.StartsWith("P4")) { returnValue = formats.P1; bin = true; }
			else if (frmtStr.StartsWith("P5")) { returnValue = formats.P2; bin = true; }
			else if (frmtStr.StartsWith("P6")) { returnValue = formats.P3; bin = true; }
			else returnValue = formats.Err;

			//unparsed = unparsed.Remove(0, 2).Trim();
			return returnValue;
		}

		//Base parsers!
		//OLD and SLOW version
		/*private uint? parseNumber()
		{
			string snum = string.Empty;

			foreach (char c in unparsed.Trim())
			{
				if (Char.IsWhiteSpace(c))
				{
					break;
				}
				else
				{
					snum += c;
				}
			}

			unparsed = unparsed.Remove(0, snum.Length).Trim();

			uint? ret = Convert.ToUInt32(snum);
			if (!ret.HasValue)
			{
				MessageBox.Show("Couln't parse " + snum + " as a number");
				
			}

			return ret;
		}*/

		//NEW and FAST version
		private uint? parseNumber()
		{
			string snum = string.Empty;
			uint? ret = null;

			char c = reader.ReadChar();

			while(char.IsWhiteSpace(c) && reader.BaseStream.Position != reader.BaseStream.Length) c = reader.ReadChar(); //Skip the whitespace

			while (!char.IsWhiteSpace(c) && reader.BaseStream.Position != reader.BaseStream.Length) //Read the char
			{
				snum += c;
				c = reader.ReadChar();
			}

			if (reader.BaseStream.Position == reader.BaseStream.Length) return null;

			ret = Convert.ToUInt32(snum);

			if (!ret.HasValue)
			{
				MessageBox.Show("Couln't parse " + snum + " as a number");

			}

			return ret;
		}

		private uint? parseBinaryNumber()
		{
			if (reader.BaseStream.Position == reader.BaseStream.Length) { return null; }

			var bite = reader.ReadByte();
			uint convertedBite = Convert.ToUInt32(bite);
			return convertedBite;
		}

		//Wrapper
		private uint? smartParseNumber()
		{
			//todo! decide whether to use parseNumber() or parse a byte to a uint? (may need a new function)
			//      based on the bin attribute held by the Parser class (this class)

			if (bin) return parseBinaryNumber();
			else return parseNumber();
		}

		//Other parsers
		private void parseDimentions()
		{
			width = Convert.ToInt32(parseNumber().Value);
			height = Convert.ToInt32(parseNumber().Value);
		}

		//Bitmap generators!
		private Bitmap parseP1Image()
		{
			Bitmap output = new Bitmap(width, height);

			for(int y=0; y < height; y++)
			{
				for(int x = 0; x < width; x++)
				{
					System.Drawing.Color drawCol;
					var num = smartParseNumber();

					if (num == 1)
					{
						drawCol = System.Drawing.Color.Black;
					}
					else
					{
						drawCol = System.Drawing.Color.White;
					}

					output.SetPixel(x, y, drawCol);
				}
			}

			return output;

		}

		private Bitmap parseP2Image(uint colours)
		{
			Bitmap output = new Bitmap(width, height);

			for(int y = 0; y < height; y++)
			{
				for(int x = 0; x < width; x++)
				{
					var num = smartParseNumber();
					int greyCol = 0;

					if(num.HasValue)
						greyCol = (int)((num.Value / (float)colours) * 255);

					System.Drawing.Color drawCol = System.Drawing.Color.FromArgb(greyCol, greyCol, greyCol);
					output.SetPixel(x, y, drawCol);
				}
			}

			return output;
		}

		private Bitmap parseP3Image(uint maxValue)
		{
			Bitmap output = new Bitmap(width, height);

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					int[] ints = new int[3];
					for (int r = 0; r < 3; r++)
					{
						var num = smartParseNumber();
						
						if(num.HasValue)
							ints[r] = (int)((num.Value / (float)maxValue) * 255);
					}

					System.Drawing.Color drawCol = System.Drawing.Color.FromArgb(ints[0], ints[1], ints[2]);
					output.SetPixel(x, y, drawCol);
				}
			}

			return output;
		}

		//Bitmap type converter
		//Law Man on stack overflow
		private BitmapImage ToBitmapImage(Bitmap bitmap)
		{
			using (var memory = new MemoryStream())
			{
				bitmap.Save(memory, ImageFormat.Png);
				memory.Position = 0;

				var bitmapImage = new BitmapImage();
				bitmapImage.BeginInit();
				bitmapImage.StreamSource = memory;
				bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
				bitmapImage.EndInit();
				bitmapImage.Freeze();

				return bitmapImage;
			}
		}

	}
}
