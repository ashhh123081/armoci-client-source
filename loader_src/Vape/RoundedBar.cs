using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Vape
{
    internal class RoundedBar : Control
    {
        private int barValue;

        public int Value
        {
            get
            {
                return this.barValue;
            }
            set
            {
                int clamped = value < 0 ? 0 : (value > 100 ? 100 : value);
                if (clamped != this.barValue)
                {
                    this.barValue = clamped;
                    Invalidate();
                }
            }
        }

        public RoundedBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                    | ControlStyles.UserPaint
                    | ControlStyles.OptimizedDoubleBuffer
                    | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            int width = base.Width;
            int height = base.Height;
            int diameter = height;
            using (GraphicsPath trackPath = RoundedBar.RoundedRect(
                    0, 0, width, height, diameter))
            {
                using (SolidBrush trackBrush = new SolidBrush(
                        Color.FromArgb(21, 21, 21)))
                {
                    graphics.FillPath(trackBrush, trackPath);
                }
            }
            if (this.barValue <= 0)
            {
                return;
            }
            int fillWidth = width * this.barValue / 100;
            if (fillWidth < height)
            {
                fillWidth = height;
            }
            using (GraphicsPath fillPath = RoundedBar.RoundedRect(
                    0, 0, fillWidth, height, diameter))
            {
                using (SolidBrush fillBrush = new SolidBrush(
                        Color.FromArgb(22, 112, 79)))
                {
                    graphics.FillPath(fillBrush, fillPath);
                }
            }
        }

        private static GraphicsPath RoundedRect(int x, int y, int width, int height, int diameter)
        {
            GraphicsPath path = new GraphicsPath();
            int radius = diameter / 2;
            path.AddArc(x, y, diameter, diameter, 180, 90);
            path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
            path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
            path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
