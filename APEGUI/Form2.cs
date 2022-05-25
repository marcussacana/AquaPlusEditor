using AquaPlusEditor;
using System;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace APEGUI
{
    public partial class Form2 : Form {
        public Form2() {
            InitializeComponent();
        }

        int FontSize => Glyphs.First().Texture.Width;
        Glyph[] Glyphs;
        FNT Font;
        private void button1_Click(object sender, EventArgs e) {
            OpenFileDialog fd = new OpenFileDialog();
            fd.Filter = "All AquaPlus Font Files|*.fnt";
            if (fd.ShowDialog() != DialogResult.OK)
                return;

            Stream Stream = new StreamReader(fd.FileName).BaseStream;
            Font = new FNT(Stream);
            Glyphs = Font.GetGlyphs();
            textBox5.Text = Font.FontSize.ToString();
            textBox4.Text = (FontSize - (FontSize/8)) + ",0";
            PreviewText();
        }

        private void textBox2_TextChanged(object sender, EventArgs e) {
            timer1.Stop();
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e) {
            PreviewText();
            timer1.Stop();
        }

        private void PreviewText() {
            if (textBox2.Text.Length == 0)
                return;

            Bitmap Texture = new Bitmap(FontSize * textBox2.Text.Length, FontSize);
            Graphics g = Graphics.FromImage(Texture);
            for (int i = 0; i < textBox2.Text.Length; i++) {
                char c = textBox2.Text[i];
                Glyph Glyph = (from x in Glyphs where x.Char == c select x).FirstOrDefault();
                if (Glyph.Char == '\x0') {
                    continue;
                }
                int X = i * FontSize;
                g.DrawImageUnscaled(Glyph.Texture, new Point(X, 0));
            }

            g.Dispose();
            pictureBox1.Image = Texture;
        }

        private void button3_Click(object sender, EventArgs e) {
            if (textBox1.Text.Length > Glyphs.Length)
                throw new Exception("Too many glyphs in the list");

            var Font = new Font(textBox3.Text, float.Parse(textBox4.Text), FontStyle.Regular, GraphicsUnit.Pixel);
            int Missed = 1;
            for (int i = 0; i < textBox1.Text.Length; i++) {
                char c = textBox1.Text[i];
                int x = GetGlyphIndex(c);
                if (x == -1) {
                    x = Glyphs.Length - Missed++;
                }
                Glyphs[x].Char = c;
                var Buffer = new Bitmap(FontSize, FontSize, PixelFormat.Format32bppArgb);
                Graphics g = Graphics.FromImage(Buffer);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g.DrawString(c.ToString(), Font, Brushes.White, new Rectangle(0, 0, FontSize, FontSize));
                g.Flush();
                g.Dispose();

                Glyphs[x].Texture = Buffer;
            }

            PreviewText();

        }

        private int GetGlyphIndex(char c) {
            for (int i = 0; i < Glyphs.Length; i++) {
                if (Glyphs[i].Char == c)
                    return i;
            }
            return -1;
        }


        //https://stackoverflow.com/questions/4820212/automatically-trim-a-bitmap-to-minimum-size
        static Bitmap TrimBitmap(Bitmap source) {
            Rectangle srcRect = default(Rectangle);
            BitmapData data = null;
            try {
                data = source.LockBits(new Rectangle(0, 0, source.Width, source.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte[] buffer = new byte[data.Height * data.Stride];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                int xMin = int.MaxValue,
                    xMax = int.MinValue,
                    yMin = int.MaxValue,
                    yMax = int.MinValue;

                bool foundPixel = false;

                // Find xMin
                for (int x = 0; x < data.Width; x++) {
                    bool stop = false;
                    for (int y = 0; y < data.Height; y++) {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0) {
                            xMin = x;
                            stop = true;
                            foundPixel = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Image is empty...
                if (!foundPixel)
                    return null;

                // Find yMin
                for (int y = 0; y < data.Height; y++) {
                    bool stop = false;
                    for (int x = xMin; x < data.Width; x++) {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0) {
                            yMin = y;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Find xMax
                for (int x = data.Width - 1; x >= xMin; x--) {
                    bool stop = false;
                    for (int y = yMin; y < data.Height; y++) {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0) {
                            xMax = x;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                // Find yMax
                for (int y = data.Height - 1; y >= yMin; y--) {
                    bool stop = false;
                    for (int x = xMin; x <= xMax; x++) {
                        byte alpha = buffer[y * data.Stride + 4 * x + 3];
                        if (alpha != 0) {
                            yMax = y;
                            stop = true;
                            break;
                        }
                    }
                    if (stop)
                        break;
                }

                srcRect = Rectangle.FromLTRB(xMin, yMin, xMax, yMax);
            } finally {
                if (data != null)
                    source.UnlockBits(data);
            }

            Bitmap dest = new Bitmap(srcRect.Width, srcRect.Height);
            Rectangle destRect = new Rectangle(0, 0, srcRect.Width, srcRect.Height);
            using (Graphics graphics = Graphics.FromImage(dest)) {
                graphics.DrawImage(source, destRect, srcRect, GraphicsUnit.Pixel);
            }
            return dest;
        }

        private void button2_Click(object sender, EventArgs e) {
            SaveFileDialog fd = new SaveFileDialog();
            fd.Filter = "All AquaPlus Font Files|*.fnt";
            if (fd.ShowDialog() != DialogResult.OK)
                return;

            using (Stream Output = new StreamWriter(fd.FileName).BaseStream) {
                Font.UpdatedGlyphs(Glyphs, Output, true);
            }
            MessageBox.Show("Saved");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            int NewSize = 0;
            if (!int.TryParse(textBox5.Text, out NewSize))
            {
                MessageBox.Show("Invalid Size");
                return;
            }

            int CharsPerLine = (Font.FontWidth / (int)Font.FontSize);
            int TotalLines = (Glyphs.Length / (CharsPerLine - 1));

            Font.Texture.Dispose();

            Font.Texture = new Bitmap(CharsPerLine * NewSize, TotalLines * NewSize);


            for (int i = 0; i < Glyphs.Length; i++)
            {

                int CharX = (i % CharsPerLine) * NewSize;
                int CharY = (i / CharsPerLine) * NewSize;

                Bitmap NewChar = new Bitmap(NewSize, NewSize);
                using (Graphics g = Graphics.FromImage(NewChar))
                {
                    g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                    g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                    g.DrawImage(Glyphs[i].Texture, new Rectangle(Point.Empty, NewChar.Size));
                    g.Flush();
                }

                Glyphs[i].Texture.Dispose();
                Glyphs[i].Texture = NewChar;
                Glyphs[i].RealX = CharX;
                Glyphs[i].RealY = CharY;
            }

            Font.FontSize = (uint)NewSize;
        }
    }
}
            
