using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OnScreenOCR
{
    public partial class MainForm : Form
    {
        const string Version = "1.0";
        const int ToolbarHeight = 32;
        const int LanguageDropdownWidth = 100;
        const int FontButtonWidth = 150;
        const int ViewPadding = 2;
        const string ConfigurationPath = "./config.json";

        Config config;

        Panel toolPanel = new Panel();
        Panel viewWrapperPanel = new Panel();
        Panel viewPanel = new Panel();
        Button ocrButton = new Button();
        Button textSwitchButton = new Button();
        Button resetButton = new Button();
        Label languageLabel = new Label();
        ComboBox languageDropdown = new ComboBox();
        Panel languagePanel = new Panel();
        Label fontLabel = new Label();
        Button fontButton = new Button();
        Button copyAllButton = new Button();

        Timer checkCursorTimer = new Timer();
        bool enableMouseHook = false;

        public MainForm()
        {
            InitializeComponent();
            LoadConfiguration();

            Text = $"{config.Translation.OnScreenOCR} {Version}";
            StartPosition = FormStartPosition.Manual;
            Location = new Point(config.WindowX, config.WindowY);
            Size = new Size(config.WindowWidth, config.WindowHeight);
            Icon = Properties.Resources.icon;
            toolPanel.Height = ToolbarHeight;
            toolPanel.Dock = DockStyle.Top;
            viewWrapperPanel.Dock = DockStyle.Fill;
            viewWrapperPanel.Padding = new Padding(ViewPadding);
            viewPanel.Dock = DockStyle.Fill;
            viewPanel.BackColor = Color.FromArgb(0xab, 0xcd, 0xef);
            Controls.Add(viewWrapperPanel);
            Controls.Add(toolPanel);
            viewWrapperPanel.Controls.Add(viewPanel);

            ocrButton.Text = config.Translation.OCR;
            ocrButton.Dock = DockStyle.Left;
            ocrButton.Click += OnOCRClicked;
            textSwitchButton.Text = config.Translation.HideText;
            textSwitchButton.Dock = DockStyle.Left;
            textSwitchButton.Click += OnTextSwitchClicked;
            resetButton.Text = config.Translation.Reset;
            resetButton.Dock = DockStyle.Left;
            resetButton.Click += OnResetClicked;
            languageLabel.Text = $"{config.Translation.Language}: ";
            languageLabel.AutoSize = true;
            languageLabel.MinimumSize = new Size(0, ToolbarHeight);
            languageLabel.TextAlign = ContentAlignment.MiddleLeft;
            languageLabel.Dock = DockStyle.Left;
            languageDropdown.Items.AddRange(OCR.Languages.ToArray<object>());
            languageDropdown.SelectedItem = (
                OCR.Languages.FirstOrDefault(l => l == config.Language) ??
                OCR.Languages.FirstOrDefault());
            languageDropdown.DropDownStyle = ComboBoxStyle.DropDownList;
            languageDropdown.Size = new Size(LanguageDropdownWidth, ToolbarHeight);
            languageDropdown.Dock = DockStyle.Fill;
            languagePanel.Size = new Size(LanguageDropdownWidth, ToolbarHeight);
            languagePanel.Controls.Add(languageDropdown);
            languagePanel.Padding = new Padding(0, 4, 0, 4);
            languagePanel.Dock = DockStyle.Left;
            fontLabel.Text = $"{config.Translation.Font}: ";
            fontLabel.AutoSize = true;
            fontLabel.MinimumSize = new Size(0, ToolbarHeight);
            fontLabel.TextAlign = ContentAlignment.MiddleLeft;
            fontLabel.Dock = DockStyle.Left;
            fontButton.Text = config.Font;
            fontButton.Size = new Size(FontButtonWidth, ToolbarHeight);
            fontButton.Click += OnFontButtonClicked;
            fontButton.Dock = DockStyle.Left;
            copyAllButton.Text = config.Translation.CopyAll;
            copyAllButton.Click += OnCopyAllClicked;
            copyAllButton.Dock = DockStyle.Left;

            toolPanel.Controls.Add(copyAllButton);
            toolPanel.Controls.Add(fontButton);
            toolPanel.Controls.Add(fontLabel);
            toolPanel.Controls.Add(languagePanel);
            toolPanel.Controls.Add(languageLabel);
            toolPanel.Controls.Add(resetButton);
            toolPanel.Controls.Add(textSwitchButton);
            toolPanel.Controls.Add(ocrButton);

            // use global mouse hook will cause ExecutionEngineException randomly
            checkCursorTimer.Interval = 1;
            checkCursorTimer.Tick += OnCursorTimerTicked;
            checkCursorTimer.Start();
            FormClosing += (s, e) => checkCursorTimer.Stop();
            ResizeEnd += (s, e) => DumpConfiguration();
            Move += (s, e) => DumpConfiguration();
            languageDropdown.SelectedValueChanged += (s, e) => DumpConfiguration();

            OnResetClicked(this, new EventArgs());
        }

        private void LoadConfiguration()
        {
            if (File.Exists(ConfigurationPath))
            {
                var json = File.ReadAllText(ConfigurationPath);
                if (!string.IsNullOrEmpty(json))
                {
                    config = JsonSerializer.Deserialize<Config>(json);
                    return;
                }
            }
            config = new Config();
        }

        private void DumpConfiguration()
        {
            config.WindowX = Location.X;
            config.WindowY = Location.Y;
            config.WindowWidth = Size.Width;
            config.WindowHeight = Size.Height;
            config.Language = languageDropdown.SelectedItem?.ToString();
            config.Font = fontButton.Text;
            var json = JsonSerializer.Serialize(config,
                new JsonSerializerOptions() { WriteIndented = true });
            File.WriteAllText(ConfigurationPath, json);
        }

        private void EnterTransparentArea()
        {
            var style = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
            Native.SetWindowLong(Handle, Native.GWL_EXSTYLE,
                style | Native.WS_EX_TOPMOST | Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT);
            Native.SetLayeredWindowAttributes(Handle, 0x00efcdab, 255, Native.LWA_COLORKEY);
            TopMost = true;
        }

        private void LeaveTransparentArea()
        {
            var style = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
            Native.SetWindowLong(Handle, Native.GWL_EXSTYLE,
                (style | Native.WS_EX_TOPMOST | Native.WS_EX_LAYERED) & ~Native.WS_EX_TRANSPARENT);
            Native.SetLayeredWindowAttributes(Handle, 0x00efcdab, 255, Native.LWA_COLORKEY);
            TopMost = true;
        }

        private void DisableTransparentArea()
        {
            var style = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
            Native.SetWindowLong(Handle, Native.GWL_EXSTYLE,
                style & ~(Native.WS_EX_TOPMOST | Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT));
        }

        private Rectangle GetViewArea()
        {
            var viewPos = viewPanel.PointToScreen(viewPanel.Location);
            viewPos = new Point(viewPos.X - ViewPadding, viewPos.Y - ViewPadding);
            var viewSize = viewPanel.Size;
            return new Rectangle(viewPos, viewSize);
        }

        private void OnCursorTimerTicked(object sender, EventArgs args)
        {
            if (WindowState == FormWindowState.Minimized)
                return;
            if (!enableMouseHook)
                return;
            var cursorPosition = MousePosition;
            int x = cursorPosition.X;
            int y = cursorPosition.Y;
            if (!(x >= Left && x < Left + Width &&
                y >= Top && y < Top + Height))
                return;
            var viewArea = GetViewArea();
            if (x >= viewArea.X && x < viewArea.X + viewArea.Width &&
                y >= viewArea.Y && y < viewArea.Y + viewArea.Height)
            {
                EnterTransparentArea();
            }
            else
            {
                LeaveTransparentArea();
            }
        }

        private void OnResetClicked(object sender, EventArgs e)
        {
            TopMost = true;
            enableMouseHook = true;
            ocrButton.Enabled = true;
            textSwitchButton.Enabled = false;
            textSwitchButton.Text = config.Translation.HideText;
            resetButton.Enabled = false;
            viewWrapperPanel.BackColor = Color.Red;

            foreach (var pictureBox in viewPanel.Controls.OfType<PictureBox>())
            {
                var image = pictureBox.Image;
                pictureBox.Image = null;
                image?.Dispose();
            }
            viewPanel.Controls.Clear();

            EnterTransparentArea();
        }

        private void OnTextSwitchClicked(object sender, EventArgs e)
        {
            bool visible = textSwitchButton.Text == config.Translation.ShowText;
            foreach (var textBox in viewPanel.Controls.OfType<TextBox>())
            {
                textBox.Visible = visible;
            }
            textSwitchButton.Text = (visible ?
                config.Translation.HideText : config.Translation.ShowText);
        }

        private void OnOCRClicked(object sender, EventArgs e)
        {
            TopMost = false;
            enableMouseHook = false;
            ocrButton.Enabled = false;
            textSwitchButton.Enabled = true;
            textSwitchButton.Text = config.Translation.HideText;
            resetButton.Enabled = true;
            viewWrapperPanel.BackColor = Color.LightGreen;

            var viewArea = GetViewArea();
            var screenShot = OCR.TakeScreenshot(viewArea);
            var pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.Image = screenShot;
            viewPanel.Controls.Add(pictureBox);

            DisableTransparentArea();

            var language = languageDropdown.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(language))
            {
                MessageBox.Show(this,
                    "No language selected, please download trained data from\r\n" +
                    "https://github.com/tesseract-ocr/tessdata_best\r\n" +
                    "and move it into folder " + OCR.TesseractDataPath,
                    "Error");
                return;
            }
            var blocks = OCR.Parse(screenShot, language);
            var converter = new FontConverter();
            foreach (var block in blocks)
            {
                var textBox = new TextBox();
                textBox.Location = block.Item1.Location;
                textBox.Text = block.Item2;
                textBox.Multiline = true;
                try
                {
                    textBox.Font = (Font)converter.ConvertFromInvariantString(fontButton.Text);
                }
                catch (NotSupportedException)
                {
                    textBox.Font = Config.DefaultFont;
                }
                textBox.MinimumSize = block.Item1.Size;
                textBox.Size = textBox.PreferredSize;
                textBox.ScrollBars = ScrollBars.Horizontal;
                textBox.BackColor = Color.LightYellow;
                textBox.ForeColor = Color.Black;
                textBox.BorderStyle = BorderStyle.None;
                viewPanel.Controls.Add(textBox);
                textBox.BringToFront();
            }
        }

        private void OnFontButtonClicked(object sender, EventArgs e)
        {
            var converter = new FontConverter();
            var dialog = new FontDialog();
            try
            {
                dialog.Font = (Font)converter.ConvertFromInvariantString(fontButton.Text);
            }
            catch (NotSupportedException)
            {
                dialog.Font = Config.DefaultFont;
            }
            var result = dialog.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                fontButton.Text = converter.ConvertToInvariantString(dialog.Font);
                foreach (var textBox in viewPanel.Controls.OfType<TextBox>())
                {
                    textBox.Font = dialog.Font;
                }
                DumpConfiguration();
            }
        }

        private void OnCopyAllClicked(object sender, EventArgs e)
        {
            var stringBuilder = new StringBuilder();
            foreach (var textBox in viewPanel.Controls.OfType<TextBox>().Reverse())
            {
                stringBuilder.AppendLine(textBox.Text);
            }
            var str = stringBuilder.ToString();
            if (!string.IsNullOrEmpty(str))
            {
                Clipboard.SetDataObject(str, true, 100, 20);
            }
            else
            {
                Clipboard.Clear();
            }
        }
    }
}
