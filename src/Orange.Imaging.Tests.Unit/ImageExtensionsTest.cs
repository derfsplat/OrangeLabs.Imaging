using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Orange.Imaging;
using Orange.Imaging.Tests.Unit.Properties;
using Xunit;
using Orange.Imaging.FieldComm;

namespace Orange.Imaging.Tests.Unit
{
    public class ImageExtensionsTest
    {
        readonly DateTime TestDate = new DateTime(2012, 1, 1, 12, 34, 56);

        [Fact]
        public void ToThumbnail_ScalesProperly()
        {
            var img = Properties.Resources.large_image_with_exif_data;
            var expectedSize = GetExpectedScale(img, 320 /*default ToThumbnail size*/);

            var resized = img.ToThumbnail();

            Assert.Equal(expectedSize, resized.Size);
        }

        private Size GetExpectedScale(Image img, int longestSide)
        {
            var size = img.Size;
            float scale;
            int newWidth = 0, newHeight = 0;

            if (size.Width > size.Height)
            {
                scale = (float)longestSide / (float)size.Width;
                newWidth = longestSide;
                newHeight = (int)((float)size.Height * scale);
            }
            else
            {
                scale = (float)longestSide / (float)size.Height;
                newHeight = longestSide;
                newWidth = (int)((float)size.Width * scale);
            }
            
            if (newHeight<= 0)
                newHeight = 1;
            if (newWidth <= 0)
                newWidth = 1;

            return new Size(newWidth, newHeight);
        }

        [Fact]
        public void ToThumbnail_DoesNotScaleImagesAlreadySmallerThanRequestedSize()
        {
            var img = Properties.Resources.vandelayindustries;
            var scaledSize = GetExpectedScale(img, 320 /*default ToThumbnail size*/);
            var originalSize = img.Size;

            var resized = img.ToThumbnail();

            Assert.Equal(originalSize, resized.Size);
            Assert.NotEqual(scaledSize, resized.Size);
        }

        [Fact]
        public void IsSameAs_ReturnsTrueForSameImage()
        {
            var img1 = Properties.Resources.vandelayindustries;
            var img2 = Properties.Resources.vandelayindustries;

            //not the same object instance
            Assert.NotSame(img1, img2);
            //but still the same image data
            Assert.True(img1.IsSameAs(img2));
        }

        [Fact]
        public void IsSameAs_ReturnsFalseForDifferentImage()
        {
            var img1 = Properties.Resources.vandelayindustries;
            //img1 PixelFormat is Format24bppRgb
            //default bitmap PixelFormat is 32bppArgb
            //==> results in different image data
            var img2 = new Bitmap(img1);

            Assert.False(img1.IsSameAs(img2));
        }

        [Fact]
        public void CopyImage_CreatesExactCopyForNonIdexedImages()
        {
            var img1 = Properties.Resources.vandelayindustries;

            var @out = img1.Copy();
            img1.ClearExifData();
            @out.ClearExifData();

            //Image data won't be EXACT, hence IsSimilarTo vs. IsSameAs
            Assert.True(img1.IsSameAs(@out));
        }

        //TODO: Get sample indexed pixel format image to test alternative copy

        [Fact]
        public void ClearExifData_ClearsAllExifDataByDefault()
        {
            var img = Properties.Resources.large_image_with_exif_data;

           img.ClearExifData();

            Assert.Empty(img.PropertyItems);
        }

        [Fact]
        public void ClearExifData_LeavesSelectedExifTags()
        {
            var img = Properties.Resources.large_image_with_exif_data;

            List<EXIFTags> tags = new List<EXIFTags>();
            tags.Add(EXIFTags.OriginalDateTime);
            img.ClearExifData(tags);

            Assert.Equal(1, img.PropertyItems.Count());
            Assert.Equal((int)EXIFTags.OriginalDateTime, img.PropertyItems[0].Id);
        }

        [Fact]
        public void CopyExifData_CopiesAllPropertyItems()
        {
            var img = Properties.Resources.large_image_with_exif_data;
            var dest = img.Copy();
            dest.ClearExifData();

            img.CopyEXIFData(dest);

            Assert.True(img.PropertyItems.SequenceEqual(dest.PropertyItems, new PropertyItemCompare()));
        }
        private class PropertyItemCompare : EqualityComparer<System.Drawing.Imaging.PropertyItem>
        {
            public override bool Equals(System.Drawing.Imaging.PropertyItem x, System.Drawing.Imaging.PropertyItem y)
            {
                if (x.Id == y.Id) return true;
                else return false;
            }

            public override int GetHashCode(System.Drawing.Imaging.PropertyItem obj)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public void IsSimilarTo_ReturnsTrueForDifferentButSimilarPhotos()
        {
            List<Image> images = new List<Image>();
            images.Add(Resources.SimilarPhoto__1_);
            images.Add(Resources.SimilarPhoto__2_);
            images.Add(Resources.SimilarPhoto__3_);
            images.Add(Resources.SimilarPhoto__4_);
            images.Add(Resources.SimilarPhoto__5_);
            images.Add(Resources.SimilarPhoto__6_);
            images.Add(Resources.SimilarPhoto__7_);
            images.Add(Resources.SimilarPhoto__8_);

            //test each image against every other image- they all should be similiar
            //implicitly tests GetFingerprint
            images.ForEach(img1 => images.ForEach(img2 =>
            {
                if (img1 != img2) Assert.True(img1.IsSimilarTo(img2));
            }));
        }

        [Fact]
        public void ResizeImage_ReturnsResizedImage()
        {
            var img = Resources.large_image_with_exif_data as Image;
            img = img.ResizePhoto();

            var expected = Resources.large_image_with_exif_data_resized;
            img.ClearExifData();
            expected.ClearExifData();

            Assert.True(img.Size.Equals(expected.Size));
            Assert.True(img.IsSimilarTo(expected));
        }

        [Fact]
        public void ResizeImage_PreservesEXIFData()
        {
            var img = Resources.large_image_with_exif_data;
            var originalItems = img.PropertyItems;
            
            var @out = img.ResizePhoto();

            Assert.True(originalItems.SequenceEqual(@out.PropertyItems, new PropertyItemCompare()));
        }

        [Fact]
        public void ScaleImage_PreservesEXIFData()
        {
            var img = Resources.large_image_with_exif_data as Image;
            var originalItems = img.PropertyItems;
            
            img = img.ResizePhoto();
            var @out = img.ScaleImage(700);

            Assert.True(img.IsDisposed() || img != @out); //we didn't just get back the image we sent
            Assert.True(originalItems.SequenceEqual(@out.PropertyItems, new PropertyItemCompare()));
        }

        [Fact]
        public void DrawCamerDate_DrawsExpectedDate()
        {
            var img = Resources.large_image_with_exif_data as Image;
            img = img.ResizePhoto().DrawCameraDate(TestDate);

            var expected = Resources.large_image_with_exif_data_dateonly;
            img.ClearExifData();
            expected.ClearExifData();

            Assert.True(img.IsSimilarTo(expected));
        }

        [Fact]
        public void DrawCaptionHeader_DrawsExpectedCaptionHeader()
        {
            var img = Resources.large_image_with_exif_data as Image;
            img = img.ResizePhoto().DrawCaptionHeader(new Orange.Imaging.FieldComm.ImageExtensions.ImageCaption()
            {
                LoanNumber = "loannumber",
                LoanType = "loantype",
                WorkOrderNumber = "workordernumber",
                Bank = "bank",
                AddressDisplay = "addressdisplay",
                OrderNumber = "123",
                Caption = "caption",
                Date = TestDate,
                IncludeTime = false
            });

            var expected = Resources.large_image_with_exif_data_captionheader_without_time;
            img.ClearExifData();
            expected.ClearExifData();

            Assert.True(img.IsSimilarTo(expected));
        }
    }
}
