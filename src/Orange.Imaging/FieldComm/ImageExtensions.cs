using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orange.Imaging;

namespace Orange.Imaging.FieldComm
{
    public static class ImageExtensions
    {
        /// <summary>
        /// After a photo is resized, it will be scaled up to this value so that photo sizes are all consistent in FC and sort order #'s or photo dates or other
        /// objects drawn on the photo will be the same size
        /// </summary>
        public const int SCALE_LONGEST_SIDE = 640; //rather than max, make same as caption header so date is always scaled the same //maxium that the photos can be resized to in setup
        const int TEXT_RECTANGLE_HEIGHT = 95;

        public class ImageCaption
        {
            /*
             * Image original, string loanNumber, string loanType, string workOrderNumber,
			string bank, string addressDisplay, string orderNumber, string caption, DateTime date, bool includeTime, 
			bool drawCameraDate = false, bool fullSizedCaption = false)*/

            public string LoanNumber { get; set; }
            public string LoanType { get; set; }
            public string WorkOrderNumber { get; set; }
            public string Bank { get; set; }
            public string AddressDisplay { get; set; }

            /// <summary>
            /// Unique Order#, not sort order
            /// </summary>
            public string OrderNumber { get; set; }

            /// <summary>
            /// Use to draw optional camera date on image
            /// </summary>
            public DateTime Date { get; set; }

            public bool IncludeCameraDate { get; set; }

            /// <summary>
            /// When <see cref="IncludeCameraDate"/> is true, includes time in date
            /// </summary>
            public bool IncludeTime { get; set; }

            /// <summary>
            /// True to keep picture 600px wide (full width of caption header), false to shink photo to original dimensions to save memory
            /// </summary>
            public bool FullSizedCaption { get; set; }

            /// <summary>
            /// The text caption of the photo
            /// </summary>
            public string Caption { get; set; }
        }

        #region Properties
        //not all systems will have the Arial Narrow font...
        private static Font cameraFont = null;

        public static Font CameraDateFont
        {
            get
            {
                if (cameraFont == null)
                    cameraFont = new Font(new FontFamily(System.Drawing.Text.GenericFontFamilies.SansSerif), 16, FontStyle.Bold);

                return cameraFont;
            }
            set { cameraFont = value; }
        }

        private static Color headerLightColor;

        public static Color HeaderLightColor
        {
            get
            {
                if (headerLightColor == Color.Empty)
                    headerLightColor = Color.FromArgb(117, 166, 241);

                return headerLightColor;
            }
            set { headerLightColor = value; }
        }

        private static Color headerDarkColor;

        public static Color HeaderDarkColor
        {
            get
            {
                if (headerDarkColor == Color.Empty)
                    headerDarkColor = Color.FromArgb(59, 97, 156);

                return headerDarkColor;
            }
            set { headerDarkColor = value; }
        }

        private static Color groupBarHighLightLightColor;

        public static Color GroupBarHighLightLightColor
        {
            get
            {
                if (groupBarHighLightLightColor == Color.Empty)
                    groupBarHighLightLightColor = Color.FromArgb(255, 255, 220);

                return groupBarHighLightLightColor;
            }
            set { groupBarHighLightLightColor = value; }
        }

        private static Color groupBarHighlightDarkColor;

        public static Color GroupBarHighlightDarkColor
        {
            get
            {
                if (groupBarHighlightDarkColor == Color.Empty)
                    groupBarHighlightDarkColor = Color.FromArgb(247, 192, 91);

                return groupBarHighlightDarkColor;
            }
            set { groupBarHighlightDarkColor = value; }
        }

        #endregion

        /// <summary>
        /// Draws the camera date onto the picture
        /// NOTE: In order to prevent large fonts getting written to the image, the image is scaled up to 
        /// SCALE_LONGEST_SIDE and the date is drawn, then resized back down to original size
        /// </summary>
        /// <param name="original">Image will be disposed. Caller must catch return value for updated image</param>
        /// <param name="date"></param>
        /// <param name="showTime"></param>
        /// <returns></returns>
        public static Image DrawCameraDate(this Image original, DateTime date, bool showTime = false)
        {
            //Get the longest original size
            int longestSide = original.Height;
            if (original.Width > original.Height)
            {
                longestSide = original.Width;
            }
            //Scale image to longest side to draw text on it
            original = original.ScaleImage(SCALE_LONGEST_SIDE);

            using (Graphics g = Graphics.FromImage(original))
            {
                DrawCameraDateInternal(g, date, showTime, original.Size);

                IDisposable old = original;
                //And shrink it back down to original size
                original = original.ResizePhoto(longestSide);

                return original;
            }
        }
        private static void DrawCameraDateInternal(Graphics g, DateTime date, bool showTime, Size imageSize)
        {
            string dateString = date.ToShortDateString();
            if (showTime)
                dateString = string.Format("{0} {1}", dateString, date.ToShortTimeString());

            Font dateFont = CameraDateFont;
            //Font dateFont = new Font("Arial Narrow", 36);
            SizeF dateStringSize = g.MeasureString(dateString, dateFont);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            g.DrawString(dateString, dateFont,
                Brushes.Yellow, new PointF(imageSize.Width - dateStringSize.Width - 25, imageSize.Height - dateStringSize.Height - 25),
                StringFormat.GenericTypographic);
        }

        /// <summary>
        /// Draws caption header
        /// </summary>
        /// <param name="original"></param>
        /// <param name="loanNumber"></param>
        /// <param name="loanType"></param>
        /// <param name="workOrderNumber"></param>
        /// <param name="bank"></param>
        /// <param name="addressDisplay"></param>
        /// <param name="orderNumber"></param>
        /// <param name="caption"></param>
        /// <param name="date"></param>
        /// <param name="includeTime"></param>
        /// <param name="drawCameraDate">Pass true to draw the camera date at the same time saving resources (image & graphics objects)</param>
        /// <param name="fullSizedCaption">True to keep picture 600px wide (full width of caption header), false to shink photo to original dimensions to save memory</param>
        /// <returns></returns>
        public static Image DrawCaptionHeader(this Image original, ImageCaption caption)
        {
            string photoDetails = string.Format(
               "Property ID: {0}    Loan Type: {1}    W/O #: {2}    Bank: {3}{4}" +
               "Address: {5}{4}" +
               "Date: {6}    Field-Comm.net Order #: {7}{4}",
               caption.LoanNumber, caption.LoanType, caption.WorkOrderNumber, caption.Bank, Environment.NewLine,
               caption.AddressDisplay,
               caption.IncludeTime ? string.Format("{0} {1}", caption.Date.ToShortDateString(), caption.Date.ToShortTimeString()) : caption.Date.ToShortDateString(),
               caption.OrderNumber.ToString());

            string captionText = string.Format("Caption: {0}", caption.Caption.Replace(@"\\n", "//n").Replace(@"\n", Environment.NewLine).Replace("//n", @"\n"));
            string message = "Image generated by:";
            string fcVersion = "Field-Comm.net";

            Font fcMessageFont = new Font("Tahoma", 12, FontStyle.Bold);
            Font fcVerFont = new Font("Tahoma", 12, FontStyle.Bold | FontStyle.Italic);
            Font photoDetailsFont = new Font("Tahoma", 8, FontStyle.Bold);

            int longestSide = original.Height;
            if (original.Width > original.Height)
            {
                longestSide = original.Width;
            }

            Rectangle scaledDimensions = Orange.Imaging.ImageExtensions.GetScaledDimensions(original.Width, original.Height, longestSide);

            SizeF detailsSize;
            SizeF captionSize;
            SizeF fcSize;

            using (Graphics g = Graphics.FromImage(original))
            {
                detailsSize = g.MeasureString(photoDetails, photoDetailsFont, scaledDimensions.Width - 10, StringFormat.GenericTypographic);
                captionSize = g.MeasureString(captionText, photoDetailsFont, scaledDimensions.Width - 20, StringFormat.GenericTypographic);
                fcSize = g.MeasureString(message + " " + fcVersion, fcMessageFont, scaledDimensions.Width);
            }

            Rectangle messageRect = new Rectangle(0, 0, scaledDimensions.Width,
                detailsSize.ToSize().Height     //Height of the Details 
                + captionSize.ToSize().Height   //Height of the caption
                + 10);

            int fcMessageHeight = fcSize.Width + 15 < scaledDimensions.Width ? fcSize.ToSize().Height : 0;

            System.Drawing.Image newPhoto = new Bitmap(scaledDimensions.Width, scaledDimensions.Height + messageRect.Height + fcMessageHeight, original.PixelFormat);

            using (original)
            {
                using (Graphics g = Graphics.FromImage(newPhoto))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    //set text bacground area

                    //draw the image TEXT_RECTANGLE_HEIGHT pixels lower than the top of the new photo 
                    //(leaving rectangle to write with)
                    g.DrawImage(original, new Rectangle(0, messageRect.Height + fcMessageHeight, scaledDimensions.Width, scaledDimensions.Height));

                    //draw white background in rectanble so text is visible
                    //g.FillRectangle(new SolidBrush(Color.White), messageRect);
                    //draw blue gradient into background of rectangle:
                    g.FillRectangle(new LinearGradientBrush(messageRect, HeaderDarkColor, HeaderLightColor,
                        LinearGradientMode.ForwardDiagonal) /*new SolidBrush(Color.White)*/, messageRect);

                    float fcVersionWidth = g.MeasureString(fcVersion, fcVerFont).Width;
                    float fcMessageWidth = g.MeasureString(message, fcMessageFont).Width;

                    if (fcVersionWidth + fcMessageWidth + 15 < newPhoto.Width)
                    {
                        //Make orange gradient for FC info
                        g.FillRectangle(new LinearGradientBrush(new Point(0, messageRect.Height),
                            new Point(newPhoto.Width, messageRect.Height + 20), GroupBarHighLightLightColor,
                            GroupBarHighlightDarkColor) /*new SolidBrush(Color.White)*/,
                            0, messageRect.Height, messageRect.Width, 20);
                        //draw version
                        g.DrawString(fcVersion, fcVerFont, new SolidBrush(Color.Black),
                            newPhoto.Width - fcVersionWidth - 5, messageRect.Height);
                        //draw message
                        g.DrawString(message, fcMessageFont, new SolidBrush(Color.Black),
                            newPhoto.Width - fcVersionWidth - fcMessageWidth - 5, messageRect.Height);
                    }
                    else
                    {
                        //Make orange gradient for FC info
                        g.FillRectangle(new LinearGradientBrush(new Point(0, messageRect.Height),
                            new Point(newPhoto.Width, messageRect.Height + 10), GroupBarHighLightLightColor,
                            GroupBarHighlightDarkColor) /*new SolidBrush(Color.White)*/,
                            0, messageRect.Height, messageRect.Width, fcSize.Height);

                        //draw message
                        g.DrawString(message + " " + fcVersion, fcMessageFont, new SolidBrush(Color.Black),
                            new RectangleF(5, messageRect.Height, newPhoto.Width, fcSize.Height));
                    }

                    //draw photo details
                    g.DrawString(photoDetails, photoDetailsFont, new SolidBrush(Color.White),
                        new RectangleF(5, 5, detailsSize.Width, detailsSize.Height), StringFormat.GenericTypographic);

                    //draw caption
                    g.DrawString(captionText, photoDetailsFont, new SolidBrush(Color.White),
                        new RectangleF(5, 5 + detailsSize.Height, captionSize.Width + 20, captionSize.Height));

                    if (caption.IncludeCameraDate)
                        DrawCameraDateInternal(g, caption.Date, caption.IncludeTime, newPhoto.Size);

                    if (caption.FullSizedCaption)
                        return newPhoto;
                    else //shrink it back down to original size
                        return newPhoto.ResizePhoto(longestSide);
                }
            }
        }

        public static void DrawSortOrderNumberNumber(Image image, int number, Rectangle bounds)
        {
            using (Graphics g = Graphics.FromImage(image))
            {
                string orderString = number.ToString();
                float fontSize = Orange.Imaging.ImageExtensions.PixelsToPoints(image.Height) / 2;

                Font font = new Font(CameraDateFont.Name, fontSize);

                try
                {
                    g.DrawString(orderString, font,
                        Brushes.DarkBlue,
                        bounds.Left,
                        bounds.Top);
                }
                catch (System.Runtime.InteropServices.ExternalException ex)
                {
                    //generic error occured in gdi+
                }

                float delta = Orange.Imaging.ImageExtensions.PointsToPixels(font.Size) / 10;

                font = new Font(font.Name, fontSize, FontStyle.Regular);
                delta /= 2;
                float x = (bounds.Left + delta);
                float y = (bounds.Y + delta);
                try
                {
                    g.DrawString(orderString, font,
                        Brushes.Yellow, x, y);
                }
                catch (System.Runtime.InteropServices.ExternalException ex)
                {
                    //generic error occured in gdi+
                }
                g.Dispose();
            }
        }

        /// <summary>
        /// If the image is less than 600 pixels wide (minimum to draw caption header), this will scale the image up to 600 pixels
        /// in width so we don't distort sort order numbers or photo date
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private static Image CheckImageSize(Image source)
        {
            return source.ScaleImage(SCALE_LONGEST_SIDE);
        }
    }
}
