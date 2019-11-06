using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Tesseract;

namespace OnScreenOCR
{
    class OCR
    {
        // you could download trained data from
        // https://github.com/tesseract-ocr/tessdata_best
        public static readonly string TesseractDataPath = "./tessdata";
        public static readonly IList<string> Languages = Directory
            .GetFiles(TesseractDataPath, "*.traineddata")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToList();
        // for parse text in multiple rows and columns
        public static readonly PageIteratorLevel OCRPageIteratorLevel = PageIteratorLevel.TextLine;
        public static readonly PageSegMode OCRPageSegMode = PageSegMode.SparseTextOsd;
        public static readonly Regex ReplaceSpaceBetweenNonASCII = new Regex(
            @"(?<=[^\x00-\x7F])\s+(?=[^\x00-\x7F])", RegexOptions.Compiled);

        public static Bitmap TakeScreenshot(Rectangle rect)
        {
            var bitmap = new Bitmap(rect.Width, rect.Height);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(rect.Location, Point.Empty, rect.Size);
            }
            return bitmap;
        }

        private static Rectangle ExtendRectangle(Rectangle rect, int right, int bottom)
        {
            return new Rectangle(rect.X, rect.Y, rect.Width + right, rect.Height + bottom);
        }

        private static Rectangle MergeRectangle(Rectangle a, Rectangle b)
        {
            var x = Math.Min(a.X, b.X);
            var y = Math.Min(a.Y, b.Y);
            var w = Math.Max(a.Right, b.Right) - x;
            var h = Math.Max(a.Bottom, b.Bottom) - y;
            return new Rectangle(x, y, w, h);
        }

        public static IList<Tuple<Rectangle, string>> MergeNearBlocks(
            IList<Tuple<Rectangle, string>> source)
        {
            // merge horizontally
            var sorted = source.OrderBy(t => t.Item1.X).ToList();
            for (int x = 0; x < sorted.Count; ++x)
            {
                var item = sorted[x];
                if (item == null)
                    continue;
                var slot = item.Item1.Height * 2;
                for (int y = x + 1; y < sorted.Count; ++y)
                {
                    var otherItem = sorted[y];
                    if (otherItem == null)
                        continue;
                    if (!ExtendRectangle(item.Item1, slot, 0).IntersectsWith(otherItem.Item1))
                        continue;
                    item = Tuple.Create(
                        MergeRectangle(item.Item1, otherItem.Item1),
                        item.Item2 + otherItem.Item2);
                    sorted[x] = item;
                    sorted[y] = null;
                }
            }
            // merge vertically
            sorted = sorted.Where(t => t != null).OrderBy(t => t.Item1.Y).ToList();
            for (int x = 0; x < sorted.Count; ++x)
            {
                var item = sorted[x];
                if (item == null)
                    continue;
                var slot = item.Item1.Height;
                for (int y = x + 1; y < sorted.Count; ++y)
                {
                    var otherItem = sorted[y];
                    if (otherItem == null)
                        continue;
                    if (!ExtendRectangle(item.Item1, 0, slot).IntersectsWith(otherItem.Item1))
                        continue;
                    item = Tuple.Create(
                        MergeRectangle(item.Item1, otherItem.Item1),
                        item.Item2 + "\r\n" + otherItem.Item2);
                    sorted[x] = item;
                    sorted[y] = null;
                }
            }
            return sorted.Where(t => t != null).ToList();
        }

        public static IList<Tuple<Rectangle, string>> Parse(Image image, string language)
        {
            byte[] imageBytes;
            using (var stream = new MemoryStream())
            {
                image.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                imageBytes = stream.ToArray();
            }
            var result = new List<Tuple<Rectangle, string>>();
            using (var engine = new TesseractEngine(TesseractDataPath, language, EngineMode.Default))
            using (var img = Pix.LoadFromMemory(imageBytes))
            using (var page = engine.Process(img, OCRPageSegMode))
            using (var iter = page.GetIterator())
            {
                do
                {
                    if (!iter.TryGetBoundingBox(OCRPageIteratorLevel, out var bounds))
                        continue;
                    var text = iter.GetText(OCRPageIteratorLevel)?.Trim();
                    if (string.IsNullOrEmpty(text))
                        continue;
                    text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
                    text = ReplaceSpaceBetweenNonASCII.Replace(text, "");
                    var rect = new Rectangle(
                        bounds.X1, bounds.Y1, bounds.Width, bounds.Height);
                    result.Add(Tuple.Create(rect, text));
                } while (iter.Next(OCRPageIteratorLevel));
            }
            var mergedResult = MergeNearBlocks(result);
            return mergedResult;
        }
    }
}
