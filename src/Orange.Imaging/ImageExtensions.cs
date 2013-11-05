using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Orange.Imaging
{
    public static class ImageExtensions
    {
        /// <summary>
        /// Default value for <see cref="ResizePhoto"/> 640px
        /// </summary>
        public const int LONGEST_PHOTO_SIDE = 600;
        
        /// <summary>
        /// Used with ToThumbnail method
        /// </summary>
        const int LONGEST_THUMB_SIDE = 320;

        public static Image ToThumbnail(this Image image)
		{
			return ToThumbnail(image, LONGEST_THUMB_SIDE);
		}

		public static Image ToThumbnail(this Image image, int longestSide)
		{
            //If the dpi and width/height are below largest then we don't need to resize the thumbnail
            if (image.HorizontalResolution <= 96 && image.VerticalResolution <= 96 && image.Width <= longestSide && image.Height <= longestSide)
                return image;

            return ResizePhoto(image, longestSide);
		}

        /// <summary>
        /// Compares the byte[] of 2 images using SequenceEquals
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool IsSameAs(this Image a, Image b)
        {
            return a.Height == b.Height && a.Width == b.Width &&
                ImageToBytes(a).SequenceEqual(ImageToBytes(b));
        }

        /// <summary>
        /// Used to determine if one image "looks" like another by using underlying <see cref="GetFingerprint"/>
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static bool IsSimilarTo(this Image a, Image b)
        {
            return GetFingerprint(a) == GetFingerprint(b);
        }

		/// <summary>
		/// Alternative to Clone() method.  Handles Indexed pixel format. 
        /// Creates a full copy of the image- useful when ensuring that an underlying FileStream/File has been released by GDI+ as GDI+ tends to hold onto handles potentially locking files and preventing sharing.
		/// </summary>
        /// <remarks>Does NOT dispose original image</remarks>
		/// <param name="image"></param>
		/// <returns></returns>
		public static Image Copy(this Image image)
		{
			Bitmap dest = null;
			if((image.PixelFormat & PixelFormat.Indexed) == PixelFormat.Indexed)
			{
				dest = new Bitmap(image.Width, image.Height);
                using (Graphics g = Graphics.FromImage(dest))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                    //use the destination graphics object to draw the source
                    g.DrawImage(image, 0, 0, dest.Width, dest.Height);

                    return dest;
                }
			}
			else
			{
                return image.Clone() as Image;
			}
		}

        /// <summary>
        /// Removes all PropertyItems (EXIF data)
        /// </summary>
        /// <returns></returns>
		public static void ClearExifData(this Image img, List<EXIFTags> tagsToKeep = null)
		{
            img.PropertyItems.Where(pi => (tagsToKeep == null || !tagsToKeep.Contains((EXIFTags)pi.Id))).ToList()
                .ForEach(pi => img.RemovePropertyItem(pi.Id));
		}

        /// <summary>
        /// Sets all property items of "source" on "dest"
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        public static void CopyEXIFData(this Image source, Image dest)
        {
            foreach (PropertyItem prop in source.PropertyItems)
            {
                dest.SetPropertyItem(prop);
            }
        }

        /// <summary>
        /// Returns a "Perceptual hash" value used for visual similarity comparsion.  Used by <see cref="IsSimilarTo"/>.
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
		public static long GetFingerprint(Image image)
		{
			Bitmap squeezedImage = new Bitmap(8, 8, PixelFormat.Format32bppRgb);
			Graphics drawingArea = Graphics.FromImage(squeezedImage);
			drawingArea.CompositingQuality = CompositingQuality.HighQuality;
			drawingArea.InterpolationMode = InterpolationMode.HighQualityBilinear;
			drawingArea.SmoothingMode = SmoothingMode.HighQuality;
			drawingArea.DrawImage(image, 0, 0, 8, 8);
			byte[] grayScaleImage = new byte[64];

			uint averageValue = 0;
			long finalHash = 0;

			for (int y = 0; y < 8; y++)
			{
				for (int x = 0; x < 8; x++)
				{
					uint pixelColour = (uint)squeezedImage.GetPixel(x, y).ToArgb();
					uint grayTone = (pixelColour & 0x00FF0000) >> 16;
					grayTone += (pixelColour & 0x0000FF00) >> 8;
					grayTone += (pixelColour & 0x000000FF);
					grayTone /= 12;

					grayScaleImage[x + y * 8] = (byte)grayTone;
					averageValue += grayTone;
				}
			}

			averageValue /= 64;

			for (int k = 0; k < 64; k++)
			{
				if (grayScaleImage[k] >= averageValue)
				{
					finalHash |= (1L << (63 - k));
				}
			}

			return finalHash;
		}

		/// <summary>
		/// Only supports JPEG header.  Reads image size WITHOUT loading GDI object. Useful when performance is a concern.
		/// </summary>
		/// <param name="filename"></param>
		/// <returns></returns>
		public static Size GetImageDimensionsForJpeg(string filename)
		{
        	FileStream stream = null;
			BinaryReader rdr = null;
			try
			{
				stream = File.OpenRead(filename);
				rdr = new BinaryReader(stream);
				// keep reading packets until we find one that contains Size info
				for(; ; )
				{
					byte code = rdr.ReadByte();
					if(code != 0xFF) throw new ApplicationException(
									  string.Format("Unexpected value in file {0}.  Ensure file is JPEG format.", filename));
					code = rdr.ReadByte();
					switch(code)
					{
						// filler byte
						case 0xFF:
							stream.Position--;
							break;
						// packets without data
						case 0xD0:
						case 0xD1:
						case 0xD2:
						case 0xD3:
						case 0xD4:
						case 0xD5:
						case 0xD6:
						case 0xD7:
						case 0xD8:
						case 0xD9:
							break;
						// packets with size information
						case 0xC0:
						case 0xC1:
						case 0xC2:
						case 0xC3:
						case 0xC4:
						case 0xC5:
						case 0xC6:
						case 0xC7:
						case 0xC8:
						case 0xC9:
						case 0xCA:
						case 0xCB:
						case 0xCC:
						case 0xCD:
						case 0xCE:
						case 0xCF:
							ReadBEUshort(rdr);
							rdr.ReadByte();
							ushort h = ReadBEUshort(rdr);
							ushort w = ReadBEUshort(rdr);
							return new Size(w, h);
						// irrelevant variable-length packets
						default:
							int len = ReadBEUshort(rdr);
							stream.Position += len - 2;
							break;
					}
				}
			}
			finally
			{
				if(rdr != null) rdr.Close();
				if(stream != null) stream.Close();
			}
		}

		private static ushort ReadBEUshort(BinaryReader rdr)
		{
			ushort hi = rdr.ReadByte();
			hi <<= 8;
			ushort lo = rdr.ReadByte();
			return (ushort)(hi | lo);
		}

		public static Rectangle GetScaledDimensions(int width, int height, int longestSide)
		{
			float scale = 0f;

			if(width > height)
			{
				scale = (float)longestSide / (float)width;
				width = longestSide;
				height = (int)((float)height * scale);
			}
			else
			{
				scale = (float)longestSide / (float)height;
				height = longestSide;
				width = (int)((float)width * scale);
			}
			if(height <= 0)
				height = 1;
			if(width <= 0)
				width = 1;
			
			return new Rectangle(0, 0, width, height);
		}

        public static bool IsDisposed(this Image image)
        {
            System.Reflection.FieldInfo nativeImageIntPtr = image.GetType().GetField("nativeImage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (nativeImageIntPtr != null)
            {
                IntPtr ptr = (IntPtr)nativeImageIntPtr.GetValue(image);
                if (ptr == IntPtr.Zero)
                    return true;
            }
            return false;
        }

		public static Image ScaleImage(this Image source, int longestSide)
		{
			if(source.Width < longestSide || source.Height < longestSide
				|| source.PixelFormat != PixelFormat.Format24bppRgb)
			{
				Rectangle scaledDimensions = GetScaledDimensions(source.Width, source.Height, longestSide);
				if(source.PixelFormat == PixelFormat.Format24bppRgb && scaledDimensions.Size == source.PhysicalDimension)
					return source;

				System.Drawing.Image newPhoto = new Bitmap(scaledDimensions.Width, scaledDimensions.Height, PixelFormat.Format24bppRgb);
				using(Graphics g2 = Graphics.FromImage(newPhoto))
				{
					g2.DrawImage(source, scaledDimensions);

                    newPhoto.RemoveResolution();
                    source.CopyEXIFData(newPhoto);

					IDisposable original = source;
					source = newPhoto;

					original.Dispose();
				}
			}

			return source;
		}

        /// <summary>
        /// Remove verticle & horizontal resolution since they are no longer valid after resize
        /// and leaving these values can lead to large strings being drawon on high DPI images when calling Graphics.DrawString()
        /// </summary>
        /// <param name="source"></param>
        private static void RemoveResolution(this Image source)
        {
            foreach (PropertyItem prop in source.PropertyItems)
            {
                if (prop.Id == (int)EXIFTags.VerticalResolution || prop.Id == (int)EXIFTags.HorizontalResolution)
                    source.RemovePropertyItem(prop.Id);
            }
        }

        #region DPI
        [DllImport("gdi32.dll", EntryPoint = "CreateDC", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern IntPtr CreateDC(string lpszDriver, string lpszDeviceName, string lpszOutput, IntPtr devMode);

		[DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
		static extern bool DeleteDC(IntPtr hdc);

		[DllImport("gdi32.dll", SetLastError = true)]
		private static extern Int32 GetDeviceCaps(IntPtr hdc, Int32 capindex);
		private const int LOGPIXELSX = 88;

		private static int dpi = -1;

        /// <summary>
        /// Returns current screen DPI- used to convert Pixesl<->points
        /// </summary>
		private static int DPI
		{
			get
			{
				if(dpi != -1)
					return dpi;

				dpi = 96;
				IntPtr hdc = IntPtr.Zero;
				try
				{
					hdc = CreateDC("DISPLAY", null, null, IntPtr.Zero);
					if(hdc != IntPtr.Zero)
					{
						dpi = GetDeviceCaps(hdc, LOGPIXELSX);
						if(dpi == 0)
							dpi = 96;
					}
				}
				catch(Exception)
				{
				}
				finally
				{
					try
					{
						if(hdc != IntPtr.Zero)
							DeleteDC(hdc);
					}
					catch(Exception)
					{
					}
				}

				return dpi;
			}
		}
        #endregion

        public static float PointsToPixels(float fontSizeInPoints)
		{
			//formula=
			//							size in points
			//	font size in pixels =	-------------------	x DPI
			//							72 points per inch
			//More info: http://msdn.microsoft.com/en-us/library/xwf9s90b.aspx

			return ((float)fontSizeInPoints / (float)72) * (float)DPI;
		}

		public static float PixelsToPoints(float pixelSize)
		{
			return ((float)pixelSize * (float)72) / DPI;
		}

		/// <summary>
		/// Resizes a photo so that the longest photo side is &lt;= LARGEST_PHOTO_SIDE
		/// </summary>
		/// <param name="srcImage"></param>
		/// <returns></returns>
		public static Image ResizePhoto(this Image srcImage, int longestPhotoSide = LONGEST_PHOTO_SIDE)
		{
			if(srcImage.Width > longestPhotoSide || srcImage.Height > longestPhotoSide)
			{
                Rectangle resized = GetScaledDimensions(srcImage.Width, srcImage.Height, longestPhotoSide);

				System.Drawing.Imaging.PixelFormat format = srcImage.PixelFormat;

				//Cannot create a graphics object for an "Indexed" pixel format
				//so if the PixelFormat enum contains "Indexed" change the format for the new bitmap
				if(Enum.GetName(typeof(System.Drawing.Imaging.PixelFormat), srcImage.PixelFormat).Contains("Indexed"))
					format = System.Drawing.Imaging.PixelFormat.Format24bppRgb;

				Bitmap dest = new Bitmap(resized.Width, resized.Height, format);
                dest.SetResolution(96, 96);

				using(srcImage)
				{
					using(Graphics g = Graphics.FromImage(dest))
					{
						g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
						g.CompositingQuality = CompositingQuality.HighSpeed;

						g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
						g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;

						g.DrawImage(srcImage, 0, 0, dest.Width, dest.Height);

                        dest.RemoveResolution();
                        srcImage.CopyEXIFData(dest);

						return dest;
					}
				}
			}
			else
				return srcImage;
		}

		public static long GetImageSize(Image image)
		{
			using(MemoryStream mem = new MemoryStream())
			{
				image.Save(mem, ImageFormat.Jpeg);
				mem.Close();

				return mem.Length;
			}
		}

		public static MemoryStream ImageToStream(Image image)
		{
			MemoryStream ms = new MemoryStream();
			try
			{
				image.Save(ms, ImageFormat.Jpeg);
				ms.Seek(0, SeekOrigin.Begin);
			}
			catch(ArgumentException)
			{
				//Parameter is not valid errors occurred with specific OSs and weird exif data on the images. The fix below will create a new image object with cleared exif 
				//data to address this if there is a problem with a normal save.
				image.ClearExifData();
				ms.Seek(0, SeekOrigin.Begin);
				image.Save(ms, ImageFormat.Jpeg);
			}

			return ms;

			//when the image object writes to a stream it leaves the current position where it stopped writing
			//we have to rewind the stream so we can start reading from the begining and not the end
			//ms.Seek(0, SeekOrigin.Begin);

			//using(BinaryReader reader = new BinaryReader(ms))
			//{
			//    return reader.ReadBytes((int)reader.BaseStream.Length);
			//}
			
		}

		public static byte[] ImageToBytes(Image image)
		{
			using(MemoryStream ms = ImageToStream(image))
			{
				return ms.ToArray();
			}
		}
		public static Image BytesToImage(byte[] bytes)
		{
			if(bytes == null || bytes.Length == 0)
				throw new ArgumentException("Image data was null.", "bytes");

			//must keep memory stream open for the life of the image otherwise this results in "generic error in gdi+" errors
			return Image.FromStream(new MemoryStream(bytes), false, false);
		}
		public static void SaveFile(string path, Image image)
		{
			SaveFile(path, ImageToBytes(image));
		}
		public static void SaveFile(string path, byte[] fileData)
		{
			try
			{
				string directory = Path.GetDirectoryName(path);
				if(!Directory.Exists(directory))
					Directory.CreateDirectory(directory);

				using(BinaryWriter writer = new BinaryWriter(new FileStream(path, System.IO.FileMode.Create)))
				{
					writer.Write(fileData);
					writer.Flush();
					writer.Close();
				}
			}
			catch(Exception ex)
			{
				throw new Exception("Error saving file \"" + path + "\".", ex);
			}
		}

		public static void CopyStream(BinaryReader br, System.IO.Stream to)
		{
			int size = 0;
			byte[] data = new byte[4096];
			do
			{
				size = br.Read(data, 0, data.Length);
				to.Write(data, 0, size);
			} while(size > 0);
		}

		public static Image ReadImage(string path)
		{
			long temp = 0;
			return ReadImage(path, out temp);
		}

		public static Image ReadImage(string path, out long sizeOnDisk)
		{
			try
			{
				try
				{
					if(!File.Exists(path))
						throw new FileNotFoundException("File not found!", path);
					sizeOnDisk = new FileInfo(path).Length;
					using(FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					{
						//gdi+ tends not to release images for faster rendering (keeps them cached)
						//copyign this image to a totally new image object in memory helps to prevent
						//gdi+ errors and file access errors
						using(Image image = Image.FromStream(fs, false, false))
						{
							return Copy(image);
						}
					}
				}
				catch(ArgumentException)
				{
					using(BinaryReader br = new BinaryReader(File.OpenRead(path)))
					{
						sizeOnDisk = br.BaseStream.Length;
						try
						{
							//supposed to help prevent "paramter invalid" exception when calling Image.FromStream()
							using(MemoryStream ms = new MemoryStream())
							{
								CopyStream(br, ms);
								ms.Seek(0, SeekOrigin.Begin);
								Image image = Image.FromStream(ms, false, false);

								ms.Close();
								br.Close();

								return image;
							}
						}
						catch(ArgumentException aex2) //paramter not valid exception
						{
							br.BaseStream.Seek(0, SeekOrigin.Begin);

							byte[] bytes = new byte[br.BaseStream.Length];
							bytes = br.ReadBytes((int)br.BaseStream.Length);

							System.Diagnostics.EventLog.WriteEntry("ExceptionManagerPublishedException",
								new Exception("Error reading photo: " + path + ".", aex2).ToString(), System.Diagnostics.EventLogEntryType.Error);

							return SystemIcons.Error.ToBitmap();

							//try
							//{

							//    //Causes "attempted to read or write protected memory" under .net 4.0
							//    //using(MemoryStream ms = new MemoryStream(bytes))
							//    //{
							//    //    Image image = Image.FromStream(ms, false, false);
							//    //    ms.Close();
							//    //    br.Close();

							//    //    return image;
							//    //}
							//}
							//catch(ArgumentException aex3)
							//{
							//    //keep source the same as app block incase user doesn't 
							//    //have permission to create an event source

							//}
						}
						br.Close();
					}
				}
			}
			catch(Exception ex)
			{
				throw new Exception("Error reading image: " + path, ex);
			}
		}
		public static byte[] ReadImageToBytes(string path)
		{
			return ImageToBytes(ReadImage(path));
		}
		public static byte[] ReadImageToBytes(string path, out long sizeOnDisk)
		{
			return ImageToBytes(ReadImage(path, out sizeOnDisk));
		}
    }

    public enum EXIFTags
    {
        #region Exif Tag IDs
        /// <summary>
        /// Represents the Exif tag for thumbnail data.
        /// </summary>
        ThumbnailData = 0x501B,
        /// <summary>
        /// Represents the Exif tag for thumbnail image width.
        /// </summary>
        ThumbnailImageWidth = 0x5020,
        /// <summary>
        /// Represents the Exif tag for thumbnail image height.
        /// </summary>
        ThumbnailImageHeight = 0x5021,
        /// <summary>
        /// Represents the Exif tag for  inage description.
        /// </summary>
        ImageDescription = 0x010E,
        /// <summary>
        /// Represents the Exif tag for the equipment model.
        /// </summary>
        EquipmentModel = 0x0110,
        EquipmentManufacturer = 0x10F,

        /// <summary>
        /// This is the modified date.  It may represent the Exif tag for date and time the picture 
        /// was taken, but it's not guaranteed.  Use OriginalDateTime for OriginalDateTime / DateTaken
        /// </summary>        
        DateTimeModified = 0x0132,
        /// <summary>
        /// Original date of photo.  Also "Date Taken" in Windows Explorer.
        /// </summary>
        OriginalDateTime = 0x9003,
        /// <summary>
        /// Represents the Exif tag for the artist.
        /// </summary>
        Artist = 0x013B,
        /// <summary>
        /// Represents the Exif tag for copyright information.
        /// </summary>
        Copyright = 0x8298,
        /// <summary>
        /// Represents the Exif tag for exposure time.
        /// </summary>
        ExposureTime = 0x829A,
        /// <summary>
        /// Represents the Exif tag for F-Number.
        /// </summary>
        FNumber = 0x829D,
        /// <summary>
        /// Represents the Exif tag for ISO speed.
        /// </summary>
        ISOSpeed = 0x8827,
        /// <summary>
        /// Represents the Exif tag for shutter speed.
        /// </summary>
        ShutterSpeed = 0x9201,
        /// <summary>
        /// Represents the Exif tag for aperture value.
        /// </summary>
        Aperture = 0x9202,
        /// <summary>
        /// Represents the Exif tag for user comments.
        /// </summary>
        UserComment = 0x9286,

        ItemSize = 0x1014,

        ItemName = 0x1002,

        /// <summary>
        /// Represents horizontal resolution of image.  
        /// GDI uses this when drawing object onto an image and if the resolution is out of sync with the image size, 
        /// it can result in improperly scaled drawings.
        /// </summary>
        HorizontalResolution = 0x011a,

        /// <summary>
        /// Represents Vertical resolution of image.  
        /// GDI uses this when drawing object onto an image and if the resolution is out of sync with the image size, 
        /// it can result in improperly scaled drawings.
        /// </summary>
        VerticalResolution = 0x011b

        #endregion
    }
}
