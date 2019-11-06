using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace OnScreenOCR
{
    class Config
    {
        public static readonly Font DefaultFont = new Font("Arial", 9);

        public TranslationModel Translation { get; set; } = new TranslationModel();
        public int WindowX { get; set; } = 100;
        public int WindowY { get; set; } = 100;
        public int WindowWidth { get; set; } = 700;
        public int WindowHeight { get; set; } = 500;
        public string Language { get; set; } = "";
        public string Font { get; set; } = (
            new FontConverter().ConvertToInvariantString(DefaultFont));

        public class TranslationModel
        {
            public string OnScreenOCR { get; set; } = "OnScreenOCR";
            public string OCR { get; set; } = "OCR";
            public string ShowText { get; set; } = "Show Text";
            public string HideText { get; set; } = "Hide Text";
            public string Reset { get; set; } = "Reset";
            public string Language { get; set; } = "Language";
            public string Font { get; set; } = "Font";
            public string CopyAll { get; set; } = "Copy All";
        }
    }
}
